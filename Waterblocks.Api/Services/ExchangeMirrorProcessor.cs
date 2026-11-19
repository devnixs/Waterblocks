using Microsoft.EntityFrameworkCore;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

/// <summary>
/// Sends pending mirror events to the Gate simulator and applies its replies.
/// </summary>
public interface IExchangeMirrorProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}

public sealed class ExchangeMirrorProcessor : IExchangeMirrorProcessor
{
    private const int BatchSize = 100;
    private const int MaxErrorLength = 1000;
    private const string RejectedByExchange = "3RD_PARTY_REJECTED";
    private const string FailedAtExchange = "3RD_PARTY_FAILED";

    private readonly FireblocksDbContext _context;
    private readonly IGateSimulatorClient _gateSimulator;
    private readonly ICollateralTransactionRunner _runner;
    private readonly ITransactionStateService _transactionState;
    private readonly IAdminTransactionNotifier _notifier;
    private readonly ILogger<ExchangeMirrorProcessor> _logger;

    public ExchangeMirrorProcessor(
        FireblocksDbContext context,
        IGateSimulatorClient gateSimulator,
        ICollateralTransactionRunner runner,
        ITransactionStateService transactionState,
        IAdminTransactionNotifier notifier,
        ILogger<ExchangeMirrorProcessor> logger)
    {
        _context = context;
        _gateSimulator = gateSimulator;
        _runner = runner;
        _transactionState = transactionState;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_gateSimulator.IsConfigured)
        {
            return;
        }

        var pendingEvents = await _context.ExchangeMirrorEvents
            .AsNoTracking()
            .Where(item => item.Status == ExchangeMirrorEventStatus.PENDING)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        // An account keeps its order, so its later events wait when an earlier event must be sent again.
        var waitingAccounts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pendingEvent in pendingEvents)
        {
            if (waitingAccounts.Contains(pendingEvent.SimulatorAccount) ||
                !await IsStillPendingAsync(pendingEvent.Id, cancellationToken))
            {
                continue;
            }

            var outcome = await _gateSimulator.SendEventAsync(pendingEvent, cancellationToken);
            if (outcome.Kind == GateEventOutcomeKind.Retry)
            {
                waitingAccounts.Add(pendingEvent.SimulatorAccount);
                await RecordRetryAsync(pendingEvent, outcome, cancellationToken);
                continue;
            }

            await ApplyOutcomeAsync(pendingEvent, outcome, cancellationToken);
        }
    }

    private Task<bool> IsStillPendingAsync(string eventId, CancellationToken cancellationToken)
    {
        return _context.ExchangeMirrorEvents
            .AsNoTracking()
            .AnyAsync(item => item.Id == eventId && item.Status == ExchangeMirrorEventStatus.PENDING, cancellationToken);
    }

    private async Task RecordRetryAsync(
        ExchangeMirrorEvent pendingEvent,
        GateEventOutcome outcome,
        CancellationToken cancellationToken)
    {
        var error = Truncate(outcome.Message);
        var now = DateTimeOffset.UtcNow;
        await _context.ExchangeMirrorEvents
            .Where(item => item.Id == pendingEvent.Id && item.Status == ExchangeMirrorEventStatus.PENDING)
            .ExecuteUpdateAsync(
                update => update
                    .SetProperty(item => item.Attempts, item => item.Attempts + 1)
                    .SetProperty(item => item.LastError, error)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        if (pendingEvent.Attempts == 0)
        {
            _logger.LogWarning(
                "Gate simulator did not take mirror event {EventId}. It stays pending: {Error}",
                pendingEvent.Id, error);
        }
    }

    private async Task ApplyOutcomeAsync(
        ExchangeMirrorEvent pendingEvent,
        GateEventOutcome outcome,
        CancellationToken cancellationToken)
    {
        var transaction = await _runner.RunAsync(
            async unitOfWork =>
            {
                var locked = await unitOfWork.LockTransactionAsync(pendingEvent.TransactionId, cancellationToken);
                var mirrorEvent = await unitOfWork.Database.ExchangeMirrorEvents
                    .FirstOrDefaultAsync(item => item.Id == pendingEvent.Id, cancellationToken);
                if (locked == null || mirrorEvent == null || !CanTakeOutcome(mirrorEvent))
                {
                    return null;
                }

                await ApplyOutcomeToEventAsync(unitOfWork, locked, mirrorEvent, outcome);
                return locked;
            },
            cancellationToken);

        if (transaction?.WorkspaceId != null)
        {
            await _notifier.NotifyUpsertAsync(transaction, transaction.WorkspaceId);
        }
    }

    private static bool CanTakeOutcome(ExchangeMirrorEvent mirrorEvent)
    {
        // A failure step can cancel an event while its request is on the way to the simulator.
        return mirrorEvent.Status is ExchangeMirrorEventStatus.PENDING or ExchangeMirrorEventStatus.CANCELLED;
    }

    private async Task ApplyOutcomeToEventAsync(
        CollateralUnitOfWork unitOfWork,
        Transaction transaction,
        ExchangeMirrorEvent mirrorEvent,
        GateEventOutcome outcome)
    {
        var now = DateTimeOffset.UtcNow;
        var isRemoval = mirrorEvent.Type == ExchangeMirrorEventType.Remove;
        mirrorEvent.Attempts++;
        mirrorEvent.UpdatedAt = now;
        mirrorEvent.LastError = Truncate(outcome.Message);

        switch (outcome.Kind)
        {
            case GateEventOutcomeKind.Applied:
                mirrorEvent.Status = ExchangeMirrorEventStatus.APPLIED;
                if (isRemoval)
                {
                    await ApproveRemovalAsync(unitOfWork, transaction, mirrorEvent, now);
                }

                break;

            case GateEventOutcomeKind.Rejected:
                mirrorEvent.Status = ExchangeMirrorEventStatus.REJECTED;
                if (isRemoval)
                {
                    await EndRemovalAsync(
                        unitOfWork,
                        transaction,
                        new TransactionStateRequest(TransactionState.CANCELLED, RejectedByExchange, mirrorEvent.LastError));
                }

                break;

            case GateEventOutcomeKind.Refused:
                mirrorEvent.Status = ExchangeMirrorEventStatus.FAILED;
                if (isRemoval)
                {
                    await EndRemovalAsync(
                        unitOfWork,
                        transaction,
                        new TransactionStateRequest(TransactionState.FAILED, FailedAtExchange, mirrorEvent.LastError));
                }

                break;
        }
    }

    private static async Task ApproveRemovalAsync(
        CollateralUnitOfWork unitOfWork,
        Transaction transaction,
        ExchangeMirrorEvent mirrorEvent,
        DateTimeOffset now)
    {
        if (transaction.State.IsFailure())
        {
            await unitOfWork.Ledger.QueueReversalAsync(transaction, mirrorEvent);
            return;
        }

        transaction.ExchangeApprovedAt ??= now;
        transaction.UpdatedAt = now;
    }

    private async Task EndRemovalAsync(
        CollateralUnitOfWork unitOfWork,
        Transaction transaction,
        TransactionStateRequest request)
    {
        if (!transaction.State.IsTerminal())
        {
            await _transactionState.ApplyCollateralChangeAsync(unitOfWork, transaction, request);
        }
    }

    private static string? Truncate(string? message)
    {
        return message?.Length > MaxErrorLength ? message[..MaxErrorLength] : message;
    }
}
