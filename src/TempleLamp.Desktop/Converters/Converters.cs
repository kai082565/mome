using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TempleLamp.Desktop.Converters;

/// <summary>
/// 步驟顏色轉換器
/// </summary>
public class StepColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepParam && int.TryParse(stepParam, out int step))
        {
            if (currentStep >= step)
            {
                return new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)); // #1976D2
            }
        }
        return new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)); // #E0E0E0
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 步驟可見性轉換器
/// </summary>
public class StepVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int currentStep && parameter is string stepParam && int.TryParse(stepParam, out int step))
        {
            return currentStep == step ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 狀態顏色轉換器
/// </summary>
public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "AVAILABLE" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)), // Green
                "LOCKED" => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),    // Orange
                "SOLD" => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)),      // Gray
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Null 轉可見性轉換器
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 反向 Boolean 轉換器
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}
