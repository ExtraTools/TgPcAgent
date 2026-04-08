using System.Diagnostics;
using System.Net;
using TgPcAgent.App.Models;
using TgPcAgent.App.Scripts;
using TgPcAgent.App.Scripts.Tools;
using TgPcAgent.Core.Automation;
using TgPcAgent.Core.Commands;
using TgPcAgent.Core.Security;

namespace TgPcAgent.App.Services;

public sealed class CommandExecutionService
{
    private const int AppsPageSize = 8;
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromSeconds(45);

    private readonly ConfigurationService _configurationService;
    private readonly FileLogger _logger;
    private readonly IResponseSender _responseSender;
    private readonly BotCommandParser _commandParser;
    private readonly CallbackPayloadParser _callbackPayloadParser;
    private readonly PairingService _pairingService;
    private readonly ConfirmationStore _confirmationStore;
    private readonly SystemStatusService _systemStatusService;
    private readonly ProcessService _processService;
    private readonly ScreenshotService _screenshotService;
    private readonly AppCatalogService _appCatalogService;
    private readonly PowerService _powerService;
    private readonly TrayNotifier _trayNotifier;
    private readonly UiDispatcher _uiDispatcher;
    private readonly ScriptEngine _scriptEngine;

    public CommandExecutionService(
        ConfigurationService configurationService,
        FileLogger logger,
        IResponseSender responseSender,
        PairingService pairingService,
        ConfirmationStore confirmationStore,
        SystemStatusService systemStatusService,
        ProcessService processService,
        ScreenshotService screenshotService,
        AppCatalogService appCatalogService,
        PowerService powerService,
        TrayNotifier trayNotifier,
        UiDispatcher uiDispatcher)
    {
        _configurationService = configurationService;
        _logger = logger;
        _responseSender = responseSender;
        _pairingService = pairingService;
        _confirmationStore = confirmationStore;
        _systemStatusService = systemStatusService;
        _processService = processService;
        _screenshotService = screenshotService;
        _appCatalogService = appCatalogService;
        _powerService = powerService;
        _trayNotifier = trayNotifier;
        _uiDispatcher = uiDispatcher;
        _commandParser = new BotCommandParser();
        _callbackPayloadParser = new CallbackPayloadParser();
        _scriptEngine = BuildScriptEngine(logger);
    }

    private static ScriptEngine BuildScriptEngine(FileLogger logger)
    {
        var engine = new ScriptEngine(logger);
        engine.RegisterTool(new FileCopyTool());
        engine.RegisterTool(new FileMoveTool());
        engine.RegisterTool(new FileDeleteTool());
        engine.RegisterTool(new FileExistsTool());
        engine.RegisterTool(new FileListTool());
        engine.RegisterTool(new FileReadTool());
        engine.RegisterTool(new FileWriteTool());
        engine.RegisterTool(new FileRenameTool());
        engine.RegisterTool(new FileSizeTool());
        engine.RegisterTool(new FileZipTool());
        engine.RegisterTool(new FileUnzipTool());
        engine.RegisterTool(new TgSendFileTool());
        engine.RegisterTool(new NotifyTool());
        engine.RegisterTool(new ScreenshotTool());
        engine.RegisterTool(new CmdRunTool());
        engine.RegisterTool(new AppOpenTool());
        engine.RegisterTool(new AppKillTool());
        engine.RegisterTool(new AppListTool());
        engine.RegisterTool(new ClipboardGetTool());
        engine.RegisterTool(new ClipboardSetTool());
        engine.RegisterTool(new KeySendTool());
        engine.RegisterTool(new TypeTextTool());
        engine.RegisterTool(new VolumeSetTool());
        engine.RegisterTool(new VolumeGetTool());
        engine.RegisterTool(new VolumeMuteTool());
        engine.RegisterTool(new VolumeUnmuteTool());
        engine.RegisterTool(new SysInfoTool());
        engine.RegisterTool(new SysDrivesTool());
        engine.RegisterTool(new SysNetworkTool());
        engine.RegisterTool(new PowerLockTool());
        engine.RegisterTool(new PowerSleepTool());
        engine.RegisterTool(new PowerShutdownTool());
        engine.RegisterTool(new PowerRestartTool());
        engine.RegisterTool(new BrowserOpenTool());
        engine.RegisterTool(new WaitTool());
        engine.RegisterTool(new LogTool());
        return engine;
    }

