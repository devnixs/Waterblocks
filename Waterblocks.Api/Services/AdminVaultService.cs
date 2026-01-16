using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminVaultService
{
    Task<AdminServiceResult<AdminWalletDto>> CreateWalletAsync(string id, CreateAdminWalletRequestDto request);
}

public sealed class AdminVaultService : AdminServiceBase, IAdminVaultService
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<AdminVaultService> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly IAddressGenerator _addressGenerator;

    public AdminVaultService(
        FireblocksDbContext context,
        ILogger<AdminVaultService> logger,
        IHubContext<AdminHub> hub,
        WorkspaceContext workspace,
        IAddressGenerator addressGenerator)
        : base(workspace)
    {
        _context = context;
        _logger = logger;
        _hub = hub;
        _addressGenerator = addressGenerator;
    }

    public async Task<AdminServiceResult<AdminWalletDto>> CreateWalletAsync(string id, CreateAdminWalletRequestDto request)
    {
        if (!TryGetWorkspaceId<AdminWalletDto>(out var workspaceId, out var failure))
        {
            return failure;
        }

        var vault = await _context.VaultAccounts
            .Where(v => v.WorkspaceId == workspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstOrDefaultAsync(v => v.Id == id);

        if (vault == null)
        {
            return NotFound<AdminWalletDto>($"Vault {id} not found", "VAULT_NOT_FOUND");
        }

        var asset = await _context.Assets.FindAsync(request.AssetId);
        if (asset == null)
        {
            return Failure<AdminWalletDto>($"Asset {request.AssetId} not found", "ASSET_NOT_FOUND");
        }

        if (asset.BlockchainType != BlockchainType.AddressBased)
        {
            var existingWallet = vault.Wallets.FirstOrDefault(w => w.AssetId == request.AssetId);
            if (existingWallet != null)
            {
                return Success(new AdminWalletDto
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
                });
            }
        }

        var existingWalletCount = vault.Wallets.Count(w => w.AssetId == request.AssetId);
        var walletType = existingWalletCount == 0 ? "Permanent" : "UTXO";

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
            .Where(v => v.WorkspaceId == workspaceId)
            .Include(v => v.Wallets)
                .ThenInclude(w => w.Addresses)
            .FirstAsync(v => v.Id == vault.Id);

        await _hub.Clients.Group(workspaceId).SendAsync("vaultUpserted", MapToDto(updatedVault));
        await _hub.Clients.Group(workspaceId).SendAsync("vaultsUpdated");

        _logger.LogInformation("Created wallet for asset {AssetId} in vault {VaultId}", request.AssetId, vault.Id);

        return Success(dto);
    }

    private static AdminVaultDto MapToDto(VaultAccount vault)
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
