using BabyToys.Models;
using BabyToys.Sessions;

namespace BabyToys.Tests;

[TestClass]
public sealed class EntryConfirmationCountdownTests
{
    [TestMethod]
    public void CountdownConfirmsOnlyAfterAllSecondsElapsed()
    {
        var countdown = new EntryConfirmationCountdown();

        Assert.AreEqual(3, countdown.RemainingSeconds);
        Assert.AreEqual(EntryConfirmationState.Waiting, countdown.Tick());
        Assert.AreEqual(2, countdown.RemainingSeconds);
        Assert.AreEqual(EntryConfirmationState.Waiting, countdown.Tick());
        Assert.AreEqual(1, countdown.RemainingSeconds);
        Assert.AreEqual(EntryConfirmationState.Confirmed, countdown.Tick());
        Assert.AreEqual(0, countdown.RemainingSeconds);
    }

    [TestMethod]
    public void CancelPreventsLaterConfirmation()
    {
        var countdown = new EntryConfirmationCountdown();

        Assert.AreEqual(EntryConfirmationState.Canceled, countdown.Cancel());
        Assert.AreEqual(EntryConfirmationState.Canceled, countdown.Cancel());
        Assert.AreEqual(EntryConfirmationState.Canceled, countdown.Tick());
        Assert.AreEqual(3, countdown.RemainingSeconds);
    }

    [TestMethod]
    public void ConfirmedCountdownCannotBeCanceled()
    {
        var countdown = new EntryConfirmationCountdown(seconds: 1);

        Assert.AreEqual(EntryConfirmationState.Confirmed, countdown.Tick());
        Assert.AreEqual(EntryConfirmationState.Confirmed, countdown.Cancel());
    }

    [TestMethod]
    public void CountdownRejectsNonPositiveDuration()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new EntryConfirmationCountdown(seconds: 0));
    }
}
