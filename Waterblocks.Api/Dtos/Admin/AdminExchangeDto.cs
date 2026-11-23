namespace Waterblocks.Api.Dtos.Admin;

/// <summary>
/// One exchange of the exchange catalog.
/// </summary>
public class AdminExchangeDto
{
    public string ExchangeType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? SimulatorBaseUrl { get; set; }
    public bool IsConnectable { get; set; }
    public bool OneAccountPerWorkspace { get; set; }
    public List<AdminExchangeCurrencyDto> Currencies { get; set; } = new();
}

public class AdminExchangeCurrencyDto
{
    public string? AssetId { get; set; }
    public string? Currency { get; set; }
}

/// <summary>
/// The body of POST /admin/exchanges and PUT /admin/exchanges/{exchangeType}. PUT replaces all fields.
/// </summary>
public class SaveAdminExchangeRequestDto
{
    public string? ExchangeType { get; set; }
    public string? Name { get; set; }
    public string? SimulatorBaseUrl { get; set; }
    public bool OneAccountPerWorkspace { get; set; }
    public List<AdminExchangeCurrencyDto>? Currencies { get; set; }
}
