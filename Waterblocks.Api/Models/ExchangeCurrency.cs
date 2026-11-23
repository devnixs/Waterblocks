using System.ComponentModel.DataAnnotations;

namespace Waterblocks.Api.Models;

/// <summary>
/// One Waterblocks asset that a connected account of an exchange can hold, with the currency name of that asset at
/// the exchange. For example, the asset USDT_ERC20 is the currency USDT at Gate.io.
/// </summary>
public class ExchangeCurrency
{
    [MaxLength(50)]
    public string ExchangeType { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AssetId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Currency { get; set; } = string.Empty;

    public Exchange Exchange { get; set; } = null!;
}
