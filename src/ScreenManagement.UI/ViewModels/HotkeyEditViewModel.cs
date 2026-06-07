using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 快捷键编辑弹窗 ViewModel
/// </summary>
public partial class HotkeyEditViewModel : ObservableObject
{
    [ObservableProperty] private HotkeyActionType _actionType = HotkeyActionType.SetDisplayMode;
    [ObservableProperty] private DisplayMode? _targetMode = DisplayMode.Extend;
    [ObservableProperty] private string? _targetDisplayId;
    [ObservableProperty] private bool _ctrlModifier = true;
    [ObservableProperty] private bool _altModifier;
    [ObservableProperty] private bool _shiftModifier = true;
    [ObservableProperty] private bool _winModifier;
    [ObservableProperty] private uint _keyCode = 0x71; // F2
    [ObservableProperty] private string _keyDisplay = "F2";
    [ObservableProperty] private string _conflictMessage = string.Empty;
    [ObservableProperty] private ObservableCollection<DisplayInfo> _availableDisplays = new();
    [ObservableProperty] private bool _isWaitingForKeyPress;
    [ObservableProperty] private string _title = "添加快捷键";

    private readonly IMonitorEnumerationService _monitorService;
    private readonly string? _editId; // null = 新建

    public HotkeyEditViewModel(IMonitorEnumerationService monitorService, HotkeyBinding? existing = null)
    {
        _monitorService = monitorService;

        if (existing != null)
        {
            Title = "编辑快捷键";
            _editId = existing.Id;
            ActionType = existing.ActionType;
            TargetMode = existing.TargetMode;
            TargetDisplayId = existing.TargetDisplayId;
            KeyCode = existing.Key;
            KeyDisplay = HotkeyBindingViewModel.FormatHotkey(ModifierKeys.None, existing.Key);

            var mods = existing.Modifiers;
            CtrlModifier = mods.HasFlag(ModifierKeys.Control);
            AltModifier = mods.HasFlag(ModifierKeys.Alt);
            ShiftModifier = mods.HasFlag(ModifierKeys.Shift);
            WinModifier = mods.HasFlag(ModifierKeys.Windows);
        }
    }

    /// <summary>构建修饰键标志</summary>
    public ModifierKeys GetModifiers()
    {
        var mods = ModifierKeys.None;
        if (CtrlModifier) mods |= ModifierKeys.Control;
        if (AltModifier) mods |= ModifierKeys.Alt;
        if (ShiftModifier) mods |= ModifierKeys.Shift;
        if (WinModifier) mods |= ModifierKeys.Windows;
        return mods;
    }

    /// <summary>生成数据模型</summary>
    public HotkeyBinding ToModel() => new()
    {
        Id = _editId ?? Guid.NewGuid().ToString(),
        ActionType = ActionType,
        TargetMode = TargetMode,
        TargetDisplayId = TargetDisplayId,
        Modifiers = GetModifiers(),
        Key = KeyCode,
        IsEnabled = true
    };

    [RelayCommand]
    private async Task LoadDisplaysAsync()
    {
        var displays = await _monitorService.GetDisplaysAsync();
        AvailableDisplays = new ObservableCollection<DisplayInfo>(displays);
    }

    [RelayCommand]
    private void StartKeyCapture()
    {
        IsWaitingForKeyPress = true;
        KeyDisplay = "按下按键...";
    }

    /// <summary>接收按键输入</summary>
    public void OnKeyPressed(uint keyCode, string keyName)
    {
        KeyCode = keyCode;
        KeyDisplay = keyName;
        IsWaitingForKeyPress = false;
    }

    [RelayCommand]
    private void Confirm()
    {
        // 关闭弹窗并返回 true (DialogResult)
        if (System.Windows.Application.Current.Windows
            .OfType<Views.HotkeyEditDialog>()
            .FirstOrDefault() is Views.HotkeyEditDialog dialog)
        {
            dialog.DialogResult = true;
            dialog.Close();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (System.Windows.Application.Current.Windows
            .OfType<Views.HotkeyEditDialog>()
            .FirstOrDefault() is Views.HotkeyEditDialog dialog)
        {
            dialog.DialogResult = false;
            dialog.Close();
        }
    }
}