    /// <summary>
    /// Handle a command received from the cloud queue.
    /// </summary>
    public async Task HandleCloudCommandAsync(CloudCommand cloudCommand, CancellationToken cancellationToken)
    {
        // Set the command ID for ack-ing if the sender supports it
        if (_responseSender is CloudRelayResultSender cloudSender)
        {
            cloudSender.SetCurrentCommandId(cloudCommand.Id);
            cloudSender.SetPendingMessageId(cloudCommand.PendingMessageId);
        }

        try
        {
            if (cloudCommand.Type == "callback")
            {
                // Recreate a minimal callback query structure
                var callbackData = cloudCommand.Text;
                var payload = _callbackPayloadParser.Parse(callbackData);
                await HandleCloudCallbackAsync(cloudCommand.ChatId, payload, cloudCommand.CallbackQueryId, cloudCommand.MessageId, cancellationToken);
                return;
            }

            if (cloudCommand.Type == "script" && !cloudCommand.Text.StartsWith("/"))
            {
                await RunScriptAsync(cloudCommand.ChatId, cloudCommand.Text, cancellationToken);
                return;
            }

            // Parse as a regular command
            var command = _commandParser.Parse(cloudCommand.Text);
            var chatId = cloudCommand.ChatId;

            // In cloud mode, the user is already authorized by the cloud
            // (cloud only enqueues commands for linked agents)
            switch (command.Type)
            {
                case BotCommandType.Ping:
                    var stopwatch = Stopwatch.StartNew();
                    await SendTextAsync(chatId, await BuildPingMessageAsync(stopwatch, cancellationToken, "<b>📡 Пинг</b>"), null, cancellationToken);
                    break;
                case BotCommandType.Status:
                    await SendTextAsync(chatId, BuildStatusMessage(_systemStatusService.Collect()), null, cancellationToken);
                    break;
                case BotCommandType.Processes:
                    await SendTextAsync(chatId, BuildProcessesMessage(_processService.GetTopProcessesByMemory(10)), null, cancellationToken);
                    break;
                case BotCommandType.Screenshot:
                    await SendScreenshotAsync(chatId, cancellationToken);
                    break;
                case BotCommandType.Apps:
                    await SendTextAsync(chatId, BuildAppsSummaryMessage(_configurationService.GetSnapshot()), BuildAppsKeyboard(), cancellationToken);
                    break;
                case BotCommandType.ScanApps:
                    await SendScanAppsPageAsync(chatId, 0, _appCatalogService.ScanStartMenuApps(), cancellationToken);
                    break;
                case BotCommandType.OpenApp:
                    await OpenAppAsync(chatId, command.Argument, cancellationToken);
                    break;
                case BotCommandType.Lock:
                    await _powerService.LockAsync();
                    await SendTextAsync(chatId, "<b>🔒 Блокировка</b>\n\nПК успешно заблокирован.", null, cancellationToken);
                    break;
                case BotCommandType.Auto:
                    await SendAutomationPanelAsync(chatId, cancellationToken);
                    break;
                case BotCommandType.Sleep:
                    await SendTextAsync(chatId, "<b>🌙 Сон</b>\n\nПеревожу ПК в спящий режим...", null, cancellationToken);
                    await _powerService.SleepAsync();
                    break;
                case BotCommandType.Shutdown:
                    await BeginPowerConfirmationAsync(chatId, "shutdown", "⛔️ Выключение ПК", cancellationToken);
                    break;
                case BotCommandType.Restart:
                    await BeginPowerConfirmationAsync(chatId, "restart", "🔄 Перезагрузка ПК", cancellationToken);
                    break;
                case BotCommandType.Script:
                    await RunScriptAsync(chatId, cloudCommand.Text, cancellationToken);
                    break;
                case BotCommandType.ScriptHelp:
                    await SendTextAsync(chatId, BuildScriptHelpText(), null, cancellationToken);
                    break;
                case BotCommandType.ScriptStop:
                    _scriptEngine.Stop();
                    await SendTextAsync(chatId, "<b>⛔️ Скрипт</b>\nПопытка остановки...", null, cancellationToken);
                    break;
                case BotCommandType.Menu:
                case BotCommandType.Help:
                    await SendTextAsync(chatId, BuildHelpText(true), null, cancellationToken);
                    break;
                default:
                    await SendTextAsync(chatId, BuildHelpText(true), null, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Command {cloudCommand.Type} failed.", ex);
            try
            {
                var errMsg = ex.Message.Length > 200 ? ex.Message[..200] + "..." : ex.Message;
                await SendTextAsync(
                    cloudCommand.ChatId,
                    $"<b>❌ Ошибка</b>\n\n<code>{System.Net.WebUtility.HtmlEncode(errMsg)}</code>",
                    null,
                    cancellationToken);
            }
            catch
            {
                // best effort
            }
        }
        finally
        {
            if (_responseSender is CloudRelayResultSender cleanupSender)
            {
                cleanupSender.ClearCurrentCommandId();
            }
        }
    }

    private async Task HandleCloudCallbackAsync(long chatId, CallbackPayload payload, string? callbackQueryId, int? messageId, CancellationToken cancellationToken)
    {
        switch (payload.Type)
        {
            case CallbackActionType.Confirm:
                if (callbackQueryId is not null)
                {
                    await HandleCloudConfirmationCallbackAsync(chatId, callbackQueryId, messageId, payload.Value, cancellationToken);
                }
                break;
            case CallbackActionType.ScanAppsPage:
                await HandleCloudScanAppsPageCallbackAsync(chatId, callbackQueryId, messageId, payload.Value, cancellationToken);
                break;
            case CallbackActionType.OpenApp:
                await HandleCloudOpenAppCallbackAsync(chatId, callbackQueryId, payload.Value, cancellationToken);
                break;
            case CallbackActionType.Automation:
                await HandleCloudAutomationCallbackAsync(chatId, callbackQueryId, messageId, payload.Value, cancellationToken);
                break;
            default:
                if (callbackQueryId is not null)
                {
                    await AnswerCallbackAsync(callbackQueryId, "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u043e\u0435 \u0434\u0435\u0439\u0441\u0442\u0432\u0438\u0435.", cancellationToken);
                }
                break;
        }
    }

    private async Task HandleCloudConfirmationCallbackAsync(long chatId, string callbackQueryId, int? messageId, string? confirmationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(confirmationId))
        {
            await AnswerCallbackAsync(callbackQueryId, "\u041d\u0435\u043a\u043e\u0440\u0440\u0435\u043a\u0442\u043d\u043e\u0435 \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u0435.", cancellationToken);
            return;
        }

        var result = _confirmationStore.Advance(chatId, confirmationId);

        switch (result.Outcome)
        {
            case ConfirmationAdvanceOutcome.AwaitingSecondApproval:
                if (messageId.HasValue)
                {
                    await EditReplyMarkupAsync(chatId, messageId.Value, null, cancellationToken);
                }
                await SendTextAsync(
                    chatId,
                    $"<b>{DescribePowerAction(result.ActionKey)}</b>\n\u0428\u0430\u0433 2/2. \u0424\u0438\u043d\u0430\u043b\u044c\u043d\u043e\u0435 \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u0435 \u0438\u0441\u0442\u0435\u043a\u0430\u0435\u0442 \u0447\u0435\u0440\u0435\u0437 {(int)ConfirmationLifetime.TotalSeconds} \u0441\u0435\u043a.",
                    BuildConfirmationKeyboard(confirmationId, "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0434\u0438\u0442\u044c \u043e\u043a\u043e\u043d\u0447\u0430\u0442\u0435\u043b\u044c\u043d\u043e"),
                    cancellationToken);
                await AnswerCallbackAsync(callbackQueryId, "\u041d\u0443\u0436\u043d\u043e \u0432\u0442\u043e\u0440\u043e\u0435 \u043f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u0435.", cancellationToken);
                break;
            case ConfirmationAdvanceOutcome.Confirmed:
                if (messageId.HasValue)
                {
                    await EditReplyMarkupAsync(chatId, messageId.Value, null, cancellationToken);
                }
                await AnswerCallbackAsync(callbackQueryId, "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u043e.", cancellationToken);
                await ExecutePowerActionAsync(chatId, result.ActionKey, cancellationToken);
                break;
            case ConfirmationAdvanceOutcome.Expired:
                await AnswerCallbackAsync(callbackQueryId, "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u0435 \u0438\u0441\u0442\u0435\u043a\u043b\u043e.", cancellationToken);
                break;
            case ConfirmationAdvanceOutcome.WrongChat:
                await AnswerCallbackAsync(callbackQueryId, "\u042d\u0442\u043e \u043d\u0435 \u0442\u0432\u043e\u044f \u043a\u043d\u043e\u043f\u043a\u0430.", cancellationToken);
                break;
            default:
                await AnswerCallbackAsync(callbackQueryId, "\u041f\u043e\u0434\u0442\u0432\u0435\u0440\u0436\u0434\u0435\u043d\u0438\u0435 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u043e.", cancellationToken);
                break;
        }
    }

    private async Task HandleCloudScanAppsPageCallbackAsync(long chatId, string? callbackQueryId, int? messageId, string? pageValue, CancellationToken cancellationToken)
    {
        if (!int.TryParse(pageValue, out var pageIndex)) pageIndex = 0;
        var apps = _appCatalogService.GetCachedOrScan();
        var page = BuildAppsPage(pageIndex, apps);

        if (messageId.HasValue)
        {
            await EditMessageTextAsync(chatId, messageId.Value, page.Text, page.Keyboard, cancellationToken);
        }
        else
        {
            await SendTextAsync(chatId, page.Text, page.Keyboard, cancellationToken);
        }

        if (callbackQueryId is not null)
        {
            await AnswerCallbackAsync(callbackQueryId, $"\u0421\u0442\u0440\u0430\u043d\u0438\u0446\u0430 {page.CurrentPage + 1}/{page.TotalPages}", cancellationToken);
        }
    }

    private async Task HandleCloudOpenAppCallbackAsync(long chatId, string? callbackQueryId, string? alias, CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        var result = _appCatalogService.TryLaunch(alias ?? string.Empty, config);
        if (callbackQueryId is not null)
        {
            await AnswerCallbackAsync(callbackQueryId, result.Success ? "\u0417\u0430\u043f\u0443\u0441\u043a\u0430\u044e..." : "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u044c.", cancellationToken);
        }
        await SendTextAsync(chatId, result.Message, null, cancellationToken);
    }

    private async Task HandleCloudAutomationCallbackAsync(long chatId, string? callbackQueryId, int? messageId, string? value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (callbackQueryId is not null)
                await AnswerCallbackAsync(callbackQueryId, "\u041d\u0435\u043a\u043e\u0440\u0440\u0435\u043a\u0442\u043d\u0430\u044f \u043a\u043e\u043c\u0430\u043d\u0434\u0430.", cancellationToken);
            return;
        }

        if (value.Equals("open", StringComparison.Ordinal))
        {
            var config = _configurationService.GetSnapshot();
            if (messageId.HasValue)
            {
                await EditMessageTextAsync(chatId, messageId.Value, BuildAutomationMessage(config), BuildAutomationKeyboard(config), cancellationToken);
            }
            if (callbackQueryId is not null)
                await AnswerCallbackAsync(callbackQueryId, "\u041f\u0430\u043d\u0435\u043b\u044c \u043e\u0431\u043d\u043e\u0432\u043b\u0435\u043d\u0430.", cancellationToken);
            return;
        }

        if (value.Equals("run:ping", StringComparison.Ordinal))
        {
            if (callbackQueryId is not null)
                await AnswerCallbackAsync(callbackQueryId, "\u041e\u0442\u043f\u0440\u0430\u0432\u043b\u044f\u044e \u0430\u0432\u0442\u043e-\u043f\u0438\u043d\u0433.", cancellationToken);
            await SendAutoPingAsync(cancellationToken);
            return;
        }

        if (value.Equals("run:screenshot", StringComparison.Ordinal))
        {
            if (callbackQueryId is not null)
                await AnswerCallbackAsync(callbackQueryId, "\u041e\u0442\u043f\u0440\u0430\u0432\u043b\u044f\u044e \u0430\u0432\u0442\u043e-\u0441\u043a\u0440\u0438\u043d.", cancellationToken);
            await SendAutoScreenshotAsync(cancellationToken);
            return;
        }

        if (TryParseAutomationSetAction(value, out var automationKey, out var intervalMinutes))
        {
            var config = _configurationService.GetSnapshot();
            switch (automationKey)
            {
                case "ping":
                    config.AutoPingIntervalMinutes = intervalMinutes;
                    break;
                case "screenshot":
                    config.AutoScreenshotIntervalMinutes = intervalMinutes;
                    break;
                default:
                    if (callbackQueryId is not null)
                        await AnswerCallbackAsync(callbackQueryId, "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u0430\u044f \u0430\u0432\u0442\u043e\u0437\u0430\u0434\u0430\u0447\u0430.", cancellationToken);
                    return;
            }
            _configurationService.Save(config);
            if (messageId.HasValue)
            {
                await EditMessageTextAsync(chatId, messageId.Value, BuildAutomationMessage(config), BuildAutomationKeyboard(config), cancellationToken);
            }
            if (callbackQueryId is not null)
                await AnswerCallbackAsync(callbackQueryId, $"{DescribeAutomationKey(automationKey)}: {AutomationIntervalCatalog.FormatLabel(intervalMinutes)}.", cancellationToken);
            return;
        }

        if (callbackQueryId is not null)
            await AnswerCallbackAsync(callbackQueryId, "\u041d\u0435\u0438\u0437\u0432\u0435\u0441\u0442\u043d\u043e\u0435 \u0434\u0435\u0439\u0441\u0442\u0432\u0438\u0435.", cancellationToken);
    }

