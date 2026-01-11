using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using FireblocksReplacement.Api.Infrastructure.Db;
using FireblocksReplacement.Api.Models;
using FireblocksReplacement.Api.Hubs;
using FireblocksReplacement.Api.Dtos.Admin;

namespace FireblocksReplacement.Api.Services;

public class AutoTransitionService : BackgroundService
{
    private static readonly TransactionState[] NonTerminalStates =
    {
        TransactionState.SUBMITTED,
        TransactionState.PENDING_SIGNATURE,
        TransactionState.PENDING_AUTHORIZATION,
        TransactionState.QUEUED,
        TransactionState.BROADCASTING,
        TransactionState.CONFIRMING
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
                    }

                    tx.TransitionTo(next.Value);
                    updated.Add(tx);
                }

                if (updated.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                    foreach (var tx in updated)
                    {
                        await _hub.Clients.All.SendAsync("transactionUpserted", MapToDto(tx), stoppingToken);
                    }
                    await _hub.Clients.All.SendAsync("transactionsUpdated", cancellationToken: stoppingToken);
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
            _ => null
        };
    }

    private static AdminTransactionDto MapToDto(Transaction transaction)
    {
        return new AdminTransactionDto
        {
            Id = transaction.Id,
            VaultAccountId = transaction.VaultAccountId,
            AssetId = transaction.AssetId,
            SourceType = transaction.SourceType,
            SourceAddress = transaction.SourceAddress,
            SourceVaultAccountId = transaction.SourceVaultAccountId,
            DestinationType = transaction.DestinationType,
            DestinationVaultAccountId = transaction.DestinationVaultAccountId,
            Amount = transaction.Amount.ToString("F18"),
            DestinationAddress = transaction.DestinationAddress,
            DestinationTag = transaction.DestinationTag,
            State = transaction.State.ToString(),
            Hash = transaction.Hash,
            Fee = transaction.Fee.ToString("F18"),
            NetworkFee = transaction.NetworkFee.ToString("F18"),
            IsFrozen = transaction.IsFrozen,
            FailureReason = transaction.FailureReason,
            ReplacedByTxId = transaction.ReplacedByTxId,
            Confirmations = transaction.Confirmations,
            CreatedAt = transaction.CreatedAt,
            UpdatedAt = transaction.UpdatedAt
        };
    }
}
