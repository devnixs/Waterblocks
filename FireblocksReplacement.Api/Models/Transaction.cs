using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FireblocksReplacement.Api.Models;

public class Transaction
{
    [Key]
    [MaxLength(100)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(50)]
    public string VaultAccountId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string WorkspaceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AssetId { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string SourceType { get; set; } = "VAULT_ACCOUNT";

    [MaxLength(500)]
    public string? SourceAddress { get; set; }

    [MaxLength(50)]
    public string? SourceVaultAccountId { get; set; }

    [Required]
    [MaxLength(20)]
    public string DestinationType { get; set; } = "ONE_TIME_ADDRESS";

    [MaxLength(50)]
    public string? DestinationVaultAccountId { get; set; }

    [Required]
    [Column(TypeName = "decimal(36,18)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(36,18)")]
    public decimal RequestedAmount { get; set; }

    [Required]
    [MaxLength(500)]
    public string DestinationAddress { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DestinationTag { get; set; }

    [Required]
    public TransactionState State { get; set; } = TransactionState.SUBMITTED;

    [MaxLength(100)]
    public string? SubStatus { get; set; }

    [MaxLength(100)]
    public string? Hash { get; set; }

    [Column(TypeName = "decimal(36,18)")]
    public decimal Fee { get; set; } = 0;

    [Column(TypeName = "decimal(36,18)")]
    public decimal NetworkFee { get; set; } = 0;

    [Column(TypeName = "decimal(36,18)")]
    public decimal ServiceFee { get; set; } = 0;

    public bool IsFrozen { get; set; } = false;

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    [MaxLength(100)]
    public string? ReplacedByTxId { get; set; }

    public int Confirmations { get; set; } = 0;

    [MaxLength(1000)]
    public string? Note { get; set; }

    [MaxLength(100)]
    public string? ExternalTxId { get; set; }

    [MaxLength(100)]
    public string? CustomerRefId { get; set; }

    [MaxLength(50)]
    public string Operation { get; set; } = "TRANSFER";

    [MaxLength(20)]
    public string? FeeCurrency { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(VaultAccountId))]
    public VaultAccount VaultAccount { get; set; } = null!;

    [ForeignKey(nameof(WorkspaceId))]
    public Workspace Workspace { get; set; } = null!;

    public void TransitionTo(TransactionState newState)
    {
        State.ValidateTransition(newState);
        State = newState;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Freeze()
    {
        if (State.IsTerminal())
        {
            throw new InvalidOperationException($"Cannot freeze transaction in terminal state {State}");
        }
        IsFrozen = true;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Unfreeze()
    {
        if (State.IsTerminal())
        {
            throw new InvalidOperationException($"Cannot unfreeze transaction in terminal state {State}");
        }
        IsFrozen = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
