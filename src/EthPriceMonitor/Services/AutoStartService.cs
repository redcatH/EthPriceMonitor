using Microsoft.Win32;

namespace EthPriceMonitor.Services;

/// <summary>
/// Production implementation of <see cref="IRegistryAccessor"/> using HKCU Run key.
/// </summary>
public class RegistryAccessor : IRegistryAccessor
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string keyName, object value)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(keyName, value);
    }

    public void DeleteValue(string keyName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        key?.DeleteValue(keyName, false);
    }

    public string? GetValue(string keyName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(keyName) as string;
    }
}

/// <summary>
/// Service for registering/unregistering the application to start with Windows
/// via the HKCU Run registry key. No admin privileges required.
/// </summary>
public class AutoStartService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    public const string AppName = "EthPriceMonitor";

    private readonly IRegistryAccessor _registry;

    public AutoStartService(IRegistryAccessor registry)
    {
        _registry = registry;
    }

    /// <summary>Whether the app is currently registered for auto-start.</summary>
    public bool IsEnabled
    {
        get
        {
            var value = _registry.GetValue(AppName);
            return string.Equals(value, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Register the app to start with Windows.</summary>
    public void Enable()
    {
        _registry.SetValue(AppName, Environment.ProcessPath!);
    }

    /// <summary>Unregister the app from Windows auto-start.</summary>
    public void Disable()
    {
        _registry.DeleteValue(AppName);
    }
}
