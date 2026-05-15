namespace EthPriceMonitor.Models;

/// <summary>
/// Price alert threshold configuration.
/// When ETH price crosses this threshold in the specified direction, an alert fires.
/// </summary>
public class AlertThreshold
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal Price { get; set; }
    public AlertDirection Direction { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Label { get; set; } = string.Empty;
}
