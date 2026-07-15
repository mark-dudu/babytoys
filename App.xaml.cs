using System.Windows;
using BabyToys.Services;

namespace BabyToys;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var launchMode = e.Args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
            ? "minimized"
            : "interactive";
        AppLogService.Current.Info($"Application started. Launch mode: {launchMode}; process id: {Environment.ProcessId}.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogService.Current.Info("Application exited.");
        base.OnExit(e);
    }
}
