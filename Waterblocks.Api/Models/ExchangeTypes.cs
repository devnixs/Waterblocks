namespace Waterblocks.Api.Models;

public static class ExchangeTypes
{
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
        "GATEIO",
        "COINHAKO",
        "BULLISH",
        "BITGET",
        "LUNO",
    };
}
