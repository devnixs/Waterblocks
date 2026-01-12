namespace Waterblocks.Api.Dtos.Fireblocks;

public class VaultAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool HiddenOnUI { get; set; }
    public string? CustomerRefId { get; set; }
    public bool AutoFuel { get; set; }
    public List<VaultAssetDto> Assets { get; set; } = new();
}

public class VaultAccountsPagedResponseDto
{
    public PagingDto? Paging { get; set; }
    public List<VaultAccountDto> Accounts { get; set; } = new();
}

public class PagingDto
{
    public string? Before { get; set; }
    public string? After { get; set; }
}

public class CreateVaultAccountRequestDto
{
    public string Name { get; set; } = string.Empty;
    public bool? HiddenOnUI { get; set; }
    public string? CustomerRefId { get; set; }
    public bool AutoFuel { get; set; } = false;
}

public class UpdateVaultAccountRequestDto
{
    public string Name { get; set; } = string.Empty;
}
