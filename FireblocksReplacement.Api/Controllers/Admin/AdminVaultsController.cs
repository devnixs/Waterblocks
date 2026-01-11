using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FireblocksReplacement.Api.Infrastructure;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Dtos.Admin;
using FireblocksReplacement.Api.Hubs;

namespace FireblocksReplacement.Api.Controllers.Admin;

[ApiController]
[Route("admin/vaults")]
public class AdminVaultsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminVaultsController> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly WorkspaceContext _workspace;

    public AdminVaultsController(
        FireblocksDbContext context,
        ILogger<AdminVaultsController> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace)
    {
        _context = context;
        _logger = logger;
        _hub = hub;
        _workspace = workspace;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminVaultDto>>>> GetVaults()
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return BadRequest(AdminResponse<List<AdminVaultDto>>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
        }

        var vaults = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
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
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return BadRequest(AdminResponse<AdminVaultDto>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
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
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return BadRequest(AdminResponse<AdminVaultDto>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
        }

        var vault = new VaultAccount
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            CustomerRefId = request.CustomerRefId,
            AutoFuel = request.AutoFuel,
            HiddenOnUI = false,
            WorkspaceId = _workspace.WorkspaceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.VaultAccounts.Add(vault);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created vault {VaultId} with name {Name}",
            vault.Id, vault.Name);

        // Reload with wallets
        vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstAsync(v => v.Id == vault.Id);

        var dto = MapToDto(vault);
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("vaultUpserted", dto);
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("vaultsUpdated");
        return Ok(AdminResponse<AdminVaultDto>.Success(dto));
    }

    [HttpGet("{id}/frozen")]
    public async Task<ActionResult<AdminResponse<List<FrozenBalanceDto>>>> GetFrozenBalances(string id)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return BadRequest(AdminResponse<List<FrozenBalanceDto>>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
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

    [HttpPost("{id}/wallets")]
    public async Task<ActionResult<AdminResponse<AdminWalletDto>>> CreateWallet(
        string id,
        [FromBody] CreateAdminWalletRequestDto request)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            return BadRequest(AdminResponse<AdminWalletDto>.Failure("Workspace is required", "WORKSPACE_REQUIRED"));
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vault == null)
        {
            return NotFound(AdminResponse<AdminWalletDto>.Failure(
                $"Vault {id} not found",
                "VAULT_NOT_FOUND"));
        }

        var assetExists = await _context.Assets.AnyAsync(a => a.AssetId == request.AssetId);
        if (!assetExists)
        {
            return BadRequest(AdminResponse<AdminWalletDto>.Failure(
                $"Asset {request.AssetId} not found",
                "ASSET_NOT_FOUND"));
        }

        var wallet = vault.Wallets.FirstOrDefault(w => w.AssetId == request.AssetId);
        if (wallet == null)
        {
            wallet = new Wallet
            {
                VaultAccountId = vault.Id,
                AssetId = request.AssetId,
                Balance = 0,
                LockedAmount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        if (wallet.Addresses.Count == 0)
        {
            var address = new Address
            {
                AddressValue = GenerateDepositAddress(request.AssetId, vault.Id),
                Type = "DEPOSIT",
                WalletId = wallet.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();
            wallet.Addresses.Add(address);
        }

        var dto = new AdminWalletDto
        {
            AssetId = wallet.AssetId,
            Balance = wallet.Balance.ToString("F18"),
            LockedAmount = wallet.LockedAmount.ToString("F18"),
            Pending = wallet.Pending.ToString("F18"),
            Available = (wallet.Balance - wallet.Pending).ToString("F18"),
            AddressCount = wallet.Addresses.Count,
            DepositAddress = wallet.Addresses.FirstOrDefault()?.AddressValue
        };

        var updatedVault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == _workspace.WorkspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstAsync(v => v.Id == vault.Id);
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("vaultUpserted", MapToDto(updatedVault));
        await _hub.Clients.Group(_workspace.WorkspaceId!).SendAsync("vaultsUpdated");
        return Ok(AdminResponse<AdminWalletDto>.Success(dto));
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
                Pending = w.Pending.ToString("F18"),
                Available = (w.Balance - w.Pending).ToString("F18"),
                AddressCount = w.Addresses.Count,
                DepositAddress = w.Addresses.FirstOrDefault()?.AddressValue
            }).ToList(),
            CreatedAt = vault.CreatedAt,
            UpdatedAt = vault.UpdatedAt
        };
    }

    private static string GenerateDepositAddress(string assetId, string vaultId)
    {
        return $"{assetId.ToLowerInvariant()}_{vaultId[..8]}_{Guid.NewGuid():N}";
    }
}
