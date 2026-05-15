using System.IO;
using System.Text.Json;
using EthPriceMonitor.Models;

namespace EthPriceMonitor.Services;

public class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsDirectory;
    private const string SettingsFileName = "settings.json";
    private const string AppFolderName = "EthPriceMonitor";

    public string SettingsFilePath { get; }

    /// <summary>
    /// Creates the service using the real %AppData% path.
    /// </summary>
    public JsonSettingsService()
    {
        _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppFolderName);
        SettingsFilePath = Path.Combine(_settingsDirectory, SettingsFileName);
    }

    /// <summary>
    /// Creates the service with a custom base directory (for testing).
    /// </summary>
    public JsonSettingsService(string baseDirectory)
    {
        _settingsDirectory = Path.Combine(baseDirectory, AppFolderName);
        SettingsFilePath = Path.Combine(_settingsDirectory, SettingsFileName);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsFilePath))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(SettingsFilePath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions);
        return settings ?? new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!Directory.Exists(_settingsDirectory))
        {
            Directory.CreateDirectory(_settingsDirectory);
        }

        await using var stream = File.Create(SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
