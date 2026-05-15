using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    string SettingsFilePath { get; }
}
