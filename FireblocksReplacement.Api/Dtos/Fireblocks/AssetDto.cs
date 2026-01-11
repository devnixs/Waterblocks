namespace FireblocksReplacement.Api.Dtos.Fireblocks;

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
    public string? Total { get; set; }
    public string? Balance { get; set; } // Deprecated - replaced by "total"
    public string? Available { get; set; }
    public string? Pending { get; set; }
    public string? Frozen { get; set; }
    public string? LockedAmount { get; set; }
    public string? Staked { get; set; }
    public string? TotalStakedCPU { get; set; }
    public string? TotalStakedNetwork { get; set; }
    public string? SelfStakedCPU { get; set; }
    public string? SelfStakedNetwork { get; set; }
    public string? PendingRefundCPU { get; set; }
    public string? PendingRefundNetwork { get; set; }
    public string? BlockHeight { get; set; }
    public string? BlockHash { get; set; }
    public List<AllocatedBalanceDto>? AllocatedBalances { get; set; }
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
    public string? Id { get; set; }
    public string? Address { get; set; }
    public string? LegacyAddress { get; set; }
    public string? EnterpriseAddress { get; set; }
    public string? Tag { get; set; }
    public string? EosAccountName { get; set; }
    public string? Status { get; set; }
    public string? ActivationTxId { get; set; }
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
    public string? Tag { get; set; }
    public string? Type { get; set; }
}

/// <summary>
/// Matches Fireblocks VaultWalletAddress schema from swagger
/// </summary>
public class VaultWalletAddressDto
{
    public string? AssetId { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? Tag { get; set; }
    public string? Type { get; set; }
    public string? CustomerRefId { get; set; }
    public string? AddressFormat { get; set; } // SEGWIT, LEGACY, BASE, PAYMENT
    public string? LegacyAddress { get; set; }
    public string? EnterpriseAddress { get; set; }
    public int? Bip44AddressIndex { get; set; }
}

/// <summary>
/// Matches Fireblocks CreateAddressResponse schema from swagger
/// </summary>
public class CreateAddressResponseDto
{
    public string? Address { get; set; }
    public string? LegacyAddress { get; set; }
    public string? EnterpriseAddress { get; set; }
    public string? Tag { get; set; }
    public int? Bip44AddressIndex { get; set; }
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
