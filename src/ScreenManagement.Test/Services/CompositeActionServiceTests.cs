using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;

namespace ScreenManagement.Test.Services;

public class CompositeActionServiceTests
{
    private readonly Mock<IDisplayService> _mockDisplayService;
    private readonly Mock<IHdrService> _mockHdrService;
    private readonly Mock<IConfigService> _mockConfigService;
    private readonly Mock<ILogger<CompositeActionService>> _mockLogger;
    private readonly CompositeActionService _service;

    public CompositeActionServiceTests()
    {
        _mockDisplayService = new Mock<IDisplayService>();
        _mockHdrService = new Mock<IHdrService>();
        _mockConfigService = new Mock<IConfigService>();
        _mockLogger = new Mock<ILogger<CompositeActionService>>();

        _mockDisplayService
            .Setup(s => s.SetDisplayModeAsync(It.IsAny<DisplayMode>()))
            .ReturnsAsync(true);
        _mockDisplayService
            .Setup(s => s.GetCurrentMode())
            .Returns(DisplayMode.Internal);

        _mockHdrService
            .Setup(s => s.ToggleHdrAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockConfigService
            .Setup(s => s.LoadAsync())
            .ReturnsAsync(new AppConfig { EnableStateRestore = false });

        _service = new CompositeActionService(
            _mockDisplayService.Object,
            _mockHdrService.Object,
            _mockConfigService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Execute_SetDisplayMode_CallsDisplayService()
    {
        // Arrange
        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.Extend
        };

        // Act
        await _service.ExecuteAsync(binding);

        // Assert
        _mockDisplayService.Verify(
            s => s.SetDisplayModeAsync(DisplayMode.Extend),
            Times.Once);
    }

    [Fact]
    public async Task Execute_ToggleHdr_CallsHdrService()
    {
        // Arrange
        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.ToggleHdr,
            TargetDisplayId = "test:0"
        };

        // Act
        await _service.ExecuteAsync(binding);

        // Assert
        _mockHdrService.Verify(
            s => s.ToggleHdrAsync("test:0"),
            Times.Once);
    }

    [Fact]
    public async Task Execute_CompositeAction_ExecutesSubActions()
    {
        // Arrange
        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.CompositeAction,
            SubActions = new List<HotkeyBinding>
            {
                new() { ActionType = HotkeyActionType.SetDisplayMode, TargetMode = DisplayMode.Extend },
                new() { ActionType = HotkeyActionType.ToggleHdr, TargetDisplayId = "display:0" }
            }
        };

        // Act
        await _service.ExecuteAsync(binding);

        // Assert
        _mockDisplayService.Verify(
            s => s.SetDisplayModeAsync(DisplayMode.Extend),
            Times.Once);
        _mockHdrService.Verify(
            s => s.ToggleHdrAsync("display:0"),
            Times.Once);
    }

    [Fact]
    public async Task Execute_CompositeWithNoSubActions_DoesNotThrow()
    {
        // Arrange
        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.CompositeAction,
            SubActions = new List<HotkeyBinding>()
        };

        // Act
        var act = () => _service.ExecuteAsync(binding);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Bug fix 1: 状态恢复——已处于目标状态但无历史时不应触发无意义切换
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StateRestore_SetDisplayMode_AlreadyAtTargetNoHistory_DoesNotSwitch()
    {
        // 已处于目标状态且没有历史记录 → 不应调用 SetDisplayModeAsync
        _mockConfigService
            .Setup(s => s.LoadAsync())
            .ReturnsAsync(new AppConfig { EnableStateRestore = true });
        _mockDisplayService
            .Setup(s => s.GetCurrentMode())
            .Returns(DisplayMode.Extend); // 当前已是目标

        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.Extend
        };

        await _service.ExecuteAsync(binding);

