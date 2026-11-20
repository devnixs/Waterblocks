using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

/// <summary>
/// The collateral balances of one asset in one exchange account.
/// </summary>
public class ExchangeAccountAsset
{
    [MaxLength(50)]
    public string ExchangeAccountId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// The on-chain address of the collateral vault account. It is not a vault address.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string CollateralAddress { get; set; } = string.Empty;

    /// <summary>
    /// The on-chain balance of the collateral vault account.
    /// </summary>
    [Column(TypeName = "decimal(36,18)")]
    public decimal Custody { get; set; }

    /// <summary>
    /// The amount that open removals hold.
    /// </summary>
    [Column(TypeName = "decimal(36,18)")]
    public decimal Locked { get; set; }

    /// <summary>
    /// The amount that the simulated exchange does not release.
    /// </summary>
    [Column(TypeName = "decimal(36,18)")]
    public decimal ExchangeHold { get; set; }

    /// <summary>
    /// The mirrored amount. An add raises it and a removal lowers it.
    /// </summary>
    [Column(TypeName = "decimal(36,18)")]
    public decimal Credit { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ExchangeAccount ExchangeAccount { get; set; } = null!;
}
