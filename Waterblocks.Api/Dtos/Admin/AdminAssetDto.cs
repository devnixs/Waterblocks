namespace Waterblocks.Api.Dtos.Admin;

public class AdminAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Decimals { get; set; }
    public string? Type { get; set; }
    public string BlockchainType { get; set; } = string.Empty;
    public string? ContractAddress { get; set; }
    public string? NativeAsset { get; set; }
    public decimal BaseFee { get; set; }
    public string? FeeAssetId { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateAdminAssetRequestDto
{
    public string AssetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int? Decimals { get; set; }
    public string? Type { get; set; }
    public string? BlockchainType { get; set; }
    public string? ContractAddress { get; set; }
    public string? NativeAsset { get; set; }
    public decimal? BaseFee { get; set; }
    public string? FeeAssetId { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateAdminAssetRequestDto
{
    public string? Name { get; set; }
    public string? Symbol { get; set; }
    public int? Decimals { get; set; }
    public string? Type { get; set; }
    public string? BlockchainType { get; set; }
    public string? ContractAddress { get; set; }
    public string? NativeAsset { get; set; }
    public decimal? BaseFee { get; set; }
    public string? FeeAssetId { get; set; }
    public bool? IsActive { get; set; }
}
