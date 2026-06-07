namespace ScreenManagement.Business.Interfaces;

/// <summary>HDR 管理服务</summary>
public interface IHdrService
{
    /// <summary>获取指定显示器的 HDR 状态</summary>
    Task<bool> IsHdrEnabledAsync(string displayId);

    /// <summary>设置 HDR 状态</summary>
    Task<bool> SetHdrAsync(string displayId, bool enable);

    /// <summary>切换 HDR 状态</summary>
    Task<bool> ToggleHdrAsync(string displayId);

    /// <summary>检测显示器是否支持 HDR</summary>
    bool SupportsHdr(string displayId);
}
