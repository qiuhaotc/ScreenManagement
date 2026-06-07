using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Interfaces;

/// <summary>显示器枚举服务</summary>
public interface IMonitorEnumerationService
{
    /// <summary>获取所有连接的显示器</summary>
    Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync();

    /// <summary>刷新显示器列表（热插拔时调用）</summary>
    Task RefreshAsync();

    /// <summary>显示器变更事件</summary>
    event EventHandler<IReadOnlyList<DisplayInfo>>? DisplaysChanged;
}
