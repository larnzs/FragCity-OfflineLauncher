package proxy

import (
	"net"
	"testing"
	"time"
)

func reserveUDPPort(t *testing.T, ip string) int {
	t.Helper()
	conn, err := net.ListenUDP("udp", &net.UDPAddr{IP: net.ParseIP(ip), Port: 0})
	if err != nil {
		t.Fatal(err)
	}
	port := conn.LocalAddr().(*net.UDPAddr).Port
	_ = conn.Close()
	return port
}

func TestClientServerUDPForwardingRoundTrip(t *testing.T) {
	internal, err := net.ListenUDP("udp", &net.UDPAddr{IP: net.ParseIP("127.0.0.1"), Port: 0})
	if err != nil {
		t.Fatal(err)
	}
	defer internal.Close()
	internalPort := internal.LocalAddr().(*net.UDPAddr).Port

	go func() {
		buf := make([]byte, 2048)
		for {
			n, peer, readErr := internal.ReadFromUDP(buf)
			if readErr != nil {
				return
			}
			_, _ = internal.WriteToUDP(append([]byte("echo:"), buf[:n]...), peer)
		}
	}()

	forwardPort := reserveUDPPort(t, "127.0.0.1")
	server, err := NewServerProxy("127.0.0.1", forwardPort, "127.0.0.1", internalPort)
	if err != nil {
		t.Fatal(err)
	}
	if err := server.Start(); err != nil {
		t.Fatal(err)
	}
	defer server.Stop()

	client, err := NewClientProxy(0, "127.0.0.1", forwardPort)
	if err != nil {
		t.Fatal(err)
	}
	if err := client.Start(); err != nil {
		t.Fatal(err)
	}
	defer client.Stop()

	game, err := net.DialUDP("udp", nil, &net.UDPAddr{IP: net.ParseIP("127.0.0.1"), Port: client.LocalPort()})
	if err != nil {
		t.Fatal(err)
	}
	defer game.Close()
	_ = game.SetDeadline(time.Now().Add(3 * time.Second))

	if _, err := game.Write([]byte("fragcity")); err != nil {
		t.Fatal(err)
	}
	buf := make([]byte, 128)
	n, err := game.Read(buf)
	if err != nil {
		t.Fatalf("did not receive relayed UDP reply: %v", err)
	}
	if got, want := string(buf[:n]), "echo:fragcity"; got != want {
		t.Fatalf("reply mismatch: got %q, want %q", got, want)
	}
}

func TestLANPeerFilter(t *testing.T) {
	cases := []struct {
		ip   string
		want bool
	}{
		{"192.168.1.5", true},
		{"10.1.2.3", true},
		{"172.16.0.9", true},
		{"127.0.0.1", true},
		{"169.254.2.3", true},
		{"8.8.8.8", false},
	}
	for _, tc := range cases {
		if got := isLANPeer(net.ParseIP(tc.ip)); got != tc.want {
			t.Errorf("isLANPeer(%s) = %v; want %v", tc.ip, got, tc.want)
		}
	}
}
