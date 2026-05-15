using System.Windows;
using System.Windows.Input;
using EthPriceMonitor.Services;
using EthPriceMonitor.Views;

namespace EthPriceMonitor.ViewModels;

public class TrayIconViewModel
{
    private readonly IServiceProvider? _serviceProvider;

    public ICommand ToggleFloatingWindowCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitApplicationCommand { get; }

    /// <summary>
    /// Parameterless constructor for XAML resource instantiation (fallback).
    /// </summary>
    public TrayIconViewModel()
    {
        ToggleFloatingWindowCommand = new RelayCommand(_ => ToggleFloatingWindow());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        ExitApplicationCommand = new RelayCommand(_ => ExitApplication());
    }

    /// <summary>
    /// DI constructor with service provider for creating settings window.
    /// </summary>
    public TrayIconViewModel(IServiceProvider serviceProvider) : this()
    {
        _serviceProvider = serviceProvider;
    }

    private void ToggleFloatingWindow()
    {
        if (Application.Current.MainWindow is { } window)
        {
            if (window.Visibility == Visibility.Visible)
            {
                window.Hide();
            }
            else
            {
                window.Show();
                window.Activate();
            }
        }
    }

    private void OpenSettings()
    {
        // If DI is available, create SettingsWindow with SettingsViewModel
        if (_serviceProvider is not null)
        {
            var settingsService = _serviceProvider.GetService(typeof(ISettingsService)) as ISettingsService;
            var alertEngine = _serviceProvider.GetService(typeof(IAlertEngine)) as IAlertEngine;
            var autoStartService = _serviceProvider.GetService(typeof(AutoStartService)) as AutoStartService;

            if (settingsService is not null)
            {
                var settingsViewModel = new SettingsViewModel(settingsService);
                var settingsWindow = new SettingsWindow(settingsViewModel);

                // Load settings into the ViewModel
                settingsViewModel.LoadSettingsAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load settings: {t.Exception}");
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());

                // When settings window closes, apply changes
                settingsWindow.Closed += (s, e) =>
                {
                    if (settingsWindow.DialogResult == true)
                    {
                        // Update alert thresholds in the engine
                        alertEngine?.UpdateThresholds(settingsViewModel.AlertThresholds);
                    }
                };

                settingsWindow.Show();
                return;
            }
        }

        // Fallback: just show the main window
        if (Application.Current.MainWindow is { } window && window.Visibility != Visibility.Visible)
        {
            window.Show();
            window.Activate();
        }
    }

    private void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    private class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

        public void Execute(object? parameter) => _execute(parameter);
    }
}
