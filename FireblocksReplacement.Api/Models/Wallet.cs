using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireblocksReplacement.Api.Models;

public class Wallet
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string VaultAccountId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AssetId { get; set; } = string.Empty;

    [Column(TypeName = "decimal(36,18)")]
    public decimal Balance { get; set; } = 0;

    [Column(TypeName = "decimal(36,18)")]
    public decimal LockedAmount { get; set; } = 0;

    [Column(TypeName = "decimal(36,18)")]
    public decimal Pending { get; set; } = 0;

    [Column(TypeName = "decimal(36,18)")]
    public decimal Frozen { get; set; } = 0;

    [Column(TypeName = "decimal(36,18)")]
    public decimal Staked { get; set; } = 0;

    [MaxLength(100)]
    public string? BlockHeight { get; set; }

    [MaxLength(100)]
    public string? BlockHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(VaultAccountId))]
    public VaultAccount VaultAccount { get; set; } = null!;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();
}
