using Microsoft.Win32;

namespace MotionSicknessHelper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        string configPath = Path.Combine(System.AppContext.BaseDirectory, "config.json");
        var config = OverlayConfig.Load(configPath);
        if (!File.Exists(configPath))
        {
            try
            {
                config.Save(configPath);
            }
            catch
            {
                // If the folder is read-only, still run with in-memory defaults.
            }
        }

        Application.Run(new AppContext(configPath, config));
    }
}

internal sealed class AppContext : ApplicationContext
{
    private readonly string _configPath;
    private readonly OverlayForm _overlay;
    private readonly NotifyIcon _notifyIcon;
    private OverlayConfig _config;

    public AppContext(string configPath, OverlayConfig config)
    {
        _configPath = configPath;
        _config = config;

        _overlay = new OverlayForm(_config);
        _overlay.Show();
        _overlay.RefreshOverlay();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "晕3D辅助 - 顶层引导线",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var settings = new ToolStripMenuItem("设置...");
        settings.Click += (_, _) => OpenSettings();

        var reload = new ToolStripMenuItem("重新加载配置");
        reload.Click += (_, _) => Reload();

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (_, _) => ExitApp();

        menu.Items.Add(settings);
        menu.Items.Add(reload);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config, _configPath);
        form.Applied += cfg =>
        {
            _config = cfg;
            _overlay.ApplyConfig(cfg);
        };

        if (form.ShowDialog() == DialogResult.OK && form.Result is { } newConfig)
        {
            _config = newConfig;
            _overlay.ApplyConfig(_config);
        }
    }

    private void Reload()
    {
        _config = OverlayConfig.Load(_configPath);
        _overlay.ApplyConfig(_config);
        _notifyIcon.ShowBalloonTip(1500, "晕3D辅助", "配置已重新加载。", ToolTipIcon.Info);
    }

    private void ExitApp()
    {
        _overlay.Close();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            if (_overlay.IsHandleCreated)
            {
                _overlay.BeginInvoke(() => _overlay.RefreshOverlay());
            }
            else
            {
                _overlay.RefreshOverlay();
            }
        }
        catch
        {
            // ignore display-change races
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _notifyIcon.Dispose();
            _overlay.Dispose();
        }
        base.Dispose(disposing);
    }
}
