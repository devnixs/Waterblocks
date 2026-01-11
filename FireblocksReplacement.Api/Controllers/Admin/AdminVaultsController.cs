using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Dtos.Admin;

namespace FireblocksReplacement.Api.Controllers.Admin;

[ApiController]
[Route("admin/vaults")]
public class AdminVaultsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminVaultsController> _logger;

    public AdminVaultsController(FireblocksDbContext context, ILogger<AdminVaultsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminVaultDto>>>> GetVaults()
    {
        var vaults = await _context.VaultAccounts
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();

        var dtos = vaults.Select(MapToDto).ToList();
        return Ok(AdminResponse<List<AdminVaultDto>>.Success(dtos));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdminResponse<AdminVaultDto>>> GetVault(string id)
    {
        var vault = await _context.VaultAccounts
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vault == null)
        {
            return NotFound(AdminResponse<AdminVaultDto>.Failure(
                $"Vault {id} not found",
                "VAULT_NOT_FOUND"));
        }

        return Ok(AdminResponse<AdminVaultDto>.Success(MapToDto(vault)));
    }

    [HttpPost]
    public async Task<ActionResult<AdminResponse<AdminVaultDto>>> CreateVault(
        [FromBody] CreateAdminVaultRequestDto request)
    {
        var vault = new VaultAccount
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            CustomerRefId = request.CustomerRefId,
            AutoFuel = request.AutoFuel,
            HiddenOnUI = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.VaultAccounts.Add(vault);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created vault {VaultId} with name {Name}",
            vault.Id, vault.Name);

        // Reload with wallets
        vault = await _context.VaultAccounts
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstAsync(v => v.Id == vault.Id);

        return Ok(AdminResponse<AdminVaultDto>.Success(MapToDto(vault)));
    }

    [HttpGet("{id}/frozen")]
    public async Task<ActionResult<AdminResponse<List<FrozenBalanceDto>>>> GetFrozenBalances(string id)
    {
        var vault = await _context.VaultAccounts
            .Include(v => v.Wallets)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vault == null)
        {
            return NotFound(AdminResponse<List<FrozenBalanceDto>>.Failure(
                $"Vault {id} not found",
                "VAULT_NOT_FOUND"));
        }

        var frozenBalances = vault.Wallets
            .Where(w => w.LockedAmount > 0)
            .Select(w => new FrozenBalanceDto
            {
                AssetId = w.AssetId,
                Amount = w.LockedAmount.ToString("F18")
            })
            .ToList();

        return Ok(AdminResponse<List<FrozenBalanceDto>>.Success(frozenBalances));
    }

    private AdminVaultDto MapToDto(VaultAccount vault)
    {
        return new AdminVaultDto
        {
            Id = vault.Id,
            Name = vault.Name,
            HiddenOnUI = vault.HiddenOnUI,
            CustomerRefId = vault.CustomerRefId,
            AutoFuel = vault.AutoFuel,
            Wallets = vault.Wallets.Select(w => new AdminWalletDto
            {
                AssetId = w.AssetId,
                Balance = w.Balance.ToString("F18"),
                LockedAmount = w.LockedAmount.ToString("F18"),
                Available = (w.Balance - w.LockedAmount).ToString("F18"),
                AddressCount = w.Addresses.Count
            }).ToList(),
            CreatedAt = vault.CreatedAt,
            UpdatedAt = vault.UpdatedAt
        };
    }
}
