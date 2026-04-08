import { loadRegistry } from "../../src/lib/agent-registry.js";
import { isAuthorizedAgent } from "../../src/lib/auth.js";
import { getPendingCommands } from "../../src/lib/command-queue.js";
import { sendJson } from "../../src/lib/http.js";
import { getRedis } from "../../src/lib/redis.js";

async function quickAuth(agentId, secret) {
  const redis = getRedis();
  const storedHash = await redis.hget("tgpcagent:auth", agentId);
  if (!storedHash) {
    const registry = await loadRegistry();
    return isAuthorizedAgent(registry, agentId, secret);
  }
  const { createHash } = await import("node:crypto");
  const hash = `sha256:${createHash("sha256").update(secret).digest("hex")}`;
  return hash === storedHash;
}

export default async function handler(request, response) {
  if (request.method !== "GET") {
    return sendJson(response, 405, { ok: false, error: "method_not_allowed" });
  }

  const agentId = request.headers["x-agent-id"] || new URL(request.url, "http://localhost").searchParams.get("agentId");
  const secret = request.headers["x-agent-secret"] || new URL(request.url, "http://localhost").searchParams.get("secret");

  if (!agentId || !secret) {
    return sendJson(response, 400, { ok: false, error: "missing_credentials" });
  }

  if (!await quickAuth(agentId, secret)) {
    return sendJson(response, 401, { ok: false, error: "unauthorized" });
  }

  try {
    const commands = await getPendingCommands(agentId);
    return sendJson(response, 200, { ok: true, commands });
  } catch (error) {
    console.error("Failed to get pending commands:", error);
    return sendJson(response, 500, { ok: false, error: "internal" });
  }
}
