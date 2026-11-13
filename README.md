# FragCity Offline Launcher — Go edition

FragCity is a Windows launcher and LAN-focused CS2 UDP relay. The primary application is written in Go. The launcher, browser-based interface, Source server queries, Workshop tools and proxy engine are packaged into one self-contained executable.

## Features

- Server browser with All, Favorites, Personal and History lists, search/filtering and server details.
- Source Engine A2S_INFO, A2S_RULES and A2S_PLAYER queries over UDP.
- Add servers by IP, hostname or host:port; remove/favorite; import and export JSON lists.
- Client UDP relay from the local CS2 port to the selected LAN host's forwarding port.
- Host UDP relay from LAN clients to a local dedicated server, with independent return paths per client.
- Host mode rejects public source IPs by default (private, loopback and link-local ranges are accepted).
- Workshop map catalogue, download/install, cancellation, safe ZIP extraction, installed detection and ACF download/install.
- Optional server/workshop list and announcement synchronization.
- Update manifest check and download/extraction into UpdatePackage (the running executable is not overwritten automatically).
- Local settings for query/local/forwarding ports, refresh schedule, Workshop path and optional remote URLs.
- Imports the existing PascalCase servers.json, savedservers.json and settings.json formats.
- Bundles the web interface, default Workshop data and map thumbnails in the executable. No .NET, Go runtime or separate proxy.exe is required.

The original C# source is preserved in legacy/csharp/ for reference. It is no longer the build target.

## Run

Double-click FragCity-Offline-Launcher.exe. It starts a loopback-only local control panel and opens it in your default browser. The HTTP API binds to 127.0.0.1 and requires a random token generated for that launch.

If your browser does not open automatically, open the local URL printed by the process.

## LAN setup

### Host PC (the PC running the CS2 dedicated server)

1. Start FragCity on the host.
2. Open Server browser → LAN Host Mode.
3. Set the dedicated server's local IP and UDP game port (commonly 127.0.0.1 and your configured CS2 game port).
4. Choose a LAN forwarding port (for example 27017) and start the relay.
5. Allow that UDP port in Windows Firewall on the Private network profile.

CLI alternative:

    FragCity-Offline-Launcher.exe server --internal-ip 127.0.0.1 --internal 27015 --ip 0.0.0.0 --port 27017

### Client PC (each LAN player)

1. Connect both PCs to the same LAN.
2. Open FragCity and choose Add server. Enter the host's private LAN IP and the dedicated server's query port (commonly 27015).
3. Set the forwarding port to the host's relay port (for example 27017) and a free local client port (default 27016).
4. Select the server and choose Connect with Steam.

The launcher starts a local UDP tunnel and asks Steam to connect CS2 to 127.0.0.1:<local-port>. The UDP data path does not require a central service, cloud relay or Internet connection. Steam/CS2 may still have their own login or offline-mode requirements.

CLI alternative:

    # On the client PC
    FragCity-Offline-Launcher.exe client --local 27016 --port 27017 --target 192.168.1.50

    # On the host PC
    FragCity-Offline-Launcher.exe server --internal-ip 127.0.0.1 --internal 27015 --ip 0.0.0.0 --port 27017

## Local data

On first launch, default acf.json, announcement.txt and workshop_maps.json files are copied from the embedded defaults beside the executable. Writable configuration and lists are also stored beside it: settings.json, servers.json, savedservers.json, workshop_maps.json, acf.json, announcement.txt and UpdatePackage/ (created by the manual updater).

Back these files up before replacing the executable. Existing C# JSON lists and settings can be reused.

## Build

Requires Go 1.23 or later:

    gofmt -w .
    go test ./...
    $env:CGO_ENABLED = "0"
    $env:GOOS = "windows"
    $env:GOARCH = "amd64"
    go build -trimpath -ldflags="-s -w" -o dist/FragCity-Offline-Launcher.exe .

GitHub Actions runs the tests and uploads a single-file Windows x64 executable.

## Networking and validation notes

- The relay forwards UDP datagrams, not arbitrary TCP traffic.
- Host mode accepts private, loopback and link-local source addresses only. Do not expose the LAN relay with a public router port-forward.
- Ensure ports are free and allow the forwarding UDP port in Windows Firewall on the Private profile.
- The legacy proxy.exe source was not included in the repository. The Go edition implements its own UDP forwarding path; run the Go edition at both ends rather than mixing it with the old binary.
- Automated tests cover UDP request/reply forwarding, JSON compatibility and ZIP path-traversal protection. A real multi-PC CS2 dedicated-server session is still required for end-to-end LAN validation.
