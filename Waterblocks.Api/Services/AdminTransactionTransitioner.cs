using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminTransactionTransitioner
{
    Task<TransitionOutcome> TransitionAsync(Transaction transaction, TransactionState newState, string workspaceId);
}

public sealed class AdminTransactionTransitioner : IAdminTransactionTransitioner
{
    private readonly ITransactionStateService _transactionState;
    private readonly IAdminTransactionNotifier _notifier;
    private readonly ILogger<AdminTransactionTransitioner> _logger;
    private readonly TransactionStateMachine _stateMachine;

    public AdminTransactionTransitioner(
        ITransactionStateService transactionState,
        IAdminTransactionNotifier notifier,
        ILogger<AdminTransactionTransitioner> logger,
        TransactionStateMachine stateMachine)
    {
        _transactionState = transactionState;
        _notifier = notifier;
        _logger = logger;
        _stateMachine = stateMachine;
    }

    public async Task<TransitionOutcome> TransitionAsync(Transaction transaction, TransactionState newState, string workspaceId)
    {
        if (transaction.State == newState)
        {
            _logger.LogInformation("Transaction {TxId} already in state {State}",
                transaction.Id, newState);

            return TransitionOutcome.FromSuccess(new TransactionStateDto
            {
                Id = TransactionCompositeId.Build(workspaceId, transaction.Id),
                State = transaction.State.ToString(),
            });
        }

        if (!_stateMachine.CanTransition(transaction.State, newState))
        {
            return TransitionOutcome.Failure(
                $"Invalid transition from {transaction.State} to {newState}",
                "INVALID_STATE_TRANSITION");
        }

        var previousState = transaction.State;
        var change = await _transactionState.ChangeAsync(transaction, new TransactionStateRequest(newState));
        if (!change.Success)
        {
            return TransitionOutcome.Failure(change.ErrorMessage!, change.ErrorCode!);
        }

        var updated = change.Transaction!;
        await _notifier.NotifyUpsertAsync(updated, workspaceId);

        _logger.LogInformation("Transitioned transaction {TxId} from {OldState} to {NewState}",
            updated.Id, previousState, newState);

        return TransitionOutcome.FromSuccess(new TransactionStateDto
        {
            Id = TransactionCompositeId.Build(workspaceId, updated.Id),
            State = updated.State.ToString(),
        });
    }
}

public sealed record TransitionOutcome(bool Success, TransactionStateDto? Result, string? ErrorMessage, string? ErrorCode)
{
    public static TransitionOutcome Failure(string message, string code)
    {
        return new TransitionOutcome(false, null, message, code);
    }

    public static TransitionOutcome FromSuccess(TransactionStateDto result)
    {
        return new TransitionOutcome(true, result, null, null);
    }
}
