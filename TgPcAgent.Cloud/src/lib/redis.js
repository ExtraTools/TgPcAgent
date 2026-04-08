import { Redis } from "@upstash/redis";

let _redis = null;

function cleanEnv(val) {
  if (!val) return val;
  return val.replace(/^\uFEFF/, "").replace(/[\r\n\s]+/g, "").trim();
}

export function getRedis() {
  if (!_redis) {
    _redis = new Redis({
      url: cleanEnv(process.env.UPSTASH_REDIS_REST_URL),
      token: cleanEnv(process.env.UPSTASH_REDIS_REST_TOKEN)
    });
  }
  return _redis;
}
