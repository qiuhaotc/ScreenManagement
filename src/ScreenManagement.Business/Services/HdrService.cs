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
                if (!TryGetAdapterTargetId(displayId, out long adapterId, out uint targetId))
                    return false;

                var request = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                        size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = adapterId,
                        id = targetId
                    }
                };

                int error = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogWarning("DisplayConfigGetDeviceInfo for HDR status failed: {Error}", error);
                    return false;
                }

                return request.AdvancedColorEnabled;
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

                var request = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
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

                int error = NativeMethods.DisplayConfigSetDeviceInfo(ref request);
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
            if (!TryGetAdapterTargetId(displayId, out long adapterId, out uint targetId))
                return false;

            var request = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                    size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                    adapterId = adapterId,
                    id = targetId
                }
            };

            int error = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
            return error == NativeTypes.ERROR_SUCCESS && request.AdvancedColorSupported;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SupportsHdr failed for {DisplayId}", displayId);
            return false;
        }
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
