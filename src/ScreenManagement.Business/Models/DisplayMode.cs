namespace ScreenManagement.Business.Models;

/// <summary>显示模式（对应 Windows DisplayTopology）</summary>
public enum DisplayMode
{
    Internal = 0,   // 仅电脑屏幕
    Clone = 1,      // 复制
    Extend = 2,     // 扩展
    External = 3    // 仅第二屏幕
}
