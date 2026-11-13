const apiToken = document.querySelector('meta[name="api-token"]').content;
const state = {
  data: null, tab: "Internet", selected: null, selectedMap: null, updateInfo: null,
  toastTimer: null, autoTimer: null, autoInterval: -1, previousMessage: "", previousAnnouncement: ""
};

async function api(path, payload, method) {
  const options = { method: method || (payload === undefined ? "GET" : "POST"), headers: { "X-FragCity-Token": apiToken } };
  if (payload !== undefined) {
    options.headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(payload);
  }
  const response = await fetch("/api/" + path, options);
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || "Request failed");
  return body;
}

function notify(message, bad = false) {
  const toast = document.getElementById("toast");
  toast.textContent = message;
  toast.className = "toast show" + (bad ? " bad" : "");
  clearTimeout(state.toastTimer);
  state.toastTimer = setTimeout(() => toast.className = "toast", 3500);
}

function formatBytes(value) {
  if (!value) return "0 bytes relayed";
  const units = ["B", "KB", "MB", "GB"];
  let n = value, i = 0;
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
  return n.toFixed(i === 0 ? 0 : 1) + " " + units[i] + " relayed";
}

function setValueUnlessFocused(id, value) {
  const element = document.getElementById(id);
  if (document.activeElement !== element) element.value = value == null ? "" : value;
}

function setCheckedUnlessFocused(id, value) {
  const element = document.getElementById(id);
  if (document.activeElement !== element) element.checked = !!value;
}

function applyState(next) {
  state.data = next;
  render();
}

async function loadState() {
  try { applyState(await api("state")); }
  catch (error) { notify(error.message, true); }
}

function scheduleAutoRefresh() {
  const settings = state.data && state.data.Settings;
  const interval = settings && settings.AutoRefresh ? Math.max(5, Number(settings.RefreshIntervalSeconds) || 15) * 1000 : 0;
  if (interval === state.autoInterval) return;
  clearInterval(state.autoTimer);
  state.autoInterval = interval;
  if (interval > 0) {
    state.autoTimer = setInterval(async () => {
      if (document.visibilityState !== "visible" || !state.data) return;
      try { applyState(await api("refresh", {})); } catch (_) {}
    }, interval);
  }
}

function currentList() {
  if (!state.data) return [];
  return state.data[state.tab] || [];
}

