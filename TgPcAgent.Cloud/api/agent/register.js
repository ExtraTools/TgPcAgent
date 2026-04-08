import { createHash } from "node:crypto";
import { loadRegistry, saveRegistry, registerAgent, createPairingCode, verifyAgent } from "../../src/lib/agent-registry.js";
import { readJsonBody, sendJson } from "../../src/lib/http.js";
import { getRedis } from "../../src/lib/redis.js";

export default async function handler(request, response) {
  try {
    return await _handler(request, response);
  } catch (err) {
    console.error("register crash:", err);
    return response.status(500).json({ ok: false, error: "internal", message: String(err?.message || err) });
  }
}

async function _handler(request, response) {
  if (request.method !== "POST") {
    return sendJson(response, 405, { ok: false, error: "method_not_allowed" });
  }

  const body = await readJsonBody(request);
  const { agentId, secret, machineName, forceRePair } = body;

  if (!agentId || !secret) {
    return sendJson(response, 400, { ok: false, error: "missing_agent_id_or_secret" });
  }

  if (typeof secret !== "string" || secret.length < 32) {
    return sendJson(response, 400, { ok: false, error: "secret_too_short" });
  }

  let registry = await loadRegistry();

  // Check if agent already exists
  const existingAgent = registry.agents[agentId];
  if (existingAgent) {
    // Agent re-registering — verify secret
    if (!verifyAgent(registry, agentId, secret)) {
      return sendJson(response, 401, { ok: false, error: "unauthorized" });
    }

    // Update machine name if changed
    if (machineName && existingAgent.machineName !== machineName) {
      existingAgent.machineName = machineName;
    }

    // Force re-pair: clear owner and generate new code
    if (forceRePair && existingAgent.ownerChatId) {
      const oldOwner = existingAgent.ownerChatId;
      // Remove agent from user's list
      const userEntry = registry.users[String(oldOwner)];
      if (userEntry) {
        userEntry.agentIds = (userEntry.agentIds || []).filter(id => id !== agentId);
        if (userEntry.activeAgentId === agentId) {
          userEntry.activeAgentId = userEntry.agentIds[0] || null;
        }
        if (userEntry.agentIds.length === 0) {
          delete registry.users[String(oldOwner)];
        }
      }
      existingAgent.ownerChatId = null;
    }

    // Issue new pairing code if not yet paired
    if (!existingAgent.ownerChatId) {
      const pairingResult = createPairingCode(registry, agentId);
      registry = pairingResult.registry;
      await saveRegistry(registry);

      return sendJson(response, 200, {
        ok: true,
        registered: false,
        updated: true,
        pairingCode: pairingResult.code,
        alreadyPaired: false
      });
    }

    await saveRegistry(registry);
    await getRedis().hset("tgpcagent:auth", { [agentId]: `sha256:${createHash("sha256").update(secret).digest("hex")}` });
    return sendJson(response, 200, {
      ok: true,
      registered: false,
      updated: true,
      alreadyPaired: true,
      ownerChatId: existingAgent.ownerChatId
    });
  }

  // New agent registration
  registry = registerAgent(registry, agentId, secret, machineName || "PC");
  const pairingResult = createPairingCode(registry, agentId);
  registry = pairingResult.registry;

  await saveRegistry(registry);
  await getRedis().hset("tgpcagent:auth", { [agentId]: `sha256:${createHash("sha256").update(secret).digest("hex")}` });

  return sendJson(response, 200, {
    ok: true,
    registered: true,
    pairingCode: pairingResult.code
  });
}
