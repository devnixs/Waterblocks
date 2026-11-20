using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The account that an API key opens in an exchange simulator.
/// </summary>
public sealed record SimulatorAccountIdentity(string AccountId, string AccountName);

public sealed record SimulatorBalance(decimal Available, decimal Locked);

/// <summary>
/// The balances of one simulator account, keyed by Waterblocks asset id.
/// </summary>
public sealed record SimulatorAccountSnapshot(
    string AccountName,
    IReadOnlyDictionary<string, SimulatorBalance> Balances);

public enum MirrorEventOutcomeKind
{
    Applied,
    Rejected,
    Refused,
    Retry,
}

public sealed record MirrorEventOutcome(MirrorEventOutcomeKind Kind, string? Message = null);

/// <summary>
/// Talks to the simulator of any exchange through the common simulator admin API. The exchange catalog row gives
/// the URL, the name and the currency names, so that no exchange needs its own code.
/// </summary>
public interface IExchangeSimulatorClient
{
    /// <summary>
    /// Finds the simulator account of an API key. The secret signs a proof and never leaves Waterblocks.
    /// </summary>
    Task<SimulatorAccountIdentity> VerifyKeyAsync(
        Exchange exchange,
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the balances of one account. Returns null when the simulator cannot give them.
    /// </summary>
    Task<SimulatorAccountSnapshot?> TryReadAccountAsync(
        Exchange exchange,
        string accountName,
        CancellationToken cancellationToken = default);

    Task<MirrorEventOutcome> SendEventAsync(
        Exchange exchange,
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default);
}
