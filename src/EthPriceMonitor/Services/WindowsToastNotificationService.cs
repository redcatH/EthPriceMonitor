using EthPriceMonitor.Models;
using Microsoft.Toolkit.Uwp.Notifications;

namespace EthPriceMonitor.Services;

/// <summary>
/// Windows Toast 通知服务实现，使用 Microsoft.Toolkit.Uwp.Notifications
/// </summary>
public class WindowsToastNotificationService : IToastNotificationService
{
    private const string HeaderId = "eth-price-alert";
    private const string HeaderTitle = "ETH 价格提醒";
    private const string HeaderArguments = "alert";

    /// <inheritdoc />
    public void ShowPriceAlert(decimal currentPrice, decimal thresholdPrice, AlertDirection direction)
    {
        string directionText = direction == AlertDirection.Above ? "高于" : "低于";

        new ToastContentBuilder()
            .AddHeader(HeaderId, HeaderTitle, HeaderArguments)
            .AddText($"ETH 价格{(direction == AlertDirection.Above ? "突破" : "跌破")} ${currentPrice:N2}")
            .AddText($"阈值: {directionText} ${thresholdPrice:N2}")
            .Show();
    }

    /// <inheritdoc />
    public string FormatAlertMessage(decimal currentPrice, decimal thresholdPrice, AlertDirection direction)
    {
        string action = direction == AlertDirection.Above ? "突破" : "跌破";
        string directionText = direction == AlertDirection.Above ? "高于" : "低于";
        return $"ETH 价格{action} ${currentPrice:N2} ({directionText} ${thresholdPrice:N2})";
    }
}
