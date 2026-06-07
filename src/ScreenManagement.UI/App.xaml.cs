using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Services;
using ScreenManagement.UI.Services;
using ScreenManagement.UI.ViewModels;
using ScreenManagement.UI.Views;
using WpfApplication = System.Windows.Application;

namespace ScreenManagement.UI;

/// <summary>
/// WPF 应用入口，配置 DI 容器和启动流程
/// </summary>
public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        var builder = Host.CreateApplicationBuilder();

        // ========== Business 层服务 ==========
        builder.Services.AddSingleton<IDisplayService, DisplayService>();
        builder.Services.AddSingleton<IHdrService, HdrService>();
        builder.Services.AddSingleton<IHotkeyService, HotkeyService>();
        builder.Services.AddSingleton<IMonitorEnumerationService, MonitorEnumerationService>();
        builder.Services.AddSingleton<IConfigService, ConfigService>();
        builder.Services.AddSingleton<IAutostartService, AutostartService>();
        builder.Services.AddSingleton<ICompositeActionService, CompositeActionService>();

        // ========== UI 层服务 ==========
        builder.Services.AddSingleton<TrayIconService>();
        builder.Services.AddSingleton<NotificationService>();

        // ========== ViewModels ==========
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<HotkeySettingsViewModel>();
        builder.Services.AddTransient<AboutViewModel>();

        // ========== Windows ==========
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddTransient<HotkeySettingsWindow>();
        builder.Services.AddTransient<AboutWindow>();

        // ========== 日志 ==========
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        _host = builder.Build();
    }

    /// <summary>从 DI 容器获取服务</summary>
    public T? GetService<T>() where T : class => _host.Services.GetService<T>();

    /// <summary>从 DI 容器获取必需服务</summary>
    public T GetRequiredService<T>() where T : class => _host.Services.GetRequiredService<T>();

    /// <summary>DI 容器</summary>
    public IServiceProvider Services => _host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        // 1. 单实例检查
        if (!ScreenManagement.Business.Services.SingleInstance.TryAcquire())
        {
            ActivateExistingInstance();
            Shutdown();
            return;
        }

        // 2. 加载配置
        var configService = GetRequiredService<IConfigService>();
        var config = await configService.LoadAsync();

        // 3. 初始化系统托盘
        var trayService = GetRequiredService<TrayIconService>();
        trayService.Initialize();
        trayService.ShowWindowRequested += (s, e) =>
        {
            var window = MainWindow ?? GetRequiredService<MainWindow>();
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        };
        trayService.ExitRequested += (s, e) => Shutdown();

        // 4. 判断启动模式
        bool isAutostart = Environment.GetCommandLineArgs().Contains("--minimized");
        if (!config.StartMinimized && !isAutostart)
        {
            var mainWindow = GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var hotkeyService = GetService<IHotkeyService>();
        hotkeyService?.UnregisterAll();

        GetService<TrayIconService>()?.Dispose();

        ScreenManagement.Business.Services.SingleInstance.Release();
        _host.Dispose();

        base.OnExit(e);
    }

    /// <summary>激活已运行的实例窗口</summary>
    private static void ActivateExistingInstance()
    {
        var handle = NativeHelper.FindWindow(null, "Screen Management");
        if (handle != IntPtr.Zero)
        {
            NativeHelper.SetForegroundWindow(handle);
            NativeHelper.ShowWindow(handle, 9); // SW_RESTORE
        }
    }
}

/// <summary>应用内使用的简化 P/Invoke 工具</summary>
internal static class NativeHelper
{
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
