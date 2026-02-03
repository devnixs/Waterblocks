using System.Text.Json.Serialization;

namespace Waterblocks.Api.Infrastructure;

internal sealed class AssetSeed
{
    public string? Description { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public string? Symbol { get; set; }
    [JsonPropertyName("API_ID")]
    public string? ApiId { get; set; }
    public int? Decimals { get; set; }
    public string? Type { get; set; }
    public string? BlockchainType { get; set; }
    public string? ContractAddress { get; set; }
    public string? NativeAsset { get; set; }
    public decimal? BaseFee { get; set; }
    public string? FuelAssetId { get; set; }
}
