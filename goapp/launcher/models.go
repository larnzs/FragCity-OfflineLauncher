package launcher

import "time"

// ServerConnectionData intentionally keeps the original C# property names so
// existing servers.json and savedservers.json files remain readable.
type ServerConnectionData struct {
	Ip            string
	Port          int
	ForwardingPort int
	ListeningPort  int
	Name          string
	Map           string
	Players       int
	MaxPlayers    int
	Ping          int
	HasPassword   bool
	IsSecure      bool
	TickRate      int
	Tags          string
	GameMode      string
	ConnectedAt   time.Time
}

type ServerInternetContainer struct {
	Internet []ServerConnectionData
}

type SavedServersContainer struct {
	Personal  []ServerConnectionData
	Favorites []ServerConnectionData
	History   []ServerConnectionData
}

type AppSettings struct {
	GameQueryPort          int
	LocalPort              int
	ProxyPort              int
	AutoRefresh            bool
	RefreshIntervalSeconds int
	WorkshopPath           string
	CurrentVersion         string
	VersionCheckUrl        string
	ServerUpdateUrl        string
	WorkshopMapUpdateUrl   string
	AnnouncementUrl        string
	UpdateCheckUrl         string
}

type PlayerInfo struct {
	Index    int
	Name     string
	Score    int
	Duration float32
}

type ServerInfo struct {
	Name        string
	Map         string
	Players     int
	MaxPlayers  int
	HasPassword bool
	IsSecure    bool
	Ping        int
}

type AppState struct {
	Internet            []ServerConnectionData
	Personal            []ServerConnectionData
	Favorites           []ServerConnectionData
	History             []ServerConnectionData
	Settings            AppSettings
	ClientProxyRunning  bool
	ServerProxyRunning  bool
	ClientProxyAddress  string
	ServerProxyAddress  string
	ClientBytesIn       uint64
	ClientBytesOut      uint64
	ServerBytesIn       uint64
	ServerBytesOut      uint64
	BlockedPublicPeers  uint64
	Message             string
}
