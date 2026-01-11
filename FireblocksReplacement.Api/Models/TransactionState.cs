namespace FireblocksReplacement.Api.Models;

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
    TIMEOUT
}

public static class TransactionStateExtensions
{
    private static readonly HashSet<TransactionState> TerminalStates = new()
    {
        TransactionState.COMPLETED,
        TransactionState.FAILED,
        TransactionState.REJECTED,
        TransactionState.CANCELLED,
        TransactionState.TIMEOUT
    };

    public static bool IsTerminal(this TransactionState state)
    {
        return TerminalStates.Contains(state);
    }

    public static bool CanTransitionTo(this TransactionState currentState, TransactionState newState)
    {
        // Terminal states cannot transition
        if (currentState.IsTerminal())
        {
            return false;
        }

        // Define valid transitions
        return (currentState, newState) switch
        {
            (TransactionState.SUBMITTED, TransactionState.PENDING_SIGNATURE) => true,
            (TransactionState.SUBMITTED, TransactionState.PENDING_AUTHORIZATION) => true,
            (TransactionState.SUBMITTED, TransactionState.QUEUED) => true,
            (TransactionState.SUBMITTED, TransactionState.FAILED) => true,
            (TransactionState.SUBMITTED, TransactionState.REJECTED) => true,
            (TransactionState.SUBMITTED, TransactionState.CANCELLED) => true,

            (TransactionState.PENDING_SIGNATURE, TransactionState.PENDING_AUTHORIZATION) => true,
            (TransactionState.PENDING_SIGNATURE, TransactionState.QUEUED) => true,
            (TransactionState.PENDING_SIGNATURE, TransactionState.FAILED) => true,
            (TransactionState.PENDING_SIGNATURE, TransactionState.REJECTED) => true,
            (TransactionState.PENDING_SIGNATURE, TransactionState.CANCELLED) => true,

            (TransactionState.PENDING_AUTHORIZATION, TransactionState.QUEUED) => true,
            (TransactionState.PENDING_AUTHORIZATION, TransactionState.FAILED) => true,
            (TransactionState.PENDING_AUTHORIZATION, TransactionState.REJECTED) => true,
            (TransactionState.PENDING_AUTHORIZATION, TransactionState.CANCELLED) => true,

            (TransactionState.QUEUED, TransactionState.BROADCASTING) => true,
            (TransactionState.QUEUED, TransactionState.FAILED) => true,
            (TransactionState.QUEUED, TransactionState.CANCELLED) => true,
            (TransactionState.QUEUED, TransactionState.TIMEOUT) => true,

            (TransactionState.BROADCASTING, TransactionState.CONFIRMING) => true,
            (TransactionState.BROADCASTING, TransactionState.COMPLETED) => true,
            (TransactionState.BROADCASTING, TransactionState.FAILED) => true,
            (TransactionState.BROADCASTING, TransactionState.TIMEOUT) => true,

            (TransactionState.CONFIRMING, TransactionState.COMPLETED) => true,
            (TransactionState.CONFIRMING, TransactionState.FAILED) => true,
            (TransactionState.CONFIRMING, TransactionState.TIMEOUT) => true,

            _ => false
        };
    }

    public static void ValidateTransition(this TransactionState currentState, TransactionState newState)
    {
        if (!currentState.CanTransitionTo(newState))
        {
            throw new InvalidOperationException(
                $"Invalid state transition from {currentState} to {newState}");
        }
    }
}
