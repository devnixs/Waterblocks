namespace Waterblocks.Api.Models;

public enum TransactionState
{
    SUBMITTED,
    PENDING_SIGNATURE,
    PENDING_AUTHORIZATION,
    QUEUED,
    BROADCASTING,
    CONFIRMING,
    COMPLETED,
    FAILED,
    REJECTED,
    CANCELLED,
    TIMEOUT,
}

public static class TransactionStateExtensions
{
    public static bool IsTerminal(this TransactionState state)
    {
        return TransactionStateMachine.Instance.IsTerminal(state);
    }

    public static bool CanTransitionTo(this TransactionState currentState, TransactionState newState)
    {
        return TransactionStateMachine.Instance.CanTransition(currentState, newState);
    }

    public static void ValidateTransition(this TransactionState currentState, TransactionState newState)
    {
        TransactionStateMachine.Instance.ValidateTransition(currentState, newState);
    }
}
