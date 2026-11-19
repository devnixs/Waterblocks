namespace Waterblocks.Api.Dtos.Admin;

public class AdminExchangeAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string CollateralAccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExchangeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSubaccount { get; set; }
    public string? MainAccountId { get; set; }
    public bool Success { get; set; }
    public AdminExchangeConnectionDto? Connection { get; set; }
    public List<AdminExchangeAssetDto> Assets { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AdminExchangeConnectionDto
{
    public string SimulatorAccountId { get; set; } = string.Empty;
    public string SimulatorAccountName { get; set; } = string.Empty;
    public string MaskedApiKey { get; set; } = string.Empty;
    public DateTimeOffset? ConnectedAt { get; set; }
}

/// <summary>
/// One exchange that Waterblocks can connect to its simulator.
/// </summary>
public class AdminExchangeProviderDto
{
    public string ExchangeType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> AssetIds { get; set; } = new();
    public bool OneAccountPerWorkspace { get; set; }
    public bool IsConfigured { get; set; }
}

public class AdminExchangeAssetDto
{
    public string AssetId { get; set; } = string.Empty;
    public string Total { get; set; } = "0";
    public string Available { get; set; } = "0";
    public string Credit { get; set; } = "0";
    public string LockedAmount { get; set; } = "0";
    public string Locked { get; set; } = "0";
    public string ExchangeHold { get; set; } = "0";
    public string? CollateralAddress { get; set; }
}

public class CreateAdminExchangeAccountRequestDto
{
    public string? ExchangeType { get; set; }
    public string? Name { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public class ConnectAdminExchangeAccountRequestDto
{
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

public class AdminCollateralTransferRequestDto
{
    public string? AssetId { get; set; }
    public string? VaultAccountId { get; set; }
    public string? Amount { get; set; }
    public bool? TreatAsGrossAmount { get; set; }
    public string? Note { get; set; }
    public string? InitiatedBy { get; set; }
}

public class UpdateAdminExchangeAssetRequestDto
{
    public string? ExchangeHold { get; set; }
    public string? Credit { get; set; }
}

public class AdminCollateralTransferDto
{
    public string Id { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string AssetId { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
    public string RequestedAmount { get; set; } = "0";
    public string NetworkFee { get; set; } = "0";
    public bool TreatAsGrossAmount { get; set; }
    public string VaultAccountId { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ToName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string State { get; set; } = string.Empty;
    public string? SubStatus { get; set; }
    public string? FailureReason { get; set; }
    public string? MirrorState { get; set; }
    public string? MirrorError { get; set; }
    public string? InitiatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
