namespace Waterblocks.Api.Models;

public static class ExchangeTypes
{
    public const string Gate = "GATEIO";

    /// <summary>
    /// The exchange types that the Fireblocks "Add an exchange account" call accepts.
    /// </summary>
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        "INDEPENDENT_RESERVE",
        "BIT",
        "BITHUMB",
        "BITSO",
        "CRYPTOCOM",
        "BYBIT_V2",
        "WHITEBIT",
        "HITBTC",
        "GEMINI",
        "HUOBI",
        Gate,
        "COINHAKO",
        "BULLISH",
        "BITGET",
        "LUNO",
    };

    /// <summary>
    /// The assets that a connected Gate simulator account can hold.
    /// </summary>
    public static readonly IReadOnlyList<string> GateSimulatorAssets = new[] { "BTC", "USDT" };
}
