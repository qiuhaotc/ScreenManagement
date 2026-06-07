namespace ScreenManagement.Business.Interfaces;

/// <summary>开机自启服务</summary>
public interface IAutostartService
{
    /// <summary>是否已启用开机自启</summary>
    bool IsAutostartEnabled();

    /// <summary>设置开机自启</summary>
    void SetAutostart(bool enable);
}
