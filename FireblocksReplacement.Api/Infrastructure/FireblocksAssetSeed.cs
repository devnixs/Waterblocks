namespace FireblocksReplacement.Api.Infrastructure;

internal sealed class FireblocksAssetSeed
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? ContractAddress { get; set; }
    public int? Decimals { get; set; }
    public string? NativeAsset { get; set; }
}
