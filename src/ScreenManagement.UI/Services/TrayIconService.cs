using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.UI.ViewModels;

namespace ScreenManagement.UI.Services;

/// <summary>
/// 系统托盘图标服务，使用 WinForms NotifyIcon
/// </summary>
public class TrayIconService : IDisposable
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private Bitmap? _iconBitmap;
    private IntPtr _iconHandle = IntPtr.Zero;

    // 缓存：避免 DropDownOpening 中的 async/await 竞态问题
    private List<ScreenManagement.Business.Models.DisplayInfo> _cachedDisplays = new();
    private List<ScreenManagement.Business.Models.HotkeyBinding> _cachedHotkeyBindings = new();

    private readonly IDisplayService _displayService;
    private readonly IHdrService _hdrService;
    private readonly IMonitorEnumerationService _monitorService;
    private readonly IConfigService _configService;
    private readonly ICompositeActionService _compositeActionService;

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? ExitRequested;

    public TrayIconService(
        IDisplayService displayService,
        IHdrService hdrService,
        IMonitorEnumerationService monitorService,
        IConfigService configService,
        ICompositeActionService compositeActionService)
    {
        _displayService = displayService;
        _hdrService = hdrService;
        _monitorService = monitorService;
        _configService = configService;
        _compositeActionService = compositeActionService;
    }

    /// <summary>初始化托盘图标</summary>
    public void Initialize(ScreenManagement.Business.Models.AppConfig initialConfig)
    {
        // 缓存初始快捷键绑定
        _cachedHotkeyBindings = initialConfig.HotkeyBindings
            ?.Where(b => b.IsEnabled).ToList() ?? new();

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = CreateMonitorIcon(Color.FromArgb(255, 37, 99, 235)),
            Text = "Screen Management",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _notifyIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.BalloonTipClicked += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);

        _displayService.DisplayModeChanged += OnDisplayModeChanged;

        // 显示器变更时更新缓存（包含 HDR 状态）
        _monitorService.DisplaysChanged += (s, displays) =>
        {
            _cachedDisplays = displays.ToList();
        };

        // 配置保存时更新快捷键缓存
        _configService.ConfigChanged += (s, config) =>
        {
            _cachedHotkeyBindings = config.HotkeyBindings
                ?.Where(b => b.IsEnabled).ToList() ?? new();
        };
    }

    /// <summary>构建右键菜单</summary>
    private System.Windows.Forms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();

        // ── 显示模式子菜单 ──
        var modeMenu = new System.Windows.Forms.ToolStripMenuItem("🖥  显示模式");
        modeMenu.DropDownItems.Add(CreateModeItem("💻  仅电脑屏幕", DisplayMode.Internal));
        modeMenu.DropDownItems.Add(CreateModeItem("🔄  复制", DisplayMode.Clone));
        modeMenu.DropDownItems.Add(CreateModeItem("📺  扩展", DisplayMode.Extend));
        modeMenu.DropDownItems.Add(CreateModeItem("🖥  仅第二屏幕", DisplayMode.External));
        menu.Items.Add(modeMenu);

        // ── HDR 子菜单 ──
        var hdrMenu = new System.Windows.Forms.ToolStripMenuItem("✨  HDR");
        hdrMenu.DropDownOpening += (s, e) =>
        {
            hdrMenu.DropDownItems.Clear();
            var hdrDisplays = _cachedDisplays.Where(d => d.SupportsHdr).ToList();
            foreach (var display in hdrDisplays)
            {
                var item = new System.Windows.Forms.ToolStripMenuItem(
                    $"{display.DisplayName}  HDR {(display.HdrEnabled ? "● 开" : "○ 关")}")
                { Tag = display.DeviceId };
                item.Click += async (s2, e2) =>
                {
                    if (s2 is System.Windows.Forms.ToolStripMenuItem mi && mi.Tag is string deviceId)
                        await _hdrService.ToggleHdrAsync(deviceId);
                };
                hdrMenu.DropDownItems.Add(item);
            }
            if (hdrMenu.DropDownItems.Count == 0)
                hdrMenu.DropDownItems.Add(new System.Windows.Forms.ToolStripLabel("无 HDR 显示器") { Enabled = false });
        };
        menu.Items.Add(hdrMenu);

        // ── 快捷键动作子菜单 ──
        var hotkeyMenu = new System.Windows.Forms.ToolStripMenuItem("⌨  快捷键动作");
        hotkeyMenu.DropDownOpening += (s, e) =>
        {
            hotkeyMenu.DropDownItems.Clear();

            if (_cachedHotkeyBindings.Count == 0)
            {
                hotkeyMenu.DropDownItems.Add(new System.Windows.Forms.ToolStripLabel("未配置快捷键") { Enabled = false });
                return;
            }

            foreach (var binding in _cachedHotkeyBindings)
            {
                var desc = HotkeyBindingViewModel.GetActionDescription(binding);
                var hotkey = HotkeyBindingViewModel.FormatHotkey(binding.Modifiers, binding.Key);
                var item = new System.Windows.Forms.ToolStripMenuItem($"{desc}   [{hotkey}]")
                { Tag = binding };
                item.Click += async (s2, e2) =>
                {
                    if (s2 is System.Windows.Forms.ToolStripMenuItem mi && mi.Tag is HotkeyBinding b)
                        await _compositeActionService.ExecuteAsync(b);
                };
                hotkeyMenu.DropDownItems.Add(item);
            }
        };
        menu.Items.Add(hotkeyMenu);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── 打开主界面 ──
        var openItem = new System.Windows.Forms.ToolStripMenuItem("打开主界面");
        openItem.Click += (s, e) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(openItem);

        // ── 关于 ──
        var aboutItem = new System.Windows.Forms.ToolStripMenuItem("关于");
        aboutItem.Click += (s, e) =>
        {
            var app = (App)System.Windows.Application.Current;
            var vm = app.GetRequiredService<AboutViewModel>();
            var aboutWindow = new Views.AboutWindow(vm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            aboutWindow.ShowDialog();
        };
        menu.Items.Add(aboutItem);

        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        // ── 退出 ──
        var exitItem = new System.Windows.Forms.ToolStripMenuItem("退出");
        exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    private System.Windows.Forms.ToolStripItem CreateModeItem(string text, DisplayMode mode)
    {
        var item = new System.Windows.Forms.ToolStripMenuItem(text) { Tag = mode };
        item.Click += async (s, e) =>
        {
            if (s is System.Windows.Forms.ToolStripMenuItem mi && mi.Tag is DisplayMode m)
                await _displayService.SetDisplayModeAsync(m);
        };
        return item;
    }

    // ────────────────────────── 图标 ──────────────────────────

    /// <summary>更新托盘图标（根据显示模式变色）</summary>
    public void UpdateIcon(DisplayMode mode)
    {
        if (_notifyIcon == null) return;

        var color = mode switch
        {
            DisplayMode.Internal  => Color.FromArgb(255, 99, 102, 241),  // 靛蓝
            DisplayMode.Clone     => Color.FromArgb(255,  8, 145, 178),  // 青色
            DisplayMode.Extend    => Color.FromArgb(255,  5, 150, 105),  // 绿色
            DisplayMode.External  => Color.FromArgb(255, 124, 58,  237), // 紫色
            _                     => Color.FromArgb(255, 37,  99,  235)  // 蓝色
        };

        ReplaceIcon(color);
        _notifyIcon.Text = $"Screen Management — {GetModeText(mode)}";
    }

    public void UpdateTooltip(string text)
    {
        if (_notifyIcon != null) _notifyIcon.Text = text;
    }

    public void ShowBalloon(string title, string text,
        System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info)
    {
        _notifyIcon?.ShowBalloonTip(3000, title, text, icon);
    }

    private void ReplaceIcon(Color accentColor)
    {
        var oldBitmap = _iconBitmap;
        _iconBitmap = CreateIconBitmap(accentColor);
        _iconHandle = _iconBitmap.GetHicon();
        _notifyIcon!.Icon = System.Drawing.Icon.FromHandle(_iconHandle);
        oldBitmap?.Dispose();
    }

    /// <summary>生成 32×32 显示器造型图标</summary>
    private static System.Drawing.Icon CreateMonitorIcon(Color accentColor)
    {
        var bmp = CreateIconBitmap(accentColor);
        var hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    private static Bitmap CreateIconBitmap(Color accentColor)
    {
        const int S = 32;
        var bmp = new Bitmap(S, S, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // 显示器外框（圆角矩形）
        using var frameBrush = new SolidBrush(accentColor);
        using var framePath = RoundedRect(new Rectangle(1, 1, 29, 21), 3);
        g.FillPath(frameBrush, framePath);

        // 屏幕区域（深色）
        using var screenBrush = new SolidBrush(Color.FromArgb(255, 12, 20, 40));
        g.FillRectangle(screenBrush, 4, 4, 23, 13);

        // 屏幕高光（左上角光晕）
        using var glowBrush = new SolidBrush(Color.FromArgb(55, 255, 255, 255));
        g.FillRectangle(glowBrush, 5, 5, 21, 5);

        // 屏幕内容线条（模拟内容）
        using var lineBrush = new SolidBrush(Color.FromArgb(100, accentColor.R, accentColor.G, accentColor.B));
        g.FillRectangle(lineBrush, 6, 12, 14, 2);
        g.FillRectangle(lineBrush, 6, 15, 9, 1);

        // 支柱
        using var standBrush = new SolidBrush(Darken(accentColor, 0.8f));
        g.FillRectangle(standBrush, 14, 22, 4, 4);

        // 底座（圆角）
        using var basePath = RoundedRect(new Rectangle(9, 26, 14, 4), 2);
        g.FillPath(standBrush, basePath);

        return bmp;
    }

    private static GraphicsPath RoundedRect(Rectangle b, int r)
    {
        int d = r * 2;
        var path = new GraphicsPath();
        path.AddArc(b.X, b.Y, d, d, 180, 90);
        path.AddArc(b.Right - d, b.Y, d, d, 270, 90);
        path.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
        path.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color Darken(Color c, float factor) =>
        Color.FromArgb(c.A, (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));

    // ────────────────────────────────────────────────────────

    public void Dispose()
    {
        _displayService.DisplayModeChanged -= OnDisplayModeChanged;
        _notifyIcon?.Dispose();
        _iconBitmap?.Dispose();
    }

    private void OnDisplayModeChanged(object? sender, DisplayMode mode)
    {
        if (System.Windows.Application.Current?.Dispatcher != null)
            System.Windows.Application.Current.Dispatcher.Invoke(() => UpdateIcon(mode));
    }

    private static string GetModeText(DisplayMode mode) => mode switch
    {
        DisplayMode.Internal => "仅电脑屏幕",
        DisplayMode.Clone    => "复制",
        DisplayMode.Extend   => "扩展",
        DisplayMode.External => "仅第二屏幕",
        _                    => "未知"
    };
}
