using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Dtos.Admin;

namespace Waterblocks.Api.Services;

public class AutoTransitionService : BackgroundService
{
    private static readonly TransactionState[] NonTerminalStates =
    {
        TransactionState.SUBMITTED,
        TransactionState.PENDING_SIGNATURE,
        TransactionState.PENDING_AUTHORIZATION,
        TransactionState.QUEUED,
        TransactionState.BROADCASTING,
        TransactionState.CONFIRMING,
    };
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoTransitionService> _logger;
    private readonly IHubContext<AdminHub> _hub;

    public AutoTransitionService(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoTransitionService> logger,
        IHubContext<AdminHub> hub)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();

                var setting = await db.AdminSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == "AutoTransitionEnabled", stoppingToken);

                var enabled = setting != null && bool.TryParse(setting.Value, out var value) && value;
                if (!enabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var transactions = await db.Transactions
                    .Where(t => NonTerminalStates.Contains(t.State))
                    .OrderBy(t => t.CreatedAt)
                    .ToListAsync(stoppingToken);

                if (transactions.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var balanceService = scope.ServiceProvider.GetRequiredService<IBalanceService>();

                var updated = new List<Transaction>();
                foreach (var tx in transactions)
                {
                    var next = GetNextState(tx.State);
                    if (next == null || !tx.State.CanTransitionTo(next.Value))
                    {
                        continue;
                    }

                    if (next == TransactionState.BROADCASTING && string.IsNullOrEmpty(tx.Hash))
                    {
                        tx.Hash = $"0x{Guid.NewGuid():N}";
                    }

                    if (next == TransactionState.CONFIRMING)
                    {
                        tx.Confirmations = Math.Max(tx.Confirmations + 1, 1);
                    }

                    if (next == TransactionState.COMPLETED)
                    {
                        if (tx.Confirmations == 0)
                        {
                            tx.Confirmations = 6;
                        }
                        // Update balances when completing: source -amount, destination +amount
                        await balanceService.CompleteTransactionAsync(tx);
                    }

                    tx.TransitionTo(next.Value);
                    updated.Add(tx);
                }

                if (updated.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    var addressLookup = await BuildAddressOwnershipLookupAsync(db, updated, stoppingToken);
                    foreach (var tx in updated)
                    {
                        await _hub.Clients.Group(tx.WorkspaceId).SendAsync("transactionUpserted", MapToDto(tx, addressLookup), stoppingToken);
                    }
                    var updatedWorkspaces = updated.Select(t => t.WorkspaceId).Where(id => !string.IsNullOrEmpty(id)).Distinct();
                    foreach (var workspaceId in updatedWorkspaces)
                    {
                        await _hub.Clients.Group(workspaceId).SendAsync("transactionsUpdated", cancellationToken: stoppingToken);
                        await _hub.Clients.Group(workspaceId).SendAsync("vaultsUpdated", cancellationToken: stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-transition loop failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private static TransactionState? GetNextState(TransactionState current)
    {
        return current switch
        {
            TransactionState.SUBMITTED => TransactionState.PENDING_AUTHORIZATION,
            TransactionState.PENDING_SIGNATURE => TransactionState.PENDING_AUTHORIZATION,
            TransactionState.PENDING_AUTHORIZATION => TransactionState.QUEUED,
            TransactionState.QUEUED => TransactionState.BROADCASTING,
            TransactionState.BROADCASTING => TransactionState.CONFIRMING,
            TransactionState.CONFIRMING => TransactionState.COMPLETED,
            _ => null,
        };
    }

    private static AdminTransactionDto MapToDto(Transaction transaction, IReadOnlyDictionary<string, AddressOwnership> addressLookup)
    {
        var sourceOwnership = ResolveAddressOwnership(addressLookup, transaction.AssetId, transaction.SourceAddress);
        var destinationOwnership = ResolveAddressOwnership(addressLookup, transaction.AssetId, transaction.DestinationAddress);
        var sourceType = sourceOwnership != null ? "INTERNAL" : "EXTERNAL";
        var destinationType = destinationOwnership != null ? "INTERNAL" : "EXTERNAL";

        return new AdminTransactionDto
        {
            Id = TransactionCompositeId.Build(transaction.WorkspaceId, transaction.Id),
            VaultAccountId = transaction.VaultAccountId,
            AssetId = transaction.AssetId,
            SourceType = sourceType,
            SourceAddress = transaction.SourceAddress,
            SourceVaultAccountName = sourceOwnership?.VaultAccountName,
            DestinationType = destinationType,
            DestinationVaultAccountName = destinationOwnership?.VaultAccountName,
            Amount = transaction.Amount.ToString("F18"),
            DestinationAddress = transaction.DestinationAddress,
            DestinationTag = transaction.DestinationTag,
            State = transaction.State.ToString(),
            Hash = transaction.Hash,
            Fee = transaction.Fee.ToString("F18"),
            NetworkFee = transaction.NetworkFee.ToString("F18"),
            IsFrozen = transaction.IsFrozen,
            FailureReason = transaction.FailureReason,
            ReplacedByTxId = transaction.ReplacedByTxId == null
                ? null
                : TransactionCompositeId.Build(transaction.WorkspaceId, transaction.ReplacedByTxId),
            Confirmations = transaction.Confirmations,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt,
        };
    }

    private sealed record AddressOwnership(string VaultAccountId, string VaultAccountName);

    private static string BuildAddressKey(string assetId, string address)
    {
        return $"{assetId}|{address}";
    }

    private static AddressOwnership? ResolveAddressOwnership(
        IReadOnlyDictionary<string, AddressOwnership> lookup,
        string assetId,
        string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        return lookup.TryGetValue(BuildAddressKey(assetId, address), out var ownership)
            ? ownership
            : null;
    }

    private static async Task<Dictionary<string, AddressOwnership>> BuildAddressOwnershipLookupAsync(
        FireblocksDbContext db,
        IEnumerable<Transaction> transactions,
        CancellationToken stoppingToken)
    {
        var addressValues = transactions
            .SelectMany(t => new[] { t.SourceAddress, t.DestinationAddress })
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address!)
            .Distinct()
            .ToList();

        if (addressValues.Count == 0)
        {
            return new Dictionary<string, AddressOwnership>();
        }

        var workspaceIds = transactions
            .Select(t => t.WorkspaceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var addresses = await db.Addresses
            .Include(a => a.Wallet)
            .ThenInclude(w => w.VaultAccount)
            .Where(a => addressValues.Contains(a.AddressValue)
                        && workspaceIds.Contains(a.Wallet.VaultAccount.WorkspaceId))
            .ToListAsync(stoppingToken);

        var lookup = new Dictionary<string, AddressOwnership>();
        foreach (var address in addresses)
        {
            var wallet = address.Wallet;
            var vault = wallet?.VaultAccount;
            if (wallet == null || vault == null)
            {
                continue;
            }

            var key = BuildAddressKey(wallet.AssetId, address.AddressValue);
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = new AddressOwnership(vault.Id, vault.Name);
            }
        }

        return lookup;
    }
}


