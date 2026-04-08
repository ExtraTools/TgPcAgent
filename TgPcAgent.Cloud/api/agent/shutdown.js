import { applyShutdown } from "../../src/lib/agent-state-machine.js";
import { isAuthorizedAgentSecret } from "../../src/lib/auth.js";
import { loadAgentState, saveAgentState } from "../../src/lib/blob-state.js";
import { readJsonBody, sendJson } from "../../src/lib/http.js";
import { sendTelegramMessage } from "../../src/lib/telegram.js";

export default async function handler(request, response) {
  if (request.method !== "POST") {
    return sendJson(response, 405, { ok: false, error: "method_not_allowed" });
  }

  const body = await readJsonBody(request);
  if (!isAuthorizedAgentSecret(body.secret)) {
    return sendJson(response, 401, { ok: false, error: "unauthorized" });
  }

  const nowIso = new Date().toISOString();
  const currentState = await loadAgentState();
  const result = applyShutdown(currentState, {
    reason: body.reason || "agent-exit"
  }, nowIso);

  await saveAgentState(result.state);

  // Legacy notifications disabled — monitoring is now via registry
  // if (result.notification) {
  //   await sendTelegramMessage(result.notification.text);
  // }

  return sendJson(response, 200, {
    ok: true,
    state: result.state,
    notified: Boolean(result.notification)
  });
}
