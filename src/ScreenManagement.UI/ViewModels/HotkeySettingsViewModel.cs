using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.UI.Views;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 快捷键设置 ViewModel
/// </summary>
public partial class HotkeySettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<HotkeyBindingViewModel> _bindings = new();

    [ObservableProperty] private string _conflictStatus = string.Empty;
    [ObservableProperty] private bool _hasConflicts;

    private readonly IHotkeyService _hotkeyService;
    private readonly IConfigService _configService;
    private readonly IMonitorEnumerationService _monitorService;

    public HotkeySettingsViewModel(
        IHotkeyService hotkeyService,
        IConfigService configService,
        IMonitorEnumerationService monitorService)
    {
        _hotkeyService = hotkeyService;
        _configService = configService;
        _monitorService = monitorService;
    }

    [RelayCommand]
    private async Task AddBindingAsync()
    {
        var editVm = new HotkeyEditViewModel(_monitorService);
        var dialog = new HotkeyEditDialog
        {
            DataContext = editVm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            Bindings.Add(HotkeyBindingViewModel.FromModel(editVm.ToModel()));
        }
    }

    [RelayCommand]
    private async Task EditBindingAsync(HotkeyBindingViewModel? binding)
    {
        if (binding == null) return;

        var model = binding.ToModel();
        var editVm = new HotkeyEditViewModel(_monitorService, model);
        var dialog = new HotkeyEditDialog
        {
            DataContext = editVm,
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            var idx = Bindings.IndexOf(binding);
            if (idx >= 0)
                Bindings[idx] = HotkeyBindingViewModel.FromModel(editVm.ToModel());
        }
    }

    [RelayCommand]
    private void DeleteBinding(HotkeyBindingViewModel? binding)
    {
        if (binding != null)
            Bindings.Remove(binding);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var config = await _configService.LoadAsync();
        config.HotkeyBindings = Bindings
            .Where(b => b.IsEnabled)
            .Select(b => b.ToModel())
            .ToList();

        await _configService.SaveAsync(config);

        // 重新注册快捷键
        _hotkeyService.UnregisterAll();
        // 窗口句柄需要从主窗口获取
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle;
            await _hotkeyService.RegisterAllAsync(config.HotkeyBindings, hwnd);
        }

        ConflictStatus = "✅ 已保存";
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var config = await _configService.LoadAsync();
        Bindings = new ObservableCollection<HotkeyBindingViewModel>(
            config.HotkeyBindings.Select(HotkeyBindingViewModel.FromModel));

        ConflictStatus = "✅ 所有快捷键可用";
    }

    [RelayCommand]
    private void Close()
    {
        // 关闭窗口
        System.Windows.Application.Current.Windows
            .OfType<HotkeySettingsWindow>()
            .FirstOrDefault()?.Close();
    }
}
