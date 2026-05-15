using System.Net.WebSockets;
using System.Text;
using EthPriceMonitor.Models;
using EthPriceMonitor.Services;
using Moq;

namespace EthPriceMonitor.Tests.Services;

/// <summary>
/// Testable subclass that exposes the receive loop for direct testing.
/// </summary>
public class TestableBinanceWebSocketService : BinanceWebSocketService
{
    public TestableBinanceWebSocketService(
        Func<IWebSocketClient> clientFactory,
        BinanceTickerParser parser,
        string primaryEndpoint,
        string fallbackEndpoint,
        int staleTimeoutSeconds = 30,
        double reconnectIntervalHours = 23.0)
        : base(clientFactory, parser, primaryEndpoint, fallbackEndpoint, staleTimeoutSeconds, reconnectIntervalHours)
    {
    }

    // Removed unused testable subclass
}

public class BinanceWebSocketServiceTests
{
    private readonly Mock<IWebSocketClient> _mockWebSocket;
    private readonly BinanceTickerParser _parser;
    private readonly BinanceWebSocketService _service;

    private const string PrimaryEndpoint = "wss://data-stream.binance.com/ws/ethusdt@ticker";
    private const string FallbackEndpoint = "wss://data-stream.binance.vision/ws/ethusdt@ticker";

    public BinanceWebSocketServiceTests()
    {
        _mockWebSocket = new Mock<IWebSocketClient>();
        _parser = new BinanceTickerParser();
        _service = new BinanceWebSocketService(
            () => _mockWebSocket.Object,
            _parser,
            PrimaryEndpoint,
            FallbackEndpoint);
    }

    private static WebSocketReceiveResult TextResult(int count, bool endOfMessage = true)
        => new(count, WebSocketMessageType.Text, endOfMessage);

    private static WebSocketReceiveResult CloseResult()
        => new(0, WebSocketMessageType.Close, true);

    private const string SampleTickerJson =
        """{"e":"24hrTicker","E":1700000000000,"s":"ETHUSDT","c":"3123.45","p":"50.00","P":"1.63","h":"3200.00","l":"3050.00","v":"15000.00","q":"46850000.00","b":"3123.40","a":"3123.50"}""";

