using System.Runtime.InteropServices;

namespace ScreenManagement.Business.Native;

/// <summary>Windows API P/Invoke 声明</summary>
public static class NativeMethods
{
    private const string User32 = "user32.dll";
    private const string Gdi32 = "gdi32.dll";
    private const string Dxva2 = "dxva2.dll";

    // ══════════════════════════════════════════════
    // 显示配置 API (CCD)
    // ══════════════════════════════════════════════

    /// <summary>获取 QueryDisplayConfig 所需的缓冲区大小</summary>
    [DllImport(User32, SetLastError = true)]
    public static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    /// <summary>查询当前显示配置</summary>
    [DllImport(User32, SetLastError = true)]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [In, Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [In, Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        out uint currentTopologyId);

    /// <summary>设置显示配置</summary>
    [DllImport(User32, SetLastError = true)]
    public static extern int SetDisplayConfig(
        uint numPathArrayElements,
        [In] DISPLAYCONFIG_PATH_INFO[]? pathArray,
        uint numModeInfoArrayElements,
        [In] DISPLAYCONFIG_MODE_INFO[]? modeInfoArray,
        uint flags);

    /// <summary>获取显示设备信息</summary>
    [DllImport(User32, SetLastError = true)]
    public static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_DEVICE_INFO_HEADER requestPacket);

    /// <summary>设置显示设备信息</summary>
    [DllImport(User32, SetLastError = true)]
    public static extern int DisplayConfigSetDeviceInfo(
        ref DISPLAYCONFIG_DEVICE_INFO_HEADER requestPacket);

    // ══════════════════════════════════════════════
    // 全局快捷键 API
    // ══════════════════════════════════════════════

    /// <summary>注册全局热键</summary>
    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(
        IntPtr hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    /// <summary>注销全局热键</summary>
    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(
        IntPtr hWnd,
        int id);

    // ══════════════════════════════════════════════
    // 显示器枚举 API
    // ══════════════════════════════════════════════

    /// <summary>枚举显示设备</summary>
    [DllImport(User32, CharSet = CharSet.Ansi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    /// <summary>获取显示器信息</summary>
    [DllImport(User32, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MONITORINFOEX lpmi);

    /// <summary>枚举显示器回调委托</summary>
    public delegate bool MonitorEnumDelegate(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        ref RECT lprcMonitor,
        IntPtr dwData);

    /// <summary>枚举所有显示器</summary>
    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumDelegate lpfnEnum,
        IntPtr dwData);

    /// <summary>获取设备上下文</summary>
    [DllImport(User32)]
    public static extern IntPtr CreateDC(
        string lpszDriver,
        string? lpszDevice,
        string? lpszOutput,
        IntPtr lpInitData);

    /// <summary>删除设备上下文</summary>
    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteDC(IntPtr hdc);

    // ══════════════════════════════════════════════
    // 窗口消息
    // ══════════════════════════════════════════════

    /// <summary>发送消息到窗口</summary>
    [DllImport(User32)]
    public static extern IntPtr SendMessage(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        IntPtr lParam);

    /// <summary>查找窗口</summary>
    [DllImport(User32, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(
        string? lpClassName,
        string? lpWindowName);

    /// <summary>设置窗口为前台窗口</summary>
    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    // ══════════════════════════════════════════════
    // 驱动通知
    // ══════════════════════════════════════════════

    /// <summary>注册设备通知</summary>
    [DllImport(User32, CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr RegisterDeviceNotification(
        IntPtr hRecipient,
        IntPtr NotificationFilter,
        uint Flags);

    /// <summary>注销设备通知</summary>
    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterDeviceNotification(IntPtr Handle);
}

// ──────────────────────────────────────────────
// 辅助结构体
// ──────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MONITORINFOEX
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice;

    public const uint MONITORINFOF_PRIMARY = 0x00000001;

    public static MONITORINFOEX Create()
    {
        return new MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>() };
    }
}
