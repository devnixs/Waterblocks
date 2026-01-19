using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;
using Waterblocks.Api.Hubs;
using Waterblocks.Api.Utils;

namespace Waterblocks.Api.Services;

public class AutoTransitionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoTransitionService> _logger;
    private readonly IHubContext<AdminHub> _hub;
    private readonly TransactionStateMachine _stateMachine;

    public AutoTransitionService(
        IServiceScopeFactory scopeFactory,
        ILogger<AutoTransitionService> logger,
        IHubContext<AdminHub> hub,
        TransactionStateMachine stateMachine)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hub = hub;
        _stateMachine = stateMachine;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<FireblocksDbContext>();
                var transactionView = scope.ServiceProvider.GetRequiredService<ITransactionViewService>();

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
                    .Where(t => _stateMachine.NonTerminalStates.Contains(t.State))
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
                    var next = _stateMachine.GetNextAutoState(tx.State);
                    if (next == null || !_stateMachine.CanTransition(tx.State, next.Value))
                    {
                        continue;
                    }

                    if (next == TransactionState.BROADCASTING && string.IsNullOrEmpty(tx.Hash))
                    {
                        var asset = await db.Assets.FindAsync(tx.AssetId);
                        if (asset != null)
                        {
                            tx.Hash = TransactionHashGenerator.Generate(tx.AssetId, asset.BlockchainType);
                        }
                        else
                        {
                            _logger.LogWarning("Asset {AssetId} not found for transaction {TxId}, skipping hash generation", tx.AssetId, tx.Id);
                            continue;
                        }
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
                    var updatedWorkspaces = updated
                        .Select(t => t.WorkspaceId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id!)
                        .Distinct()
                        .ToList();

                    var addressLookup = await transactionView.BuildAddressOwnershipLookupAsync(updated, updatedWorkspaces);
                    foreach (var tx in updated)
                    {
                        if (string.IsNullOrWhiteSpace(tx.WorkspaceId))
                        {
                            continue;
                        }

                        await _hub.Clients.Group(tx.WorkspaceId).SendAsync(
                            "transactionUpserted",
                            transactionView.MapToAdminDto(tx, addressLookup, tx.WorkspaceId),
                            stoppingToken);
                    }
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

}


