using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;

namespace ScreenManagement.Test.Services;

public class HdrServiceTests
{
    private readonly Mock<ILogger<HdrService>> _mockLogger;
    private readonly HdrService _service;

    public HdrServiceTests()
    {
        _mockLogger = new Mock<ILogger<HdrService>>();
        _service = new HdrService(_mockLogger.Object);
    }

    [Fact]
    public async Task IsHdrEnabled_InvalidId_ReturnsFalse()
    {
        // Act
        var result = await _service.IsHdrEnabledAsync("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetHdr_InvalidId_ReturnsFalse()
    {
        // Act
        var result = await _service.SetHdrAsync("", true);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SupportsHdr_InvalidId_ReturnsFalse()
    {
        // Act
        var result = _service.SupportsHdr("");

        // Assert
        result.Should().BeFalse();
    }
}
