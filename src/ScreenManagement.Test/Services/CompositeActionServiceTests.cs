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
}
