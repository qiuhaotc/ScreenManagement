namespace ScreenManagement.Business.Models;

/// <summary>显示器信息</summary>
public class DisplayInfo
{
    /// <summary>设备唯一标识</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>显示名称（如 "DELL U2723QE"）</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>是否为主显示器</summary>
    public bool IsPrimary { get; set; }

    /// <summary>是否为内置显示器（笔记本屏幕）</summary>
    public bool IsInternal { get; set; }

    /// <summary>当前显示模式下是否处于激活状态（未激活表示物理已连接但当前被禁用）</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>是否支持 HDR</summary>
    public bool SupportsHdr { get; set; }

    /// <summary>HDR 当前是否启用</summary>
    public bool HdrEnabled { get; set; }

    /// <summary>显示器区域</summary>
    public Rect Bounds { get; set; }

    /// <summary>水平分辨率</summary>
    public int ResolutionX { get; set; }

    /// <summary>垂直分辨率</summary>
    public int ResolutionY { get; set; }

    /// <summary>刷新率（Hz × 1000，如 60000 = 60Hz）</summary>
    public uint RefreshRate { get; set; }

    /// <summary>适配器 ID（Windows 内部使用）</summary>
    public uint AdapterId { get; set; }

    /// <summary>源 ID（Windows 内部使用）</summary>
    public uint SourceId { get; set; }
}

/// <summary>矩形区域</summary>
public struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}
