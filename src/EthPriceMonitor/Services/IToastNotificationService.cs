using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

/// <summary>
/// Toast 通知服务接口，用于发送 Windows Toast 通知
/// </summary>
public interface IToastNotificationService
{
    /// <summary>
    /// 显示价格预警通知
    /// </summary>
    /// <param name="currentPrice">当前价格</param>
    /// <param name="thresholdPrice">阈值价格</param>
    /// <param name="direction">预警方向（高于/低于）</param>
    void ShowPriceAlert(decimal currentPrice, decimal thresholdPrice, AlertDirection direction);

    /// <summary>
    /// 格式化预警消息文本（便于单元测试）
    /// </summary>
    /// <param name="currentPrice">当前价格</param>
    /// <param name="thresholdPrice">阈值价格</param>
    /// <param name="direction">预警方向</param>
    /// <returns>格式化后的消息字符串</returns>
    string FormatAlertMessage(decimal currentPrice, decimal thresholdPrice, AlertDirection direction);
}
