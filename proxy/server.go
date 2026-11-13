package proxy

import (
	"errors"
	"fmt"
	"net"
	"sync"
	"sync/atomic"
	"time"
)

const sessionIdleTimeout = 90 * time.Second

type clientSession struct {
	key  string
	peer *net.UDPAddr
	conn *net.UDPConn
}

type ServerProxy struct {
	listener   *net.UDPConn
	listenAddr *net.UDPAddr
	target     *net.UDPAddr
	done       chan struct{}
	stopOnce   sync.Once
	wg         sync.WaitGroup

	sessionsMu sync.Mutex
	sessions   map[string]*clientSession

	running      atomic.Bool
	bytesIn      atomic.Uint64
	bytesOut     atomic.Uint64
	blockedPeers atomic.Uint64
}

// ServerProxy accepts datagrams from private/LAN addresses and relays them to
// the local game server. Each remote endpoint receives an independent UDP socket
// so the game server can reply to the correct client session.
func NewServerProxy(bindIP string, forwardPort int, internalIP string, internalPort int) (*ServerProxy, error) {
	if forwardPort < 1 || forwardPort > 65535 || internalPort < 1 || internalPort > 65535 {
		return nil, errors.New("UDP ports must be between 1 and 65535")
	}
	bind := net.ParseIP(bindIP)
	if bindIP == "" {
		bind = net.IPv4zero
	}
	if bind == nil {
		return nil, fmt.Errorf("bind address must be an IP address: %q", bindIP)
	}
	target := net.ParseIP(internalIP)
	if target == nil {
		return nil, fmt.Errorf("internal address must be an IP address: %q", internalIP)
	}
	return &ServerProxy{
		listenAddr: &net.UDPAddr{IP: bind, Port: forwardPort},
		target:     &net.UDPAddr{IP: target, Port: internalPort},
		done:       make(chan struct{}),
		sessions:   make(map[string]*clientSession),
	}, nil
}

func (s *ServerProxy) Start() error {
	if s.running.Swap(true) {
		return errors.New("server proxy is already running")
	}
	listener, err := net.ListenUDP("udp", s.listenAddr)
	if err != nil {
		s.running.Store(false)
		return fmt.Errorf("listen on %s: %w", s.listenAddr, err)
	}
	s.listener = listener
	s.wg.Add(1)
	go s.readLoop()
	go func() {
		s.wg.Wait()
		close(s.done)
	}()
	return nil
}

func (s *ServerProxy) readLoop() {
	defer s.wg.Done()
	defer s.closeSockets()
	buffer := make([]byte, 65535)
	for {
		n, peer, err := s.listener.ReadFromUDP(buffer)
		if err != nil {
			return
		}
		if !isLANPeer(peer.IP) {
			s.blockedPeers.Add(1)
			continue
		}
		session, err := s.getOrCreateSession(peer)
		if err != nil {
			continue
		}
		_, _ = session.conn.Write(buffer[:n])
		_ = session.conn.SetReadDeadline(time.Now().Add(sessionIdleTimeout))
		s.bytesIn.Add(uint64(n))
	}
}

func (s *ServerProxy) getOrCreateSession(peer *net.UDPAddr) (*clientSession, error) {
	key := peer.String()
	s.sessionsMu.Lock()
	defer s.sessionsMu.Unlock()
	if existing := s.sessions[key]; existing != nil {
		return existing, nil
	}
	conn, err := net.DialUDP("udp", nil, s.target)
	if err != nil {
		return nil, err
	}
	peerCopy := *peer
	session := &clientSession{key: key, peer: &peerCopy, conn: conn}
	s.sessions[key] = session
	s.wg.Add(1)
	go s.readReplies(session)
	return session, nil
}

func (s *ServerProxy) readReplies(session *clientSession) {
	defer s.wg.Done()
	defer func() {
		s.sessionsMu.Lock()
		if s.sessions[session.key] == session {
			delete(s.sessions, session.key)
		}
		s.sessionsMu.Unlock()
		_ = session.conn.Close()
	}()

	buffer := make([]byte, 65535)
	for {
		_ = session.conn.SetReadDeadline(time.Now().Add(sessionIdleTimeout))
		n, err := session.conn.Read(buffer)
		if err != nil {
			return
		}
		if _, err := s.listener.WriteToUDP(buffer[:n], session.peer); err != nil {
			return
		}
		s.bytesOut.Add(uint64(n))
	}
}

func (s *ServerProxy) closeSockets() {
	s.stopOnce.Do(func() {
		s.running.Store(false)
		if s.listener != nil {
			_ = s.listener.Close()
		}
		s.sessionsMu.Lock()
		for _, session := range s.sessions {
			_ = session.conn.Close()
		}
		s.sessionsMu.Unlock()
	})
}

func (s *ServerProxy) Stop() {
	s.closeSockets()
	<-s.done
}

func (s *ServerProxy) Done() <-chan struct{} { return s.done }
func (s *ServerProxy) IsRunning() bool      { return s.running.Load() }
func (s *ServerProxy) LocalAddress() string {
	if s.listener == nil {
		return ""
	}
	return s.listener.LocalAddr().String()
}
func (s *ServerProxy) Counters() (received, sent, blocked uint64) {
	return s.bytesIn.Load(), s.bytesOut.Load(), s.blockedPeers.Load()
}

func isLANPeer(ip net.IP) bool {
	return ip != nil && (ip.IsPrivate() || ip.IsLoopback() || ip.IsLinkLocalUnicast())
}
