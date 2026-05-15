using System.Globalization;
using System.Text.Json;
using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

/// <summary>
/// Parses Binance 24hrTicker JSON messages into TickerData objects.
/// Uses System.Text.Json (no third-party dependencies).
/// Binance sends prices as JSON strings (e.g., "c": "3123.45"), not numbers.
/// </summary>
public class BinanceTickerParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Parse a Binance 24hrTicker JSON string into TickerData.
    /// Returns null if JSON is invalid or cannot be parsed.
    /// </summary>
    public TickerData? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new TickerData
            {
                Symbol = root.TryGetProperty("s", out var s) ? s.GetString() ?? string.Empty : string.Empty,
                LastPrice = GetDecimalValue(root, "c"),
                PriceChange = GetDecimalValue(root, "p"),
                PriceChangePercent = GetDecimalValue(root, "P"),
                HighPrice = GetDecimalValue(root, "h"),
                LowPrice = GetDecimalValue(root, "l"),
                Volume = GetDecimalValue(root, "v"),
                QuoteVolume = GetDecimalValue(root, "q"),
                EventTime = root.TryGetProperty("E", out var E) 
                    ? DateTimeOffset.FromUnixTimeMilliseconds(E.GetInt64()).DateTime 
                    : DateTime.MinValue
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a decimal value from a JSON property. Handles both JSON number and JSON string formats.
    /// Binance sends prices as strings (e.g., "c": "3123.45"), while event time is a number.
    /// </summary>
    private static decimal GetDecimalValue(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            return 0m;

        if (element.ValueKind == JsonValueKind.Number)
            return element.GetDecimal();

        if (element.ValueKind == JsonValueKind.String)
        {
            var str = element.GetString();
            if (str != null && decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;
        }

        return 0m;
    }
}
