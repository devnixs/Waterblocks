using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure;
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
    private readonly WorkspaceContext _workspace;

    public VaultWalletsController(
        FireblocksDbContext context,
        ILogger<VaultWalletsController> logger,
        WorkspaceContext workspace)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
    }

    [HttpGet]
    public async Task<ActionResult<VaultAssetDto>> GetWallet(string vaultAccountId, string assetId)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        return Ok(MapToVaultAssetDto(wallet));
    }

    [HttpPost]
    public async Task<ActionResult<CreateVaultAssetResponseDto>> CreateWallet(
        string vaultAccountId,
        string assetId,
        [FromBody] CreateVaultAssetRequestDto? request = null)
    {
        // Verify vault account exists
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var vaultExists = await _context.VaultAccounts.AnyAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);
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
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (existingWallet != null)
        {
            // Return existing wallet
            return Ok(MapToCreateVaultAssetResponseDto(existingWallet, request?.EosAccountName));
        }

        // Create new wallet
        var wallet = new Wallet
        {
            VaultAccountId = vaultAccountId,
            AssetId = assetId,
            Balance = 0,
            LockedAmount = 0,
            Pending = 0,
            Frozen = 0,
            Staked = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // Create initial address
        var address = new Address
        {
            AddressValue = GenerateDepositAddress(assetId, vaultAccountId),
            Type = "Permanent",
            WalletId = wallet.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created wallet for asset {AssetId} in vault {VaultAccountId}", assetId, vaultAccountId);

        // Reload with addresses
        wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstAsync(w => w.Id == wallet.Id);

        return Ok(MapToCreateVaultAssetResponseDto(wallet, request?.EosAccountName));
    }

    [HttpPost("balance")]
    public async Task<ActionResult<VaultAssetDto>> RefreshBalance(string vaultAccountId, string assetId)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        var wallet = await _context.Wallets
            .Include(w => w.VaultAccount)
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == assetId && w.VaultAccount.WorkspaceId == _workspace.WorkspaceId);

        if (wallet == null)
        {
            throw new KeyNotFoundException($"Wallet for asset {assetId} not found in vault {vaultAccountId}");
        }

        // In a real implementation, this would recalculate balance from transactions
        // For now, just return current balance
        wallet.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Refreshed balance for asset {AssetId} in vault {VaultAccountId}", assetId, vaultAccountId);

        return Ok(MapToVaultAssetDto(wallet));
    }

    private VaultAssetDto MapToVaultAssetDto(Wallet wallet)
    {
        var total = wallet.Balance;
        var available = wallet.Balance - wallet.LockedAmount - wallet.Frozen;

        return new VaultAssetDto
        {
            Id = wallet.AssetId,
            Total = total.ToString("G29"),
            Balance = total.ToString("G29"), // Deprecated field, same as total
            Available = available.ToString("G29"),
            Pending = wallet.Pending.ToString("G29"),
            Frozen = wallet.Frozen.ToString("G29"),
            LockedAmount = wallet.LockedAmount.ToString("G29"),
            Staked = wallet.Staked.ToString("G29"),
            TotalStakedCPU = null,
            TotalStakedNetwork = null,
            SelfStakedCPU = null,
            SelfStakedNetwork = null,
            PendingRefundCPU = null,
            PendingRefundNetwork = null,
            BlockHeight = wallet.BlockHeight,
            BlockHash = wallet.BlockHash,
            AllocatedBalances = null
        };
    }

    private CreateVaultAssetResponseDto MapToCreateVaultAssetResponseDto(Wallet wallet, string? eosAccountName)
    {
        var primaryAddress = wallet.Addresses.FirstOrDefault();

        return new CreateVaultAssetResponseDto
        {
            Id = wallet.AssetId,
            Address = primaryAddress?.AddressValue,
            LegacyAddress = null,
            EnterpriseAddress = null,
            Tag = primaryAddress?.Tag,
            EosAccountName = eosAccountName,
            Status = "READY",
            ActivationTxId = null
        };
    }

    private static string GenerateDepositAddress(string assetId, string vaultAccountId)
    {
        // Generate different address formats based on asset type
        return assetId.ToUpperInvariant() switch
        {
            "BTC" => $"bc1q{Guid.NewGuid():N}"[..42],
            "ETH" or "USDT" or "USDC" => $"0x{Guid.NewGuid():N}{Guid.NewGuid():N}"[..42],
            _ => $"{assetId.ToLowerInvariant()}_{vaultAccountId[..Math.Min(8, vaultAccountId.Length)]}_{Guid.NewGuid():N}"
        };
    }
}
