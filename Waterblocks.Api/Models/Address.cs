using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

public class Address
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string AddressValue { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Tag { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? CustomerRefId { get; set; }

    [MaxLength(20)]
    public string? AddressFormat { get; set; } // SEGWIT, LEGACY, BASE, PAYMENT

    [MaxLength(500)]
    public string? LegacyAddress { get; set; }

    [MaxLength(500)]
    public string? EnterpriseAddress { get; set; }

    public int? Bip44AddressIndex { get; set; }

    public int WalletId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(WalletId))]
    public Wallet Wallet { get; set; } = null!;
}