function render() {
  if (!state.data) return;
  const data = state.data;
  const clientOn = data.ClientProxyRunning;
  const serverOn = data.ServerProxyRunning;
  document.getElementById("client-dot").className = "status-dot " + (clientOn ? "on" : "");
  document.getElementById("server-dot").className = "status-dot " + (serverOn ? "on" : "");
  document.getElementById("client-status").textContent = clientOn ? "ACTIVE" : "STOPPED";
  document.getElementById("server-status").textContent = serverOn ? "ACTIVE" : "STOPPED";
  document.getElementById("client-address").textContent = clientOn ? data.ClientProxyAddress : "127.0.0.1:—";
  document.getElementById("server-address").textContent = serverOn ? data.ServerProxyAddress : "0.0.0.0:—";
  document.getElementById("client-traffic").textContent = formatBytes(data.ClientBytesIn + data.ClientBytesOut);
  document.getElementById("server-traffic").textContent = formatBytes(data.ServerBytesIn + data.ServerBytesOut);
  document.getElementById("stop-client").disabled = !clientOn;
  document.getElementById("stop-server").disabled = !serverOn;
  if (data.BlockedPublicPeers) {
    document.getElementById("server-traffic").textContent += " · " + data.BlockedPublicPeers + " non-LAN packet(s) blocked";
  }

  document.getElementById("announcement-text").textContent = data.Announcement || "Ready · LAN play does not require an Internet connection.";
  document.getElementById("steam-id").textContent = data.SteamID ? "STEAM ID: " + data.SteamID : "";
  const settings = data.Settings || {};
  const settingsMap = {
    "setting-query": settings.GameQueryPort,
    "setting-local": settings.LocalPort,
    "setting-proxy": settings.ProxyPort,
    "setting-refresh": settings.RefreshIntervalSeconds,
    "setting-server-url": settings.ServerUpdateUrl,
    "setting-workshop-url": settings.WorkshopMapUpdateUrl,
    "setting-announcement-url": settings.AnnouncementUrl,
    "setting-update-url": settings.UpdateCheckUrl,
    "setting-version-url": settings.VersionCheckUrl,
    "setting-workshop-path": data.WorkshopPath
  };
  Object.entries(settingsMap).forEach(([id, value]) => setValueUnlessFocused(id, value));
  setCheckedUnlessFocused("setting-autorefresh", settings.AutoRefresh);
  setValueUnlessFocused("workshop-path", data.WorkshopPath);
  setValueUnlessFocused("internal-port", settings.GameQueryPort);
  setValueUnlessFocused("forward-port", settings.ProxyPort);
  scheduleAutoRefresh();

  document.querySelectorAll(".tab").forEach(button => button.classList.toggle("active", button.dataset.tab === state.tab));
  const filter = document.getElementById("search").value.trim().toLowerCase();
  const list = currentList().filter(s => !filter || [s.Name, s.Ip, s.Map, s.Tags, s.GameMode].some(v => String(v || "").toLowerCase().includes(filter)));
  document.getElementById("server-count").textContent = list.length + (list.length === 1 ? " server" : " servers");
  const container = document.getElementById("server-list");
  if (!list.length) {
    container.innerHTML = '<div class="empty"><span>⌁</span><strong>No servers in this list</strong><small>Add a LAN server, or use Sync remote lists when an optional update URL is configured.</small></div>';
  } else {
    container.innerHTML = list.map((s, index) => {
      const isSelected = state.selected && state.selected.Ip === s.Ip && state.selected.Port === s.Port;
      const online = Number(s.Ping) >= 0;
      const favorite = (data.Favorites || []).some(f => f.Ip === s.Ip && f.Port === s.Port);
      return '<article class="server-row ' + (isSelected ? "selected" : "") + '" data-index="' + index + '">' +
        '<div class="server-main"><div class="server-name">' + esc(s.Name || "Unknown server") + '</div>' +
        '<div class="server-address">' + esc(s.Ip) + ':' + s.Port + '</div></div>' +
        '<div class="server-meta"><span class="map">' + esc(s.Map || "Unknown map") + '</span>' +
        '<span class="players">' + (online ? (s.Players || 0) + '/' + (s.MaxPlayers || 0) : "—") + '</span>' +
        '<span class="ping ' + (online ? "online" : "") + '">' + (online ? s.Ping + " ms" : "Offline") + '</span></div>' +
        '<div class="row-actions"><button class="icon-button fav ' + (favorite ? "is-fav" : "") + '" data-action="favorite" title="Toggle favorite">' + (favorite ? "♥" : "♡") + '</button>' +
        (state.tab !== "Internet" ? '<button class="icon-button" data-action="remove" title="Remove from this list">×</button>' : "") +
        '</div></article>';
    }).join("");
    container.querySelectorAll(".server-row").forEach(row => {
      row.addEventListener("click", event => {
        const chosen = list[Number(row.dataset.index)];
        if (event.target.closest("[data-action]")) {
          event.stopPropagation();
          handleRowAction(event.target.closest("[data-action]").dataset.action, chosen);
          return;
        }
        state.selected = chosen;
        render();
      });
      row.addEventListener("dblclick", () => connectSelected(true));
    });
  }
  renderSelectedServer();
  renderWorkshop();
}

