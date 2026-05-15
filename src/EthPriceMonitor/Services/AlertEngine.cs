using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

/// <summary>
/// Monitors ETH price via WebSocket and triggers Toast notifications
/// when price crosses configured thresholds.
/// Includes configurable cooldown logic to prevent repeated alerts,
/// and resets cooldown when price returns to the other side of a threshold.
/// </summary>
public class AlertEngine : IAlertEngine
{
    private readonly IWebSocketService _webSocketService;
    private readonly IToastNotificationService _toastService;
    private readonly int _cooldownMinutes;

    private List<AlertThreshold> _thresholds = new();
    private readonly Dictionary<Guid, DateTime> _lastTriggered = new();
    private readonly Dictionary<Guid, bool> _wasOnTriggeredSide = new();
    private readonly object _lock = new();

    public AlertEngine(
        IWebSocketService webSocketService,
        IToastNotificationService toastService,
        int cooldownMinutes = 5)
    {
        _webSocketService = webSocketService ?? throw new ArgumentNullException(nameof(webSocketService));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
        _cooldownMinutes = cooldownMinutes;
    }

    /// <inheritdoc />
    public void Start()
    {
        _webSocketService.TickerReceived += OnTickerReceived;
    }

    /// <inheritdoc />
    public void Stop()
    {
        _webSocketService.TickerReceived -= OnTickerReceived;
    }

    /// <inheritdoc />
    public void UpdateThresholds(IEnumerable<AlertThreshold> thresholds)
    {
        lock (_lock)
        {
            _thresholds = thresholds?.ToList() ?? new List<AlertThreshold>();
        }
    }

    private void OnTickerReceived(object? sender, TickerData ticker)
    {
        if (ticker is null) return;

        List<AlertThreshold> snapshot;
        lock (_lock)
        {
            snapshot = _thresholds.ToList();
        }

        foreach (var threshold in snapshot)
        {
            if (!threshold.IsEnabled)
                continue;

            bool isOnTriggeredSide = threshold.Direction switch
            {
                AlertDirection.Above => ticker.LastPrice >= threshold.Price,
                AlertDirection.Below => ticker.LastPrice <= threshold.Price,
                _ => false
            };

            bool wasOnTriggeredSide = _wasOnTriggeredSide.GetValueOrDefault(threshold.Id, false);

            // Track which side of the threshold we're on
            _wasOnTriggeredSide[threshold.Id] = isOnTriggeredSide;

            if (!isOnTriggeredSide)
            {
                // Price returned to the other side — reset cooldown
                _lastTriggered.Remove(threshold.Id);
                continue;
            }

            // Price is on the triggered side — check if this is a fresh cross
            if (wasOnTriggeredSide)
            {
                // Was already on this side, not a fresh cross — skip
                continue;
            }

            // Fresh cross! Check cooldown
            if (_lastTriggered.TryGetValue(threshold.Id, out var lastTime))
            {
                if (DateTime.UtcNow - lastTime < TimeSpan.FromMinutes(_cooldownMinutes))
                {
                    // Still within cooldown — skip
                    continue;
                }
            }

            // Fire the alert
            _lastTriggered[threshold.Id] = DateTime.UtcNow;
            _toastService.ShowPriceAlert(ticker.LastPrice, threshold.Price, threshold.Direction);
        }
    }
}
