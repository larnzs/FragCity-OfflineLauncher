package launcher

import (
	"os"
	"path/filepath"
	"testing"
)

func TestLoadsLegacyPascalCaseFiles(t *testing.T) {
	dir := t.TempDir()
	servers := `{"Internet":[{"Ip":"192.168.1.50","Port":27015,"ForwardingPort":27017,"ListeningPort":27016,"Name":"LAN Test","Map":"de_dust2","Players":2,"MaxPlayers":10,"Ping":12}]}`
	saved := `{"Personal":[{"Ip":"10.0.0.2","Port":27015,"Name":"Personal"}],"Favorites":[],"History":[]}`
	settings := `{"GameQueryPort":27015,"LocalPort":27016,"ProxyPort":27017,"AutoRefresh":false,"RefreshIntervalSeconds":15,"CurrentVersion":"1.7.0"}`
	for name, data := range map[string]string{
		"servers.json": servers,
		"savedservers.json": saved,
		"settings.json": settings,
	} {
		if err := os.WriteFile(filepath.Join(dir, name), []byte(data), 0600); err != nil {
			t.Fatal(err)
		}
	}

	app, err := NewApp(dir)
	if err != nil { t.Fatal(err) }
	defer app.Close()
	state := app.State()
	if len(state.Internet) != 1 || state.Internet[0].Name != "LAN Test" {
		t.Fatalf("legacy Internet list was not loaded: %#v", state.Internet)
	}
	if len(state.Personal) != 1 || state.Personal[0].Ip != "10.0.0.2" {
		t.Fatalf("legacy Personal list was not loaded: %#v", state.Personal)
	}
	if state.Settings.LocalPort != 27016 || state.Settings.CurrentVersion != "1.7.0" {
		t.Fatalf("legacy settings were not loaded: %#v", state.Settings)
	}
	if err := app.AddServer(ServerConnectionData{Ip:"192.168.1.51", Port:27015, ForwardingPort:27017, ListeningPort:27016}); err != nil {
		t.Fatal(err)
	}
	reloaded, err := NewApp(dir)
	if err != nil { t.Fatal(err) }
	defer reloaded.Close()
	if len(reloaded.State().Personal) != 2 {
		t.Fatalf("saved server was not persisted: %#v", reloaded.State().Personal)
	}
}
