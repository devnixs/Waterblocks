using System.ComponentModel.DataAnnotations;

namespace FireblocksReplacement.Api.Models;

public class VaultAccount
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public bool HiddenOnUI { get; set; } = false;

    [MaxLength(255)]
    public string? CustomerRefId { get; set; }

    public bool AutoFuel { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
