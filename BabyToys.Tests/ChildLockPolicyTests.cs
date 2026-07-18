using BabyToys.Models;
using BabyToys.Sessions;

namespace BabyToys.Tests;

[TestClass]
public sealed class ChildLockPolicyTests
{
    [TestMethod]
    public void UnlockRequiresFullHoldDuration()
    {
        Assert.IsFalse(ChildLockPolicy.IsUnlockReady(TimeSpan.FromMilliseconds(2999)));
        Assert.IsTrue(ChildLockPolicy.IsUnlockReady(TimeSpan.FromSeconds(3)));
    }

    [TestMethod]
    public void SleepResultSelectsSafeTerminalState()
    {
        Assert.AreEqual(ChildLockState.Ended, ChildLockPolicy.GetStateAfterSleepRequest(succeeded: true));
        Assert.AreEqual(ChildLockState.SleepFailedBlack, ChildLockPolicy.GetStateAfterSleepRequest(succeeded: false));
    }

    [TestMethod]
    public void RemainingTimeNeverBecomesNegative()
    {
        var now = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        Assert.AreEqual(TimeSpan.FromSeconds(5), ChildLockPolicy.GetRemaining(now.AddSeconds(5), now));
        Assert.AreEqual(TimeSpan.Zero, ChildLockPolicy.GetRemaining(now.AddSeconds(-1), now));
    }
}
