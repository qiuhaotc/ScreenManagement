using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Interfaces;

/// <summary>快捷键触发的参数</summary>
public class HotkeyTriggeredEventArgs : EventArgs
{
    public HotkeyBinding Binding { get; init; } = null!;
}

/// <summary>快捷键管理服务</summary>
public interface IHotkeyService
{
    /// <summary>注册所有启用的快捷键（需要窗口句柄）</summary>
    Task RegisterAllAsync(IEnumerable<HotkeyBinding> bindings, IntPtr hwnd);

    /// <summary>注册单个快捷键</summary>
    /// <returns>null=成功，否则返回错误信息</returns>
    string? RegisterHotkey(HotkeyBinding binding, IntPtr hwnd);

    /// <summary>注销所有快捷键</summary>
    void UnregisterAll();

    /// <summary>检测快捷键是否可用（不实际注册）</summary>
    bool IsHotkeyAvailable(ModifierKeys modifiers, uint key);

    /// <summary>快捷键触发事件</summary>
    event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    /// <summary>初始化窗口消息处理</summary>
    void Initialize(IntPtr hwnd);
}
