package launcher

import (
	"context"
	"encoding/json"
	"errors"
	"io"
	"io/fs"
	"net"
	"net/http"
	"net/url"
	"strings"
	"time"
)

func NewHTTPHandler(app *App, site fs.FS, token string) http.Handler {
	mux := http.NewServeMux()
	mux.Handle("/api/", localAPI(app, token))
	mux.Handle("/maps/", http.FileServer(http.FS(site)))
	static := http.FileServer(http.FS(site))
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet && r.Method != http.MethodHead {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		if r.URL.Path == "/" {
			index, err := fs.ReadFile(site, "index.html")
			if err != nil {
				http.Error(w, "interface not found", http.StatusInternalServerError)
				return
			}
			html := strings.Replace(string(index), "__API_TOKEN__", token, 1)
			w.Header().Set("Content-Type", "text/html; charset=utf-8")
			w.Header().Set("Cache-Control", "no-store")
			_, _ = io.WriteString(w, html)
			return
		}
		w.Header().Set("Cache-Control", "no-cache")
		static.ServeHTTP(w, r)
	})
	return securityHeaders(mux)
}

func securityHeaders(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("X-Content-Type-Options", "nosniff")
		w.Header().Set("Referrer-Policy", "no-referrer")
		w.Header().Set("X-Frame-Options", "DENY")
		w.Header().Set("Content-Security-Policy", "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'")
		next.ServeHTTP(w, r)
	})
}

func NewAPIHandler(app *App, token string) http.Handler {
	return localAPI(app, token)
}

