using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Native;

namespace ScreenManagement.Business.Services;

/// <summary>显示模式管理服务 — 使用 Windows CCD API</summary>
public class DisplayService : IDisplayService
{
    private readonly ILogger<DisplayService> _logger;

    public DisplayService(ILogger<DisplayService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<DisplayMode>? DisplayModeChanged;

    /// <inheritdoc />
    public DisplayMode GetCurrentMode()
    {
        try
        {
            uint pathCount = 0, modeCount = 0;
            int error = NativeMethods.GetDisplayConfigBufferSizes(
                NativeTypes.QDC_DATABASE_CURRENT,
                out pathCount, out modeCount);

            if (error != NativeTypes.ERROR_SUCCESS)
            {
                _logger.LogWarning("GetDisplayConfigBufferSizes failed: {Error}", error);
                return DisplayMode.Extend; // 默认假设扩展模式
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            error = NativeMethods.QueryDisplayConfig(
                NativeTypes.QDC_DATABASE_CURRENT,
                ref pathCount, paths,
                ref modeCount, modes,
                out uint topologyId);

            if (error != NativeTypes.ERROR_SUCCESS)
            {
                _logger.LogWarning("QueryDisplayConfig failed: {Error}", error);
                return DisplayMode.Extend;
            }

            return topologyId switch
            {
                NativeTypes.DISPLAYCONFIG_TOPOLOGY_INTERNAL => DisplayMode.Internal,
                NativeTypes.DISPLAYCONFIG_TOPOLOGY_CLONE => DisplayMode.Clone,
                NativeTypes.DISPLAYCONFIG_TOPOLOGY_EXTEND => DisplayMode.Extend,
                NativeTypes.DISPLAYCONFIG_TOPOLOGY_EXTERNAL => DisplayMode.External,
                _ => DisplayMode.Extend
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCurrentMode failed");
            return DisplayMode.Extend;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetDisplayModeAsync(DisplayMode mode)
    {
        return await Task.Run(() =>
        {
            try
            {
                uint pathCount = 0, modeCount = 0;
                int error = NativeMethods.GetDisplayConfigBufferSizes(
                    NativeTypes.QDC_DATABASE_CURRENT,
                    out pathCount, out modeCount);

                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogError("GetDisplayConfigBufferSizes failed: {Error}", error);
                    return false;
                }

                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                error = NativeMethods.QueryDisplayConfig(
                    NativeTypes.QDC_DATABASE_CURRENT,
                    ref pathCount, paths,
                    ref modeCount, modes,
                    out uint _);

                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogError("QueryDisplayConfig failed: {Error}", error);
                    return false;
                }

                uint topologyFlag = mode switch
                {
                    DisplayMode.Internal => NativeTypes.SDC_TOPOLOGY_INTERNAL,
                    DisplayMode.Clone => NativeTypes.SDC_TOPOLOGY_CLONE,
                    DisplayMode.Extend => NativeTypes.SDC_TOPOLOGY_EXTEND,
                    DisplayMode.External => NativeTypes.SDC_TOPOLOGY_EXTERNAL,
                    _ => NativeTypes.SDC_TOPOLOGY_EXTEND
                };

                uint flags = NativeTypes.SDC_APPLY
                           | NativeTypes.SDC_USE_SUPPLIED_DISPLAY_CONFIG
                           | topologyFlag;

                error = NativeMethods.SetDisplayConfig(
                    pathCount, paths,
                    modeCount, modes,
                    flags);

                if (error != NativeTypes.ERROR_SUCCESS)
                {
                    _logger.LogError("SetDisplayConfig failed: {Error}, Mode={Mode}", error, mode);
                    return false;
                }

                _logger.LogInformation("Display mode set to {Mode}", mode);
                DisplayModeChanged?.Invoke(this, mode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetDisplayModeAsync failed for mode {Mode}", mode);
                return false;
            }
        });
    }
}
