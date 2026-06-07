using System.Windows;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;

namespace ScreenManagement.UI.Services;

/// <summary>
/// 系统托盘图标服务，使用 WinForms NotifyIcon
/// </summary>
public class TrayIconService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private readonly IDisplayService _displayService;
    private readonly IHdrService _hdrService;
    private readonly IMonitorEnumerationService _monitorService;
    private readonly IConfigService _configService;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(
        IDisplayService displayService,
        IHdrService hdrService,
        IMonitorEnumerationService monitorService,
        IConfigService configService)
    {
        _displayService = displayService;
        _hdrService = hdrService;
        _monitorService = monitorService;
        _configService = configService;
    }

    /// <summary>初始化托盘图标</summary>
    public void Initialize()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Screen Management",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _notifyIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.BalloonTipClicked += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

        // 监听显示模式变化以更新图标
        _displayService.DisplayModeChanged += OnDisplayModeChanged;
    }

    /// <summary>构建右键菜单</summary>
    private System.Windows.Forms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        // 显示模式子菜单
        var modeMenu = new System.Windows.Forms.ToolStripMenuItem("显示模式");
        modeMenu.DropDownItems.Add(CreateModeItem("仅电脑屏幕", DisplayMode.Internal));
        modeMenu.DropDownItems.Add(CreateModeItem("复制", DisplayMode.Clone));
        modeMenu.DropDownItems.Add(CreateModeItem("扩展", DisplayMode.Extend));
        modeMenu.DropDownItems.Add(CreateModeItem("仅第二屏幕", DisplayMode.External));
        menu.Items.Add(modeMenu);

        // HDR 子菜单
        var hdrMenu = new System.Windows.Forms.ToolStripMenuItem("HDR");
        hdrMenu.DropDownOpening += async (s, e) =>
        {
            hdrMenu.DropDownItems.Clear();
            var displays = await _monitorService.GetDisplaysAsync();
            foreach (var display in displays.Where(d => d.SupportsHdr))
            {
                var item = new System.Windows.Forms.ToolStripMenuItem(
                    $"{display.DisplayName} HDR {(display.HdrEnabled ? "✓ 开" : "○ 关")}")
                {
                    Tag = display.DeviceId
                };
                item.Click += async (s2, e2) =>
                {
                    if (s2 is System.Windows.Forms.ToolStripMenuItem mi && mi.Tag is string deviceId)
                    {
                        await _hdrService.ToggleHdrAsync(deviceId);
                    }
                };
                hdrMenu.DropDownItems.Add(item);
            }

            if (hdrMenu.DropDownItems.Count == 0)
            {
                hdrMenu.DropDownItems.Add(
                    new System.Windows.Forms.ToolStripLabel("无 HDR 显示器") { Enabled = false });
            }
        };
        menu.Items.Add(hdrMenu);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // 打开主界面
        var openItem = new System.Windows.Forms.ToolStripMenuItem("打开主界面");
        openItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(openItem);

        // 关于
        var aboutItem = new System.Windows.Forms.ToolStripMenuItem("关于");
        aboutItem.Click += (s, e) =>
        {
            var aboutWindow = new Views.AboutWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
        };
        menu.Items.Add(aboutItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // 退出
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    private System.Windows.Forms.ToolStripItem CreateModeItem(string text, DisplayMode mode)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(text)
        {
            Tag = mode
        };
        item.Click += async (s, e) =>
        {
            if (s is System.Windows.Forms.ToolStripMenuItem mi && mi.Tag is DisplayMode m)
                await _displayService.SetDisplayModeAsync(m);
        };
        return item;
    }

    /// <summary>更新托盘图标</summary>
    public void UpdateIcon(DisplayMode mode)
    {
        if (_notifyIcon == null) return;

        // 使用不同图标表示不同模式（若有资源）
        _notifyIcon.Icon = mode switch
        {
            DisplayMode.Internal => System.Drawing.SystemIcons.Application,
            DisplayMode.Clone => System.Drawing.SystemIcons.Shield,
            DisplayMode.Extend => System.Drawing.SystemIcons.Information,
            DisplayMode.External => System.Drawing.SystemIcons.WinLogo,
            _ => System.Drawing.SystemIcons.Application
        };

        _notifyIcon.Text = $"Screen Management - {GetModeText(mode)}";
    }

    /// <summary>更新托盘提示</summary>
    public void UpdateTooltip(string text)
    {
        if (_notifyIcon != null)
            _notifyIcon.Text = text;
    }

    /// <summary>显示气泡通知</summary>
    public void ShowBalloon(string title, string text, System.Windows.Forms.ToolTipIcon icon =
        System.Windows.Forms.ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
    }

    public void Dispose()
    {
        _displayService.DisplayModeChanged -= OnDisplayModeChanged;
        _notifyIcon?.Dispose();
    }

    private void OnDisplayModeChanged(object? sender, DisplayMode mode)
    {
        // UI 线程安全调用
        if (System.Windows.Application.Current?.Dispatcher != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateIcon(mode));
        }
    }

    private static string GetModeText(DisplayMode mode) => mode switch
    {
        DisplayMode.Internal => "仅电脑屏幕",
        DisplayMode.Clone => "复制",
        DisplayMode.Extend => "扩展",
        DisplayMode.External => "仅第二屏幕",
        _ => "未知"
    };
}
