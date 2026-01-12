namespace FireblocksReplacement.Api.Dtos.Admin;

public class AdminVaultDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool HiddenOnUI { get; set; }
    public string? CustomerRefId { get; set; }
    public bool AutoFuel { get; set; }
    public List<AdminWalletDto> Wallets { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AdminWalletDto
{
    public int Id { get; set; }
    public string AssetId { get; set; } = string.Empty;
    public string Type { get; set; } = "Permanent";
    public string Balance { get; set; } = "0";
    public string LockedAmount { get; set; } = "0";
    public string Pending { get; set; } = "0";
    public string Available { get; set; } = "0";
    public int AddressCount { get; set; }
    public string? DepositAddress { get; set; }
}

public class CreateAdminVaultRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? CustomerRefId { get; set; }
    public bool AutoFuel { get; set; } = false;
}

public class FrozenBalanceDto
{
    public string AssetId { get; set; } = string.Empty;
    public string Amount { get; set; } = "0";
}

public class CreateAdminWalletRequestDto
{
    public string AssetId { get; set; } = string.Empty;
}
