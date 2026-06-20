using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ScreenManagement.Business.Interfaces;

namespace ScreenManagement.Business.Services;

/// <summary>开机自启服务 — 通过注册表 Run 键管理</summary>
public class AutostartService : IAutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "qiuhaotc.ScreenManagement";

    private readonly ILogger<AutostartService> _logger;

    public AutostartService(ILogger<AutostartService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsAutostartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(AppName);
            return value != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check autostart status");
            return false;
        }
    }

    /// <inheritdoc />
    public void SetAutostart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? "";
                var commandLine = $"\"{exePath}\"";

                // 检查是否有 --minimized 参数需要保留
                var args = Environment.GetCommandLineArgs();
                if (args.Contains("--minimized"))
                {
                    commandLine += " --minimized";
                }

                key?.SetValue(AppName, commandLine);
                _logger.LogInformation("Autostart enabled: {Command}", commandLine);
            }
            else
            {
                key?.DeleteValue(AppName, throwOnMissingValue: false);
                _logger.LogInformation("Autostart disabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set autostart to {Enabled}", enable);
        }
    }
}
