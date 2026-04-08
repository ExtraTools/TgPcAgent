import { getConfig } from "./config.js";
import { verifyAgent } from "./agent-registry.js";

/**
 * Legacy single-agent auth — checks against env AGENT_SECRET.
 */
export function isAuthorizedAgentSecret(value) {
  const { agentSecret } = getConfig();
  return typeof value === "string" && value.length > 0 && value === agentSecret;
}

/**
 * Multi-agent auth — verifies agentId + secret pair against registry.
 */
export function isAuthorizedAgent(registry, agentId, secret) {
  if (!agentId || !secret) {
    return false;
  }
  return verifyAgent(registry, agentId, secret);
}
