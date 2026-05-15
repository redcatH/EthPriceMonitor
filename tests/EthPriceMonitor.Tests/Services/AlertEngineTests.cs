using EthPriceMonitor.Models;
using EthPriceMonitor.Services;
using Moq;

namespace EthPriceMonitor.Tests.Services;

public class AlertEngineTests
{
    private readonly Mock<IWebSocketService> _mockWebSocket;
    private readonly Mock<IToastNotificationService> _mockToast;
    private readonly AlertEngine _engine;
    private const int CooldownMinutes = 5;

    public AlertEngineTests()
    {
        _mockWebSocket = new Mock<IWebSocketService>();
        _mockToast = new Mock<IToastNotificationService>();
        _engine = new AlertEngine(_mockWebSocket.Object, _mockToast.Object, cooldownMinutes: CooldownMinutes);
    }

    /// <summary>
    /// Helper: simulate a TickerReceived event from the WebSocket service.
    /// </summary>
    private void RaiseTickerReceived(decimal lastPrice)
    {
        var ticker = new TickerData { LastPrice = lastPrice };
        _mockWebSocket.Raise(ws => ws.TickerReceived += null, _mockWebSocket.Object, ticker);
    }

    private static AlertThreshold MakeThreshold(
        decimal price,
        AlertDirection direction,
        bool isEnabled = true,
        Guid? id = null)
    {
        return new AlertThreshold
        {
            Id = id ?? Guid.NewGuid(),
            Price = price,
            Direction = direction,
            IsEnabled = isEnabled,
            Label = $"Test {direction} {price}"
        };
    }

    // ─── Test 1: Price crosses above threshold → triggers alert ───

    [Fact]
    public void PriceCrossesAboveThreshold_TriggersAlert()
    {
        // Arrange
        var threshold = MakeThreshold(3000m, AlertDirection.Above);
        _engine.UpdateThresholds(new[] { threshold });
        _engine.Start();

        // Price below threshold — no alert
        RaiseTickerReceived(2900m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Never);

        // Price crosses above — alert fires
        RaiseTickerReceived(3100m);

        _mockToast.Verify(
            t => t.ShowPriceAlert(3100m, 3000m, AlertDirection.Above),
            Times.Once);
    }

    // ─── Test 2: Price crosses below threshold → triggers alert ───

    [Fact]
    public void PriceCrossesBelowThreshold_TriggersAlert()
    {
        // Arrange
        var threshold = MakeThreshold(3000m, AlertDirection.Below);
        _engine.UpdateThresholds(new[] { threshold });
        _engine.Start();

        // Price above threshold — no alert
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Never);

        // Price crosses below — alert fires
        RaiseTickerReceived(2900m);

