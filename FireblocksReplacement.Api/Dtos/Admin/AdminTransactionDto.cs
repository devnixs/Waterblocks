namespace FireblocksReplacement.Api.Dtos.Admin;

public class AdminTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string VaultAccountId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string SourceType { get; set; } = "EXTERNAL";
    public string? SourceAddress { get; set; }
    public string? SourceVaultAccountId { get; set; }
    public string? SourceVaultAccountName { get; set; }
    public string DestinationType { get; set; } = "EXTERNAL";
    public string? DestinationVaultAccountId { get; set; }
    public string? DestinationVaultAccountName { get; set; }
    public string Amount { get; set; } = "0";
    public string DestinationAddress { get; set; } = string.Empty;
    public string? DestinationTag { get; set; }
    public string State { get; set; } = "SUBMITTED";
    public string? Hash { get; set; }
    public string Fee { get; set; } = "0";
    public string NetworkFee { get; set; } = "0";
    public bool IsFrozen { get; set; }
    public string? FailureReason { get; set; }
    public string? ReplacedByTxId { get; set; }
    public int Confirmations { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class CreateAdminTransactionRequestDto
{
    public string? Type { get; set; } = "OUTGOING"; // optional, derived from source/destination
    public string? VaultAccountId { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string SourceType { get; set; } = "EXTERNAL";
    public string? SourceAddress { get; set; }
    public string? SourceVaultAccountId { get; set; }
    public string DestinationType { get; set; } = "EXTERNAL";
    public string? DestinationAddress { get; set; }
    public string? DestinationVaultAccountId { get; set; }
    public string Amount { get; set; } = "0";
    public string? DestinationTag { get; set; }
    public string? InitialState { get; set; }
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
