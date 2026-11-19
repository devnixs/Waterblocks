using Waterblocks.Api.Infrastructure.Db;
using Waterblocks.Api.Models;

namespace Waterblocks.Api.Services;

public sealed record TransactionStateRequest(
    TransactionState NewState,
    string? SubStatus = null,
    string? FailureReason = null);

public sealed record TransactionStateChange(
    bool Success,
    Transaction? Transaction,
    string? ErrorMessage,
    string? ErrorCode)
{
    public static TransactionStateChange Changed(Transaction transaction)
    {
        return new TransactionStateChange(true, transaction, null, null);
    }

    public static TransactionStateChange Failure(string message, string code)
    {
        return new TransactionStateChange(false, null, message, code);
    }
}

/// <summary>
/// The shared path for a state change and its balance effects.
/// </summary>
public interface ITransactionStateService
{
    Task<TransactionStateChange> ChangeAsync(
        Transaction transaction,
        TransactionStateRequest request,
        CancellationToken cancellationToken = default);

    Task<TransactionStateChange> ApplyCollateralChangeAsync(
        CollateralUnitOfWork unitOfWork,
        Transaction transaction,
        TransactionStateRequest request);
}

public sealed class TransactionStateService : ITransactionStateService
{
    private readonly FireblocksDbContext _context;
    private readonly IBalanceService _balanceService;
    private readonly ICollateralTransactionRunner _collateralRunner;
    private readonly TransactionStateMachine _stateMachine;

    public TransactionStateService(
        FireblocksDbContext context,
        IBalanceService balanceService,
        ICollateralTransactionRunner collateralRunner,
        TransactionStateMachine stateMachine)
    {
        _context = context;
        _balanceService = balanceService;
        _collateralRunner = collateralRunner;
        _stateMachine = stateMachine;
    }

    public Task<TransactionStateChange> ChangeAsync(
        Transaction transaction,
        TransactionStateRequest request,
        CancellationToken cancellationToken = default)
    {
        return transaction.IsCollateralTransfer
            ? ChangeCollateralTransferAsync(transaction.Id, request, cancellationToken)
            : ChangeStandardTransactionAsync(transaction, request, cancellationToken);
    }

    public async Task<TransactionStateChange> ApplyCollateralChangeAsync(
        CollateralUnitOfWork unitOfWork,
        Transaction transaction,
        TransactionStateRequest request)
    {
        var newState = request.NewState;
        if (transaction.State == newState)
        {
            return TransactionStateChange.Changed(transaction);
        }

        if (!_stateMachine.CanTransition(transaction.State, newState))
        {
            return TransactionStateChange.Failure(
                $"Invalid transition from {transaction.State} to {newState}",
                "INVALID_STATE_TRANSITION");
        }

        if (!newState.IsFailure() && transaction.IsWaitingForExchangeApproval)
        {
            return TransactionStateChange.Failure(
                "The removal is waiting for the approval of the exchange",
                "EXCHANGE_APPROVAL_PENDING");
        }

        if (newState == TransactionState.CONFIRMING)
        {
            transaction.Confirmations = Math.Max(transaction.Confirmations + 1, 1);
        }

        if (newState.IsFailure() || newState == TransactionState.COMPLETED)
        {
            await unitOfWork.LockVaultWalletsAsync(transaction.VaultAccountId);
        }

        await ApplyBalanceEffectsAsync(unitOfWork.Balance, transaction, newState);
        ApplyState(transaction, request);
        return TransactionStateChange.Changed(transaction);
    }

    private async Task<TransactionStateChange> ChangeStandardTransactionAsync(
        Transaction transaction,
        TransactionStateRequest request,
        CancellationToken cancellationToken)
    {
        _stateMachine.ValidateTransition(transaction.State, request.NewState);
        await ApplyBalanceEffectsAsync(_balanceService, transaction, request.NewState);
        ApplyState(transaction, request);
        await _context.SaveChangesAsync(cancellationToken);
        return TransactionStateChange.Changed(transaction);
    }

    private Task<TransactionStateChange> ChangeCollateralTransferAsync(
        string transactionId,
        TransactionStateRequest request,
        CancellationToken cancellationToken)
    {
        return _collateralRunner.RunAsync(
            async unitOfWork =>
            {
                var transaction = await unitOfWork.LockTransactionAsync(transactionId, cancellationToken)
                    ?? throw new KeyNotFoundException($"Transaction {transactionId} not found");
                return await ApplyCollateralChangeAsync(unitOfWork, transaction, request);
            },
            cancellationToken);
    }

    private static async Task ApplyBalanceEffectsAsync(
        IBalanceService balanceService,
        Transaction transaction,
        TransactionState newState)
    {
        if (newState.IsFailure())
        {
            await balanceService.RollbackTransactionAsync(transaction);
        }

        if (newState == TransactionState.COMPLETED)
        {
            if (transaction.Confirmations == 0)
            {
                transaction.Confirmations = 6;
            }

            await balanceService.CompleteTransactionAsync(transaction);
        }
    }

    private static void ApplyState(Transaction transaction, TransactionStateRequest request)
    {
        transaction.TransitionTo(request.NewState);
        if (request.SubStatus != null)
        {
            transaction.SubStatus = request.SubStatus;
        }

        if (request.FailureReason != null)
        {
            transaction.FailureReason = request.FailureReason;
        }
    }
}
