# FragCity Offline Launcher — Go edition

This is the primary Go implementation of FragCity. The launcher, browser interface, Steam Source queries, Workshop manager, update checker, and UDP forwarding engine are packaged into one Windows x64 executable.

## Included features

- Server browser with All, Favorites, Personal and History lists, filtering, server details, player/map counts, ping, VAC/password status, tickrate, game mode and tags.
- Source A2S_INFO, A2S_RULES and A2S_PLAYER queries over UDP.
- Add servers using an IP, hostname or host:port; remove entries; favorite servers; import/export JSON lists.
- Client UDP relay: local CS2 UDP port to the selected host's forwarding port, with optional Steam launch.
- Host UDP relay: LAN clients to a local dedicated server, with a separate return path per client. Public source IPs are rejected by default.
- Workshop catalogue, installed-state detection, search, ZIP download/install, cancellation, safe extraction and ACF download/install.
- Optional remote server/workshop/announcement synchronization.
- Manual update manifest check and download to UpdatePackage. The running app is not overwritten; replacement remains a manual step.
- Ports, auto-refresh, Workshop directory and remote URL settings.
- Reads/writes existing PascalCase servers.json, savedservers.json and settings.json formats.
- Embedded Go runtime, browser UI, default workshop data and map thumbnails; no .NET, Go install or external proxy.exe needed.

## Run

Double-click FragCity-Offline-Launcher.exe. It starts a loopback-only control panel and opens it in the default browser. The local API binds only to 127.0.0.1 and uses a fresh per-run token.

If the browser does not open automatically, use the local URL printed by the app.

## LAN setup

### Host PC

1. Start FragCity on the PC running the CS2 dedicated server.
2. Open Server browser → LAN Host Mode.
3. Set the dedicated server's local IP and UDP game port (usually 127.0.0.1 and your CS2 game port).
4. Choose a LAN forwarding port (for example 27017) and start the relay.
5. Allow that UDP port in Windows Firewall on the Private network profile.

You can also run this in PowerShell:

```powershell
FragCity-Offline-Launcher.exe server --internal-ip 127.0.0.1 --internal 27015 --ip 0.0.0.0 --port 27017
```

### Client PC

1. Connect both PCs to the same LAN.
2. Open FragCity → Add server and enter the host's private LAN IP. Use the dedicated server query port (default 27015).
3. Set the forwarding port to the host relay port (for example 27017). The local port (default 27016) must be free.
4. Select the server and choose Connect with Steam.

The launcher asks Steam to connect CS2 to 127.0.0.1 on the local port. The UDP data path uses no central server, cloud relay or Internet connection. Steam/CS2 may still have its own sign-in or offline-mode requirements.

Direct CLI relay usage:

```powershell
# Client side
FragCity-Offline-Launcher.exe client --local 27016 --port 27017 --target 192.168.1.50

# Host side
FragCity-Offline-Launcher.exe server --internal-ip 127.0.0.1 --internal 27015 --ip 0.0.0.0 --port 27017
```

## Local files

Writable data is stored next to the executable: servers.json, savedservers.json, settings.json, workshop_maps.json, acf.json, announcement.txt and UpdatePackage/ (only created by the updater). Back these up before replacing the executable. Existing C# server and settings JSON can be reused.

## Build

Requires Go 1.23 or later. From this folder:

```powershell
gofmt -w .
go test ./...
$env:CGO_ENABLED = "0"
$env:GOOS = "windows"
$env:GOARCH = "amd64"
go build -trimpath -ldflags="-s -w" -o dist/FragCity-Offline-Launcher.exe .
```

GitHub Actions runs the package tests and produces a single-file Windows x64 artifact.

## Networking notes

- The relay forwards UDP datagrams, not arbitrary TCP traffic.
- Host mode accepts private, loopback and link-local source IPs only. Do not expose it with a public router port-forward.
- Ensure the chosen ports are free and allow the host relay through Windows Firewall on the Private profile.
- The old proxy.exe source was not included in the repository. The Go edition implements a new UDP forwarding path; run the Go version at both ends instead of mixing it with the unknown legacy binary.
- CI tests cover UDP request/reply forwarding, legacy JSON compatibility and ZIP traversal protection. A real multi-PC CS2 dedicated-server session is still needed for full end-to-end validation.
