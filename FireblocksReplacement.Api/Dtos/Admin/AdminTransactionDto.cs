namespace FireblocksReplacement.Api.Dtos.Admin;

public class AdminTransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string VaultAccountId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateAdminTransactionRequestDto
{
    public string Type { get; set; } = "OUTGOING"; // INCOMING or OUTGOING
    public string VaultAccountId { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
    public string? DestinationAddress { get; set; }
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
