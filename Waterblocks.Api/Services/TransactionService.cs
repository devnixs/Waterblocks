using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Dtos.Fireblocks;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface ITransactionService
{
    Task<CreateTransactionResponseDto> CreateTransactionAsync(CreateTransactionRequestDto request);
}

public sealed class TransactionService : ITransactionService
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<TransactionService> _logger;
    private readonly WorkspaceContext _workspace;
    private readonly IBalanceService _balanceService;
    private readonly IRealtimeNotifier _realtimeNotifier;
    private readonly ITransactionFactory _transactionFactory;

    public TransactionService(
        FireblocksDbContext context,
        ILogger<TransactionService> logger,
        WorkspaceContext workspace,
        IBalanceService balanceService,
        IRealtimeNotifier realtimeNotifier,
        ITransactionFactory transactionFactory)
    {
        _context = context;
        _logger = logger;
        _workspace = workspace;
        _balanceService = balanceService;
        _realtimeNotifier = realtimeNotifier;
        _transactionFactory = transactionFactory;
    }

    public async Task<CreateTransactionResponseDto> CreateTransactionAsync(CreateTransactionRequestDto request)
    {
        if (string.IsNullOrEmpty(_workspace.WorkspaceId))
        {
            throw new UnauthorizedAccessException("Workspace is required");
        }

        if (request.Source == null)
        {
            throw new ArgumentException("Source is required");
        }

        if (string.IsNullOrEmpty(request.AssetId))
        {
            throw new ArgumentException("AssetId is required");
        }

        if (request.Source.Type == TransferPeerType.EXCHANGE_ACCOUNT ||
            request.Destination?.Type == TransferPeerType.EXCHANGE_ACCOUNT)
        {
            throw new ArgumentException(
                "EXCHANGE_ACCOUNT peers are not supported here. Use /off_exchange/add or /off_exchange/remove");
        }

        var vaultAccountId = request.Source.Id;
        var vaultAccount = await _context.VaultAccounts
            .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == _workspace.WorkspaceId);
        if (vaultAccount == null)
        {
            throw new KeyNotFoundException($"Vault account {vaultAccountId} not found");
        }

        var asset = await _transactionFactory.RequireAssetAsync(request.AssetId);
        var requestedAmount = _transactionFactory.ParseRequestedAmount(request.Amount);
        await _transactionFactory.EnsureExternalTxIdIsFreeAsync(request.ExternalTxId);

        // Get source address from source vault
        var sourceAddress = await _transactionFactory.RequireSourceAddressAsync(
            vaultAccountId,
            asset,
            _workspace.WorkspaceId);

        // Get or validate destination address
        var destinationAddress = string.Empty;
        var destinationVaultId = string.Empty;
        var destinationWorkspaceId = string.Empty;
        var destinationTag = request.Destination?.OneTimeAddress?.Tag?.Trim();

        if (request.Destination?.Type == TransferPeerType.VAULT_ACCOUNT && !string.IsNullOrEmpty(request.Destination.Id))
        {
            // Destination is a vault
            destinationVaultId = request.Destination.Id;
            var destinationWallet = await _transactionFactory.GetOrCreateDestinationWalletAsync(
                destinationVaultId,
                asset,
                _workspace.WorkspaceId);

            destinationWorkspaceId = destinationWallet.VaultAccount?.WorkspaceId ?? string.Empty;

            // Check if specific address is provided via OneTimeAddress (override)
            var explicitAddress = request.Destination.OneTimeAddress?.Address;
            if (!string.IsNullOrWhiteSpace(explicitAddress))
            {
                // Validate it belongs to this vault
                var canonicalAddress = destinationWallet.Addresses
                    .FirstOrDefault(a => AddressComparison.Equals(a.AddressValue, explicitAddress, asset.IsCaseSensitive))
                    ?.AddressValue;

                if (canonicalAddress == null)
                {
                    throw new ArgumentException(
                        $"Address {explicitAddress} does not belong to vault {destinationVaultId}");
                }

                destinationAddress = canonicalAddress;
            }
            else
            {
                // Use first address if not specified (default behavior)
                destinationAddress = destinationWallet.Addresses.First().AddressValue;
            }

            if (asset.BlockchainType == BlockchainType.MemoBased && string.IsNullOrWhiteSpace(destinationTag))
            {
                destinationTag = destinationWallet.Addresses.FirstOrDefault()?.Tag;
            }
        }
        else
        {
            // Destination is external address
            destinationAddress = request.Destination?.OneTimeAddress?.Address ?? string.Empty;
            if (!string.IsNullOrEmpty(destinationAddress) && !ValidateAddressFormat(request.AssetId, destinationAddress))
            {
                throw new ArgumentException($"Invalid destination address format for asset {request.AssetId}");
            }

            destinationAddress = await ResolveCanonicalKnownAddressAsync(asset, destinationAddress);
        }

        // Final validation: ensure destination address is not empty
        if (string.IsNullOrWhiteSpace(destinationAddress))
        {
            throw new ArgumentException("Destination address is required");
        }

        if (asset.BlockchainType == BlockchainType.MemoBased && string.IsNullOrWhiteSpace(destinationTag))
        {
            destinationTag = null;
        }

        // Final validation: ensure destination vault ID is set if type is VAULT_ACCOUNT
        if (request.Destination?.Type == TransferPeerType.VAULT_ACCOUNT && string.IsNullOrWhiteSpace(destinationVaultId))
        {
            throw new ArgumentException("Destination vault ID is required when destination type is VAULT_ACCOUNT");
        }

        var treatAsGrossAmount = request.TreatAsGrossAmount ?? false;
        var amounts = _transactionFactory.CalculateAmounts(asset, requestedAmount, treatAsGrossAmount);

        var transaction = _transactionFactory.Create(new TransactionDraft
        {
            VaultAccountId = vaultAccountId,
            WorkspaceId = _workspace.WorkspaceId,
            Asset = asset,
            SourceAddress = sourceAddress,
            DestinationAddress = destinationAddress,
            DestinationTag = destinationTag,
            Amounts = amounts,
            TreatAsGrossAmount = treatAsGrossAmount,
            Note = request.Note,
            ExternalTxId = request.ExternalTxId,
            CustomerRefId = request.CustomerRefId,
            Operation = request.Operation,
            InitiatedBy = _workspace.ApiKey,
        });

        var reserveResult = await _balanceService.ReserveFundsAsync(transaction);
        if (!reserveResult.Success)
        {
            throw new InvalidOperationException(reserveResult.ErrorMessage);
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        await _realtimeNotifier.NotifyTransactionCreatedAsync(_workspace.WorkspaceId, destinationWorkspaceId);

        _logger.LogInformation("Created transaction {TransactionId} for {Amount} {AssetId} (requested: {RequestedAmount}, fee: {Fee} {FeeCurrency}) from vault {VaultAccountId}",
            transaction.Id, amounts.TransferAmount, request.AssetId, requestedAmount, amounts.NetworkFee, amounts.FeeCurrency, vaultAccountId);

        return new CreateTransactionResponseDto
        {
            Id = TransactionCompositeId.Build(_workspace.WorkspaceId, transaction.Id),
            Status = transaction.State.ToString(),
        };
    }

    private static bool ValidateAddressFormat(string assetId, string address)
    {
        return assetId switch
        {
            "BTC" => address.StartsWith("bc1") || address.StartsWith("1") || address.StartsWith("3"),
            "ETH" or "USDT" or "USDC" => address.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && address.Length == 42,
            _ => !string.IsNullOrWhiteSpace(address),
        };
    }

    private async Task<string> ResolveCanonicalKnownAddressAsync(Asset asset, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        var normalizedAddress = AddressComparison.Normalize(address, asset.IsCaseSensitive);
        var blockchainId = asset.NativeAsset ?? asset.AssetId;

        var addresses = _context.Addresses
            .Include(a => a.Wallet)
            .Join(
                _context.Assets.Where(a => a.AssetId == blockchainId || a.NativeAsset == blockchainId),
                addressEntity => addressEntity.Wallet.AssetId,
                chainAsset => chainAsset.AssetId,
                (addressEntity, _) => addressEntity);

        var canonicalAddress = asset.IsCaseSensitive
            ? await addresses
                .Where(a => a.AddressValue == address)
                .Select(a => a.AddressValue)
                .FirstOrDefaultAsync()
            : await addresses
                .Where(a => a.AddressValue.ToLower() == normalizedAddress)
                .Select(a => a.AddressValue)
                .FirstOrDefaultAsync();

        return canonicalAddress ?? address;
    }
}
