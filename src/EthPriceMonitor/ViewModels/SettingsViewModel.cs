using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using EthPriceMonitor.Models;
using EthPriceMonitor.Services;

namespace EthPriceMonitor.ViewModels;

/// <summary>
/// ViewModel for the Settings window.
/// Manages alert thresholds and application preferences.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private AlertThreshold? _selectedThreshold;
    private bool _autoStartWithWindows;
    private bool _showFloatingWindow;
    private double _windowOpacity = 1.0;
    private bool _windowTopmost = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Collection of alert thresholds bound to the UI.
    /// </summary>
    public ObservableCollection<AlertThreshold> AlertThresholds { get; } = new();

    /// <summary>
    /// Whether the app starts automatically with Windows.
    /// </summary>
    public bool AutoStartWithWindows
    {
        get => _autoStartWithWindows;
        set => SetField(ref _autoStartWithWindows, value);
    }

    /// <summary>
    /// Whether the floating price window is shown.
    /// </summary>
    public bool ShowFloatingWindow
    {
        get => _showFloatingWindow;
        set => SetField(ref _showFloatingWindow, value);
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
    /// Currently selected threshold in the list (for delete operations).
    /// </summary>
    public AlertThreshold? SelectedThreshold
    {
        get => _selectedThreshold;
        set => SetField(ref _selectedThreshold, value);
    }

    /// <summary>
    /// Command to add a new alert threshold.
    /// </summary>
    public ICommand AddThresholdCommand { get; }

    /// <summary>
    /// Command to remove the selected alert threshold.
    /// </summary>
    public ICommand RemoveThresholdCommand { get; }

    /// <summary>
    /// Command to save settings and close.
    /// </summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Command to cancel and close without saving.
    /// </summary>
    public ICommand CancelCommand { get; }

    public SettingsViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        AddThresholdCommand = new RelayCommand(ExecuteAddThreshold);
        RemoveThresholdCommand = new RelayCommand(ExecuteRemoveThreshold, () => SelectedThreshold != null);
        SaveCommand = new AsyncRelayCommand(ExecuteSaveAsync);
        CancelCommand = new RelayCommand(() => { });
    }

    /// <summary>
    /// Loads settings from the service into the ViewModel properties.
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        AlertThresholds.Clear();
        foreach (var threshold in settings.AlertThresholds)
        {
            AlertThresholds.Add(threshold);
        }

        AutoStartWithWindows = settings.AutoStartWithWindows;
        ShowFloatingWindow = settings.ShowFloatingWindow;
        WindowOpacity = settings.WindowOpacity;
        WindowTopmost = settings.WindowTopmost;
    }

    /// <summary>
    /// Adds a new alert threshold with the specified price and direction.
    /// Price must be greater than 0.
    /// </summary>
    public void AddThreshold(decimal price, AlertDirection direction)
    {
        if (price <= 0)
            return;

        var threshold = new AlertThreshold
        {
            Price = price,
            Direction = direction,
            IsEnabled = true,
            Label = direction == AlertDirection.Above
                ? $"突破 ${price:F2}"
                : $"跌破 ${price:F2}"
        };

        AlertThresholds.Add(threshold);
    }

    /// <summary>
    /// Removes the specified alert threshold from the collection.
    /// </summary>
    public void RemoveThreshold(AlertThreshold threshold)
    {
        ArgumentNullException.ThrowIfNull(threshold);
        AlertThresholds.Remove(threshold);
    }

    private void ExecuteAddThreshold()
    {
        // Add a default threshold — the user can edit it in the UI
        AddThreshold(3000m, AlertDirection.Above);
    }

    private void ExecuteRemoveThreshold()
    {
        if (SelectedThreshold != null)
        {
            RemoveThreshold(SelectedThreshold);
        }
    }

    internal async Task ExecuteSaveAsync()
    {
        var settings = new AppSettings
        {
            AlertThresholds = AlertThresholds.ToList(),
            AutoStartWithWindows = AutoStartWithWindows,
            ShowFloatingWindow = ShowFloatingWindow,
            WindowOpacity = WindowOpacity,
            WindowTopmost = WindowTopmost
        };

        await _settingsService.SaveSettingsAsync(settings);
    }

    protected void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

/// <summary>
/// Simple synchronous ICommand implementation.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Async ICommand implementation for save operations.
/// </summary>
public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter)
    {
        await _execute();
    }
}
