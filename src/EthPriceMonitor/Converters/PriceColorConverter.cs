using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace EthPriceMonitor.Converters;

/// <summary>
/// Converts a bool (IsPriceUp) to a Brush: true=#16C784 (green), false=#EA3943 (red).
/// </summary>
public class PriceColorConverter : IValueConverter
{
    private static readonly Brush UpBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16C784")!);
    private static readonly Brush DownBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EA3943")!);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isUp)
            return isUp ? UpBrush : DownBrush;

        return DownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
