using System;
using System.IO;
using System.Text.Json;

namespace CSserver_browser
{
    public class AppSettings
    {
        public int GameQueryPort { get; set; } = 27015;
        public int LocalPort { get; set; } = 27016;
        public int ProxyPort { get; set; } = 27017;
        public bool AutoRefresh { get; set; } = false;
        public int RefreshIntervalSeconds { get; set; } = 15;
        public string WorkshopPath { get; set; } = "";
        public string CurrentVersion { get; set; } = "1.6.0";
        public string VersionCheckUrl { get; set; } = "";
        public string ServerUpdateUrl { get; set; } = "";
        public string WorkshopMapUpdateUrl { get; set; } = "";
        public string AnnouncementUrl { get; set; } = "";   // لینک فایل متنی اعلان
        public string UpdateCheckUrl { get; set; } = "";     // لینک latest_version.json (برای آپدیت برنامه)
    }

    // ServerConfig و ServerListWrapper را از اینجا حذف کنید

    public static class SettingsManager
    {
        private static string SettingsPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}