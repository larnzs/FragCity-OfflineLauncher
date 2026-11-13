# FragCity Offline Launcher — Go edition

This is the first Go migration branch of FragCity Offline Launcher. It replaces the separate `proxy.exe` child process with an in-process UDP relay and a local control panel bundled into a single Go executable.

## Current migration scope

Implemented in this branch:

- LAN server list, Personal, Favorites, and History lists.
- Reads and writes the existing PascalCase `servers.json`, `savedservers.json`, and `settings.json` data formats.
- Add/remove/favorite LAN servers.
- Source-engine A2S_INFO server status query and A2S_PLAYER query.
- Client UDP relay: local CS2 UDP port to a selected LAN host and forwarding port.
- Host UDP relay: private/LAN clients to a local game-server IP and port, with a separate return socket per client.
- LAN-only default: public source IPs are rejected by host mode.
- Embedded local control panel; it binds only to 127.0.0.1 and uses a per-launch API token.
- Windows x64 single-file build with CGO disabled; Go runtime is included in the executable.

Not yet migrated from the C# application:

- Workshop map download/installation and ACF management.
- Remote server/workshop list synchronization, announcements, and the C# auto-updater.
- The original native WinForms layout and its additional server detail/context-menu screens.

The previous binary's source is not present in this repository. This Go version therefore implements its own plain bidirectional UDP forwarding path; run the Go client and Go host relay together rather than mixing this edition with an unknown legacy proxy binary until compatibility has been tested.

## LAN setup

### On the PC hosting the CS2 dedicated server

Run the Go executable in server mode:

```powershell
FragCity-Offline-Launcher.exe server --internal-ip 127.0.0.1 --internal 27015 --ip 0.0.0.0 --port 27017
```

- `--internal-ip`: address where the dedicated server is listening (usually 127.0.0.1).
- `--internal`: dedicated server's UDP game port.
- `--ip`: local interface for the relay; use 0.0.0.0 to listen on available IPv4 interfaces.
- `--port`: UDP port that LAN players send to.

Alternatively, open the desktop launcher and use **LAN Host Mode**.

Allow the forwarding UDP port on the Windows Firewall **Private** profile. Do not create a public router port-forward for this LAN-only build.

### On each LAN player's PC

Open the launcher, add a Personal server:

- IP: the host PC's private LAN IP, for example 192.168.1.50.
- Game query port: the dedicated server game port (for example 27015).
- Forwarding port: the host relay port (for example 27017).
- Local connect port: a free local UDP port (default 27016).

Select the server and choose **Connect with Steam**. The launcher starts a local UDP relay and asks Steam to connect CS2 to 127.0.0.1 on the local port.

Both PCs must be on the same LAN. No central service, cloud relay, or external server list is needed for this flow. Steam/CS2 itself must still be installed and configured to permit LAN play.

## Build

From this directory:

```powershell
go test ./...
$env:CGO_ENABLED = "0"
$env:GOOS = "windows"
$env:GOARCH = "amd64"
go build -trimpath -ldflags="-s -w" -o dist/FragCity-OfflineLauncher.exe .
```

The GitHub Actions workflow builds and uploads `FragCity-OfflineLauncher.exe` for Windows x64. The executable has no external Go DLL/runtime dependency; the UI opens in the system browser using the embedded HTML/CSS/JS files.

## Important

This relay forwards UDP datagrams only. The host intentionally accepts only private, loopback, and link-local source addresses by default. The Go rewrite has not yet been tested against a live CS2 dedicated server or every legacy proxy behavior; test on a private LAN before using it in production.
