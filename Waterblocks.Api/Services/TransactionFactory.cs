using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services;

public sealed record TransferAmounts(
    decimal RequestedAmount,
    decimal TransferAmount,
    decimal NetworkFee,
    string FeeCurrency);

public sealed class TransactionDraft
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public required string VaultAccountId { get; init; }
    public required string WorkspaceId { get; init; }
    public required Asset Asset { get; init; }
    public required string SourceAddress { get; init; }
    public required string DestinationAddress { get; init; }
    public string? DestinationTag { get; init; }
    public required TransferAmounts Amounts { get; init; }
    public bool TreatAsGrossAmount { get; init; }
    public string? Note { get; init; }
    public string? ExternalTxId { get; init; }
    public string? CustomerRefId { get; init; }
    public string? Operation { get; init; }
    public string? InitiatedBy { get; init; }
}

/// <summary>
/// Holds the transaction rules that POST /transactions and the collateral calls share.
/// </summary>
public interface ITransactionFactory
{
    decimal ParseRequestedAmount(string? amount);
    Task<Asset> RequireAssetAsync(string assetId);
    Task EnsureExternalTxIdIsFreeAsync(string? externalTxId);
    Task<string> RequireSourceAddressAsync(string vaultAccountId, Asset asset, string workspaceId);
    Task<Wallet> GetOrCreateDestinationWalletAsync(string vaultAccountId, Asset asset, string workspaceId);
    TransferAmounts CalculateAmounts(Asset asset, decimal requestedAmount, bool treatAsGrossAmount);
    Transaction Create(TransactionDraft draft);
}

public sealed class TransactionFactory : ITransactionFactory
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<TransactionFactory> _logger;
    private readonly IWalletAddressService _walletAddressService;

    public TransactionFactory(
        FireblocksDbContext context,
        ILogger<TransactionFactory> logger,
        IWalletAddressService walletAddressService)
    {
        _context = context;
        _logger = logger;
        _walletAddressService = walletAddressService;
    }

    public decimal ParseRequestedAmount(string? amount)
    {
        if (!decimal.TryParse(amount, out var requestedAmount) || requestedAmount <= 0)
        {
            throw new ArgumentException("Invalid amount");
        }

        return requestedAmount;
    }

    public async Task<Asset> RequireAssetAsync(string assetId)
    {
        var asset = await _context.Assets.FindAsync(assetId);
        if (asset == null)
        {
            throw new KeyNotFoundException($"Asset {assetId} not found");
        }

        return asset;
    }

    public async Task EnsureExternalTxIdIsFreeAsync(string? externalTxId)
    {
        if (string.IsNullOrWhiteSpace(externalTxId))
        {
            return;
        }

        var exists = await _context.Transactions
            .AnyAsync(t => t.ExternalTxId == externalTxId);
        if (exists)
        {
            throw new DuplicateExternalTxIdException(externalTxId);
        }
    }

    public async Task<string> RequireSourceAddressAsync(string vaultAccountId, Asset asset, string workspaceId)
    {
        var sourceWallet = await _context.Wallets
            .Include(w => w.Addresses)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == asset.AssetId);

        if (sourceWallet == null)
        {
            throw new KeyNotFoundException($"Wallet for vault {vaultAccountId} and asset {asset.AssetId} not found");
        }

        if (!sourceWallet.Addresses.Any())
        {
            await _walletAddressService.EnsurePrimaryAddressAsync(sourceWallet, asset, workspaceId);
        }

        return sourceWallet.Addresses.First().AddressValue;
    }

    public async Task<Wallet> GetOrCreateDestinationWalletAsync(string vaultAccountId, Asset asset, string workspaceId)
    {
        var destinationWallet = await _context.Wallets
            .Include(w => w.Addresses)
            .Include(w => w.VaultAccount)
            .FirstOrDefaultAsync(w => w.VaultAccountId == vaultAccountId && w.AssetId == asset.AssetId);

        if (destinationWallet == null)
        {
            var destinationVault = await _context.VaultAccounts
                .FirstOrDefaultAsync(v => v.Id == vaultAccountId);
            if (destinationVault == null)
            {
                throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
            }

            destinationWallet = new Wallet
            {
                VaultAccountId = vaultAccountId,
                AssetId = asset.AssetId,
                Type = "Permanent",
                Balance = 0,
                Pending = 0,
                LockedAmount = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                VaultAccount = destinationVault,
            };

            _context.Wallets.Add(destinationWallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created wallet for vault {VaultId} and asset {AssetId}",
                vaultAccountId, asset.AssetId);
        }

        if (!destinationWallet.Addresses.Any())
        {
            await _walletAddressService.EnsurePrimaryAddressAsync(
                destinationWallet,
                asset,
                destinationWallet.VaultAccount?.WorkspaceId ?? workspaceId);
        }

        return destinationWallet;
    }

    public TransferAmounts CalculateAmounts(Asset asset, decimal requestedAmount, bool treatAsGrossAmount)
    {
        var networkFee = asset.BaseFee;
        var feeCurrency = asset.GetFeeAssetId();
        var transferAmount = requestedAmount;

        if (treatAsGrossAmount && feeCurrency == asset.AssetId)
        {
            transferAmount = requestedAmount - networkFee;
            if (transferAmount <= 0)
            {
                throw new ArgumentException($"Amount ({requestedAmount}) must be greater than fee ({networkFee})");
            }
        }

        return new TransferAmounts(requestedAmount, transferAmount, networkFee, feeCurrency);
    }

    public Transaction Create(TransactionDraft draft)
    {
        var now = DateTimeOffset.UtcNow;
        return new Transaction
        {
            Id = draft.Id,
            VaultAccountId = draft.VaultAccountId,
            WorkspaceId = draft.WorkspaceId,
            AssetId = draft.Asset.AssetId,
            SourceAddress = draft.SourceAddress,
            Amount = draft.Amounts.TransferAmount,
            RequestedAmount = draft.Amounts.RequestedAmount,
            NetworkFee = draft.Amounts.NetworkFee,
            Fee = draft.Amounts.NetworkFee,
            DestinationAddress = draft.DestinationAddress,
            DestinationTag = draft.DestinationTag,
            State = TransactionState.SUBMITTED,
            Note = draft.Note,
            ExternalTxId = draft.ExternalTxId,
            CustomerRefId = draft.CustomerRefId,
            Operation = draft.Operation ?? "TRANSFER",
            FeeCurrency = draft.Amounts.FeeCurrency,
            TreatAsGrossAmount = draft.TreatAsGrossAmount,
            CreatedAt = now,
            UpdatedAt = now,
            Hash = TransactionHashGenerator.Generate(draft.Asset.AssetId, draft.Asset.BlockchainType),
            InitiatedBy = draft.InitiatedBy,
        };
    }
}
