using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;

namespace Waterblocks.Api.Controllers.Admin;

[ApiController]
[Route("admin/vaults")]
public class AdminVaultsController : AdminControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminVaultsController> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly Waterblocks.Api.Services.IAddressGenerator _addressGenerator;

    public AdminVaultsController(
        FireblocksDbContext context,
        ILogger<AdminVaultsController> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace,
        Waterblocks.Api.Services.IAddressGenerator addressGenerator)
        : base(workspace)
    {
        _context = context;
        _logger = logger;
        _hub = hub;
        _addressGenerator = addressGenerator;
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
        if (string.IsNullOrEmpty(Workspace.WorkspaceId))
        {
            return WorkspaceRequired<AdminWalletDto>();
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == Workspace.WorkspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vault == null)
        {
            return NotFound(AdminResponse<AdminWalletDto>.Failure(
                $"Vault {id} not found",
                "VAULT_NOT_FOUND"));
        }

        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            return BadRequest(AdminResponse<AdminWalletDto>.Failure(
                $"Asset {request.AssetId} not found",
                "ASSET_NOT_FOUND"));
        }

        // For AccountBased and MemoBased assets, return existing wallet if one exists
        if (asset.BlockchainType != BlockchainType.AddressBased)
        {
            var existingWallet = vault.Wallets.FirstOrDefault(w => w.AssetId == request.AssetId);
            if (existingWallet != null)
            {
                var existingDto = new AdminWalletDto
                {
                    Id = existingWallet.Id,
                    AssetId = existingWallet.AssetId,
                    Type = existingWallet.Type,
                    Balance = existingWallet.Balance.ToString("F18"),
                    LockedAmount = existingWallet.LockedAmount.ToString("F18"),
                    Pending = existingWallet.Pending.ToString("F18"),
                    Available = (existingWallet.Balance - existingWallet.Pending).ToString("F18"),
                    AddressCount = existingWallet.Addresses.Count,
                    DepositAddress = existingWallet.Addresses.FirstOrDefault()?.AddressValue,
                };
                return Ok(AdminResponse<AdminWalletDto>.Success(existingDto));
            }
        }

        // Check if this is the first wallet for this asset (for AddressBased/UTXO assets)
        var existingWalletCount = vault.Wallets.Count(w => w.AssetId == request.AssetId);
        var walletType = existingWalletCount == 0 ? "Permanent" : "UTXO";

        // Create new wallet
        var wallet = new Wallet
        {
            VaultAccountId = vault.Id,
            AssetId = request.AssetId,
            Type = walletType,
            Balance = 0,
            LockedAmount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        if (wallet.Addresses.Count == 0)
        {
            var address = new Address
            {
                AddressValue = _addressGenerator.GenerateAdminDepositAddress(request.AssetId, vault.Id),
                Type = "Permanent",
                WalletId = wallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();
            wallet.Addresses.Add(address);
        }

        var dto = new AdminWalletDto
        {
            Id = wallet.Id,
            AssetId = wallet.AssetId,
            Type = wallet.Type,
            Balance = wallet.Balance.ToString("F18"),
            LockedAmount = wallet.LockedAmount.ToString("F18"),
            Pending = wallet.Pending.ToString("F18"),
            Available = (wallet.Balance - wallet.Pending).ToString("F18"),
            AddressCount = wallet.Addresses.Count,
            DepositAddress = wallet.Addresses.FirstOrDefault()?.AddressValue,
        };

        var updatedVault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == Workspace.WorkspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstAsync(v => v.Id == vault.Id);
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("vaultUpserted", MapToDto(updatedVault));
        await _hub.Clients.Group(Workspace.WorkspaceId!).SendAsync("vaultsUpdated");
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
