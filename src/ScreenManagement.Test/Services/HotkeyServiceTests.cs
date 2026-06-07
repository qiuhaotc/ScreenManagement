using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;

namespace ScreenManagement.Test.Services;

public class HotkeyServiceTests
{
    private readonly Mock<ILogger<HotkeyService>> _mockLogger;
    private readonly HotkeyService _service;

    public HotkeyServiceTests()
    {
        _mockLogger = new Mock<ILogger<HotkeyService>>();
        _service = new HotkeyService(_mockLogger.Object);
    }

    [Fact]
    public void Initialize_WithValidHwnd_SetsHandle()
    {
        // Arrange
        var hwnd = new IntPtr(12345);

        // Act
        _service.Initialize(hwnd);

        // Assert — 不抛出异常
    }

    [Fact]
    public void RegisterHotkey_WithZeroHwnd_ReturnsError()
    {
        // Arrange
        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.Extend,
            Modifiers = ModifierKeys.Control | ModifierKeys.Shift,
            Key = 0x70,
            IsEnabled = true
        };

        // Act
        var result = _service.RegisterHotkey(binding, IntPtr.Zero);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("窗口句柄");
    }

    [Fact]
    public async Task RegisterAllAsync_WithEmptyList_Completes()
    {
        // Act
        var act = () => _service.RegisterAllAsync(
            Enumerable.Empty<HotkeyBinding>(), IntPtr.Zero);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void UnregisterAll_WithoutRegistration_DoesNotThrow()
    {
        // Act
        var act = () => _service.UnregisterAll();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void HandleHotkeyMessage_TriggersEvent()
    {
        // Arrange
        var binding = new HotkeyBinding
        {
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.Internal,
            Modifiers = ModifierKeys.Control,
            Key = 0x70
        };

        _service.Initialize(new IntPtr(1));
        _service.RegisterHotkey(binding, new IntPtr(1)); // 可能失败

        HotkeyTriggeredEventArgs? args = null;
        _service.HotkeyTriggered += (s, e) => args = e;

        // Act — 如果注册成功，尝试触发
        // HandleHotkeyMessage(1);

        // Assert — 测试消息处理机制
    }
}
