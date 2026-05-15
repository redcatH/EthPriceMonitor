using System.ComponentModel;
using System.Globalization;
using EthPriceMonitor.Models;
using EthPriceMonitor.Services;

namespace EthPriceMonitor.ViewModels;

/// <summary>
/// ViewModel for the floating ETH/USDT price window.
/// Binds to TickerData updates and exposes formatted display properties.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private string _currentPrice = "$0.00";
    private string _priceChangePercent = "0.00%";
    private bool _isPriceUp = true;
    private string _highPrice = "0.00";
    private string _lowPrice = "0.00";
    private ConnectionState _connectionStatus = ConnectionState.Disconnected;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Formatted current price, e.g. "$3,123.45"
    /// </summary>
    public string CurrentPrice
    {
        get => _currentPrice;
        private set => SetField(ref _currentPrice, value);
    }

    /// <summary>
    /// Formatted 24h change percentage with sign, e.g. "+1.23%" or "-0.50%"
    /// </summary>
    public string PriceChangePercent
    {
        get => _priceChangePercent;
        private set => SetField(ref _priceChangePercent, value);
    }

    /// <summary>
    /// True when price is up (green), false when down (red).
    /// </summary>
    public bool IsPriceUp
    {
        get => _isPriceUp;
        private set => SetField(ref _isPriceUp, value);
    }

    /// <summary>
    /// 24h high price, e.g. "3200.00"
    /// </summary>
    public string HighPrice
    {
        get => _highPrice;
        private set => SetField(ref _highPrice, value);
    }

    /// <summary>
    /// 24h low price, e.g. "3050.00"
    /// </summary>
    public string LowPrice
    {
        get => _lowPrice;
        private set => SetField(ref _lowPrice, value);
    }

    /// <summary>
    /// WebSocket connection state for status indicator.
    /// </summary>
    public ConnectionState ConnectionStatus
    {
        get => _connectionStatus;
        set => SetField(ref _connectionStatus, value);
    }

    /// <summary>
    /// Updates all display properties from a TickerData instance.
    /// </summary>
    public void UpdateFromTicker(TickerData ticker)
    {
        ArgumentNullException.ThrowIfNull(ticker);

        CurrentPrice = ticker.FormattedPrice;
        IsPriceUp = ticker.PriceChangePercent >= 0;
        PriceChangePercent = FormatPercent(ticker.PriceChangePercent);
        HighPrice = ticker.HighPrice.ToString("F2", CultureInfo.InvariantCulture);
        LowPrice = ticker.LowPrice.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a decimal percentage with sign prefix, e.g. "+1.23%" or "-0.50%".
    /// </summary>
    private static string FormatPercent(decimal percent)
    {
        var sign = percent >= 0 ? "+" : "";
        return $"{sign}{percent:F2}%";
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
