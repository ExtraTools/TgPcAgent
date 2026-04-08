import { loadRegistry } from "../../src/lib/agent-registry.js";
import { sendJson } from "../../src/lib/http.js";

export default async function handler(request, response) {
  if (request.method !== "GET") {
    return sendJson(response, 405, { ok: false });
  }

  const registry = await loadRegistry();

  return sendJson(response, 200, {
    ok: true,
    storage: "upstash-redis",
    users: registry.users,
    agents: Object.fromEntries(
      Object.entries(registry.agents).map(([id, a]) => [id, { ownerChatId: a.ownerChatId, status: a.status, machineName: a.machineName }])
    ),
    agentIds: Object.keys(registry.agents),
    serverTime: new Date().toISOString()
  });
}
