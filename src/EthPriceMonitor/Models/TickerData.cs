namespace EthPriceMonitor.Models;

/// <summary>
/// Maps Binance 24hrTicker WebSocket event fields to strongly-typed properties.
/// Field mapping: s→Symbol, c→LastPrice, p→PriceChange, P→PriceChangePercent,
/// h→HighPrice, l→LowPrice, v→Volume, q→QuoteVolume, E→EventTime (ms→DateTime)
/// </summary>
public class TickerData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal PriceChange { get; set; }
    public decimal PriceChangePercent { get; set; }
    public decimal HighPrice { get; set; }
    public decimal LowPrice { get; set; }
    public decimal Volume { get; set; }
    public decimal QuoteVolume { get; set; }
    public DateTime EventTime { get; set; }

    /// <summary>
    /// Formatted price with $ prefix, thousands separator, and 2 decimal places.
    /// e.g. "$3,123.45"
    /// </summary>
    public string FormattedPrice => LastPrice.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
