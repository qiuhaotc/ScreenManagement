using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Services;

/// <summary>组合动作执行服务 — 支持嵌套/递归执行多个动作</summary>
public class CompositeActionService : ICompositeActionService
{
    private readonly IDisplayService _displayService;
    private readonly IHdrService _hdrService;
    private readonly ILogger<CompositeActionService> _logger;

    public CompositeActionService(
        IDisplayService displayService,
        IHdrService hdrService,
        ILogger<CompositeActionService> logger)
    {
        _displayService = displayService;
        _hdrService = hdrService;
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
        if (binding.TargetMode.HasValue)
        {
            bool result = await _displayService.SetDisplayModeAsync(binding.TargetMode.Value);
            _logger.LogInformation("SetDisplayMode({Mode}) result: {Result}", binding.TargetMode.Value, result);
        }
        else
        {
            _logger.LogWarning("SetDisplayMode action has no target mode");
        }
    }

    private async Task ExecuteToggleHdrAsync(HotkeyBinding binding)
    {
        if (!string.IsNullOrEmpty(binding.TargetDisplayId))
        {
            if (binding.HdrTargetState.HasValue)
            {
                bool result = await _hdrService.SetHdrAsync(
                    binding.TargetDisplayId, binding.HdrTargetState.Value);
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
        else
        {
            _logger.LogWarning("ToggleHdr action has no target display");
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
