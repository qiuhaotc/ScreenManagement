using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Native;

namespace ScreenManagement.Business.Services;

/// <summary>HDR 管理服务 — 使用 Windows CCD 高级颜色 API</summary>
public class HdrService : IHdrService
{
    private readonly ILogger<HdrService> _logger;

    /// <inheritdoc />
    public event EventHandler? HdrStateChanged;

    public HdrService(
        ILogger<HdrService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsHdrEnabledAsync(string displayId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var (success, _, enabled) = QueryHdrState(displayId);
                return success && enabled;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IsHdrEnabledAsync failed for {DisplayId}", displayId);
                return false;
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> SetHdrAsync(string displayId, bool enable)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!TryGetAdapterTargetId(displayId, out long adapterId, out uint targetId))
                    return false;

                int error = SetHdrState(adapterId, targetId, enable);
                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogError("DisplayConfigSetDeviceInfo for HDR failed: {Error}", error);
                    return false;
                }

                _logger.LogInformation("HDR set to {Enabled} for display {DisplayId}", enable, displayId);
                HdrStateChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetHdrAsync failed for {DisplayId}", displayId);
                return false;
            }
        });
    }

    /// <inheritdoc />
    public async Task<bool> ToggleHdrAsync(string displayId)
    {
        bool current = await IsHdrEnabledAsync(displayId);
        return await SetHdrAsync(displayId, !current);
    }

    /// <inheritdoc />
    public bool SupportsHdr(string displayId)
    {
        try
        {
            var (success, supported, _) = QueryHdrState(displayId);
            return success && supported;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SupportsHdr failed for {DisplayId}", displayId);
            return false;
        }
    }

    /// <summary>
    /// 查询显示器的 HDR 能力与状态。
    /// 优先使用 v2（Windows 10 2004+）：v2 提供独立的 HDR 标志
    /// （highDynamicRangeSupported / highDynamicRangeUserEnabled），
    /// 可区分 HDR 与仅宽色域 (WCG) 的“高级颜色”；旧系统回退到 v1。
    /// </summary>
    private (bool Success, bool SupportsHdr, bool HdrEnabled) QueryHdrState(string displayId)
    {
        if (!TryGetAdapterTargetId(displayId, out long adapterId, out uint targetId))
            return (false, false, false);

        // v2：HDR 专用标志
        var request2 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO_2,
                size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>(),
                adapterId = adapterId,
                id = targetId
            }
        };

        if (NativeMethods.DisplayConfigGetDeviceInfo(ref request2) == NativeTypes.ERROR_SUCCESS)
        {
            return (true, request2.HighDynamicRangeSupported, request2.HighDynamicRangeUserEnabled);
        }

        // v1 回退（旧系统不支持 v2）：advancedColor 标志包含 WCG，无法严格区分 HDR
        _logger.LogDebug("DisplayConfigGetDeviceInfo v2 not supported for {DisplayId}, falling back to v1", displayId);

        var request1 = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                adapterId = adapterId,
                id = targetId
            }
        };

        if (NativeMethods.DisplayConfigGetDeviceInfo(ref request1) == NativeTypes.ERROR_SUCCESS)
        {
            return (true, request1.AdvancedColorSupported, request1.AdvancedColorEnabled);
        }

        return (false, false, false);
    }

    /// <summary>
    /// 设置 HDR 状态。优先使用 HDR 专用 API（DISPLAYCONFIG_SET_HDR_STATE，Windows 10 2004+），
    /// 仅控制 HDR 而不影响 WCG；旧系统回退到 SET_ADVANCED_COLOR_STATE。
    /// </summary>
    private int SetHdrState(long adapterId, uint targetId, bool enable)
    {
        var request2 = new DISPLAYCONFIG_SET_HDR_STATE
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_SET_HDR_STATE,
                size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_SET_HDR_STATE>(),
                adapterId = adapterId,
                id = targetId
            },
            value = enable ? 1u : 0u
        };

        int error = NativeMethods.DisplayConfigSetDeviceInfo(ref request2);
        if (error == NativeTypes.ERROR_SUCCESS)
            return error;

        _logger.LogDebug("DisplayConfigSetDeviceInfo (SET_HDR_STATE) failed with {Error}, falling back to SET_ADVANCED_COLOR_STATE", error);

        var request1 = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
        {
            header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
            {
                type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                adapterId = adapterId,
                id = targetId
            },
            state = enable
                ? NativeTypes.DISPLAYCONFIG_ADVANCED_COLOR_ENABLED
                : NativeTypes.DISPLAYCONFIG_ADVANCED_COLOR_DISABLED
        };

        return NativeMethods.DisplayConfigSetDeviceInfo(ref request1);
    }

    /// <summary>
    /// 将 displayId 解析为当前有效的 adapterId 和 targetId。
    /// 支持两种格式：
    ///   1. monitorDevicePath（稳定，以 \\?\ 开头）：通过枚举活动路径反查当前 LUID
    ///   2. 旧格式 "LUID:TargetId"：直接解析（重启后可能失效）
    /// </summary>
    private bool TryGetAdapterTargetId(string displayId, out long adapterId, out uint targetId)
    {
        if (displayId.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            return TryResolveByDevicePath(displayId, out adapterId, out targetId);

        return TryParseDisplayId(displayId, out adapterId, out targetId);
    }

    /// <summary>通过枚举所有显示路径，根据 monitorDevicePath 匹配找到当前的 adapterId 和 targetId</summary>
    private bool TryResolveByDevicePath(string monitorDevicePath, out long adapterId, out uint targetId)
    {
        adapterId = 0;
        targetId = 0;

        int error = NativeMethods.GetDisplayConfigBufferSizes(NativeTypes.QDC_ALL_PATHS, out uint pathCount, out uint modeCount);
        if (error != NativeTypes.ERROR_SUCCESS)
            return false;

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        error = NativeMethods.QueryDisplayConfig(NativeTypes.QDC_ALL_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (error != NativeTypes.ERROR_SUCCESS)
            return false;

        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];
            var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            if (NativeMethods.DisplayConfigGetDeviceInfo(ref request) == NativeTypes.ERROR_SUCCESS
                && string.Equals(request.monitorDevicePath, monitorDevicePath, StringComparison.OrdinalIgnoreCase))
            {
                adapterId = path.targetInfo.adapterId;
                targetId = path.targetInfo.id;
                return true;
            }
        }

        _logger.LogWarning("Could not resolve display path: {MonitorDevicePath}", monitorDevicePath);
        return false;
    }

    /// <summary>从 displayId 字符串解析出 adapterId 和 targetId</summary>
    private static bool TryParseDisplayId(string displayId, out long adapterId, out uint targetId)
    {
        adapterId = 0;
        targetId = 0;

        if (string.IsNullOrEmpty(displayId))
            return false;

        // displayId 格式: "LUID:TargetId" 例如 "12345:0"
        var parts = displayId.Split(':');
        if (parts.Length != 2)
            return false;

        return long.TryParse(parts[0], out adapterId)
            && uint.TryParse(parts[1], out targetId);
    }
}
