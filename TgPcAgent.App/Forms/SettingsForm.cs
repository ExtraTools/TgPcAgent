using TgPcAgent.App.Models;
using TgPcAgent.App.Services;
using TgPcAgent.Core.Security;

namespace TgPcAgent.App.Forms;

public sealed class SettingsForm : Form
{
    private readonly ConfigurationService _configurationService;
    private readonly StartupService _startupService;
    private readonly PairingService _pairingService;
    private readonly bool _isInitialSetup;
    private readonly AppConfig _workingCopy;

    private readonly TextBox _cloudUrlTextBox = new();
    private readonly TextBox _agentIdTextBox = new();
    private readonly CheckBox _runAtStartupCheckBox = new();
    private readonly CheckBox _autoUpdateCheckBox = new();
    private readonly CheckBox _silentUpdateCheckBox = new();
    private readonly TextBox _appsTextBox = new();

    public SettingsForm(
        ConfigurationService configurationService,
        StartupService startupService,
        PairingService pairingService,
        bool isInitialSetup = false)
    {
        _configurationService = configurationService;
        _startupService = startupService;
        _pairingService = pairingService;
        _isInitialSetup = isInitialSetup;
        _workingCopy = _configurationService.GetSnapshot();

        Text = isInitialSetup ? "Первичная настройка TgPcAgent" : "Настройки TgPcAgent";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 560);
        Width = 860;
        Height = 680;

        UiTheme.ApplyWindow(this);
        BuildLayout();
        LoadCurrentValues();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
            BackColor = UiTheme.Window
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var heroPanel = BuildHeroPanel();
        var cloudPanel = BuildCloudPanel();
        var appsPanel = BuildAppsPanel();
        var footerPanel = BuildFooterPanel();
        var pathLabel = new Label
        {
            Text = $"Конфиг хранится в: {_configurationService.ConfigPath}",
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            Margin = new Padding(0, 12, 0, 0)
        };
        UiTheme.ApplyMutedText(pathLabel);

        root.Controls.Add(heroPanel, 0, 0);
        root.Controls.Add(cloudPanel, 0, 1);
        root.Controls.Add(appsPanel, 0, 2);
        root.Controls.Add(footerPanel, 0, 3);
        root.Controls.Add(pathLabel, 0, 4);

