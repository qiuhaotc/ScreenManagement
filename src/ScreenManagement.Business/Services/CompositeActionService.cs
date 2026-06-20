using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Services;

/// <summary>组合动作执行服务 — 支持嵌套/递归执行多个动作</summary>
public class CompositeActionService : ICompositeActionService
{
    private readonly IDisplayService _displayService;
    private readonly IHdrService _hdrService;
    private readonly IConfigService _configService;
    private readonly ILogger<CompositeActionService> _logger;

    /// <summary>记录每个绑定上次切换前的显示模式，键为 binding.Id</summary>
    private readonly Dictionary<string, DisplayMode> _previousDisplayModes = new();

    /// <summary>记录每个绑定上次切换前的 HDR 状态，键为 "bindingId:displayId"</summary>
    private readonly Dictionary<string, bool> _previousHdrStates = new();

    public CompositeActionService(
        IDisplayService displayService,
        IHdrService hdrService,
        IConfigService configService,
        ILogger<CompositeActionService> logger)
    {
        _displayService = displayService;
        _hdrService = hdrService;
        _configService = configService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HotkeyBinding binding)
    {
        _logger.LogInformation("Executing action: {ActionType}", binding.ActionType);

        try
        {
            switch (binding.ActionType)
            {
                case HotkeyActionType.SetDisplayMode:
                    await ExecuteSetDisplayModeAsync(binding);
                    break;

                case HotkeyActionType.ToggleHdr:
                    await ExecuteToggleHdrAsync(binding);
                    break;

                case HotkeyActionType.CompositeAction:
                    await ExecuteCompositeAsync(binding);
                    break;

                default:
                    _logger.LogWarning("Unknown action type: {ActionType}", binding.ActionType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute action {ActionType}", binding.ActionType);
        }
    }

    private async Task ExecuteSetDisplayModeAsync(HotkeyBinding binding)
    {
        if (!binding.TargetMode.HasValue)
        {
            _logger.LogWarning("SetDisplayMode action has no target mode");
            return;
        }

        var config = await _configService.LoadAsync();
        if (config.EnableStateRestore)
        {
            var currentMode = _displayService.GetCurrentMode();
            if (currentMode == binding.TargetMode.Value
                && _previousDisplayModes.TryGetValue(binding.Id, out var prevMode))
            {
                // 已经是目标状态，恢复之前的状态
                _previousDisplayModes.Remove(binding.Id);
                bool restoreResult = await _displayService.SetDisplayModeAsync(prevMode);
                _logger.LogInformation("RestoreDisplayMode({Mode}) result: {Result}", prevMode, restoreResult);
                return;
            }
            // 记录当前状态，然后切换
            _previousDisplayModes[binding.Id] = currentMode;
        }

        bool result = await _displayService.SetDisplayModeAsync(binding.TargetMode.Value);
        _logger.LogInformation("SetDisplayMode({Mode}) result: {Result}", binding.TargetMode.Value, result);
    }

    private async Task ExecuteToggleHdrAsync(HotkeyBinding binding)
    {
        if (string.IsNullOrEmpty(binding.TargetDisplayId))
        {
            _logger.LogWarning("ToggleHdr action has no target display");
            return;
        }

        if (binding.HdrTargetState.HasValue)
        {
            var config = await _configService.LoadAsync();
            if (config.EnableStateRestore)
            {
                var currentHdr = await _hdrService.IsHdrEnabledAsync(binding.TargetDisplayId);
                var key = $"{binding.Id}:{binding.TargetDisplayId}";
                if (currentHdr == binding.HdrTargetState.Value
                    && _previousHdrStates.TryGetValue(key, out var prevState))
                {
                    // 已经是目标状态，恢复之前的状态
                    _previousHdrStates.Remove(key);
                    bool restoreResult = await _hdrService.SetHdrAsync(binding.TargetDisplayId, prevState);
                    _logger.LogInformation("RestoreHdr({Display}, {State}) result: {Result}",
                        binding.TargetDisplayId, prevState, restoreResult);
                    return;
                }
                _previousHdrStates[key] = currentHdr;
            }

            bool result = await _hdrService.SetHdrAsync(binding.TargetDisplayId, binding.HdrTargetState.Value);
            _logger.LogInformation("SetHdr({Display}, {State}) result: {Result}",
                binding.TargetDisplayId, binding.HdrTargetState.Value, result);
        }
        else
        {
            bool result = await _hdrService.ToggleHdrAsync(binding.TargetDisplayId);
            _logger.LogInformation("ToggleHdr({Display}) result: {Result}",
                binding.TargetDisplayId, result);
        }
    }

    private async Task ExecuteCompositeAsync(HotkeyBinding binding)
    {
        if (binding.SubActions == null || binding.SubActions.Count == 0)
        {
            _logger.LogWarning("Composite action has no sub-actions");
            return;
        }

        _logger.LogInformation("Executing {Count} sub-actions", binding.SubActions.Count);

        foreach (var subAction in binding.SubActions)
        {
            // 递归执行子动作（子动作也可以是组合动作）
            await ExecuteAsync(subAction);

            // 在动作之间加短暂延迟，让系统有时间处理
            await Task.Delay(500);
        }

        _logger.LogInformation("Composite action completed ({Count} sub-actions)", binding.SubActions.Count);
    }
}
