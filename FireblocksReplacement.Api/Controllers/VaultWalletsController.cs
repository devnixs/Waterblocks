using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Dtos.Fireblocks;

namespace FireblocksReplacement.Api.Controllers;

[ApiController]
[Route("vault/accounts/{vaultAccountId}/{assetId}")]
public class VaultWalletsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<VaultWalletsController> _logger;

    public VaultWalletsController(FireblocksDbContext context, ILogger<VaultWalletsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<WalletAssetDto>> GetWallet(string vaultAccountId, string assetId)
    {
        var wallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        var dto = new WalletAssetDto
        {
            Id = wallet.AssetId,
            Balance = wallet.Balance.ToString("F18"),
            LockedAmount = wallet.LockedAmount.ToString("F18"),
            Available = (wallet.Balance - wallet.LockedAmount).ToString("F18"),
            Addresses = wallet.Addresses.Select(a => new AddressDto
            {
                Address = a.AddressValue,
                Tag = a.Tag,
                Type = a.Type
            }).ToList()
        };

        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<WalletAssetDto>> CreateWallet(string vaultAccountId, string assetId)
    {
        // Verify vault account exists
        var vaultExists = await _context.VaultAccounts.AnyAsync(v => v.Id == vaultAccountId);
        if (!vaultExists)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        // Verify asset exists
        var assetExists = await _context.Assets.AnyAsync(a => a.AssetId == assetId);
        if (!assetExists)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        // Check if wallet already exists
        var existingWallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (existingWallet != null)
        {
            // Return existing wallet
            return Ok(MapToDto(existingWallet));
        }

        // Create new wallet
        var wallet = new Wallet
        {
            VaultAccountId = vaultAccountId,
            AssetId = assetId,
            Balance = 0,
            LockedAmount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        if (wallet.Addresses.Count == 0)
        {
            var address = new Address
            {
                AddressValue = GenerateDepositAddress(assetId, vaultAccountId),
                Type = "DEPOSIT",
                WalletId = wallet.Id,
                CreatedAt = DateTime.UtcNow
            };
            _context.Addresses.Add(address);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Created wallet for asset {AssetId} in vault {VaultAccountId}", assetId, vaultAccountId);

        // Reload with addresses
        wallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstAsync(w => w.Id == wallet.Id);

        return Ok(MapToDto(wallet));
    }

    [HttpPost("balance")]
    public async Task<ActionResult<WalletAssetDto>> RefreshBalance(string vaultAccountId, string assetId)
    {
        var wallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        // In a real implementation, this would recalculate balance from transactions
        // For now, just return current balance
        wallet.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Refreshed balance for asset {AssetId} in vault {VaultAccountId}", assetId, vaultAccountId);

        return Ok(MapToDto(wallet));
    }

    private WalletAssetDto MapToDto(Wallet wallet)
    {
        return new WalletAssetDto
        {
            Id = wallet.AssetId,
            Balance = wallet.Balance.ToString("F18"),
            LockedAmount = wallet.LockedAmount.ToString("F18"),
            Available = (wallet.Balance - wallet.LockedAmount).ToString("F18"),
            Addresses = wallet.Addresses.Select(a => new AddressDto
            {
                Address = a.AddressValue,
                Tag = a.Tag,
                Type = a.Type
            }).ToList()
        };
    }

    private static string GenerateDepositAddress(string assetId, string vaultAccountId)
    {
        return $"{assetId.ToLowerInvariant()}_{vaultAccountId[..8]}_{Guid.NewGuid():N}";
    }
}
