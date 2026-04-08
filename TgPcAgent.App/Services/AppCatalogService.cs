using System.Diagnostics;
using TgPcAgent.App.Models;

namespace TgPcAgent.App.Services;

public sealed class AppCatalogService
{
    private readonly FileLogger _logger;
    private IReadOnlyList<DiscoveredApp> _cachedApps = [];

    public AppCatalogService(FileLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<DiscoveredApp> GetConfiguredApps(AppConfig config)
    {
        return config.AllowedApps
            .Where(app => !string.IsNullOrWhiteSpace(app.Alias) && !string.IsNullOrWhiteSpace(app.TargetPath))
            .Select(app => new DiscoveredApp(
                Alias: NormalizeAlias(app.Alias),
                DisplayName: app.Alias.Trim(),
                LaunchTarget: app.TargetPath,
                Source: "configured"))
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<DiscoveredApp> ScanStartMenuApps()
    {
        var directories = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        }
        .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

        var discoveredApps = new Dictionary<string, DiscoveredApp>(StringComparer.OrdinalIgnoreCase);
        var fileEnumerator = new SafeFileSystemEnumerator(
            directory => Directory.EnumerateDirectories(directory),
            directory => Directory.EnumerateFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly),
            directory => _logger.Info($"Skipped inaccessible start menu directory '{directory}'."));

        foreach (var shortcutPath in fileEnumerator.EnumerateFilesRecursive(directories))
        {
            var fileName = Path.GetFileNameWithoutExtension(shortcutPath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var alias = NormalizeAlias(fileName);
            if (string.IsNullOrWhiteSpace(alias) || discoveredApps.ContainsKey(alias))
            {
                continue;
            }

            discoveredApps[alias] = new DiscoveredApp(alias, fileName, shortcutPath, "start-menu");
        }

        _cachedApps = discoveredApps.Values
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.Info($"Scanned {_cachedApps.Count} start menu apps.");
        return _cachedApps;
    }

    public IReadOnlyList<DiscoveredApp> GetCachedOrScan()
    {
        return _cachedApps.Count > 0 ? _cachedApps : ScanStartMenuApps();
    }

    public (bool Success, string Message) TryLaunch(string requestedAlias, AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(requestedAlias))
        {
            return (false, "Нужен alias приложения: /open steam");
        }

        var normalizedAlias = NormalizeAlias(requestedAlias);
        var configured = config.AllowedApps.FirstOrDefault(app => NormalizeAlias(app.Alias) == normalizedAlias);

        if (configured is not null)
        {
            return LaunchConfiguredApp(configured);
        }

        var discovered = GetCachedOrScan().FirstOrDefault(app => app.Alias == normalizedAlias);
        if (discovered is null)
        {
            return (false, $"Приложение '{requestedAlias}' не найдено.");
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = discovered.LaunchTarget,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return (true, $"Открыл {discovered.DisplayName}.");
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to launch discovered app '{discovered.DisplayName}'.", exception);
            return (false, $"Не удалось открыть {discovered.DisplayName}: {exception.Message}");
        }
    }

    public static string NormalizeAlias(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[input.Length];
        var length = 0;

        foreach (var character in input)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = char.ToLowerInvariant(character);
            }
        }

        return new string(buffer[..length]);
    }

    private (bool Success, string Message) LaunchConfiguredApp(ConfiguredApp configured)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = configured.TargetPath,
                Arguments = configured.Arguments ?? string.Empty,
                UseShellExecute = configured.UseShellExecute
            };

            Process.Start(startInfo);
            return (true, $"Открыл {configured.Alias}.");
        }
        catch (Exception exception)
        {
            _logger.Error($"Failed to launch configured app '{configured.Alias}'.", exception);
            return (false, $"Не удалось открыть {configured.Alias}: {exception.Message}");
        }
    }
}
