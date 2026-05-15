using Moq;
using EthPriceMonitor.Services;
using Xunit;

namespace EthPriceMonitor.Tests.Services;

public class AutoStartServiceTests
{
    private readonly Mock<IRegistryAccessor> _registryMock;
    private readonly AutoStartService _service;

    public AutoStartServiceTests()
    {
        _registryMock = new Mock<IRegistryAccessor>(MockBehavior.Strict);
        _service = new AutoStartService(_registryMock.Object);
    }

    [Fact]
    public void EnableAutoStart_WritesRegistryKey()
    {
        // Arrange
        var expectedPath = Environment.ProcessPath!;
        _registryMock
            .Setup(r => r.SetValue(AutoStartService.AppName, expectedPath))
            .Verifiable();

        // Act
        _service.Enable();

        // Assert
        _registryMock.Verify(r => r.SetValue(AutoStartService.AppName, expectedPath), Times.Once);
    }

    [Fact]
    public void DisableAutoStart_RemovesRegistryKey()
    {
        // Arrange
        _registryMock
            .Setup(r => r.DeleteValue(AutoStartService.AppName))
            .Verifiable();

        // Act
        _service.Disable();

        // Assert
        _registryMock.Verify(r => r.DeleteValue(AutoStartService.AppName), Times.Once);
    }

    [Fact]
    public void IsEnabled_ReturnsTrueWhenKeyMatchesCurrentExePath()
    {
        // Arrange
        var expectedPath = Environment.ProcessPath!;
        _registryMock
            .Setup(r => r.GetValue(AutoStartService.AppName))
            .Returns(expectedPath);

        // Act
        var result = _service.IsEnabled;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsEnabled_ReturnsFalseWhenKeyNotFound()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetValue(AutoStartService.AppName))
            .Returns((string?)null);

        // Act
        var result = _service.IsEnabled;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsEnabled_ReturnsFalseWhenKeyValueDiffers()
    {
        // Arrange
        _registryMock
            .Setup(r => r.GetValue(AutoStartService.AppName))
            .Returns("C:\\SomeOther\\app.exe");

        // Act
        var result = _service.IsEnabled;

        // Assert
        Assert.False(result);
    }
}
