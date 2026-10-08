async function load() {
  const tabs = await chrome.tabs.query({windowType:"normal"});
  const stored = await chrome.storage.local.get({enabled:true,idleMinutes:20});
  document.querySelector("#tabs").textContent = tabs.length;
  document.querySelector("#discarded").textContent = tabs.filter(t => t.discarded).length;
  document.querySelector("#enabled").checked = stored.enabled;
  document.querySelector("#minutes").value = String(stored.idleMinutes);
  document.querySelector("#status").textContent = stored.enabled ? "ACTIVO" : "PAUSADO";
}
document.querySelector("#optimize").addEventListener("click", async () => {
  const r = await chrome.runtime.sendMessage({type:"optimize"});
  document.querySelector("#message").textContent = "Se suspendieron " + (r?.discarded ?? 0) + " pestañas.";
  await load();
});
document.querySelector("#enabled").addEventListener("change", async e => {
  await chrome.storage.local.set({enabled:e.target.checked}); await load();
});
document.querySelector("#minutes").addEventListener("change", async e => {
  await chrome.storage.local.set({idleMinutes:Number(e.target.value)});
});
load();