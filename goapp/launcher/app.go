package launcher

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"sync"
	"time"

	"fragcity/proxy"
)

type App struct {
	mu        sync.RWMutex
	dataDir   string
	internet  []ServerConnectionData
	personal  []ServerConnectionData
	favorites []ServerConnectionData
	history   []ServerConnectionData
	settings  AppSettings

	clientProxy *proxy.ClientProxy
	serverProxy *proxy.ServerProxy
	message     string

	workshopMaps     []WorkshopMapItem
	workshopCancel   context.CancelFunc
	workshopDownload WorkshopDownloadState
	announcement     string
	steamID          string
	acfURL           string
}

func NewApp(dataDir string) (*App, error) {
	if err := os.MkdirAll(dataDir, 0755); err != nil {
		return nil, fmt.Errorf("create data directory: %w", err)
	}
	app := &App{
		dataDir: dataDir,
		settings: AppSettings{
			GameQueryPort: 27015, LocalPort: 27016, ProxyPort: 27017,
			RefreshIntervalSeconds: 15, CurrentVersion: "0.2.0",
		},
		message: "Ready. LAN play and UDP forwarding do not require Internet access.",
	}
	if err := readJSON(filepath.Join(dataDir, "settings.json"), &app.settings); err != nil && !errors.Is(err, os.ErrNotExist) {
		app.message = "Settings could not be read; using defaults."
	}
	app.normalizeSettings()

	var internet ServerInternetContainer
	if err := readJSON(filepath.Join(dataDir, "servers.json"), &internet); err == nil {
		app.internet = internet.Internet
	} else if !errors.Is(err, os.ErrNotExist) {
		app.message = "Server list could not be read; starting with an empty list."
	}
	var saved SavedServersContainer
	if err := readJSON(filepath.Join(dataDir, "savedservers.json"), &saved); err == nil {
		app.personal, app.favorites, app.history = saved.Personal, saved.Favorites, saved.History
	} else if !errors.Is(err, os.ErrNotExist) {
		app.message = "Saved server data could not be read; starting with empty saved lists."
	}
	if app.settings.WorkshopPath == "" {
		app.settings.WorkshopPath = discoverWorkshopPath()
	}
	app.reloadWorkshopMaps()
	app.loadAnnouncement()
	app.loadACFURL()
	_ = app.CopyPreparedACF()
	app.steamID = readSteamID()
	return app, nil
}

func readJSON(path string, dst any) error {
	data, err := os.ReadFile(path)
	if err != nil {
		return err
	}
	if len(strings.TrimSpace(string(data))) == 0 {
		return nil
	}
	if err := json.Unmarshal(data, dst); err != nil {
		return fmt.Errorf("parse %s: %w", filepath.Base(path), err)
	}
	return nil
}

func (a *App) normalizeSettings() {
	if a.settings.GameQueryPort == 0 { a.settings.GameQueryPort = 27015 }
	if a.settings.LocalPort == 0 { a.settings.LocalPort = 27016 }
	if a.settings.ProxyPort == 0 { a.settings.ProxyPort = 27017 }
	if a.settings.RefreshIntervalSeconds <= 0 { a.settings.RefreshIntervalSeconds = 15 }
	if a.settings.CurrentVersion == "" { a.settings.CurrentVersion = "0.2.0" }
}

func atomicWrite(path string, data []byte) error {
	temp := path + ".tmp"
	if err := os.WriteFile(temp, data, 0644); err != nil {
		return err
	}
	if err := os.Rename(temp, path); err != nil {
		_ = os.Remove(temp)
		return err
	}
	return nil
}

func (a *App) saveLocked() error {
	internetJSON, err := json.MarshalIndent(ServerInternetContainer{Internet: a.internet}, "", "  ")
	if err != nil { return err }
	savedJSON, err := json.MarshalIndent(SavedServersContainer{
		Personal: a.personal, Favorites: a.favorites, History: a.history,
	}, "", "  ")
	if err != nil { return err }
	settingsJSON, err := json.MarshalIndent(a.settings, "", "  ")
	if err != nil { return err }
	if err := atomicWrite(filepath.Join(a.dataDir, "servers.json"), internetJSON); err != nil { return err }
	if err := atomicWrite(filepath.Join(a.dataDir, "savedservers.json"), savedJSON); err != nil { return err }
	return atomicWrite(filepath.Join(a.dataDir, "settings.json"), settingsJSON)
}

