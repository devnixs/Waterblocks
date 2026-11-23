namespace Waterblocks.Api.Infrastructure;

/// <summary>
/// One exchange of all_exchanges.json, the first content of the exchange catalog.
/// </summary>
internal sealed class ExchangeSeed
{
    public string? ExchangeType { get; set; }
    public string? Name { get; set; }
    public bool OneAccountPerWorkspace { get; set; }
    public List<ExchangeCurrencySeed> Currencies { get; set; } = new();
}

internal sealed class ExchangeCurrencySeed
{
    public string? AssetId { get; set; }
    public string? Currency { get; set; }
}
