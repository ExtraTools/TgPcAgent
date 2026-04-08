using System.Diagnostics;
using System.Drawing;
using System.Threading;
using TgPcAgent.App.Forms;
using TgPcAgent.App.Services;
using TgPcAgent.Core.Interop;
using TgPcAgent.Core.Security;

namespace TgPcAgent.App.Tray;

public sealed class AgentApplicationContext : ApplicationContext
{
    private readonly ConfigurationService _configurationService;
    private readonly FileLogger _logger;
    private readonly StartupService _startupService;
    private readonly UiDispatcher _uiDispatcher;
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayNotifier _trayNotifier;
    private readonly HardwareMonitorService _hardwareMonitorService;
    private readonly CloudApiClient _cloudApiClient;
    private readonly CloudRelayResultSender _cloudRelayResultSender;
    private readonly CloudCommandPollingService _cloudCommandPollingService;
    private readonly CloudRelayService _cloudRelayService;
    private readonly AutomationService _automationService;
    private readonly GitHubUpdateService _updateService;
    private readonly PairingService _pairingService;
    private readonly FirstRunExperiencePolicy _firstRunExperiencePolicy;
    private readonly CommandExecutionService _commandExecutionService;
    private int _shutdownRequested;

    // Last known pairing code from cloud registration
    private string? _lastPairingCode;

    public AgentApplicationContext()
    {
        _configurationService = new ConfigurationService();
        _logger = new FileLogger(_configurationService.LogsDirectory);
        _startupService = new StartupService();
        _uiDispatcher = new UiDispatcher(SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());
        _firstRunExperiencePolicy = new FirstRunExperiencePolicy();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "TgPcAgent",
            Visible = true
        };
        _trayNotifier = new TrayNotifier(_notifyIcon, _uiDispatcher);

        _hardwareMonitorService = new HardwareMonitorService();
        var systemStatusService = new SystemStatusService(_hardwareMonitorService);
        var processService = new ProcessService();
        var screenshotService = new ScreenshotService();
        var appCatalogService = new AppCatalogService(_logger);
        var powerService = new PowerService();
        _pairingService = new PairingService(ownerChatId: _configurationService.Current.OwnerChatId);
        var confirmationStore = new ConfirmationStore();

        // Cloud-mode services
        _cloudApiClient = new CloudApiClient(_logger);
        _cloudRelayResultSender = new CloudRelayResultSender(_cloudApiClient, _configurationService, _logger);

        _commandExecutionService = new CommandExecutionService(
            _configurationService,
            _logger,
            _cloudRelayResultSender,
            _pairingService,
            confirmationStore,
            systemStatusService,
            processService,
            screenshotService,
            appCatalogService,
            powerService,
            _trayNotifier,
            _uiDispatcher);

        _cloudCommandPollingService = new CloudCommandPollingService(
            _configurationService,
            _cloudApiClient,
            _commandExecutionService.HandleCloudCommandAsync,
            _logger);
        _cloudRelayService = new CloudRelayService(_configurationService, _logger);
        _automationService = new AutomationService(_configurationService, _commandExecutionService, _logger);
        _updateService = new GitHubUpdateService(_logger, _trayNotifier, _uiDispatcher, _configurationService);

        _notifyIcon.ContextMenuStrip = BuildContextMenu();
        _notifyIcon.DoubleClick += (_, _) => ShowAgentSummary();

        // Ensure agent credentials exist and register with cloud
        EnsureAgentRegistration();

        // On first run, show pairing dialog instead of settings
        MaybeOpenFirstRunPairing();

