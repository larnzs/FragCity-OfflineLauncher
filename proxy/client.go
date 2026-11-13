package proxy

import (
	"errors"
	"fmt"
	"net"
	"strconv"
	"sync"
	"sync/atomic"
)

// ClientProxy forwards UDP datagrams from a local game port to a remote LAN relay.
// Replies are sent back to the most recent local game endpoint.
type ClientProxy struct {
	localConn  *net.UDPConn
	remoteConn *net.UDPConn
	done       chan struct{}
	stopOnce   sync.Once
	wg         sync.WaitGroup

	peerMu sync.RWMutex
	peer   *net.UDPAddr

	running  atomic.Bool
	bytesIn  atomic.Uint64
	bytesOut atomic.Uint64
}

func NewClientProxy(localPort int, targetIP string, remotePort int) (*ClientProxy, error) {
	if localPort < 0 || localPort > 65535 || remotePort < 1 || remotePort > 65535 {
		return nil, errors.New("UDP ports must be between 1 and 65535 (local may be 0 for an ephemeral port)")
	}
	ip := net.ParseIP(targetIP)
	if ip == nil {
		return nil, fmt.Errorf("target must be an IP address: %q", targetIP)
	}
	target := &net.UDPAddr{IP: ip, Port: remotePort}
	remote, err := net.DialUDP("udp", nil, target)
	if err != nil {
		return nil, fmt.Errorf("open remote UDP socket: %w", err)
	}
	local, err := net.ListenUDP("udp", &net.UDPAddr{IP: net.ParseIP("127.0.0.1"), Port: localPort})
	if err != nil {
		_ = remote.Close()
		return nil, fmt.Errorf("listen on local UDP port %d: %w", localPort, err)
	}
	return &ClientProxy{localConn: local, remoteConn: remote, done: make(chan struct{})}, nil
}

func (c *ClientProxy) Start() error {
	if c.running.Swap(true) {
		return errors.New("client proxy is already running")
	}
	c.wg.Add(2)
	go c.forwardLocalToRemote()
	go c.forwardRemoteToLocal()
	go func() {
		c.wg.Wait()
		close(c.done)
	}()
	return nil
}

func (c *ClientProxy) forwardLocalToRemote() {
	defer c.wg.Done()
	defer c.closeSockets()
	buffer := make([]byte, 65535)
	for {
		n, peer, err := c.localConn.ReadFromUDP(buffer)
		if err != nil {
			return
		}
		c.peerMu.Lock()
		peerCopy := *peer
		c.peer = &peerCopy
		c.peerMu.Unlock()

		written, err := c.remoteConn.Write(buffer[:n])
		if err != nil {
			return
		}
		c.bytesIn.Add(uint64(n))
		c.bytesOut.Add(uint64(written))
	}
}

func (c *ClientProxy) forwardRemoteToLocal() {
	defer c.wg.Done()
	defer c.closeSockets()
	buffer := make([]byte, 65535)
	for {
		n, err := c.remoteConn.Read(buffer)
		if err != nil {
			return
		}
		c.peerMu.RLock()
		peer := c.peer
		c.peerMu.RUnlock()
		if peer == nil {
			continue
		}
		written, err := c.localConn.WriteToUDP(buffer[:n], peer)
		if err != nil {
			return
		}
		c.bytesIn.Add(uint64(n))
		c.bytesOut.Add(uint64(written))
	}
}

func (c *ClientProxy) closeSockets() {
	c.stopOnce.Do(func() {
		c.running.Store(false)
		_ = c.localConn.Close()
		_ = c.remoteConn.Close()
	})
}

func (c *ClientProxy) Stop() {
	c.closeSockets()
	<-c.done
}

func (c *ClientProxy) Done() <-chan struct{} { return c.done }
func (c *ClientProxy) IsRunning() bool      { return c.running.Load() }
func (c *ClientProxy) LocalAddress() string { return c.localConn.LocalAddr().String() }
func (c *ClientProxy) LocalPort() int {
	_, port, _ := net.SplitHostPort(c.LocalAddress())
	n, _ := strconv.Atoi(port)
	return n
}
func (c *ClientProxy) Counters() (received, sent uint64) {
	return c.bytesIn.Load(), c.bytesOut.Load()
}
