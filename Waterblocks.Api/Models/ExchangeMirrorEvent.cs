using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

public enum ExchangeMirrorEventStatus
{
    PENDING,
    APPLIED,
    REJECTED,
    FAILED,
    CANCELLED,
}

public static class ExchangeMirrorEventType
{
    public const string Add = "add";
    public const string Remove = "remove";
}

/// <summary>
/// One collateral change that Waterblocks must send to the simulator of the exchange account.
/// </summary>
public class ExchangeMirrorEvent
{
    public const string ReversalSuffix = ":reversal";

    [Key]
    [MaxLength(200)]
    public string Id { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TransactionId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? WorkspaceId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ExchangeAccountId { get; set; } = string.Empty;

    /// <summary>
    /// The exchange type that selects the exchange, and with it the simulator URL, for this event.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ExchangeType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string SimulatorAccountName { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Type { get; set; } = ExchangeMirrorEventType.Add;

    [Required]
    [MaxLength(50)]
    public string AssetId { get; set; } = string.Empty;

    [Column(TypeName = "decimal(36,18)")]
    public decimal Amount { get; set; }

    public ExchangeMirrorEventStatus Status { get; set; } = ExchangeMirrorEventStatus.PENDING;

    public int Attempts { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
