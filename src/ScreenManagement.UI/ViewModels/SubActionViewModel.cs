using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ScreenManagement.Business.Models;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 子动作 ViewModel（用于组合动作中的单个动作编辑）
/// </summary>
public partial class SubActionViewModel : ObservableObject
{
    [ObservableProperty] private HotkeyActionType _actionType = HotkeyActionType.SetDisplayMode;
    [ObservableProperty] private int _targetModeIndex = 2; // 默认：扩展
    [ObservableProperty] private string? _targetDisplayId;
    [ObservableProperty] private int _hdrModeIndex; // 0=切换, 1=开启, 2=关闭
    [ObservableProperty] private ObservableCollection<DisplayInfo> _availableDisplays = new();

    /// <summary>动作类型下拉索引（0=切换显示模式, 1=切换HDR）</summary>
    public int ActionTypeIndex
    {
        get => ActionType == HotkeyActionType.ToggleHdr ? 1 : 0;
        set => ActionType = value == 1 ? HotkeyActionType.ToggleHdr : HotkeyActionType.SetDisplayMode;
    }

    public bool IsSetDisplayMode => ActionType == HotkeyActionType.SetDisplayMode;
    public bool IsToggleHdr => ActionType == HotkeyActionType.ToggleHdr;

    partial void OnActionTypeChanged(HotkeyActionType value)
    {
        OnPropertyChanged(nameof(ActionTypeIndex));
        OnPropertyChanged(nameof(IsSetDisplayMode));
        OnPropertyChanged(nameof(IsToggleHdr));
    }

    /// <summary>从数据模型创建</summary>
    public static SubActionViewModel FromModel(HotkeyBinding model) => new()
    {
        ActionType = model.ActionType,
        TargetModeIndex = (int)(model.TargetMode ?? DisplayMode.Extend),
        TargetDisplayId = model.TargetDisplayId,
        HdrModeIndex = model.HdrTargetState.HasValue ? (model.HdrTargetState.Value ? 1 : 2) : 0
    };

    /// <summary>生成数据模型</summary>
    public HotkeyBinding ToModel() => new()
    {
        ActionType = ActionType,
        TargetMode = IsSetDisplayMode ? (DisplayMode)TargetModeIndex : null,
        TargetDisplayId = IsToggleHdr ? TargetDisplayId : null,
        HdrTargetState = IsToggleHdr
            ? (HdrModeIndex == 1 ? (bool?)true : HdrModeIndex == 2 ? false : null)
            : null,
        IsEnabled = true
    };
}