func cloneServers(input []ServerConnectionData) []ServerConnectionData {
	return append([]ServerConnectionData(nil), input...)
}

func cloneMaps(input []WorkshopMapItem) []WorkshopMapItem {
	return append([]WorkshopMapItem(nil), input...)
}

func (a *App) State() AppState {
	a.mu.RLock()
	state := AppState{
		Internet: cloneServers(a.internet), Personal: cloneServers(a.personal),
		Favorites: cloneServers(a.favorites), History: cloneServers(a.history),
		Settings: a.settings, Message: a.message, Announcement: a.announcement,
		SteamID: a.steamID, WorkshopMaps: cloneMaps(a.workshopMaps),
		WorkshopPath: a.settings.WorkshopPath, WorkshopDownload: a.workshopDownload,
		ACFAvailable: a.acfURL != "",
	}
	client, server := a.clientProxy, a.serverProxy
	a.mu.RUnlock()
	if client != nil && client.IsRunning() {
		state.ClientProxyRunning, state.ClientProxyAddress = true, client.LocalAddress()
		state.ClientBytesIn, state.ClientBytesOut = client.Counters()
	}
	if server != nil && server.IsRunning() {
		state.ServerProxyRunning, state.ServerProxyAddress = true, server.LocalAddress()
		state.ServerBytesIn, state.ServerBytesOut, state.BlockedPublicPeers = server.Counters()
	}
	return state
}

func (a *App) setMessage(message string) {
	a.mu.Lock()
	a.message = message
	a.mu.Unlock()
}

func validPort(port int, allowZero bool) bool {
	if allowZero && port == 0 { return true }
	return port >= 1 && port <= 65535
}

func (a *App) AddServer(server ServerConnectionData) error {
	if net.ParseIP(server.Ip) == nil {
		return errors.New("enter a valid IP address")
	}
	if !validPort(server.Port, false) || !validPort(server.ForwardingPort, true) || !validPort(server.ListeningPort, true) {
		return errors.New("ports must be between 1 and 65535; forwarding and local ports can be 0 to use defaults")
	}
	if strings.TrimSpace(server.Name) == "" { server.Name = net.JoinHostPort(server.Ip, strconv.Itoa(server.Port)) }
	if server.Map == "" { server.Map = "Unknown" }
	if server.Ping == 0 { server.Ping = -1 }

	a.mu.Lock()
	defer a.mu.Unlock()
	for _, existing := range a.personal {
		if existing.Ip == server.Ip && existing.Port == server.Port {
			return errors.New("that server is already in your Personal list")
		}
	}
	a.personal = append(a.personal, server)
	a.message = "Server added to Personal."
	return a.saveLocked()
}

func (a *App) AddServerFromAddress(ctx context.Context, address string) error {
	a.mu.RLock()
	defaultPort, localPort, forwardPort := a.settings.GameQueryPort, a.settings.LocalPort, a.settings.ProxyPort
	a.mu.RUnlock()
	ip, port, err := parseServerAddress(address, defaultPort)
	if err != nil { return err }

	server := ServerConnectionData{
		Ip: ip, Port: port, ForwardingPort: forwardPort, ListeningPort: localPort,
		Name: net.JoinHostPort(ip, strconv.Itoa(port)), Map: "Unknown", Ping: -1, Tags: "Official",
	}
	queryCtx, cancel := context.WithTimeout(ctx, 2500*time.Millisecond)
	info, queryErr := QueryServerInfo(queryCtx, ip, port)
	cancel()
	if queryErr == nil {
		server.Name, server.Map = info.Name, info.Map
		server.Players, server.MaxPlayers = info.Players, info.MaxPlayers
		server.Ping, server.HasPassword, server.IsSecure = info.Ping, info.HasPassword, info.IsSecure
	}
	rulesCtx, rulesCancel := context.WithTimeout(ctx, 1500*time.Millisecond)
	rules, _ := QueryServerRules(rulesCtx, ip, port)
	rulesCancel()
	server.TickRate = rulesInt(rules, "tickrate", "sv_tickrate")
	server.Tags = rulesString(rules, "Official", "sv_tags", "tags")
	server.GameMode = rulesString(rules, "", "game_des", "GameMode", "sv_game_mode", "game_mode")
	if server.Name == "" { server.Name = net.JoinHostPort(ip, strconv.Itoa(port)) }
	if queryErr != nil { server.Name += " (Offline)" }
	return a.AddServer(server)
}

