using CommunityToolkit.Mvvm.ComponentModel;
using System.Reflection;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 关于界面 ViewModel
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty] private string _version = GetAppVersion();
    [ObservableProperty] private string _developer = "qiuhaotc";
    [ObservableProperty] private string _githubUrl = "https://github.com/qiuhaotc/ScreenManagement";
    [ObservableProperty] private string _copyright = $"© {DateTime.Now.Year} qiuhaotc";

    /// <summary>获取运行环境信息</summary>
    public string RuntimeInfo =>
        $".NET {Environment.Version} | {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";

    private static string GetAppVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVer))
        {
            // 去掉可能附加的 git hash（格式：1.0.0+abc123）
            var plus = infoVer.IndexOf('+');
            return plus >= 0 ? infoVer[..plus] : infoVer;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.1.0";
    }
}
