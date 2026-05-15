namespace EthPriceMonitor.Models;

public class AppSettings
{
    public List<AlertThreshold> AlertThresholds { get; set; } = new();
    public bool AutoStartWithWindows { get; set; }
    public string WebSocketUrl { get; set; } = "wss://data-stream.binance.com/ws/ethusdt@ticker";
    public bool ShowFloatingWindow { get; set; } = true;
    public double WindowOpacity { get; set; } = 1.0;
    public bool WindowTopmost { get; set; } = true;
}
