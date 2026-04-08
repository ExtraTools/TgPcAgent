import test from "node:test";
import assert from "node:assert/strict";

import {
  applyHeartbeat,
  applyShutdown,
  applyTimeout,
  buildCloudStatusMessage
} from "../src/lib/agent-state-machine.js";

test("first heartbeat marks agent online and emits online notification", () => {
  const now = "2026-04-08T12:00:00.000Z";

  const result = applyHeartbeat(null, {
    machineName: "SOMNI-PC",
    heartbeatId: "hb-1"
  }, now);

  assert.equal(result.state.status, "online");
  assert.equal(result.state.machineName, "SOMNI-PC");
  assert.equal(result.state.lastHeartbeatId, "hb-1");
  assert.match(result.notification.text, /снова в сети/i);
});

test("heartbeat while already online updates timestamp without duplicate notification", () => {
  const state = {
    status: "online",
    machineName: "SOMNI-PC",
    lastHeartbeatAt: "2026-04-08T12:00:00.000Z",
    lastHeartbeatId: "hb-1",
    lastNotificationType: "online",
    offlineNotifiedAt: null,
    onlineNotifiedAt: "2026-04-08T12:00:00.000Z"
  };

  const result = applyHeartbeat(state, {
    machineName: "SOMNI-PC",
    heartbeatId: "hb-2"
  }, "2026-04-08T12:00:45.000Z");

  assert.equal(result.state.status, "online");
  assert.equal(result.state.lastHeartbeatAt, "2026-04-08T12:00:45.000Z");
  assert.equal(result.state.lastHeartbeatId, "hb-2");
  assert.equal(result.notification, null);
});

test("stale heartbeat marks agent offline and emits outage notification once in Moscow time", () => {
  const state = {
    status: "online",
    machineName: "SOMNI-PC",
    lastHeartbeatAt: "2026-04-08T12:00:00.000Z",
    lastHeartbeatId: "hb-1",
    lastNotificationType: "online",
    offlineNotifiedAt: null,
    onlineNotifiedAt: "2026-04-08T12:00:00.000Z"
  };

  const result = applyTimeout(state, "2026-04-08T12:03:01.000Z", 120000, "hb-1");

  assert.equal(result.state.status, "offline");
  assert.ok(result.notification.text.includes("15:00:00"));
  assert.ok(result.notification.text.includes("МСК"));

  const secondPass = applyTimeout(result.state, "2026-04-08T12:04:01.000Z", 120000, "hb-1");
  assert.equal(secondPass.state.status, "offline");
  assert.equal(secondPass.notification, null);
});

test("stale timeout job is ignored when a newer heartbeat already arrived", () => {
  const state = {
    status: "online",
    machineName: "SOMNI-PC",
    lastHeartbeatAt: "2026-04-08T12:01:00.000Z",
    lastHeartbeatId: "hb-2",
    lastNotificationType: "online",
    offlineNotifiedAt: null,
    onlineNotifiedAt: "2026-04-08T12:00:00.000Z"
  };

  const result = applyTimeout(state, "2026-04-08T12:03:30.000Z", 120000, "hb-1");

  assert.equal(result.state.status, "online");
  assert.equal(result.notification, null);
});

test("heartbeat after outage emits back online notification", () => {
  const state = {
    status: "offline",
    machineName: "SOMNI-PC",
    lastHeartbeatAt: "2026-04-08T12:00:00.000Z",
    lastHeartbeatId: "hb-1",
    lastNotificationType: "offline-timeout",
    offlineNotifiedAt: "2026-04-08T12:03:01.000Z",
    onlineNotifiedAt: "2026-04-08T12:00:00.000Z"
  };

  const result = applyHeartbeat(state, {
    machineName: "SOMNI-PC",
    heartbeatId: "hb-3"
  }, "2026-04-08T12:04:10.000Z");

  assert.equal(result.state.status, "online");
  assert.match(result.notification.text, /снова в сети/i);
});

test("shutdown event marks agent offline immediately", () => {
  const state = {
    status: "online",
    machineName: "SOMNI-PC",
    lastHeartbeatAt: "2026-04-08T12:00:00.000Z",
    lastHeartbeatId: "hb-1",
    lastNotificationType: "online",
    offlineNotifiedAt: null,
    onlineNotifiedAt: "2026-04-08T12:00:00.000Z"
  };

  const result = applyShutdown(state, {
    reason: "agent-exit"
  }, "2026-04-08T12:00:10.000Z");

  assert.equal(result.state.status, "offline");
  assert.match(result.notification.text, /завершает работу|оффлайн/i);
});

test("status message shows Moscow time and UTC+3 marker", () => {
  const message = buildCloudStatusMessage({
    status: "offline",
    machineName: "SOMNI-PC",
    lastHeartbeatAt: "2026-04-08T12:00:00.000Z"
  }, "2026-04-08T12:05:00.000Z");

  assert.match(message, /оффлайн/i);
  assert.ok(message.includes("15:00:00"));
  assert.ok(message.includes("15:05:00"));
  assert.ok(message.includes("UTC+3"));
});
