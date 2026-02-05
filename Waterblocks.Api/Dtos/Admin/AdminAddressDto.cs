namespace Waterblocks.Api.Dtos.Admin;

public class AdminAddressDto
{
    public string Address { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

public class WalletAddressDto
{
    public int Id { get; set; }
    public string AddressValue { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AddressFormat { get; set; }
    public string? LegacyAddress { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
