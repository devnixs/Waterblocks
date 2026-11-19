using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services;

public sealed record GateSimulatorBalance(decimal SpotAvailable, decimal SpotLocked);

public sealed record GateSimulatorAccountInfo(
    string Name,
    long UserId,
    IReadOnlyDictionary<string, GateSimulatorBalance> Balances);

public enum GateEventOutcomeKind
{
    Applied,
    Rejected,
    Refused,
    Retry,
}

public sealed record GateEventOutcome(GateEventOutcomeKind Kind, string? Message = null);

public interface IGateSimulatorClient
{
    bool IsConfigured { get; }

    /// <summary>
    /// Signs a Gate API call with the key and secret, and returns the user id of the account.
    /// </summary>
    Task<long> VerifyCredentialsAsync(string apiKey, string apiSecret, CancellationToken cancellationToken = default);

    Task<GateSimulatorAccountInfo?> FindAccountByUserIdAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one account. Returns null when the simulator cannot give it.
    /// </summary>
    Task<GateSimulatorAccountInfo?> TryGetAccountAsync(string accountName, CancellationToken cancellationToken = default);

    Task<GateEventOutcome> SendEventAsync(ExchangeMirrorEvent mirrorEvent, CancellationToken cancellationToken = default);
}

public sealed class GateSimulatorClient : IGateSimulatorClient
{
    public const string BaseUrlSetting = "GATE_SIMULATOR_BASE_URL";
    public const string AdminTokenSetting = "GATE_SIMULATOR_ADMIN_TOKEN";

