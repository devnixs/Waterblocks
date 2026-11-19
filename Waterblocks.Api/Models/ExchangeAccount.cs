using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

/// <summary>
/// An exchange account of one workspace, with an optional connection to a Gate simulator account.
/// </summary>
public class ExchangeAccount
{
    public const string ApprovedStatus = "APPROVED";

    /// <summary>
    /// The unique index that allows one Gate account in each workspace.
    /// </summary>
    public const string OneGateAccountPerWorkspaceIndex = "IX_ExchangeAccounts_WorkspaceId_Gate";

    /// <summary>
    /// The unique index that allows one exchange account for each Gate simulator account, in all workspaces.
    /// </summary>
    public const string OneAccountPerSimulatorAccountIndex = "IX_ExchangeAccounts_GateSimulatorUserId";

    [Key]
    [MaxLength(50)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(50)]
    public string CollateralAccountId { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(50)]
    public string WorkspaceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ExchangeType { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = ApprovedStatus;

    [MaxLength(255)]
    public string? MainAccountId { get; set; }

    [MaxLength(100)]
    public string? GateSimulatorAccount { get; set; }

    public long? GateSimulatorUserId { get; set; }

    [MaxLength(20)]
    public string? MaskedApiKey { get; set; }

    public DateTimeOffset? ConnectedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public bool IsConnected => GateSimulatorAccount != null;

    public Workspace Workspace { get; set; } = null!;

    public ICollection<ExchangeAccountAsset> Assets { get; set; } = new List<ExchangeAccountAsset>();
}
