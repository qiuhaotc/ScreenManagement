using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ScreenManagement.UI.Views;

/// <summary>
/// 关于窗口
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.AboutViewModel vm)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = vm.GithubUrl,
                UseShellExecute = true
            });
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
