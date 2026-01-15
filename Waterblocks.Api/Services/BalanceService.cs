using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IBalanceService
{
    /// <summary>
    /// Validates and reserves funds for an outgoing transaction.
    /// Updates the Pending amount for the source wallet.
    /// Also reserves fee in the appropriate wallet (same or different for ERC20).
    /// </summary>
    Task<BalanceResult> ReserveFundsAsync(Transaction transaction);

    /// <summary>
    /// Completes a transaction by updating balances.
    /// Source wallet: Balance -= amount, Pending -= amount
    /// Fee wallet: Balance -= fee, Pending -= fee (may be same as source)
    /// Destination wallet (if internal): Balance += amount
    /// </summary>
    Task CompleteTransactionAsync(Transaction transaction);

    /// <summary>
    /// Rolls back a failed/cancelled transaction.
    /// Releases the reserved funds and fees from Pending.
    /// </summary>
    Task RollbackTransactionAsync(Transaction transaction);

    /// <summary>
    /// Credits funds for an incoming transaction (from external source).
    /// Destination wallet: Balance += amount
    /// </summary>
    Task CreditIncomingAsync(Transaction transaction);
}

public class BalanceResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static BalanceResult Ok() => new() { Success = true };
    public static BalanceResult Fail(string message, string code) => new()
    {
        Success = false,
        ErrorMessage = message,
        ErrorCode = code,
    };
}

public class BalanceService : IBalanceService
{
    private readonly FireblocksDbContext _context;
    private readonly ILogger<BalanceService> _logger;

    public BalanceService(FireblocksDbContext context, ILogger<BalanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BalanceResult> ReserveFundsAsync(Transaction transaction)
    {
        var sourceWallet = await GetWalletByAddressAsync(
            transaction.SourceAddress,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (sourceWallet == null)
        {
            return BalanceResult.Ok();
        }

        var sourceVaultId = sourceWallet.VaultAccountId;

        // Check if fee is paid in a different asset
        var feeCurrency = transaction.FeeCurrency ?? transaction.AssetId;
        var feeAmount = transaction.NetworkFee;
        var hasSeparateFeeWallet = feeCurrency != transaction.AssetId;
        Wallet? feeWallet = null;

        if (hasSeparateFeeWallet && feeAmount > 0)
        {
            feeWallet = await GetOrCreateWalletAsync(
                sourceVaultId,
                feeCurrency,
                transaction.WorkspaceId);

            if (feeWallet == null)
            {
                return BalanceResult.Fail(
                    $"Fee wallet ({feeCurrency}) not found for vault {sourceVaultId}",
                    "FEE_WALLET_NOT_FOUND");
            }

            // Check fee wallet balance
            var availableFeeBalance = feeWallet.Balance - feeWallet.Pending;
            if (availableFeeBalance < feeAmount)
            {
                _logger.LogWarning(
                    "Insufficient {FeeCurrency} balance for fee: available {Available}, required {Fee}",
                    feeCurrency, availableFeeBalance, feeAmount);

                return BalanceResult.Fail(
                $"Insufficient {feeCurrency} for fee. Available: {availableFeeBalance:G29}, Required: {feeAmount:G29}",
                "INSUFFICIENT_FEE_BALANCE");
        }
        }

        // Calculate total amount to reserve from source wallet
        // If fee is in same asset, include fee in the reservation
        var amountToReserve = hasSeparateFeeWallet ? transaction.Amount : transaction.Amount + feeAmount;
        var availableBalance = sourceWallet.Balance - sourceWallet.Pending;

        if (availableBalance < amountToReserve)
        {
            _logger.LogWarning(
            "Insufficient balance for transaction {TxId}: available {Available}, requested {Amount}",
            transaction.Id, availableBalance, amountToReserve);

            return BalanceResult.Fail(
                $"Insufficient balance. Available: {availableBalance:G29}, Requested: {amountToReserve:G29}",
                "INSUFFICIENT_BALANCE");
        }

        // Reserve the transfer amount
        sourceWallet.Pending += transaction.Amount;
        sourceWallet.UpdatedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Reserved {Amount} {AssetId} for transaction {TxId}. New pending: {Pending}",
            transaction.Amount, transaction.AssetId, transaction.Id, sourceWallet.Pending);

        // Reserve fee separately if in different wallet
        if (hasSeparateFeeWallet && feeWallet != null && feeAmount > 0)
        {
            feeWallet.Pending += feeAmount;
            feeWallet.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Reserved {Fee} {FeeCurrency} for fee on transaction {TxId}. New pending: {Pending}",
                feeAmount, feeCurrency, transaction.Id, feeWallet.Pending);
        }
        else if (!hasSeparateFeeWallet && feeAmount > 0)
        {
            // Fee is in same asset, add to pending
            sourceWallet.Pending += feeAmount;

            _logger.LogInformation(
                "Reserved {Fee} {AssetId} for fee on transaction {TxId}. Total pending: {Pending}",
                feeAmount, transaction.AssetId, transaction.Id, sourceWallet.Pending);
        }

        return BalanceResult.Ok();
    }

