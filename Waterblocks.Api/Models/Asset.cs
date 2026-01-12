using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

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

    /// <summary>
    /// Fixed network fee for transactions in this asset.
    /// For tokens (ERC20), this is denominated in the native asset (e.g., ETH).
    /// </summary>
    [Column(TypeName = "decimal(36,18)")]
    public decimal BaseFee { get; set; } = 0;

    /// <summary>
    /// The asset used to pay fees. If null, fees are paid in the native asset.
    /// For base assets (BTC, ETH), this is the same as AssetId.
    /// For tokens (USDC, USDT), this is typically "ETH".
    /// </summary>
    [MaxLength(50)]
    public string? FeeAssetId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the asset ID used to pay fees for this asset.
    /// Returns FeeAssetId if set, otherwise NativeAsset, otherwise self.
    /// </summary>
    public string GetFeeAssetId() => FeeAssetId ?? NativeAsset ?? AssetId;

    /// <summary>
    /// Returns true if fees for this asset are paid in a different asset.
    /// </summary>
    public bool HasSeparateFeeAsset() => GetFeeAssetId() != AssetId;
}
