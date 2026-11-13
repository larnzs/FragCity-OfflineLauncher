package launcher

import "time"

// Keep these names compatible with the PascalCase JSON written by the C# app.
type ServerConnectionData struct {
	Ip             string
	Port           int
	ForwardingPort int
	ListeningPort  int
	Name           string
	Map            string
	Players        int
	MaxPlayers     int
	Ping           int
	HasPassword    bool
	IsSecure       bool
	TickRate       int
	Tags           string
	GameMode       string
	ConnectedAt    time.Time
}

type ServerInternetContainer struct {
	Internet []ServerConnectionData
}

type SavedServersContainer struct {
	Personal  []ServerConnectionData
	Favorites []ServerConnectionData
	History   []ServerConnectionData
}

type RemoteServerConfig struct {
	TargetIp       string
	GameQueryPort  int
	ForwardingPort int
	ListeningPort  int
}

type RemoteServerContainer struct {
	Internet []RemoteServerConfig
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
	GameMode    string
	Tags        string
	TickRate    int
}

type WorkshopMapItem struct {
	WorkshopId int64
	Size        int64
	TimeUpdated int64
	Manifest    string
	MapName     string
	DownloadUrl string
	LocalPath   string
	IsInstalled bool
}

type WorkshopDownloadState struct {
	Active      bool
	Completed   bool
	WorkshopID  int64
	Percent     int
	Received    int64
	Total       int64
	Message     string
}

type UpdateInfo struct {
	Version    string
	DownloadUrl string
}

type AppState struct {
	Internet           []ServerConnectionData
	Personal           []ServerConnectionData
	Favorites          []ServerConnectionData
	History            []ServerConnectionData
	Settings           AppSettings
	ClientProxyRunning bool
	ServerProxyRunning bool
	ClientProxyAddress string
	ServerProxyAddress string
	ClientBytesIn      uint64
	ClientBytesOut     uint64
	ServerBytesIn      uint64
	ServerBytesOut     uint64
	BlockedPublicPeers uint64
	Message            string
	Announcement       string
	SteamID            string
	WorkshopMaps       []WorkshopMapItem
	WorkshopPath       string
	WorkshopDownload   WorkshopDownloadState
	ACFAvailable       bool
}