        _cloudCommandPollingService.Start();
        _cloudRelayService.Start();
        _automationService.Start();
        _updateService.Start();
        ShowStartupHints();
    }

    public FileLogger Logger => _logger;

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        try
        {
            _cloudRelayService.SendShutdownAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        }
        catch
        {
            // Best effort shutdown ping only.
        }

        _cloudRelayService.Dispose();
        _automationService.Dispose();
        _updateService.Dispose();
        _cloudCommandPollingService.Dispose();
        _cloudApiClient.Dispose();
        _hardwareMonitorService.Dispose();
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }

    public void RequestShutdown()
    {
        if (Interlocked.Exchange(ref _shutdownRequested, 1) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            Environment.Exit(0);
        });

        Application.Exit();
        ExitThread();
    }

    public Task HandleInstanceControlCommandAsync(InstanceControlCommand command)
    {
        return _uiDispatcher.InvokeAsync(() =>
        {
            switch (command)
            {
                case InstanceControlCommand.ShutdownExisting:
                    _trayNotifier.ShowInfo("TgPcAgent", "Другой экземпляр запросил закрытие приложения.");
                    RequestShutdown();
                    break;
                case InstanceControlCommand.RestartExisting:
                    _trayNotifier.ShowInfo("TgPcAgent", "Другой экземпляр запросил перезапуск приложения.");
                    RequestShutdown();
                    break;
            }
        });
    }

    private void EnsureAgentRegistration()
    {
        var config = _configurationService.GetSnapshot();

        if (_configurationService.EnsureAgentCredentials(config))
        {
            _configurationService.Save(config);
            _logger.Info($"Generated agent credentials: AgentId={config.AgentId}");
        }

        // Register with cloud in background
        _ = Task.Run(async () =>
        {
            var snapshot = _configurationService.GetSnapshot();
            var baseUrl = snapshot.CloudRelayUrl?.Trim();
            var agentId = snapshot.AgentId;
            var secret = _configurationService.GetAgentSecret(snapshot);

            if (string.IsNullOrWhiteSpace(baseUrl) ||
                string.IsNullOrWhiteSpace(agentId) ||
                string.IsNullOrWhiteSpace(secret))
            {
                _logger.Info("Skipped cloud registration: missing CloudRelayUrl or agent credentials.");
                return;
            }

            if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri))
            {
                _logger.Info("Skipped cloud registration: invalid CloudRelayUrl.");
                return;
            }

            var result = await _cloudApiClient.RegisterAsync(baseUri, agentId, secret, Environment.MachineName, CancellationToken.None);
            if (result is null)
            {
                _logger.Info("Cloud registration returned null.");
                return;
            }

            if (result.AlreadyPaired && result.OwnerChatId.HasValue)
            {
                _logger.Info($"Agent already paired with chatId={result.OwnerChatId}.");
                var cfg = _configurationService.GetSnapshot();
                if (cfg.OwnerChatId != result.OwnerChatId.Value)
                {
                    cfg.OwnerChatId = result.OwnerChatId.Value;
                    _configurationService.Save(cfg);
                    _pairingService.SetOwner(result.OwnerChatId.Value);
                    _logger.Info($"OwnerChatId restored from cloud: {result.OwnerChatId.Value}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(result.PairingCode))
            {
                _lastPairingCode = result.PairingCode;
                _logger.Info($"Cloud registration OK. PairingCode={result.PairingCode}");
                _uiDispatcher.InvokeAsync(() =>
                    _trayNotifier.ShowInfo("TgPcAgent", $"Код привязки: {result.PairingCode}"));
            }
        });
    }

    private void RebuildContextMenu()
    {
        _uiDispatcher.InvokeAsync(() =>
        {
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.ContextMenuStrip = BuildContextMenu();
        });
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = UiTheme.Surface,
            ForeColor = UiTheme.Text
        };

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var versionItem = new ToolStripMenuItem($"TgPcAgent v{version?.ToString(3)}")
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = UiTheme.MutedText,
        };
        versionItem.Click += (_, _) => OpenUpdateWindow();
        menu.Items.Add(versionItem);

        var ownerChatId = _configurationService.GetSnapshot().OwnerChatId;
        var pairingText = ownerChatId.HasValue
            ? $"Привязан: {ownerChatId.Value}"
            : "Не привязан";
        var pairingInfoItem = new ToolStripMenuItem(pairingText)
        {
            ForeColor = ownerChatId.HasValue ? UiTheme.MutedText : Color.OrangeRed,
            Enabled = false
        };
        menu.Items.Add(pairingInfoItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Статус агента", null, (_, _) => ShowAgentSummary());

        if (ownerChatId.HasValue)
        {
            menu.Items.Add("Перепривязка", null, (_, _) => ForceRePair());
        }
        else
        {
            menu.Items.Add("Показать код привязки", null, (_, _) => ShowPairingCode());
        }

        menu.Items.Add("Обновления", null, (_, _) => OpenUpdateWindow());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettings());
        menu.Items.Add("Открыть лог", null, (_, _) => OpenLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => RequestShutdown());
        return menu;
    }

    private void MaybeOpenFirstRunPairing()
    {
        if (!_firstRunExperiencePolicy.ShouldOpenSettings(_configurationService.ConfigExists))
        {
            return;
        }

        // Save config so next launch won't trigger first-run again
        _configurationService.Save(_configurationService.GetSnapshot());

        _trayNotifier.ShowInfo("TgPcAgent", "Первый запуск. Получаю код привязки...");
        ShowPairingCode();
    }

    private void ShowStartupHints()
    {
        var config = _configurationService.GetSnapshot();

        if (string.IsNullOrWhiteSpace(config.AgentId))
        {
            _logger.Info("AgentId is empty after EnsureAgentRegistration. Will retry next launch.");
            return;
        }

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (config.OwnerChatId.HasValue)
        {
            _trayNotifier.ShowInfo("TgPcAgent", $"v{version?.ToString(3)} запущен. Привязан к Telegram.");
        }
        else
        {
            _trayNotifier.ShowInfo("TgPcAgent", $"v{version?.ToString(3)} запущен. Не привязан к Telegram — откройте меню для привязки.");
        }
    }

    private void ShowAgentSummary()
    {
        var config = _configurationService.GetSnapshot();
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var pairedStatus = config.OwnerChatId.HasValue
            ? $"Привязан к Telegram (chatId: {config.OwnerChatId.Value})"
            : "Не привязан к Telegram";
        var updateMode = config.AutoUpdate
            ? (config.SilentUpdate ? "автоматически" : "с подтверждением")
            : "отключено";
        var summary = $"""
                       TgPcAgent v{version?.ToString(3)}

                       AgentId: {config.AgentId ?? "не создан"}
                       Cloud URL: {config.CloudRelayUrl ?? "не указан"}
                       Telegram: {pairedStatus}
                       Автозапуск: {FormatYesNo(_startupService.IsEnabled())}
                       Обновления: {updateMode}
                       Конфиг: {_configurationService.ConfigPath}
                       """;

        MessageBox.Show(summary, "TgPcAgent", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowPairingCode()
    {
        var config = _configurationService.GetSnapshot();

        if (string.IsNullOrWhiteSpace(config.CloudRelayUrl))
        {
            MessageBox.Show("Сначала укажи Cloud Relay URL в настройках.", "TgPcAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(config.AgentId))
        {
            MessageBox.Show("AgentId не сгенерирован. Перезапусти приложение.", "TgPcAgent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Request a fresh pairing code from cloud
        _ = Task.Run(async () =>
        {
            var snapshot = _configurationService.GetSnapshot();
            var baseUrl = snapshot.CloudRelayUrl!.Trim();
            var secret = _configurationService.GetAgentSecret(snapshot);
            if (string.IsNullOrWhiteSpace(secret)) return;

            if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri))
                return;

            var result = await _cloudApiClient.RegisterAsync(
                baseUri, snapshot.AgentId!, secret, Environment.MachineName, CancellationToken.None);

            if (result == null)
            {
                await _uiDispatcher.InvokeAsync(() =>
                    MessageBox.Show("Не удалось связаться с cloud.", "TgPcAgent",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning));
                return;
            }

            if (result.AlreadyPaired)
            {
                await _uiDispatcher.InvokeAsync(() =>
                    MessageBox.Show("ПК уже привязан. Используй \"Перепривязка\" в меню трея.", "TgPcAgent",
                        MessageBoxButtons.OK, MessageBoxIcon.Information));
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.PairingCode))
            {
                _lastPairingCode = result.PairingCode;
                await _uiDispatcher.InvokeAsync(() =>
                {
                    _trayNotifier.ShowInfo("TgPcAgent", $"Код привязки: {result.PairingCode}");
                    ShowPairingCodeDialog(result.PairingCode);
                });
            }
        });
    }

    private void ForceRePair()
    {
        _ = Task.Run(async () =>
        {
            var snapshot = _configurationService.GetSnapshot();
            var baseUrl = snapshot.CloudRelayUrl?.Trim();
            var secret = _configurationService.GetAgentSecret(snapshot);
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(secret) ||
                string.IsNullOrWhiteSpace(snapshot.AgentId))
                return;

            if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/", UriKind.Absolute, out var baseUri))
                return;

            var confirm = await _uiDispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    "Отвязать ПК от текущего Telegram-аккаунта\nи получить новый код привязки?",
                    "TgPcAgent — Перепривязка",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question));

            if (confirm != DialogResult.OK) return;

            var result = await _cloudApiClient.RegisterAsync(
                baseUri, snapshot.AgentId!, secret, Environment.MachineName, CancellationToken.None, forceRePair: true);

            if (result != null && !string.IsNullOrWhiteSpace(result.PairingCode))
            {
                _lastPairingCode = result.PairingCode;
                _pairingService.ResetOwner();
                var currentConfig = _configurationService.GetSnapshot();
                currentConfig.OwnerChatId = null;
                _configurationService.Save(currentConfig);
                RebuildContextMenu();

                await _uiDispatcher.InvokeAsync(() =>
                {
                    _trayNotifier.ShowInfo("TgPcAgent", $"Новый код: {result.PairingCode}");
                    ShowPairingCodeDialog(result.PairingCode);
                });
            }
            else
            {
                await _uiDispatcher.InvokeAsync(() =>
                    MessageBox.Show("Не удалось выполнить перепривязку.", "TgPcAgent",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning));
            }
        });
    }

    private void OpenSettings(bool isInitialSetup = false)
    {
        using var settingsForm = new SettingsForm(_configurationService, _startupService, _pairingService, isInitialSetup);
        var result = settingsForm.ShowDialog();
        if (result != DialogResult.OK)
        {
            return;
        }

        _logger.Info("Settings saved.");
        ShowStartupHints();
    }

    private void OpenLogs()
    {
        if (!File.Exists(_logger.CurrentLogPath))
        {
            File.WriteAllText(_logger.CurrentLogPath, string.Empty);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _logger.CurrentLogPath,
            UseShellExecute = true
        });
    }

    private void OpenUpdateWindow()
    {
        using var updateForm = new Forms.UpdateForm(_logger);
        updateForm.ShowDialog();
    }

    private static string FormatYesNo(bool value)
    {
        return value ? "да" : "нет";
    }

    private static void ShowPairingCodeDialog(string code)
    {
        var pairCommand = $"/pair {code}";

        using var form = new Form
        {
            Text = "Код привязки",
            StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Width = 420,
            Height = 260
        };
        UiTheme.ApplyWindow(form);

        var label = new Label
        {
            Text = "Отправь эту команду боту в Telegram:",
            AutoSize = true,
            Location = new Point(24, 20)
        };

        var codeBox = new TextBox
        {
            Text = pairCommand,
            ReadOnly = true,
            Font = new Font("Consolas", 14f, FontStyle.Bold),
            Location = new Point(24, 50),
            Width = 360,
            TextAlign = HorizontalAlignment.Center
        };
        UiTheme.ApplyInput(codeBox);

        var timerLabel = new Label
        {
            Text = "Код действует 10 минут.",
            AutoSize = true,
            Location = new Point(24, 88)
        };
        UiTheme.ApplyMutedText(timerLabel);

        var botLink = new LinkLabel
        {
            Text = "Открыть бота: @WaitDino_bot",
            AutoSize = true,
            Location = new Point(24, 112),
            LinkColor = UiTheme.Accent,
            ActiveLinkColor = UiTheme.Accent,
            VisitedLinkColor = UiTheme.Accent
        };
        botLink.LinkClicked += (_, _) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/WaitDino_bot",
                UseShellExecute = true
            });
        };

        var copyButton = new Button
        {
            Text = "Копировать",
            Width = 170,
            Height = 36,
            Location = new Point(24, 145)
        };
        UiTheme.ApplyPrimaryButton(copyButton);
        copyButton.Click += (_, _) =>
        {
            try
            {
                // Use TextBox selection + Copy to avoid STA thread issues
                codeBox.SelectAll();
                codeBox.Copy();
            }
            catch
            {
                // Fallback: silently ignore if clipboard is not available
            }
            copyButton.Text = "Скопировано!";
            copyButton.Enabled = false;
            var timer = new System.Windows.Forms.Timer { Interval = 1500 };
            timer.Tick += (_, _) =>
            {
                copyButton.Text = "Копировать";
                copyButton.Enabled = true;
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        };

        var closeButton = new Button
        {
            Text = "Закрыть",
            Width = 170,
            Height = 36,
            Location = new Point(214, 145),
            DialogResult = DialogResult.OK
        };
        UiTheme.ApplySecondaryButton(closeButton);

        form.Controls.Add(label);
        form.Controls.Add(codeBox);
        form.Controls.Add(timerLabel);
        form.Controls.Add(botLink);
        form.Controls.Add(copyButton);
        form.Controls.Add(closeButton);
        form.AcceptButton = closeButton;
        form.ShowDialog();
    }
}
