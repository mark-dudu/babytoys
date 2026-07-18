using System.Windows;
using BabyToys.Services;

namespace BabyToys;

public partial class App : System.Windows.Application
{
    private readonly SingleInstanceService _singleInstanceService = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_singleInstanceService.TryAcquire())
        {
            System.Windows.MessageBox.Show(
                "BabyToys 已经在运行，请检查主窗口或系统托盘。",
                "BabyToys",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        var launchMode = e.Args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase))
            ? "minimized"
            : "interactive";
        AppLogService.Current.Info($"Application started. Launch mode: {launchMode}; process id: {Environment.ProcessId}.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogService.Current.Info("Application exited.");
        _singleInstanceService.Dispose();
        base.OnExit(e);
    }
}
