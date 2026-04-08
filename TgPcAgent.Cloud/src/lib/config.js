export function getRequiredEnv(name) {
  const value = (process.env[name] || "").trim();
  if (!value) {
    throw new Error(`Missing required env: ${name}`);
  }

  return value;
}

export function getOptionalEnv(name, fallback = null) {
  const raw = process.env[name];
  return raw ? raw.trim() : fallback;
}

export function getConfig() {
  return {
    blobPath: getOptionalEnv("AGENT_STATE_BLOB_PATH", "state/agent-state.json"),
    registryBlobPath: getOptionalEnv("REGISTRY_BLOB_PATH", "state/registry.json"),
    queueBlobPrefix: getOptionalEnv("QUEUE_BLOB_PREFIX", "queue/"),
    agentSecret: getOptionalEnv("AGENT_SECRET", ""),
    botToken: getRequiredEnv("TELEGRAM_BOT_TOKEN"),
    ownerChatId: Number.parseInt(getOptionalEnv("TELEGRAM_OWNER_CHAT_ID", "0"), 10),
    heartbeatTimeoutMs: Number.parseInt(getOptionalEnv("HEARTBEAT_TIMEOUT_MS", "120000"), 10),
    webhookSecret: getOptionalEnv("WEBHOOK_SECRET", ""),
    pairingCodeLifetimeMs: Number.parseInt(getOptionalEnv("PAIRING_CODE_LIFETIME_MS", "600000"), 10),
    commandTtlMs: Number.parseInt(getOptionalEnv("COMMAND_TTL_MS", "120000"), 10)
  };
}
