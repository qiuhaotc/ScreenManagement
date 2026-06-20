using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ScreenManagement.Business.Models;

namespace ScreenManagement.UI.Converters;

/// <summary>布尔值转 HDR 状态文本</summary>
public class BoolToHdrStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool enabled)
            return enabled ? "● 已开启" : "○ 已关闭";
        return "○ 不支持";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>显示模式转图标</summary>
public class DisplayModeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DisplayMode mode)
        {
            return mode switch
            {
                DisplayMode.Internal => "💻",
                DisplayMode.Clone => "🔄",
                DisplayMode.Extend => "📺📺",
                DisplayMode.External => "🖥",
                _ => "❓"
            };
        }
        return "❓";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>显示模式转中文名称</summary>
public class DisplayModeToNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DisplayMode mode)
        {
            return mode switch
            {
                DisplayMode.Internal => "仅电脑屏幕",
                DisplayMode.Clone => "复制",
                DisplayMode.Extend => "扩展",
                DisplayMode.External => "仅第二屏幕",
                _ => "未知"
            };
        }
        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>布尔值转 Visibility（true=Visible, false=Collapsed）</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        // 非空字符串也视为可见
        if (value is string s)
            return string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>取反转换器（支持 bool 和 Visibility 目标类型）</summary>
public class NotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            if (targetType == typeof(Visibility))
                return b ? Visibility.Collapsed : Visibility.Visible;
            return !b;
        }
        return targetType == typeof(Visibility) ? Visibility.Collapsed : (object)false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return !b;
        if (value is Visibility v)
            return v != Visibility.Visible;
        return false;
    }
}
