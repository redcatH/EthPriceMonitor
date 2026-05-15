using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

/// <summary>
/// Monitors ETH price via WebSocket and triggers Toast notifications
/// when price crosses configured thresholds.
/// Includes cooldown logic to prevent repeated alerts.
/// </summary>
public interface IAlertEngine
{
    /// <summary>
    /// Subscribe to IWebSocketService.TickerReceived and begin monitoring.
    /// </summary>
    void Start();

    /// <summary>
    /// Unsubscribe from IWebSocketService.TickerReceived and stop monitoring.
    /// </summary>
    void Stop();

    /// <summary>
    /// Update the list of active alert thresholds. Replaces any previously set thresholds.
    /// </summary>
    /// <param name="thresholds">The new set of alert thresholds to monitor.</param>
    void UpdateThresholds(IEnumerable<AlertThreshold> thresholds);
}
