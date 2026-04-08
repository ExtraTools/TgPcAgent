using Microsoft.Win32;

namespace TgPcAgent.App.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TgPcAgent";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string currentValue &&
               string.Equals(currentValue, BuildExecutableValue(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("Unable to open startup registry key.");

        if (enabled)
        {
            key.SetValue(ValueName, BuildExecutableValue());
            return;
        }

        key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string BuildExecutableValue()
    {
        return $"\"{Application.ExecutablePath}\"";
    }
}
