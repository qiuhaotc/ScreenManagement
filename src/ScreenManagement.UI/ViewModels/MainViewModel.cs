using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.UI.Views;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 主界面 ViewModel
/// </summary>
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private DisplayMode _currentMode;
    [ObservableProperty] private ObservableCollection<DisplayInfo> _displays = new();
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _autoStartEnabled;

    private readonly IDisplayService _displayService;
    private readonly IHdrService _hdrService;
    private readonly IMonitorEnumerationService _monitorService;
    private readonly IConfigService _configService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IAutostartService _autostartService;

    public MainViewModel(
        IDisplayService displayService,
        IHdrService hdrService,
        IMonitorEnumerationService monitorService,
        IConfigService configService,
        IHotkeyService hotkeyService,
        IAutostartService autostartService)
    {
        _displayService = displayService;
        _hdrService = hdrService;
        _monitorService = monitorService;
        _configService = configService;
        _hotkeyService = hotkeyService;
        _autostartService = autostartService;

        _displayService.DisplayModeChanged += OnDisplayModeChanged;

        // 从注册表读取开机自启状态
        _autoStartEnabled = _autostartService.IsAutostartEnabled();
    }

    partial void OnAutoStartEnabledChanged(bool value)
    {
        _autostartService.SetAutostart(value);
        _ = SaveAutoStartToConfigAsync(value);
    }

    private async Task SaveAutoStartToConfigAsync(bool value)
    {
        var config = await _configService.LoadAsync();
        config.AutoStart = value;
        await _configService.SaveAsync(config);
    }

    [RelayCommand]
    private async Task SetDisplayModeAsync(string modeStr)
    {
        if (!Enum.TryParse<DisplayMode>(modeStr, out var mode))
            return;

        IsLoading = true;
        StatusMessage = "正在切换...";

        try
        {
            var success = await _displayService.SetDisplayModeAsync(mode);
            StatusMessage = success
                ? $"已切换到: {GetModeDisplayName(mode)}"
                : "切换失败，请检查显示器连接";

            if (success)
            {
                CurrentMode = mode;
                await RefreshDisplaysAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"切换失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ToggleHdrAsync(DisplayInfo? display)
    {
        if (display == null || !display.SupportsHdr)
            return;

        try
        {
            var success = await _hdrService.ToggleHdrAsync(display.DeviceId);
            // UI 刷新由 HdrStateChanged → MonitorEnumerationService.RefreshAsync
            // → DisplaysChanged → MainWindow.OnDisplaysChanged 事件链完成
            StatusMessage = success
                ? $"已切换 {display.DisplayName} 的 HDR"
                : "HDR 切换失败";
        }
        catch (Exception ex)
        {
            StatusMessage = $"HDR 切换失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshDisplaysAsync()
    {
        try
        {
            var displays = await _monitorService.GetDisplaysAsync();
            Displays = new ObservableCollection<DisplayInfo>(displays);
            CurrentMode = _displayService.GetCurrentMode();
        }
        catch
        {
            // 容错：显示空列表
            Displays = new ObservableCollection<DisplayInfo>();
        }
    }

    [RelayCommand]
    private void OpenHotkeySettings()
    {
        var app = (App)System.Windows.Application.Current;
        var vm = app.GetRequiredService<HotkeySettingsViewModel>();
        var window = new HotkeySettingsWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        var app = (App)System.Windows.Application.Current;
        var vm = app.GetRequiredService<AboutViewModel>();
        var window = new AboutWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    private void OnDisplayModeChanged(object? sender, DisplayMode mode)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentMode = mode;
            StatusMessage = $"已切换到: {GetModeDisplayName(mode)}";
        });
    }

    public static string GetModeDisplayName(DisplayMode mode) => mode switch
    {
        DisplayMode.Internal => "仅电脑屏幕",
        DisplayMode.Clone => "复制",
        DisplayMode.Extend => "扩展",
        DisplayMode.External => "仅第二屏幕",
        _ => "未知"
    };
}
