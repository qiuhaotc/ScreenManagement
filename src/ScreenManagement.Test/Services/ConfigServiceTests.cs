using Microsoft.Extensions.Logging;
using Moq;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Services;
using Xunit;
using FluentAssertions;

namespace ScreenManagement.Test.Services;

public class ConfigServiceTests
{
    private readonly Mock<ILogger<ConfigService>> _mockLogger;
    private readonly ConfigService _service;

    public ConfigServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConfigService>>();
        _service = new ConfigService(_mockLogger.Object);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesData()
    {
        // Arrange
        var original = AppConfig.CreateDefault();
        original.AutoStart = true;
        original.HotkeyBindings.Add(new HotkeyBinding
        {
            ActionType = HotkeyActionType.SetDisplayMode,
            TargetMode = DisplayMode.Extend,
            Modifiers = ModifierKeys.Control | ModifierKeys.Alt,
            Key = 0x42, // B
            IsEnabled = true
        });

        original.HotkeyBindings[0].IsEnabled = false;

        // Act
        await _service.SaveAsync(original);
        var loaded = await _service.LoadAsync();

        // Assert
        loaded.AutoStart.Should().BeTrue();
        loaded.HotkeyBindings[0].IsEnabled.Should().BeFalse();
        loaded.HotkeyBindings.Should().HaveCount(original.HotkeyBindings.Count);
    }

    [Fact]
    public void ConfigFilePath_IsInAppData()
    {
        // Assert
        _service.ConfigFilePath.Should().Contain("ScreenManagement");
        _service.ConfigFilePath.Should().Contain("config.json");
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectory()
    {
        // Arrange
        var config = AppConfig.CreateDefault();

        // Act
        var act = () => _service.SaveAsync(config);

        // Assert
        await act.Should().NotThrowAsync();
        File.Exists(_service.ConfigFilePath).Should().BeTrue();
    }
}