    public async Task HandleUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update.Message?.Text is string)
        {
            await HandleMessageAsync(update.Message, cancellationToken);
            return;
        }

        if (update.CallbackQuery is not null)
        {
            await HandleCallbackAsync(update.CallbackQuery, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        var text = message.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var chatId = message.Chat.Id;
        var command = _commandParser.Parse(text);
        var stopwatch = Stopwatch.StartNew();

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) || command.Type == BotCommandType.Menu)
        {
            await SendTextAsync(chatId, BuildHelpText(_pairingService.IsAuthorized(chatId)), null, cancellationToken);
            return;
        }

        if (command.Type == BotCommandType.Pair)
        {
            await HandlePairCommandAsync(chatId, command.Argument, cancellationToken);
            return;
        }

        if (!_pairingService.IsAuthorized(chatId))
        {
            if (!_pairingService.OwnerChatId.HasValue)
            {
                await SendTextAsync(chatId, BuildPairingPrompt(), BuildPairingKeyboard(), cancellationToken, includeMainKeyboard: false);
            }

            return;
        }

        switch (command.Type)
        {
            case BotCommandType.Ping:
                await SendTextAsync(chatId, await BuildPingMessageAsync(stopwatch, cancellationToken, "<b>Пинг</b>"), null, cancellationToken);
                break;
            case BotCommandType.Status:
                await SendTextAsync(chatId, BuildStatusMessage(_systemStatusService.Collect()), null, cancellationToken);
                break;
            case BotCommandType.Processes:
                await SendTextAsync(chatId, BuildProcessesMessage(_processService.GetTopProcessesByMemory(10)), null, cancellationToken);
                break;
            case BotCommandType.Screenshot:
                await SendScreenshotAsync(chatId, cancellationToken);
                break;
            case BotCommandType.Apps:
                await SendTextAsync(chatId, BuildAppsSummaryMessage(_configurationService.GetSnapshot()), BuildAppsKeyboard(), cancellationToken);
                break;
            case BotCommandType.ScanApps:
                await SendScanAppsPageAsync(chatId, 0, _appCatalogService.ScanStartMenuApps(), cancellationToken);
                break;
            case BotCommandType.OpenApp:
                await OpenAppAsync(chatId, command.Argument, cancellationToken);
                break;
            case BotCommandType.Lock:
                await _powerService.LockAsync();
                await SendTextAsync(chatId, "<b>Блокировка</b>\nПК заблокирован.", null, cancellationToken);
                break;
            case BotCommandType.Auto:
                await SendAutomationPanelAsync(chatId, cancellationToken);
                break;
            case BotCommandType.Sleep:
                await SendTextAsync(chatId, "<b>Сон</b>\nПеревожу ПК в спящий режим.", null, cancellationToken);
                await _powerService.SleepAsync();
                break;
            case BotCommandType.Shutdown:
                await BeginPowerConfirmationAsync(chatId, "shutdown", "Выключение ПК", cancellationToken);
                break;
            case BotCommandType.Restart:
                await BeginPowerConfirmationAsync(chatId, "restart", "Перезагрузка ПК", cancellationToken);
                break;
            case BotCommandType.Script:
                await RunScriptAsync(chatId, command.Argument ?? "", cancellationToken);
                break;
            case BotCommandType.ScriptHelp:
                await SendTextAsync(chatId, BuildScriptHelpText(), null, cancellationToken);
                break;
            case BotCommandType.ScriptStop:
                _scriptEngine.Stop();
                await SendTextAsync(chatId, "<b>⛔️ Скрипт</b>\nПопытка остановки...", null, cancellationToken);
                break;
            case BotCommandType.Help:
            case BotCommandType.Unknown:
                await SendTextAsync(chatId, BuildHelpText(true), null, cancellationToken);
                break;
            default:
                await SendTextAsync(chatId, BuildHelpText(true), null, cancellationToken);
                break;
        }
    }

    private async Task HandlePairCommandAsync(long chatId, string? pairingCode, CancellationToken cancellationToken)
    {
        var attempt = _pairingService.TryPair(chatId, pairingCode);

        switch (attempt.Outcome)
        {
            case PairingAttemptOutcome.Paired:
            {
                var config = _configurationService.GetSnapshot();
                config.OwnerChatId = chatId;
                _configurationService.Save(config);
                _trayNotifier.ShowInfo("TgPcAgent", $"Чат {chatId} привязан.");
                await SendTextAsync(
                    chatId,
                    "<b>Привязка завершена</b>\nЖми кнопки ниже или отправь /menu в любой момент.",
                    null,
                    cancellationToken);
                break;
            }
            case PairingAttemptOutcome.CodeExpired:
                await SendTextAsync(chatId, "Код привязки истек. Сгенерируй новый из tray-приложения.", BuildPairingKeyboard(), cancellationToken, includeMainKeyboard: false);
                break;
            case PairingAttemptOutcome.AlreadyPaired:
                if (_pairingService.IsAuthorized(chatId))
                {
                    await SendTextAsync(chatId, "Этот чат уже привязан.", null, cancellationToken);
                }
                break;
            default:
                await SendTextAsync(chatId, "Неверный код привязки.", BuildPairingKeyboard(), cancellationToken, includeMainKeyboard: false);
                break;
        }
    }

    private async Task HandleCallbackAsync(TelegramCallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From.Id;
        if (!_pairingService.IsAuthorized(chatId))
        {
            await AnswerCallbackAsync(callbackQuery.Id, "Нет доступа.", cancellationToken);
            return;
        }

        var payload = _callbackPayloadParser.Parse(callbackQuery.Data);
        switch (payload.Type)
        {
            case CallbackActionType.Confirm:
                await HandleConfirmationCallbackAsync(callbackQuery, payload.Value, cancellationToken);
                break;
            case CallbackActionType.ScanAppsPage:
                await HandleScanAppsPageCallbackAsync(callbackQuery, payload.Value, cancellationToken);
                break;
            case CallbackActionType.OpenApp:
                await HandleOpenAppCallbackAsync(callbackQuery, payload.Value, cancellationToken);
                break;
            case CallbackActionType.Automation:
                await HandleAutomationCallbackAsync(callbackQuery, payload.Value, cancellationToken);
                break;
            default:
                await AnswerCallbackAsync(callbackQuery.Id, "Неизвестное действие.", cancellationToken);
                break;
        }
    }

    private async Task HandleConfirmationCallbackAsync(TelegramCallbackQuery callbackQuery, string? confirmationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(confirmationId))
        {
            await AnswerCallbackAsync(callbackQuery.Id, "Некорректное подтверждение.", cancellationToken);
            return;
        }

        var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From.Id;
        var result = _confirmationStore.Advance(chatId, confirmationId);

        switch (result.Outcome)
        {
            case ConfirmationAdvanceOutcome.AwaitingSecondApproval:
            {
                if (callbackQuery.Message is not null)
                {
                    await EditReplyMarkupAsync(chatId, callbackQuery.Message.MessageId, null, cancellationToken);
                }

                await SendTextAsync(
                    chatId,
                    $"<b>{DescribePowerAction(result.ActionKey)}</b>\nШаг 2/2. Финальное подтверждение истекает через {(int)ConfirmationLifetime.TotalSeconds} сек.",
                    BuildConfirmationKeyboard(confirmationId, "Подтвердить окончательно"),
                    cancellationToken);
                await AnswerCallbackAsync(callbackQuery.Id, "Нужно второе подтверждение.", cancellationToken);
                break;
            }
            case ConfirmationAdvanceOutcome.Confirmed:
            {
                if (callbackQuery.Message is not null)
                {
                    await EditReplyMarkupAsync(chatId, callbackQuery.Message.MessageId, null, cancellationToken);
                }

                await AnswerCallbackAsync(callbackQuery.Id, "Подтверждено.", cancellationToken);
                await ExecutePowerActionAsync(chatId, result.ActionKey, cancellationToken);
                break;
            }
            case ConfirmationAdvanceOutcome.Expired:
                await AnswerCallbackAsync(callbackQuery.Id, "Подтверждение истекло.", cancellationToken);
                break;
            case ConfirmationAdvanceOutcome.WrongChat:
                await AnswerCallbackAsync(callbackQuery.Id, "Это не твоя кнопка.", cancellationToken);
                break;
            default:
                await AnswerCallbackAsync(callbackQuery.Id, "Подтверждение не найдено.", cancellationToken);
                break;
        }
    }

    private async Task HandleScanAppsPageCallbackAsync(TelegramCallbackQuery callbackQuery, string? pageValue, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message is null)
        {
            await AnswerCallbackAsync(callbackQuery.Id, "Сообщение не найдено.", cancellationToken);
            return;
        }

        if (!int.TryParse(pageValue, out var pageIndex))
        {
            pageIndex = 0;
        }

        var apps = _appCatalogService.GetCachedOrScan();
        var page = BuildAppsPage(pageIndex, apps);

        await EditMessageTextAsync(
            callbackQuery.Message.Chat.Id,
            callbackQuery.Message.MessageId,
            page.Text,
            page.Keyboard,
            cancellationToken);
        await AnswerCallbackAsync(callbackQuery.Id, $"Страница {page.CurrentPage + 1}/{page.TotalPages}", cancellationToken);
    }

    private async Task HandleOpenAppCallbackAsync(TelegramCallbackQuery callbackQuery, string? alias, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From.Id;
        var config = _configurationService.GetSnapshot();
        var result = _appCatalogService.TryLaunch(alias ?? string.Empty, config);

        await AnswerCallbackAsync(callbackQuery.Id, result.Success ? "Запускаю..." : "Не удалось запустить.", cancellationToken);
        await SendTextAsync(chatId, result.Message, null, cancellationToken);
    }

    private async Task HandleAutomationCallbackAsync(TelegramCallbackQuery callbackQuery, string? value, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message?.Chat.Id ?? callbackQuery.From.Id;
        if (string.IsNullOrWhiteSpace(value))
        {
            await AnswerCallbackAsync(callbackQuery.Id, "Некорректная команда.", cancellationToken);
            return;
        }

        if (value.Equals("open", StringComparison.Ordinal))
        {
            await RefreshAutomationPanelAsync(callbackQuery, chatId, "Панель обновлена.", cancellationToken);
            return;
        }

        if (value.Equals("run:ping", StringComparison.Ordinal))
        {
            await AnswerCallbackAsync(callbackQuery.Id, "Отправляю авто-пинг.", cancellationToken);
            await SendAutoPingAsync(cancellationToken);
            return;
        }

        if (value.Equals("run:screenshot", StringComparison.Ordinal))
        {
            await AnswerCallbackAsync(callbackQuery.Id, "Отправляю авто-скрин.", cancellationToken);
            await SendAutoScreenshotAsync(cancellationToken);
            return;
        }

        if (TryParseAutomationSetAction(value, out var automationKey, out var intervalMinutes))
        {
            var config = _configurationService.GetSnapshot();

            switch (automationKey)
            {
                case "ping":
                    config.AutoPingIntervalMinutes = intervalMinutes;
                    break;
                case "screenshot":
                    config.AutoScreenshotIntervalMinutes = intervalMinutes;
                    break;
                default:
                    await AnswerCallbackAsync(callbackQuery.Id, "Неизвестная автозадача.", cancellationToken);
                    return;
            }

            _configurationService.Save(config);
            await RefreshAutomationPanelAsync(
                callbackQuery,
                chatId,
                $"{DescribeAutomationKey(automationKey)}: {AutomationIntervalCatalog.FormatLabel(intervalMinutes)}.",
                cancellationToken);
            return;
        }

        await AnswerCallbackAsync(callbackQuery.Id, "Неизвестное действие.", cancellationToken);
    }

    private async Task BeginPowerConfirmationAsync(long chatId, string actionKey, string actionTitle, CancellationToken cancellationToken)
    {
        var confirmation = _confirmationStore.Create(chatId, actionKey, ConfirmationLifetime);
        await SendTextAsync(
            chatId,
            $"<b>{WebUtility.HtmlEncode(actionTitle)}</b>\nШаг 1/2. Подтверждение истекает через {(int)ConfirmationLifetime.TotalSeconds} сек.",
            BuildConfirmationKeyboard(confirmation.Id, "Подтвердить 1/2"),
            cancellationToken);
    }

    private async Task ExecutePowerActionAsync(long chatId, string? actionKey, CancellationToken cancellationToken)
    {
        switch (actionKey)
        {
            case "shutdown":
                await SendTextAsync(chatId, "<b>Выключение</b>\nПодтверждено. Выключаю ПК.", null, cancellationToken);
                await _powerService.ShutdownAsync();
                break;
            case "restart":
                await SendTextAsync(chatId, "<b>Перезагрузка</b>\nПодтверждено. Перезагружаю ПК.", null, cancellationToken);
                await _powerService.RestartAsync();
                break;
            default:
                await SendTextAsync(chatId, "Неизвестное действие питания.", null, cancellationToken);
                break;
        }
    }

    private async Task OpenAppAsync(long chatId, string? requestedAlias, CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        var result = _appCatalogService.TryLaunch(requestedAlias ?? string.Empty, config);
        await SendTextAsync(chatId, result.Message, null, cancellationToken);
    }

    private async Task SendScreenshotAsync(long chatId, CancellationToken cancellationToken)
    {
        await SendScreenshotDocumentAsync(
            chatId,
            "<b>Скриншот</b>",
            notifyLocalUser: true,
            cancellationToken);
    }

    public async Task SendAutoPingAsync(CancellationToken cancellationToken)
    {
        var ownerChatId = _configurationService.GetSnapshot().OwnerChatId;
        if (!ownerChatId.HasValue)
        {
            return;
        }

        await SendTextAsync(
            ownerChatId.Value,
            await BuildPingMessageAsync(null, cancellationToken, "<b>📡 Авто-пинг</b>"),
            null,
            cancellationToken);
    }

    public async Task SendAutoScreenshotAsync(CancellationToken cancellationToken)
    {
        var ownerChatId = _configurationService.GetSnapshot().OwnerChatId;
        if (!ownerChatId.HasValue)
        {
            return;
        }

        await SendScreenshotDocumentAsync(
            ownerChatId.Value,
            "<b>📸 Авто-скриншот</b>",
            notifyLocalUser: false,
            cancellationToken);
    }

    private async Task<string> BuildPingMessageAsync(Stopwatch? stopwatch, CancellationToken cancellationToken, string title)
    {
        var results = await _systemStatusService.PingDefaultHostsAsync(cancellationToken);
        var lines = new List<string>
        {
            title,
            $"<b>⏱ Время:</b> <code>{WebUtility.HtmlEncode(MoscowTimeService.FormatNow())}</code>\n"
        };

        if (stopwatch is not null)
        {
            lines.Add($"<b>⚡️ Ответ:</b> <code>{stopwatch.ElapsedMilliseconds} мс</code>");
        }

        foreach (var result in results)
        {
            if (result.Success)
            {
                lines.Add($"🌐 <b>{result.Host}</b> ➔ <code>{result.RoundtripTimeMs} мс</code>");
            }
            else
            {
                lines.Add($"❌ <b>{result.Host}</b> ➔ <i>ошибка</i> (<code>{WebUtility.HtmlEncode(result.Error ?? "неизвестно")}</code>)");
            }
        }

        return string.Join('\n', lines);
    }

    private string BuildStatusMessage(SystemStatusSnapshot snapshot)
    {
        var usedMemoryMb = snapshot.TotalMemoryMb - snapshot.AvailableMemoryMb;
        var driveLines = snapshot.Drives.Count == 0
            ? "<i>Нет данных по дискам.</i>"
            : string.Join('\n', snapshot.Drives.Select(drive =>
                $"▪️ <b>{WebUtility.HtmlEncode(drive.Name)}</b> ➔ <code>{drive.FreeGb:0.0} / {drive.TotalGb:0.0} ГБ</code>"));
        var ipText = snapshot.LocalIpv4Addresses.Count == 0
            ? "<i>нет</i>"
            : string.Join(", ", snapshot.LocalIpv4Addresses.Select(x => $"<code>{WebUtility.HtmlEncode(x)}</code>"));

        return string.Join('\n', new[]
        {
            "<b>💻 Статус ПК</b>",
            $"<b>⏱ Время:</b> <code>{WebUtility.HtmlEncode(MoscowTimeService.FormatNow())}</code>",
            $"<b>🖥 Компьютер:</b> <code>{WebUtility.HtmlEncode(snapshot.MachineName)}</code>",
            $"<b>🪟 ОС:</b> <code>{WebUtility.HtmlEncode(snapshot.OsDescription)}</code>",
            $"<b>🟢 Аптайм:</b> <code>{FormatUptime(snapshot.Uptime)}</code>\n",
            $"<b>⚡️ Загрузка CPU:</b> <code>{FormatPercent(snapshot.Hardware.CpuLoadPercent)}</code>",
            $"<b>🔥 Темп. CPU:</b> <code>{FormatTemperature(snapshot.Hardware.CpuTemperatureCelsius)}</code>",
            $"<b>🎮 Темп. GPU:</b> <code>{FormatTemperature(snapshot.Hardware.GpuTemperatureCelsius)}</code>",
            $"<b>🧠 RAM:</b> <code>{usedMemoryMb} / {snapshot.TotalMemoryMb} МБ</code>",
            $"<b>🌐 IPv4:</b> {ipText}\n",
            "<b>💽 Диски:</b>",
            driveLines
        });
    }

    private static string BuildProcessesMessage(IReadOnlyList<ProcessSnapshot> processes)
    {
        var lines = new List<string>
        {
            "<b>⚙️ Топ процессов</b>",
            $"<b>⏱ Время:</b> <code>{WebUtility.HtmlEncode(MoscowTimeService.FormatNow())}</code>\n"
        };
        lines.AddRange(processes.Select(process =>
            $"▪️ <b>{WebUtility.HtmlEncode(process.Name)}</b> (PID <code>{process.Id}</code>) ➔ <code>{process.MemoryMb:0.0} МБ</code>"));
        return string.Join('\n', lines);
    }

    private string BuildAppsSummaryMessage(AppConfig config)
    {
        var configuredApps = _appCatalogService.GetConfiguredApps(config);
        var scannedApps = _appCatalogService.GetCachedOrScan();

        var lines = new List<string>
        {
            "<b>📦 Приложения</b>",
            $"<b>📌 Сохранено:</b> <code>{configuredApps.Count}</code>",
            $"<b>🔎 Меню Пуск:</b> <code>{scannedApps.Count}</code>\n",
            "Для запуска: <code>/open alias</code>\n",
            "<i>Или воспользуйтесь кнопкой ниже (<code>/scanapps</code>).</i>"
        };

        if (configuredApps.Count > 0)
        {
            lines.Add("\n<b>📌 Сохраненные алиасы:</b>");
            lines.AddRange(configuredApps.Take(10).Select(app =>
                $"▪️ <code>{WebUtility.HtmlEncode(app.Alias)}</code> ➔ <b>{WebUtility.HtmlEncode(app.DisplayName)}</b>"));
        }

        return string.Join('\n', lines);
    }

    private async Task SendScanAppsPageAsync(long chatId, int pageIndex, IReadOnlyList<DiscoveredApp> apps, CancellationToken cancellationToken)
    {
        var page = BuildAppsPage(pageIndex, apps);
        await SendTextAsync(chatId, page.Text, page.Keyboard, cancellationToken);
    }

    private AppsPage BuildAppsPage(int pageIndex, IReadOnlyList<DiscoveredApp> apps)
    {
        if (apps.Count == 0)
        {
            return new AppsPage(
                0,
                1,
                "<b>🔎 Скан приложений</b>\nВ меню Пуск ничего не найдено.",
                null);
        }

        var totalPages = (int)Math.Ceiling(apps.Count / (double)AppsPageSize);
        var currentPage = Math.Clamp(pageIndex, 0, Math.Max(totalPages - 1, 0));
        var items = apps
            .Skip(currentPage * AppsPageSize)
            .Take(AppsPageSize)
            .ToList();

        var lines = new List<string>
        {
            $"<b>🔎 Приложения </b>(<code>{currentPage + 1}/{totalPages}</code>)",
            $"<b>📌 Всего найдено:</b> <code>{apps.Count}</code>\n",
            "<i>Выберите приложение для запуска:</i>"
        };
        lines.AddRange(items.Select((app, index) =>
            $"<b>{currentPage * AppsPageSize + index + 1}.</b> <code>{WebUtility.HtmlEncode(app.Alias)}</code> ➔ {WebUtility.HtmlEncode(app.DisplayName)}"));

        var keyboardRows = new List<object>();
        keyboardRows.AddRange(items.Select(app => new[]
        {
            new
            {
                text = TrimButtonLabel(app.DisplayName),
                callback_data = $"scanapps:open:{app.Alias}"
            }
        }));

        var navigationRow = new List<object>();
        if (currentPage > 0)
        {
            navigationRow.Add(new
            {
                text = "Назад",
                callback_data = $"scanapps:page:{currentPage - 1}"
            });
        }

        navigationRow.Add(new
        {
            text = $"{currentPage + 1}/{totalPages}",
            callback_data = $"scanapps:page:{currentPage}"
        });

        if (currentPage < totalPages - 1)
        {
            navigationRow.Add(new
            {
                text = "Далее",
                callback_data = $"scanapps:page:{currentPage + 1}"
            });
        }

        keyboardRows.Add(navigationRow.ToArray());

        return new AppsPage(
            currentPage,
            totalPages,
            string.Join('\n', lines),
            new
            {
                inline_keyboard = keyboardRows.ToArray()
            });
    }

    private static string BuildHelpText(bool authorized)
    {
        if (!authorized)
        {
            return """
                   <b>🤖 TgPcAgent</b>

                   Бот заработает только после локальной привязки.
                   Открой tray-приложение и отправь:
                   <code>/pair 123456</code>
                   """;
        }

        return """
               <b>🤖 TgPcAgent</b>

               <b>Доступные команды:</b>
               <code>/ping</code> — 📡 ответ агента + сетевые пинги
               <code>/status</code> — 💻 статус ПК, температуры, RAM, диски
               <code>/processes</code> — ⚙️ топ процессов по памяти
               <code>/screenshot</code> — 📸 снимок экрана
               <code>/auto</code> — ⏱ панель авто-пинга и авто-скрина
               <code>/apps</code> — 📦 сводка по приложениям
               <code>/scanapps</code> — 🔎 список приложений из Пуска
               <code>/open alias</code> — 🚀 запустить приложение
               <code>/lock</code> / <code>/sleep</code> — 🔒 блокировка / сон
               <code>/shutdown</code> / <code>/restart</code> — ⛔️ отключение питания

               <b>🔧 Скрипты:</b>
               <code>/scripthelp</code> — 📋 справка по скриптам + AI промпт
               <code>/scriptstop</code> — ⛔️ остановить скрипт
               Или просто отправь JSON-скрипт в чат!
               """;
    }

    private static string BuildPairingPrompt()
    {
        return """
               <b>🔗 Нужна привязка</b>

               1️⃣ Открой иконку TgPcAgent в трее на ПК.
               2️⃣ Нажми "Показать код привязки".
               3️⃣ Отправь сюда:
               <code>/pair 123456</code>
               """;
    }

    private static object BuildPairingKeyboard()
    {
        return new
        {
            keyboard = new[]
            {
                new[]
                {
                    new { text = "/start" }
                }
            },
            resize_keyboard = true,
            is_persistent = true
        };
    }

    private static object BuildMainMenuKeyboard()
    {
        return new
        {
            keyboard = new[]
            {
                new[] { new { text = "/status" }, new { text = "/ping" } },
                new[] { new { text = "/processes" }, new { text = "/screenshot" } },
                new[] { new { text = "/apps" }, new { text = "/scanapps" } },
                new[] { new { text = "/auto" }, new { text = "/menu" } },
                new[] { new { text = "/lock" }, new { text = "/sleep" } },
                new[] { new { text = "/shutdown" }, new { text = "/restart" } }
            },
            resize_keyboard = true,
            is_persistent = true
        };
    }

    private static object BuildAppsKeyboard()
    {
        return new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new
                    {
                        text = "Открыть список найденных приложений",
                        callback_data = "scanapps:page:0"
                    }
                }
            }
        };
    }

    private static object BuildConfirmationKeyboard(string confirmationId, string buttonText)
    {
        return new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new
                    {
                        text = buttonText,
                        callback_data = $"confirm:{confirmationId}"
                    }
                }
            }
        };
    }

    private static string DescribePowerAction(string? actionKey)
    {
        return actionKey switch
        {
            "shutdown" => "Выключение ПК",
            "restart" => "Перезагрузка ПК",
            _ => "Действие питания"
        };
    }

    private static string FormatTemperature(double? temperature)
    {
        return temperature.HasValue ? $"{temperature.Value:0.0} C" : "нет";
    }

    private static string FormatPercent(double? percent)
    {
        return percent.HasValue ? $"{percent.Value:0.0} %" : "нет";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        return $"{(int)uptime.TotalDays}д {uptime.Hours}ч {uptime.Minutes}м";
    }

    private static string TrimButtonLabel(string input)
    {
        const int maxLength = 28;
        return input.Length <= maxLength ? input : $"{input[..(maxLength - 3)]}...";
    }

    private async Task SendAutomationPanelAsync(long chatId, CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        await SendTextAsync(chatId, BuildAutomationMessage(config), BuildAutomationKeyboard(config), cancellationToken);
    }

    private async Task RefreshAutomationPanelAsync(TelegramCallbackQuery callbackQuery, long chatId, string callbackText, CancellationToken cancellationToken)
    {
        var config = _configurationService.GetSnapshot();
        if (callbackQuery.Message is not null)
        {
            await EditMessageTextAsync(
                chatId,
                callbackQuery.Message.MessageId,
                BuildAutomationMessage(config),
                BuildAutomationKeyboard(config),
                cancellationToken);
        }

        await AnswerCallbackAsync(callbackQuery.Id, callbackText, cancellationToken);
    }

    private async Task SendScreenshotDocumentAsync(long chatId, string title, bool notifyLocalUser, CancellationToken cancellationToken)
    {
        if (notifyLocalUser)
        {
            _trayNotifier.ShowInfo("TgPcAgent", "Получен авторизованный запрос на скриншот.");
        }

        var screenshot = await _uiDispatcher.InvokeAsync(() => _screenshotService.CaptureVirtualScreen());
        await SendDocumentAsync(
            chatId,
            screenshot.Content,
            $"desktop-{DateTime.Now:yyyyMMdd-HHmmss}.jpg",
            $"{title}\n<b>📐 Разрешение:</b> <code>{screenshot.Width}x{screenshot.Height}</code>\n<b>⏱ Время:</b> <code>{WebUtility.HtmlEncode(MoscowTimeService.FormatNow())}</code>",
            cancellationToken);
    }

    private static bool TryParseAutomationSetAction(string rawValue, out string automationKey, out int intervalMinutes)
    {
        automationKey = string.Empty;
        intervalMinutes = 0;

        var parts = rawValue.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !parts[0].Equals("set", StringComparison.Ordinal))
        {
            return false;
        }

        automationKey = parts[1];
        if (!int.TryParse(parts[2], out intervalMinutes))
        {
            return false;
        }

        return AutomationIntervalCatalog.IsSupported(intervalMinutes);
    }

    private static string BuildAutomationMessage(AppConfig config)
    {
        return string.Join('\n', new[]
        {
            "<b>⏱ Автозадачи</b>",
            $"<b>⏱ Время:</b> <code>{WebUtility.HtmlEncode(MoscowTimeService.FormatNow())}</code>\n",
            $"<b>📸 Скриншоты:</b> <code>{WebUtility.HtmlEncode(AutomationIntervalCatalog.FormatLabel(config.AutoScreenshotIntervalMinutes))}</code>",
            $"<b>📡 Пинги:</b> <code>{WebUtility.HtmlEncode(AutomationIntervalCatalog.FormatLabel(config.AutoPingIntervalMinutes))}</code>\n",
            "<i>Выберите интервал для автоматических задач или запустите их вручную.</i>"
        });
    }

    private static object BuildAutomationKeyboard(AppConfig config)
    {
        var rows = new List<object>
        {
            BuildAutomationIntervalRow("screenshot", config.AutoScreenshotIntervalMinutes, 0, 1, 5, 15),
            BuildAutomationIntervalRow("screenshot", config.AutoScreenshotIntervalMinutes, 30, 60),
            BuildAutomationIntervalRow("ping", config.AutoPingIntervalMinutes, 0, 1, 5, 15),
            BuildAutomationIntervalRow("ping", config.AutoPingIntervalMinutes, 30, 60),
            new[]
            {
                BuildInlineButton("Скрин сейчас", "auto:run:screenshot"),
                BuildInlineButton("Пинг сейчас", "auto:run:ping")
            },
            new[]
            {
                BuildInlineButton("Обновить", "auto:open")
            }
        };

        return new
        {
            inline_keyboard = rows.ToArray()
        };
    }

    private static object[] BuildAutomationIntervalRow(string automationKey, int currentMinutes, params int[] values)
    {
        return values
            .Select(value => BuildInlineButton(
                value == currentMinutes ? $"● {FormatAutomationButtonLabel(value)}" : FormatAutomationButtonLabel(value),
                $"auto:set:{automationKey}:{value}"))
            .ToArray();
    }

    private static object BuildInlineButton(string text, string callbackData)
    {
        return new
        {
            text,
            callback_data = callbackData
        };
    }

    private static string FormatAutomationButtonLabel(int minutes)
    {
        return minutes switch
        {
            0 => "Выкл",
            1 => "1м",
            60 => "1ч",
            _ => $"{minutes}м"
        };
    }

    private static string DescribeAutomationKey(string automationKey)
    {
        return automationKey switch
        {
            "ping" => "Авто-пинг",
            "screenshot" => "Авто-скрин",
            _ => "Автозадача"
        };
    }

    private async Task SendTextAsync(long chatId, string text, object? replyMarkup, CancellationToken cancellationToken, bool includeMainKeyboard = true)
    {
        var effectiveMarkup = replyMarkup;
        if (effectiveMarkup is null && includeMainKeyboard && _pairingService.IsAuthorized(chatId))
        {
            effectiveMarkup = BuildMainMenuKeyboard();
        }

        await _responseSender.SendTextAsync(chatId, text, effectiveMarkup, cancellationToken);
    }

    private async Task SendDocumentAsync(long chatId, byte[] content, string fileName, string caption, CancellationToken cancellationToken)
    {
        await _responseSender.SendDocumentAsync(chatId, content, fileName, caption, cancellationToken);
    }

    private async Task EditMessageTextAsync(long chatId, int messageId, string text, object? replyMarkup, CancellationToken cancellationToken)
    {
        await _responseSender.EditMessageTextAsync(chatId, messageId, text, replyMarkup, cancellationToken);
    }

    private async Task EditReplyMarkupAsync(long chatId, int messageId, object? replyMarkup, CancellationToken cancellationToken)
    {
        await _responseSender.EditReplyMarkupAsync(chatId, messageId, replyMarkup, cancellationToken);
    }

    private async Task AnswerCallbackAsync(string callbackId, string text, CancellationToken cancellationToken)
    {
        await _responseSender.AnswerCallbackAsync(callbackId, text, cancellationToken);
    }

    private sealed record AppsPage(int CurrentPage, int TotalPages, string Text, object? Keyboard);

    private async Task RunScriptAsync(long chatId, string rawText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            await SendTextAsync(chatId, "<b>❌ Пустой скрипт</b>\nОтправь JSON-скрипт или /scripthelp для справки.", null, cancellationToken);
            return;
        }

        var ctx = new Scripts.ScriptContext(chatId, _responseSender, _screenshotService, _logger);
        await _scriptEngine.ExecuteAsync(rawText, ctx, cancellationToken);
    }

    private string BuildScriptHelpText()
    {
        var toolsList = string.Join("\n", _scriptEngine.Tools.Values
            .Select(t => $"<code>{t.Name}</code> — {t.Description}"));

        return $"""
               <b>🔧 Движок скриптов TgPcAgent</b>

               Отправь JSON-скрипт прямо в чат (или через <code>/script JSON</code>).
               Скрипт выполнится пошагово с отчётом в реальном времени.

               <b>📋 Доступные инструменты ({_scriptEngine.Tools.Count}):</b>
               {toolsList}

               <b>📝 Формат скрипта:</b>
               <pre>{WebUtility.HtmlEncode(@"{
                 ""name"": ""Мой скрипт"",
                 ""stopOnError"": true,
                 ""steps"": [
                   { ""action"": ""cmd.run"", ""args"": { ""command"": ""echo Hello"" } },
                   { ""action"": ""notify"", ""args"": { ""text"": ""Готово!"" } }
                 ]
               }")}</pre>

               Алиасы: <code>action</code> = <code>tool</code>, <code>args</code> = <code>params</code> (оба работают).

               <b>🤖 Промпт для AI (скопируй в ChatGPT/Claude):</b>
               <pre>{WebUtility.HtmlEncode(@"Ты — генератор автоматизаций для ПК. Выдай ТОЛЬКО валидный JSON (без маркдауна и комментариев).
               ПРАВИЛА:
               1. В путях Windows ВСЕГДА используй прямые слеши / (например C:/data/log.txt)
               2. Файловые инструменты работают с конкретными файлами. Маски типа *.log запрещены (для них используй cmd.run)
               3. Используй формат: {""action"": ""имя"", ""args"": {...}}

               Схема:
               {""name"": ""Название"", ""stopOnError"": true, ""steps"": [{""action"": ""название"", ""args"": {...}, ""ignoreError"": false}]}

               Инструменты:
               - file.copy {""from"":""..."", ""to"":""...""}
               - file.move {""from"":""..."", ""to"":""...""}
               - file.delete {""path"":""...""}
               - file.exists {""path"":""...""}
               - file.list {""path"":""...""}
               - file.read {""path"":""...""}
               - file.write {""path"":""..."", ""content"":""...""}
               - file.rename {""path"":""..."", ""newName"":""...""}
               - file.size {""path"":""...""}
               - file.zip {""path"":""..."", ""zipPath"":""...""}
               - file.unzip {""zipPath"":""..."", ""to"":""...""}
               - tg.sendFile {""path"":""...""}  (отправить файл в Telegram, макс 50МБ)
               - cmd.run {""command"":""...""}  (таймаут 30с)
               - app.open {""path"":""notepad.exe""}
               - app.kill {""name"":""notepad""}
               - app.list {}
               - clipboard.get {}
               - clipboard.set {""text"":""...""}
               - key.send {""keys"":""Ctrl+C""}
               - type.text {""text"":""...""}
               - volume.set {""level"":50}
               - volume.get {}
               - volume.mute {} / volume.unmute {}
               - sys.info {} / sys.drives {} / sys.network {}
               - power.lock {} / power.sleep {} / power.shutdown {} / power.restart {}
               - browser.open {""url"":""...""}
               - screenshot {}
               - wait {""ms"":5000}
               - notify {""text"":""...""}
               - log {""text"":""...""}

               Моя задача: [ ОПИШИ ЗАДАЧУ ]")}</pre>

               <b>⛔️ Остановка:</b> /scriptstop
               """;
    }
}