func (a *App) RemoveServer(group, ip string, port int) error {
	a.mu.Lock()
	defer a.mu.Unlock()
	var list *[]ServerConnectionData
	switch strings.ToLower(group) {
	case "internet": list = &a.internet
	case "personal": list = &a.personal
	case "favorites": list = &a.favorites
	case "history": list = &a.history
	default: return errors.New("unknown server list")
	}
	kept := (*list)[:0]
	for _, server := range *list {
		if server.Ip != ip || server.Port != port { kept = append(kept, server) }
	}
	*list = kept
	if a.clientProxy == nil { a.message = "Server removed." }
	return a.saveLocked()
}

func (a *App) ToggleFavorite(group, ip string, port int) error {
	a.mu.Lock()
	defer a.mu.Unlock()
	var selected *ServerConnectionData
	for _, list := range [][]ServerConnectionData{a.internet, a.personal, a.favorites, a.history} {
		for i := range list {
			if list[i].Ip == ip && list[i].Port == port {
				copy := list[i]
				selected = &copy
				break
			}
		}
		if selected != nil { break }
	}
	if selected == nil { return errors.New("server not found") }
	for i := range a.favorites {
		if a.favorites[i].Ip == ip && a.favorites[i].Port == port {
			a.favorites = append(a.favorites[:i], a.favorites[i+1:]...)
			a.message = "Removed from Favorites."
			return a.saveLocked()
		}
	}
	a.favorites = append(a.favorites, *selected)
	a.message = "Added to Favorites."
	return a.saveLocked()
}

func (a *App) UpdateSettings(settings AppSettings) error {
	if !validPort(settings.GameQueryPort, false) || !validPort(settings.LocalPort, false) || !validPort(settings.ProxyPort, false) {
		return errors.New("game, local, and proxy ports must be between 1 and 65535")
	}
	if settings.RefreshIntervalSeconds < 5 || settings.RefreshIntervalSeconds > 3600 {
		return errors.New("refresh interval must be between 5 and 3600 seconds")
	}
	a.mu.Lock()
	defer a.mu.Unlock()
	settings.CurrentVersion = a.settings.CurrentVersion
	if settings.WorkshopPath == "" { settings.WorkshopPath = a.settings.WorkshopPath }
	a.settings = settings
	a.message = "Settings saved."
	a.refreshWorkshopInstalledLocked()
	return a.saveLocked()
}

func findServer(list []ServerConnectionData, ip string, port int) (ServerConnectionData, bool) {
	for _, server := range list {
		if server.Ip == ip && server.Port == port { return server, true }
	}
	return ServerConnectionData{}, false
}

func (a *App) findServerLocked(group, ip string, port int) (ServerConnectionData, bool) {
	switch strings.ToLower(group) {
	case "internet": return findServer(a.internet, ip, port)
	case "personal": return findServer(a.personal, ip, port)
	case "favorites": return findServer(a.favorites, ip, port)
	case "history": return findServer(a.history, ip, port)
	}
	return ServerConnectionData{}, false
}

func (a *App) Connect(group, ip string, port int) error {
	return a.startClientTunnel(group, ip, port, true)
}

func (a *App) StartClientProxy(group, ip string, port int) error {
	return a.startClientTunnel(group, ip, port, false)
}

func (a *App) startClientTunnel(group, ip string, port int, openSteam bool) error {
	a.mu.RLock()
	server, ok := a.findServerLocked(group, ip, port)
	settings := a.settings
	a.mu.RUnlock()
	if !ok { return errors.New("select a server first") }
	localPort := server.ListeningPort
	if localPort == 0 { localPort = settings.LocalPort }
	remotePort := server.ForwardingPort
	if remotePort == 0 { remotePort = settings.ProxyPort }

	a.StopClientProxy()
	client, err := proxy.NewClientProxy(localPort, server.Ip, remotePort)
	if err != nil { return err }
	if err := client.Start(); err != nil { return err }
	a.mu.Lock()
	a.clientProxy = client
	server.ConnectedAt = time.Now()
	for i := range a.history {
		if a.history[i].Ip == server.Ip && a.history[i].Port == server.Port {
			a.history = append(a.history[:i], a.history[i+1:]...)
			break
		}
	}
	a.history = append([]ServerConnectionData{server}, a.history...)
	if len(a.history) > 100 { a.history = a.history[:100] }
	a.message = fmt.Sprintf("UDP tunnel active: 127.0.0.1:%d → %s:%d", localPort, server.Ip, remotePort)
	_ = a.saveLocked()
	a.mu.Unlock()

	go func() {
		<-client.Done()
		a.mu.Lock()
		if a.clientProxy == client {
			a.clientProxy = nil
			a.message = "Client UDP tunnel stopped."
		}
		a.mu.Unlock()
	}()
	if openSteam {
		if err := launchSteam(localPort); err != nil {
			a.setMessage(fmt.Sprintf("Tunnel active on 127.0.0.1:%d. Steam could not be opened automatically: %v", localPort, err))
		}
	}
	return nil
}

