import { createHash, randomInt } from "node:crypto";

import { getRedis } from "./redis.js";
import { getConfig } from "./config.js";

const REGISTRY_KEY = "tgpcagent:registry";

// ---------------------------------------------------------------------------
// Redis I/O
// ---------------------------------------------------------------------------

export async function loadRegistry() {
  try {
    const data = await getRedis().get(REGISTRY_KEY);
    if (!data) return emptyRegistry();
    return normalize(typeof data === "string" ? JSON.parse(data) : data);
  } catch {
    return emptyRegistry();
  }
}

export async function saveRegistry(registry) {
  await getRedis().set(REGISTRY_KEY, JSON.stringify(registry));
}

// ---------------------------------------------------------------------------
// Agent CRUD
// ---------------------------------------------------------------------------

export function registerAgent(registry, agentId, secret, machineName) {
  const normalized = normalize(registry);
  normalized.agents[agentId] = {
    secretHash: hashSecret(secret),
    machineName: machineName || "PC",
    ownerChatId: null,
    status: "offline",
    lastHeartbeatAt: null,
    registeredAt: new Date().toISOString(),
    appVersion: null
  };
  return normalized;
}

export function verifyAgent(registry, agentId, secret) {
  const agent = normalize(registry).agents[agentId];
  if (!agent) {
    return false;
  }
  return agent.secretHash === hashSecret(secret);
}

export function getAgent(registry, agentId) {
  return normalize(registry).agents[agentId] || null;
}

export function updateAgentHeartbeat(registry, agentId, heartbeatData) {
  const normalized = normalize(registry);
  const agent = normalized.agents[agentId];
  if (!agent) {
    return normalized;
  }
  agent.status = "online";
  agent.lastHeartbeatAt = heartbeatData.nowIso || new Date().toISOString();
  if (heartbeatData.machineName) {
    agent.machineName = heartbeatData.machineName;
  }
  if (heartbeatData.appVersion) {
    agent.appVersion = heartbeatData.appVersion;
  }
  return normalized;
}

// ---------------------------------------------------------------------------
// Pairing
// ---------------------------------------------------------------------------

export function createPairingCode(registry, agentId) {
  const normalized = normalize(registry);
  const { pairingCodeLifetimeMs } = getConfig();

  cleanExpiredPairingCodes(normalized);

  for (const [existingCode, entry] of Object.entries(normalized.pairingCodes)) {
    if (entry.agentId === agentId && new Date(entry.expiresAt) > new Date()) {
      return { registry: normalized, code: existingCode };
    }
  }

  const code = generateAlphanumericCode(6);
  normalized.pairingCodes[code] = {
    agentId,
    expiresAt: new Date(Date.now() + pairingCodeLifetimeMs).toISOString()
  };
  return { registry: normalized, code };
}

export function completePairing(registry, code, chatId) {
  const normalized = normalize(registry);
  cleanExpiredPairingCodes(normalized);

  const entry = normalized.pairingCodes[code];
  if (!entry) {
    return { registry: normalized, success: false, reason: "invalid_code" };
  }

  if (new Date(entry.expiresAt) <= new Date()) {
    delete normalized.pairingCodes[code];
    return { registry: normalized, success: false, reason: "code_expired" };
  }

  const { agentId } = entry;
  const agent = normalized.agents[agentId];
  if (!agent) {
    delete normalized.pairingCodes[code];
    return { registry: normalized, success: false, reason: "agent_not_found" };
  }

  // Link agent to user
  agent.ownerChatId = chatId;

  // Add agent to user's list
  const chatKey = String(chatId);
  if (!normalized.users[chatKey]) {
    normalized.users[chatKey] = { activeAgentId: null, agentIds: [] };
  }
  const user = normalized.users[chatKey];
  if (!user.agentIds.includes(agentId)) {
    user.agentIds.push(agentId);
  }
  user.activeAgentId = agentId;

  delete normalized.pairingCodes[code];
  return { registry: normalized, success: true, agentId, machineName: agent.machineName };
}

// ---------------------------------------------------------------------------
// User helpers
// ---------------------------------------------------------------------------

export function getUserActiveAgent(registry, chatId) {
  const normalized = normalize(registry);
  const user = normalized.users[String(chatId)];
  if (!user || !user.activeAgentId) {
    return null;
  }
  return { agentId: user.activeAgentId, agent: normalized.agents[user.activeAgentId] || null };
}

export function listUserAgents(registry, chatId) {
  const normalized = normalize(registry);
  const user = normalized.users[String(chatId)];
  if (!user) {
    return [];
  }
  return user.agentIds.map(id => ({
    agentId: id,
    ...normalized.agents[id]
  })).filter(a => a.machineName);
}

export function setUserActiveAgent(registry, chatId, agentId) {
  const normalized = normalize(registry);
  const user = normalized.users[String(chatId)];
  if (!user || !user.agentIds.includes(agentId)) {
    return { registry: normalized, success: false };
  }
  user.activeAgentId = agentId;
  return { registry: normalized, success: true };
}

// ---------------------------------------------------------------------------
// Internals
// ---------------------------------------------------------------------------

function emptyRegistry() {
  return { agents: {}, users: {}, pairingCodes: {} };
}

function normalize(reg) {
  return {
    agents: reg?.agents || {},
    users: reg?.users || {},
    pairingCodes: reg?.pairingCodes || {}
  };
}

function hashSecret(secret) {
  return `sha256:${createHash("sha256").update(secret).digest("hex")}`;
}

function generateAlphanumericCode(length) {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no I,O,0,1 to avoid confusion
  let code = "";
  for (let i = 0; i < length; i++) {
    code += chars[randomInt(chars.length)];
  }
  return code;
}

function cleanExpiredPairingCodes(registry) {
  const now = new Date();
  for (const [code, entry] of Object.entries(registry.pairingCodes)) {
    if (new Date(entry.expiresAt) <= now) {
      delete registry.pairingCodes[code];
    }
  }
}
