const apiToken = document.querySelector('meta[name="api-token"]').content;
const state = { data: null, tab: "Internet", selected: null, toastTimer: null };

async function api(path, payload) {
  const options = { method: payload === undefined ? "GET" : "POST", headers: { "X-FragCity-Token": apiToken } };
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
  state.toastTimer = setTimeout(() => toast.className = "toast", 3200);
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
  if (document.activeElement !== element) element.value = value;
}

function applyState(next) {
  state.data = next;
  render();
}

async function loadState() {
  try { applyState(await api("state")); }
  catch (error) { notify(error.message, true); }
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
  setValueUnlessFocused("internal-port", data.Settings.GameQueryPort);
  setValueUnlessFocused("forward-port", data.Settings.ProxyPort);
  setValueUnlessFocused("setting-query", data.Settings.GameQueryPort);
  setValueUnlessFocused("setting-local", data.Settings.LocalPort);
  setValueUnlessFocused("setting-proxy", data.Settings.ProxyPort);
  setValueUnlessFocused("setting-refresh", data.Settings.RefreshIntervalSeconds);

  document.querySelectorAll(".tab").forEach(button => button.classList.toggle("active", button.dataset.tab === state.tab));
  const filter = document.getElementById("search").value.trim().toLowerCase();
  const list = currentList().filter(s => !filter || [s.Name, s.Ip, s.Map, s.Tags].some(v => String(v || "").toLowerCase().includes(filter)));
  document.getElementById("server-count").textContent = list.length + (list.length === 1 ? " server" : " servers");
  const container = document.getElementById("server-list");
  if (!list.length) {
    container.innerHTML = '<div class="empty"><span>⌁</span><strong>No servers in this list</strong><small>Add a LAN server or copy your existing servers.json / savedservers.json beside the executable.</small></div>';
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
        (state.tab === "Personal" || state.tab === "History" ? '<button class="icon-button" data-action="remove" title="Remove">×</button>' : "") +
        '</div></article>';
    }).join("");
    container.querySelectorAll(".server-row").forEach(row => {
      row.addEventListener("click", event => {
        const index = Number(row.dataset.index);
        const chosen = list[index];
        if (event.target.closest("[data-action]")) {
          event.stopPropagation();
          handleRowAction(event.target.closest("[data-action]").dataset.action, chosen);
          return;
        }
        state.selected = chosen;
        render();
      });
    });
  }

  if (state.selected) {
    document.getElementById("selected-server").innerHTML = '<span class="selection-mark">◎</span><span><strong>' + esc(state.selected.Name || "Selected server") + '</strong><small>' + esc(state.selected.Ip) + ':' + state.selected.Port + '</small></span>';
    document.getElementById("connect").disabled = false;
  } else {
    document.getElementById("selected-server").innerHTML = '<span class="selection-mark">◎</span><span>No server selected</span>';
    document.getElementById("connect").disabled = true;
  }
  if (data.Message) notifyQuiet(data.Message);
}

function notifyQuiet(message) {
  const toast = document.getElementById("toast");
  if (!toast.classList.contains("show")) toast.textContent = message;
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

document.querySelectorAll(".tab").forEach(button => button.addEventListener("click", () => {
  state.tab = button.dataset.tab;
  state.selected = null;
  render();
}));
document.getElementById("search").addEventListener("input", render);

document.getElementById("refresh").addEventListener("click", async () => {
  const button = document.getElementById("refresh");
  button.disabled = true; button.textContent = "Refreshing…";
  try { applyState(await api("refresh", {})); notify("Server status refreshed."); }
  catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.textContent = "Refresh servers"; }
});

document.getElementById("connect").addEventListener("click", async () => {
  if (!state.selected) return;
  const button = document.getElementById("connect");
  button.disabled = true; button.textContent = "Starting UDP tunnel…";
  try {
    applyState(await api("connect", { Group: state.tab, Ip: state.selected.Ip, Port: state.selected.Port }));
    notify("UDP tunnel started. Steam should open shortly.");
  } catch (error) { notify(error.message, true); }
  finally { button.disabled = false; button.innerHTML = 'Connect with Steam <span>↗</span>'; render(); }
});

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

document.getElementById("settings-form").addEventListener("submit", async event => {
  event.preventDefault();
  const settings = Object.assign({}, state.data.Settings, {
    GameQueryPort: Number(document.getElementById("setting-query").value),
    LocalPort: Number(document.getElementById("setting-local").value),
    ProxyPort: Number(document.getElementById("setting-proxy").value),
    RefreshIntervalSeconds: Number(document.getElementById("setting-refresh").value)
  });
  try { applyState(await api("settings", settings)); notify("Settings saved."); }
  catch (error) { notify(error.message, true); }
});

const dialog = document.getElementById("add-dialog");
document.getElementById("add-open").addEventListener("click", () => dialog.showModal());
document.getElementById("add-form").addEventListener("submit", async event => {
  if (event.submitter && event.submitter.value === "cancel") return;
  event.preventDefault();
  const server = {
    Name: document.getElementById("add-name").value.trim(),
    Ip: document.getElementById("add-ip").value.trim(),
    Port: Number(document.getElementById("add-port").value),
    ForwardingPort: Number(document.getElementById("add-forward").value),
    ListeningPort: Number(document.getElementById("add-local").value),
    Ping: -1
  };
  try {
    applyState(await api("add", server));
    dialog.close();
    state.tab = "Personal";
    state.selected = server;
    render();
    notify("LAN server saved.");
    document.getElementById("add-form").reset();
  } catch (error) { notify(error.message, true); }
});

loadState();
setInterval(() => {
  if (document.visibilityState === "visible") loadState();
}, 2500);
