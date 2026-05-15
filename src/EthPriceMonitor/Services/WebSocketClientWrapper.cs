using System.Net.WebSockets;

namespace EthPriceMonitor.Services;

/// <summary>
/// Default implementation of IWebSocketClient wrapping System.Net.WebSockets.ClientWebSocket.
/// </summary>
public class WebSocketClientWrapper : IWebSocketClient
{
    private readonly ClientWebSocket _client = new();

    public WebSocketState State => _client.State;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        => _client.ConnectAsync(uri, cancellationToken);

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => _client.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        => _client.ReceiveAsync(buffer, cancellationToken);

    public ValueTask SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        => new(_client.SendAsync(buffer, messageType, endOfMessage, cancellationToken));

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
