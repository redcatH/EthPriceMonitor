namespace EthPriceMonitor.Services;

/// <summary>
/// Abstraction over HKCU registry access for testability.
/// Operates on the SOFTWARE\Microsoft\Windows\CurrentVersion\Run key.
/// </summary>
public interface IRegistryAccessor
{
    /// <summary>Set a named value in the Run key.</summary>
    void SetValue(string keyName, object value);

    /// <summary>Delete a named value from the Run key.</summary>
    void DeleteValue(string keyName);

    /// <summary>Get a named value from the Run key, or null if not found.</summary>
    string? GetValue(string keyName);
}
