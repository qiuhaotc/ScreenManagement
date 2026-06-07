using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Native;

namespace ScreenManagement.Business.Services;

/// <summary>HDR 管理服务 — 使用 Windows CCD 高级颜色 API</summary>
public class HdrService : IHdrService
{
    private readonly ILogger<HdrService> _logger;

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
                if (!TryParseDisplayId(displayId, out long adapterId, out uint sourceId))
                    return false;

                var request = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                        size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                        adapterId = adapterId,
                        id = sourceId
                    }
                };

                int error = NativeMethods.DisplayConfigGetDeviceInfo(ref request.header);
                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogWarning("DisplayConfigGetDeviceInfo for HDR status failed: {Error}", error);
                    return false;
                }

                return request.advancedColorEnabled != 0;
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
                if (!TryParseDisplayId(displayId, out long adapterId, out uint sourceId))
                    return false;

                var request = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE,
                        size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>(),
                        adapterId = adapterId,
                        id = sourceId
                    },
                    state = enable
                        ? NativeTypes.DISPLAYCONFIG_ADVANCED_COLOR_ENABLED
                        : NativeTypes.DISPLAYCONFIG_ADVANCED_COLOR_DISABLED
                };

                int error = NativeMethods.DisplayConfigSetDeviceInfo(ref request.header);
                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogError("DisplayConfigSetDeviceInfo for HDR failed: {Error}", error);
                    return false;
                }

                _logger.LogInformation("HDR set to {Enabled} for display {DisplayId}", enable, displayId);
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
            if (!TryParseDisplayId(displayId, out long adapterId, out uint sourceId))
                return false;

            var request = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO,
                    size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO>(),
                    adapterId = adapterId,
                    id = sourceId
                }
            };

            int error = NativeMethods.DisplayConfigGetDeviceInfo(ref request.header);
            return error == NativeTypes.ERROR_SUCCESS && request.advancedColorSupported != 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SupportsHdr failed for {DisplayId}", displayId);
            return false;
        }
    }

    /// <summary>从 displayId 字符串解析出 adapterId 和 sourceId</summary>
    private static bool TryParseDisplayId(string displayId, out long adapterId, out uint sourceId)
    {
        adapterId = 0;
        sourceId = 0;

        if (string.IsNullOrEmpty(displayId))
            return false;

        // displayId 格式: "LUID:SourceId" 例如 "12345:0"
        var parts = displayId.Split(':');
        if (parts.Length != 2)
            return false;

        return long.TryParse(parts[0], out adapterId)
            && uint.TryParse(parts[1], out sourceId);
    }
}
