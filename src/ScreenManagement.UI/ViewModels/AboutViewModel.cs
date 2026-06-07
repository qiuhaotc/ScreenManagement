using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenManagement.UI.ViewModels;

/// <summary>
/// 关于界面 ViewModel
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty] private string _version = "0.1.0";
    [ObservableProperty] private string _developer = "qiuhaotc";
    [ObservableProperty] private string _githubUrl = "https://github.com/qiuhaotc/ScreenManagement";
    [ObservableProperty] private string _copyright = $"© {DateTime.Now.Year} Screen Management";

    /// <summary>获取运行环境信息</summary>
    public string RuntimeInfo =>
        $".NET {Environment.Version} | {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}";
}
