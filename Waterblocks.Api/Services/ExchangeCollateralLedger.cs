using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services;

/// <summary>
/// Applies the exchange side of collateral transfers. Every balance method runs inside a database transaction.
/// </summary>
public interface IExchangeCollateralLedger
{
    Task<ExchangeAccountAsset> EnsureAssetRowAsync(string exchangeAccountId, string assetId);
    Task<bool> TryLockRemovalAsync(Transaction transaction);
    Task SettleAddAsync(Transaction transaction);
    Task SettleRemovalAsync(Transaction transaction);
    Task ReleaseRemovalAsync(Transaction transaction);
    Task QueueReversalAsync(Transaction transaction, ExchangeMirrorEvent removalEvent);
}

public sealed class ExchangeCollateralLedger : IExchangeCollateralLedger
{
    private readonly FireblocksDbContext _context;
    private readonly IAddressGenerator _addressGenerator;

    public ExchangeCollateralLedger(FireblocksDbContext context, IAddressGenerator addressGenerator)
    {
        _context = context;
        _addressGenerator = addressGenerator;
    }

    public async Task<ExchangeAccountAsset> EnsureAssetRowAsync(string exchangeAccountId, string assetId)
    {
        var existing = await FindAssetRowAsync(exchangeAccountId, assetId);
        if (existing != null)
        {
            return existing;
        }

        var address = _addressGenerator.GenerateVaultWalletDepositAddress(assetId, exchangeAccountId);
        var now = DateTimeOffset.UtcNow;
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ExchangeAccountAssets"
                ("ExchangeAccountId", "AssetId", "CollateralAddress", "Custody", "Locked", "ExchangeHold", "Credit", "CreatedAt", "UpdatedAt")
            VALUES ({exchangeAccountId}, {assetId}, {address}, 0, 0, 0, 0, {now}, {now})
            ON CONFLICT ("ExchangeAccountId", "AssetId") DO NOTHING
            """);

        return await FindAssetRowAsync(exchangeAccountId, assetId)
            ?? throw new InvalidOperationException($"Collateral row for {assetId} was not created");
    }

    public async Task<bool> TryLockRemovalAsync(Transaction transaction)
    {
        EnsureDatabaseTransaction();
        var outflow = transaction.CollateralOutflow;
        var now = DateTimeOffset.UtcNow;

        var lockedRows = await AssetRow(transaction)
            .Where(asset => asset.Custody - asset.Locked - asset.ExchangeHold >= outflow)
            .ExecuteUpdateAsync(update => update
                .SetProperty(asset => asset.Locked, asset => asset.Locked + outflow)
                .SetProperty(asset => asset.UpdatedAt, now));

        if (lockedRows == 0)
        {
            return false;
        }

        var simulatorAccount = await FindSimulatorAccountAsync(transaction);
        if (simulatorAccount != null)
        {
            transaction.RequiresExchangeApproval = true;
            AddEvent(transaction, EventId(transaction), simulatorAccount, ExchangeMirrorEventType.Remove, outflow);
        }

        return true;
    }

    public async Task SettleAddAsync(Transaction transaction)
    {
        EnsureDatabaseTransaction();
        var amount = transaction.Amount;
        var largestTotalBeforeAdd = DecimalColumnGuard.MaxReadableValue - amount;
        var now = DateTimeOffset.UtcNow;

        var settledRows = await AssetRow(transaction)
            .Where(asset => asset.Custody <= largestTotalBeforeAdd && asset.Credit <= largestTotalBeforeAdd)
            .ExecuteUpdateAsync(update => update
                .SetProperty(asset => asset.Custody, asset => asset.Custody + amount)
                .SetProperty(asset => asset.Credit, asset => asset.Credit + amount)
                .SetProperty(asset => asset.UpdatedAt, now));

        if (settledRows == 0)
        {
            await RequireOneRowAsync(AssetRow(transaction).CountAsync());
            throw new UnsafeNumericValueException(DecimalColumnGuard.TooLargeMessage(CollateralTotalName(transaction.AssetId)));
        }

        var simulatorAccount = await FindSimulatorAccountAsync(transaction);
        if (simulatorAccount != null)
        {
            AddEvent(transaction, EventId(transaction), simulatorAccount, ExchangeMirrorEventType.Add, amount);
        }
    }

    public async Task SettleRemovalAsync(Transaction transaction)
    {
        EnsureDatabaseTransaction();
        var outflow = transaction.CollateralOutflow;
        var now = DateTimeOffset.UtcNow;

        await RequireOneRowAsync(AssetRow(transaction).ExecuteUpdateAsync(update => update
            .SetProperty(asset => asset.Custody, asset => asset.Custody - outflow)
            .SetProperty(asset => asset.Locked, asset => asset.Locked > outflow ? asset.Locked - outflow : 0m)
            .SetProperty(asset => asset.Credit, asset => asset.Credit > outflow ? asset.Credit - outflow : 0m)
            .SetProperty(asset => asset.UpdatedAt, now)));
    }

    public async Task ReleaseRemovalAsync(Transaction transaction)
    {
        EnsureDatabaseTransaction();
        var outflow = transaction.CollateralOutflow;
        var now = DateTimeOffset.UtcNow;

        await RequireOneRowAsync(AssetRow(transaction).ExecuteUpdateAsync(update => update
            .SetProperty(asset => asset.Locked, asset => asset.Locked > outflow ? asset.Locked - outflow : 0m)
            .SetProperty(asset => asset.UpdatedAt, now)));

        var eventId = EventId(transaction);
        var removalEvent = await _context.ExchangeMirrorEvents.FirstOrDefaultAsync(item => item.Id == eventId);
        if (removalEvent == null)
        {
            return;
        }

        if (removalEvent.Status == ExchangeMirrorEventStatus.PENDING)
        {
            removalEvent.Status = ExchangeMirrorEventStatus.CANCELLED;
            removalEvent.UpdatedAt = now;
        }
        else if (transaction.ExchangeApprovedAt != null)
        {
            await QueueReversalAsync(transaction, removalEvent);
        }
    }

    public async Task QueueReversalAsync(Transaction transaction, ExchangeMirrorEvent removalEvent)
    {
        var reversalId = removalEvent.Id + ExchangeMirrorEvent.ReversalSuffix;
        var exists = await _context.ExchangeMirrorEvents.AnyAsync(item => item.Id == reversalId);
        if (!exists)
        {
            AddEvent(
                transaction,
                reversalId,
                removalEvent.SimulatorAccount,
                ExchangeMirrorEventType.Add,
                removalEvent.Amount);
        }
    }

    private void AddEvent(Transaction transaction, string eventId, string simulatorAccount, string type, decimal amount)
    {
        var now = DateTimeOffset.UtcNow;
        _context.ExchangeMirrorEvents.Add(new ExchangeMirrorEvent
        {
            Id = eventId,
            TransactionId = transaction.Id,
            WorkspaceId = transaction.WorkspaceId,
            ExchangeAccountId = transaction.ExchangeAccountId!,
            SimulatorAccount = simulatorAccount,
            Type = type,
            AssetId = transaction.AssetId,
            Amount = amount,
            Status = ExchangeMirrorEventStatus.PENDING,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    public static string CollateralTotalName(string assetId)
    {
        return $"The {assetId} collateral total";
    }

    private static string EventId(Transaction transaction)
    {
        return TransactionCompositeId.Build(transaction.WorkspaceId, transaction.Id);
    }

    private IQueryable<ExchangeAccountAsset> AssetRow(Transaction transaction)
    {
        var exchangeAccountId = transaction.ExchangeAccountId;
        var assetId = transaction.AssetId;
        return _context.ExchangeAccountAssets
            .Where(asset => asset.ExchangeAccountId == exchangeAccountId && asset.AssetId == assetId);
    }

    private Task<ExchangeAccountAsset?> FindAssetRowAsync(string exchangeAccountId, string assetId)
    {
        return _context.ExchangeAccountAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(asset => asset.ExchangeAccountId == exchangeAccountId && asset.AssetId == assetId);
    }

    private Task<string?> FindSimulatorAccountAsync(Transaction transaction)
    {
        return _context.ExchangeAccounts
            .Where(account => account.Id == transaction.ExchangeAccountId)
            .Select(account => account.GateSimulatorAccount)
            .FirstOrDefaultAsync();
    }

    private void EnsureDatabaseTransaction()
    {
        if (_context.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException("A collateral balance change needs a database transaction");
        }
    }

    private static async Task RequireOneRowAsync(Task<int> update)
    {
        if (await update != 1)
        {
            throw new InvalidOperationException("The collateral balance row does not exist");
        }
    }
}
