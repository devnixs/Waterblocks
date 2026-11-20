using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// A test double for one exchange simulator. It serves the common simulator admin API that Waterblocks calls:
/// POST /_api/keys/verify, GET /_api/accounts/{name} and POST /_api/collateral/events. Balances use exchange currencies.
/// </summary>
public sealed class FakeExchangeSimulator
{
    private static readonly TimeSpan MaxProofAge = TimeSpan.FromSeconds(60);

    private readonly object _sync = new();
    private readonly Dictionary<string, FakeSimulatorAccount> _accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeSimulatorEvent> _events = new(StringComparer.Ordinal);
    private int _eventRequests;
    private int _verifyRequests;

    public FakeExchangeSimulator(string baseUrl)
    {
        BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    /// <summary>
    /// When true, every call fails with a network error.
    /// </summary>
    public bool IsOffline { get; set; }

    /// <summary>
    /// When set, every call answers with this status.
    /// </summary>
    public HttpStatusCode? ForcedStatus { get; set; }

    /// <summary>
    /// When set, event calls answer with this status and change nothing.
    /// </summary>
    public HttpStatusCode? ForcedEventStatus { get; set; }

    /// <summary>
    /// When true, account reads fail with HTTP 500 and other calls still work.
    /// </summary>
    public bool FailAccountReads { get; set; }

    /// <summary>
    /// When set, every /_api call must send this value in the X-Admin-Token header.
    /// </summary>
    public string? RequiredAdminToken { get; set; }

    /// <summary>
    /// When true, the next event is applied and then its reply is lost.
    /// </summary>
    public bool LoseNextEventReply { get; set; }

    /// <summary>
    /// When set, event calls wait for this task after they apply the event.
    /// </summary>
    public Task? HoldEventReplies { get; set; }

    public int EventRequests => Volatile.Read(ref _eventRequests);

    public int VerifyRequests => Volatile.Read(ref _verifyRequests);

    public void AddAccount(string name, long userId, string apiKey, string apiSecret)
    {
        lock (_sync)
        {
            _accounts[name] = new FakeSimulatorAccount(name, userId, apiKey, apiSecret);
        }
    }

    public void SetBalance(string accountName, string currency, decimal spotAvailable, decimal spotLocked = 0m)
    {
        lock (_sync)
        {
            _accounts[accountName].Balances[currency] = new FakeSimulatorBalance(spotAvailable, spotLocked);
        }
    }

    public decimal GetSpotAvailable(string accountName, string currency)
    {
        lock (_sync)
        {
            return _accounts[accountName].Balances.TryGetValue(currency, out var balance) ? balance.SpotAvailable : 0m;
        }
    }

    public FakeSimulatorEvent? FindEvent(string eventId)
    {
        lock (_sync)
        {
            return _events.GetValueOrDefault(eventId);
        }
    }

    public IReadOnlyList<FakeSimulatorEvent> GetEvents()
    {
        lock (_sync)
        {
            return _events.Values.ToList();
        }
    }

    public async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsOffline)
        {
            throw new HttpRequestException("The fake simulator is offline");
        }

        if (ForcedStatus != null)
        {
            return Error(ForcedStatus.Value, "forced status");
        }

        var path = request.RequestUri!.AbsolutePath;
        if (RequiredAdminToken != null &&
            path.StartsWith("/_api/", StringComparison.Ordinal) &&
            HeaderValue(request, "X-Admin-Token") != RequiredAdminToken)
        {
            return Error(HttpStatusCode.Unauthorized, "the X-Admin-Token header is missing or wrong");
        }

        if (request.Method == HttpMethod.Post && path == "/_api/keys/verify")
        {
            return await VerifyKeyAsync(request, cancellationToken);
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/_api/accounts/", StringComparison.Ordinal))
        {
            return FailAccountReads
                ? Error(HttpStatusCode.InternalServerError, "account read failed")
                : ReadAccount(Uri.UnescapeDataString(path["/_api/accounts/".Length..]));
        }

        if (request.Method == HttpMethod.Post && path == "/_api/collateral/events")
        {
            return await ApplyEventAsync(request, cancellationToken);
        }

        return Error(HttpStatusCode.NotFound, "not found");
    }