func launchSteam(localPort int) error {
	if runtime.GOOS != "windows" { return errors.New("automatic Steam launch is available on Windows") }
	uri := "steam://rungameid/730//+connect%20127.0.0.1%3A" + strconv.Itoa(localPort)
	return exec.Command("rundll32.exe", "url.dll,FileProtocolHandler", uri).Start()
}

func (a *App) StopClientProxy() {
	a.mu.Lock()
	client := a.clientProxy
	a.clientProxy = nil
	a.mu.Unlock()
	if client != nil { client.Stop() }
	a.setMessage("Client UDP tunnel stopped.")
}

func (a *App) StartServerProxy(bindIP string, forwardPort int, internalIP string, internalPort int) error {
	a.StopServerProxy()
	if bindIP == "" { bindIP = "0.0.0.0" }
	if internalIP == "" { internalIP = "127.0.0.1" }
	server, err := proxy.NewServerProxy(bindIP, forwardPort, internalIP, internalPort)
	if err != nil { return err }
	if err := server.Start(); err != nil { return err }
	a.mu.Lock()
	a.serverProxy = server
	a.message = fmt.Sprintf("LAN UDP relay active: %s:%d → %s:%d", bindIP, forwardPort, internalIP, internalPort)
	a.mu.Unlock()
	go func() {
		<-server.Done()
		a.mu.Lock()
		if a.serverProxy == server {
			a.serverProxy = nil
			a.message = "Server UDP relay stopped."
		}
		a.mu.Unlock()
	}()
	return nil
}

func (a *App) StopServerProxy() {
	a.mu.Lock()
	server := a.serverProxy
	a.serverProxy = nil
	a.mu.Unlock()
	if server != nil { server.Stop() }
	a.setMessage("Server UDP relay stopped.")
}

func (a *App) RefreshServers(ctx context.Context) error {
	a.mu.RLock()
	all := append(cloneServers(a.internet), a.personal...)
	all = append(all, a.favorites...)
	all = append(all, a.history...)
	a.mu.RUnlock()
	unique := make(map[string]ServerConnectionData)
	for _, server := range all {
		key := net.JoinHostPort(server.Ip, strconv.Itoa(server.Port))
		unique[key] = server
	}
	type result struct { key string; info ServerInfo; rules map[string]string; err error }
	results := make(chan result, len(unique))
	sem := make(chan struct{}, 16)
	var wg sync.WaitGroup
	for key, server := range unique {
		wg.Add(1)
		go func(key string, server ServerConnectionData) {
			defer wg.Done()
			select {
			case sem <- struct{}{}:
			case <-ctx.Done():
				results <- result{key: key, err: ctx.Err()}
				return
			}
			defer func(){ <-sem }()
			queryCtx, cancel := context.WithTimeout(ctx, 2*time.Second)
			info, err := QueryServerInfo(queryCtx, server.Ip, server.Port)
			cancel()
			var rules map[string]string
			if err == nil {
				rulesCtx, rulesCancel := context.WithTimeout(ctx, 1200*time.Millisecond)
				rules, _ = QueryServerRules(rulesCtx, server.Ip, server.Port)
				rulesCancel()
			}
			results <- result{key: key, info: info, rules: rules, err: err}
		}(key, server)
	}
	wg.Wait()
	close(results)
	a.mu.Lock()
	defer a.mu.Unlock()
	for result := range results {
		update := func(list []ServerConnectionData) {
			for i := range list {
				key := net.JoinHostPort(list[i].Ip, strconv.Itoa(list[i].Port))
				if key != result.key { continue }
				if result.err != nil { list[i].Ping = -1; continue }
				list[i].Name, list[i].Map = result.info.Name, result.info.Map
				list[i].Players, list[i].MaxPlayers = result.info.Players, result.info.MaxPlayers
				list[i].HasPassword, list[i].IsSecure, list[i].Ping = result.info.HasPassword, result.info.IsSecure, result.info.Ping
				list[i].TickRate = rulesInt(result.rules, "tickrate", "sv_tickrate")
				list[i].Tags = rulesString(result.rules, list[i].Tags, "sv_tags", "tags")
				list[i].GameMode = rulesString(result.rules, list[i].GameMode, "game_des", "GameMode", "sv_game_mode", "game_mode")
			}
		}
		update(a.internet); update(a.personal); update(a.favorites); update(a.history)
	}
	a.message = "Server status refresh completed."
	return a.saveLocked()
}

