using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Services;

/// <summary>配置读写服务 — 使用 JSON 文件持久化</summary>
public class ConfigService : IConfigService
{
    private readonly ILogger<ConfigService> _logger;
    private readonly string _configDir;
    private readonly string _configFile;

    /// <inheritdoc />
    public event EventHandler<AppConfig>? ConfigChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
        _configDir = AppContext.BaseDirectory;
        _configFile = Path.Combine(_configDir, "config.json");
    }

    /// <inheritdoc />
    public string ConfigFilePath => _configFile;

    /// <inheritdoc />
    public async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (!File.Exists(_configFile))
            {
                _logger.LogInformation("Config file not found, creating default config at {Path}", _configFile);
                var defaultConfig = AppConfig.CreateDefault();
                await SaveAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configFile);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);

            if (config == null)
            {
                _logger.LogWarning("Failed to deserialize config, using default");
                return AppConfig.CreateDefault();
            }

            _logger.LogInformation("Config loaded from {Path}", _configFile);
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load config, using default");
            return AppConfig.CreateDefault();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppConfig config)
    {
        try
        {
            if (!Directory.Exists(_configDir))
                Directory.CreateDirectory(_configDir);

            var json = JsonSerializer.Serialize(config, JsonOptions);
            await File.WriteAllTextAsync(_configFile, json);

            _logger.LogInformation("Config saved to {Path}", _configFile);
            ConfigChanged?.Invoke(this, config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save config to {Path}", _configFile);
            throw;
        }
    }
}
