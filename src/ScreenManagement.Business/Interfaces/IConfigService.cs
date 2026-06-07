using ScreenManagement.Business.Models;

namespace ScreenManagement.Business.Interfaces;

/// <summary>配置读写服务</summary>
public interface IConfigService
{
    /// <summary>加载配置</summary>
    Task<AppConfig> LoadAsync();

    /// <summary>保存配置</summary>
    Task SaveAsync(AppConfig config);

    /// <summary>配置文件路径</summary>
    string ConfigFilePath { get; }
}
