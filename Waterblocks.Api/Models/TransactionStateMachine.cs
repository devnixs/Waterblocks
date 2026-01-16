namespace Waterblocks.Api.Models;

public sealed class TransactionStateMachine
{
    public static TransactionStateMachine Instance { get; } = new();

    private static readonly TransactionState[] NonTerminalStateList =
    {
        TransactionState.SUBMITTED,
        TransactionState.PENDING_SIGNATURE,
        TransactionState.PENDING_AUTHORIZATION,
        TransactionState.QUEUED,
        TransactionState.BROADCASTING,
        TransactionState.CONFIRMING,
    };

    private static readonly HashSet<TransactionState> TerminalStates = new()
    {
        TransactionState.COMPLETED,
        TransactionState.FAILED,
        TransactionState.REJECTED,
        TransactionState.CANCELLED,
        TransactionState.TIMEOUT,
    };

    private static readonly IReadOnlyDictionary<TransactionState, HashSet<TransactionState>> AllowedTransitions =
        new Dictionary<TransactionState, HashSet<TransactionState>>
        {
            [TransactionState.SUBMITTED] = new HashSet<TransactionState>
            {
                TransactionState.PENDING_SIGNATURE,
                TransactionState.PENDING_AUTHORIZATION,
                TransactionState.QUEUED,
                TransactionState.FAILED,
                TransactionState.REJECTED,
                TransactionState.CANCELLED,
            },
            [TransactionState.PENDING_SIGNATURE] = new HashSet<TransactionState>
            {
                TransactionState.PENDING_AUTHORIZATION,
                TransactionState.QUEUED,
                TransactionState.FAILED,
                TransactionState.REJECTED,
                TransactionState.CANCELLED,
            },
            [TransactionState.PENDING_AUTHORIZATION] = new HashSet<TransactionState>
            {
                TransactionState.QUEUED,
                TransactionState.FAILED,
                TransactionState.REJECTED,
                TransactionState.CANCELLED,
            },
            [TransactionState.QUEUED] = new HashSet<TransactionState>
            {
                TransactionState.BROADCASTING,
                TransactionState.FAILED,
                TransactionState.CANCELLED,
                TransactionState.TIMEOUT,
            },
            [TransactionState.BROADCASTING] = new HashSet<TransactionState>
            {
                TransactionState.CONFIRMING,
                TransactionState.COMPLETED,
                TransactionState.FAILED,
                TransactionState.TIMEOUT,
            },
            [TransactionState.CONFIRMING] = new HashSet<TransactionState>
            {
                TransactionState.COMPLETED,
                TransactionState.FAILED,
                TransactionState.TIMEOUT,
            },
        };

    public IReadOnlyCollection<TransactionState> NonTerminalStates => NonTerminalStateList;

    public bool IsTerminal(TransactionState state)
    {
        return TerminalStates.Contains(state);
    }

    public bool CanTransition(TransactionState currentState, TransactionState newState)
    {
        if (IsTerminal(currentState))
        {
            return false;
        }

        return AllowedTransitions.TryGetValue(currentState, out var allowed) && allowed.Contains(newState);
    }

    public void ValidateTransition(TransactionState currentState, TransactionState newState)
    {
        if (!CanTransition(currentState, newState))
        {
            throw new InvalidOperationException(
                $"Invalid state transition from {currentState} to {newState}");
        }
    }

    public TransactionState? GetNextAutoState(TransactionState currentState)
    {
        return currentState switch
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
}
