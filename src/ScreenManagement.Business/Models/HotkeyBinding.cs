using System.Text.Json.Serialization;

namespace ScreenManagement.Business.Models;

/// <summary>快捷键绑定</summary>
public class HotkeyBinding
{
    /// <summary>唯一标识 (GUID)</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>动作类型</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HotkeyActionType ActionType { get; set; }

    /// <summary>目标显示模式（SetDisplayMode 时使用）</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DisplayMode? TargetMode { get; set; }

    /// <summary>目标显示器 ID（ToggleHdr 时使用）</summary>
    public string? TargetDisplayId { get; set; }

    /// <summary>HDR 目标状态（null=切换当前状态）</summary>
    public bool? HdrTargetState { get; set; }

    /// <summary>修饰键（ModifierKeys 位标志）</summary>
    public ModifierKeys Modifiers { get; set; }

    /// <summary>虚拟键码</summary>
    public uint Key { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>子动作列表（组合动作时使用）</summary>
    public List<HotkeyBinding>? SubActions { get; set; }

    /// <summary>是否为组合动作</summary>
    [JsonIgnore]
    public bool IsComposite => ActionType == HotkeyActionType.CompositeAction;
}
