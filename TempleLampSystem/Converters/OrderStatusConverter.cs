using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TempleLampSystem.Converters;

public class OrderStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime endDate)
        {
            var today = DateTime.UtcNow.Date;
            var daysLeft = (endDate - today).Days;

            if (endDate < today)
                return "已過期";
            if (daysLeft <= 30)
                return $"即將到期（{daysLeft}天）";
            return "有效";
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class OrderStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime endDate)
        {
            var today = DateTime.UtcNow.Date;
            var daysLeft = (endDate - today).Days;

            if (endDate < today)
                return new SolidColorBrush(Colors.Gray);
            if (daysLeft <= 30)
                return new SolidColorBrush(Colors.OrangeRed);
            return new SolidColorBrush(Colors.Green);
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
