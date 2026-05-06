using System.Text.Json.Serialization;

namespace Waterblocks.Api.Dtos.Admin;

public class AdminTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string VaultAccountId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public AdminTransactionPartyType SourceType { get; set; } = AdminTransactionPartyType.EXTERNAL;
    public string? SourceAddress { get; set; }
    public string? SourceVaultAccountName { get; set; }
    public AdminTransactionPartyType DestinationType { get; set; } = AdminTransactionPartyType.EXTERNAL;
    public string? DestinationVaultAccountName { get; set; }
    public string Amount { get; set; } = "0";
    public string DestinationAddress { get; set; } = string.Empty;
    public string? DestinationTag { get; set; }
    public string State { get; set; } = "SUBMITTED";
    public string? Hash { get; set; }
    public string Fee { get; set; } = "0";
    public string NetworkFee { get; set; } = "0";
    public string? FeeCurrency { get; set; }
    public bool TreatAsGrossAmount { get; set; }
    public bool IsFrozen { get; set; }
    public string? FailureReason { get; set; }
    public string? ReplacedByTxId { get; set; }
    public int Confirmations { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AdminTransactionsPageDto
{
    public List<AdminTransactionDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}

public class CreateAdminTransactionRequestDto
{
    public string? Type { get; set; } = "OUTGOING"; // optional, derived from source/destination
    public string? VaultAccountId { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string? SourceAddress { get; set; }
    public string? DestinationAddress { get; set; }
    public string Amount { get; set; } = "0";
    public string? DestinationTag { get; set; }
    public string? InitialState { get; set; }
    public string? Hash { get; set; }

    /// <summary>
    /// Network fee amount. If not provided, calculated from FeeLevel or defaults to Medium.
    /// </summary>
    public string? NetworkFee { get; set; }

    /// <summary>
    /// Fee level: LOW, MEDIUM, or HIGH. Used to calculate fee if NetworkFee not provided.
    /// </summary>
    public string? FeeLevel { get; set; }

    /// <summary>
    /// If true, the fee is deducted from the amount. If false, fee is added to amount.
    /// </summary>
    public bool? TreatAsGrossAmount { get; set; }

    /// <summary>
    /// If true, outgoing transactions are created and immediately completed using the normal completion flow.
    /// </summary>
    public bool? CompleteImmediately { get; set; }
}

public class FailTransactionRequestDto
{
    public string Reason { get; set; } = "NETWORK_ERROR";
}

public class TransactionStateDto
{
    public string Id { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AdminTransactionPartyType
{
    INTERNAL,
    EXTERNAL,
}
