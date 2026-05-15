using EthPriceMonitor.Models;
using EthPriceMonitor.Services;

namespace EthPriceMonitor.Tests.Services;

public class ToastNotificationServiceTests
{
    /// <summary>
    /// 测试：价格高于阈值时，格式化消息应返回 "ETH 价格突破 $X,XXX.XX (高于 $3,000.00)"
    /// </summary>
    [Fact]
    public void FormatAlertMessage_PriceAboveThreshold_ReturnsCorrectFormat()
    {
        // Arrange
        var service = new WindowsToastNotificationService();
        decimal currentPrice = 3250.75m;
        decimal thresholdPrice = 3000.00m;

        // Act
        string message = service.FormatAlertMessage(currentPrice, thresholdPrice, AlertDirection.Above);

        // Assert
        Assert.Equal("ETH 价格突破 $3,250.75 (高于 $3,000.00)", message);
    }

    /// <summary>
    /// 测试：价格低于阈值时，格式化消息应返回 "ETH 价格跌破 $X,XXX.XX (低于 $2,500.00)"
    /// </summary>
    [Fact]
    public void FormatAlertMessage_PriceBelowThreshold_ReturnsCorrectFormat()
    {
        // Arrange
        var service = new WindowsToastNotificationService();
        decimal currentPrice = 2345.50m;
        decimal thresholdPrice = 2500.00m;

        // Act
        string message = service.FormatAlertMessage(currentPrice, thresholdPrice, AlertDirection.Below);

        // Assert
        Assert.Equal("ETH 价格跌破 $2,345.50 (低于 $2,500.00)", message);
    }

    /// <summary>
    /// 测试：价格整数部分无千位分隔符时格式正确
    /// </summary>
    [Fact]
    public void FormatAlertMessage_SmallPrice_NoThousandSeparator()
    {
        // Arrange
        var service = new WindowsToastNotificationService();
        decimal currentPrice = 999.99m;
        decimal thresholdPrice = 1000.00m;

        // Act
        string message = service.FormatAlertMessage(currentPrice, thresholdPrice, AlertDirection.Below);

        // Assert
        Assert.Equal("ETH 价格跌破 $999.99 (低于 $1,000.00)", message);
    }
}
