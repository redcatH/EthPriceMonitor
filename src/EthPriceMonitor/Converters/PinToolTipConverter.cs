using System.Globalization;
using System.Windows.Data;

namespace EthPriceMonitor.Converters;

/// <summary>
/// Converts a bool (IsClickThrough) to a ToolTip string:
/// true = "取消置顶（恢复拖动）", false = "置顶（点击穿透）".
/// </summary>
public class PinToolTipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isClickThrough)
            return isClickThrough ? "取消置顶（恢复拖动）" : "置顶（点击穿透）";

        return "置顶（点击穿透）";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
