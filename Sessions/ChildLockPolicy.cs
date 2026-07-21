using BabyToys.Models;

namespace BabyToys.Sessions;

public static class ChildLockPolicy
{
    public static readonly TimeSpan UnlockHoldDuration = TimeSpan.FromSeconds(3);

    public static bool IsUnlockReady(TimeSpan elapsed) => elapsed >= UnlockHoldDuration;

    public static ChildLockState GetStateAfterSleepRequest(bool succeeded) =>
        succeeded ? ChildLockState.Sleeping : ChildLockState.SleepFailedBlack;

    public static TimeSpan GetRemaining(DateTimeOffset deadline, DateTimeOffset now)
    {
        var remaining = deadline - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
