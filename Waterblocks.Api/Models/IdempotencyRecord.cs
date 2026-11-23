using System.ComponentModel.DataAnnotations;

namespace Waterblocks.Api.Models;

/// <summary>
/// The first reply of a request with an Idempotency-Key header.
/// </summary>
public class IdempotencyRecord
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string WorkspaceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Route { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Key { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }
}
