using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// A test double for the Gate simulator. It answers the calls that Waterblocks makes.
/// </summary>
public sealed class FakeGateSimulator : HttpMessageHandler
{
    public const string BaseUrl = "http://gate-simulator.test";

    private readonly object _sync = new();
    private readonly Dictionary<string, FakeGateAccount> _accounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FakeGateEvent> _events = new(StringComparer.Ordinal);
    private int _eventRequests;

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

    public void AddAccount(string name, long userId, string apiKey, string apiSecret)
    {
        lock (_sync)
        {
            _accounts[name] = new FakeGateAccount(name, userId, apiKey, apiSecret);
        }
    }

    public void SetBalance(string accountName, string assetId, decimal spotAvailable, decimal spotLocked = 0m)
    {
        lock (_sync)
        {
            _accounts[accountName].Balances[assetId] = new FakeGateBalance(spotAvailable, spotLocked);
        }
    }

    public decimal GetSpotAvailable(string accountName, string assetId)
    {
        lock (_sync)
        {
            return _accounts[accountName].Balances.TryGetValue(assetId, out var balance)
                ? balance.SpotAvailable
                : 0m;
        }
    }

    public FakeGateEvent? FindEvent(string eventId)
    {
        lock (_sync)
        {
            return _events.GetValueOrDefault(eventId);
        }
    }

    public IReadOnlyList<FakeGateEvent> GetEvents()
    {
        lock (_sync)
        {
            return _events.Values.ToList();
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (IsOffline)
        {
            throw new HttpRequestException("The fake Gate simulator is offline");
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
            return Error(HttpStatusCode.Unauthorized, "the admin token is missing or wrong");
        }

        if (request.Method == HttpMethod.Get && path == "/api/v4/spot/fee")
        {
            return ReadSpotFee(request);
        }

        if (request.Method == HttpMethod.Get && path == "/_api/accounts")
        {
            return ListAccounts();
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

    protected override void Dispose(bool disposing)
    {
        // The test owns this object, so the HTTP client factory must not end it.
    }

    private HttpResponseMessage ReadSpotFee(HttpRequestMessage request)
    {
        var key = HeaderValue(request, "KEY");
        var timestamp = HeaderValue(request, "Timestamp");
        var signature = HeaderValue(request, "SIGN");
        lock (_sync)
        {
            var account = _accounts.Values.FirstOrDefault(item => item.ApiKey == key);
            if (account == null || signature != Sign(account.ApiSecret, timestamp))
            {
                return Error(HttpStatusCode.Unauthorized, "invalid key or signature");
            }

            return Json(HttpStatusCode.OK, new Dictionary<string, object> { ["user_id"] = account.UserId });
        }
    }

    private HttpResponseMessage ListAccounts()
    {
        lock (_sync)
        {
            return Json(HttpStatusCode.OK, _accounts.Values.Select(ToAccountReply).ToList());
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
            throw new HttpRequestException("The reply of the fake Gate simulator was lost");
        }

        return reply;
    }

    private HttpResponseMessage ApplyEvent(string eventId, string accountName, string type, string assetId, decimal amount)
    {
        lock (_sync)
        {
            if (!_accounts.TryGetValue(accountName, out var account))
            {
                return Error(HttpStatusCode.NotFound, "account not found");
            }

            if (_events.TryGetValue(eventId, out var existing))
            {
                return existing.Type == type && existing.AssetId == assetId && existing.Amount == amount
                    ? Json(HttpStatusCode.OK, ToEventReply(existing))
                    : Error(HttpStatusCode.Conflict, "event exists with different data");
            }

            var balance = account.Balances.GetValueOrDefault(assetId) ?? new FakeGateBalance(0m, 0m);
            var status = "applied";
            if (type == "add")
            {
                account.Balances[assetId] = balance with { SpotAvailable = balance.SpotAvailable + amount };
            }
            else if (balance.SpotAvailable < amount)
            {
                status = "rejected";
            }
            else
            {
                account.Balances[assetId] = balance with { SpotAvailable = balance.SpotAvailable - amount };
            }

            var created = new FakeGateEvent(eventId, accountName, type, assetId, amount, status);
            _events[eventId] = created;
            return Json(HttpStatusCode.Created, ToEventReply(created));
        }
    }

    private static object ToAccountReply(FakeGateAccount account)
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

    private static Dictionary<string, string> ToEventReply(FakeGateEvent item)
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

    private static string Sign(string secret, string timestamp)
    {
        var bodyHash = Convert.ToHexString(SHA512.HashData(Array.Empty<byte>())).ToLowerInvariant();
        var payload = $"GET\n/api/v4/spot/fee\n\n{bodyHash}\n{timestamp}";
        return Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object body)
    {
        return new HttpResponseMessage(status) { Content = JsonContent.Create(body) };
    }

    private static HttpResponseMessage Error(HttpStatusCode status, string message)
    {
        return Json(status, new Dictionary<string, string> { ["error"] = message });
    }

    private sealed class FakeGateAccount
    {
        public FakeGateAccount(string name, long userId, string apiKey, string apiSecret)
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
        public Dictionary<string, FakeGateBalance> Balances { get; } = new(StringComparer.Ordinal);
    }

    private sealed record FakeGateBalance(decimal SpotAvailable, decimal SpotLocked);
}

public sealed record FakeGateEvent(
    string EventId,
    string Account,
    string Type,
    string AssetId,
    decimal Amount,
    string Status);
