using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireblocksReplacement.Api.Models;

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

    public int WalletId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(WalletId))]
    public Wallet Wallet { get; set; } = null!;
}
