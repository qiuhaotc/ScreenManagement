using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;

namespace ScreenManagement.Test.Services;

public class MonitorEnumerationServiceTests
{
    private readonly Mock<ILogger<MonitorEnumerationService>> _mockLogger;
    private readonly Mock<IHdrService> _mockHdrService;
    private readonly MonitorEnumerationService _service;

    public MonitorEnumerationServiceTests()
    {
        _mockLogger = new Mock<ILogger<MonitorEnumerationService>>();
        _mockHdrService = new Mock<IHdrService>();

        _mockHdrService
            .Setup(s => s.SupportsHdr(It.IsAny<string>()))
            .Returns(false);

        _service = new MonitorEnumerationService(
            _mockLogger.Object,
            _mockHdrService.Object);
    }

    [Fact]
    public async Task GetDisplays_ReturnsList()
    {
        // Act
        var displays = await _service.GetDisplaysAsync();

        // Assert
        displays.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_DoesNotThrow()
    {
        // Act
        var act = () => _service.RefreshAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisplaysChanged_EventIsRaised()
    {
        // Arrange
        IReadOnlyList<DisplayInfo>? raisedDisplays = null;
        _service.DisplaysChanged += (s, d) => raisedDisplays = d;

        // Act
        await _service.RefreshAsync();

        // Assert
        raisedDisplays.Should().NotBeNull();
    }
}
