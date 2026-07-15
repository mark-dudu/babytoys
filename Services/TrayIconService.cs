using Forms = System.Windows.Forms;

namespace BabyToys.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public event EventHandler? ShowRequested;
    public event EventHandler? StartRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 BabyToys", null, (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("启动儿童模式", null, (_, _) => StartRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new Forms.NotifyIcon
        {
            Text = "BabyToys 儿童模式",
            Icon = System.Drawing.SystemIcons.Shield,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }
}
