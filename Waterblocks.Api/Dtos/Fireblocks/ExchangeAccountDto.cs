namespace Waterblocks.Api.Dtos.Fireblocks;

/// <summary>
/// Matches Fireblocks AddExchangeAccountRequest. creds is the encrypted credentials blob and key is the exchange API key.
/// Waterblocks decrypts creds only to connect the account, and stores neither creds nor the secret.
/// </summary>
public class AddExchangeAccountRequestDto
{
    public string? ExchangeType { get; set; }
    public string? Name { get; set; }
    public string? Creds { get; set; }
    public string? Key { get; set; }
    public string? MainAccountId { get; set; }
}

public class AddExchangeAccountResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ExchangeType { get; set; } = string.Empty;
}

/// <summary>
/// Matches Fireblocks ExchangeAccount.
/// </summary>
public class ExchangeAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<ExchangeAssetDto> Assets { get; set; } = new();
    public bool Success { get; set; }
    public List<ExchangeTradingAccountDto> TradingAccounts { get; set; } = new();
    public bool IsSubaccount { get; set; }
    public string? MainAccountId { get; set; }
}

/// <summary>
/// Matches Fireblocks ExchangeAsset. The field assetId is left out, because Waterblocks has no Fireblocks asset UUID.
/// </summary>
public class ExchangeAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Balance { get; set; } = "0";
    public string LockedAmount { get; set; } = "0";
    public string Total { get; set; } = "0";
    public string Available { get; set; } = "0";
    public string Credit { get; set; } = "0";
    public string ProviderSymbol { get; set; } = string.Empty;
    public string AssetSymbol { get; set; } = string.Empty;
    public string AssetLegacyId { get; set; } = string.Empty;
}

public class ExchangeTradingAccountDto
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public List<ExchangeAssetDto> Assets { get; set; } = new();
}

public class PagedExchangeAccountsResponseDto
{
    public List<ExchangeAccountDto> Exchanges { get; set; } = new();
    public ExchangeAccountsPagingDto Paging { get; set; } = new();
}

public class ExchangeAccountsPagingDto
{
    public string? Before { get; set; }
    public string? After { get; set; }
}

public class ExchangeCredentialsPublicKeyDto
{
    public string PublicKey { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
}

/// <summary>
/// Matches Fireblocks AddCollateralRequestBody.
/// </summary>
public class AddCollateralRequestDto
{
    public CreateTransactionRequestDto? TransactionRequest { get; set; }
    public object? IsSrcCollateral { get; set; }
}

/// <summary>
/// Matches Fireblocks RemoveCollateralRequestBody.
/// </summary>
public class RemoveCollateralRequestDto
{
    public CreateTransactionRequestDto? TransactionRequest { get; set; }
    public object? IsDstCollateral { get; set; }
}
