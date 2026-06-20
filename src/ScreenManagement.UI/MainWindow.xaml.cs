using System.Windows;
using System.Windows.Interop;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.UI.Services;
using ScreenManagement.UI.ViewModels;

namespace ScreenManagement.UI;

/// <summary>
/// 主窗口 — 显示器状态和快捷切换
/// </summary>
public partial class MainWindow : Window
{
    private const int WM_HOTKEY = 0x0312;

    private readonly IHotkeyService _hotkeyService;
    private readonly IConfigService _configService;
    private readonly IMonitorEnumerationService _monitorService;
    private readonly IDisplayService _displayService;
    private readonly TrayIconService _trayService;
    private readonly NotificationService _notificationService;

    public MainWindow(
        MainViewModel viewModel,
        IHotkeyService hotkeyService,
        IConfigService configService,
        IMonitorEnumerationService monitorService,
        IDisplayService displayService,
        TrayIconService trayService,
        NotificationService notificationService)
    {
        DataContext = viewModel;
        _hotkeyService = hotkeyService;
        _configService = configService;
        _monitorService = monitorService;
        _displayService = displayService;
        _trayService = trayService;
        _notificationService = notificationService;

        InitializeComponent();

        Loaded += OnLoaded;
        Closing += OnClosing;
        _monitorService.DisplaysChanged += OnDisplaysChanged;
        _displayService.DisplayModeChanged += OnDisplayModeChanged;
    }

    /// <summary>
    /// HWND 创建后（窗口显示前）初始化热键钩子，
    /// 由 App.xaml.cs 调用 EnsureHandle() 触发，即使窗口隐藏也能生效。
    /// </summary>
    protected override async void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // 挂载 WM_HOTKEY 消息钩子
        if (HwndSource.FromHwnd(hwnd) is HwndSource source)
            source.AddHook(WndProc);

        _hotkeyService.Initialize(hwnd);
        _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

        var config = await _configService.LoadAsync();
        await _hotkeyService.RegisterAllAsync(config.HotkeyBindings, hwnd);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            await vm.RefreshDisplaysCommand.ExecuteAsync(null);
    }

    /// <summary>窗口消息钩子 — 将 WM_HOTKEY 转发给 HotkeyService</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            _hotkeyService.HandleHotkeyMessage(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // 关闭窗口时隐藏而非退出（托盘运行）
        e.Cancel = true;
        Hide();
    }

    private void OnDisplaysChanged(object? sender, IReadOnlyList<ScreenManagement.Business.Models.DisplayInfo> displays)
    {
        Dispatcher.Invoke(async () =>
        {
            if (DataContext is MainViewModel vm)
                await vm.RefreshDisplaysCommand.ExecuteAsync(null);
        });
    }

    private void OnDisplayModeChanged(object? sender, ScreenManagement.Business.Models.DisplayMode mode)
    {
        _trayService.UpdateIcon(mode);
        _notificationService.ShowNotification(
            "显示模式已切换",
            $"当前模式: {GetModeName(mode)}");
    }

    private async void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
    {
        var compositeService = ((App)System.Windows.Application.Current)
            .GetRequiredService<ICompositeActionService>();
        await compositeService.ExecuteAsync(e.Binding);
    }

    private static string GetModeName(ScreenManagement.Business.Models.DisplayMode mode) => mode switch
    {
        ScreenManagement.Business.Models.DisplayMode.Internal => "仅电脑屏幕",
        ScreenManagement.Business.Models.DisplayMode.Clone => "复制",
        ScreenManagement.Business.Models.DisplayMode.Extend => "扩展",
        ScreenManagement.Business.Models.DisplayMode.External => "仅第二屏幕",
        _ => "未知"
    };
}