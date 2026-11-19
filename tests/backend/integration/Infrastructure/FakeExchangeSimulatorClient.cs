using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;
using Waterblocks.Api.Services.ExchangeSimulators;

namespace Waterblocks.IntegrationTests.Infrastructure;

/// <summary>
/// An in-memory simulator client for a second exchange type. It shows that a new exchange needs only a new client.
/// </summary>
public sealed class FakeExchangeSimulatorClient : IExchangeSimulatorClient
{
    private readonly object _sync = new();
    private readonly List<FakeSimulatorAccount> _accounts = new();
    private readonly List<ExchangeMirrorEvent> _events = new();

    public FakeExchangeSimulatorClient(
        string exchangeType,
        string displayName,
        IReadOnlyList<string> assetIds,
        bool oneAccountPerWorkspace)
    {
        ExchangeType = exchangeType;
        DisplayName = displayName;
        AssetIds = assetIds;
        OneAccountPerWorkspace = oneAccountPerWorkspace;
    }

    public string ExchangeType { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> AssetIds { get; }
    public bool OneAccountPerWorkspace { get; }
    public bool IsConfigured => true;

    public void AddAccount(string accountId, string accountName, string apiKey, string apiSecret)
    {
        lock (_sync)
        {
            _accounts.Add(new FakeSimulatorAccount(accountId, accountName, apiKey, apiSecret));
        }
    }

    public decimal GetAvailable(string accountName, string assetId)
    {
        lock (_sync)
        {
            return FindAccount(accountName).Balances.GetValueOrDefault(assetId);
        }
    }

    public IReadOnlyList<ExchangeMirrorEvent> GetEvents()
    {
        lock (_sync)
        {
            return _events.ToList();
        }
    }

    public Task<SimulatorAccountIdentity> IdentifyAccountAsync(
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var account = _accounts.FirstOrDefault(item => item.ApiKey == apiKey && item.ApiSecret == apiSecret)
                ?? throw ExchangeOperationException.BadRequest(
                    "INVALID_EXCHANGE_CREDENTIALS",
                    $"The {DisplayName} simulator refused this API key or secret");
            return Task.FromResult(new SimulatorAccountIdentity(account.AccountId, account.AccountName));
        }
    }

    public Task<SimulatorAccountSnapshot?> TryReadAccountAsync(
        string accountName,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var balances = FindAccount(accountName).Balances
                .ToDictionary(balance => balance.Key, balance => new SimulatorBalance(balance.Value, 0m));
            return Task.FromResult<SimulatorAccountSnapshot?>(new SimulatorAccountSnapshot(accountName, balances));
        }
    }

    public Task<MirrorEventOutcome> SendEventAsync(
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_events.Any(item => item.Id == mirrorEvent.Id))
            {
                return Task.FromResult(new MirrorEventOutcome(MirrorEventOutcomeKind.Applied));
            }

            var balances = FindAccount(mirrorEvent.SimulatorAccountName).Balances;
            var available = balances.GetValueOrDefault(mirrorEvent.AssetId);
            if (mirrorEvent.Type == ExchangeMirrorEventType.Remove && available < mirrorEvent.Amount)
            {
                return Task.FromResult(new MirrorEventOutcome(MirrorEventOutcomeKind.Rejected, "the amount is above the available balance"));
            }

            balances[mirrorEvent.AssetId] = mirrorEvent.Type == ExchangeMirrorEventType.Add
                ? available + mirrorEvent.Amount
                : available - mirrorEvent.Amount;
            _events.Add(mirrorEvent);
            return Task.FromResult(new MirrorEventOutcome(MirrorEventOutcomeKind.Applied));
        }
    }

    private FakeSimulatorAccount FindAccount(string accountName)
    {
        return _accounts.Single(item => item.AccountName == accountName);
    }

    private sealed record FakeSimulatorAccount(string AccountId, string AccountName, string ApiKey, string ApiSecret)
    {
        public Dictionary<string, decimal> Balances { get; } = new(StringComparer.Ordinal);
    }
}
