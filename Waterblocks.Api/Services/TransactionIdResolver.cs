using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface ITransactionIdResolver
{
    bool TryUnwrap(string compositeId, out string rawId);
    Task<Transaction?> FindWorkspaceTransactionAsync(
        string compositeId,
        bool includeVaultAccount = false,
        bool allowExternalId = false,
        CancellationToken cancellationToken = default);
    Task<Transaction> RequireWorkspaceTransactionAsync(
        string compositeId,
        bool includeVaultAccount = false,
        bool allowExternalId = false,
        CancellationToken cancellationToken = default);
}

public sealed class TransactionIdResolver : ITransactionIdResolver
{
    private readonly FireblocksDbContext _context;
    private readonly WorkspaceContext _workspace;
    private readonly ITransactionViewService _transactionView;

    public TransactionIdResolver(
        FireblocksDbContext context,
        WorkspaceContext workspace,
        ITransactionViewService transactionView)
    {
        _context = context;
        _workspace = workspace;
        _transactionView = transactionView;
    }

    public bool TryUnwrap(string compositeId, out string rawId)
    {
        return TransactionCompositeId.TryUnwrap(compositeId, _workspace.WorkspaceId, out rawId);
    }

    public async Task<Transaction?> FindWorkspaceTransactionAsync(
        string compositeId,
        bool includeVaultAccount = false,
        bool allowExternalId = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(compositeId))
        {
            return null;
        }

        if (!TryUnwrap(compositeId, out var rawId))
        {
            return null;
        }

        var workspaceAddresses = await _transactionView.GetWorkspaceAddressesAsync(_workspace.WorkspaceId ?? string.Empty);

        IQueryable<Transaction> query = _context.Transactions;
        if (includeVaultAccount)
        {
            query = query.Include(t => t.VaultAccount);
        }

        query = _transactionView.ApplyWorkspaceAddressFilter(query, workspaceAddresses);

        return await query.FirstOrDefaultAsync(
            t => t.Id == rawId || (allowExternalId && t.ExternalTxId == rawId),
            cancellationToken);
    }

    public async Task<Transaction> RequireWorkspaceTransactionAsync(
        string compositeId,
        bool includeVaultAccount = false,
        bool allowExternalId = false,
        CancellationToken cancellationToken = default)
    {
        var transaction = await FindWorkspaceTransactionAsync(
            compositeId,
            includeVaultAccount,
            allowExternalId,
            cancellationToken);

        if (transaction == null)
        {
            throw new KeyNotFoundException($"Transaction {compositeId} not found");
        }

        return transaction;
    }
}