        Controls.Add(root);
    }

    private Control BuildHeroPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 14)
        };
        UiTheme.ApplyPanel(panel);

        var badge = new Label
        {
            Text = _isInitialSetup ? "ПЕРВЫЙ ЗАПУСК" : "SHARED BOT MODE",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 10)
        };
        badge.ForeColor = UiTheme.Accent;

        var title = new Label
        {
            Text = _isInitialSetup ? "Настрой агента перед первым запуском" : "Управление локальным агентом",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 18f, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 8)
        };

        var subtitle = new Label
        {
            Text = "Агент подключается к общему боту через Cloud Relay. Токен бота хранится в облаке, локально хранятся только учётные данные агента (зашифрованы DPAPI).",
            AutoSize = true,
            MaximumSize = new Size(760, 0)
        };
        UiTheme.ApplyMutedText(subtitle);

        panel.Controls.Add(badge, 0, 0);
        panel.Controls.Add(title, 0, 1);
        panel.Controls.Add(subtitle, 0, 2);
        return panel;
    }

    private Control BuildCloudPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 14)
        };
        UiTheme.ApplyPanel(panel, alternate: true);

        var cloudUrlLabel = new Label
        {
            Text = "Cloud Relay URL",
            AutoSize = true
        };

        _cloudUrlTextBox.Dock = DockStyle.Top;
        _cloudUrlTextBox.ReadOnly = true;
        _cloudUrlTextBox.Margin = new Padding(0, 6, 0, 12);
        UiTheme.ApplyInput(_cloudUrlTextBox);

        var agentIdLabel = new Label
        {
            Text = "Agent ID (только чтение)",
            AutoSize = true
        };

        _agentIdTextBox.Dock = DockStyle.Top;
        _agentIdTextBox.ReadOnly = true;
        _agentIdTextBox.Margin = new Padding(0, 6, 0, 12);
        UiTheme.ApplyInput(_agentIdTextBox);

        _runAtStartupCheckBox.Text = "Запускать вместе с Windows";
        _runAtStartupCheckBox.AutoSize = true;
        UiTheme.ApplyCheckbox(_runAtStartupCheckBox);

        _autoUpdateCheckBox.Text = "Проверять обновления автоматически";
        _autoUpdateCheckBox.AutoSize = true;
        _autoUpdateCheckBox.Margin = new Padding(0, 4, 0, 0);
        UiTheme.ApplyCheckbox(_autoUpdateCheckBox);
        _autoUpdateCheckBox.CheckedChanged += (_, _) => _silentUpdateCheckBox.Enabled = _autoUpdateCheckBox.Checked;

        _silentUpdateCheckBox.Text = "Устанавливать без подтверждения";
        _silentUpdateCheckBox.AutoSize = true;
        _silentUpdateCheckBox.Margin = new Padding(18, 2, 0, 0);
        UiTheme.ApplyCheckbox(_silentUpdateCheckBox);

        panel.Controls.Add(cloudUrlLabel, 0, 0);
        panel.Controls.Add(_cloudUrlTextBox, 0, 1);
        panel.Controls.Add(agentIdLabel, 0, 2);
        panel.Controls.Add(_agentIdTextBox, 0, 3);
        panel.Controls.Add(_runAtStartupCheckBox, 0, 4);
        panel.Controls.Add(_autoUpdateCheckBox, 0, 5);
        panel.Controls.Add(_silentUpdateCheckBox, 0, 6);
        return panel;
    }

    private Control BuildAppsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18)
        };
        UiTheme.ApplyPanel(panel);

        var title = new Label
        {
            Text = "Разрешённые приложения",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold, GraphicsUnit.Point),
            Margin = new Padding(0, 0, 0, 6)
        };

        var hint = new Label
        {
            Text = "Формат строки: alias|path|arguments. По одной записи на строку. Аргументы можно не указывать.",
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Margin = new Padding(0, 0, 0, 10)
        };
        UiTheme.ApplyMutedText(hint);

        _appsTextBox.Multiline = true;
        _appsTextBox.ScrollBars = ScrollBars.Vertical;
        _appsTextBox.Dock = DockStyle.Fill;
        _appsTextBox.Font = new Font("Consolas", 10f, FontStyle.Regular, GraphicsUnit.Point);
        UiTheme.ApplyInput(_appsTextBox);

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(hint, 0, 1);
        panel.Controls.Add(_appsTextBox, 0, 2);
        return panel;
    }

    private Control BuildFooterPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };

        var saveButton = new Button
        {
            Text = _isInitialSetup ? "Сохранить и запустить" : "Сохранить",
            AutoSize = true
        };
        UiTheme.ApplyPrimaryButton(saveButton);
        saveButton.Click += (_, _) => SaveAndClose();

        var cancelButton = new Button
        {
            Text = _isInitialSetup ? "Пропустить пока" : "Отмена",
            AutoSize = true
        };
        UiTheme.ApplySecondaryButton(cancelButton);
        cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);
        return panel;
    }

    private void LoadCurrentValues()
    {
        _cloudUrlTextBox.Text = _workingCopy.CloudRelayUrl ?? CloudRelayDefaults.ProductionUrl;
        _agentIdTextBox.Text = _workingCopy.AgentId ?? "будет создан при запуске";
        _runAtStartupCheckBox.Checked = _startupService.IsEnabled() || _workingCopy.RunAtStartup;
        _autoUpdateCheckBox.Checked = _workingCopy.AutoUpdate;
        _silentUpdateCheckBox.Checked = _workingCopy.SilentUpdate;
        _silentUpdateCheckBox.Enabled = _autoUpdateCheckBox.Checked;
        _appsTextBox.Text = string.Join(
            Environment.NewLine,
            _workingCopy.AllowedApps.Select(app =>
            {
                var arguments = string.IsNullOrWhiteSpace(app.Arguments) ? string.Empty : $"|{app.Arguments}";
                return $"{app.Alias}|{app.TargetPath}{arguments}";
            }));
    }

    private void SaveAndClose()
    {
        try
        {
            _workingCopy.CloudRelayUrl = CloudRelayDefaults.ProductionUrl;
            _workingCopy.RunAtStartup = _runAtStartupCheckBox.Checked;
            _workingCopy.AutoUpdate = _autoUpdateCheckBox.Checked;
            _workingCopy.SilentUpdate = _silentUpdateCheckBox.Checked;
            _workingCopy.AllowedApps = ParseConfiguredApps(_appsTextBox.Lines);
            _configurationService.Save(_workingCopy);
            _startupService.SetEnabled(_workingCopy.RunAtStartup);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Не удалось сохранить настройки",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static List<ConfiguredApp> ParseConfiguredApps(IEnumerable<string> lines)
    {
        var result = new List<ConfiguredApp>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new InvalidOperationException($"Неверная строка приложения: '{line}'. Формат: alias|path|arguments");
            }

            result.Add(new ConfiguredApp
            {
                Alias = parts[0],
                TargetPath = parts[1],
                Arguments = parts.Length == 3 ? parts[2] : null,
                UseShellExecute = true
            });
        }

        return result;
    }
}