func (a *App) Players(ctx context.Context, ip string, port int) ([]PlayerInfo, error) {
	return QueryPlayers(ctx, ip, port)
}

func (a *App) ExportServers(group string) ([]ServerConnectionData, error) {
	a.mu.RLock()
	defer a.mu.RUnlock()
	switch strings.ToLower(group) {
	case "internet": return cloneServers(a.internet), nil
	case "personal": return cloneServers(a.personal), nil
	case "favorites": return cloneServers(a.favorites), nil
	case "history": return cloneServers(a.history), nil
	case "all":
		all := cloneServers(a.internet)
		all = append(all, a.personal...)
		return all, nil
	default: return nil, errors.New("unknown server list")
	}
}

func (a *App) ImportServers(servers []ServerConnectionData) (int, error) {
	a.mu.Lock()
	defer a.mu.Unlock()
	added := 0
	for _, server := range servers {
		if net.ParseIP(server.Ip) == nil || !validPort(server.Port, false) { continue }
		exists := false
		for _, old := range a.personal {
			if old.Ip == server.Ip && old.Port == server.Port { exists = true; break }
		}
		if !exists { a.personal = append(a.personal, server); added++ }
	}
	a.message = fmt.Sprintf("Imported %d server(s).", added)
	return added, a.saveLocked()
}

func (a *App) SyncRemoteData(ctx context.Context) error {
	a.mu.RLock()
	settings := a.settings
	a.mu.RUnlock()
	client := &http.Client{Timeout: 10 * time.Second}
	var notices []string
	if settings.ServerUpdateUrl != "" {
		req, err := http.NewRequestWithContext(ctx, http.MethodGet, settings.ServerUpdateUrl, nil)
		if err != nil { notices = append(notices, "server list URL invalid"); } else {
			resp, requestErr := client.Do(req)
			if requestErr != nil { notices = append(notices, "server list sync: "+requestErr.Error()) } else {
				defer resp.Body.Close()
				var remote RemoteServerContainer
				if resp.StatusCode != http.StatusOK { notices = append(notices, fmt.Sprintf("server list sync returned HTTP %d", resp.StatusCode))
				} else if decodeErr := json.NewDecoder(io.LimitReader(resp.Body, 4<<20)).Decode(&remote); decodeErr != nil { notices = append(notices, "server list JSON: "+decodeErr.Error())
				} else {
					a.mu.Lock()
					added := 0
					for _, cfg := range remote.Internet {
						ip := net.ParseIP(cfg.TargetIp)
						if ip == nil || !validPort(cfg.GameQueryPort, false) { continue }
						found := false
						for _, existing := range a.internet {
							if existing.Ip == ip.String() && existing.Port == cfg.GameQueryPort { found = true; break }
						}
						if !found {
							a.internet = append(a.internet, ServerConnectionData{
								Ip: ip.String(), Port: cfg.GameQueryPort, ForwardingPort: cfg.ForwardingPort,
								ListeningPort: cfg.ListeningPort, Name: "Unknown", Map: "Unknown",
								Ping: -1, Tags: "Official",
							})
							added++
						}
					}
					a.message = fmt.Sprintf("Remote server sync added %d server(s).", added)
					_ = a.saveLocked()
					a.mu.Unlock()
				}
			}
		}
	}
	if settings.WorkshopMapUpdateUrl != "" {
		req, err := http.NewRequestWithContext(ctx, http.MethodGet, settings.WorkshopMapUpdateUrl, nil)
		if err != nil { notices = append(notices, "workshop map URL invalid") } else {
			resp, requestErr := client.Do(req)
			if requestErr != nil { notices = append(notices, "workshop map sync: "+requestErr.Error()) } else {
				defer resp.Body.Close()
				var remote []WorkshopMapItem
				if resp.StatusCode != http.StatusOK { notices = append(notices, fmt.Sprintf("workshop map sync returned HTTP %d", resp.StatusCode))
				} else if decodeErr := json.NewDecoder(io.LimitReader(resp.Body, 8<<20)).Decode(&remote); decodeErr != nil { notices = append(notices, "workshop map JSON: "+decodeErr.Error())
				} else {
					a.mu.Lock()
					byID := make(map[int64]bool)
					for _, m := range a.workshopMaps { byID[m.WorkshopId] = true }
					added := 0
					for _, m := range remote {
						if m.WorkshopId > 0 && m.MapName != "" && !byID[m.WorkshopId] {
							a.workshopMaps = append(a.workshopMaps, m)
							byID[m.WorkshopId] = true
							added++
						}
					}
					_ = a.saveWorkshopMapsLocked()
					a.refreshWorkshopInstalledLocked()
					a.message = fmt.Sprintf("Remote workshop sync added %d map(s).", added)
					a.mu.Unlock()
				}
			}
		}
	}
	if settings.AnnouncementUrl != "" {
		req, err := http.NewRequestWithContext(ctx, http.MethodGet, settings.AnnouncementUrl, nil)
		if err == nil {
			if resp, requestErr := client.Do(req); requestErr == nil {
				body, readErr := io.ReadAll(io.LimitReader(resp.Body, 64<<10))
				resp.Body.Close()
				if readErr == nil && resp.StatusCode == http.StatusOK {
					_ = atomicWrite(filepath.Join(a.dataDir, "announcement.txt"), body)
					a.mu.Lock()
					a.announcement = strings.TrimSpace(string(body))
					a.mu.Unlock()
				}
			}
		}
	}
	if len(notices) > 0 {
		a.setMessage("Remote sync finished with warnings: " + strings.Join(notices, " · "))
		return errors.New(strings.Join(notices, "; "))
	}
	return nil
}

