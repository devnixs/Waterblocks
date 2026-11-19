using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services.ExchangeSimulators;

/// <summary>
/// The account that an API key and secret open in an exchange simulator.
/// </summary>
public sealed record SimulatorAccountIdentity(string AccountId, string AccountName);

public sealed record SimulatorBalance(decimal Available, decimal Locked);

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
/// Connects exchange accounts of one exchange type to the simulator of that exchange.
/// Each exchange that Waterblocks can connect has one implementation.
/// </summary>
public interface IExchangeSimulatorClient
{
    /// <summary>
    /// The Fireblocks exchange type that this client serves, for example GATEIO.
    /// </summary>
    string ExchangeType { get; }

    /// <summary>
    /// The exchange name that the Admin UI and the error messages show.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// The assets that a connected account can hold.
    /// </summary>
    IReadOnlyList<string> AssetIds { get; }

    /// <summary>
    /// True when a workspace can have only one account of this exchange type.
    /// </summary>
    bool OneAccountPerWorkspace { get; }

    bool IsConfigured { get; }

    /// <summary>
    /// Finds the simulator account of an API key and secret. The secret is used only for this call.
    /// </summary>
    Task<SimulatorAccountIdentity> IdentifyAccountAsync(
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the balances of one account. Returns null when the simulator cannot give them.
    /// </summary>
    Task<SimulatorAccountSnapshot?> TryReadAccountAsync(
        string accountName,
        CancellationToken cancellationToken = default);

    Task<MirrorEventOutcome> SendEventAsync(
        ExchangeMirrorEvent mirrorEvent,
        CancellationToken cancellationToken = default);
}