        _mockDisplayService.Verify(
            s => s.SetDisplayModeAsync(It.IsAny<DisplayMode>()),
            Times.Never);
    }

    [Fact]
    public async Task StateRestore_SetDisplayMode_SwitchThenRestore()
    {
        // 第一次按键：不在目标 → 切换；第二次按键：已在目标且有历史 → 恢复
        _mockConfigService
            .Setup(s => s.LoadAsync())
            .ReturnsAsync(new AppConfig { EnableStateRestore = true });

        var currentMode = DisplayMode.Internal;
        _mockDisplayService
            .Setup(s => s.GetCurrentMode())
            .Returns(() => currentMode);
        _mockDisplayService
            .Setup(s => s.SetDisplayModeAsync(It.IsAny<DisplayMode>()))
            .Callback<DisplayMode>(m => currentMode = m)
            .ReturnsAsync(true);

        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.Extend
        };

        // 第一次：Internal → Extend
        await _service.ExecuteAsync(binding);
        currentMode.Should().Be(DisplayMode.Extend);

        // 第二次：Extend（目标）→ 恢复到 Internal
        await _service.ExecuteAsync(binding);
        currentMode.Should().Be(DisplayMode.Internal);

        // 第三次：不在目标（Internal）→ 再次切换到 Extend
        await _service.ExecuteAsync(binding);
        currentMode.Should().Be(DisplayMode.Extend);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Bug fix 2: 组合动作——整体预判模式，避免子动作互相影响状态检测
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StateRestore_CompositeAction_CapturesAllStateBeforeApply()
    {
        // 验证：应用组合动作时，所有子动作的历史状态在任何切换发生前就被采集
        // 场景：[SetDisplayMode(External), SetHDR(DisplayX, true)]
        // 第一次按键时 DisplayMode=Extend、HDR=false → 应保存 Extend 和 false
        _mockConfigService
            .Setup(s => s.LoadAsync())
            .ReturnsAsync(new AppConfig { EnableStateRestore = true });

        var currentDisplayMode = DisplayMode.Extend;
        var currentHdr = false;

        _mockDisplayService
            .Setup(s => s.GetCurrentMode())
            .Returns(() => currentDisplayMode);
        _mockDisplayService
            .Setup(s => s.SetDisplayModeAsync(It.IsAny<DisplayMode>()))
            .Callback<DisplayMode>(m => currentDisplayMode = m)
            .ReturnsAsync(true);

        _mockHdrService
            .Setup(s => s.IsHdrEnabledAsync(It.IsAny<string>()))
            .ReturnsAsync(() => currentHdr);
        _mockHdrService
            .Setup(s => s.SetHdrAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, v) => currentHdr = v)
            .ReturnsAsync(true);

        var displaySubAction = new HotkeyBinding
        {
            Id = "sub-display",
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.External
        };
        var hdrSubAction = new HotkeyBinding
        {
            Id = "sub-hdr",
            ActionType = HotkeyActionType.ToggleHdr,
            TargetDisplayId = "display:0",
            HdrTargetState = true
        };
        var composite = new HotkeyBinding
        {
            ActionType = HotkeyActionType.CompositeAction,
            SubActions = new List<HotkeyBinding> { displaySubAction, hdrSubAction }
        };

        // 第一次：应用 → External + HDR on
        await _service.ExecuteAsync(composite);
        currentDisplayMode.Should().Be(DisplayMode.External);
        currentHdr.Should().BeTrue();

        // 第二次：应恢复到 Extend + HDR off
        await _service.ExecuteAsync(composite);
        currentDisplayMode.Should().Be(DisplayMode.Extend);
        currentHdr.Should().BeFalse();
    }

    [Fact]
    public async Task StateRestore_CompositeAction_RestoreUsesStoredState_NotCurrentState()
    {
        // 关键验证：恢复时直接读取已存储的历史值，不重新检测当前状态
        // 模拟切换显示模式后 HDR 状态被系统重置的场景
        _mockConfigService
            .Setup(s => s.LoadAsync())
            .ReturnsAsync(new AppConfig { EnableStateRestore = true });

        var callOrder = new List<string>();
        var currentDisplayMode = DisplayMode.Extend;

        _mockDisplayService
            .Setup(s => s.GetCurrentMode())
            .Returns(() => currentDisplayMode);
        _mockDisplayService
            .Setup(s => s.SetDisplayModeAsync(It.IsAny<DisplayMode>()))
            .Callback<DisplayMode>(m =>
            {
                currentDisplayMode = m;
                callOrder.Add($"SetDisplay:{m}");
            })
            .ReturnsAsync(true);

        // HDR 状态随显示模式切换而重置（模拟系统行为）
        var hdrState = false;
        _mockHdrService
            .Setup(s => s.IsHdrEnabledAsync(It.IsAny<string>()))
            .ReturnsAsync(() => hdrState);
        _mockHdrService
            .Setup(s => s.SetHdrAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, bool>((_, v) =>
            {
                hdrState = v;
                callOrder.Add($"SetHdr:{v}");
            })
            .ReturnsAsync(true);

        var displaySubAction = new HotkeyBinding
        {
            Id = "sub-display",
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.External
        };
        var hdrSubAction = new HotkeyBinding
        {
            Id = "sub-hdr",
            ActionType = HotkeyActionType.ToggleHdr,
            TargetDisplayId = "display:0",
            HdrTargetState = true
        };
        var composite = new HotkeyBinding
        {
            ActionType = HotkeyActionType.CompositeAction,
            SubActions = new List<HotkeyBinding> { displaySubAction, hdrSubAction }
        };

        // 第一次：应用
        await _service.ExecuteAsync(composite);
        callOrder.Should().ContainInOrder("SetDisplay:External", "SetHdr:True");

        callOrder.Clear();

        // 第二次：恢复（不管 HDR 现在的实际状态如何，都应恢复到历史值 false）
        await _service.ExecuteAsync(composite);
        callOrder.Should().ContainInOrder("SetDisplay:Extend", "SetHdr:False");
        currentDisplayMode.Should().Be(DisplayMode.Extend);
        hdrState.Should().BeFalse();
    }
}
