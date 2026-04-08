import { applyTimeout } from "../../src/lib/agent-state-machine.js";
import { loadAgentState, saveAgentState } from "../../src/lib/blob-state.js";
import { getConfig } from "../../src/lib/config.js";
import { sendJson } from "../../src/lib/http.js";
import { sendTelegramMessage } from "../../src/lib/telegram.js";

export default async function handler(_request, response) {
  const config = getConfig();
  const currentState = await loadAgentState();
  const nowIso = new Date().toISOString();
  const result = applyTimeout(currentState, nowIso, config.heartbeatTimeoutMs);

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
