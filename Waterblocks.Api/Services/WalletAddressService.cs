using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IWalletAddressService
{
    Task<Address> EnsurePrimaryAddressAsync(Wallet wallet, Asset asset, string? workspaceId);
    Task<Address> CreateAddressAsync(Wallet wallet, Asset asset, string? workspaceId, string? description, string? customerRefId);
}

public sealed class WalletAddressService : IWalletAddressService
{
    private readonly FireblocksDbContext _context;
    private readonly IAddressGenerator _addressGenerator;
    private readonly ILogger<WalletAddressService> _logger;

    public WalletAddressService(
        FireblocksDbContext context,
        IAddressGenerator addressGenerator,
        ILogger<WalletAddressService> logger)
    {
        _context = context;
        _addressGenerator = addressGenerator;
        _logger = logger;
    }

    public async Task<Address> EnsurePrimaryAddressAsync(Wallet wallet, Asset asset, string? workspaceId)
    {
        var existing = wallet.Addresses.FirstOrDefault();
        if (existing != null)
        {
            return existing;
        }

        var addressValue = await ResolveAddressValueAsync(asset, wallet.VaultAccountId, asset.AssetId, workspaceId);
        var tag = asset.BlockchainType == BlockchainType.MemoBased
            ? await GenerateUniqueMemoTagAsync(wallet)
            : null;
        var address = CreateAddressEntity(wallet, addressValue, "Permanent", null, null, null, null, tag);

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();
        wallet.Addresses.Add(address);

        _logger.LogInformation(
            "Created primary address {Address} for asset {AssetId} in vault {VaultAccountId}",
            addressValue, asset.AssetId, wallet.VaultAccountId);

        return address;
    }

    public async Task<Address> CreateAddressAsync(
        Wallet wallet,
        Asset asset,
        string? workspaceId,
        string? description,
        string? customerRefId)
    {
        if (asset.BlockchainType == BlockchainType.MemoBased)
        {
            var primary = await EnsurePrimaryAddressAsync(wallet, asset, workspaceId);
            var memoTag = await GenerateUniqueMemoTagAsync(wallet);
            var memoAddress = CreateAddressEntity(
                wallet,
                primary.AddressValue,
                "DEPOSIT",
                description,
                customerRefId,
                null,
                null,
                memoTag);

            _context.Addresses.Add(memoAddress);
            await _context.SaveChangesAsync();
            wallet.Addresses.Add(memoAddress);

            _logger.LogInformation(
                "Created memo-based address {Address} with tag {Tag} for asset {AssetId} in vault {VaultAccountId}",
                primary.AddressValue, memoTag, asset.AssetId, wallet.VaultAccountId);

            return memoAddress;
        }

        if (asset.BlockchainType != BlockchainType.AddressBased)
        {
            return await EnsurePrimaryAddressAsync(wallet, asset, workspaceId);
        }

        var bip44AddressIndex = wallet.Addresses.Count;
        var generatedAddress = _addressGenerator.GenerateVaultAddress(asset.AssetId, bip44AddressIndex);
        var type = bip44AddressIndex == 0 ? "Permanent" : "DEPOSIT";

        var address = CreateAddressEntity(
            wallet,
            generatedAddress.AddressValue,
            type,
            description,
            customerRefId,
            bip44AddressIndex,
            generatedAddress,
            null);

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync();
        wallet.Addresses.Add(address);

        _logger.LogInformation(
            "Created address {Address} for asset {AssetId} in vault {VaultAccountId}",
            generatedAddress.AddressValue, asset.AssetId, wallet.VaultAccountId);

        return address;
    }

    private async Task<string> ResolveAddressValueAsync(
        Asset asset,
        string vaultAccountId,
        string assetId,
        string? workspaceId)
    {
        // For account-based blockchains, reuse the address from another wallet on the same blockchain
        // E.g., USDC and ETH should share the same address since they're both on Ethereum
        if (asset.BlockchainType == BlockchainType.AccountBased || asset.BlockchainType == BlockchainType.MemoBased)
        {
            var blockchainId = asset.NativeAsset ?? asset.AssetId;
            var existingAddress = await FindExistingBlockchainAddressAsync(vaultAccountId, blockchainId, assetId, workspaceId);
            return existingAddress ?? _addressGenerator.GenerateVaultWalletDepositAddress(assetId, vaultAccountId);
        }

        return _addressGenerator.GenerateVaultWalletDepositAddress(assetId, vaultAccountId);
    }

    private async Task<string?> FindExistingBlockchainAddressAsync(
        string vaultAccountId,
        string blockchainId,
        string excludeAssetId,
        string? workspaceId)
    {
        var wallets = _context.Wallets
            .Include(w => w.Addresses)
            .Include(w => w.VaultAccount)
            .Where(w => w.VaultAccountId == vaultAccountId && w.AssetId != excludeAssetId);

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            wallets = wallets.Where(w => w.VaultAccount.WorkspaceId == workspaceId);
        }

        var walletsOnSameBlockchain = await wallets
            .Join(
                _context.Assets.Where(a =>
                    a.AssetId == blockchainId ||
                    a.NativeAsset == blockchainId),
                w => w.AssetId,
                a => a.AssetId,
                (w, a) => w)
            .ToListAsync();

        return walletsOnSameBlockchain
            .SelectMany(w => w.Addresses)
            .FirstOrDefault()?.AddressValue;
    }

    private static Address CreateAddressEntity(
        Wallet wallet,
        string addressValue,
        string type,
        string? description,
        string? customerRefId,
        int? bip44AddressIndex,
        AddressGenerationResult? generatedAddress,
        string? tag)
    {
        return new Address
        {
            AddressValue = addressValue,
            Tag = tag ?? generatedAddress?.Tag,
            Type = type,
            Description = description,
            CustomerRefId = customerRefId,
            AddressFormat = generatedAddress?.AddressFormat,
            LegacyAddress = generatedAddress?.LegacyAddress,
            EnterpriseAddress = generatedAddress?.EnterpriseAddress,
            Bip44AddressIndex = bip44AddressIndex,
            WalletId = wallet.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task<string> GenerateUniqueMemoTagAsync(Wallet wallet)
    {
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var tag = _addressGenerator.GenerateMemoTag();
            var exists = await _context.Addresses.AnyAsync(a => a.WalletId == wallet.Id && a.Tag == tag);
            if (!exists)
            {
                return tag;
            }
        }

        return _addressGenerator.GenerateMemoTag();
    }
}
