using EthPriceMonitor.Models;
using EthPriceMonitor.ViewModels;

namespace EthPriceMonitor.Tests.ViewModels;

public class MainViewModelTests
{
    private static TickerData CreateTicker(
        decimal lastPrice = 3123.45m,
        decimal priceChangePercent = 1.23m,
        decimal highPrice = 3200.00m,
        decimal lowPrice = 3050.00m)
    {
        return new TickerData
        {
            Symbol = "ETHUSDT",
            LastPrice = lastPrice,
            PriceChange = priceChangePercent * lastPrice / 100m,
            PriceChangePercent = priceChangePercent,
            HighPrice = highPrice,
            LowPrice = lowPrice,
            Volume = 10000m,
            QuoteVolume = 31000000m,
            EventTime = DateTime.UtcNow
        };
    }

    [Fact]
    public void UpdateFromTicker_SetsCurrentPrice()
    {
        var vm = new MainViewModel();
        var ticker = CreateTicker(lastPrice: 3123.45m);

        vm.UpdateFromTicker(ticker);

        Assert.Equal("$3,123.45", vm.CurrentPrice);
    }

    [Fact]
    public void UpdateFromTicker_PositiveChange_SetsIsPriceUpTrue()
    {
        var vm = new MainViewModel();
        var ticker = CreateTicker(priceChangePercent: 1.23m);

        vm.UpdateFromTicker(ticker);

        Assert.True(vm.IsPriceUp);
    }

    [Fact]
    public void UpdateFromTicker_NegativeChange_SetsIsPriceUpFalse()
    {
        var vm = new MainViewModel();
        var ticker = CreateTicker(priceChangePercent: -0.50m);

        vm.UpdateFromTicker(ticker);

        Assert.False(vm.IsPriceUp);
    }

    [Fact]
    public void UpdateFromTicker_FormatsPercentWithSign()
    {
        var vm = new MainViewModel();

        // Positive percent
        var positiveTicker = CreateTicker(priceChangePercent: 1.23m);
        vm.UpdateFromTicker(positiveTicker);
        Assert.Equal("+1.23%", vm.PriceChangePercent);

        // Negative percent
        var negativeTicker = CreateTicker(priceChangePercent: -0.50m);
        vm.UpdateFromTicker(negativeTicker);
        Assert.Equal("-0.50%", vm.PriceChangePercent);
    }

    [Fact]
    public void UpdateFromTicker_SetsHighAndLowPrice()
    {
        var vm = new MainViewModel();
        var ticker = CreateTicker(highPrice: 3200.00m, lowPrice: 3050.50m);

        vm.UpdateFromTicker(ticker);

        Assert.Equal("3200.00", vm.HighPrice);
        Assert.Equal("3050.50", vm.LowPrice);
    }

    [Fact]
    public void UpdateFromTicker_NullTicker_ThrowsArgumentNullException()
    {
        var vm = new MainViewModel();

        Assert.Throws<ArgumentNullException>(() => vm.UpdateFromTicker(null!));
    }

    [Fact]
    public void UpdateFromTicker_ZeroChange_SetsIsPriceUpTrue()
    {
        var vm = new MainViewModel();
        var ticker = CreateTicker(priceChangePercent: 0.00m);

        vm.UpdateFromTicker(ticker);

        Assert.True(vm.IsPriceUp);
        Assert.Equal("+0.00%", vm.PriceChangePercent);
    }

    [Fact]
    public void UpdateFromTicker_RaisesPropertyChangedEvents()
    {
        var vm = new MainViewModel();
        // Use negative change so IsPriceUp flips from default (true) to false,
        // ensuring PropertyChanged fires for all properties.
        var ticker = CreateTicker(priceChangePercent: -1.00m);

        var changedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        vm.UpdateFromTicker(ticker);

        Assert.Contains("CurrentPrice", changedProperties);
        Assert.Contains("IsPriceUp", changedProperties);
        Assert.Contains("PriceChangePercent", changedProperties);
        Assert.Contains("HighPrice", changedProperties);
        Assert.Contains("LowPrice", changedProperties);
    }
}