func (a *App) loadAnnouncement() {
	data, err := os.ReadFile(filepath.Join(a.dataDir, "announcement.txt"))
	if err == nil { a.announcement = strings.TrimSpace(string(data)) }
}

func readSteamID() string {
	if runtime.GOOS != "windows" { return "" }
	output, err := exec.Command("reg.exe", "query", `HKCU\Software\Valve\Steam\ActiveProcess`, "/v", "SteamID").Output()
	if err != nil { return "" }
	fields := strings.Fields(string(output))
	for i := len(fields)-1; i >= 0; i-- {
		value := strings.TrimSpace(fields[i])
		if strings.HasPrefix(strings.ToLower(value), "0x") {
			if n, parseErr := strconv.ParseUint(value[2:], 16, 64); parseErr == nil { return strconv.FormatUint(n, 10) }
		}
		if n, parseErr := strconv.ParseUint(value, 10, 64); parseErr == nil { return strconv.FormatUint(n, 10) }
	}
	return ""
}

func discoverWorkshopPath() string {
	if runtime.GOOS == "windows" {
		output, err := exec.Command("reg.exe", "query", `HKCU\Software\Valve\Steam`, "/v", "SteamPath").Output()
		if err == nil {
			fields := strings.Fields(string(output))
			if len(fields) > 0 {
				steamPath := strings.ReplaceAll(fields[len(fields)-1], "/", string(os.PathSeparator))
				candidate := filepath.Join(steamPath, "steamapps", "workshop")
				if info, statErr := os.Stat(candidate); statErr == nil && info.IsDir() { return candidate }
			}
		}
	}
	candidates := []string{}
	if runtime.GOOS == "windows" {
		if pf := os.Getenv("ProgramFiles(x86)"); pf != "" { candidates = append(candidates, filepath.Join(pf, "Steam", "steamapps", "workshop")) }
		if pf := os.Getenv("ProgramFiles"); pf != "" { candidates = append(candidates, filepath.Join(pf, "Steam", "steamapps", "workshop")) }
	} else if home, err := os.UserHomeDir(); err == nil {
		candidates = append(candidates, filepath.Join(home, ".steam", "steam", "steamapps", "workshop"))
	}
	for _, candidate := range candidates {
		if info, err := os.Stat(candidate); err == nil && info.IsDir() { return candidate }
	}
	return ""
}

func (a *App) Close() {
	a.StopClientProxy()
	a.StopServerProxy()
	a.CancelWorkshopDownload()
}
