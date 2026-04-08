import { handleWebhookUpdate } from "../src/lib/webhook-handler.js";
import { readJsonBody, sendJson } from "../src/lib/http.js";
import { getConfig } from "../src/lib/config.js";

export default async function handler(request, response) {
  if (request.method !== "POST") {
    return sendJson(response, 405, { ok: false, error: "method_not_allowed" });
  }

  // Optional: verify Telegram webhook secret token
  const { webhookSecret } = getConfig();
  if (webhookSecret) {
    const headerSecret = request.headers["x-telegram-bot-api-secret-token"];
    if (headerSecret !== webhookSecret) {
      return sendJson(response, 401, { ok: false, error: "unauthorized" });
    }
  }

  try {
    const update = await readJsonBody(request);
    await handleWebhookUpdate(update);
    return sendJson(response, 200, { ok: true });
  } catch (error) {
    console.error("Webhook handler error:", error);
    // Always return 200 to Telegram to prevent retries
    return sendJson(response, 200, { ok: true, error: "internal" });
  }
}
