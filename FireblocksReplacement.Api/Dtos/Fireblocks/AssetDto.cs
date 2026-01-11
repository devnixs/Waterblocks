namespace FireblocksReplacement.Api.Dtos.Fireblocks;

public class AssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Decimals { get; set; }
    public string? Type { get; set; }
}

public class WalletAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Balance { get; set; } = "0";
    public string LockedAmount { get; set; } = "0";
    public string Available { get; set; } = "0";
    public List<AddressDto> Addresses { get; set; } = new();
}

public class AddressDto
{
    public string Address { get; set; } = string.Empty;
    public string? Tag { get; set; }
    public string? Type { get; set; }
}
