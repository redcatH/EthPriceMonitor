using EthPriceMonitor.Models;
using EthPriceMonitor.Services;

namespace EthPriceMonitor.Tests.Services;

public class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonSettingsService _service;

    public JsonSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"EthPriceMonitor_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new JsonSettingsService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_SettingsPersistCorrectly()
    {
        // Arrange
        var settings = new AppSettings
        {
            AutoStartWithWindows = true,
            WebSocketUrl = "wss://data-stream.binance.com/ws/btcusdt@ticker",
            ShowFloatingWindow = false,
            AlertThresholds = new List<AlertThreshold>
            {
                new() { Id = Guid.NewGuid(), Price = 3000m, Direction = AlertDirection.Above, IsEnabled = true, Label = "ETH above 3000" }
            }
        };

        // Act
        await _service.SaveSettingsAsync(settings);
        var loaded = await _service.LoadSettingsAsync();

        // Assert
        Assert.Equal(settings.AutoStartWithWindows, loaded.AutoStartWithWindows);
        Assert.Equal(settings.WebSocketUrl, loaded.WebSocketUrl);
        Assert.Equal(settings.ShowFloatingWindow, loaded.ShowFloatingWindow);
        Assert.Single(loaded.AlertThresholds);
        Assert.Equal(settings.AlertThresholds[0].Price, loaded.AlertThresholds[0].Price);
        Assert.Equal(settings.AlertThresholds[0].Direction, loaded.AlertThresholds[0].Direction);
        Assert.Equal(settings.AlertThresholds[0].Label, loaded.AlertThresholds[0].Label);
    }

    [Fact]
    public async Task LoadDefaultSettings_WhenFileDoesNotExist()
    {
        // Act — no file has been saved yet
        var settings = await _service.LoadSettingsAsync();

        // Assert — should return default AppSettings
        Assert.NotNull(settings);
        Assert.Empty(settings.AlertThresholds);
        Assert.False(settings.AutoStartWithWindows);
        Assert.Equal("wss://data-stream.binance.com/ws/ethusdt@ticker", settings.WebSocketUrl);
        Assert.True(settings.ShowFloatingWindow);
    }

    [Fact]
    public async Task AddAlertThreshold_SavesAndLoadsCorrectly()
    {
        // Arrange — start with default settings
        var settings = await _service.LoadSettingsAsync();
        var threshold = new AlertThreshold
        {
            Id = Guid.NewGuid(),
            Price = 2500m,
            Direction = AlertDirection.Below,
            IsEnabled = true,
            Label = "ETH below 2500"
        };
        settings.AlertThresholds.Add(threshold);

        // Act
        await _service.SaveSettingsAsync(settings);
        var loaded = await _service.LoadSettingsAsync();

        // Assert
        Assert.Single(loaded.AlertThresholds);
        Assert.Equal(threshold.Id, loaded.AlertThresholds[0].Id);
        Assert.Equal(2500m, loaded.AlertThresholds[0].Price);
        Assert.Equal(AlertDirection.Below, loaded.AlertThresholds[0].Direction);
        Assert.Equal("ETH below 2500", loaded.AlertThresholds[0].Label);
    }

    [Fact]
    public async Task RemoveAlertThreshold_PersistsRemoval()
    {
        // Arrange — save settings with two thresholds
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var settings = new AppSettings
        {
            AlertThresholds = new List<AlertThreshold>
            {
                new() { Id = id1, Price = 3000m, Direction = AlertDirection.Above, IsEnabled = true, Label = "Above 3000" },
                new() { Id = id2, Price = 2000m, Direction = AlertDirection.Below, IsEnabled = true, Label = "Below 2000" }
            }
        };
        await _service.SaveSettingsAsync(settings);

        // Act — remove one threshold and save
        var loaded = await _service.LoadSettingsAsync();
        loaded.AlertThresholds.RemoveAll(t => t.Id == id1);
        await _service.SaveSettingsAsync(loaded);
        var reloaded = await _service.LoadSettingsAsync();

        // Assert — only one threshold remains
        Assert.Single(reloaded.AlertThresholds);
        Assert.Equal(id2, reloaded.AlertThresholds[0].Id);
        Assert.Equal(2000m, reloaded.AlertThresholds[0].Price);
    }
}
