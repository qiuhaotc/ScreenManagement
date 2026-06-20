using System.Text.Json.Serialization;

namespace ScreenManagement.Business.Models;

/// <summary>应用配置</summary>
public class AppConfig
{
    /// <summary>快捷键绑定列表</summary>
    public List<HotkeyBinding> HotkeyBindings { get; set; } = new();

    /// <summary>是否开机自启</summary>
    public bool AutoStart { get; set; }

    /// <summary>启动时最小化到托盘</summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>是否显示切换通知</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>界面语言</summary>
    public string Language { get; set; } = "zh-CN";

    /// <summary>快捷键触发时，若当前已是目标状态则恢复之前状态（默认开启）</summary>
    public bool EnableStateRestore { get; set; } = true;

    /// <summary>创建默认配置</summary>
    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            HotkeyBindings = new List<HotkeyBinding>
            {
                //new()
                //{
                //    ActionType = HotkeyActionType.SetDisplayMode,
                //    TargetMode = DisplayMode.Internal,
                //    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                //    Key = 0x70, // F1
                //    IsEnabled = true
                //},
                //new()
                //{
                //    ActionType = HotkeyActionType.SetDisplayMode,
                //    TargetMode = DisplayMode.Extend,
                //    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                //    Key = 0x71, // F2
                //    IsEnabled = true
                //},
                //new()
                //{
                //    ActionType = HotkeyActionType.SetDisplayMode,
                //    TargetMode = DisplayMode.External,
                //    Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
                //    Key = 0x72, // F3
                //    IsEnabled = true
                //}
            },
            AutoStart = false,
            StartMinimized = false,
            ShowNotifications = true,
            Language = "zh-CN"
        };
    }
}