function renderSelectedServer() {
  const s = state.selected;
  if (!s) {
    document.getElementById("selected-server").innerHTML = '<span class="selection-mark">◎</span><span>No server selected</span>';
    document.getElementById("connect").disabled = true;
    document.getElementById("start-client").disabled = true;
    document.getElementById("players-open").disabled = true;
    for (const id of ["detail-name","detail-address","detail-players","detail-map","detail-ping","detail-tick","detail-secure"]) document.getElementById(id).textContent = "—";
    document.getElementById("detail-name").textContent = "No server selected";
    document.getElementById("detail-tags").textContent = "Select a server to view its tags and game mode.";
    return;
  }
  document.getElementById("selected-server").innerHTML = '<span class="selection-mark">◎</span><span><strong>' + esc(s.Name || "Selected server") + '</strong><small>' + esc(s.Ip) + ':' + s.Port + '</small></span>';
  document.getElementById("connect").disabled = false;
  document.getElementById("start-client").disabled = !!(state.data && state.data.ClientProxyRunning);
  document.getElementById("players-open").disabled = false;
  document.getElementById("detail-name").textContent = s.Name || "Unknown";
  document.getElementById("detail-address").textContent = s.Ip + ":" + s.Port;
  document.getElementById("detail-players").textContent = (s.Players || 0) + "/" + (s.MaxPlayers || 0);
  document.getElementById("detail-map").textContent = s.Map || "Unknown";
  document.getElementById("detail-ping").textContent = Number(s.Ping) >= 0 ? s.Ping + " ms" : "Offline";
  document.getElementById("detail-tick").textContent = s.TickRate || "—";
  document.getElementById("detail-secure").textContent = (s.IsSecure ? "VAC secure" : "Not secure") + (s.HasPassword ? " · Password" : "");
  document.getElementById("detail-tags").textContent = [s.GameMode, s.Tags].filter(Boolean).join(" · ") || "No server tags reported.";
}

function renderWorkshop() {
  const maps = (state.data && state.data.WorkshopMaps) || [];
  const search = document.getElementById("map-search").value.trim().toLowerCase();
  const filtered = maps.filter(m => String(m.MapName || "").toLowerCase().includes(search) || String(m.WorkshopId).includes(search));
  document.getElementById("map-count").textContent = filtered.length + " / " + maps.length + " maps";
  const list = document.getElementById("map-list");
  if (!filtered.length) {
    list.innerHTML = '<div class="empty"><span>⌁</span><strong>No maps found</strong><small>Place workshop_maps.json beside the executable or configure a workshop map index URL in Settings.</small></div>';
  } else {
    list.innerHTML = filtered.map((m, index) => '<button class="map-row ' +
      (state.selectedMap && state.selectedMap.WorkshopId === m.WorkshopId ? "selected" : "") +
      '" data-map-index="' + index + '"><span class="map-row-main"><strong>' + esc(m.MapName) + '</strong><small>ID ' + m.WorkshopId + '</small></span><span class="map-row-size">' +
      (Number(m.Size) > 0 ? (Number(m.Size) / 1048576).toFixed(1) + " MB" : "—") + '</span><span class="map-state ' +
      (m.IsInstalled ? "installed" : "") + '">' + (m.IsInstalled ? "Installed" : "Not installed") + '</span></button>').join("");
    list.querySelectorAll(".map-row").forEach(row => row.addEventListener("click", () => {
      state.selectedMap = filtered[Number(row.dataset.mapIndex)];
      renderWorkshop();
    }));
  }
  const m = state.selectedMap && maps.find(x => x.WorkshopId === state.selectedMap.WorkshopId);
  if (!m) {
    document.getElementById("map-name").textContent = "No map selected";
    for (const id of ["map-id","map-size","map-updated","map-status"]) document.getElementById(id).textContent = "—";
    document.getElementById("map-manifest").textContent = "Manifest: —";
    document.getElementById("map-download").disabled = true;
    return;
  }
  document.getElementById("map-name").textContent = m.MapName;
  document.getElementById("map-id").textContent = m.WorkshopId;
  document.getElementById("map-size").textContent = m.Size ? (m.Size / 1048576).toFixed(1) + " MB" : "—";
  document.getElementById("map-updated").textContent = m.TimeUpdated ? new Date(m.TimeUpdated * 1000).toLocaleDateString() : "—";
  document.getElementById("map-status").textContent = m.IsInstalled ? "Installed" : "Not installed";
  document.getElementById("map-manifest").textContent = "Manifest: " + (m.Manifest || "—");
  const imageName = encodeURIComponent(String(m.MapName || "").replace(/[\\\\/]/g, ""));
  const preview = document.getElementById("map-preview");
  preview.replaceChildren();
  if (imageName) {
    const image = document.createElement("img");
    image.alt = "";
    image.src = "/maps/" + imageName + ".jpg";
    image.addEventListener("error", () => { preview.textContent = "FC"; });
    preview.appendChild(image);
  } else {
    preview.textContent = "FC";
  }
  document.getElementById("map-download").disabled = m.IsInstalled || !m.DownloadUrl || (state.data.WorkshopDownload && state.data.WorkshopDownload.Active);
}

