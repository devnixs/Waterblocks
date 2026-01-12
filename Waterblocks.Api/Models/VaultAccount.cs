using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

public class VaultAccount
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string WorkspaceId { get; set; } = string.Empty;

    public bool HiddenOnUI { get; set; } = false;

    [MaxLength(255)]
    public string? CustomerRefId { get; set; }

    public bool AutoFuel { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(WorkspaceId))]
    public Workspace Workspace { get; set; } = null!;

    // Navigation properties
    public ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
