import { getRedis } from "./redis.js";

const STATE_KEY = "tgpcagent:agent-state";

export async function loadAgentState() {
  try {
    const data = await getRedis().get(STATE_KEY);
    if (!data) return null;
    return typeof data === "string" ? JSON.parse(data) : data;
  } catch {
    return null;
  }
}

export async function saveAgentState(state) {
  await getRedis().set(STATE_KEY, JSON.stringify(state));
}
