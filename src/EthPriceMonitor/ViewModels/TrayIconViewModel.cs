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
    public ICommand UnpinWindowCommand { get; }

    /// <summary>
    /// Parameterless constructor for XAML resource instantiation (fallback).
    /// </summary>
    public TrayIconViewModel()
    {
        ToggleFloatingWindowCommand = new RelayCommand(_ => ToggleFloatingWindow());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        ExitApplicationCommand = new RelayCommand(_ => ExitApplication());
        UnpinWindowCommand = new RelayCommand(_ => UnpinWindow(), _ => CanUnpinWindow());
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

                // Show as dialog and apply all changes on save
                var result = settingsWindow.ShowDialog();
                if (result == true)
                {
                    // Update alert thresholds in the engine
                    alertEngine?.UpdateThresholds(settingsViewModel.AlertThresholds);

                    // Apply auto-start setting
                    if (autoStartService is not null)
                    {
                        if (settingsViewModel.AutoStartWithWindows && !autoStartService.IsEnabled)
                            autoStartService.Enable();
                        else if (!settingsViewModel.AutoStartWithWindows && autoStartService.IsEnabled)
                            autoStartService.Disable();
                    }

                    // Apply floating window visibility, opacity, and topmost
                    if (Application.Current.MainWindow is Window mainWindow)
                    {
                        mainWindow.Topmost = settingsViewModel.WindowTopmost;
                        mainWindow.Opacity = settingsViewModel.WindowOpacity;

                        if (settingsViewModel.ShowFloatingWindow && mainWindow.Visibility != Visibility.Visible)
                            mainWindow.Show();
                        else if (!settingsViewModel.ShowFloatingWindow && mainWindow.Visibility == Visibility.Visible)
                            mainWindow.Hide();
                    }
                }
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

    /// <summary>
    /// Unpins the floating window (disables click-through mode).
    /// Escape hatch when the window is pinned and the Pin button is also click-through.
    /// </summary>
    private void UnpinWindow()
    {
        if (Application.Current.MainWindow is { DataContext: MainViewModel vm } && vm.IsClickThrough)
        {
            vm.IsClickThrough = false;
        }
    }

    private bool CanUnpinWindow()
    {
        return Application.Current.MainWindow is { DataContext: MainViewModel vm } && vm.IsClickThrough;
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
