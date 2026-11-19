using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

/// <summary>
/// The services of one database transaction for a collateral change.
/// </summary>
public sealed class CollateralUnitOfWork
{
    public CollateralUnitOfWork(
        FireblocksDbContext database,
        IBalanceService balance,
        ITransactionFactory transactions,
        IExchangeCollateralLedger ledger)
    {
        Database = database;
        Balance = balance;
        Transactions = transactions;
        Ledger = ledger;
    }

    public FireblocksDbContext Database { get; }
    public IBalanceService Balance { get; }
    public ITransactionFactory Transactions { get; }
    public IExchangeCollateralLedger Ledger { get; }

    /// <summary>
    /// Reads a transaction and holds its row until the database transaction ends.
    /// </summary>
    public async Task<Transaction?> LockTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        var rows = await Database.Transactions
            .FromSqlInterpolated($"""SELECT * FROM "Transactions" WHERE "Id" = {transactionId} FOR UPDATE""")
            .ToListAsync(cancellationToken);

        return rows.SingleOrDefault();
    }

    /// <summary>
    /// Holds the wallet rows of a vault, so that parallel collateral changes read current balances.
    /// </summary>
    public Task LockVaultWalletsAsync(string vaultAccountId, CancellationToken cancellationToken = default)
    {
        return Database.Wallets
            .FromSqlInterpolated($"""SELECT * FROM "Wallets" WHERE "VaultAccountId" = {vaultAccountId} ORDER BY "Id" FOR UPDATE""")
            .ToListAsync(cancellationToken);
    }
}

public interface ICollateralTransactionRunner
{
    Task<T> RunAsync<T>(Func<CollateralUnitOfWork, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs collateral work in one database transaction, with a new database context for each attempt.
/// </summary>
public sealed class CollateralTransactionRunner : ICollateralTransactionRunner
{
    private readonly FireblocksDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;

    public CollateralTransactionRunner(FireblocksDbContext context, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _scopeFactory = scopeFactory;
    }

    public Task<T> RunAsync<T>(Func<CollateralUnitOfWork, Task<T>> work, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async attemptToken =>
            {
                using var scope = _scopeFactory.CreateScope();
                var services = scope.ServiceProvider;
                var database = services.GetRequiredService<FireblocksDbContext>();
                var unitOfWork = new CollateralUnitOfWork(
                    database,
                    services.GetRequiredService<IBalanceService>(),
                    services.GetRequiredService<ITransactionFactory>(),
                    services.GetRequiredService<IExchangeCollateralLedger>());

                await using var transaction = await database.Database.BeginTransactionAsync(attemptToken);
                var result = await work(unitOfWork);
                await database.SaveChangesAsync(attemptToken);
                await transaction.CommitAsync(attemptToken);
                return result;
            },
            cancellationToken);
    }
}
