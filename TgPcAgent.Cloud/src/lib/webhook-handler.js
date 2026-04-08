import { loadRegistry, saveRegistry, completePairing, getUserActiveAgent, listUserAgents, setUserActiveAgent } from "./agent-registry.js";
import { enqueueCommand } from "./command-queue.js";
import { sendTelegramMessageTo, answerCallbackQuery, sendChatAction } from "./telegram.js";

async function sendPendingAndEnqueue(chatId, agentId, commandData) {
  const pendingMsg = await sendTelegramMessageTo(chatId, "\u23f3");
  const pendingMessageId = pendingMsg?.result?.message_id || null;
  return enqueueCommand(agentId, { ...commandData, pendingMessageId });
}

// Commands that the agent handles locally
const AGENT_COMMANDS = new Set([
  "ping", "status", "processes", "screenshot", "apps", "scanapps", "open",
  "lock", "sleep", "auto", "menu", "help", "script", "scripthelp", "scriptstop"
]);

// Commands that require double-confirmation (handled by cloud)
const POWER_COMMANDS = new Set(["shutdown", "restart"]);

// ---------------------------------------------------------------------------
// Main webhook entry point
// ---------------------------------------------------------------------------

export async function handleWebhookUpdate(update) {
  if (update.message?.text) {
    await handleMessage(update.message);
    return;
  }

  if (update.callback_query) {
    await handleCallbackQuery(update.callback_query);
    return;
  }
}

// ---------------------------------------------------------------------------
// Message handling
// ---------------------------------------------------------------------------