function esc(value) {
  return String(value == null ? "" : value).replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[ch]));
}

async function handleRowAction(action, server) {
  try {
    const payload = { Group: state.tab, Ip: server.Ip, Port: server.Port };
    if (action === "favorite") applyState(await api("favorite", payload));
    if (action === "remove") {
      applyState(await api("remove", payload));
      if (state.selected && state.selected.Ip === server.Ip && state.selected.Port === server.Port) state.selected = null;
      render();
    }
  } catch (error) { notify(error.message, true); }
}

async function connectSelected(launchSteam) {
  if (!state.selected) return;
  const path = launchSteam ? "connect" : "start-client";
  try {
    applyState(await api(path, { Group: state.tab, Ip: state.selected.Ip, Port: state.selected.Port }));
    notify(launchSteam ? "UDP tunnel started. Steam should launch CS2." : "Client UDP tunnel started.");
  } catch (error) { notify(error.message, true); }
}

document.querySelectorAll(".nav-item").forEach(button => button.addEventListener("click", () => {
  document.querySelectorAll(".nav-item").forEach(x => x.classList.toggle("active", x === button));
  document.querySelectorAll(".view").forEach(view => view.classList.toggle("active", view.id === button.dataset.view));
}));
document.querySelectorAll(".tab").forEach(button => button.addEventListener("click", () => {
  state.tab = button.dataset.tab;
  state.selected = null;
  render();
}));
document.getElementById("search").addEventListener("input", render);
document.getElementById("map-search").addEventListener("input", renderWorkshop);
document.getElementById("refresh").addEventListener("click", async () => {
  const button = document.getElementById("refresh");
  button.disabled = true; button.textContent = "Refreshing…";
  try { applyState(await api("refresh", {})); notify("Local server status refreshed."); }
  catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.textContent = "Refresh"; }
});
document.getElementById("sync").addEventListener("click", async () => {
  const button = document.getElementById("sync");
  button.disabled = true; button.textContent = "Syncing…";
  try {
    const result = await api("sync", {});
    if (result.state) { applyState(result.state); notify("Remote sync warning: " + result.warning, true); }
    else { applyState(result); notify("Remote server, Workshop and announcement sync completed."); }
  } catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.textContent = "Sync remote lists"; }
});
document.getElementById("connect").addEventListener("click", () => connectSelected(true));
document.getElementById("start-client").addEventListener("click", () => connectSelected(false));
document.getElementById("stop-client").addEventListener("click", async () => {
  try { applyState(await api("stop-client", {})); notify("Client tunnel stopped."); }
  catch (error) { notify(error.message, true); }
});
document.getElementById("stop-server").addEventListener("click", async () => {
  try { applyState(await api("server/stop", {})); notify("Host relay stopped."); }
  catch (error) { notify(error.message, true); }
});
document.getElementById("host-form").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const payload = {
      BindIP: document.getElementById("bind-ip").value.trim(),
      InternalIP: document.getElementById("internal-ip").value.trim(),
      InternalPort: Number(document.getElementById("internal-port").value),
      ForwardPort: Number(document.getElementById("forward-port").value)
    };
    applyState(await api("server/start", payload));
    notify("LAN UDP relay started.");
  } catch (error) { notify(error.message, true); }
});

