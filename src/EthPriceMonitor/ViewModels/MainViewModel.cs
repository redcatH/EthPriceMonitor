using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
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
    private double _windowOpacity = 1.0;
    private bool _windowTopmost = true;
    private bool _isClickThrough;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when click-through mode should be toggled at the View level.
    /// The View (FloatingWindow) subscribes to this to call Win32 API.
    /// </summary>
    public event Action<bool>? ClickThroughChanged;

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

    public double WindowOpacity
    {
        get => _windowOpacity;
        set => SetField(ref _windowOpacity, Math.Clamp(value, 0.2, 1.0));
    }

    public bool WindowTopmost
    {
        get => _windowTopmost;
        set => SetField(ref _windowTopmost, value);
    }

    /// <summary>
    /// Whether the window is in click-through mode (pinned).
    /// When true, mouse clicks pass through to windows below.
    /// </summary>
    public bool IsClickThrough
    {
        get => _isClickThrough;
        set
        {
            if (SetField(ref _isClickThrough, value))
            {
                ClickThroughChanged?.Invoke(value);
            }
        }
    }

    /// <summary>
    /// Command to toggle click-through (pin/unpin) mode.
    /// </summary>
    public ICommand ToggleClickThroughCommand { get; }

    public MainViewModel()
    {
        ToggleClickThroughCommand = new SimpleCommand(() => IsClickThrough = !IsClickThrough);
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

/// <summary>
/// Minimal ICommand implementation for parameterless actions.
/// </summary>
internal class SimpleCommand : ICommand
{
    private readonly Action _execute;

    public SimpleCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