async function handleMessage(message) {
  const chatId = message.chat.id;
  const text = (message.text || "").trim();

  if (!text.startsWith("/")) {
    // JSON script detection
    const trimmed = text.trim();
    if ((trimmed.startsWith("{") || trimmed.includes("```")) && trimmed.includes('"steps"')) {
      const registry = await loadRegistry();
      const activeAgent = getUserActiveAgent(registry, chatId);
      if (activeAgent && activeAgent.agent) {
        await sendPendingAndEnqueue(chatId, activeAgent.agentId, {
          chatId,
          text,
          type: "script"
        });
      } else {
        await sendTelegramMessageTo(chatId, buildNoPcMessage());
      }
    }
    return;
  }

  const [rawCommand, ...args] = text.split(/\s+/);
  const command = rawCommand.replace(/^\//, "").replace(/@.*$/, "").toLowerCase();
  const argument = args.join(" ");

  // /start — welcome
  if (command === "start") {
    const startArg = argument.trim();
    // Deep link: /start pair_XXXXXX
    if (startArg.startsWith("pair_")) {
      await handlePairCommand(chatId, startArg.replace("pair_", ""));
      return;
    }
    await sendWelcome(chatId);
    return;
  }

  // /pair CODE
  if (command === "pair") {
    await handlePairCommand(chatId, argument);
    return;
  }

  // /my_pcs — list user's PCs
  if (command === "my_pcs" || command === "mypcs") {
    await handleMyPcs(chatId);
    return;
  }

  // /select NAME — switch active PC
  if (command === "select") {
    await handleSelectPc(chatId, argument);
    return;
  }

  // Check if user has an active agent
  const registry = await loadRegistry();
  const activeAgent = getUserActiveAgent(registry, chatId);
  if (!activeAgent || !activeAgent.agent) {
    await sendTelegramMessageTo(chatId, buildNoPcMessage());
    return;
  }

  // Power commands — double-confirmation on cloud side
  if (POWER_COMMANDS.has(command)) {
    await handlePowerCommand(chatId, command, activeAgent);
    return;
  }

  // Agent commands — enqueue for the agent to pick up
  if (AGENT_COMMANDS.has(command) || command === "unknown") {
    await sendPendingAndEnqueue(chatId, activeAgent.agentId, {
      chatId,
      text,
      type: command
    });
    return;
  }

  // Unknown command — still send to agent (might be /open alias or similar)
  await sendPendingAndEnqueue(chatId, activeAgent.agentId, {
    chatId,
    text,
    type: command === "open" ? "open" : "unknown"
  });
}

// ---------------------------------------------------------------------------
// Callback query handling
// ---------------------------------------------------------------------------

async function handleCallbackQuery(callbackQuery) {
  const chatId = callbackQuery.message?.chat?.id || callbackQuery.from.id;
  const data = callbackQuery.data || "";

  // Power confirmation callbacks (handled by cloud)
  if (data.startsWith("power:")) {
    await handlePowerCallback(chatId, callbackQuery, data);
    return;
  }

  // All other callbacks — forward to agent
  const registry = await loadRegistry();
  const activeAgent = getUserActiveAgent(registry, chatId);
  if (!activeAgent || !activeAgent.agent) {
    await answerCallbackQuery(callbackQuery.id, "No PC linked.");
    return;
  }

  await enqueueCommand(activeAgent.agentId, {
    chatId,
    text: data,
    type: "callback",
    callbackQueryId: callbackQuery.id,
    messageId: callbackQuery.message?.message_id || null
  });
}

// ---------------------------------------------------------------------------
// /pair
// ---------------------------------------------------------------------------

async function handlePairCommand(chatId, code) {
  if (!code || code.trim().length === 0) {
    await sendTelegramMessageTo(chatId, [
      "<b>🔗 Привязка ПК</b>",
      "",
      "Откройте иконку TgPcAgent в трее на ПК.",
      "Нажмите \"Показать код привязки\".",
      "Отправьте: <code>/pair XXXXXX</code>"
    ].join("\n"));
    return;
  }

  const registry = await loadRegistry();
  const result = completePairing(registry, code.trim().toUpperCase(), chatId);
  await saveRegistry(result.registry);

  if (result.success) {
    await sendTelegramMessageTo(chatId, [
      "<b>✅ Привязка завершена!</b>",
      "",
      `🖥 ПК <b>${result.machineName}</b> успешно привязан.`,
      "Теперь вы можете управлять им через команды.\n",
      "Отправьте /help для списка команд."
    ].join("\n"), { replyMarkup: buildMainKeyboard() });
  } else if (result.reason === "code_expired") {
    await sendTelegramMessageTo(chatId, "Код привязки истёк. Сгенерируйте новый в tray-приложении.");
  } else {
    await sendTelegramMessageTo(chatId, "Неверный код привязки. Проверьте и попробуйте снова.");
  }
}

// ---------------------------------------------------------------------------
// /my_pcs
// ---------------------------------------------------------------------------

async function handleMyPcs(chatId) {
  const registry = await loadRegistry();
  const agents = listUserAgents(registry, chatId);
  const activeData = getUserActiveAgent(registry, chatId);

  if (agents.length === 0) {
    await sendTelegramMessageTo(chatId, buildNoPcMessage());
    return;
  }

  const lines = ["<b>💻 Ваши ПК</b>", ""];
  for (const a of agents) {
    const isActive = activeData && activeData.agentId === a.agentId;
    const statusIcon = a.status === "online" ? "🟢" : "🔴";
    const activeMarker = isActive ? " 📌 (активный)" : "";
    lines.push(`${statusIcon} <code>${a.machineName}</code>${activeMarker}`);
  }

  if (agents.length > 1) {
    lines.push("");
    lines.push("Для переключения: <code>/select ИМЯ_ПК</code>");
  }

  await sendTelegramMessageTo(chatId, lines.join("\n"));
}

// ---------------------------------------------------------------------------
// /select
// ---------------------------------------------------------------------------

async function handleSelectPc(chatId, name) {
  if (!name.trim()) {
    await sendTelegramMessageTo(chatId, "Укажите имя ПК: <code>/select ИМЯ_ПК</code>");
    return;
  }

  const registry = await loadRegistry();
  const agents = listUserAgents(registry, chatId);
  const target = agents.find(a => a.machineName.toLowerCase() === name.trim().toLowerCase());

  if (!target) {
    await sendTelegramMessageTo(chatId, `ПК с именем <code>${name}</code> не найден. Проверьте /my_pcs.`);
    return;
  }

  const result = setUserActiveAgent(registry, chatId, target.agentId);
  await saveRegistry(result.registry);

  if (result.success) {
    await sendTelegramMessageTo(chatId, `Активный ПК: <b>${target.machineName}</b>`, { replyMarkup: buildMainKeyboard() });
  } else {
    await sendTelegramMessageTo(chatId, "Не удалось переключить ПК.");
  }
}

// ---------------------------------------------------------------------------
// Power commands (cloud-side double confirmation)
// ---------------------------------------------------------------------------

// In-memory confirmation store (acceptable for serverless — short-lived)
// For production, should use Blob, but for 45-second confirmations it's fine
// because Vercel reuses warm instances frequently enough.
// NOTE: Since serverless can lose state, user may need to re-confirm
// which is actually safer.

async function handlePowerCommand(chatId, command, activeAgent) {
  const confirmId = `${command}:${chatId}:${Date.now()}`;
  const actionTitle = command === "shutdown" ? "Выключение ПК" : "Перезагрузка ПК";

  // Enqueue directly as a power command to agent with step 1 info
  // The agent will handle the double-confirmation locally
  await enqueueCommand(activeAgent.agentId, {
    chatId,
    text: `/${command}`,
    type: command
  });
}

async function handlePowerCallback(chatId, callbackQuery, data) {
  // Power callbacks forwarded to agent as well
  const registry = await loadRegistry();
  const activeAgent = getUserActiveAgent(registry, chatId);
  if (!activeAgent || !activeAgent.agent) {
    await answerCallbackQuery(callbackQuery.id, "No PC linked.");
    return;
  }

  await enqueueCommand(activeAgent.agentId, {
    chatId,
    text: data,
    type: "callback",
    callbackQueryId: callbackQuery.id,
    messageId: callbackQuery.message?.message_id || null
  });
}

// ---------------------------------------------------------------------------
// UI builders
// ---------------------------------------------------------------------------

async function sendWelcome(chatId) {
  const registry = await loadRegistry();
  const activeAgent = getUserActiveAgent(registry, chatId);

  if (activeAgent && activeAgent.agent) {
    await sendTelegramMessageTo(chatId, buildHelpText(activeAgent.agent.machineName), { replyMarkup: buildMainKeyboard() });
  } else {
    await sendTelegramMessageTo(chatId, buildNoPcMessage());
  }
}

function buildNoPcMessage() {
  return [
    "<b>🤖 TgPcAgent</b>",
    "",
    "У вас пока нет привязанных ПК.\n",
    "1️⃣ Установите TgPcAgent на ПК.",
    "2️⃣ Откройте tray-иконку → \"Показать код привязки\".",
    "3️⃣ Отправьте сюда: <code>/pair XXXXXX</code>"
  ].join("\n");
}

function buildHelpText(machineName) {
  return [
    "<b>🤖 TgPcAgent</b>",
    `🖥 Активный ПК: <code>${machineName}</code>`,
    "",
    "<b>Доступные команды:</b>",
    "<code>/ping</code> — 📡 ответ агента + сетевые пинги",
    "<code>/status</code> — 💻 статус ПК, температуры, RAM, диски",
    "<code>/processes</code> — ⚙️ топ процессов по памяти",
    "<code>/screenshot</code> — 📸 снимок экрана",
    "<code>/auto</code> — ⏱ панель авто-пинга и авто-скрина",
    "<code>/apps</code> — 📦 сводка по приложениям",
    "<code>/scanapps</code> — 🔎 список приложений из Пуска",
    "<code>/open alias</code> — 🚀 запустить приложение",
    "<code>/lock</code> / <code>/sleep</code> — 🔒 блокировка / сон",
    "<code>/shutdown</code> / <code>/restart</code> — ⛔️ отключение питания",
    "",
    "<b>🛠 Скрипты:</b>",
    "<code>/scripthelp</code> — 📋 справка + AI промпт",
    "<code>/scriptstop</code> — ⛔️ остановить скрипт",
    "Или просто отправь JSON-скрипт!",
    "",
    "<code>/my_pcs</code> — 💻 список ваших ПК",
    "<code>/select ИМЯ</code> — 🔁 переключить активный ПК"
  ].join("\n");
}

export function buildMainKeyboard() {
  return {
    keyboard: [
      [{ text: "/status" }, { text: "/ping" }],
      [{ text: "/processes" }, { text: "/screenshot" }],
      [{ text: "/apps" }, { text: "/scanapps" }],
      [{ text: "/auto" }, { text: "/scripthelp" }],
      [{ text: "/lock" }, { text: "/sleep" }],
      [{ text: "/shutdown" }, { text: "/restart" }],
      [{ text: "/my_pcs" }]
    ],
    resize_keyboard: true,
    is_persistent: true
  };
}