const addDialog = document.getElementById("add-dialog");
document.getElementById("add-open").addEventListener("click", () => addDialog.showModal());
document.getElementById("add-form").addEventListener("submit", async event => {
  if (event.submitter && event.submitter.value === "cancel") return;
  event.preventDefault();
  const button = document.getElementById("add-submit");
  button.disabled = true; button.textContent = "Querying…";
  try {
    applyState(await api("add-address", { Address: document.getElementById("add-address").value.trim() }));
    state.tab = "Personal"; state.selected = null;
    addDialog.close(); render();
    notify("Server queried and saved to Personal.");
    document.getElementById("add-form").reset();
  } catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.textContent = "Query & save"; }
});

document.getElementById("players-open").addEventListener("click", async () => {
  if (!state.selected) return;
  const dialog = document.getElementById("players-dialog");
  document.getElementById("players-server").textContent = state.selected.Name + " · " + state.selected.Ip + ":" + state.selected.Port;
  document.getElementById("players-list").innerHTML = '<div class="empty">Querying server players…</div>';
  dialog.showModal();
  try {
    const result = await api("players", { Ip: state.selected.Ip, Port: state.selected.Port });
    const players = result.Players || [];
    document.getElementById("players-list").innerHTML = players.length ? players.map(p =>
      '<div class="player-row"><span>' + p.Index + '</span><strong>' + esc(p.Name || "(unnamed)") + '</strong><span>' +
      p.Score + ' score</span><span>' + Math.floor(p.Duration / 60) + ' min</span></div>').join("") :
      '<div class="empty">No players reported or the server does not support A2S_PLAYER.</div>';
  } catch (error) {
    document.getElementById("players-list").innerHTML = '<div class="empty">' + esc(error.message) + '</div>';
  }
});
document.getElementById("players-close").addEventListener("click", () => document.getElementById("players-dialog").close());

