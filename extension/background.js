const DEFAULTS = { enabled: true, idleMinutes: 20, maxTabsToDiscard: 10, excludePinned: true };

async function settings() {
  const saved = await chrome.storage.local.get(DEFAULTS);
  return { ...DEFAULTS, ...saved };
}

async function discardInactive() {
  const cfg = await settings();
  if (!cfg.enabled) return { discarded: 0 };

  const tabs = await chrome.tabs.query({ windowType: "normal" });
  const now = Date.now();
  const threshold = cfg.idleMinutes * 60 * 1000;
  let discarded = 0;

  const candidates = tabs
    .filter(t => !t.active && !t.discarded)
    .filter(t => !(cfg.excludePinned && t.pinned))
    .filter(t => !t.url?.startsWith("chrome://"))
    .filter(t => typeof t.lastAccessed === "number" && now - t.lastAccessed >= threshold)
    .sort((a, b) => a.lastAccessed - b.lastAccessed);

  for (const tab of candidates.slice(0, cfg.maxTabsToDiscard)) {
    try {
      if (await chrome.tabs.discard(tab.id)) discarded++;
    } catch (_) {}
  }

  await chrome.storage.local.set({ lastRun: Date.now(), lastDiscarded: discarded });
  return { discarded };
}

chrome.runtime.onStartup.addListener(discardInactive);
chrome.runtime.onInstalled.addListener(discardInactive);
chrome.alarms.create("ramchrome", { periodInMinutes: 5 });
chrome.alarms.onAlarm.addListener(a => { if (a.name === "ramchrome") discardInactive(); });

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  if (message?.type === "optimize") {
    discardInactive().then(sendResponse);
    return true;
  }
});