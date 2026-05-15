namespace EthPriceMonitor.Models;

/// <summary>
/// 价格预警方向
/// </summary>
public enum AlertDirection
{
    /// <summary>
    /// 价格高于阈值（突破）
    /// </summary>
    Above,

    /// <summary>
    /// 价格低于阈值（跌破）
    /// </summary>
    Below
}
