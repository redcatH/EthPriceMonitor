using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

/// <summary>
/// WebSocket service interface for managing Binance streaming connections.
/// </summary>
public interface IWebSocketService
{
    /// <summary>
    /// Connect to the Binance WebSocket stream.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Disconnect from the Binance WebSocket stream.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Raised when a new ticker data message is received.
    /// </summary>
    event EventHandler<TickerData>? TickerReceived;

    /// <summary>
    /// Raised when the connection state changes.
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState CurrentState { get; }
}
