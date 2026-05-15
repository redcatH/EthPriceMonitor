using EthPriceMonitor.Models;
using EthPriceMonitor.Services;
using EthPriceMonitor.ViewModels;
using Moq;

namespace EthPriceMonitor.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService
            .Setup(s => s.LoadSettingsAsync())
            .ReturnsAsync(new AppSettings());

        _viewModel = new SettingsViewModel(_mockSettingsService.Object);
    }

    [Fact]
    public void AddThreshold_ValidPrice_AddsToList()
    {
        // Arrange — collection starts empty
        Assert.Empty(_viewModel.AlertThresholds);

        // Act
        _viewModel.AddThreshold(3500m, AlertDirection.Above);

        // Assert
        Assert.Single(_viewModel.AlertThresholds);
        var added = _viewModel.AlertThresholds[0];
        Assert.Equal(3500m, added.Price);
        Assert.Equal(AlertDirection.Above, added.Direction);
        Assert.True(added.IsEnabled);
        Assert.Contains("3500", added.Label);
    }

    [Fact]
    public void AddThreshold_InvalidPrice_DoesNotAdd()
    {
        // Arrange
        Assert.Empty(_viewModel.AlertThresholds);

        // Act — price ≤ 0 should be rejected
        _viewModel.AddThreshold(0m, AlertDirection.Above);
        _viewModel.AddThreshold(-100m, AlertDirection.Below);

        // Assert — nothing added
        Assert.Empty(_viewModel.AlertThresholds);
    }

    [Fact]
    public void RemoveThreshold_ExistingThreshold_RemovesFromList()
    {
        // Arrange
        _viewModel.AddThreshold(3000m, AlertDirection.Above);
        _viewModel.AddThreshold(2500m, AlertDirection.Below);
        Assert.Equal(2, _viewModel.AlertThresholds.Count);

        var toRemove = _viewModel.AlertThresholds[0];

        // Act
        _viewModel.RemoveThreshold(toRemove);

        // Assert
        Assert.Single(_viewModel.AlertThresholds);
        Assert.DoesNotContain(toRemove, _viewModel.AlertThresholds);
    }

    [Fact]
    public async Task SaveCommand_CallsSettingsService()
    {
        // Arrange
        _viewModel.AddThreshold(4000m, AlertDirection.Above);
        _viewModel.AutoStartWithWindows = true;
        _viewModel.ShowFloatingWindow = false;

        _mockSettingsService
            .Setup(s => s.SaveSettingsAsync(It.IsAny<AppSettings>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        await _viewModel.ExecuteSaveAsync();

        // Assert — SaveSettingsAsync was called once with the correct settings
        _mockSettingsService.Verify(
            s => s.SaveSettingsAsync(It.Is<AppSettings>(settings =>
                settings.AlertThresholds.Count == 1 &&
                settings.AlertThresholds[0].Price == 4000m &&
                settings.AutoStartWithWindows == true &&
                settings.ShowFloatingWindow == false)),
            Times.Once);
    }

    [Fact]
    public async Task LoadSettingsAsync_PopulatesPropertiesFromService()
    {
        // Arrange
        var settings = new AppSettings
        {
            AutoStartWithWindows = true,
            ShowFloatingWindow = false,
            AlertThresholds = new List<AlertThreshold>
            {
                new() { Price = 5000m, Direction = AlertDirection.Above, IsEnabled = true, Label = "突破 $5000.00" },
                new() { Price = 2000m, Direction = AlertDirection.Below, IsEnabled = false, Label = "跌破 $2000.00" }
            }
        };

        _mockSettingsService
            .Setup(s => s.LoadSettingsAsync())
            .ReturnsAsync(settings);

        // Act
        await _viewModel.LoadSettingsAsync();

        // Assert
        Assert.Equal(2, _viewModel.AlertThresholds.Count);
        Assert.Equal(5000m, _viewModel.AlertThresholds[0].Price);
        Assert.Equal(2000m, _viewModel.AlertThresholds[1].Price);
        Assert.True(_viewModel.AutoStartWithWindows);
        Assert.False(_viewModel.ShowFloatingWindow);
    }

    [Fact]
    public void AddThreshold_BelowDirection_GeneratesCorrectLabel()
    {
        // Act
        _viewModel.AddThreshold(2500m, AlertDirection.Below);

        // Assert
        var added = _viewModel.AlertThresholds[0];
        Assert.Contains("跌破", added.Label);
        Assert.Contains("2500", added.Label);
    }
}