document.getElementById("export").addEventListener("click", async () => {
  try {
    const servers = await api("export?group=all", undefined, "GET");
    const blob = new Blob([JSON.stringify(servers, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url; anchor.download = "fragcity-servers-export.json"; anchor.click();
    URL.revokeObjectURL(url);
    notify("Server list exported.");
  } catch (error) { notify(error.message, true); }
});
document.getElementById("import-open").addEventListener("click", () => document.getElementById("import-file").click());
document.getElementById("import-file").addEventListener("change", async event => {
  const file = event.target.files && event.target.files[0];
  if (!file) return;
  try {
    const parsed = JSON.parse(await file.text());
    const servers = Array.isArray(parsed) ? parsed : ((parsed && parsed.Internet) || []);
    const result = await api("import", { Servers: servers });
    applyState(result.state);
    notify("Imported " + result.added + " server(s) to Personal.");
    state.tab = "Personal"; render();
  } catch (error) { notify("Import failed: " + error.message, true); }
  finally { event.target.value = ""; }
});

document.getElementById("settings-form").addEventListener("submit", async event => {
  event.preventDefault();
  const settings = Object.assign({}, state.data.Settings, {
    GameQueryPort: Number(document.getElementById("setting-query").value),
    LocalPort: Number(document.getElementById("setting-local").value),
    ProxyPort: Number(document.getElementById("setting-proxy").value),
    RefreshIntervalSeconds: Number(document.getElementById("setting-refresh").value),
    AutoRefresh: document.getElementById("setting-autorefresh").checked,
    ServerUpdateUrl: document.getElementById("setting-server-url").value.trim(),
    WorkshopMapUpdateUrl: document.getElementById("setting-workshop-url").value.trim(),
    AnnouncementUrl: document.getElementById("setting-announcement-url").value.trim(),
    UpdateCheckUrl: document.getElementById("setting-update-url").value.trim(),
    VersionCheckUrl: document.getElementById("setting-version-url").value.trim(),
    WorkshopPath: document.getElementById("setting-workshop-path").value.trim()
  });
  try { applyState(await api("settings", settings)); notify("Settings saved."); }
  catch (error) { notify(error.message, true); }
});

document.getElementById("workshop-path-form").addEventListener("submit", async event => {
  event.preventDefault();
  try { applyState(await api("workshop/path", { Path: document.getElementById("workshop-path").value.trim() })); notify("Workshop directory saved."); }
  catch (error) { notify(error.message, true); }
});
document.getElementById("workshop-refresh").addEventListener("click", async () => {
  try { applyState(await api("workshop/refresh", {})); notify("Workshop map index reloaded."); }
  catch (error) { notify(error.message, true); }
});
document.getElementById("map-download").addEventListener("click", async () => {
  if (!state.selectedMap) return;
  try {
    applyState(await api("workshop/download", { WorkshopID: state.selectedMap.WorkshopId }));
    notify("Workshop map download started.");
  } catch (error) { notify(error.message, true); }
});
document.getElementById("map-cancel").addEventListener("click", async () => {
  try { applyState(await api("workshop/cancel", {})); notify("Cancellation requested."); }
  catch (error) { notify(error.message, true); }
});
document.getElementById("acf-install").addEventListener("click", async () => {
  const button = document.getElementById("acf-install");
  button.disabled = true; button.textContent = "Downloading ACF…";
  try { applyState(await api("workshop/acf", {})); notify("ACF file installed."); }
  catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.textContent = "Install ACF file"; }
});

const updateDialog = document.getElementById("update-dialog");
document.getElementById("update-open").addEventListener("click", () => updateDialog.showModal());
document.getElementById("update-close").addEventListener("click", () => updateDialog.close());
document.getElementById("update-check").addEventListener("click", async () => {
  const button = document.getElementById("update-check");
  const message = document.getElementById("update-message");
  button.disabled = true; message.textContent = "Checking configured update manifest…";
  try {
    state.updateInfo = await api("update/check", {});
    if (state.updateInfo.DownloadUrl) {
      message.textContent = "Version " + state.updateInfo.Version + " is available. Download and extract it to UpdatePackage?";
      document.getElementById("update-download").disabled = false;
    } else {
      message.textContent = "You are already up to date (" + (state.data.Settings.CurrentVersion || "unknown") + ").";
      document.getElementById("update-download").disabled = true;
    }
  } catch (error) { message.textContent = error.message; notify(error.message, true); }
  finally { button.disabled = false; }
});
document.getElementById("update-download").addEventListener("click", async () => {
  if (!state.updateInfo || !state.updateInfo.DownloadUrl) return;
  if (!confirm("Download and extract this update? The running executable will not be replaced automatically.")) return;
  const button = document.getElementById("update-download");
  button.disabled = true; button.textContent = "Downloading…";
  try {
    const result = await api("update/download", state.updateInfo);
    document.getElementById("update-message").textContent = result.message + " Folder: " + result.folder;
    notify("Update package extracted.");
  } catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.textContent = "Download package"; }
});

async function periodicStateRefresh() {
  if (document.visibilityState !== "visible") return;
  try {
    const next = await api("state");
    applyState(next);
    const download = next.WorkshopDownload || {};
    document.getElementById("download-progress").style.width = (download.Percent || 0) + "%";
    document.getElementById("download-message").textContent = download.Message || "No download running.";
    document.getElementById("map-cancel").disabled = !download.Active;
    document.getElementById("map-download").disabled = !state.selectedMap || state.selectedMap.IsInstalled || !state.selectedMap.DownloadUrl || !!download.Active;
  } catch (_) {}
}

loadState();
setInterval(periodicStateRefresh, 1500);
