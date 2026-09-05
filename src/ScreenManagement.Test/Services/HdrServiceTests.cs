using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Native;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;
using System.Runtime.InteropServices;

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

    // ── 原生结构体布局保护（防止 P/Invoke 内存布局漂移） ──

    [Fact]
    public void GetAdvancedColorInfo2_LayoutMatchesWindows()
    {
        // header(20) + value(4) + colorEncoding(4) + bitsPerColorChannel(4) + activeColorMode(4)
        Marshal.SizeOf<DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2>().Should().Be(36);
    }

    [Fact]
    public void SetHdrState_LayoutMatchesWindows()
    {
        // header(20) + value(4)
        Marshal.SizeOf<DISPLAYCONFIG_SET_HDR_STATE>().Should().Be(24);
    }

    [Fact]
    public void GetAdvancedColorInfo2_BitFlagsMatchWindows()
    {
        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 { value = 0x01 }.AdvancedColorSupported.Should().BeTrue();
        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 { value = 0x02 }.AdvancedColorActive.Should().BeTrue();
        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 { value = 0x10 }.HighDynamicRangeSupported.Should().BeTrue();
        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 { value = 0x20 }.HighDynamicRangeUserEnabled.Should().BeTrue();
        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 { value = 0x40 }.WideColorSupported.Should().BeTrue();
        new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO_2 { value = 0x80 }.WideColorUserEnabled.Should().BeTrue();
    }
}
