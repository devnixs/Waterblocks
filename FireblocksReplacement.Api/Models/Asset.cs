using System.ComponentModel.DataAnnotations;

namespace FireblocksReplacement.Api.Models;

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

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
