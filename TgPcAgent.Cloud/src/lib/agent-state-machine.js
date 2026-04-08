const moscowClockFormatter = new Intl.DateTimeFormat("ru-RU", {
  timeZone: "Europe/Moscow",
  hour: "2-digit",
  minute: "2-digit",
  second: "2-digit",
  hour12: false
});

function normalizeState(state) {
  return {
    status: state?.status ?? "offline",
    machineName: state?.machineName ?? "ПК",
    lastHeartbeatAt: state?.lastHeartbeatAt ?? null,
    lastHeartbeatId: state?.lastHeartbeatId ?? null,
    lastNotificationType: state?.lastNotificationType ?? null,
    offlineNotifiedAt: state?.offlineNotifiedAt ?? null,
    onlineNotifiedAt: state?.onlineNotifiedAt ?? null
  };
}

function formatClock(isoString) {
  if (!isoString) {
    return "неизвестно";
  }

  const date = new Date(isoString);
  if (Number.isNaN(date.getTime())) {
    return "неизвестно";
  }

  return moscowClockFormatter.format(date);
}

export function applyHeartbeat(state, heartbeat, nowIso) {
  const current = normalizeState(state);
  const nextState = {
    ...current,
    status: "online",
    machineName: heartbeat.machineName || current.machineName,
    lastHeartbeatAt: nowIso,
    lastHeartbeatId: heartbeat.heartbeatId || nowIso
  };

  if (current.status !== "online") {
    nextState.onlineNotifiedAt = nowIso;
    nextState.lastNotificationType = "online";

    return {
      state: nextState,
      notification: {
        type: "online",
        text: `🟢 ${nextState.machineName} снова в сети.`
      }
    };
  }

  return {
    state: nextState,
    notification: null
  };
}

export function applyShutdown(state, shutdownEvent, nowIso) {
  const current = normalizeState(state);
  const nextState = {
    ...current,
    status: "offline",
    lastHeartbeatAt: nowIso,
    offlineNotifiedAt: nowIso,
    lastNotificationType: "offline-shutdown"
  };

  const reasonText = shutdownEvent?.reason === "agent-exit"
    ? "Агент завершает работу, ПК уходит в оффлайн."
    : "ПК уходит в оффлайн.";

  return {
    state: nextState,
    notification: {
      type: "offline-shutdown",
      text: `🟠 ${current.machineName}: ${reasonText}`
    }
  };
}

export function applyTimeout(state, nowIso, timeoutMs, expectedHeartbeatId = null) {
  const current = normalizeState(state);
  if (current.status !== "online" || !current.lastHeartbeatAt) {
    return { state: current, notification: null };
  }

  if (expectedHeartbeatId && current.lastHeartbeatId && current.lastHeartbeatId !== expectedHeartbeatId) {
    return { state: current, notification: null };
  }

  const ageMs = Date.parse(nowIso) - Date.parse(current.lastHeartbeatAt);
  if (ageMs <= timeoutMs) {
    return { state: current, notification: null };
  }

  const nextState = {
    ...current,
    status: "offline",
    offlineNotifiedAt: nowIso,
    lastNotificationType: "offline-timeout"
  };

  if (current.lastNotificationType === "offline-timeout") {
    return { state: nextState, notification: null };
  }

  return {
    state: nextState,
    notification: {
      type: "offline-timeout",
      text: `🔴 ${current.machineName} недоступен. Последний heartbeat был в ${formatClock(current.lastHeartbeatAt)} МСК.`
    }
  };
}

export function buildCloudStatusMessage(state, nowIso) {
  const current = normalizeState(state);
  const statusText = current.status === "online" ? "онлайн" : "оффлайн";

  return [
    "<b>Облачный статус агента</b>",
    `Статус: <b>${statusText}</b>`,
    `ПК: <code>${current.machineName}</code>`,
    `Последний heartbeat: <code>${formatClock(current.lastHeartbeatAt)}</code> МСК (UTC+3)`,
    `Сейчас: <code>${formatClock(nowIso)}</code> МСК (UTC+3)`
  ].join("\n");
}
