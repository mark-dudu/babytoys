using BabyToys.Services;

namespace BabyToys.Tests;

[TestClass]
public sealed class StartupRegistrationPolicyTests
{
    private const string CurrentExecutablePath = @"C:\Apps\Baby Toys\BabyToys.exe";

    [TestMethod]
    public void MatchesDesiredState_EnabledWithCurrentCommand_ReturnsTrue()
    {
        var command = StartupRegistrationPolicy.BuildCommand(CurrentExecutablePath);

        Assert.AreEqual("\"C:\\Apps\\Baby Toys\\BabyToys.exe\" --minimized", command);
        Assert.IsTrue(StartupRegistrationPolicy.MatchesDesiredState(command, CurrentExecutablePath, enabled: true));
    }

    [TestMethod]
    public void MatchesDesiredState_EnabledWithDifferentPath_ReturnsFalse()
    {
        const string command = "\"C:\\Old Apps\\BabyToys.exe\" --minimized";

        Assert.IsFalse(StartupRegistrationPolicy.MatchesDesiredState(command, CurrentExecutablePath, enabled: true));
    }

    [TestMethod]
    public void MatchesDesiredState_EnabledWithMalformedCommand_ReturnsFalse()
    {
        const string command = "\"C:\\Apps\\Baby Toys\\BabyToys.exe\"";

        Assert.IsFalse(StartupRegistrationPolicy.MatchesDesiredState(command, CurrentExecutablePath, enabled: true));
    }

    [TestMethod]
    public void MatchesDesiredState_EnabledWithoutRegistration_ReturnsFalse()
    {
        Assert.IsFalse(StartupRegistrationPolicy.MatchesDesiredState(null, CurrentExecutablePath, enabled: true));
    }

    [TestMethod]
    public void MatchesDesiredState_DisabledWithoutRegistration_ReturnsTrue()
    {
        Assert.IsTrue(StartupRegistrationPolicy.MatchesDesiredState(null, CurrentExecutablePath, enabled: false));
    }

    [TestMethod]
    public void MatchesDesiredState_DisabledWithStaleRegistration_ReturnsFalse()
    {
        const string command = "\"C:\\Old Apps\\BabyToys.exe\" --minimized";

        Assert.IsFalse(StartupRegistrationPolicy.MatchesDesiredState(command, CurrentExecutablePath, enabled: false));
    }
}