    private const string SpotFeePath = "/api/v4/spot/fee";
    private const string AccountsPath = "/_api/accounts";
    private const string CollateralEventsPath = "/_api/collateral/events";
    private const string EventSource = "waterblocks";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GateSimulatorClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public bool IsConfigured =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri) &&
        (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps);

    private string BaseUrl => _configuration[BaseUrlSetting]?.Trim().TrimEnd('/') ?? string.Empty;

    private string? AdminToken => _configuration[AdminTokenSetting]?.Trim();

    public async Task<long> VerifyCredentialsAsync(
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw ExchangeOperationException.BadRequest(
                "GATE_SIMULATOR_NOT_CONFIGURED",
                "The Gate simulator URL is not configured");
        }

        if (!IsHeaderSafe(apiKey))
        {
            throw InvalidCredentials();
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + SpotFeePath);
        request.Headers.TryAddWithoutValidation("KEY", apiKey);
        request.Headers.TryAddWithoutValidation("Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("SIGN", Sign(apiSecret, timestamp));

        using var response = await SendAsync(request, cancellationToken) ?? throw Unreachable();
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw InvalidCredentials();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw Unreachable();
        }

        using var document = await ReadJsonAsync(response, cancellationToken) ?? throw Unreachable();
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("user_id", out var userId) &&
            userId.TryGetInt64(out var value))
        {
            return value;
        }

        throw Unreachable();
    }

    public async Task<GateSimulatorAccountInfo?> FindAccountByUserIdAsync(
        long userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw Unreachable();
        }

        using var request = CreateAdminRequest(HttpMethod.Get, AccountsPath);
        using var response = await SendAsync(request, cancellationToken) ?? throw Unreachable();
        if (!response.IsSuccessStatusCode)
        {
            throw Unreachable();
        }

        using var document = await ReadJsonAsync(response, cancellationToken) ?? throw Unreachable();
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw Unreachable();
        }

        return document.RootElement
            .EnumerateArray()
            .Select(ParseAccount)
            .FirstOrDefault(account => account?.UserId == userId);
    }

    public async Task<GateSimulatorAccountInfo?> TryGetAccountAsync(
        string accountName,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = CreateAdminRequest(HttpMethod.Get, $"{AccountsPath}/{Uri.EscapeDataString(accountName)}");
        using var response = await SendAsync(request, cancellationToken);
        if (response is not { IsSuccessStatusCode: true })
        {
            return null;
        }

        using var document = await ReadJsonAsync(response, cancellationToken);
        return document == null ? null : ParseAccount(document.RootElement);
    }

    public async Task<GateEventOutcome> SendEventAsync(
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new GateEventOutcome(GateEventOutcomeKind.Retry, "The Gate simulator URL is not configured");
        }

        using var request = CreateAdminRequest(HttpMethod.Post, CollateralEventsPath);
        request.Content = JsonContent.Create(new Dictionary<string, string>
        {
            ["event_id"] = mirrorEvent.Id,
            ["account"] = mirrorEvent.SimulatorAccount,
            ["type"] = mirrorEvent.Type,
            ["asset"] = mirrorEvent.AssetId,
            ["amount"] = AmountText.Format(mirrorEvent.Amount),
            ["source"] = EventSource,
        });

        using var response = await SendAsync(request, cancellationToken);
        if (response == null)
        {
            return new GateEventOutcome(GateEventOutcomeKind.Retry, "The Gate simulator cannot be reached");
        }

        using var document = await ReadJsonAsync(response, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return ParseEventReply(document);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var error = ReadString(document, "error") ?? $"HTTP {(int)response.StatusCode}";
            return new GateEventOutcome(GateEventOutcomeKind.Refused, error);
        }

        return new GateEventOutcome(GateEventOutcomeKind.Retry, $"HTTP {(int)response.StatusCode}");
    }

    private static GateEventOutcome ParseEventReply(JsonDocument? document)
    {
        return ReadString(document, "status") switch
        {
            "applied" => new GateEventOutcome(GateEventOutcomeKind.Applied),
            "rejected" => new GateEventOutcome(
                GateEventOutcomeKind.Rejected,
                ReadString(document, "reason") ?? "The exchange rejected the removal"),
            _ => new GateEventOutcome(GateEventOutcomeKind.Retry, "The Gate simulator sent an unknown event status"),
        };
    }

    private HttpRequestMessage CreateAdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, BaseUrl + path);
        if (!string.IsNullOrEmpty(AdminToken))
        {
            request.Headers.TryAddWithoutValidation("X-Admin-Token", AdminToken);
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

    private static GateSimulatorAccountInfo? ParseAccount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("user_id", out var userId) ||
            !userId.TryGetInt64(out var userIdValue))
        {
            return null;
        }

        var balances = new Dictionary<string, GateSimulatorBalance>(StringComparer.Ordinal);
        if (element.TryGetProperty("balances", out var balanceElement) && balanceElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var balance in balanceElement.EnumerateObject())
            {
                balances[balance.Name] = new GateSimulatorBalance(
                    ReadDecimal(balance.Value, "spot_available"),
                    ReadDecimal(balance.Value, "spot_locked"));
            }
        }

        return new GateSimulatorAccountInfo(name.GetString()!, userIdValue, balances);
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

    private static string? ReadString(JsonDocument? document, string propertyName)
    {
        if (document?.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static string Sign(string apiSecret, string timestamp)
    {
        var bodyHash = Convert.ToHexString(SHA512.HashData(Array.Empty<byte>())).ToLowerInvariant();
        var payload = $"GET\n{SpotFeePath}\n\n{bodyHash}\n{timestamp}";
        var signature = HMACSHA512.HashData(Encoding.UTF8.GetBytes(apiSecret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(signature).ToLowerInvariant();
    }

    private static bool IsHeaderSafe(string value)
    {
        return value.All(character => character is > ' ' and < (char)127);
    }

    private static ExchangeOperationException InvalidCredentials()
    {
        return ExchangeOperationException.BadRequest(
            "INVALID_EXCHANGE_CREDENTIALS",
            "The Gate simulator refused this API key or secret");
    }

    private static ExchangeOperationException Unreachable()
    {
        return new ExchangeOperationException(
            HttpStatusCode.BadGateway,
            "GATE_SIMULATOR_UNREACHABLE",
            "The Gate simulator cannot be reached");
    }
}
