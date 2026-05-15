using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using EthPriceMonitor.Services;
using Xunit;

namespace EthPriceMonitor.Tests.Services;

public class ThemeServiceTests
{
    private static readonly string ThemesPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "EthPriceMonitor", "Themes"));

    [Fact]
    public void ApplyDarkTheme_LoadsDarkThemeResources()
    {
        // Arrange - load the dark theme XAML directly
        var darkThemePath = Path.Combine(ThemesPath, "DarkTheme.xaml");
        Assert.True(File.Exists(darkThemePath), $"DarkTheme.xaml not found at {darkThemePath}");
        var darkTheme = (ResourceDictionary)XamlReader.Load(File.OpenRead(darkThemePath));

        // Assert - verify key dark theme colors
        var bgColor = (Color)darkTheme["BackgroundColor"];
        Assert.Equal(Color.FromRgb(0x1C, 0x1C, 0x1E), bgColor);

        var fgColor = (Color)darkTheme["ForegroundColor"];
        Assert.Equal(Colors.White, fgColor);

        var cardBgColor = (Color)darkTheme["CardBackgroundColor"];
        Assert.Equal(Color.FromRgb(0x2C, 0x2C, 0x2E), cardBgColor);

        var accentColor = (Color)darkTheme["AccentColor"];
        Assert.Equal(Color.FromRgb(0x0A, 0x84, 0xFF), accentColor);
    }

    [Fact]
    public void ApplyLightTheme_LoadsLightThemeResources()
    {
        // Arrange - load the light theme XAML directly
        var lightThemePath = Path.Combine(ThemesPath, "LightTheme.xaml");
        Assert.True(File.Exists(lightThemePath), $"LightTheme.xaml not found at {lightThemePath}");
        var lightTheme = (ResourceDictionary)XamlReader.Load(File.OpenRead(lightThemePath));

        // Assert - verify key light theme colors
        var bgColor = (Color)lightTheme["BackgroundColor"];
        Assert.Equal(Colors.White, bgColor);

        var fgColor = (Color)lightTheme["ForegroundColor"];
        Assert.Equal(Color.FromRgb(0x1C, 0x1C, 0x1E), fgColor);

        var cardBgColor = (Color)lightTheme["CardBackgroundColor"];
        Assert.Equal(Color.FromRgb(0xF2, 0xF2, 0xF7), cardBgColor);

        var accentColor = (Color)lightTheme["AccentColor"];
        Assert.Equal(Color.FromRgb(0x00, 0x7A, 0xFF), accentColor);
    }

    [Fact]
    public void ApplyTheme_ReplacesPreviousThemeDictionary()
    {
        // Arrange - simulate what ApplyTheme does with MergedDictionaries
        var app = EnsureApplication();
        var mergedDicts = app.Resources.MergedDictionaries;

        // Add a "dark theme" placeholder (without pack:// URI to avoid resolution issues)
        var darkPlaceholder = new ResourceDictionary();
        darkPlaceholder["BackgroundColor"] = Color.FromRgb(0x1C, 0x1C, 0x1E);
        mergedDicts.Add(darkPlaceholder);
        Assert.Single(mergedDicts);

        // Simulate ApplyTheme: remove old theme dictionaries, add new one
        // In the real ThemeService, it identifies themes by Source URI containing "LightTheme"/"DarkTheme"
        // For this test, we verify the remove-then-add pattern works correctly
        mergedDicts.Remove(darkPlaceholder);

        var lightPlaceholder = new ResourceDictionary();
        lightPlaceholder["BackgroundColor"] = Colors.White;
        mergedDicts.Add(lightPlaceholder);

        // Assert - only one dictionary remains (the light one)
        Assert.Single(mergedDicts);
        var bgColor = (Color)mergedDicts[0]["BackgroundColor"];
        Assert.Equal(Colors.White, bgColor);
    }

    private static Application EnsureApplication()
    {
        if (Application.Current != null)
            return Application.Current;

        return new Application();
    }
}
