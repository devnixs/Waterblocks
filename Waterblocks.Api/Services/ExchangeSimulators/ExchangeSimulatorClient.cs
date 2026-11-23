using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The client of the common simulator admin API: POST /_api/keys/verify, GET /_api/accounts/{name} and
/// POST /_api/collateral/events. Every exchange simulator that Waterblocks connects to serves these calls.
/// </summary>
public sealed class ExchangeSimulatorClient : IExchangeSimulatorClient
{
    private const string KeyVerifyPath = "/_api/keys/verify";
    private const string AccountsPath = "/_api/accounts";
    private const string CollateralEventsPath = "/_api/collateral/events";
    private const string AdminTokenHeader = "X-Admin-Token";
    private const string EventSource = "waterblocks";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public ExchangeSimulatorClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    /// <summary>
    /// Returns the key proof: the lowercase hex HMAC-SHA256 of "KEY_VERIFY\n&lt;key&gt;\n&lt;timestamp&gt;", keyed with the secret.
    /// </summary>
    public static string SignKeyProof(string secret, string key, string timestamp)
    {
        var proof = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"KEY_VERIFY\n{key}\n{timestamp}"));
        return Convert.ToHexString(proof).ToLowerInvariant();
    }

    public async Task<SimulatorAccountIdentity> VerifyKeyAsync(
        Exchange exchange,
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default)
    {
        if (!exchange.IsConnectable)
        {
            throw ExchangeCatalog.NotConfigured(exchange);
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var request = CreateRequest(exchange, HttpMethod.Post, KeyVerifyPath);
        request.Content = JsonContent.Create(new Dictionary<string, string>
        {
            ["key"] = apiKey,
            ["timestamp"] = timestamp,
            ["signature"] = SignKeyProof(apiSecret, apiKey, timestamp),
        });

        using var response = await SendAsync(request, cancellationToken) ?? throw Unreachable(exchange);
        if (response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest)
        {
            throw ExchangeOperationException.BadRequest(
                "INVALID_EXCHANGE_CREDENTIALS",
                $"The {exchange.Name} simulator refused this API key or secret");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ExchangeOperationException(
                HttpStatusCode.BadGateway,
                "EXCHANGE_SIMULATOR_UNAUTHORIZED",
                $"The {exchange.Name} simulator refused the admin token. Check {ExchangeSimulatorSettings.Section}__{exchange.ExchangeType}__{ExchangeSimulatorSettings.AdminTokenName}");
        }

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            throw new ExchangeOperationException(
                HttpStatusCode.BadGateway,
                "EXCHANGE_SIMULATOR_UNSUPPORTED",
                $"The {exchange.Name} simulator has no key check (POST {KeyVerifyPath}). Check the simulator URL or update the simulator");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw Unreachable(exchange);
        }

        using var document = await ReadJsonAsync(response, cancellationToken) ?? throw Unreachable(exchange);
        var name = ReadString(document.RootElement, "name");
        var accountId = ReadAccountId(document.RootElement);
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(accountId))
        {
            throw Unreachable(exchange);
        }

        return new SimulatorAccountIdentity(accountId, name);
    }

    public async Task<SimulatorAccountSnapshot?> TryReadAccountAsync(
        Exchange exchange,
        string accountName,
        CancellationToken cancellationToken = default)
    {
        if (!exchange.IsConnectable)
        {
            return null;
        }

        using var request = CreateRequest(exchange, HttpMethod.Get, $"{AccountsPath}/{Uri.EscapeDataString(accountName)}");
        using var response = await SendAsync(request, cancellationToken);
        if (response is not { IsSuccessStatusCode: true })
        {
            return null;
        }

        using var document = await ReadJsonAsync(response, cancellationToken);
        if (document == null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var balances = new Dictionary<string, SimulatorBalance>(StringComparer.Ordinal);
        if (document.RootElement.TryGetProperty("balances", out var balanceElement) &&
            balanceElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var currency in exchange.Currencies)
            {
                if (balanceElement.TryGetProperty(currency.Currency, out var balance))
                {
                    balances[currency.AssetId] = new SimulatorBalance(
                        ReadDecimal(balance, "spot_available"),
                        ReadDecimal(balance, "spot_locked"));
                }
            }
        }

        return new SimulatorAccountSnapshot(accountName, balances);
    }

    public async Task<MirrorEventOutcome> SendEventAsync(
        Exchange exchange,
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default)
    {
        if (!exchange.IsConnectable)
        {
            return new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, $"The {exchange.Name} simulator URL is not configured");
        }

        var currency = exchange.Currencies.FirstOrDefault(item => item.AssetId == mirrorEvent.AssetId)?.Currency;
        if (currency == null)
        {
            return new MirrorEventOutcome(
                MirrorEventOutcomeKind.Refused,
                $"{exchange.Name} has no currency for the Waterblocks asset {mirrorEvent.AssetId}");
        }

        using var request = CreateRequest(exchange, HttpMethod.Post, CollateralEventsPath);
        request.Content = JsonContent.Create(new Dictionary<string, string>
        {
            ["event_id"] = mirrorEvent.Id,
            ["account"] = mirrorEvent.SimulatorAccountName,
            ["type"] = mirrorEvent.Type,
            ["asset"] = currency,
            ["amount"] = AmountText.Format(mirrorEvent.Amount),
            ["source"] = EventSource,
        });

        using var response = await SendAsync(request, cancellationToken);
        if (response == null)
        {
            return new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, $"The {exchange.Name} simulator cannot be reached");
        }

        using var document = await ReadJsonAsync(response, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return ReadEventReply(exchange, document);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var error = document == null ? null : ReadString(document.RootElement, "error");
            return new MirrorEventOutcome(MirrorEventOutcomeKind.Refused, error ?? $"HTTP {(int)response.StatusCode}");
        }

        return new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, $"HTTP {(int)response.StatusCode}");
    }

    private static MirrorEventOutcome ReadEventReply(Exchange exchange, JsonDocument? document)
    {
        var root = document?.RootElement;
        return (root == null ? null : ReadString(root.Value, "status")) switch
        {
            "applied" => new MirrorEventOutcome(MirrorEventOutcomeKind.Applied),
            "rejected" => new MirrorEventOutcome(
                MirrorEventOutcomeKind.Rejected,
                ReadString(root!.Value, "reason") ?? "The exchange rejected the removal"),
            _ => new MirrorEventOutcome(
                MirrorEventOutcomeKind.Retry,
                $"The {exchange.Name} simulator sent an unknown event status"),
        };
    }

    private HttpRequestMessage CreateRequest(Exchange exchange, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, exchange.SimulatorBaseUrl!.Trim().TrimEnd('/') + path);
        var adminToken = _configuration[ExchangeSimulatorSettings.AdminTokenKey(exchange.ExchangeType)]?.Trim();
        if (!string.IsNullOrEmpty(adminToken))
        {
            request.Headers.TryAddWithoutValidation(AdminTokenHeader, adminToken);
        }

        return request;
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<JsonDocument?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? ReadAccountId(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("user_id", out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => value.GetString(),
            _ => null,
        };
    }

    private static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static ExchangeOperationException Unreachable(Exchange exchange)
    {
        return new ExchangeOperationException(
            HttpStatusCode.BadGateway,
            "EXCHANGE_SIMULATOR_UNREACHABLE",
            $"The {exchange.Name} simulator cannot be reached");
    }
}
