using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Interfaces;

/// <summary>组合动作执行服务</summary>
public interface ICompositeActionService
{
    /// <summary>执行一个快捷键动作（支持组合动作递归）</summary>
    Task ExecuteAsync(HotkeyBinding binding);
}