    public async Task CompleteTransactionAsync(Transaction transaction)
    {
        var feeCurrency = transaction.FeeCurrency ?? transaction.AssetId;
        var feeAmount = transaction.NetworkFee;
        var hasSeparateFeeWallet = feeCurrency != transaction.AssetId;

        var sourceWallet = await GetWalletByAddressAsync(
            transaction.SourceAddress,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (sourceWallet != null)
        {
            var sourceVaultId = sourceWallet.VaultAccountId;

            sourceWallet.Balance -= transaction.Amount;
            sourceWallet.Pending -= transaction.Amount;
            sourceWallet.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Deducted {Amount} {AssetId} from vault {VaultId}. New balance: {Balance}",
                transaction.Amount, transaction.AssetId, sourceVaultId, sourceWallet.Balance);

            if (!hasSeparateFeeWallet && feeAmount > 0)
            {
                sourceWallet.Balance -= feeAmount;
                sourceWallet.Pending -= feeAmount;

                _logger.LogInformation(
                    "Deducted fee {Fee} {AssetId} from vault {VaultId}. New balance: {Balance}",
                    feeAmount, transaction.AssetId, sourceVaultId, sourceWallet.Balance);
            }

            if (hasSeparateFeeWallet && feeAmount > 0)
            {
                var feeWallet = await GetWalletAsync(
                    sourceVaultId,
                    feeCurrency,
                    transaction.WorkspaceId);

                if (feeWallet != null)
                {
                    feeWallet.Balance -= feeAmount;
                    feeWallet.Pending -= feeAmount;
                    feeWallet.UpdatedAt = DateTimeOffset.UtcNow;

                    _logger.LogInformation(
                        "Deducted fee {Fee} {FeeCurrency} from vault {VaultId}. New balance: {Balance}",
                        feeAmount, feeCurrency, sourceVaultId, feeWallet.Balance);
                }
            }
        }

        var destinationWallet = await GetWalletByAddressAsync(
            transaction.DestinationAddress,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (destinationWallet != null)
        {
            destinationWallet.Balance += transaction.Amount;
            destinationWallet.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Credited {Amount} {AssetId} to vault {VaultId}. New balance: {Balance}",
                transaction.Amount, transaction.AssetId, destinationWallet.VaultAccountId, destinationWallet.Balance);
        }
    }

    public async Task RollbackTransactionAsync(Transaction transaction)
    {
        var feeCurrency = transaction.FeeCurrency ?? transaction.AssetId;
        var feeAmount = transaction.NetworkFee;
        var hasSeparateFeeWallet = feeCurrency != transaction.AssetId;

        var wallet = await GetWalletByAddressAsync(
            transaction.SourceAddress,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (wallet == null)
        {
            return;
        }

        var sourceVaultId = wallet.VaultAccountId;

        // Rollback transfer amount
        wallet.Pending -= transaction.Amount;

        // Rollback fee if in same wallet
        if (!hasSeparateFeeWallet && feeAmount > 0)
        {
            wallet.Pending -= feeAmount;
        }

        if (wallet.Pending < 0) wallet.Pending = 0; // Safety guard
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Rolled back {Amount} {AssetId} pending for vault {VaultId}. New pending: {Pending}",
            transaction.Amount, transaction.AssetId, sourceVaultId, wallet.Pending);

        // Rollback fee from separate wallet if applicable
        if (hasSeparateFeeWallet && feeAmount > 0)
        {
            var feeWallet = await GetWalletAsync(
                sourceVaultId,
                feeCurrency,
                transaction.WorkspaceId);

            if (feeWallet != null)
            {
                feeWallet.Pending -= feeAmount;
                if (feeWallet.Pending < 0) feeWallet.Pending = 0; // Safety guard
                feeWallet.UpdatedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Rolled back fee {Fee} {FeeCurrency} pending for vault {VaultId}. New pending: {Pending}",
                    feeAmount, feeCurrency, feeWallet.VaultAccountId, feeWallet.Pending);
            }
        }
    }

    public async Task CreditIncomingAsync(Transaction transaction)
    {
        var wallet = await GetWalletByAddressAsync(
            transaction.DestinationAddress,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (wallet != null)
        {
            wallet.Balance += transaction.Amount;
            wallet.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Credited incoming {Amount} {AssetId} to vault {VaultId}. New balance: {Balance}",
                transaction.Amount, transaction.AssetId, wallet.VaultAccountId, wallet.Balance);
        }
    }

    private async Task<Wallet?> GetWalletByAddressAsync(string? address, string assetId, string workspaceId)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var normalizedAddress = address.Trim();

        return await _context.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => a.AddressValue == normalizedAddress
                        && a.Wallet.AssetId == assetId
                        && a.Wallet.VaultAccount.WorkspaceId == workspaceId)
            .Select(a => a.Wallet)
            .FirstOrDefaultAsync();
    }

    private async Task<Wallet?> GetWalletAsync(string vaultAccountId, string assetId, string workspaceId)
    {
        return await _context.Wallets
            .Include(w => w.VaultAccount)
            .FirstOrDefaultAsync(w =>
                w.VaultAccountId == vaultAccountId &&
                w.AssetId == assetId &&
                w.VaultAccount.WorkspaceId == workspaceId);
    }

    private async Task<Wallet?> GetOrCreateWalletAsync(string vaultAccountId, string assetId, string workspaceId)
    {
        var wallet = await GetWalletAsync(vaultAccountId, assetId, workspaceId);

        if (wallet == null)
        {
            // Verify vault exists and belongs to workspace
            var vault = await _context.VaultAccounts
                .FirstOrDefaultAsync(v => v.Id == vaultAccountId && v.WorkspaceId == workspaceId);

            if (vault == null)
            {
                return null;
            }

            wallet = new Wallet
            {
                VaultAccountId = vaultAccountId,
                AssetId = assetId,
                Balance = 0,
                Pending = 0,
                LockedAmount = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created wallet for vault {VaultId} and asset {AssetId}",
                vaultAccountId, assetId);
        }

        return wallet;
    }
}
