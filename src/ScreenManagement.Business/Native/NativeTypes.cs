using System.Runtime.InteropServices;

namespace ScreenManagement.Business.Native;

/// <summary>Windows API 类型定义</summary>
public static class NativeTypes
{
    // ──────────────────────────────────────────────
    // 显示拓扑 ID
    // ──────────────────────────────────────────────
    public const uint DISPLAYCONFIG_TOPOLOGY_INTERNAL = 0x00000001;
    public const uint DISPLAYCONFIG_TOPOLOGY_CLONE = 0x00000002;
    public const uint DISPLAYCONFIG_TOPOLOGY_EXTEND = 0x00000004;
    public const uint DISPLAYCONFIG_TOPOLOGY_EXTERNAL = 0x00000008;

    // ──────────────────────────────────────────────
    // QueryDisplayConfig Flags
    // ──────────────────────────────────────────────
    public const uint QDC_ALL_PATHS = 0x00000001;
    public const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    public const uint QDC_DATABASE_CURRENT = 0x00000004;

    // ──────────────────────────────────────────────
    // SetDisplayConfig Flags
    // ──────────────────────────────────────────────
    public const uint SDC_APPLY = 0x00000080;
    public const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    public const uint SDC_USE_DATABASE_CURRENT = 0x00002000;
    public const uint SDC_TOPOLOGY_INTERNAL = 0x00000001;
    public const uint SDC_TOPOLOGY_CLONE = 0x00000002;
    public const uint SDC_TOPOLOGY_EXTEND = 0x00000004;
    public const uint SDC_TOPOLOGY_EXTERNAL = 0x00000008;

    // ──────────────────────────────────────────────
    // 设备信息类型
    // ──────────────────────────────────────────────
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_PREFERRED_MODE = 3;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADAPTER_NAME = 4;
    public const uint DISPLAYCONFIG_DEVICE_INFO_SET_TARGET_PERSISTENCE = 5;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_BASE_TYPE = 6;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_SUPPORT_VIRTUAL_RESOLUTION = 7;
    public const uint DISPLAYCONFIG_DEVICE_INFO_SET_SUPPORT_VIRTUAL_RESOLUTION = 8;
    public const uint DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO = 9;
    public const uint DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE = 10;

    // ──────────────────────────────────────────────
    // 高级颜色状态
    // ──────────────────────────────────────────────
    public const uint DISPLAYCONFIG_ADVANCED_COLOR_ENABLED = 1;
    public const uint DISPLAYCONFIG_ADVANCED_COLOR_DISABLED = 0;

    // ──────────────────────────────────────────────
    // 窗口消息常量
    // ──────────────────────────────────────────────
    public const int WM_HOTKEY = 0x0312;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_DEVICECHANGE = 0x0219;
    public const int DBT_DEVICEARRIVAL = 0x8000;
    public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    // ──────────────────────────────────────────────
    // 错误码
    // ──────────────────────────────────────────────
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;
    public const int ERROR_HOTKEY_NOT_REGISTERED = 1419;

    // ──────────────────────────────────────────────
    // 输出技术类型（用于判断内置/外接显示器）
    // ──────────────────────────────────────────────
    public const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000;
    public const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 0x80000004;
    public const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 0x8000000A;

    // 无效的模式索引
    public const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;
}

// ──────────────────────────────────────────────
// 结构体定义
// ──────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint flags;
    public uint outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string monitorFriendlyDeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string monitorDevicePath;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DISPLAYCONFIG_PATH_SOURCE_INFO
{
    public long adapterId; // LUID, Pack=4
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DISPLAYCONFIG_PATH_TARGET_INFO
{
    public long adapterId; // LUID, Pack=4
    public uint id;
    public uint modeInfoIdx;
    public uint outputTechnology;
    public uint rotation;
    public uint scaling;
    public DISPLAYCONFIG_RATIONAL refreshRate;
    public uint scanLineOrdering;
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public bool targetAvailable;
    public uint statusFlags;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DISPLAYCONFIG_MODE_INFO
{
    public uint infoType;
    public uint id;
    public long adapterId; // LUID, Pack=4
    public DISPLAYCONFIG_MODE_INFO_UNION info;
}

[StructLayout(LayoutKind.Explicit)]
public struct DISPLAYCONFIG_MODE_INFO_UNION
{
    [FieldOffset(0)] public DISPLAYCONFIG_TARGET_MODE targetMode;
    [FieldOffset(0)] public DISPLAYCONFIG_SOURCE_MODE sourceMode;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_TARGET_MODE
{
    public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_SOURCE_MODE
{
    public uint width;
    public uint height;
    public uint pixelFormat;
    public POINTL position;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
{
    public ulong pixelRate;
    public DISPLAYCONFIG_RATIONAL hSyncFreq;
    public DISPLAYCONFIG_RATIONAL vSyncFreq;
    public DISPLAYCONFIG_2DREGION activeSize;
    public DISPLAYCONFIG_2DREGION totalSize;
    public uint videoStandard;
    public ushort scanLineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_2DREGION
{
    public uint cx;
    public uint cy;
}

[StructLayout(LayoutKind.Sequential)]
public struct POINTL
{
    public int x;
    public int y;
}

// adapterId 是 Windows LUID，底层为 2×4 字节字段（对齐=4）。
// C# long 对齐=8，必须用 Pack=4 追使尺寸与 Windows 结构体匹配。
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
{
    public uint type;
    public uint size;
    public long adapterId; // LUID：LowPart(u32) + HighPart(i32)→共 8 字节，Pack=4 后对齐=4
    public uint id;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    // 位域打包在 value 中:
    //   bit0 advancedColorSupported
    //   bit1 advancedColorEnabled
    //   bit2 wideColorEnforced
    //   bit3 advancedColorForceDisabled
    public uint value;
    public uint colorEncoding;
    public uint bitsPerColorChannel;

    public readonly bool AdvancedColorSupported => (value & 0x1) != 0;
    public readonly bool AdvancedColorEnabled => (value & 0x2) != 0;
    public readonly bool WideColorEnforced => (value & 0x4) != 0;
    public readonly bool AdvancedColorForceDisabled => (value & 0x8) != 0;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint state;
}

[StructLayout(LayoutKind.Sequential)]
public struct DISPLAY_DEVICE
{
    public uint cb;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;
    public uint StateFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceID;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;

    public const uint DISPLAY_DEVICE_ACTIVE = 0x00000001;
    public const uint DISPLAY_DEVICE_PRIMARY_DEVICE = 0x00000004;
    public const uint DISPLAY_DEVICE_MIRRORING_DRIVER = 0x00000008;
    public const uint DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
}