func localAPI(app *App, token string) http.Handler {
	mux := http.NewServeMux()
	mux.HandleFunc("/api/state", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodGet) { return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/add", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var server ServerConnectionData
		if !decodeJSON(w, r, &server) { return }
		if err := app.AddServer(server); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/add-address", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Address string }
		if !decodeJSON(w, r, &request) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
		defer cancel()
		if err := app.AddServerFromAddress(ctx, request.Address); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/remove", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Group string; Ip string; Port int }
		if !decodeJSON(w, r, &request) { return }
		if err := app.RemoveServer(request.Group, request.Ip, request.Port); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/favorite", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Group string; Ip string; Port int }
		if !decodeJSON(w, r, &request) { return }
		if err := app.ToggleFavorite(request.Group, request.Ip, request.Port); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/start-client", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Group string; Ip string; Port int }
		if !decodeJSON(w, r, &request) { return }
		if err := app.StartClientProxy(request.Group, request.Ip, request.Port); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/connect", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Group string; Ip string; Port int }
		if !decodeJSON(w, r, &request) { return }
		if err := app.Connect(request.Group, request.Ip, request.Port); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/stop-client", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		app.StopClientProxy()
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/server/start", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct {
			BindIP       string
			InternalIP   string
			InternalPort int
			ForwardPort  int
		}
		if !decodeJSON(w, r, &request) { return }
		if err := app.StartServerProxy(request.BindIP, request.ForwardPort, request.InternalIP, request.InternalPort); err != nil {
			writeError(w, http.StatusBadRequest, err)
			return
		}
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/server/stop", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		app.StopServerProxy()
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/settings", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var settings AppSettings
		if !decodeJSON(w, r, &settings) { return }
		if err := app.UpdateSettings(settings); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/refresh", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 15*time.Second)
		defer cancel()
		if err := app.RefreshServers(ctx); err != nil && !errors.Is(err, context.DeadlineExceeded) {
			writeError(w, http.StatusBadGateway, err)
			return
		}
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/sync", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 20*time.Second)
		defer cancel()
		err := app.SyncRemoteData(ctx)
		// Sync is optional: the launcher remains usable when the computer is offline.
		if err != nil { writeJSON(w, http.StatusOK, map[string]any{"state": app.State(), "warning": err.Error()}); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/players", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Ip string; Port int }
		if !decodeJSON(w, r, &request) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 4*time.Second)
		defer cancel()
		players, err := app.Players(ctx, request.Ip, request.Port)
		if err != nil { writeError(w, http.StatusBadGateway, err); return }
		writeJSON(w, http.StatusOK, struct { Players []PlayerInfo }{Players: players})
	})
	mux.HandleFunc("/api/export", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodGet) { return }
		group := r.URL.Query().Get("group")
		if group == "" { group = "all" }
		servers, err := app.ExportServers(group)
		if err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusOK, servers)
	})
	mux.HandleFunc("/api/import", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Servers []ServerConnectionData }
		if !decodeJSON(w, r, &request) { return }
		if len(request.Servers) > 5000 { writeError(w, http.StatusBadRequest, errors.New("import is limited to 5000 servers")); return }
		added, err := app.ImportServers(request.Servers)
		if err != nil { writeError(w, http.StatusInternalServerError, err); return }
		writeJSON(w, http.StatusOK, map[string]any{"added": added, "state": app.State()})
	})

	mux.HandleFunc("/api/workshop/path", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { Path string }
		if !decodeJSON(w, r, &request) { return }
		if err := app.SetWorkshopPath(request.Path); err != nil { writeError(w, http.StatusBadRequest, err); return }
		app.CopyPreparedACF()
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/workshop/refresh", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		app.RefreshWorkshopMaps()
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/workshop/download", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var request struct { WorkshopID int64 }
		if !decodeJSON(w, r, &request) { return }
		if err := app.StartWorkshopDownload(request.WorkshopID); err != nil { writeError(w, http.StatusBadRequest, err); return }
		writeJSON(w, http.StatusAccepted, app.State())
	})
	mux.HandleFunc("/api/workshop/cancel", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		app.CancelWorkshopDownload()
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/workshop/acf", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 10*time.Minute)
		defer cancel()
		if err := app.InstallACF(ctx); err != nil { writeError(w, http.StatusBadGateway, err); return }
		writeJSON(w, http.StatusOK, app.State())
	})
	mux.HandleFunc("/api/update/check", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 20*time.Second)
		defer cancel()
		info, err := app.CheckUpdate(ctx)
		if err != nil { writeError(w, http.StatusBadGateway, err); return }
		writeJSON(w, http.StatusOK, info)
	})
	mux.HandleFunc("/api/update/download", func(w http.ResponseWriter, r *http.Request) {
		if !requireMethod(w, r, http.MethodPost) { return }
		var info UpdateInfo
		if !decodeJSON(w, r, &info) { return }
		ctx, cancel := context.WithTimeout(r.Context(), 15*time.Minute)
		defer cancel()
		destination, err := app.DownloadUpdate(ctx, info)
		if err != nil { writeError(w, http.StatusBadGateway, err); return }
		writeJSON(w, http.StatusOK, map[string]string{"folder": destination, "message": "Update extracted. Close the launcher and replace the application files manually."})
	})

	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		host, _, splitErr := net.SplitHostPort(r.RemoteAddr)
		if splitErr != nil || !net.ParseIP(host).IsLoopback() {
			http.Error(w, "loopback access only", http.StatusForbidden)
			return
		}
		if r.Header.Get("X-FragCity-Token") != token {
			http.Error(w, "unauthorized", http.StatusForbidden)
			return
		}
		if origin := r.Header.Get("Origin"); origin != "" {
			parsed, err := url.Parse(origin)
			if err != nil || parsed.Scheme != "http" || parsed.Host != r.Host {
				http.Error(w, "origin rejected", http.StatusForbidden)
				return
			}
		}
		mux.ServeHTTP(w, r)
	})
}

func requireMethod(w http.ResponseWriter, r *http.Request, method string) bool {
	if r.Method != method {
		w.Header().Set("Allow", method)
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return false
	}
	return true
}

func decodeJSON(w http.ResponseWriter, r *http.Request, dst any) bool {
	defer r.Body.Close()
	decoder := json.NewDecoder(io.LimitReader(r.Body, 8<<20))
	if err := decoder.Decode(dst); err != nil {
		writeError(w, http.StatusBadRequest, err)
		return false
	}
	return true
}

func writeJSON(w http.ResponseWriter, status int, value any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(value)
}

func writeError(w http.ResponseWriter, status int, err error) {
	writeJSON(w, status, map[string]string{"error": err.Error()})
}
