using System.Windows;
using System.Windows.Threading;
using EthPriceMonitor.Services;
using EthPriceMonitor.ViewModels;
using EthPriceMonitor.Views;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;

namespace EthPriceMonitor;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private IWebSocketService? _webSocketService;
    private IAlertEngine? _alertEngine;
    private ThemeService? _themeService;
    private ISettingsService? _settingsService;
    private FloatingWindow? _floatingWindow;
    private MainViewModel? _mainViewModel;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handling
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Resolve core services
        _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        _webSocketService = _serviceProvider.GetRequiredService<IWebSocketService>();
        _alertEngine = _serviceProvider.GetRequiredService<IAlertEngine>();
        _themeService = _serviceProvider.GetRequiredService<ThemeService>();

        // Load settings
        var settings = await _settingsService.LoadSettingsAsync();

        // Apply theme based on system preference
        _themeService.ApplyTheme(_themeService.IsSystemDarkTheme());

        // Create FloatingWindow with MainViewModel
        _mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        _floatingWindow = new FloatingWindow { DataContext = _mainViewModel };
        MainWindow = _floatingWindow;

        // Initialize tray icon from resource dictionary
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");

        // Override TrayIcon DataContext with DI-resolved ViewModel
        var trayViewModel = _serviceProvider.GetRequiredService<TrayIconViewModel>();
        _trayIcon.DataContext = trayViewModel;

        // Wire MainViewModel to receive TickerReceived events
        _webSocketService.TickerReceived += OnTickerReceived;
        _webSocketService.ConnectionStateChanged += OnConnectionStateChanged;

        // Wire AlertEngine to receive TickerReceived events
        _alertEngine.UpdateThresholds(settings.AlertThresholds);
        _alertEngine.Start();

        // Start WebSocket service
        try
        {
            await _webSocketService.ConnectAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Log but don't crash — auto-reconnect will handle it
            System.Diagnostics.Debug.WriteLine($"WebSocket initial connection failed: {ex.Message}");
        }

        // Start ThemeService watching
        _themeService.StartWatching();

        // Show floating window if settings say so
        if (settings.ShowFloatingWindow)
        {
            _floatingWindow.Show();
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Stop AlertEngine
        _alertEngine?.Stop();

        // Stop WebSocket service
        if (_webSocketService is not null)
        {
            try
            {
                await _webSocketService.DisconnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebSocket disconnect error: {ex.Message}");
            }
        }

        // Stop ThemeService watching
        _themeService?.StopWatching();

        // Save settings
        if (_settingsService is not null)
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                await _settingsService.SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
            }
        }

        // Dispose tray icon
        _trayIcon?.Dispose();

        // Dispose DI container
        _serviceProvider?.Dispose();

        base.OnExit(e);
    }

    private void OnTickerReceived(object? sender, Models.TickerData ticker)
    {
        // Marshal to UI thread
        Dispatcher.BeginInvoke(() => _mainViewModel?.UpdateFromTicker(ticker));
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        // Marshal to UI thread
        Dispatcher.BeginInvoke(() =>
        {
            if (_mainViewModel is not null)
            {
                _mainViewModel.ConnectionStatus = state;
            }
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<BinanceTickerParser>();
        services.AddSingleton<IWebSocketService>(sp =>
        {
            var parser = sp.GetRequiredService<BinanceTickerParser>();
            var settingsService = sp.GetRequiredService<ISettingsService>();
            return new BinanceWebSocketService(
                parser,
                settingsService,
                "wss://data-stream.binance.vision/ws/ethusdt@ticker");
        });
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IToastNotificationService, WindowsToastNotificationService>();
        services.AddSingleton<IAlertEngine, AlertEngine>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<AutoStartService>();
        services.AddSingleton<IRegistryAccessor, RegistryAccessor>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<TrayIconViewModel>(sp => new TrayIconViewModel(sp));
    }

    #region Global Exception Handling

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unhandled UI exception: {e.Exception}");
        MessageBox.Show(
            $"发生意外错误:\n{e.Exception.Message}",
            "ETH Price Monitor",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled domain exception: {ex}");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    #endregion
}
