namespace FireblocksReplacement.Api.Dtos.Fireblocks;

public class TransactionDto
{
    public string Id { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public SourceDto Source { get; set; } = new();
    public DestinationDto Destination { get; set; } = new();
    public string Amount { get; set; } = "0";
    public string Fee { get; set; } = "0";
    public string NetworkFee { get; set; } = "0";
    public string Status { get; set; } = "SUBMITTED";
    public string? TxHash { get; set; }
    public long CreatedAt { get; set; }
    public long LastUpdated { get; set; }
    public int NumOfConfirmations { get; set; }
}

public class SourceDto
{
    public string Type { get; set; } = "VAULT_ACCOUNT";
    public string Id { get; set; } = string.Empty;
}

public class DestinationDto
{
    public string Type { get; set; } = "ONE_TIME_ADDRESS";
    public OneTimeAddressDto? OneTimeAddress { get; set; }
}

public class OneTimeAddressDto
{
    public string Address { get; set; } = string.Empty;
    public string? Tag { get; set; }
}

public class CreateTransactionRequestDto
{
    public string AssetId { get; set; } = string.Empty;
    public SourceDto Source { get; set; } = new();
    public DestinationDto Destination { get; set; } = new();
    public string Amount { get; set; } = "0";
    public string? Note { get; set; }
    public FeeDto? Fee { get; set; }
}

public class FeeDto
{
    public string? FeeLevel { get; set; }
    public string? GasPrice { get; set; }
    public string? GasLimit { get; set; }
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
    public string Fee { get; set; } = "0";
    public string GasPrice { get; set; } = "0";
}

public class NetworkFeeResponseDto
{
    public string AssetId { get; set; } = string.Empty;
    public FeeEstimateDto Low { get; set; } = new();
    public FeeEstimateDto Medium { get; set; } = new();
    public FeeEstimateDto High { get; set; } = new();
}
