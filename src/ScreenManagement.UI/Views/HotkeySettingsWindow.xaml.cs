using System.Windows;
using ScreenManagement.UI.ViewModels;

namespace ScreenManagement.UI.Views;

/// <summary>
/// 快捷键设置窗口
/// </summary>
public partial class HotkeySettingsWindow : Window
{
    public HotkeySettingsWindow()
    {
        InitializeComponent();
    }

    public HotkeySettingsWindow(HotkeySettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Loaded += async (s, e) => await viewModel.LoadCommand.ExecuteAsync(null);
    }
}
