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

    /// <summary>
    /// Writable per-user data storage. The first run imports existing portable files
    /// from the application folder, then keeps mutable data outside Program Files.
    /// </summary>
    public static class AppDataPaths
    {
        private static readonly object SyncRoot = new object();
        private static bool initialized;

        private static string AppDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FragCity",
                "OfflineLauncher");

        public static string RootDirectory
        {
            get
            {
                EnsureInitialized();
                return AppDirectory;
            }
        }

        public static string GetFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
                throw new ArgumentException("Expected a file name, not a path.", nameof(fileName));

            EnsureInitialized();
            return Path.Combine(AppDirectory, fileName);
        }

        private static void EnsureInitialized()
        {
            lock (SyncRoot)
            {
                if (initialized) return;

                Directory.CreateDirectory(AppDirectory);

                // Migrate existing portable installations exactly once per file.
                // Never overwrite an existing profile or its offline server cache.
                string[] fileNames =
                {
                    "settings.json",
                    "servers.json",
                    "savedservers.json",
                    "workshop_maps.json",
                    "announcement.txt"
                };

                string sourceDirectory = AppDomain.CurrentDomain.BaseDirectory;
                foreach (string fileName in fileNames)
                {
                    string source = Path.Combine(sourceDirectory, fileName);
                    string destination = Path.Combine(AppDirectory, fileName);
                    if (File.Exists(destination) || !File.Exists(source)) continue;

                    try
                    {
                        File.Copy(source, destination, overwrite: false);
                    }
                    catch (IOException)
                    {
                        // Keep startup resilient; the app can use defaults if a
                        // legacy file is locked or otherwise unavailable.
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // The destination is still usable even if legacy migration
                        // was not permitted.
                    }
                }

                initialized = true;
            }
        }
    }

    public static class SettingsManager
    {
        private static string SettingsPath => AppDataPaths.GetFilePath("settings.json");

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