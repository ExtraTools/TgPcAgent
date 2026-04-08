import { loadRegistry } from "../../src/lib/agent-registry.js";
import { isAuthorizedAgent } from "../../src/lib/auth.js";
import { ackCommand } from "../../src/lib/command-queue.js";
import { readJsonBody, sendJson } from "../../src/lib/http.js";
import { sendTelegramMessageTo, sendTelegramPhoto, sendTelegramDocument, answerCallbackQuery, editMessageText, deleteMessage } from "../../src/lib/telegram.js";
import { buildMainKeyboard } from "../../src/lib/webhook-handler.js";

export default async function handler(request, response) {
  if (request.method !== "POST") {
    return sendJson(response, 405, { ok: false, error: "method_not_allowed" });
  }

  const body = await readJsonBody(request);
  const { agentId, secret, commandId } = body;

  if (!agentId || !secret) {
    return sendJson(response, 400, { ok: false, error: "missing_credentials" });
  }

  const registry = await loadRegistry();
  if (!isAuthorizedAgent(registry, agentId, secret)) {
    return sendJson(response, 401, { ok: false, error: "unauthorized" });
  }

  try {
    // Ack the command (remove from queue)
    if (commandId) {
      await ackCommand(agentId, commandId);
    }

    // Deliver result to Telegram
    const { chatId, text, photoBase64, photoFileName, documentBase64, documentFileName, replyMarkup,
            callbackQueryId, callbackText, editMessageId, editText, editReplyMarkup, pendingMessageId } = body;

    if (chatId && pendingMessageId) {
      await deleteMessage(chatId, pendingMessageId);
    }

    // Handle answerCallbackQuery
    if (callbackQueryId && callbackText !== undefined) {
      try {
        await answerCallbackQuery(callbackQueryId, callbackText);
      } catch (err) {
        console.error("answerCallbackQuery failed:", err.message);
      }
    }

    // Handle editMessageText
    if (chatId && editMessageId && editText) {
      try {
        await editMessageText(chatId, editMessageId, editText, editReplyMarkup || null);
      } catch (err) {
        console.error("editMessageText failed:", err.message);
      }
    }

    let messageId = null;

    // Send photo
    if (chatId && photoBase64) {
      const photoBuffer = Buffer.from(photoBase64, "base64");
      await sendTelegramPhoto(chatId, photoBuffer, text || "", {
        fileName: photoFileName || "screenshot.jpg"
      });
    }
    // Send document
    else if (chatId && documentBase64) {
      const docBuffer = Buffer.from(documentBase64, "base64");
      await sendTelegramDocument(chatId, docBuffer, documentFileName || "file", text || "");
    }
    // Send text message
    else if (chatId && text) {
      const tgResult = await sendTelegramMessageTo(chatId, text, {
        replyMarkup: replyMarkup || buildMainKeyboard()
      });
      if (tgResult && tgResult.result && tgResult.result.message_id) {
        messageId = tgResult.result.message_id;
      }
    }

    return sendJson(response, 200, { ok: true, messageId });
  } catch (error) {
    console.error("Result delivery error:", error);
    return sendJson(response, 500, { ok: false, error: "delivery_failed" });
  }
}