        _mockToast.Verify(
            t => t.ShowPriceAlert(2900m, 3000m, AlertDirection.Below),
            Times.Once);
    }

    // ─── Test 3: Price stays on same side → no alert ───

    [Fact]
    public void PriceDoesNotCrossThreshold_NoAlert()
    {
        // Arrange
        var threshold = MakeThreshold(3000m, AlertDirection.Above);
        _engine.UpdateThresholds(new[] { threshold });
        _engine.Start();

        // Price stays above threshold for multiple ticks
        RaiseTickerReceived(3100m); // first cross — triggers
        RaiseTickerReceived(3200m); // still above — no additional
        RaiseTickerReceived(3050m); // still above — no additional

        // Only one call from the initial cross
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Once);
    }

    // ─── Test 4: Disabled threshold does not trigger alert ───

    [Fact]
    public void DisabledThreshold_DoesNotTriggerAlert()
    {
        // Arrange
        var threshold = MakeThreshold(3000m, AlertDirection.Above, isEnabled: false);
        _engine.UpdateThresholds(new[] { threshold });
        _engine.Start();

        // Price crosses above disabled threshold
        RaiseTickerReceived(3100m);

        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Never);
    }

    // ─── Test 5: Cooldown prevents repeated alert within 5 minutes ───

    [Fact]
    public void CooldownPreventsRepeatedAlert()
    {
        // Arrange — use a short cooldown for testing
        var engine = new AlertEngine(
            _mockWebSocket.Object, _mockToast.Object, cooldownMinutes: CooldownMinutes);
        var threshold = MakeThreshold(3000m, AlertDirection.Above);
        engine.UpdateThresholds(new[] { threshold });
        engine.Start();

        // First cross — triggers
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(3100m, 3000m, AlertDirection.Above),
            Times.Once);

        // Second tick still above — suppressed by cooldown
        RaiseTickerReceived(3200m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Once); // still only 1 call total
    }

    // ─── Test 6: Price returns and recrosses after cooldown → triggers again ───

    [Fact]
    public void PriceReturnsAndRecrosses_TriggersAgain()
    {
        // Arrange — use 0-minute cooldown so cooldown resets when price returns
        var engine = new AlertEngine(
            _mockWebSocket.Object, _mockToast.Object, cooldownMinutes: 0);
        var threshold = MakeThreshold(3000m, AlertDirection.Above);
        engine.UpdateThresholds(new[] { threshold });
        engine.Start();

        // Cross above — triggers
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(3100m, 3000m, AlertDirection.Above),
            Times.Once);

        // Price returns below — cooldown resets
        RaiseTickerReceived(2900m);

        // Cross above again — triggers again
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(3100m, 3000m, AlertDirection.Above),
            Times.Exactly(2));
    }

    // ─── Test 7: Stop unsubscribes from TickerReceived ───

    [Fact]
    public void Stop_UnsubscribesFromTickerReceived()
    {
        var threshold = MakeThreshold(3000m, AlertDirection.Above);
        _engine.UpdateThresholds(new[] { threshold });
        _engine.Start();

        // Cross above while running
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Once);

        // Stop the engine
        _engine.Stop();

        // Raise another tick — should NOT trigger
        RaiseTickerReceived(3200m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<AlertDirection>()),
            Times.Once); // still only 1
    }

    // ─── Test 8: UpdateThresholds replaces existing thresholds ───

    [Fact]
    public void UpdateThresholds_ReplacesExistingThresholds()
    {
        var threshold1 = MakeThreshold(3000m, AlertDirection.Above);
        _engine.UpdateThresholds(new[] { threshold1 });
        _engine.Start();

        // Cross above 3000 — triggers
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), 3000m, AlertDirection.Above),
            Times.Once);

        // Replace with new threshold at 4000
        var threshold2 = MakeThreshold(4000m, AlertDirection.Above);
        _engine.UpdateThresholds(new[] { threshold2 });

        // Price at 3100 — below new threshold, no alert
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(It.IsAny<decimal>(), 4000m, AlertDirection.Above),
            Times.Never);
    }

    // ─── Test 9: Exact threshold price triggers alert (>= for Above, <= for Below) ───

    [Fact]
    public void ExactThresholdPrice_TriggersAlert()
    {
        var aboveThreshold = MakeThreshold(3000m, AlertDirection.Above);
        var belowThreshold = MakeThreshold(2000m, AlertDirection.Below);
        _engine.UpdateThresholds(new[] { aboveThreshold, belowThreshold });
        _engine.Start();

        // Price exactly at 3000 — Above threshold triggers (>=)
        RaiseTickerReceived(3000m);

        _mockToast.Verify(
            t => t.ShowPriceAlert(3000m, 3000m, AlertDirection.Above),
            Times.Once);
    }

    // ─── Test 10: Multiple thresholds fire independently ───

    [Fact]
    public void MultipleThresholds_FireIndependently()
    {
        var above3000 = MakeThreshold(3000m, AlertDirection.Above);
        var below2500 = MakeThreshold(2500m, AlertDirection.Below);
        _engine.UpdateThresholds(new[] { above3000, below2500 });
        _engine.Start();

        // Price crosses above 3000
        RaiseTickerReceived(3100m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(3100m, 3000m, AlertDirection.Above),
            Times.Once);

        // Price drops below 2500
        RaiseTickerReceived(2400m);
        _mockToast.Verify(
            t => t.ShowPriceAlert(2400m, 2500m, AlertDirection.Below),
            Times.Once);
    }
}
