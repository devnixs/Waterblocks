using System.Text.Json.Serialization;

namespace Waterblocks.Api.Dtos.Fireblocks;

/// <summary>
/// Matches Fireblocks TransactionResponse schema from swagger
/// </summary>
public class TransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string? AssetId { get; set; }
    public TransferPeerPathResponseDto? Source { get; set; }
    public TransferPeerPathResponseDto? Destination { get; set; }
    public string? RequestedAmount { get; set; }
    public string? Amount { get; set; }
    public string? NetAmount { get; set; }
    public string? AmountUSD { get; set; }
    public string? ServiceFee { get; set; }
    public string? NetworkFee { get; set; }
    public decimal? CreatedAt { get; set; }
    public decimal LastUpdated { get; set; }
    public string Status { get; set; } = "SUBMITTED";
    public string TxHash { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string? SubStatus { get; set; }
    public string DestinationAddress { get; set; } = string.Empty;
    public string SourceAddress { get; set; } = string.Empty;
    public string DestinationAddressDescription { get; set; } = string.Empty;
    public string DestinationTag { get; set; } = string.Empty;
    public List<string> SignedBy { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public string RejectedBy { get; set; } = string.Empty;
    public string AddressType { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string ExchangeTxId { get; set; } = string.Empty;
    public string FeeCurrency { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public List<NetworkRecordDto> NetworkRecords { get; set; } = new();
    public AmlScreeningResultDto AmlScreeningResult { get; set; } = new();
    public string CustomerRefId { get; set; } = string.Empty;
    public decimal? NumOfConfirmations { get; set; }
    public List<SignedMessageDto> SignedMessages { get; set; } = new();
    public object ExtraParameters { get; set; } = new Dictionary<string, object>();
    public string ExternalTxId { get; set; } = string.Empty;
    public string ReplacedTxHash { get; set; } = string.Empty;
    public List<TransactionResponseDestinationDto> Destinations { get; set; } = new();
    public BlockInfoDto BlockInfo { get; set; } = new();
    public AuthorizationInfoDto AuthorizationInfo { get; set; } = new();
    public AmountInfoDto AmountInfo { get; set; } = new();
    public decimal? Index { get; set; }
    public string BlockchainIndex { get; set; } = string.Empty;
}

/// <summary>
/// Matches Fireblocks TransferPeerPathResponse schema
/// </summary>
public class TransferPeerPathResponseDto
{
    public string Type { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? SubType { get; set; }
    public string? VirtualType { get; set; }
    public string? VirtualId { get; set; }
}

/// <summary>
/// Matches Fireblocks AmountInfo schema
/// </summary>
public class AmountInfoDto
{
    public string? Amount { get; set; }
    public string? RequestedAmount { get; set; }
    public string? NetAmount { get; set; }
    public string? AmountUSD { get; set; }
}

/// <summary>
/// Matches Fireblocks BlockInfo schema
/// </summary>
public class BlockInfoDto
{
    public string? BlockHeight { get; set; }
    public string? BlockHash { get; set; }
}

/// <summary>
/// Matches Fireblocks AuthorizationInfo schema
/// </summary>
public class AuthorizationInfoDto
{
    public bool? AllowOperatorAsAuthorizer { get; set; }
    public string? Logic { get; set; }
    public List<AuthorizationGroupDto> Groups { get; set; } = new();
}

/// <summary>
/// Matches Fireblocks AuthorizationGroups schema
/// </summary>
public class AuthorizationGroupDto
{
    public decimal? Th { get; set; }
    public Dictionary<string, string>? Users { get; set; }
}

/// <summary>
/// Matches Fireblocks NetworkRecord schema
/// </summary>
public class NetworkRecordDto
{
    public TransferPeerPathResponseDto? Source { get; set; }
    public TransferPeerPathResponseDto? Destination { get; set; }
    public string TxHash { get; set; } = string.Empty;
    public string NetworkFee { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string NetAmount { get; set; } = string.Empty;
    public bool? IsDropped { get; set; }
    public string Type { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public string SourceAddress { get; set; } = string.Empty;
    public string AmountUSD { get; set; } = string.Empty;
    public decimal? Index { get; set; }
}

/// <summary>
/// Matches Fireblocks AmlScreeningResult schema
/// </summary>
public class AmlScreeningResultDto
{
    public string Provider { get; set; } = string.Empty;
    public object Payload { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Matches Fireblocks SignedMessage schema
/// </summary>
public class SignedMessageDto
{
    public string Content { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public List<decimal>? DerivationPath { get; set; }
    public SignatureDto? Signature { get; set; }
    public string PublicKey { get; set; } = string.Empty;
}

/// <summary>
/// Signature details in SignedMessage
/// </summary>
public class SignatureDto
{
    public string FullSig { get; set; } = string.Empty;
    public string R { get; set; } = string.Empty;
    public string S { get; set; } = string.Empty;
    public decimal? V { get; set; }
}

/// <summary>
/// Matches Fireblocks TransactionResponseDestination schema
/// </summary>
public class TransactionResponseDestinationDto
{
    public string Amount { get; set; } = string.Empty;
    public string AmountUSD { get; set; } = string.Empty;
    public AmlScreeningResultDto AmlScreeningResult { get; set; } = new();
    public TransferPeerPathResponseDto? Destination { get; set; }
    public AuthorizationInfoDto AuthorizationInfo { get; set; } = new();
}

// Keep legacy types for request DTOs
public class SourceDto
{
    public string Type { get; set; } = "VAULT_ACCOUNT";
    public string Id { get; set; } = string.Empty;
}

public class DestinationDto
{
    public string Type { get; set; } = "ONE_TIME_ADDRESS";
    public string? Id { get; set; }
    public OneTimeAddressDto? OneTimeAddress { get; set; }
}

public class OneTimeAddressDto
{
    public string Address { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

/// <summary>
/// Matches Fireblocks TransactionRequest schema from swagger
/// </summary>
public class CreateTransactionRequestDto
{
    public string? AssetId { get; set; }
    public SourceDto? Source { get; set; }
    public DestinationDto? Destination { get; set; }
    public string? Amount { get; set; }
    public string? Fee { get; set; }
    public string? FeeLevel { get; set; }
    public string? PriorityFee { get; set; }
    public bool? FailOnLowFee { get; set; }
    public string? MaxFee { get; set; }
    public string? GasPrice { get; set; }
    public string? GasLimit { get; set; }
    public string? NetworkFee { get; set; }
    public string? Note { get; set; }
    public bool? AutoStaking { get; set; }
    public string? NetworkStaking { get; set; }
    public string? CpuStaking { get; set; }
    public object? ExtraParameters { get; set; }
    public string? Operation { get; set; }
    public string? CustomerRefId { get; set; }
    public string? ReplaceTxByHash { get; set; }
    public string? ExternalTxId { get; set; }
    public List<TransactionRequestDestinationDto>? Destinations { get; set; }
    public bool? TreatAsGrossAmount { get; set; }
    public bool? ForceSweep { get; set; }
}

public class TransactionRequestDestinationDto
{
    public string? Amount { get; set; }
    public DestinationDto? Destination { get; set; }
}

public class EstimateFeeRequestDto
{
    public string AssetId { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
    public SourceDto Source { get; set; } = new();
    public DestinationDto Destination { get; set; } = new();
}

public class EstimateFeeResponseDto
{
    public FeeEstimateDto Low { get; set; } = new();
    public FeeEstimateDto Medium { get; set; } = new();
    public FeeEstimateDto High { get; set; } = new();
}

public class FeeEstimateDto
{
    public string? FeePerByte { get; set; }
    public string? GasPrice { get; set; }
    public string? GasLimit { get; set; }
    public string? NetworkFee { get; set; }
    public string? BaseFee { get; set; }
    public string? PriorityFee { get; set; }
}

public class NetworkFeeResponseDto
{
    public FeeEstimateDto Low { get; set; } = new();
    public FeeEstimateDto Medium { get; set; } = new();
    public FeeEstimateDto High { get; set; } = new();
}

/// <summary>
/// Response for create transaction endpoint
/// </summary>
public class CreateTransactionResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Response for cancel transaction endpoint
/// </summary>
public class CancelTransactionResponseDto
{
    public bool Success { get; set; }
}

/// <summary>
/// Response for freeze/unfreeze endpoints
/// </summary>
public class FreezeTransactionResponseDto
{
    public bool Success { get; set; }
}

/// <summary>
/// Response for drop transaction endpoint
/// </summary>
public class DropTransactionResponseDto
{
    public bool Success { get; set; }
    public List<string>? Transactions { get; set; }
}

/// <summary>
/// Response for validate address endpoint
/// </summary>
public class ValidateAddressResponseDto
{
    public bool IsValid { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresTag { get; set; }
}
