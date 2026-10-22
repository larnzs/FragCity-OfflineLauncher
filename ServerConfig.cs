namespace CSserver_browser
{
    // کلاس ServerConfig (احتمالاً در فایلی دیگر است، باید افزوده شود)
    public class ServerConfig
    {
        public string TargetIp { get; set; } = "";
        public int GameQueryPort { get; set; }
        public int ForwardingPort { get; set; }   // اضافه شود
        public int ListeningPort { get; set; }    // اضافه شود
    }
    public class ServerListWrapper
    {
        public List<ServerConfig> Servers { get; set; } = new List<ServerConfig>();
    }
}