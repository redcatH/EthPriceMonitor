using EthPriceMonitor.Models;
using EthPriceMonitor.Services;
using Xunit;

namespace EthPriceMonitor.Tests.Services;

public class BinanceTickerParserTests
{
    private readonly BinanceTickerParser _parser = new();

    // Realistic Binance 24hrTicker JSON (stream format)
    private const string ValidTickerJson = """
        {
            "e": "24hrTicker",
            "E": 1715779200000,
            "s": "ETHUSDT",
            "p": "25.5000",
            "P": "1.23",
            "c": "3123.45",
            "h": "3200.00",
            "l": "3050.00",
            "v": "12345.678",
            "q": "38500000.00"
        }
        """;

    [Fact]
    public void ParseValidJson_ReturnsCorrectTickerData()
    {
        // Act
        var result = _parser.Parse(ValidTickerJson);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ETHUSDT", result.Symbol);
        Assert.Equal(3123.45m, result.LastPrice);
        Assert.Equal(25.5000m, result.PriceChange);
        Assert.Equal(1.23m, result.PriceChangePercent);
        Assert.Equal(3200.00m, result.HighPrice);
        Assert.Equal(3050.00m, result.LowPrice);
        Assert.Equal(12345.678m, result.Volume);
        Assert.Equal(38500000.00m, result.QuoteVolume);
        // E=1715779200000 ms → 2024-05-15 12:00:00 UTC
        Assert.Equal(2024, result.EventTime.Year);
        Assert.Equal(5, result.EventTime.Month);
        Assert.Equal(15, result.EventTime.Day);
    }

    [Fact]
    public void ParseInvalidJson_ReturnsNull()
    {
        // Arrange
        var malformedJson = "{ this is not valid json }}}";

        // Act
        var result = _parser.Parse(malformedJson);

        // Assert — must return null, not throw
        Assert.Null(result);
    }

    [Fact]
    public void ParseMissingFields_ReturnsPartialData()
    {
        // Arrange — JSON missing "h" (HighPrice), "q" (QuoteVolume), "p" (PriceChange)
        var partialJson = """
            {
                "e": "24hrTicker",
                "E": 1715779200000,
                "s": "ETHUSDT",
                "P": "1.23",
                "c": "3123.45",
                "l": "3050.00",
                "v": "12345.678"
            }
            """;

        // Act
        var result = _parser.Parse(partialJson);

        // Assert — should still parse available fields, missing ones default to 0
        Assert.NotNull(result);
        Assert.Equal("ETHUSDT", result.Symbol);
        Assert.Equal(3123.45m, result.LastPrice);
        Assert.Equal(1.23m, result.PriceChangePercent);
        Assert.Equal(0m, result.HighPrice);       // missing → default
        Assert.Equal(0m, result.QuoteVolume);      // missing → default
        Assert.Equal(0m, result.PriceChange);       // missing → default
        Assert.Equal(3050.00m, result.LowPrice);   // present
    }

    [Fact]
    public void FormatPrice_TwoDecimalPlaces()
    {
        // Arrange
        var ticker = new TickerData { LastPrice = 3123.4567m };

        // Act
        var formatted = ticker.FormattedPrice;

        // Assert — exactly 2 decimal places with $ prefix and thousands separator
        Assert.Equal("$3,123.46", formatted);
    }

    [Fact]
    public void FormatPrice_WholeNumber_StillShowsTwoDecimals()
    {
        // Arrange
        var ticker = new TickerData { LastPrice = 3000m };

        // Act
        var formatted = ticker.FormattedPrice;

        // Assert
        Assert.Equal("$3,000.00", formatted);
    }
}
