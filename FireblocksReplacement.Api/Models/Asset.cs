using System.ComponentModel.DataAnnotations;

namespace FireblocksReplacement.Api.Models;

/// <summary>
/// Defines how the asset's blockchain handles addresses and transactions.
/// </summary>
public enum BlockchainType
{
    /// <summary>
    /// Account-based blockchains (ETH, USDC, etc.) - single address per asset.
    /// </summary>
    AccountBased,

    /// <summary>
    /// Address-based/UTXO blockchains (BTC, Cardano, etc.) - multiple addresses per asset.
    /// </summary>
    AddressBased,

    /// <summary>
    /// Memo-based blockchains (XRP, XLM, etc.) - single address with memo/tag for routing.
    /// </summary>
    MemoBased
}

public class Asset
{
    [Key]
    [MaxLength(50)]
    public string AssetId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Symbol { get; set; } = string.Empty;

    public int Decimals { get; set; } = 18;

    [MaxLength(50)]
    public string? Type { get; set; }

    /// <summary>
    /// Determines how wallets handle addresses for this asset.
    /// </summary>
    public BlockchainType BlockchainType { get; set; } = BlockchainType.AccountBased;

    [MaxLength(200)]
    public string? ContractAddress { get; set; }

    [MaxLength(50)]
    public string? NativeAsset { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
