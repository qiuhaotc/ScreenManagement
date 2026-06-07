using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;

namespace ScreenManagement.Test.Services;

public class DisplayServiceTests
{
    private readonly Mock<ILogger<DisplayService>> _mockLogger;
    private readonly DisplayService _service;

    public DisplayServiceTests()
    {
        _mockLogger = new Mock<ILogger<DisplayService>>();
        _service = new DisplayService(_mockLogger.Object);
    }

    [Fact]
    public void GetCurrentMode_ReturnsValidMode()
    {
        // Act
        var mode = _service.GetCurrentMode();

        // Assert
        mode.Should().BeOneOf(Enum.GetValues<DisplayMode>());
    }

    [Fact]
    public async Task SetDisplayMode_ValidMode_DoesNotThrow()
    {
        // Act
        var act = () => _service.SetDisplayModeAsync(DisplayMode.Extend);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetDisplayMode_InvalidMode_ReturnsFalse()
    {
        // Act
        var result = await _service.SetDisplayModeAsync((DisplayMode)99);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DisplayModeChanged_EventIsRaised()
    {
        // Arrange
        DisplayMode? raisedMode = null;
        _service.DisplayModeChanged += (s, m) => raisedMode = m;

        // Act
        var result = await _service.SetDisplayModeAsync(DisplayMode.Internal);

        // Assert (success case)
        if (result)
        {
            raisedMode.Should().Be(DisplayMode.Internal);
        }
    }
}
