namespace Waterblocks.Api.Dtos.Fireblocks;

public class AssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Decimals { get; set; }
    public string? Type { get; set; }
}

/// <summary>
/// Matches Fireblocks VaultAsset schema from swagger
/// </summary>
public class VaultAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Total { get; set; } = string.Empty;
    public string Balance { get; set; } = string.Empty; // Deprecated - replaced by "total"
    public string Available { get; set; } = string.Empty;
    public string Pending { get; set; } = string.Empty;
    public string Frozen { get; set; } = string.Empty;
    public string LockedAmount { get; set; } = string.Empty;
    public string Staked { get; set; } = string.Empty;
    public string TotalStakedCPU { get; set; } = string.Empty;
    public string TotalStakedNetwork { get; set; } = string.Empty;
    public string SelfStakedCPU { get; set; } = string.Empty;
    public string SelfStakedNetwork { get; set; } = string.Empty;
    public string PendingRefundCPU { get; set; } = string.Empty;
    public string PendingRefundNetwork { get; set; } = string.Empty;
    public string BlockHeight { get; set; } = string.Empty;
    public string BlockHash { get; set; } = string.Empty;
    public List<AllocatedBalanceDto> AllocatedBalances { get; set; } = new();
}

/// <summary>
/// Matches Fireblocks AllocatedBalance schema from swagger
/// </summary>
public class AllocatedBalanceDto
{
    public string? AllocationId { get; set; }
    public string? ThirdPartyAccountId { get; set; }
    public string? Affiliation { get; set; }
    public string? VirtualType { get; set; }
    public string? Total { get; set; }
    public string? Available { get; set; }
    public string? Pending { get; set; }
    public string? Frozen { get; set; }
    public string? Locked { get; set; }
    public string? Staked { get; set; }
}

/// <summary>
/// Matches Fireblocks CreateVaultAssetResponse schema from swagger
/// </summary>
public class CreateVaultAssetResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LegacyAddress { get; set; } = string.Empty;
    public string EnterpriseAddress { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string EosAccountName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ActivationTxId { get; set; } = string.Empty;
}

/// <summary>
/// Request body for creating a vault asset/wallet
/// </summary>
public class CreateVaultAssetRequestDto
{
    public string? EosAccountName { get; set; }
}

public class AddressDto
{
    public string Address { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Matches Fireblocks VaultWalletAddress schema from swagger
/// </summary>
public class VaultWalletAddressDto
{
    public string AssetId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string CustomerRefId { get; set; } = string.Empty;
    public string AddressFormat { get; set; } = string.Empty; // SEGWIT, LEGACY, BASE, PAYMENT
    public string LegacyAddress { get; set; } = string.Empty;
    public string EnterpriseAddress { get; set; } = string.Empty;
    public int Bip44AddressIndex { get; set; }
}

/// <summary>
/// Matches Fireblocks CreateAddressResponse schema from swagger
/// </summary>
public class CreateAddressResponseDto
{
    public string Address { get; set; } = string.Empty;
    public string LegacyAddress { get; set; } = string.Empty;
    public string EnterpriseAddress { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int Bip44AddressIndex { get; set; }
}

/// <summary>
/// Request body for creating an address
/// </summary>
public class CreateAddressRequestDto
{
    public string? Description { get; set; }
    public string? CustomerRefId { get; set; }
}

/// <summary>
/// Matches Fireblocks PaginatedAddressResponse schema from swagger
/// </summary>
public class PaginatedAddressResponseDto
{
    public List<VaultWalletAddressDto> Addresses { get; set; } = new();
    public PagingDto Paging { get; set; } = new();
}
