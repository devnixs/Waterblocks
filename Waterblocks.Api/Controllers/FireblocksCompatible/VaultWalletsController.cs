using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Dtos.Fireblocks;

namespace Waterblocks.Api.Controllers;

[ApiController]
[Route("vault/accounts/{vaultAccountId}/{assetId}")]
public class VaultWalletsController : ControllerBase
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<VaultWalletsController> _logger;
    private readonly WorkspaceContext _workspace;
    private readonly Waterblocks.Api.Services.IAddressGenerator _addressGenerator;

    public VaultWalletsController(
        FireblocksDbContext context,
        ILogger<VaultWalletsController> logger,
        WorkspaceContext workspace,
        Waterblocks.Api.Services.IAddressGenerator addressGenerator)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
        _addressGenerator = addressGenerator;
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

        // Verify asset exists and get its blockchain type
        var asset = await _context.Assets.FindAsync(assetId);
        if (asset == null)
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
            // For AccountBased and MemoBased assets, return the existing wallet as-is
            if (asset.BlockchainType != BlockchainType.AddressBased)
            {
                return Ok(MapToCreateVaultAssetResponseDto(existingWallet, request?.EosAccountName));
            }

            // For AddressBased/UTXO assets (like BTC), create a new address on the existing wallet
            // This keeps all addresses under one wallet with a shared balance
            var newAddress = new Address
            {
                AddressValue = _addressGenerator.GenerateVaultWalletDepositAddress(assetId, vaultAccountId),
                Type = "Permanent",
                WalletId = existingWallet.Id,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            _context.Addresses.Add(newAddress);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created new address {Address} for AddressBased asset {AssetId} in vault {VaultAccountId}",
                newAddress.AddressValue, assetId, vaultAccountId);

            // Reload with all addresses
            existingWallet = await _context.Wallets
                .Include(w => w.VaultAccount)
                .Include(w => w.Addresses)
                .FirstAsync(w => w.Id == existingWallet.Id);

            return Ok(MapToCreateVaultAssetResponseDto(existingWallet, request?.EosAccountName, newAddress));
        }

        // No existing wallet - create new one
        var wallet = new Wallet
        {
            VaultAccountId = vaultAccountId,
            AssetId = assetId,
            Type = "Permanent",
            Balance = 0,
            LockedAmount = 0,
            Pending = 0,
            Frozen = 0,
            Staked = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        // For account-based blockchains, reuse the address from another wallet on the same blockchain
        // E.g., USDC and ETH should share the same address since they're both on Ethereum
        string addressValue;
        if (asset.BlockchainType == BlockchainType.AccountBased || asset.BlockchainType == BlockchainType.MemoBased)
        {
            var blockchainId = asset.NativeAsset ?? asset.AssetId;
            var existingAddress = await FindExistingBlockchainAddressAsync(vaultAccountId, blockchainId, assetId, _workspace.WorkspaceId!);
            addressValue = existingAddress ?? _addressGenerator.GenerateVaultWalletDepositAddress(assetId, vaultAccountId);
        }
        else
        {
            addressValue = _addressGenerator.GenerateVaultWalletDepositAddress(assetId, vaultAccountId);
        }

        // Create initial address
        var address = new Address
        {
            AddressValue = addressValue,
            Type = "Permanent",
            WalletId = wallet.Id,
            CreatedAt = DateTimeOffset.UtcNow,
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
            TotalStakedCPU = string.Empty,
            TotalStakedNetwork = string.Empty,
            SelfStakedCPU = string.Empty,
            SelfStakedNetwork = string.Empty,
            PendingRefundCPU = string.Empty,
            PendingRefundNetwork = string.Empty,
            BlockHeight = wallet.BlockHeight ?? "100",
            BlockHash = wallet.BlockHash ?? "xxyy",
            AllocatedBalances = new List<AllocatedBalanceDto>(),
        };
    }

    private CreateVaultAssetResponseDto MapToCreateVaultAssetResponseDto(
        Wallet wallet,
        string? eosAccountName,
        Address? specificAddress = null)
    {
        // Use specific address if provided (e.g., newly created address for UTXO asset)
        // Otherwise, use the first address in the wallet
        var addressToReturn = specificAddress ?? wallet.Addresses.FirstOrDefault();

        return new CreateVaultAssetResponseDto
        {
            Id = wallet.AssetId,
            Address = addressToReturn?.AddressValue ?? string.Empty,
            LegacyAddress = addressToReturn?.LegacyAddress ?? string.Empty,
            EnterpriseAddress = addressToReturn?.EnterpriseAddress ?? string.Empty,
            Tag = addressToReturn?.Tag ?? string.Empty,
            EosAccountName = eosAccountName ?? string.Empty,
            Status = "READY",
            ActivationTxId = string.Empty,
        };
    }

    /// <summary>
    /// Finds an existing address on the same blockchain within the vault.
    /// For account-based blockchains, all assets on the same chain should share one address.
    /// </summary>
    private async Task<string?> FindExistingBlockchainAddressAsync(
        string vaultAccountId,
        string blockchainId,
        string excludeAssetId,
        string workspaceId)
    {
        // Find wallets in this vault for assets on the same blockchain
        var walletsOnSameBlockchain = await _context.Wallets
            .Include(w => w.Addresses)
            .Include(w => w.VaultAccount)
            .Where(w => w.VaultAccountId == vaultAccountId
                     && w.AssetId != excludeAssetId
                     && w.VaultAccount.WorkspaceId == workspaceId)
            .Join(
                _context.Assets.Where(a =>
                    a.AssetId == blockchainId || // The native asset itself (e.g., ETH)
                    a.NativeAsset == blockchainId), // Tokens on this blockchain (e.g., USDC, USDT)
                w => w.AssetId,
                a => a.AssetId,
                (w, a) => w)
            .ToListAsync();

        // Return the first address found on this blockchain
        return walletsOnSameBlockchain
            .SelectMany(w => w.Addresses)
            .FirstOrDefault()?.AddressValue;
    }

}
