import { randomUUID } from "node:crypto";

import { getRedis } from "./redis.js";
import { getConfig } from "./config.js";

function queueKey(agentId) {
  return `tgpcagent:queue:${agentId}`;
}

function commandKey(agentId, commandId) {
  return `tgpcagent:cmd:${agentId}:${commandId}`;
}

// ---------------------------------------------------------------------------
// Enqueue
// ---------------------------------------------------------------------------

export async function enqueueCommand(agentId, { chatId, text, type, callbackQueryId, messageId, pendingMessageId }) {
  const { commandTtlMs } = getConfig();
  const commandId = randomUUID();
  const command = {
    id: commandId,
    chatId,
    text: text || "",
    type: type || "unknown",
    callbackQueryId: callbackQueryId || null,
    messageId: messageId || null,
    pendingMessageId: pendingMessageId || null,
    createdAt: new Date().toISOString()
  };

  const redis = getRedis();
  const ttlSeconds = Math.ceil(commandTtlMs / 1000);
  const key = commandKey(agentId, commandId);

  await redis.set(key, JSON.stringify(command), { ex: ttlSeconds });
  await redis.rpush(queueKey(agentId), commandId);

  return command;
}

// ---------------------------------------------------------------------------
// Poll
// ---------------------------------------------------------------------------

export async function getPendingCommands(agentId) {
  const redis = getRedis();
  const listKey = queueKey(agentId);

  const ids = await redis.lrange(listKey, 0, -1);
  if (!ids || ids.length === 0) return [];

  const commands = [];
  const expiredIds = [];

  for (const id of ids) {
    const raw = await redis.get(commandKey(agentId, id));
    if (!raw) {
      expiredIds.push(id);
      continue;
    }
    const cmd = typeof raw === "string" ? JSON.parse(raw) : raw;
    commands.push(cmd);
  }

  if (expiredIds.length > 0) {
    for (const id of expiredIds) {
      await redis.lrem(listKey, 0, id);
    }
  }

  return commands.sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt));
}

// ---------------------------------------------------------------------------
// Ack
// ---------------------------------------------------------------------------

export async function ackCommand(agentId, commandId) {
  const redis = getRedis();
  await redis.del(commandKey(agentId, commandId));
  await redis.lrem(queueKey(agentId), 0, commandId);
}
