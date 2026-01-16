using Waterblocks.Api.Dtos.Admin;
using Waterblocks.Api.Infrastructure;
using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public interface IAdminTransactionTransitioner
{
    Task<TransitionOutcome> TransitionAsync(Transaction transaction, TransactionState newState, string workspaceId);
}

public sealed class AdminTransactionTransitioner : IAdminTransactionTransitioner
{
    private readonly FireblocksDbContext _context;
    private readonly IBalanceService _balanceService;
    private readonly IAdminTransactionNotifier _notifier;
    private readonly ILogger<AdminTransactionTransitioner> _logger;
    private readonly TransactionStateMachine _stateMachine;

    public AdminTransactionTransitioner(
        FireblocksDbContext context,
        IBalanceService balanceService,
        IAdminTransactionNotifier notifier,
        ILogger<AdminTransactionTransitioner> logger,
        TransactionStateMachine stateMachine)
    {
        _context = context;
        _balanceService = balanceService;
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

        if (newState == TransactionState.REJECTED ||
            newState == TransactionState.CANCELLED ||
            newState == TransactionState.TIMEOUT)
        {
            await _balanceService.RollbackTransactionAsync(transaction);
        }

        transaction.TransitionTo(newState);
        await _context.SaveChangesAsync();
        await _notifier.NotifyUpsertAsync(transaction, workspaceId);

        _logger.LogInformation("Transitioned transaction {TxId} from {OldState} to {NewState}",
            transaction.Id, transaction.State, newState);

        return TransitionOutcome.FromSuccess(new TransactionStateDto
        {
            Id = TransactionCompositeId.Build(workspaceId, transaction.Id),
            State = transaction.State.ToString(),
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
