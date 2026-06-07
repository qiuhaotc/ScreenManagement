using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Native;
using System.Runtime.InteropServices;

namespace ScreenManagement.Business.Services;

/// <summary>显示器枚举服务 — 使用 EnumDisplayDevices / EnumDisplayMonitors</summary>
public class MonitorEnumerationService : IMonitorEnumerationService
{
    private readonly ILogger<MonitorEnumerationService> _logger;
    private readonly IHdrService _hdrService;
    private List<DisplayInfo> _cachedDisplays = new();

    public MonitorEnumerationService(
        ILogger<MonitorEnumerationService> logger,
        IHdrService hdrService)
    {
        _logger = logger;
        _hdrService = hdrService;
    }

    /// <inheritdoc />
    public event EventHandler<IReadOnlyList<DisplayInfo>>? DisplaysChanged;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync()
    {
        if (_cachedDisplays.Count > 0)
            return _cachedDisplays.AsReadOnly();

        await RefreshAsync();
        return _cachedDisplays.AsReadOnly();
    }

    /// <inheritdoc />
    public Task RefreshAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var displays = new List<DisplayInfo>();

                // 枚举所有显示设备
                uint i = 0;
                while (true)
                {
                    var dd = new DISPLAY_DEVICE
                    {
                        cb = (uint)Marshal.SizeOf<DISPLAY_DEVICE>()
                    };

                    if (!NativeMethods.EnumDisplayDevices(null, i, ref dd, 0))
                        break;

                    i++;

                    // 跳过镜像驱动和非活动设备
                    if ((dd.StateFlags & DISPLAY_DEVICE.DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                        continue;

                    bool isPrimary = (dd.StateFlags & DISPLAY_DEVICE.DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

                    var display = new DisplayInfo
                    {
                        DisplayName = dd.DeviceString,
                        IsPrimary = isPrimary,
                        IsInternal = IsInternalDisplay(dd.DeviceString, dd.DeviceID),
                        DeviceId = $"0:{i - 1}" // 临时 ID，后续通过 QueryDisplayConfig 修正
                    };

                    displays.Add(display);
                }

                // 通过 QueryDisplayConfig 获取更准确的显示器信息
                EnrichWithDisplayConfig(displays);

                // 填充 HDR 支持信息
                foreach (var display in displays)
                {
                    try
                    {
                        display.SupportsHdr = _hdrService.SupportsHdr(display.DeviceId);
                        if (display.SupportsHdr)
                        {
                            display.HdrEnabled = _hdrService.IsHdrEnabledAsync(display.DeviceId)
                                .GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to query HDR info for {Display}", display.DisplayName);
                    }
                }

                _cachedDisplays = displays;
                DisplaysChanged?.Invoke(this, displays.AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshAsync failed");
            }
        });
    }

    /// <summary>通过 CCD API 丰富显示器信息</summary>
    private void EnrichWithDisplayConfig(List<DisplayInfo> displays)
    {
        try
        {
            uint pathCount = 0, modeCount = 0;
            int error = NativeMethods.GetDisplayConfigBufferSizes(
                NativeTypes.QDC_DATABASE_CURRENT,
                out pathCount, out modeCount);

            if (error != NativeTypes.ERROR_SUCCESS)
                return;

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            error = NativeMethods.QueryDisplayConfig(
                NativeTypes.QDC_DATABASE_CURRENT,
                ref pathCount, paths,
                ref modeCount, modes,
                out uint _);

            if (error != NativeTypes.ERROR_SUCCESS)
                return;

            for (int i = 0; i < pathCount && i < displays.Count; i++)
            {
                var path = paths[i];
                displays[i].AdapterId = (uint)(path.sourceInfo.adapterId & 0xFFFFFFFF);
                displays[i].SourceId = path.sourceInfo.id;
                displays[i].DeviceId = $"{path.sourceInfo.adapterId}:{path.sourceInfo.id}";

                if (path.sourceInfo.modeInfoIdx < modeCount)
                {
                    var mode = modes[path.sourceInfo.modeInfoIdx];
                    displays[i].ResolutionX = (int)mode.info.sourceMode.width;
                    displays[i].ResolutionY = (int)mode.info.sourceMode.height;
                }

                if (path.targetInfo.refreshRate.Denominator > 0)
                {
                    displays[i].RefreshRate = path.targetInfo.refreshRate.Numerator
                                            / path.targetInfo.refreshRate.Denominator;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnrichWithDisplayConfig failed");
        }
    }

    /// <summary>判断是否为内置显示器</summary>
    private static bool IsInternalDisplay(string deviceString, string deviceId)
    {
        // 内置显示器通常包含关键词
        var lower = (deviceString + deviceId).ToLowerInvariant();
        return lower.Contains("internal")
            || lower.Contains("built-in")
            || lower.Contains("laptop")
            || lower.Contains("integrated")
            || lower.Contains("mobile")
            || deviceId.Contains("LGD")   // LG Display (笔记本面板)
            || deviceId.Contains("AUO")    // AU Optronics (笔记本面板)
            || deviceId.Contains("BOE")    // BOE (笔记本面板)
            || deviceId.Contains("CMN")    // Chi Mei (笔记本面板)
            || deviceId.Contains("IVO");   // InfoVision (笔记本面板)
    }
}
