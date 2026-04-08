import { applyHeartbeat } from "../../src/lib/agent-state-machine.js";
import { getConfig } from "../../src/lib/config.js";
import { loadRegistry, saveRegistry, verifyAgent, updateAgentHeartbeat } from "../../src/lib/agent-registry.js";
import { loadAgentState, saveAgentState } from "../../src/lib/blob-state.js";
import { readJsonBody, sendJson } from "../../src/lib/http.js";

export default async function handler(request, response) {
  if (request.method !== "POST") {
    return sendJson(response, 405, { ok: false, error: "method_not_allowed" });
  }

  const body = await readJsonBody(request);
  const { agentId, secret } = body;

  let registry = await loadRegistry();
  const legacyAuth = (() => {
    const { agentSecret } = getConfig();
    return typeof secret === "string" && secret.length > 0 && secret === agentSecret;
  })();
  const registryAuth = agentId && secret && verifyAgent(registry, agentId, secret);

  if (!legacyAuth && !registryAuth) {
    return sendJson(response, 401, { ok: false, error: "unauthorized" });
  }

  const nowIso = new Date().toISOString();
  const heartbeatId = body.heartbeatId || nowIso;
  const currentState = await loadAgentState();
  const result = applyHeartbeat(currentState, {
    machineName: body.machineName || "PC",
    heartbeatId
  }, nowIso);

  await saveAgentState(result.state);

  return sendJson(response, 200, {
    ok: true,
    state: result.state,
    notified: false
  });
}
