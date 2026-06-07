using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Interfaces;

/// <summary>显示模式管理服务</summary>
public interface IDisplayService
{
    /// <summary>获取当前显示模式</summary>
    DisplayMode GetCurrentMode();

    /// <summary>切换到指定显示模式</summary>
    /// <returns>是否成功</returns>
    Task<bool> SetDisplayModeAsync(DisplayMode mode);

    /// <summary>显示模式变更事件</summary>
    event EventHandler<DisplayMode>? DisplayModeChanged;
}
