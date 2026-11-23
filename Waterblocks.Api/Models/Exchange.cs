using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Waterblocks.Api.Models;

/// <summary>
/// One exchange of the Waterblocks exchange catalog. The facts of an exchange are data in this table, so that a new
/// exchange needs no code change.
/// </summary>
public class Exchange
{
    /// <summary>
    /// The Fireblocks exchange type, for example GATEIO.
    /// </summary>
    [Key]
    [MaxLength(50)]
    public string ExchangeType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The origin of the simulator of this exchange. Without it, accounts of this exchange cannot connect.
    /// </summary>
    [MaxLength(500)]
    public string? SimulatorBaseUrl { get; set; }

    /// <summary>
    /// True when a workspace can have only one account of this exchange.
    /// </summary>
    public bool OneAccountPerWorkspace { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ExchangeCurrency> Currencies { get; set; } = new List<ExchangeCurrency>();

    [NotMapped]
    public bool IsConnectable => !string.IsNullOrWhiteSpace(SimulatorBaseUrl);
}
