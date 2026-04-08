import { getConfig } from "./config.js";

/**
 * Send a text message to a specific chat.
 * Overload: sendTelegramMessage(text) uses ownerChatId from env (backward compat).
 * Overload: sendTelegramMessageTo(chatId, text, options) targets specific chat.
 */
export async function sendTelegramMessage(text) {
  const { botToken, ownerChatId } = getConfig();
  await sendTelegramMessageTo(ownerChatId, text, { botToken });
}

export async function sendTelegramMessageTo(chatId, text, options = {}) {
  const botToken = options.botToken || getConfig().botToken;
  const replyMarkup = options.replyMarkup || undefined;

  const body = {
    chat_id: chatId,
    text,
    parse_mode: "HTML",
    disable_web_page_preview: true
  };

  if (replyMarkup) {
    body.reply_markup = replyMarkup;
  }

  const response = await fetch(`https://api.telegram.org/bot${botToken}/sendMessage`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body)
  });

  if (!response.ok) {
    const errBody = await response.text();
    throw new Error(`Telegram sendMessage failed: ${response.status} ${errBody}`);
  }

  return response.json();
}

export async function sendTelegramPhoto(chatId, photoBuffer, caption, options = {}) {
  const botToken = options.botToken || getConfig().botToken;
  const form = new FormData();
  form.append("chat_id", String(chatId));
  form.append("caption", caption || "");
  form.append("parse_mode", "HTML");
  form.append("photo", new Blob([photoBuffer], { type: "image/jpeg" }), options.fileName || "photo.jpg");

  const response = await fetch(`https://api.telegram.org/bot${botToken}/sendPhoto`, {
    method: "POST",
    body: form
  });

  if (!response.ok) {
    const errBody = await response.text();
    throw new Error(`Telegram sendPhoto failed: ${response.status} ${errBody}`);
  }

  return response.json();
}

export async function sendTelegramDocument(chatId, docBuffer, fileName, caption, options = {}) {
  const botToken = options.botToken || getConfig().botToken;
  const form = new FormData();
  form.append("chat_id", String(chatId));
  form.append("caption", caption || "");
  form.append("parse_mode", "HTML");
  form.append("document", new Blob([docBuffer], { type: "application/octet-stream" }), fileName || "file");

  const response = await fetch(`https://api.telegram.org/bot${botToken}/sendDocument`, {
    method: "POST",
    body: form
  });

  if (!response.ok) {
    const errBody = await response.text();
    throw new Error(`Telegram sendDocument failed: ${response.status} ${errBody}`);
  }

  return response.json();
}

export async function answerCallbackQuery(callbackQueryId, text, options = {}) {
  const botToken = options.botToken || getConfig().botToken;

  const response = await fetch(`https://api.telegram.org/bot${botToken}/answerCallbackQuery`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      callback_query_id: callbackQueryId,
      text: text || "",
      show_alert: false
    })
  });

  if (!response.ok) {
    const errBody = await response.text();
    throw new Error(`Telegram answerCallbackQuery failed: ${response.status} ${errBody}`);
  }
}

export async function editMessageText(chatId, messageId, text, replyMarkup, options = {}) {
  const botToken = options.botToken || getConfig().botToken;

  const body = {
    chat_id: chatId,
    message_id: messageId,
    text,
    parse_mode: "HTML",
    disable_web_page_preview: true
  };

  if (replyMarkup) {
    body.reply_markup = replyMarkup;
  }

  const response = await fetch(`https://api.telegram.org/bot${botToken}/editMessageText`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body)
  });

  if (!response.ok) {
    const errBody = await response.text();
    // Ignore "message is not modified" errors
    if (!errBody.includes("message is not modified")) {
      throw new Error(`Telegram editMessageText failed: ${response.status} ${errBody}`);
    }
  }
}

export async function sendChatAction(chatId, action = "typing") {
  const botToken = getConfig().botToken;
  await fetch(`https://api.telegram.org/bot${botToken}/sendChatAction`, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ chat_id: chatId, action })
  });
}

export async function deleteMessage(chatId, messageId) {
  const botToken = getConfig().botToken;
  try {
    await fetch(`https://api.telegram.org/bot${botToken}/deleteMessage`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ chat_id: chatId, message_id: messageId })
    });
  } catch {
    // best effort
  }
}
