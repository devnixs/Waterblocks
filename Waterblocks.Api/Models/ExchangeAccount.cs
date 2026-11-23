using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

/// <summary>
/// An exchange account of one workspace, with an optional connection to an account in the simulator of its exchange.
/// </summary>
public class ExchangeAccount
{
    public const string ApprovedStatus = "APPROVED";

    /// <summary>
    /// The unique index that allows one connection for each simulator account of an exchange type, in all workspaces.
    /// </summary>
    public const string OneConnectionPerSimulatorAccountIndex = "IX_ExchangeAccounts_ExchangeType_SimulatorAccountId";

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

    /// <summary>
    /// The stable id of the connected account in the exchange simulator.
    /// </summary>
    [MaxLength(100)]
    public string? SimulatorAccountId { get; set; }

    /// <summary>
    /// The name of the connected account in the exchange simulator.
    /// </summary>
    [MaxLength(100)]
    public string? SimulatorAccountName { get; set; }

    [MaxLength(20)]
    public string? MaskedApiKey { get; set; }

    public DateTimeOffset? ConnectedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [NotMapped]
    public bool IsConnected => SimulatorAccountName != null;

    public Workspace Workspace { get; set; } = null!;

    public ICollection<ExchangeAccountAsset> Assets { get; set; } = new List<ExchangeAccountAsset>();
}
