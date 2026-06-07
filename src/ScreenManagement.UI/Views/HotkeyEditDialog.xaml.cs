using System.Windows;
using System.Windows.Input;
using ScreenManagement.UI.ViewModels;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ScreenManagement.UI.Views;

/// <summary>
/// 快捷键编辑弹窗
/// </summary>
public partial class HotkeyEditDialog : Window
{
    public HotkeyEditDialog()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Loaded += async (s, e) =>
        {
            if (DataContext is HotkeyEditViewModel vm)
                await vm.LoadDisplaysCommand.ExecuteAsync(null);
        };
    }

    private void OnKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (DataContext is not HotkeyEditViewModel vm || !vm.IsWaitingForKeyPress)
            return;

        // 捕获非修饰键
        var key = e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return;

        var keyCode = (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
        vm.OnKeyPressed(keyCode, key.ToString());
        e.Handled = true;
    }
}
