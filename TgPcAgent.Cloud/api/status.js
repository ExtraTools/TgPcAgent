import { buildCloudStatusMessage } from "../src/lib/agent-state-machine.js";
import { loadAgentState } from "../src/lib/blob-state.js";
import { sendJson } from "../src/lib/http.js";

export default async function handler(_request, response) {
  const state = await loadAgentState();
  const nowIso = new Date().toISOString();

  return sendJson(response, 200, {
    ok: true,
    nowIso,
    state,
    message: buildCloudStatusMessage(state, nowIso)
  });
}
