using System.Net.WebSockets;
using System.Text;
using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

/// <summary>
/// Binance WebSocket service with full lifecycle management:
/// - Auto-reconnect with exponential backoff (5s→10s→20s→...→5min cap, ±20% jitter)
/// - 24h proactive reconnect (disconnect and reconnect every 23 hours)
/// - 30s stale data detection (reconnect if no message for 30 seconds)
/// - Endpoint fallback (primary → fallback on connection failure)
/// </summary>
public class BinanceWebSocketService : IWebSocketService
{
    private readonly Func<IWebSocketClient> _clientFactory;
    private readonly BinanceTickerParser _parser;
    private readonly ISettingsService? _settingsService;
    private readonly string _primaryEndpoint;
    private readonly string _fallbackEndpoint;
    private readonly int _staleTimeoutSeconds;
    private readonly double _reconnectIntervalHours;

    // Exponential backoff constants
    private const double InitialBackoffSeconds = 5.0;
    private const double MaxBackoffSeconds = 300.0; // 5 minutes
    private const double BackoffMultiplier = 2.0;
    private const double JitterRange = 0.2; // ±20%

    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _disposeCts = new();

    private ConnectionState _currentState = ConnectionState.Disconnected;
    private IWebSocketClient? _activeClient;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveLoopTask;
    private Task? _proactiveReconnectTask;
    private Task? _staleDetectionTask;
    private DateTime _lastMessageTime = DateTime.MinValue;
    private int _reconnectAttempt;
    private bool _isDisposed;
    private bool _isExplicitDisconnect;

    public event EventHandler<TickerData>? TickerReceived;
    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public ConnectionState CurrentState
    {
        get { lock (_stateLock) { return _currentState; } }
    }

    /// <summary>
    /// Creates a new BinanceWebSocketService with explicit endpoints.
    /// </summary>
    /// <param name="clientFactory">Factory to create WebSocket clients (for testability and reconnect)</param>
    /// <param name="parser">Binance ticker JSON parser</param>
    /// <param name="primaryEndpoint">Primary WebSocket endpoint URL</param>
    /// <param name="fallbackEndpoint">Fallback WebSocket endpoint URL</param>
    /// <param name="staleTimeoutSeconds">Seconds without a message before considering data stale (default: 30)</param>
    /// <param name="reconnectIntervalHours">Hours between proactive reconnects (default: 23)</param>
    public BinanceWebSocketService(
        Func<IWebSocketClient> clientFactory,
        BinanceTickerParser parser,
        string primaryEndpoint,
        string fallbackEndpoint,
        int staleTimeoutSeconds = 30,
        double reconnectIntervalHours = 23.0)
    {
        _clientFactory = clientFactory;
        _parser = parser;
        _primaryEndpoint = primaryEndpoint;
        _fallbackEndpoint = fallbackEndpoint;
        _staleTimeoutSeconds = staleTimeoutSeconds;
        _reconnectIntervalHours = reconnectIntervalHours;
    }

    /// <summary>
    /// Creates a new BinanceWebSocketService that resolves the primary endpoint
    /// from ISettingsService asynchronously at connection time.
    /// This avoids sync-over-async deadlock in DI registration.
    /// </summary>
    public BinanceWebSocketService(
        Func<IWebSocketClient> clientFactory,
        BinanceTickerParser parser,
        ISettingsService settingsService,
        string fallbackEndpoint,
        int staleTimeoutSeconds = 30,
        double reconnectIntervalHours = 23.0)
    {
        _clientFactory = clientFactory;
        _parser = parser;
        _settingsService = settingsService;
        _primaryEndpoint = string.Empty; // Resolved in TryConnectWithFallbackAsync
        _fallbackEndpoint = fallbackEndpoint;
        _staleTimeoutSeconds = staleTimeoutSeconds;
        _reconnectIntervalHours = reconnectIntervalHours;
    }

    /// <summary>
    /// Convenience constructor that uses WebSocketClientWrapper as the default client factory.
    /// </summary>
    public BinanceWebSocketService(
        BinanceTickerParser parser,
        string primaryEndpoint,
        string fallbackEndpoint,
        int staleTimeoutSeconds = 30,
        double reconnectIntervalHours = 23.0)
        : this(() => new WebSocketClientWrapper(), parser, primaryEndpoint, fallbackEndpoint,
            staleTimeoutSeconds, reconnectIntervalHours)
    {
    }

    /// <summary>
    /// Convenience constructor with ISettingsService, using WebSocketClientWrapper as the default client factory.
    /// </summary>
    public BinanceWebSocketService(
        BinanceTickerParser parser,
        ISettingsService settingsService,
        string fallbackEndpoint,
        int staleTimeoutSeconds = 30,
        double reconnectIntervalHours = 23.0)
        : this(() => new WebSocketClientWrapper(), parser, settingsService, fallbackEndpoint,
            staleTimeoutSeconds, reconnectIntervalHours)
    {
    }

    /// <summary>
    /// Calculates the reconnect delay for a given attempt number with exponential backoff and jitter.
    /// Public static method for testability.
    /// Expected sequence: 5s→10s→20s→40s→80s→160s→300s (capped at 5min), with ±20% jitter
    /// </summary>
    /// <param name="attempt">Zero-based attempt number</param>
    /// <returns>Delay with jitter applied</returns>
    public static TimeSpan CalculateReconnectDelay(int attempt)
    {
        var baseDelay = InitialBackoffSeconds * Math.Pow(BackoffMultiplier, attempt);
        baseDelay = Math.Min(baseDelay, MaxBackoffSeconds);

        // Apply ±20% jitter
        var jitterFactor = 1.0 + (Random.Shared.NextDouble() * 2 - 1) * JitterRange;
        var jitteredDelay = baseDelay * jitterFactor;

        return TimeSpan.FromSeconds(jitteredDelay);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (CurrentState == ConnectionState.Connected)
            return;

        _isExplicitDisconnect = false;
        SetState(ConnectionState.Connecting);
        _reconnectAttempt = 0;

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _disposeCts.Token);
        _connectionCts = linkedCts;

