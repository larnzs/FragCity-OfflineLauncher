package launcher

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"net"
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
	mu       sync.RWMutex
	dataDir  string
	internet []ServerConnectionData
	personal []ServerConnectionData
	favorites []ServerConnectionData
	history  []ServerConnectionData
	settings AppSettings

	clientProxy *proxy.ClientProxy
	serverProxy *proxy.ServerProxy
	message     string
}

func NewApp(dataDir string) (*App, error) {
	if err := os.MkdirAll(dataDir, 0755); err != nil {
		return nil, fmt.Errorf("create data directory: %w", err)
	}
	app := &App{
		dataDir: dataDir,
		settings: AppSettings{
			GameQueryPort:          27015,
			LocalPort:              27016,
			ProxyPort:              27017,
			RefreshIntervalSeconds: 15,
			CurrentVersion:         "0.1.0",
		},
		message: "Ready. This launcher works with local server data and does not require an Internet connection for LAN play.",
	}
	_ = readJSON(filepath.Join(dataDir, "servers.json"), &struct {
		Internet *[]ServerConnectionData
	}{Internet: &app.internet})
	var saved SavedServersContainer
	if err := readJSON(filepath.Join(dataDir, "savedservers.json"), &saved); err != nil && !errors.Is(err, os.ErrNotExist) {
		app.message = "Saved server data could not be read; starting with empty saved lists."
	}
	app.personal = saved.Personal
	app.favorites = saved.Favorites
	app.history = saved.History
	if err := readJSON(filepath.Join(dataDir, "settings.json"), &app.settings); err != nil && !errors.Is(err, os.ErrNotExist) {
		app.message = "Settings could not be read; using defaults."
	}
	app.normalizeSettings()
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
	if a.settings.CurrentVersion == "" { a.settings.CurrentVersion = "0.1.0" }
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

func (a *App) State() AppState {
	a.mu.RLock()
	state := AppState{
		Internet: cloneServers(a.internet), Personal: cloneServers(a.personal),
		Favorites: cloneServers(a.favorites), History: cloneServers(a.history),
		Settings: a.settings, Message: a.message,
	}
	client, server := a.clientProxy, a.serverProxy
	a.mu.RUnlock()

	if client != nil && client.IsRunning() {
		state.ClientProxyRunning = true
		state.ClientProxyAddress = client.LocalAddress()
		state.ClientBytesIn, state.ClientBytesOut = client.Counters()
	}
	if server != nil && server.IsRunning() {
		state.ServerProxyRunning = true
		state.ServerProxyAddress = server.LocalAddress()
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
		return errors.New("enter a valid IPv4 or IPv6 address")
	}
	if !validPort(server.Port, false) || !validPort(server.ForwardingPort, true) || !validPort(server.ListeningPort, true) {
		return errors.New("ports must be between 1 and 65535; forwarding and local ports may be left at 0 to use settings")
	}
	if strings.TrimSpace(server.Name) == "" { server.Name = net.JoinHostPort(server.Ip, strconv.Itoa(server.Port)) }
	if server.Map == "" { server.Map = "Unknown" }
	if server.Ping == 0 { server.Ping = -1 }

	a.mu.Lock()
	defer a.mu.Unlock()
	for _, existing := range a.personal {
		if existing.Ip == server.Ip && existing.Port == server.Port {
			return errors.New("that server is already in your personal list")
		}
	}
	a.personal = append(a.personal, server)
	a.message = "LAN server added to Personal."
	return a.saveLocked()
}

func (a *App) RemoveServer(group, ip string, port int) error {
	a.mu.Lock()
	defer a.mu.Unlock()
	var list *[]ServerConnectionData
	switch strings.ToLower(group) {
	case "personal": list = &a.personal
	case "favorites": list = &a.favorites
	case "history": list = &a.history
	default: return errors.New("only Personal, Favorites, or History entries can be removed")
	}
	kept := (*list)[:0]
	for _, s := range *list {
		if s.Ip != ip || s.Port != port { kept = append(kept, s) }
	}
	*list = kept
	a.message = "Server removed."
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
	a.settings = settings
	a.message = "Settings saved."
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
	for _, list := range [][]ServerConnectionData{a.internet, a.personal, a.favorites, a.history} {
		if server, ok := findServer(list, ip, port); ok { return server, true }
	}
	return ServerConnectionData{}, false
}

func (a *App) Connect(group, ip string, port int) error {
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
	if err := launchSteam(localPort); err != nil {
		a.setMessage(fmt.Sprintf("Tunnel is running on 127.0.0.1:%d. Steam could not be opened automatically: %v", localPort, err))
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
	type result struct { key string; info ServerInfo; err error }
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
			queryCtx, cancel := context.WithTimeout(ctx, 1800*time.Millisecond)
			defer cancel()
			info, err := QueryServerInfo(queryCtx, server.Ip, server.Port)
			results <- result{key: key, info: info, err: err}
		}(key, server)
	}
	wg.Wait()
	close(results)

	a.mu.Lock()
	defer a.mu.Unlock()
	for result := range results {
		if result.err != nil { continue }
		update := func(list []ServerConnectionData) {
			for i := range list {
				key := net.JoinHostPort(list[i].Ip, strconv.Itoa(list[i].Port))
				if key != result.key { continue }
				list[i].Name = result.info.Name
				list[i].Map = result.info.Map
				list[i].Players = result.info.Players
				list[i].MaxPlayers = result.info.MaxPlayers
				list[i].HasPassword = result.info.HasPassword
				list[i].IsSecure = result.info.IsSecure
				list[i].Ping = result.info.Ping
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

func (a *App) Close() {
	a.StopClientProxy()
	a.StopServerProxy()
}
