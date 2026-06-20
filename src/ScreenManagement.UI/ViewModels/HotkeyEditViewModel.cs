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
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DisplayMode? _targetMode = DisplayMode.Extend;
    [ObservableProperty] private string? _targetDisplayId;
    [ObservableProperty] private int _hdrModeIndex; // 0=切换, 1=开启, 2=关闭
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
    [ObservableProperty] private ObservableCollection<SubActionViewModel> _subActions = new();

    // ─── 计算属性（用于 XAML Visibility 绑定）───────────────────────────
    public bool IsSetDisplayMode => ActionType == HotkeyActionType.SetDisplayMode;
    public bool IsToggleHdr => ActionType == HotkeyActionType.ToggleHdr;
    public bool IsCompositeAction => ActionType == HotkeyActionType.CompositeAction;

    /// <summary>动作类型下拉索引（0=切换显示模式, 1=切换HDR, 2=组合动作）</summary>
    public int ActionTypeIndex
    {
        get => ActionType switch
        {
            HotkeyActionType.ToggleHdr => 1,
            HotkeyActionType.CompositeAction => 2,
            _ => 0
        };
        set => ActionType = value switch
        {
            1 => HotkeyActionType.ToggleHdr,
            2 => HotkeyActionType.CompositeAction,
            _ => HotkeyActionType.SetDisplayMode
        };
    }

    /// <summary>目标显示模式下拉索引（0=仅电脑, 1=复制, 2=扩展, 3=仅第二屏）</summary>
    public int TargetModeIndex
    {
        get => (int)(TargetMode ?? DisplayMode.Extend);
        set => TargetMode = (DisplayMode)value;
    }

    partial void OnActionTypeChanged(HotkeyActionType value)
    {
        OnPropertyChanged(nameof(ActionTypeIndex));
        OnPropertyChanged(nameof(IsSetDisplayMode));
        OnPropertyChanged(nameof(IsToggleHdr));
        OnPropertyChanged(nameof(IsCompositeAction));
    }

    partial void OnTargetModeChanged(DisplayMode? value)
    {
        OnPropertyChanged(nameof(TargetModeIndex));
    }

    private readonly IMonitorEnumerationService _monitorService;
    private readonly string? _editId; // null = 新建

    public HotkeyEditViewModel(IMonitorEnumerationService monitorService, HotkeyBinding? existing = null)
    {
        _monitorService = monitorService;

        if (existing != null)
        {
            Title = "编辑快捷键";
            _editId = existing.Id;
            Name = existing.Name ?? string.Empty;
            ActionType = existing.ActionType;
            TargetMode = existing.TargetMode;
            TargetDisplayId = existing.TargetDisplayId;
            HdrModeIndex = existing.HdrTargetState.HasValue
                ? (existing.HdrTargetState.Value ? 1 : 2)
                : 0;
            KeyCode = existing.Key;
            KeyDisplay = HotkeyBindingViewModel.FormatHotkey(ModifierKeys.None, existing.Key);

            var mods = existing.Modifiers;
            CtrlModifier = mods.HasFlag(ModifierKeys.Control);
            AltModifier = mods.HasFlag(ModifierKeys.Alt);
            ShiftModifier = mods.HasFlag(ModifierKeys.Shift);
            WinModifier = mods.HasFlag(ModifierKeys.Windows);

            if (existing.SubActions != null)
            {
                foreach (var sub in existing.SubActions)
                    SubActions.Add(SubActionViewModel.FromModel(sub));
            }
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
        Name = string.IsNullOrWhiteSpace(Name) ? null : Name.Trim(),
        ActionType = ActionType,
        TargetMode = IsSetDisplayMode ? TargetMode : null,
        TargetDisplayId = IsToggleHdr ? TargetDisplayId : null,
        HdrTargetState = IsToggleHdr
            ? (HdrModeIndex == 1 ? (bool?)true : HdrModeIndex == 2 ? false : null)
            : null,
        Modifiers = GetModifiers(),
        Key = KeyCode,
        IsEnabled = true,
        SubActions = IsCompositeAction
            ? SubActions.Select(s => s.ToModel()).ToList()
            : null
    };

    [RelayCommand]
    private async Task LoadDisplaysAsync()
    {
        var displays = await _monitorService.GetDisplaysAsync();
        AvailableDisplays = new ObservableCollection<DisplayInfo>(displays);
        // 同步到各子动作
        foreach (var sub in SubActions)
            sub.AvailableDisplays = AvailableDisplays;
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
    private void AddSubAction()
    {
        var sub = new SubActionViewModel { AvailableDisplays = AvailableDisplays };
        SubActions.Add(sub);
    }

    [RelayCommand]
    private void RemoveSubAction(SubActionViewModel? sub)
    {
        if (sub != null)
            SubActions.Remove(sub);
    }

    [RelayCommand]
    private void Confirm()
    {
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
