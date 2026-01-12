using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Services;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/vaults")]
public class AdminVaultsController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminVaultsController> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly IAdminVaultService _vaultService;

    public AdminVaultsController(
        FireblocksDbContext context,
        ILogger<AdminVaultsController> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace,
        IAdminVaultService vaultService)
        : base(workspace)
    {
        _context = context;
        _logger = logger;
        _hub = hub;
        _vaultService = vaultService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminResponse<List<AdminVaultDto>>>> GetVaults()
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<List<AdminVaultDto>>();
        }

        var vaults = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == Workspace.WorkspaceId)
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
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminVaultDto>();
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == Workspace.WorkspaceId)
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
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminVaultDto>();
        }

        var vault = new VaultAccount
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            CustomerRefId = request.CustomerRefId,
            AutoFuel = request.AutoFuel,
            HiddenOnUI = false,
            WorkspaceId = Workspace.WorkspaceId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.VaultAccounts.Add(vault);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created vault {VaultId} with name {Name}",
            vault.Id, vault.Name);

        // Reload with wallets
        vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == Workspace.WorkspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstAsync(v => v.Id == vault.Id);

        var dto = MapToDto(vault);
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("vaultUpserted", dto);
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("vaultsUpdated");
        return Ok(AdminResponse<AdminVaultDto>.Success(dto));
    }

    [HttpGet("{id}/frozen")]
    public async Task<ActionResult<AdminResponse<List<FrozenBalanceDto>>>> GetFrozenBalances(string id)
    {
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<List<FrozenBalanceDto>>();
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == Workspace.WorkspaceId)
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
                Amount = w.LockedAmount.ToString("F18"),
            })
            .ToList();

        return Ok(AdminResponse<List<FrozenBalanceDto>>.Success(frozenBalances));
    }

    [HttpPost("{id}/wallets")]
    public async Task<ActionResult<AdminResponse<AdminWalletDto>>> CreateWallet(
        string id,
        [FromBody] CreateAdminWalletRequestDto request)
    {
        return (await _vaultService.CreateWalletAsync(id, request)).ToActionResult(this);
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
                Id = w.Id,
                AssetId = w.AssetId,
                Type = w.Type,
                Balance = w.Balance.ToString("F18"),
                LockedAmount = w.LockedAmount.ToString("F18"),
                Pending = w.Pending.ToString("F18"),
                Available = (w.Balance - w.Pending).ToString("F18"),
                AddressCount = w.Addresses.Count,
                DepositAddress = w.Addresses.FirstOrDefault()?.AddressValue,
            }).ToList(),
            CreatedAt = vault.CreatedAt,
            UpdatedAt = vault.UpdatedAt,
        };
    }

}
