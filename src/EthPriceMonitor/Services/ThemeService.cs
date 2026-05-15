using System.Windows;
using Microsoft.Win32;
using Windows.UI.ViewManagement;

namespace EthPriceMonitor.Services;

public class ThemeService
{
    private UISettings? _uiSettings;
    private bool _isWatching;

    public event EventHandler<bool>? ThemeChanged;

    /// <summary>
    /// Determines whether the system is currently using a dark theme.
    /// </summary>
    public bool IsSystemDarkTheme()
    {
        // Try UISettings first
        try
        {
            var settings = new UISettings();
            var background = settings.GetColorValue(UIColorType.Background);
            // If the system background color is dark (R+G+B < 384), it's dark theme
            return (background.R + background.G + background.B) < 384;
        }
        catch
        {
            // Fallback to registry
            return IsSystemDarkThemeFromRegistry();
        }
    }

    /// <summary>
    /// Applies the specified theme by swapping the theme ResourceDictionary.
    /// </summary>
    /// <param name="isDark">True for dark theme, false for light theme.</param>
    public void ApplyTheme(bool isDark)
    {
        if (Application.Current == null) return;

        var appResources = Application.Current.Resources;
        var mergedDicts = appResources.MergedDictionaries;

        // Remove any existing theme dictionaries
        var existingThemes = mergedDicts
            .Where(d => d.Source != null &&
                        (d.Source.OriginalString.Contains("LightTheme") ||
                         d.Source.OriginalString.Contains("DarkTheme")))
            .ToList();

        foreach (var theme in existingThemes)
        {
            mergedDicts.Remove(theme);
        }

        // Add the new theme dictionary
        var themeSource = isDark
            ? new Uri("pack://application:,,,/Themes/DarkTheme.xaml")
            : new Uri("pack://application:,,,/Themes/LightTheme.xaml");

        var newTheme = new ResourceDictionary { Source = themeSource };
        mergedDicts.Add(newTheme);
    }

    /// <summary>
    /// Starts watching for system theme changes.
    /// </summary>
    public void StartWatching()
    {
        if (_isWatching) return;

        try
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;
            _isWatching = true;
        }
        catch
        {
            // UISettings not available; cannot watch
        }
    }

    /// <summary>
    /// Stops watching for system theme changes.
    /// </summary>
    public void StopWatching()
    {
        if (!_isWatching) return;

        if (_uiSettings != null)
        {
            _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
            _uiSettings = null;
        }

        _isWatching = false;
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        // UISettings events may fire on a background thread
        var isDark = IsSystemDarkTheme();
        ThemeChanged?.Invoke(this, isDark);
    }

    private static bool IsSystemDarkThemeFromRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0; // 0 = dark, 1 = light
            }
        }
        catch
        {
            // Registry not available
        }

        return false; // Default to light
    }
}
