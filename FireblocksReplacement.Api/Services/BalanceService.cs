using Microsoft.EntityFrameworkCore;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;

namespace FireblocksReplacement.Api.Services;

public interface IBalanceService
{
    /// <summary>
    /// Validates and reserves funds for an outgoing transaction.
    /// Updates the Pending amount for the source wallet.
    /// </summary>
    Task<BalanceResult> ReserveFundsAsync(Transaction transaction);

    /// <summary>
    /// Completes a transaction by updating balances.
    /// Source wallet: Balance -= amount, Pending -= amount
    /// Destination wallet (if internal): Balance += amount
    /// </summary>
    Task CompleteTransactionAsync(Transaction transaction);

    /// <summary>
    /// Rolls back a failed/cancelled transaction.
    /// Releases the reserved funds from Pending.
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
        ErrorCode = code
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
        // Only reserve funds for internal sources
        if (transaction.SourceType != "INTERNAL" || string.IsNullOrWhiteSpace(transaction.SourceVaultAccountId))
        {
            return BalanceResult.Ok();
        }

        var wallet = await GetOrCreateWalletAsync(
            transaction.SourceVaultAccountId,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (wallet == null)
        {
            return BalanceResult.Fail(
                $"Source wallet not found for vault {transaction.SourceVaultAccountId}",
                "WALLET_NOT_FOUND");
        }

        var availableBalance = wallet.Balance - wallet.Pending;
        if (availableBalance < transaction.Amount)
        {
            _logger.LogWarning(
                "Insufficient balance for transaction {TxId}: available {Available}, requested {Amount}",
                transaction.Id, availableBalance, transaction.Amount);

            return BalanceResult.Fail(
                $"Insufficient balance. Available: {availableBalance:F18}, Requested: {transaction.Amount:F18}",
                "INSUFFICIENT_BALANCE");
        }

        wallet.Pending += transaction.Amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "Reserved {Amount} {AssetId} for transaction {TxId}. New pending: {Pending}",
            transaction.Amount, transaction.AssetId, transaction.Id, wallet.Pending);

        return BalanceResult.Ok();
    }

    public async Task CompleteTransactionAsync(Transaction transaction)
    {
        // Deduct from source if internal
        if (transaction.SourceType == "INTERNAL" && !string.IsNullOrWhiteSpace(transaction.SourceVaultAccountId))
        {
            var sourceWallet = await GetWalletAsync(
                transaction.SourceVaultAccountId,
                transaction.AssetId,
                transaction.WorkspaceId);

            if (sourceWallet != null)
            {
                sourceWallet.Balance -= transaction.Amount;
                sourceWallet.Pending -= transaction.Amount;
                sourceWallet.UpdatedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Deducted {Amount} {AssetId} from vault {VaultId}. New balance: {Balance}",
                    transaction.Amount, transaction.AssetId, transaction.SourceVaultAccountId, sourceWallet.Balance);
            }
        }

        // Credit destination if internal
        if (transaction.DestinationType == "INTERNAL" && !string.IsNullOrWhiteSpace(transaction.DestinationVaultAccountId))
        {
            var destWallet = await GetOrCreateWalletAsync(
                transaction.DestinationVaultAccountId,
                transaction.AssetId,
                transaction.WorkspaceId);

            if (destWallet != null)
            {
                destWallet.Balance += transaction.Amount;
                destWallet.UpdatedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Credited {Amount} {AssetId} to vault {VaultId}. New balance: {Balance}",
                    transaction.Amount, transaction.AssetId, transaction.DestinationVaultAccountId, destWallet.Balance);
            }
        }
    }

    public async Task RollbackTransactionAsync(Transaction transaction)
    {
        // Only rollback for internal sources
        if (transaction.SourceType != "INTERNAL" || string.IsNullOrWhiteSpace(transaction.SourceVaultAccountId))
        {
            return;
        }

        var wallet = await GetWalletAsync(
            transaction.SourceVaultAccountId,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (wallet != null)
        {
            wallet.Pending -= transaction.Amount;
            if (wallet.Pending < 0) wallet.Pending = 0; // Safety guard
            wallet.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Rolled back {Amount} {AssetId} pending for vault {VaultId}. New pending: {Pending}",
                transaction.Amount, transaction.AssetId, transaction.SourceVaultAccountId, wallet.Pending);
        }
    }

    public async Task CreditIncomingAsync(Transaction transaction)
    {
        // Only credit for internal destinations
        if (transaction.DestinationType != "INTERNAL" || string.IsNullOrWhiteSpace(transaction.DestinationVaultAccountId))
        {
            return;
        }

        var wallet = await GetOrCreateWalletAsync(
            transaction.DestinationVaultAccountId,
            transaction.AssetId,
            transaction.WorkspaceId);

        if (wallet != null)
        {
            wallet.Balance += transaction.Amount;
            wallet.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation(
                "Credited incoming {Amount} {AssetId} to vault {VaultId}. New balance: {Balance}",
                transaction.Amount, transaction.AssetId, transaction.DestinationVaultAccountId, wallet.Balance);
        }
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
                UpdatedAt = DateTimeOffset.UtcNow
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
