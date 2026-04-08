using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace TgPcAgent.App.Services;

public sealed class GitHubUpdateService : IDisposable
{
    private const string GitHubRepo = "ExtraTools/TgPcAgent";
    private const string AssetPrefix = "TgPcAgent-Setup-";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    private readonly FileLogger _logger;
    private readonly TrayNotifier _trayNotifier;
    private readonly UiDispatcher _uiDispatcher;
    private readonly ConfigurationService _configurationService;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public GitHubUpdateService(
        FileLogger logger,
        TrayNotifier trayNotifier,
        UiDispatcher uiDispatcher,
        ConfigurationService configurationService)
    {
        _logger = logger;
        _trayNotifier = trayNotifier;
        _uiDispatcher = uiDispatcher;
        _configurationService = configurationService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TgPcAgent-Updater");
    }

    public void Start()
    {
        if (_loopTask is not null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _loopTask?.GetAwaiter().GetResult(); }
        catch { /* ignore */ }
        _cts?.Dispose();
        _httpClient.Dispose();
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        await Task.Delay(StartupDelay, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var config = _configurationService.GetSnapshot();
                if (config.AutoUpdate)
                {
                    await CheckAndUpdateAsync(ct, config.SilentUpdate);
                }
                else
                {
                    _logger.Info("Auto-update disabled in settings. Skipping.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Error("Update check failed.", ex);
            }

            await Task.Delay(CheckInterval, ct);
        }
    }

    public async Task CheckAndUpdateAsync(CancellationToken ct, bool silentInstall = true)
    {
        var currentVersion = GetCurrentVersion();
        _logger.Info($"Update check: current={currentVersion}");

        var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
        _logger.Info($"Fetching {url}");
        var response = await _httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.Info($"GitHub API returned {(int)response.StatusCode} {response.StatusCode}. Skipping.");
            return;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.GetProperty("tag_name").GetString() ?? "";
        var remoteVersion = ParseVersion(tagName);
        _logger.Info($"Remote version: {tagName} (parsed: {remoteVersion})");

        if (remoteVersion is null || remoteVersion <= currentVersion)
        {
            _logger.Info("No update available.");
            return;
        }

        _logger.Info($"Update available: {currentVersion} -> {remoteVersion} ({tagName})");

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains("Lite", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            _logger.Error($"No setup asset found in release {tagName}.");
            return;
        }

        if (!silentInstall)
        {
            var userChoice = DialogResult.None;
            await _uiDispatcher.InvokeAsync(() =>
            {
                userChoice = MessageBox.Show(
                    $"Доступна новая версия: {tagName}\n\nТекущая: {currentVersion}\nНовая: {remoteVersion}\n\nОбновить сейчас?",
                    "TgPcAgent — Обновление",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
            });

            if (userChoice != DialogResult.Yes)
            {
                _logger.Info("User declined update.");
                return;
            }
        }

        _trayNotifier.ShowInfo("TgPcAgent", $"Обновление {tagName}. Скачиваю...");
        _logger.Info($"Downloading: {downloadUrl}");

        var tempDir = Path.Combine(Path.GetTempPath(), "TgPcAgent-Update");
        Directory.CreateDirectory(tempDir);
        var tempSetupPath = Path.Combine(tempDir, "TgPcAgent-Setup.exe");

        await using (var stream = await _httpClient.GetStreamAsync(downloadUrl, ct))
        await using (var fileStream = File.Create(tempSetupPath))
        {
            await stream.CopyToAsync(fileStream, ct);
        }

        var fileInfo = new FileInfo(tempSetupPath);
        if (!fileInfo.Exists || fileInfo.Length < 1024)
        {
            _logger.Error($"Downloaded file invalid: exists={fileInfo.Exists}, size={fileInfo.Length} bytes. Aborting.");
            return;
        }

        _logger.Info($"Downloaded {fileInfo.Length / 1024.0 / 1024.0:F1} MB -> {tempSetupPath}");
        _trayNotifier.ShowInfo("TgPcAgent", "Устанавливаю обновление...");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tempSetupPath,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            };
            var proc = Process.Start(psi);
            if (proc is not null)
            {
                _logger.Info($"Installer started: PID={proc.Id}");
            }
            else
            {
                _logger.Error("Process.Start returned null — installer did not launch.");
                _trayNotifier.ShowInfo("TgPcAgent", "Не удалось запустить установщик.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to start installer.", ex);
            _trayNotifier.ShowInfo("TgPcAgent", $"Ошибка запуска установщика: {ex.Message}");
        }
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    }

    private static Version? ParseVersion(string tag)
    {
        var cleaned = tag.TrimStart('v', 'V');
        var dashIdx = cleaned.IndexOf('-');
        if (dashIdx > 0) cleaned = cleaned[..dashIdx];
        return Version.TryParse(cleaned, out var version) ? version : null;
    }
}
