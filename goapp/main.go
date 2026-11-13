package main

import (
	"context"
	"crypto/rand"
	"embed"
	"encoding/hex"
	"flag"
	"fmt"
	"io/fs"
	"log"
	"net"
	"net/http"
	"os"
	"os/exec"
	"os/signal"
	"path/filepath"
	"runtime"
	"syscall"
	"time"

	"fragcity/launcher"
	"fragcity/proxy"
)

//go:embed web/*
var embeddedWeb embed.FS

func main() {
	if len(os.Args) > 1 {
		switch os.Args[1] {
		case "client":
			runClient(os.Args[2:])
			return
		case "server":
			runServer(os.Args[2:])
			return
		case "version", "--version", "-version":
			fmt.Println("FragCity Offline Launcher Go 0.1.0")
			return
		}
	}
	runDesktop()
}

func runClient(args []string) {
	flags := flag.NewFlagSet("client", flag.ExitOnError)
	localPort := flags.Int("local", 27016, "local UDP port for the game client")
	remotePort := flags.Int("port", 27017, "remote UDP forwarding port")
	target := flags.String("target", "", "LAN IP address of the host running server mode")
	_ = flags.Parse(args)

	if *target == "" {
		log.Fatal("client mode requires --target <LAN-IP>")
	}
	client, err := proxy.NewClientProxy(*localPort, *target, *remotePort)
	if err != nil {
		log.Fatal(err)
	}
	if err := client.Start(); err != nil {
		log.Fatal(err)
	}
	log.Printf("UDP client relay: 127.0.0.1:%d -> %s:%d", *localPort, *target, *remotePort)
	waitForShutdown(client.Stop)
}

func runServer(args []string) {
	flags := flag.NewFlagSet("server", flag.ExitOnError)
	internalIP := flags.String("internal-ip", "127.0.0.1", "IP address of the local game server")
	internalPort := flags.Int("internal", 27015, "UDP port of the local game server")
	bindIP := flags.String("ip", "0.0.0.0", "local interface to bind the LAN relay to")
	forwardPort := flags.Int("port", 27017, "UDP port clients connect to")
	_ = flags.Parse(args)

	server, err := proxy.NewServerProxy(*bindIP, *forwardPort, *internalIP, *internalPort)
	if err != nil {
		log.Fatal(err)
	}
	if err := server.Start(); err != nil {
		log.Fatal(err)
	}
	log.Printf("LAN UDP relay: %s:%d -> %s:%d (private/LAN clients only)", *bindIP, *forwardPort, *internalIP, *internalPort)
	waitForShutdown(server.Stop)
}

func waitForShutdown(stop func()) {
	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer cancel()
	<-ctx.Done()
	stop()
}

func runDesktop() {
	executable, err := os.Executable()
	if err != nil {
		log.Fatal(err)
	}
	dataDir := filepath.Dir(executable)
	if custom := os.Getenv("FRAGCITY_DATA_DIR"); custom != "" {
		dataDir = custom
	}

	app, err := launcher.NewApp(dataDir)
	if err != nil {
		log.Fatal(err)
	}
	defer app.Close()

	tokenBytes := make([]byte, 32)
	if _, err := rand.Read(tokenBytes); err != nil {
		log.Fatal(err)
	}
	token := hex.EncodeToString(tokenBytes)
	site, err := fs.Sub(embeddedWeb, "web")
	if err != nil {
		log.Fatal(err)
	}

	listener, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		log.Fatal(err)
	}
	httpServer := &http.Server{
		Handler:           launcher.NewHTTPHandler(app, site, token),
		ReadHeaderTimeout: 5 * time.Second,
		IdleTimeout:       60 * time.Second,
	}
	address := "http://" + listener.Addr().String() + "/"
	go func() {
		if serveErr := httpServer.Serve(listener); serveErr != nil && serveErr != http.ErrServerClosed {
			log.Printf("Local interface stopped: %v", serveErr)
		}
	}()

	if err := openBrowser(address); err != nil {
		log.Printf("Open this address in your browser: %s (%v)", address, err)
	}
	log.Printf("FragCity Go launcher is running at %s", address)

	ctx, cancel := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer cancel()
	<-ctx.Done()
	shutdownCtx, shutdownCancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer shutdownCancel()
	_ = httpServer.Shutdown(shutdownCtx)
}

func openBrowser(address string) error {
	switch runtime.GOOS {
	case "windows":
		return exec.Command("rundll32.exe", "url.dll,FileProtocolHandler", address).Start()
	case "darwin":
		return exec.Command("open", address).Start()
	default:
		return exec.Command("xdg-open", address).Start()
	}
}
