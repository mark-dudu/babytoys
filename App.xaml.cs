using System.Windows;
using BabyToys.Services;

namespace BabyToys;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLogService.Current.Info("Application started.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogService.Current.Info("Application exited.");
        base.OnExit(e);
    }
}