    private void SetupMockConnectedWithBlockingReceive()
    {
        _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);
        _mockWebSocket
            .Setup(ws => ws.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockWebSocket
            .Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return CloseResult();
            });
    }

    [Fact]
    public async Task ConnectAsync_SuccessfulConnection_RaisesConnectedEvent()
    {
        var stateChanges = new List<ConnectionState>();
        _service.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        SetupMockConnectedWithBlockingReceive();

        await _service.ConnectAsync(CancellationToken.None);

        Assert.Contains(ConnectionState.Connecting, stateChanges);
        Assert.Contains(ConnectionState.Connected, stateChanges);
        Assert.Equal(ConnectionState.Connected, _service.CurrentState);

        await _service.DisconnectAsync();
    }

    [Fact]
    public async Task ReceiveTickerData_RaisesTickerReceivedEvent()
    {
        // Arrange - test the parser + event raising by directly invoking the parser
        // and verifying the event mechanism works, since mocking ArraySegment<byte>
        // buffer writes through Moq is unreliable (struct copy semantics).
        var receivedTickers = new List<TickerData>();
        _service.TickerReceived += (_, ticker) => receivedTickers.Add(ticker);

        // Test the parser directly with the sample JSON
        var ticker = _parser.Parse(SampleTickerJson);

        // Simulate what the receive loop does
        Assert.NotNull(ticker);
        Assert.Equal("ETHUSDT", ticker.Symbol);
        Assert.Equal(3123.45m, ticker.LastPrice);

        // Now test the full pipeline with a mock that writes data into the buffer
        // We use a different approach: instead of trying to write through Moq's buffer,
        // we create a mock IWebSocketClient implementation that directly writes to the buffer
        var mockClient = new MockWebSocketClient(SampleTickerJson);
        var serviceWithMock = new BinanceWebSocketService(
            () => mockClient,
            _parser,
            PrimaryEndpoint,
            FallbackEndpoint);

        serviceWithMock.TickerReceived += (_, t) => receivedTickers.Add(t);

        await serviceWithMock.ConnectAsync(CancellationToken.None);
        await Task.Delay(500);

        Assert.True(receivedTickers.Count >= 1, $"Expected at least 1 ticker, got {receivedTickers.Count}");
        Assert.Equal("ETHUSDT", receivedTickers[0].Symbol);
        Assert.Equal(3123.45m, receivedTickers[0].LastPrice);

        await serviceWithMock.DisconnectAsync();
    }

    [Fact]
    public async Task DisconnectAsync_ClosesConnection_RaisesDisconnectedEvent()
    {
        var stateChanges = new List<ConnectionState>();
        _service.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        SetupMockConnectedWithBlockingReceive();
        _mockWebSocket
            .Setup(ws => ws.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.ConnectAsync(CancellationToken.None);
        await Task.Delay(100);

        await _service.DisconnectAsync();
        await Task.Delay(100);

        Assert.Contains(ConnectionState.Disconnected, stateChanges);
        Assert.Equal(ConnectionState.Disconnected, _service.CurrentState);
    }

    [Fact]
    public void Reconnect_ExponentialBackoff_CalculatesCorrectDelay()
    {
        var expectedBaseDelays = new[] { 5, 10, 20, 40, 80, 160, 300, 300 };

        for (int attempt = 0; attempt < expectedBaseDelays.Length; attempt++)
        {
            var delay = BinanceWebSocketService.CalculateReconnectDelay(attempt);
            var expectedBase = TimeSpan.FromSeconds(expectedBaseDelays[attempt]);
            var jitterMin = expectedBase * 0.8;
            var jitterMax = expectedBase * 1.2;

            Assert.InRange(delay, jitterMin, jitterMax);
        }
    }

    [Fact]
    public async Task StaleDataDetection_NoMessageFor30Seconds_TriggersReconnect()
    {
        var mockWs = new Mock<IWebSocketClient>();
        var shortStaleService = new BinanceWebSocketService(
            () => mockWs.Object,
            _parser,
            PrimaryEndpoint,
            FallbackEndpoint,
            staleTimeoutSeconds: 1,
            reconnectIntervalHours: 999);

        var stateChanges = new List<ConnectionState>();
        shortStaleService.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        mockWs.Setup(ws => ws.State).Returns(WebSocketState.Open);
        mockWs
            .Setup(ws => ws.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockWs
            .Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return CloseResult();
            });

        await shortStaleService.ConnectAsync(CancellationToken.None);
        await Task.Delay(3000);

        Assert.Contains(ConnectionState.Reconnecting, stateChanges);

        await shortStaleService.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_ConnectionFails_TriesFallbackEndpoint()
    {
        var stateChanges = new List<ConnectionState>();
        _service.ConnectionStateChanged += (_, state) => stateChanges.Add(state);

        var connectUris = new List<Uri>();
        _mockWebSocket.Setup(ws => ws.State).Returns(WebSocketState.Open);

        _mockWebSocket
            .Setup(ws => ws.ConnectAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .Callback<Uri, CancellationToken>((uri, ct) => connectUris.Add(uri))
            .Returns((Uri uri, CancellationToken ct) =>
            {
                if (uri.Host == "data-stream.binance.com")
                    throw new WebSocketException("Connection refused");
                return Task.CompletedTask;
            });

        _mockWebSocket
            .Setup(ws => ws.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(async (ArraySegment<byte> buffer, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return CloseResult();
            });

        await _service.ConnectAsync(CancellationToken.None);
        await Task.Delay(200);

        Assert.True(connectUris.Count >= 2, $"Expected at least 2 connect attempts, got {connectUris.Count}");
        Assert.Equal("data-stream.binance.com", connectUris[0].Host);
        Assert.Equal("data-stream.binance.vision", connectUris[1].Host);
        Assert.Equal(ConnectionState.Connected, _service.CurrentState);

        await _service.DisconnectAsync();
    }
}

/// <summary>
/// A real (non-mock) IWebSocketClient implementation for testing.
/// Sends a predefined JSON message on first ReceiveAsync, then blocks.
/// </summary>
internal class MockWebSocketClient : IWebSocketClient
{
    private readonly string _jsonMessage;
    private int _receiveCallCount;
    private WebSocketState _state = WebSocketState.None;

    public MockWebSocketClient(string jsonMessage)
    {
        _jsonMessage = jsonMessage;
    }

    public WebSocketState State => _state;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Open;
        return Task.CompletedTask;
    }

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _state = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        _receiveCallCount++;
        if (_receiveCallCount == 1)
        {
            var bytes = Encoding.UTF8.GetBytes(_jsonMessage);
            bytes.AsSpan().CopyTo(buffer);
            return Task.FromResult(new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
        }

        // Block subsequent calls until cancelled
        var tcs = new TaskCompletionSource<WebSocketReceiveResult>();
        cancellationToken.Register(() => tcs.TrySetResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true)));
        return tcs.Task;
    }

    public ValueTask SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _state = WebSocketState.Closed;
        return ValueTask.CompletedTask;
    }
}
