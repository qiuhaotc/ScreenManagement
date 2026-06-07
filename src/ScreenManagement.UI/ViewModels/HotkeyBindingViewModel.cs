using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 快捷键绑定 ViewModel（用于列表展示）
/// </summary>
public partial class HotkeyBindingViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private HotkeyActionType _actionType;
    [ObservableProperty] private string _actionDescription = string.Empty;
    [ObservableProperty] private string _hotkeyDisplay = string.Empty;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private bool _hasConflict;

    /// <summary>还原为数据模型</summary>
    public HotkeyBinding ToModel() => new()
    {
        Id = Id,
        ActionType = ActionType,
        IsEnabled = IsEnabled
    };

    /// <summary>从数据模型创建 ViewModel</summary>
    public static HotkeyBindingViewModel FromModel(HotkeyBinding model) => new()
    {
        Id = model.Id,
        ActionType = model.ActionType,
        ActionDescription = GetActionDescription(model),
        HotkeyDisplay = FormatHotkey(model.Modifiers, model.Key),
        IsEnabled = model.IsEnabled
    };

    /// <summary>格式化快捷键显示文本</summary>
    public static string FormatHotkey(ModifierKeys modifiers, uint key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(VirtualKeyToString(key));
        return string.Join(" + ", parts);
    }

    /// <summary>获取动作描述</summary>
    public static string GetActionDescription(HotkeyBinding model) => model.ActionType switch
    {
        HotkeyActionType.SetDisplayMode => $"切换到\"{MainViewModel.GetModeDisplayName(model.TargetMode ?? DisplayMode.Extend)}\"",
        HotkeyActionType.ToggleHdr => model.HdrTargetState.HasValue
            ? $"设置 HDR {(model.HdrTargetState.Value ? "开启" : "关闭")}"
            : $"切换 HDR ({model.TargetDisplayId ?? "全部"})",
        HotkeyActionType.CompositeAction => $"组合动作 ({model.SubActions?.Count ?? 0} 项)",
        _ => "未知动作"
    };

    private static string VirtualKeyToString(uint key) => key switch
    {
        0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
        0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
        0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        0x41 => "A", 0x42 => "B", 0x43 => "C", 0x44 => "D", 0x45 => "E",
        0x46 => "F", 0x47 => "G", 0x48 => "H", 0x49 => "I", 0x4A => "J",
        0x4B => "K", 0x4C => "L", 0x4D => "M", 0x4E => "N", 0x4F => "O",
        0x50 => "P", 0x51 => "Q", 0x52 => "R", 0x53 => "S", 0x54 => "T",
        0x55 => "U", 0x56 => "V", 0x57 => "W", 0x58 => "X", 0x59 => "Y",
        0x5A => "Z",
        _ => ((System.Windows.Forms.Keys)key).ToString()
    };
}
