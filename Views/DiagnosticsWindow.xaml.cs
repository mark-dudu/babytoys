using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using BabyToys.Services;
using Forms = System.Windows.Forms;

namespace BabyToys.Views;

public partial class DiagnosticsWindow : Window
{
    private readonly WakeDeviceService _wakeDeviceService = new();
    private readonly SessionRecoveryService _recoveryService = new();

    public DiagnosticsWindow()
    {
        InitializeComponent();
        Icon = AppIconService.WindowIcon;
        Loaded += async (_, _) => await RefreshReportAsync();
    }

    private async Task RefreshReportAsync()
    {
        StatusTextBlock.Text = "正在检查可唤醒设备…";
        var devices = await _wakeDeviceService.GetArmedDevicesAsync();
        var marker = _recoveryService.ReadPreviousMarker();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "未知";
        var report = new StringBuilder()
            .AppendLine($"BabyToys 版本：{version}")
            .AppendLine($"操作系统：{Environment.OSVersion}")
            .AppendLine($"进程架构：{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}")
            .AppendLine($"显示器数量：{Forms.Screen.AllScreens.Length}")
            .AppendLine($"设置文件：{AppPaths.SettingsPath}")
            .AppendLine($"日志目录：{AppPaths.LogsDirectory}")
            .AppendLine($"异常会话标记：{(marker is null ? "无" : $"{marker.StartedAt:yyyy-MM-dd HH:mm:ss zzz}")}")
            .AppendLine()
            .AppendLine("当前允许唤醒电脑的设备：");

        if (devices.Count == 0)
        {
            report.AppendLine("（未检测到，或系统未返回设备）");
        }
        else
        {
            foreach (var device in devices)
            {
                report.AppendLine($"- {device}");
            }
        }

        report
            .AppendLine()
            .AppendLine("如需关闭某个设备的唤醒权限，请以管理员身份打开终端并运行：")
            .AppendLine("powercfg /devicedisablewake \"设备名称\"")
            .AppendLine()
            .AppendLine("恢复权限：")
            .AppendLine("powercfg /deviceenablewake \"设备名称\"");

        ReportTextBox.Text = report.ToString();
        StatusTextBlock.Text = $"检查完成：{devices.Count} 个可唤醒设备";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(ReportTextBox.Text);
            StatusTextBlock.Text = "诊断报告已复制";
        }
        catch (ExternalException ex)
        {
            AppLogService.Current.Error("Failed to copy diagnostics report to clipboard.", ex);
            StatusTextBlock.Text = "剪贴板暂时不可用，请重试";
        }
    }

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = AppLogService.Current.TryOpenLogsDirectory()
            ? "已打开日志目录"
            : "无法打开日志目录";
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshReportAsync();
    }
}
