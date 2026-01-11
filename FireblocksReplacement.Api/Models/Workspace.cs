using System.ComponentModel.DataAnnotations;

namespace FireblocksReplacement.Api.Models;

public class Workspace
{
    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<VaultAccount> VaultAccounts { get; set; } = new List<VaultAccount>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}