    private async Task<HttpResponseMessage> VerifyKeyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _verifyRequests);
        using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var body = document.RootElement;
        var key = body.GetProperty("key").GetString()!;
        var timestamp = body.GetProperty("timestamp").GetString()!;
        var signature = body.GetProperty("signature").GetString()!;
        if (body.TryGetProperty("secret", out _) || body.TryGetProperty("apiSecret", out _))
        {
            return Error(HttpStatusCode.BadRequest, "a key check must never send the secret");
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, CultureInfo.InvariantCulture));
        lock (_sync)
        {
            var account = _accounts.Values.FirstOrDefault(item => item.ApiKey == key);
            if (account == null || age.Duration() > MaxProofAge || signature != Proof(account.ApiSecret, key, timestamp))
            {
                return Error(HttpStatusCode.UnprocessableEntity, "the key or its proof is not valid");
            }

            return Json(HttpStatusCode.OK, new Dictionary<string, object> { ["name"] = account.Name, ["user_id"] = account.UserId });
        }
    }

    private HttpResponseMessage ReadAccount(string accountName)
    {
        lock (_sync)
        {
            return _accounts.TryGetValue(accountName, out var account)
                ? Json(HttpStatusCode.OK, ToAccountReply(account))
                : Error(HttpStatusCode.NotFound, "account not found");
        }
    }

    private async Task<HttpResponseMessage> ApplyEventAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _eventRequests);
        if (ForcedEventStatus != null)
        {
            return Error(ForcedEventStatus.Value, "the fake simulator refused the event");
        }

        using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var body = document.RootElement;
        var reply = ApplyEvent(
            body.GetProperty("event_id").GetString()!,
            body.GetProperty("account").GetString()!,
            body.GetProperty("type").GetString()!,
            body.GetProperty("asset").GetString()!,
            decimal.Parse(body.GetProperty("amount").GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture));

        if (HoldEventReplies != null)
        {
            await HoldEventReplies.WaitAsync(cancellationToken);
        }

        if (LoseNextEventReply)
        {
            LoseNextEventReply = false;
            throw new HttpRequestException("The reply of the fake simulator was lost");
        }

        return reply;
    }

    private HttpResponseMessage ApplyEvent(string eventId, string accountName, string type, string currency, decimal amount)
    {
        lock (_sync)
        {
            if (!_accounts.TryGetValue(accountName, out var account))
            {
                return Error(HttpStatusCode.NotFound, "account not found");
            }

            if (_events.TryGetValue(eventId, out var existing))
            {
                return existing.Type == type && existing.AssetId == currency && existing.Amount == amount
                    ? Json(HttpStatusCode.OK, ToEventReply(existing))
                    : Error(HttpStatusCode.Conflict, "event exists with different data");
            }

            var balance = account.Balances.GetValueOrDefault(currency) ?? new FakeSimulatorBalance(0m, 0m);
            var status = "applied";
            if (type == "add")
            {
                account.Balances[currency] = balance with { SpotAvailable = balance.SpotAvailable + amount };
            }
            else if (balance.SpotAvailable < amount)
            {
                status = "rejected";
            }
            else
            {
                account.Balances[currency] = balance with { SpotAvailable = balance.SpotAvailable - amount };
            }

            var created = new FakeSimulatorEvent(eventId, accountName, type, currency, amount, status);
            _events[eventId] = created;
            return Json(HttpStatusCode.Created, ToEventReply(created));
        }
    }

    private static string Proof(string secret, string key, string timestamp)
    {
        var proof = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"KEY_VERIFY\n{key}\n{timestamp}"));
        return Convert.ToHexString(proof).ToLowerInvariant();
    }

    private static object ToAccountReply(FakeSimulatorAccount account)
    {
        return new Dictionary<string, object>
        {
            ["name"] = account.Name,
            ["user_id"] = account.UserId,
            ["balances"] = account.Balances.ToDictionary(
                balance => balance.Key,
                balance => new Dictionary<string, string>
                {
                    ["spot_available"] = balance.Value.SpotAvailable.ToString(CultureInfo.InvariantCulture),
                    ["spot_locked"] = balance.Value.SpotLocked.ToString(CultureInfo.InvariantCulture),
                }),
        };
    }

    private static Dictionary<string, string> ToEventReply(FakeSimulatorEvent item)
    {
        var reply = new Dictionary<string, string>
        {
            ["event_id"] = item.EventId,
            ["status"] = item.Status,
        };
        if (item.Status == "rejected")
        {
            reply["reason"] = "the amount is above the available balance";
        }

        return reply;
    }

    private static string HeaderValue(HttpRequestMessage request, string name)
    {
        return request.Headers.TryGetValues(name, out var values) ? values.First() : string.Empty;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
    {
        return new HttpResponseMessage(status) { Content = JsonContent.Create(body) };
    }

    private static HttpResponseMessage Error(HttpStatusCode status, string message)
    {
        return Json(status, new Dictionary<string, string> { ["error"] = message });
    }

    private sealed class FakeSimulatorAccount
    {
        public FakeSimulatorAccount(string name, long userId, string apiKey, string apiSecret)
        {
            Name = name;
            UserId = userId;
            ApiKey = apiKey;
            ApiSecret = apiSecret;
        }

        public string Name { get; }
        public long UserId { get; }
        public string ApiKey { get; }
        public string ApiSecret { get; }
        public Dictionary<string, FakeSimulatorBalance> Balances { get; } = new(StringComparer.Ordinal);
    }

    private sealed record FakeSimulatorBalance(decimal SpotAvailable, decimal SpotLocked);
}

/// <summary>
/// One collateral event that a fake simulator applied. AssetId is the exchange currency that Waterblocks sent.
/// </summary>
public sealed record FakeSimulatorEvent(
    string EventId,
    string Account,
    string Type,
    string AssetId,
    decimal Amount,
    string Status);

/// <summary>
/// Routes each request to the fake simulator of its host, so that several exchanges can run in one test.
/// </summary>
public sealed class FakeSimulatorNetwork : HttpMessageHandler
{
    private readonly Dictionary<string, FakeExchangeSimulator> _simulators = new(StringComparer.OrdinalIgnoreCase);

    public FakeExchangeSimulator Add(FakeExchangeSimulator simulator)
    {
        lock (_simulators)
        {
            _simulators[new Uri(simulator.BaseUrl).Host] = simulator;
        }

        return simulator;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        FakeExchangeSimulator? simulator;
        lock (_simulators)
        {
            simulator = _simulators.GetValueOrDefault(request.RequestUri!.Host);
        }

        return simulator == null
            ? throw new HttpRequestException($"No fake simulator listens on {request.RequestUri!.Host}")
            : simulator.HandleAsync(request, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        // The test owns this object, so the HTTP client factory must not end it.
    }
}
