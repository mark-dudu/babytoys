using BabyToys.Models;

namespace BabyToys.Sessions;

public sealed class EntryConfirmationCountdown
{
    public const int DefaultSeconds = 3;

    public int RemainingSeconds { get; private set; }
    public EntryConfirmationState State { get; private set; } = EntryConfirmationState.Waiting;

    public EntryConfirmationCountdown(int seconds = DefaultSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(seconds, 0);
        RemainingSeconds = seconds;
    }

    public EntryConfirmationState Tick()
    {
        if (State != EntryConfirmationState.Waiting)
        {
            return State;
        }

        RemainingSeconds = Math.Max(0, RemainingSeconds - 1);
        if (RemainingSeconds == 0)
        {
            State = EntryConfirmationState.Confirmed;
        }

        return State;
    }

    public EntryConfirmationState Cancel()
    {
        if (State == EntryConfirmationState.Waiting)
        {
            State = EntryConfirmationState.Canceled;
        }

        return State;
    }
}
