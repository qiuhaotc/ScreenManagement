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
            if (currentMode == binding.TargetMode.Value)
            {
                // 已经处于目标状态：有历史则恢复，没有历史则不做任何操作（避免无意义的 API 调用）
                if (_previousDisplayModes.TryGetValue(binding.Id, out var prevMode))
                {
                    _previousDisplayModes.Remove(binding.Id);
                    bool restoreResult = await _displayService.SetDisplayModeAsync(prevMode);
                    _logger.LogInformation("RestoreDisplayMode({Mode}) result: {Result}", prevMode, restoreResult);
                }
                return;
            }
            // 不处于目标状态：记录当前状态，然后切换
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
                if (currentHdr == binding.HdrTargetState.Value)
                {
                    // 已经处于目标 HDR 状态：有历史则恢复，没有历史则不做任何操作
                    if (_previousHdrStates.TryGetValue(key, out var prevState))
                    {
                        _previousHdrStates.Remove(key);
                        bool restoreResult = await _hdrService.SetHdrAsync(binding.TargetDisplayId, prevState);
                        _logger.LogInformation("RestoreHdr({Display}, {State}) result: {Result}",
                            binding.TargetDisplayId, prevState, restoreResult);
                    }
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

        var subActions = binding.SubActions;
        _logger.LogInformation("Executing {Count} sub-actions", subActions.Count);

        var config = await _configService.LoadAsync();
        if (!config.EnableStateRestore)
        {
            // 未启用状态恢复：直接顺序执行
            foreach (var sub in subActions)
            {
                await ExecuteAsync(sub);
                await Task.Delay(500);
            }
            _logger.LogInformation("Composite action completed ({Count} sub-actions)", subActions.Count);
            return;
        }

        // 启用了状态恢复：在执行任何子动作之前，整体判断应该「应用」还是「恢复」。
        // 核心问题：子动作间有延迟且会互相影响（如切换显示模式会重置 HDR），
        // 若每个子动作各自独立检测，后面的子动作会读到被前面子动作改变后的错误状态。
        bool shouldRestore = await CheckAllSubActionsAtTargetAsync(subActions);

        if (shouldRestore)
        {
            _logger.LogInformation("Composite: restoring all sub-actions");
            foreach (var sub in subActions)
            {
                await RestoreSubActionAsync(sub);
                await Task.Delay(500);
            }
        }
        else
        {
            // 先一次性采集所有子动作的当前状态（在任何切换发生之前），然后再执行切换
            _logger.LogInformation("Composite: capturing states then applying all sub-actions");
            await CaptureSubActionStatesAsync(subActions);
            foreach (var sub in subActions)
            {
                await ApplySubActionAsync(sub);
                await Task.Delay(500);
            }
        }

        _logger.LogInformation("Composite action completed ({Count} sub-actions)", subActions.Count);
    }

    /// <summary>
    /// 检查所有子动作是否都已处于目标状态且存有历史记录（即可以全部恢复）。
    /// 在任何子动作执行之前调用，读到的是真正的"执行前"状态。
    /// </summary>
    private async Task<bool> CheckAllSubActionsAtTargetAsync(List<HotkeyBinding> subActions)
    {
        foreach (var sub in subActions)
        {
            switch (sub.ActionType)
            {
                case HotkeyActionType.SetDisplayMode:
                    if (!sub.TargetMode.HasValue) return false;
                    if (_displayService.GetCurrentMode() != sub.TargetMode.Value) return false;
                    if (!_previousDisplayModes.ContainsKey(sub.Id)) return false;
                    break;

                case HotkeyActionType.ToggleHdr when sub.HdrTargetState.HasValue:
                    if (string.IsNullOrEmpty(sub.TargetDisplayId)) return false;
                    var hdrKey = $"{sub.Id}:{sub.TargetDisplayId}";
                    bool currentHdr = await _hdrService.IsHdrEnabledAsync(sub.TargetDisplayId);
                    if (currentHdr != sub.HdrTargetState.Value) return false;
                    if (!_previousHdrStates.ContainsKey(hdrKey)) return false;
                    break;

                case HotkeyActionType.CompositeAction:
                    if (!await CheckAllSubActionsAtTargetAsync(sub.SubActions ?? [])) return false;
                    break;

                default:
                    // 纯切换（无目标状态）或未知类型：无法判断是否处于目标，视为「需要应用」
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 在执行任何子动作之前，一次性将所有子动作的当前状态写入历史字典。
    /// 这样即使后续子动作改变了系统状态，每个历史值记录的都是执行前的真实状态。
    /// </summary>
    private async Task CaptureSubActionStatesAsync(List<HotkeyBinding> subActions)
    {
        foreach (var sub in subActions)
        {
            switch (sub.ActionType)
            {
                case HotkeyActionType.SetDisplayMode:
                    _previousDisplayModes[sub.Id] = _displayService.GetCurrentMode();
                    break;

                case HotkeyActionType.ToggleHdr when sub.HdrTargetState.HasValue:
                    if (!string.IsNullOrEmpty(sub.TargetDisplayId))
                    {
                        var key = $"{sub.Id}:{sub.TargetDisplayId}";
                        _previousHdrStates[key] = await _hdrService.IsHdrEnabledAsync(sub.TargetDisplayId);
                    }
                    break;

                case HotkeyActionType.CompositeAction:
                    await CaptureSubActionStatesAsync(sub.SubActions ?? []);
                    break;
            }
        }
    }

    /// <summary>仅执行切换（状态已在 CaptureSubActionStatesAsync 中保存），不再重复检测。</summary>
    private async Task ApplySubActionAsync(HotkeyBinding sub)
    {
        switch (sub.ActionType)
        {
            case HotkeyActionType.SetDisplayMode:
                if (sub.TargetMode.HasValue)
                {
                    bool r = await _displayService.SetDisplayModeAsync(sub.TargetMode.Value);
                    _logger.LogInformation("[Composite] SetDisplayMode({Mode}) result: {Result}", sub.TargetMode.Value, r);
                }
                break;

            case HotkeyActionType.ToggleHdr when sub.HdrTargetState.HasValue:
                if (!string.IsNullOrEmpty(sub.TargetDisplayId))
                {
                    bool r = await _hdrService.SetHdrAsync(sub.TargetDisplayId, sub.HdrTargetState.Value);
                    _logger.LogInformation("[Composite] SetHdr({Display}, {State}) result: {Result}",
                        sub.TargetDisplayId, sub.HdrTargetState.Value, r);
                }
                break;

            case HotkeyActionType.ToggleHdr:
                if (!string.IsNullOrEmpty(sub.TargetDisplayId))
                {
                    bool r = await _hdrService.ToggleHdrAsync(sub.TargetDisplayId);
                    _logger.LogInformation("[Composite] ToggleHdr({Display}) result: {Result}", sub.TargetDisplayId, r);
                }
                break;

            case HotkeyActionType.CompositeAction:
                // 嵌套组合：状态已由外层递归采集，直接应用
                foreach (var nested in sub.SubActions ?? [])
                {
                    await ApplySubActionAsync(nested);
                    await Task.Delay(500);
                }
                break;
        }
    }

    /// <summary>仅执行恢复（从历史字典读取），不再重新检测当前状态。</summary>
    private async Task RestoreSubActionAsync(HotkeyBinding sub)
    {
        switch (sub.ActionType)
        {
            case HotkeyActionType.SetDisplayMode:
                if (_previousDisplayModes.TryGetValue(sub.Id, out var prevMode))
                {
                    _previousDisplayModes.Remove(sub.Id);
                    bool r = await _displayService.SetDisplayModeAsync(prevMode);
                    _logger.LogInformation("[Composite] RestoreDisplayMode({Mode}) result: {Result}", prevMode, r);
                }
                break;

            case HotkeyActionType.ToggleHdr when sub.HdrTargetState.HasValue:
                if (!string.IsNullOrEmpty(sub.TargetDisplayId))
                {
                    var key = $"{sub.Id}:{sub.TargetDisplayId}";
                    if (_previousHdrStates.TryGetValue(key, out var prevHdr))
                    {
                        _previousHdrStates.Remove(key);
                        bool r = await _hdrService.SetHdrAsync(sub.TargetDisplayId, prevHdr);
                        _logger.LogInformation("[Composite] RestoreHdr({Display}, {State}) result: {Result}",
                            sub.TargetDisplayId, prevHdr, r);
                    }
                }
                break;

            case HotkeyActionType.ToggleHdr:
                // 纯切换没有历史状态，按恢复语义再 toggle 一次
                if (!string.IsNullOrEmpty(sub.TargetDisplayId))
                {
                    bool r = await _hdrService.ToggleHdrAsync(sub.TargetDisplayId);
                    _logger.LogInformation("[Composite] ToggleHdr(restore) {Display} result: {Result}", sub.TargetDisplayId, r);
                }
                break;

            case HotkeyActionType.CompositeAction:
                foreach (var nested in sub.SubActions ?? [])
                {
                    await RestoreSubActionAsync(nested);
                    await Task.Delay(500);
                }
                break;
        }
    }
}
