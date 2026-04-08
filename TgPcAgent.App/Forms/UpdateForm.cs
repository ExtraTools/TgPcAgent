using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace TgPcAgent.App.Forms;

public sealed class UpdateForm : Form
{
    private readonly Services.FileLogger _logger;
    private readonly HttpClient _httpClient;
    private readonly Label _statusLabel;
    private readonly Label _versionLabel;
    private readonly Label _detailsLabel;
    private readonly ProgressBar _progressBar;
    private readonly Button _actionButton;
    private readonly Button _closeButton;
    private CancellationTokenSource? _cts;

    private const string GitHubRepo = "ExtraTools/TgPcAgent";
    private const string AssetPrefix = "TgPcAgent-Setup-";

    private string? _downloadUrl;
    private string? _remoteTag;
    private Version? _remoteVersion;

    public UpdateForm(Services.FileLogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "TgPcAgent-Updater");

        Text = "TgPcAgent — Обновления";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 480;
        Height = 320;
        UiTheme.ApplyWindow(this);

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28)
        };

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;

        var titleLabel = new Label
        {
            Text = "Обновления TgPcAgent",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            AutoSize = true,
            Location = new System.Drawing.Point(28, 24)
        };

        _versionLabel = new Label
        {
            Text = $"Текущая версия: v{currentVersion?.ToString(3)}",
            AutoSize = true,
            Location = new System.Drawing.Point(28, 58)
        };
        UiTheme.ApplyMutedText(_versionLabel);

        _statusLabel = new Label
        {
            Text = "Проверка обновлений...",
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            AutoSize = true,
            Location = new System.Drawing.Point(28, 94)
        };

        _detailsLabel = new Label
        {
            Text = "",
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(410, 0),
            Location = new System.Drawing.Point(28, 120)
        };
        UiTheme.ApplyMutedText(_detailsLabel);

        _progressBar = new ProgressBar
        {
            Location = new System.Drawing.Point(28, 160),
            Width = 410,
            Height = 24,
            Style = ProgressBarStyle.Continuous,
            Visible = false
        };

        _actionButton = new Button
        {
            Text = "Проверить",
            Width = 160,
            Height = 38,
            Location = new System.Drawing.Point(28, 200),
            Enabled = false,
            Visible = false
        };
        UiTheme.ApplyPrimaryButton(_actionButton);
        _actionButton.Click += OnActionClick;

        _closeButton = new Button
        {
            Text = "Закрыть",
            Width = 120,
            Height = 38,
            Location = new System.Drawing.Point(318, 200)
        };
        UiTheme.ApplySecondaryButton(_closeButton);
        _closeButton.Click += (_, _) => Close();

        Controls.Add(titleLabel);
        Controls.Add(_versionLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_detailsLabel);
        Controls.Add(_progressBar);
        Controls.Add(_actionButton);
        Controls.Add(_closeButton);

        Load += async (_, _) => await CheckForUpdatesAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        _httpClient.Dispose();
        base.OnFormClosed(e);
    }

    private async Task CheckForUpdatesAsync()
    {
        _cts = new CancellationTokenSource();
        try
        {
            SetStatus("Проверка обновлений...", "Подключение к GitHub...");
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

            var url = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
            var response = await _httpClient.GetAsync(url, _cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                SetStatus("Ошибка", $"GitHub вернул {(int)response.StatusCode}. Попробуйте позже.");
                ShowRetryButton();
                return;
            }

            var json = await response.Content.ReadAsStringAsync(_cts.Token);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _remoteTag = root.GetProperty("tag_name").GetString() ?? "";
            _remoteVersion = ParseVersion(_remoteTag);

            if (_remoteVersion is null || _remoteVersion <= currentVersion)
            {
                SetStatus("Обновлений нет", $"v{currentVersion.ToString(3)} — последняя версия.");
                _detailsLabel.ForeColor = Color.FromArgb(22, 163, 74);
                return;
            }

            string? releaseBody = null;
            if (root.TryGetProperty("body", out var bodyEl))
                releaseBody = bodyEl.GetString();

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("Lite", StringComparison.OrdinalIgnoreCase))
                    {
                        _downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(_downloadUrl))
            {
                SetStatus("Ошибка", $"Установщик не найден в релизе {_remoteTag}.");
                return;
            }

            SetStatus($"Доступна {_remoteTag}", $"v{currentVersion.ToString(3)} → v{_remoteVersion.ToString(3)}");
            _statusLabel.ForeColor = UiTheme.Accent;

            _actionButton.Text = "Скачать и установить";
            _actionButton.Visible = true;
            _actionButton.Enabled = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Error("Update check failed in UI.", ex);
            SetStatus("Ошибка", ex.Message);
            ShowRetryButton();
        }
    }

    private async void OnActionClick(object? sender, EventArgs e)
    {
        if (_downloadUrl is null) return;

        _actionButton.Enabled = false;
        _actionButton.Text = "Скачивание...";
        _progressBar.Visible = true;
        _progressBar.Value = 0;

        _cts = new CancellationTokenSource();
        try
        {
            SetStatus("Скачивание...", _downloadUrl);

            var tempDir = Path.Combine(Path.GetTempPath(), "TgPcAgent-Update");
            Directory.CreateDirectory(tempDir);
            var tempPath = Path.Combine(tempDir, "TgPcAgent-Setup.exe");

            using var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(_cts.Token);
            await using var fileStream = File.Create(tempPath);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, _cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), _cts.Token);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var pct = (int)(totalRead * 100 / totalBytes);
                    _progressBar.Value = Math.Min(pct, 100);
                    _detailsLabel.Text = $"{totalRead / 1024.0 / 1024.0:F1} / {totalBytes / 1024.0 / 1024.0:F1} MB";
                }
            }

            _progressBar.Value = 100;
            SetStatus("Скачано!", $"{totalRead / 1024.0 / 1024.0:F1} MB → {tempPath}");

            _logger.Info($"Update downloaded: {totalRead} bytes -> {tempPath}");

            await Task.Delay(500, _cts.Token);
            SetStatus("Установка...", "Запуск установщика...");

            try
            {
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                    UseShellExecute = true
                });

                if (proc is not null)
                {
                    _logger.Info($"Installer started: PID={proc.Id}");
                    SetStatus("Установка запущена", "Приложение закроется автоматически...");
                }
                else
                {
                    _logger.Error("Process.Start returned null.");
                    SetStatus("Ошибка", "Установщик не запустился. Запустите вручную.");
                    _actionButton.Text = "Открыть папку";
                    _actionButton.Enabled = true;
                    var dir = tempDir;
                    _actionButton.Click -= OnActionClick;
                    _actionButton.Click += (_, _) =>
                    {
                        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to start installer.", ex);
                SetStatus("Ошибка запуска", ex.Message);
                _actionButton.Text = "Повторить";
                _actionButton.Enabled = true;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.Error("Download failed.", ex);
            SetStatus("Ошибка скачивания", ex.Message);
            _actionButton.Text = "Повторить";
            _actionButton.Enabled = true;
            _progressBar.Value = 0;
        }
    }

    private void SetStatus(string status, string details)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetStatus(status, details));
            return;
        }
        _statusLabel.Text = status;
        _statusLabel.ForeColor = UiTheme.Text;
        _detailsLabel.Text = details;
        _detailsLabel.ForeColor = UiTheme.MutedText;
    }

    private void ShowRetryButton()
    {
        _actionButton.Text = "Повторить";
        _actionButton.Visible = true;
        _actionButton.Enabled = true;
        _actionButton.Click -= OnActionClick;
        _actionButton.Click += async (_, _) =>
        {
            _actionButton.Click -= OnActionClick;
            _actionButton.Click += OnActionClick;
            _actionButton.Visible = false;
            await CheckForUpdatesAsync();
        };
    }

    private static Version? ParseVersion(string tag)
    {
        var cleaned = tag.TrimStart('v', 'V');
        var dashIdx = cleaned.IndexOf('-');
        if (dashIdx > 0) cleaned = cleaned[..dashIdx];
        return Version.TryParse(cleaned, out var version) ? version : null;
    }
}