        try
        {
            await TryConnectWithFallbackAsync(linkedCts.Token);

            SetState(ConnectionState.Connected);
            _lastMessageTime = DateTime.UtcNow;

            // Start background tasks
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(linkedCts.Token), linkedCts.Token);
            _proactiveReconnectTask = Task.Run(() => ProactiveReconnectLoopAsync(linkedCts.Token), linkedCts.Token);
            _staleDetectionTask = Task.Run(() => StaleDetectionLoopAsync(linkedCts.Token), linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            SetState(ConnectionState.Disconnected);
        }
    }

    public async Task DisconnectAsync()
    {
        _isExplicitDisconnect = true;
        _connectionCts?.Cancel();

        try
        {
            if (_activeClient != null && _activeClient.State == WebSocketState.Open)
            {
                await _activeClient.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnect",
                    CancellationToken.None);
            }
        }
        catch
        {
            // Ignore close errors during disconnect
        }

        SetState(ConnectionState.Disconnected);
        _activeClient = null;
    }

    private async Task TryConnectWithFallbackAsync(CancellationToken cancellationToken)
    {
        // Resolve primary endpoint from settings if ISettingsService was injected
        var primaryEndpoint = _primaryEndpoint;
        if (_settingsService is not null)
        {
            var settings = await _settingsService.LoadSettingsAsync();
            primaryEndpoint = settings.WebSocketUrl;
        }

        var endpoints = new[] { primaryEndpoint, _fallbackEndpoint };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var uri = new Uri(endpoint);
                _activeClient = _clientFactory();
                await _activeClient.ConnectAsync(uri, cancellationToken);
                return; // Success
            }
            catch (WebSocketException)
            {
                // Try next endpoint
                continue;
            }
        }

        // All endpoints failed
        throw new WebSocketException("Failed to connect to both primary and fallback endpoints");
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        try
        {
            while (!cancellationToken.IsCancellationRequested && _activeClient != null)
            {
                var result = await _activeClient.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Server closed connection - trigger reconnect unless explicit disconnect
                    if (!_isExplicitDisconnect)
                    {
                        await HandleDisconnectionAsync("Server closed connection", cancellationToken);
                    }
                    return;
                }

                if (result.Count > 0)
                {
                    _lastMessageTime = DateTime.UtcNow;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var ticker = _parser.Parse(json);

                    if (ticker != null)
                    {
                        TickerReceived?.Invoke(this, ticker);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disconnect
        }
        catch (WebSocketException)
        {
            if (!_isExplicitDisconnect)
            {
                await HandleDisconnectionAsync("WebSocket error", cancellationToken);
            }
        }
    }

    private async Task ProactiveReconnectLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(_reconnectIntervalHours), cancellationToken);

                if (CurrentState == ConnectionState.Connected && !_isExplicitDisconnect)
                {
                    // Proactive reconnect: disconnect and reconnect before 24h limit
                    _isExplicitDisconnect = false;
                    _connectionCts?.Cancel();

                    try
                    {
                        if (_activeClient != null && _activeClient.State == WebSocketState.Open)
                        {
                            await _activeClient.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "Proactive reconnect",
                                CancellationToken.None);
                        }
                    }
                    catch
                    {
                        // Ignore close errors
                    }

                    await ReconnectAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disconnect
        }
    }

    private async Task StaleDetectionLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_staleTimeoutSeconds), cancellationToken);

                if (CurrentState == ConnectionState.Connected && !_isExplicitDisconnect)
                {
                    var timeSinceLastMessage = DateTime.UtcNow - _lastMessageTime;
                    if (timeSinceLastMessage > TimeSpan.FromSeconds(_staleTimeoutSeconds))
                    {
                        // Stale data detected - trigger reconnect
                        await HandleDisconnectionAsync("Stale data detected", cancellationToken);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during disconnect
        }
    }

    private async Task HandleDisconnectionAsync(string reason, CancellationToken cancellationToken)
    {
        var currentState = CurrentState;
        if (currentState == ConnectionState.Disconnected || currentState == ConnectionState.Reconnecting)
            return;

        SetState(ConnectionState.Reconnecting);
        await ReconnectAsync(cancellationToken);
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_isExplicitDisconnect)
        {
            var delay = CalculateReconnectDelay(_reconnectAttempt);
            await Task.Delay(delay, cancellationToken);

            try
            {
                SetState(ConnectionState.Connecting);
                await TryConnectWithFallbackAsync(cancellationToken);
                SetState(ConnectionState.Connected);
                _lastMessageTime = DateTime.UtcNow;
                _reconnectAttempt = 0;

                // Restart background tasks with new CTS
                var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, _disposeCts.Token);
                _connectionCts = linkedCts;

                _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(linkedCts.Token), linkedCts.Token);
                _proactiveReconnectTask = Task.Run(() => ProactiveReconnectLoopAsync(linkedCts.Token), linkedCts.Token);
                _staleDetectionTask = Task.Run(() => StaleDetectionLoopAsync(linkedCts.Token), linkedCts.Token);
                return;
            }
            catch (WebSocketException)
            {
                _reconnectAttempt++;
                // Continue retrying
            }
        }

        SetState(ConnectionState.Disconnected);
    }

    private void SetState(ConnectionState newState)
    {
        lock (_stateLock)
        {
            _currentState = newState;
        }
        ConnectionStateChanged?.Invoke(this, newState);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await DisconnectAsync();
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}