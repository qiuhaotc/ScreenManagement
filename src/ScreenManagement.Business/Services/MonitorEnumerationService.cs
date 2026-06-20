using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Native;
using System.Runtime.InteropServices;

namespace ScreenManagement.Business.Services;

/// <summary>显示器枚举服务 — 使用 CCD QueryDisplayConfig 仅枚举活动（已连接并启用）的显示器</summary>
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
                var displays = EnumerateActiveDisplays();
                _cachedDisplays = displays;
                DisplaysChanged?.Invoke(this, displays.AsReadOnly());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshAsync failed");
            }
        });
    }

    /// <summary>使用 CCD API 枚举所有活动显示路径（真实连接且启用的显示器）</summary>
    private List<DisplayInfo> EnumerateActiveDisplays()
    {
        var displays = new List<DisplayInfo>();

        // 仅查询活动路径，避免列出 GPU 上所有未连接的物理接口
        int error = NativeMethods.GetDisplayConfigBufferSizes(
            NativeTypes.QDC_ONLY_ACTIVE_PATHS,
            out uint pathCount, out uint modeCount);

        if (error != NativeTypes.ERROR_SUCCESS)
        {
            _logger.LogWarning("GetDisplayConfigBufferSizes failed: {Error}", error);
            return displays;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        error = NativeMethods.QueryDisplayConfig(
            NativeTypes.QDC_ONLY_ACTIVE_PATHS,
            ref pathCount, paths,
            ref modeCount, modes,
            IntPtr.Zero);

        if (error != NativeTypes.ERROR_SUCCESS)
        {
            _logger.LogWarning("QueryDisplayConfig failed: {Error}", error);
            return displays;
        }

        // 用于去重：同一物理显示器在克隆模式下可能出现在多条路径中
        var seenTargets = new HashSet<string>();
        bool firstActive = true;

        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];

            // 唯一标识：adapterId(LUID) + targetId
            string deviceId = $"{path.targetInfo.adapterId}:{path.targetInfo.id}";
            if (!seenTargets.Add(deviceId))
                continue;

            // 获取显示器友好名称与输出技术
            var (friendlyName, outputTechnology, _) =
                GetTargetDeviceName(path.targetInfo.adapterId, path.targetInfo.id);

            bool isInternal =
                outputTechnology == NativeTypes.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL ||
                outputTechnology == NativeTypes.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED ||
                outputTechnology == NativeTypes.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED;

            var display = new DisplayInfo
            {
                DeviceId = deviceId,
                DisplayName = !string.IsNullOrWhiteSpace(friendlyName)
                    ? friendlyName
                    : (isInternal ? "内置显示器" : $"显示器 {displays.Count + 1}"),
                IsInternal = isInternal,
                IsPrimary = firstActive,
                AdapterId = (uint)(path.targetInfo.adapterId & 0xFFFFFFFF),
                SourceId = path.sourceInfo.id
            };

            firstActive = false;

            // 分辨率：从 source 模式信息读取
            uint srcIdx = path.sourceInfo.modeInfoIdx;
            if (srcIdx != NativeTypes.DISPLAYCONFIG_PATH_MODE_IDX_INVALID && srcIdx < modeCount)
            {
                var srcMode = modes[srcIdx].info.sourceMode;
                display.ResolutionX = (int)srcMode.width;
                display.ResolutionY = (int)srcMode.height;
            }

            // 刷新率
            if (path.targetInfo.refreshRate.Denominator > 0)
            {
                display.RefreshRate = path.targetInfo.refreshRate.Numerator
                                    / path.targetInfo.refreshRate.Denominator;
            }

            // HDR 支持与状态
            try
            {
                display.SupportsHdr = _hdrService.SupportsHdr(deviceId);
                if (display.SupportsHdr)
                {
                    display.HdrEnabled = _hdrService.IsHdrEnabledAsync(deviceId)
                        .GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query HDR info for {Display}", display.DisplayName);
            }

            displays.Add(display);
        }

        _logger.LogInformation("Enumerated {Count} active display(s)", displays.Count);
        return displays;
    }

    /// <summary>获取目标显示器的友好名称和输出技术</summary>
    private (string friendlyName, uint outputTechnology, bool success) GetTargetDeviceName(
        long adapterId, uint targetId)
    {
        try
        {
            var request = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = NativeTypes.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = adapterId,
                    id = targetId
                }
            };

            int error = NativeMethods.DisplayConfigGetDeviceInfo(ref request);
            if (error != NativeTypes.ERROR_SUCCESS)
            {
                _logger.LogDebug("DisplayConfigGetDeviceInfo (target name) failed: {Error}", error);
                return (string.Empty, 0, false);
            }

            return (request.monitorFriendlyDeviceName, request.outputTechnology, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetTargetDeviceName failed for target {TargetId}", targetId);
            return (string.Empty, 0, false);
        }
    }
}
