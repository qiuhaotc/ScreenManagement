using ScreenManagement.Business.Interfaces;

namespace ScreenManagement.UI.Services;

/// <summary>
/// 桌面通知服务（使用托盘气泡 + 可选 Toast）
/// </summary>
public class NotificationService
{
    private readonly TrayIconService _trayService;
    private readonly IConfigService _configService;

    public NotificationService(TrayIconService trayService, IConfigService configService)
    {
        _trayService = trayService;
        _configService = configService;
    }

    /// <summary>显示通知</summary>
    public async void ShowNotification(string title, string message)
    {
        var config = await _configService.LoadAsync();
        if (!config.ShowNotifications) return;

        _trayService.ShowBalloon(title, message,
            System.Windows.Forms.ToolTipIcon.Info);
    }

    /// <summary>显示错误通知</summary>
    public void ShowError(string title, string message)
    {
        _trayService.ShowBalloon(title, message,
            System.Windows.Forms.ToolTipIcon.Error);
    }
}
