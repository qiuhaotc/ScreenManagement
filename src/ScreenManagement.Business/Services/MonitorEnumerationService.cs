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

        // HDR 状态变更时自动刷新缓存
        _hdrService.HdrStateChanged += async (s, e) => await RefreshAsync();
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
        var seenTargets = new HashSet<string>();

        // 第一步：查询活动路径，获取完整的分辨率、刷新率等信息
        EnumerateDisplayPaths(NativeTypes.QDC_ONLY_ACTIVE_PATHS, displays, seenTargets, markAsActive: true);

        // 第二步：查询所有路径，将已连接但当前未激活的显示器也加入列表
        EnumerateDisplayPaths(NativeTypes.QDC_ALL_PATHS, displays, seenTargets, markAsActive: false);

        _logger.LogInformation("Enumerated {Total} display(s) ({Active} active, {Inactive} inactive)",
            displays.Count,
            displays.Count(d => d.IsActive),
            displays.Count(d => !d.IsActive));

        return displays;
    }

    /// <summary>按指定标志枚举显示路径，将结果追加到 displays 列表中</summary>
    private void EnumerateDisplayPaths(uint flags, List<DisplayInfo> displays, HashSet<string> seenTargets, bool markAsActive)
    {
        int error = NativeMethods.GetDisplayConfigBufferSizes(flags, out uint pathCount, out uint modeCount);
        if (error != NativeTypes.ERROR_SUCCESS)
        {
            _logger.LogWarning("GetDisplayConfigBufferSizes(flags={Flags}) failed: {Error}", flags, error);
            return;
        }

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
        error = NativeMethods.QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
        if (error != NativeTypes.ERROR_SUCCESS)
        {
            _logger.LogWarning("QueryDisplayConfig(flags={Flags}) failed: {Error}", flags, error);
            return;
        }

        // 仅在枚举活动路径时才判断"第一个"作为主显示器
        bool firstActive = markAsActive;

        for (int i = 0; i < pathCount; i++)
        {
            var path = paths[i];

            // 先获取设备名称，以便使用稳定的 monitorDevicePath 作为唯一标识
            var (friendlyName, outputTechnology, monitorDevicePath, success) =
                GetTargetDeviceName(path.targetInfo.adapterId, path.targetInfo.id);

            // 优先使用 monitorDevicePath（跨重启稳定），回退到 adapterId:targetId（LUID，重启后会变）
            string deviceId = !string.IsNullOrEmpty(monitorDevicePath)
                ? monitorDevicePath
                : $"{path.targetInfo.adapterId}:{path.targetInfo.id}";

            if (!seenTargets.Add(deviceId))
                continue;

            bool isInternal =
                outputTechnology == NativeTypes.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL ||
                outputTechnology == NativeTypes.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED ||
                outputTechnology == NativeTypes.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED;

            // 在"所有路径"查询中，判断是否为真实物理连接的显示器：
            // 必须满足 GetTargetDeviceName 成功，且拥有 EDID 友好名称（证明有真实显示器），
            // 或者是内置显示器（嵌入式面板通常无 EDID 名称）。
            if (!markAsActive && (!success || (string.IsNullOrWhiteSpace(friendlyName) && !isInternal)))
                continue;

            var display = new DisplayInfo
            {
                DeviceId = deviceId,
                DisplayName = !string.IsNullOrWhiteSpace(friendlyName)
                    ? friendlyName
                    : (isInternal ? "内置显示器" : $"显示器 {displays.Count + 1}"),
                IsInternal = isInternal,
                IsActive = markAsActive,
                IsPrimary = markAsActive && firstActive,
                AdapterId = (uint)(path.targetInfo.adapterId & 0xFFFFFFFF),
                SourceId = path.sourceInfo.id
            };

            if (markAsActive)
            {
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
            }

            // HDR 支持与状态（连接但未激活的显示器也可查询）
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
    }

    /// <summary>获取目标显示器的友好名称、输出技术和稳定设备路径</summary>
    private (string friendlyName, uint outputTechnology, string monitorDevicePath, bool success) GetTargetDeviceName(
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
                return (string.Empty, 0, string.Empty, false);
            }

            return (request.monitorFriendlyDeviceName, request.outputTechnology, request.monitorDevicePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetTargetDeviceName failed for target {TargetId}", targetId);
            return (string.Empty, 0, string.Empty, false);
        }
    }
}
