using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The client of the Gate simulator. It serves the GATEIO exchange type.
/// </summary>
public sealed class GateSimulatorClient : ExchangeSimulatorClientBase
{
    private const string SpotFeePath = "/api/v4/spot/fee";
    private const string AccountsPath = "/_api/accounts";
    private const string CollateralEventsPath = "/_api/collateral/events";
    private const string EventSource = "waterblocks";

    /// <summary>
    /// The Gate currency of each Waterblocks asset that a connected account can hold.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> GateCurrencies = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BTC"] = "BTC",
        ["USDT_ERC20"] = "USDT",
    };

    private static readonly IReadOnlyList<string> CollateralAssetIds = new[] { "BTC", "USDT_ERC20" };

    public GateSimulatorClient(HttpClient httpClient, IConfiguration configuration)
        : base(httpClient, configuration)
    {
    }

    public override string ExchangeType => "GATEIO";

    public override string DisplayName => "Gate.io";

    public override IReadOnlyList<string> AssetIds => CollateralAssetIds;

    public override bool OneAccountPerWorkspace => true;

    /// <summary>
    /// Signs a Gate API call with the key and secret to get the user id, and then finds the account of that user.
    /// </summary>
    public override async Task<SimulatorAccountIdentity> IdentifyAccountAsync(
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw NotConfigured();
        }

        var userId = await ReadUserIdAsync(apiKey, apiSecret, cancellationToken);
        var account = await FindAccountByUserIdAsync(userId, cancellationToken) ?? throw AccountNotFound();
        return new SimulatorAccountIdentity(userId.ToString(CultureInfo.InvariantCulture), account.AccountName);
    }

    public override async Task<SimulatorAccountSnapshot?> TryReadAccountAsync(
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
        return document == null ? null : ParseAccount(document.RootElement)?.Snapshot;
    }

    public override async Task<MirrorEventOutcome> SendEventAsync(
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return NotConfiguredOutcome();
        }

        if (!GateCurrencies.TryGetValue(mirrorEvent.AssetId, out var currency))
        {
            return new MirrorEventOutcome(
                MirrorEventOutcomeKind.Refused,
                $"Gate.io has no currency for the Waterblocks asset {mirrorEvent.AssetId}");
        }

        using var request = CreateAdminRequest(HttpMethod.Post, CollateralEventsPath);
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
            return UnreachableOutcome();
        }

        using var document = await ReadJsonAsync(response, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return ParseEventReply(document);
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
        {
            var error = ReadString(document, "error") ?? $"HTTP {(int)response.StatusCode}";
            return new MirrorEventOutcome(MirrorEventOutcomeKind.Refused, error);
        }

        return new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, $"HTTP {(int)response.StatusCode}");
    }

    private async Task<long> ReadUserIdAsync(string apiKey, string apiSecret, CancellationToken cancellationToken)
    {
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

    private async Task<GateAccount?> FindAccountByUserIdAsync(long userId, CancellationToken cancellationToken)
    {
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

    private static MirrorEventOutcome ParseEventReply(JsonDocument? document)
    {
        return ReadString(document, "status") switch
        {
            "applied" => new MirrorEventOutcome(MirrorEventOutcomeKind.Applied),
            "rejected" => new MirrorEventOutcome(
                MirrorEventOutcomeKind.Rejected,
                ReadString(document, "reason") ?? "The exchange rejected the removal"),
            _ => new MirrorEventOutcome(MirrorEventOutcomeKind.Retry, "The Gate simulator sent an unknown event status"),
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

    private static GateAccount? ParseAccount(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("name", out var name) ||
            name.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("user_id", out var userId) ||
            !userId.TryGetInt64(out var userIdValue))
        {
            return null;
        }

        // The snapshot uses Waterblocks asset ids, so that callers do not know the Gate currency names.
        var balances = new Dictionary<string, SimulatorBalance>(StringComparer.Ordinal);
        if (element.TryGetProperty("balances", out var balanceElement) && balanceElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var (assetId, currency) in GateCurrencies)
            {
                if (balanceElement.TryGetProperty(currency, out var balance))
                {
                    balances[assetId] = new SimulatorBalance(
                        ReadDecimal(balance, "spot_available"),
                        ReadDecimal(balance, "spot_locked"));
                }
            }
        }

        return new GateAccount(userIdValue, new SimulatorAccountSnapshot(name.GetString()!, balances));
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

    private sealed record GateAccount(long UserId, SimulatorAccountSnapshot Snapshot)
    {
        public string AccountName => Snapshot.AccountName;
    }
}
