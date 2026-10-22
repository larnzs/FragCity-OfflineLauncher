#nullable disable

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSserver_browser
{
    public partial class Form1 : Form
    {
        // ---------- کنترل‌های نوار وضعیت ----------
        private ToolStripStatusLabel lblServerCount;
        private ToolStripStatusLabel lblSelectedIp;
        private ToolStripStatusLabel lblProxyStatus;
        private ToolStripStatusLabel lblClock;
        private ToolStripStatusLabel lblMessage;
        private ToolStripButton btnStartProxy;
        private ToolStripButton btnStopProxy;
        private Process currentProxyProcess = null;

        private System.Windows.Forms.Timer clockTimer;
        private System.Windows.Forms.Timer autoRefreshTimer;
        private System.Windows.Forms.Timer searchDebounceTimer;
        private bool isApplyingFilter = false;

        private ToolStripProgressBar toolStripProgressBar;

        private List<ServerConnectionData> internetServers = new List<ServerConnectionData>();
        private List<ServerConnectionData> personalServers = new List<ServerConnectionData>();
        private List<ServerConnectionData> favoriteServers = new List<ServerConnectionData>();
        private List<ServerConnectionData> historyServers = new List<ServerConnectionData>();

        private MenuStrip mainMenu;
        private ToolStripMenuItem fileMenu, viewMenu, helpMenu;
        private ToolStripMenuItem exitMenuItem, refreshMenuItem, aboutMenuItem;
        private Panel topMessagePanel;
        private Label topMessageLabel;
        private System.Windows.Forms.Timer announcementTimer;

        private AppSettings appSettings;
        private Dictionary<string, DateTime> lastRefreshTimes = new Dictionary<string, DateTime>();
        private int sortColumn = -1;
        private bool sortAscending = true;
        private ToolStripMenuItem settingsMenu;

        private List<WorkshopMapItem> workshopMapsList = new List<WorkshopMapItem>();

        private bool _ignoreSearchTextChanged = false;

        // کنسول کرکره‌ای
        private Panel consolePanel;
        private Button btnToggleConsole;
        private bool consoleExpanded = false;
        private const int CONSOLE_HEIGHT = 60;
        private int formOriginalHeight;

        // ======= بخش قابلیت‌های جدید =======
        private CancellationTokenSource _refreshCts;

        // SteamID در منوی بالا
        private ToolStripMenuItem steamIdMenuItem;

        public Form1()
        {
            InitializeComponent();

            this.Size = new Size(1100, 666);
            this.MinimumSize = new Size(1100, 666);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.Padding = new Padding(0);
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            SetupMenuStrip();
            SetupAutoRefresh();
            LoadSettings();
            SetupTopMessageBar();
            SetupStatusStrip();
            ConfigureAllListViews();
            SetupConsoleBox();

            formOriginalHeight = this.Height;

            LoadAllServers();
            AttachCommonEvents();
            ApplyVisualStyle();
            UpdateServerCount();
            StartClock();
            SetupSearchDebouncer();

            if (tabControl1 != null)
                tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;

            this.FormClosing += Form1_FormClosing;

            LoadWorkshopMapsForCheck();
            CopyPreparedAcf();

            RetrieveSteamInfo();

            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                this.BeginInvoke(new Action(async () => await RefreshCurrentListView(false)));
            });
        }

        public void ReloadSettings()
        {
            appSettings = SettingsManager.Load();
        }

        // ========== تنظیمات ==========
        private void LoadSettings()
        {
            appSettings = SettingsManager.Load();

            numGameQueryPort.Value = appSettings.GameQueryPort;
            numLocalPort.Value = appSettings.LocalPort;
            numProxyPort.Value = appSettings.ProxyPort;

            autoRefreshTimer.Interval = appSettings.RefreshIntervalSeconds * 1000;
            autoRefreshTimer.Enabled = appSettings.AutoRefresh;

            var autoRefreshMenuItem = viewMenu?.DropDownItems.OfType<ToolStripMenuItem>()
                .FirstOrDefault(x => x.Text == "Auto Refresh (15s)");
            if (autoRefreshMenuItem != null)
                autoRefreshMenuItem.Checked = appSettings.AutoRefresh;
        }

        private void SaveSettings()
        {
            appSettings.GameQueryPort = (int)numGameQueryPort.Value;
            appSettings.LocalPort = (int)numLocalPort.Value;
            appSettings.ProxyPort = (int)numProxyPort.Value;
            appSettings.AutoRefresh = autoRefreshTimer.Enabled;
            appSettings.RefreshIntervalSeconds = autoRefreshTimer.Interval / 1000;
            SettingsManager.Save(appSettings);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            StopProxyProcess();
            SaveSettings();
        }

        // ========== WORKSHOP ==========
        private void LoadWorkshopMapsForCheck()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workshop_maps.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string json = File.ReadAllText(jsonPath);
                    workshopMapsList = JsonSerializer.Deserialize<List<WorkshopMapItem>>(json)
                                    ?? new List<WorkshopMapItem>();
                }
                catch { workshopMapsList = new List<WorkshopMapItem>(); }
            }
        }

        private void CopyPreparedAcf()
        {
            string wsPath = appSettings.WorkshopPath;
            if (string.IsNullOrEmpty(wsPath) || !Directory.Exists(wsPath)) return;
            string src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appworkshop_730.acf");
            if (File.Exists(src))
            {
                try { File.Copy(src, Path.Combine(wsPath, "appworkshop_730.acf"), true); } catch { }
            }
        }

        // ========== Remote data ==========
        private async Task FetchAndMergeRemoteServersAsync()
        {
            string url = appSettings.ServerUpdateUrl;
            if (string.IsNullOrEmpty(url))
            {
                SetMessage("Server update URL not configured.");
                return;
            }

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
                {
                    string json = await client.GetStringAsync(url);
                    var remoteContainer = JsonSerializer.Deserialize<RemoteServerInternetContainer>(json);
                    if (remoteContainer?.Internet == null || remoteContainer.Internet.Count == 0)
                    {
                        SetMessage("No new servers found.");
                        return;
                    }

                    int before = internetServers.Count;
                    foreach (var cfg in remoteContainer.Internet)
                    {
                        if (!internetServers.Any(s => s.Ip == cfg.TargetIp && s.Port == cfg.GameQueryPort))
                        {
                            internetServers.Add(new ServerConnectionData
                            {
                                Ip = cfg.TargetIp,
                                Port = cfg.GameQueryPort,
                                ForwardingPort = cfg.ForwardingPort,
                                ListeningPort = cfg.ListeningPort,
                                Name = "Unknown",
                                Map = "Unknown",
                                Players = 0,
                                MaxPlayers = 0,
                                Ping = -1,
                                HasPassword = false,
                                IsSecure = false,
                                TickRate = 0,
                                Tags = "Official",
                                GameMode = "",
                                ConnectedAt = DateTime.MinValue
                            });
                        }
                    }

                    int added = internetServers.Count - before;
                    SaveAllServerLists();
                    if (added > 0)
                        SetMessage($"Added {added} new server(s).");
                    else
                        SetMessage("No new servers to add.");
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                SetMessage("Remote server list returned 404; using the cached list.", Color.Orange);
            }
            catch (Exception ex)
            {
                SetMessage($"Remote server list unavailable; using cached data. {ex.Message}", Color.Orange);
            }
        }

        private async Task FetchAndMergeRemoteWorkshopMapsAsync()
        {
            string url = appSettings.WorkshopMapUpdateUrl;
            if (string.IsNullOrEmpty(url))
            {
                SetMessage("Workshop maps update URL not configured.");
                return;
            }

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    string json = await client.GetStringAsync(url);
                    var remoteMaps = JsonSerializer.Deserialize<List<WorkshopMapItem>>(json);
                    if (remoteMaps == null || remoteMaps.Count == 0)
                    {
                        SetMessage("No new workshop maps found.");
                        return;
                    }

                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "workshop_maps.json");
                    List<WorkshopMapItem> localList = new List<WorkshopMapItem>();
                    if (File.Exists(localPath))
                    {
                        string localJson = File.ReadAllText(localPath);
                        localList = JsonSerializer.Deserialize<List<WorkshopMapItem>>(localJson)
                                    ?? new List<WorkshopMapItem>();
                    }

                    int before = localList.Count;
                    foreach (var rm in remoteMaps)
                        if (!localList.Any(m => m.WorkshopId == rm.WorkshopId))
                            localList.Add(rm);

                    int added = localList.Count - before;
                    if (added > 0)
                    {
                        string newJson = JsonSerializer.Serialize(localList, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(localPath, newJson);
                        SetMessage($"Added {added} new workshop map(s).");
                    }
                    else SetMessage("No new workshop maps to add.");
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                SetMessage("Remote workshop list returned 404; keeping the local list.", Color.Orange);
            }
            catch (Exception ex)
            {
                SetMessage($"Remote workshop list unavailable; keeping local data. {ex.Message}", Color.Orange);
            }
        }

        private async Task FetchAndMergeRemoteAnnouncementAsync()
        {
            string url = appSettings.AnnouncementUrl;
            if (string.IsNullOrEmpty(url))
                return;

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
                {
                    string text = await client.GetStringAsync(url);
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "announcement.txt");
                    File.WriteAllText(localPath, text);
                    LoadAnnouncement();
                }
            }
            catch { }
        }

        // ========== File management ==========
        private string ServersFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servers.json");
        private string SavedServersFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "savedservers.json");

        private void LoadAllServers()
        {
            if (File.Exists(ServersFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ServersFilePath);
                    var internetContainer = JsonSerializer.Deserialize<ServerInternetContainer>(json);
                    internetServers = internetContainer?.Internet ?? new List<ServerConnectionData>();
                }
                catch { internetServers = new List<ServerConnectionData>(); }
            }
            else internetServers = new List<ServerConnectionData>();

            if (File.Exists(SavedServersFilePath))
            {
                try
                {
                    string json = File.ReadAllText(SavedServersFilePath);
                    var savedContainer = JsonSerializer.Deserialize<SavedServersContainer>(json);
                    personalServers = savedContainer?.Personal ?? new List<ServerConnectionData>();
                    favoriteServers = savedContainer?.Favorites ?? new List<ServerConnectionData>();
                    historyServers = savedContainer?.History ?? new List<ServerConnectionData>();
                }
                catch
                {
                    personalServers = new List<ServerConnectionData>();
                    favoriteServers = new List<ServerConnectionData>();
                    historyServers = new List<ServerConnectionData>();
                }
            }
            else
            {
                personalServers = new List<ServerConnectionData>();
                favoriteServers = new List<ServerConnectionData>();
                historyServers = new List<ServerConnectionData>();
            }

            RefreshListView(listViewServers, internetServers);
            RefreshListView(listViewPersonal, personalServers);
            RefreshListView(listViewFavorites, favoriteServers);
            RefreshListView(listViewHistory, historyServers);
        }

        private void SaveAllServerLists()
        {
            var internetContainer = new ServerInternetContainer { Internet = internetServers };
            string internetJson = JsonSerializer.Serialize(internetContainer,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ServersFilePath, internetJson);

            var savedContainer = new SavedServersContainer
            {
                Personal = personalServers,
                Favorites = favoriteServers,
                History = historyServers
            };
            string savedJson = JsonSerializer.Serialize(savedContainer,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SavedServersFilePath, savedJson);
        }

        // ========== Search ==========
        private void SetupSearchDebouncer()
        {
            searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            searchDebounceTimer.Tick += (s, e) =>
            {
                searchDebounceTimer.Stop();
                ApplyFilter();
            };
        }

        private void ApplyFilter()
        {
            if (isApplyingFilter) return;
            isApplyingFilter = true;
            try { FilterServers(); }
            finally { isApplyingFilter = false; }
        }

        private void FilterServers()
        {
            ListView currentLv = GetCurrentListView();
            if (currentLv == null) return;
            var fullList = GetCurrentDataList();
            if (fullList == null) return;

            var snapshot = fullList.ToList();
            string filter = svSearchBox.Text.Trim().ToLowerInvariant();
            var filtered = new List<ServerConnectionData>();
            foreach (var server in snapshot)
            {
                // Keep cached server entries visible even when the latest query failed.
                if (string.IsNullOrEmpty(filter) ||
                    (server.Name?.ToLowerInvariant().Contains(filter) ?? false) ||
                    ($"{server.Ip}:{server.Port}".ToLowerInvariant().Contains(filter)) ||
                    (server.Map?.ToLowerInvariant().Contains(filter) ?? false) ||
                    (server.Tags?.ToLowerInvariant().Contains(filter) ?? false))
                {
                    filtered.Add(server);
                }
            }
            if (currentLv.InvokeRequired)
                currentLv.Invoke(new Action(() => RefreshListView(currentLv, filtered)));
            else
                RefreshListView(currentLv, filtered);
        }

        // ========== Timers ==========
        private void SetupAutoRefresh()
        {
            autoRefreshTimer = new System.Windows.Forms.Timer { Interval = 15000 };
            autoRefreshTimer.Tick += async (s, e) => await RefreshCurrentListView(false);
            autoRefreshTimer.Enabled = false;
        }

        // ========== Menu Strip ==========
        private void SetupMenuStrip()
        {
            mainMenu = new MenuStrip();
            mainMenu.Dock = DockStyle.Top;
            mainMenu.GripStyle = ToolStripGripStyle.Hidden;

            fileMenu = new ToolStripMenuItem("File");
            var exportMenuItem = new ToolStripMenuItem("Export Servers...");
            exportMenuItem.Click += ExportServers_Click;
            var importMenuItem = new ToolStripMenuItem("Import Servers...");
            importMenuItem.Click += ImportServers_Click;
            exitMenuItem = new ToolStripMenuItem("Exit");
            exitMenuItem.Click += (s, e) => { SaveSettings(); Application.Exit(); };
            fileMenu.DropDownItems.Add(exportMenuItem);
            fileMenu.DropDownItems.Add(importMenuItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitMenuItem);

            viewMenu = new ToolStripMenuItem("View");
            refreshMenuItem = new ToolStripMenuItem("Refresh Server List");
            refreshMenuItem.Click += async (s, e) => await RefreshCurrentListView(true);
            viewMenu.DropDownItems.Add(refreshMenuItem);

            ToolStripMenuItem playersMenuItem = new ToolStripMenuItem("Players");
            playersMenuItem.Click += (s, e) => ShowTotalPlayers();
            viewMenu.DropDownItems.Add(playersMenuItem);

            ToolStripMenuItem autoRefreshMenuItem = new ToolStripMenuItem("Auto Refresh (15s)");
            autoRefreshMenuItem.CheckOnClick = true;
            autoRefreshMenuItem.Checked = false;
            autoRefreshMenuItem.Click += (s, e) =>
            {
                autoRefreshTimer.Enabled = autoRefreshMenuItem.Checked;
                autoRefreshTimer.Interval = appSettings.RefreshIntervalSeconds * 1000;
                SetMessage(autoRefreshMenuItem.Checked ? "Auto refresh enabled" : "Auto refresh disabled");
            };
            viewMenu.DropDownItems.Add(autoRefreshMenuItem);

            ToolStripMenuItem reloadAnnouncementMenuItem = new ToolStripMenuItem("Reload Announcement");
            reloadAnnouncementMenuItem.Click += (s, e) => LoadAnnouncement();
            viewMenu.DropDownItems.Add(reloadAnnouncementMenuItem);

            helpMenu = new ToolStripMenuItem("Help");
            aboutMenuItem = new ToolStripMenuItem("About");
            aboutMenuItem.Click += (s, e) =>
                MessageBox.Show(
                    "CS2 Server Browser\nVersion 1.7\n\n" +
                    "Servers and application created by faceit.com/larnz (labubu).\n" +
                    "Special thanks to Amir Mousavi for the proxy solution.\n\n",
                    "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
            helpMenu.DropDownItems.Add(aboutMenuItem);

            steamIdMenuItem = new ToolStripMenuItem("Steam ID: –");
            steamIdMenuItem.ForeColor = Color.DodgerBlue;
            steamIdMenuItem.Alignment = ToolStripItemAlignment.Right;
            steamIdMenuItem.Click += (s, e) =>
            {
                string id = steamIdMenuItem.Text.Replace("Steam ID: ", "").Trim();
                if (!string.IsNullOrEmpty(id) && id != "–")
                {
                    Clipboard.SetText(id);
                    SetMessage("Steam ID copied to clipboard.");
                }
            };

            settingsMenu = new ToolStripMenuItem("Settings");
            var changeWorkshopMenuItem = new ToolStripMenuItem("Change Workshop Directory...");
            changeWorkshopMenuItem.Click += ChangeWorkshopDirectory_Click;
            settingsMenu.DropDownItems.Add(changeWorkshopMenuItem);

            var websiteMenuItem = new ToolStripMenuItem("Website");
            websiteMenuItem.Click += (s, e) => { Process.Start("https://cs.fragcity.ir"); };
            var supportMenuItem = new ToolStripMenuItem("Support");
            supportMenuItem.Click += (s, e) => { Process.Start("https://rubika.ir/fragcity"); };
            var skinChangerMenuItem = new ToolStripMenuItem("Skin Changer");
            skinChangerMenuItem.Click += (s, e) => { Process.Start("https://cs.fragcity.ir/weaponpaints.php"); };

            mainMenu.Items.Add(fileMenu);
            mainMenu.Items.Add(viewMenu);
            mainMenu.Items.Add(settingsMenu);
            mainMenu.Items.Add(websiteMenuItem);
            mainMenu.Items.Add(supportMenuItem);
            mainMenu.Items.Add(skinChangerMenuItem);
            mainMenu.Items.Add(steamIdMenuItem);
            mainMenu.Items.Add(helpMenu);

            this.Controls.Add(mainMenu);
            mainMenu.BringToFront();
        }

        private void ChangeWorkshopDirectory_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select the new CS2 workshop folder (contains content/730)";
                if (!string.IsNullOrEmpty(appSettings.WorkshopPath) && Directory.Exists(appSettings.WorkshopPath))
                    dlg.SelectedPath = appSettings.WorkshopPath;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    appSettings.WorkshopPath = dlg.SelectedPath;
                    SettingsManager.Save(appSettings);

                    var wsForm = Application.OpenForms.OfType<WorkshopMapsForm>().FirstOrDefault();
                    if (wsForm != null)
                        wsForm.UpdateWorkshopPath(dlg.SelectedPath);

                    CopyPreparedAcf();
                    MessageBox.Show("Workshop folder updated successfully.", "Settings",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ExportServers_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON files (*.json)|*.json";
                sfd.FileName = "servers_export.json";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var list = GetCurrentDataList();
                    string json = JsonSerializer.Serialize(list);
                    File.WriteAllText(sfd.FileName, json);
                    SetMessage($"Exported {list.Count} servers.");
                }
            }
        }

        private void ImportServers_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON files (*.json)|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string json = File.ReadAllText(ofd.FileName);
                        var imported = JsonSerializer.Deserialize<List<ServerConnectionData>>(json);
                        if (imported != null && imported.Count > 0)
                        {
                            personalServers.AddRange(imported);
                            SaveAllServerLists();
                            RefreshListView(listViewPersonal, personalServers);
                            SetMessage($"Imported {imported.Count} servers.");
                            if (svSearchBox != null) svSearchBox.Text = "";
                            _ = RefreshCurrentListView(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Import failed: " + ex.Message);
                    }
                }
            }
        }

        private void ShowTotalPlayers()
        {
            ListView currentLv = GetCurrentListView();
            int totalPlayers = 0;
            foreach (ListViewItem item in currentLv.Items)
                if (item.Tag is ServerConnectionData server)
                    totalPlayers += server.Players;
            MessageBox.Show($"Total players online: {totalPlayers}", "Players",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ========== Top message bar ==========
        private void SetupTopMessageBar()
        {
            topMessagePanel = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(5, 0, 5, 0) };
            topMessageLabel = new Label { Text = "Ready", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            topMessagePanel.Controls.Add(topMessageLabel);
            this.Controls.Add(topMessagePanel);
            topMessagePanel.BringToFront();

            LoadAnnouncement();
            announcementTimer = new System.Windows.Forms.Timer { Interval = 120000 };
            announcementTimer.Tick += (s, e) => LoadAnnouncement();
            announcementTimer.Start();
        }

        private void LoadAnnouncement()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "announcement.txt");
            try
            {
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path).Trim();
                    topMessageLabel.Text = !string.IsNullOrEmpty(text) ? text : "Ready";
                }
                else topMessageLabel.Text = "Ready";
            }
            catch { topMessageLabel.Text = "Ready"; }
        }

        // ========== Status Strip ==========
        private void SetupStatusStrip()
        {
            statusStrip1.Items.Clear();
            statusStrip1.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;

            lblServerCount = new ToolStripStatusLabel { Text = "0 servers", Margin = new Padding(2, 0, 15, 0) };
            lblClock = new ToolStripStatusLabel { Text = DateTime.Now.ToString("HH:mm:ss"), Margin = new Padding(2, 0, 15, 0) };
            lblProxyStatus = new ToolStripStatusLabel { Text = "Proxy: Idle", Margin = new Padding(2, 0, 15, 0), ForeColor = Color.LightGray };

            btnStartProxy = new ToolStripButton
            {
                Text = "Start Proxy",
                Enabled = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Margin = new Padding(2, 0, 15, 0)
            };
            btnStartProxy.Click += BtnStartProxy_Click;

            btnStopProxy = new ToolStripButton
            {
                Text = "Stop Proxy",
                Enabled = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ForeColor = Color.Red,
                Margin = new Padding(2, 0, 15, 0)
            };
            btnStopProxy.Click += BtnStopProxy_Click;

            lblSelectedIp = new ToolStripStatusLabel { Text = "Selected: –", Margin = new Padding(2, 0, 15, 0) };

            toolStripProgressBar = new ToolStripProgressBar
            {
                Size = new Size(120, 14),
                Style = ProgressBarStyle.Continuous,
                Visible = false,
                Alignment = ToolStripItemAlignment.Right
            };

            lblMessage = new ToolStripStatusLabel
            {
                Text = "Ready",
                Margin = new Padding(4, 0, 4, 0),
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White
            };

            statusStrip1.Items.Add(lblServerCount);
            statusStrip1.Items.Add(lblClock);
            statusStrip1.Items.Add(lblProxyStatus);
            statusStrip1.Items.Add(btnStartProxy);
            statusStrip1.Items.Add(btnStopProxy);
            statusStrip1.Items.Add(lblSelectedIp);
            statusStrip1.Items.Add(toolStripProgressBar);
            statusStrip1.Items.Add(lblMessage);
        }

        private void StartClock()
        {
            clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clockTimer.Tick += (s, e) => lblClock.Text = DateTime.Now.ToString("HH:mm:ss");
            clockTimer.Start();
        }

        // ========== Console ==========
        private void SetupConsoleBox()
        {
            consolePanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            btnToggleConsole = new Button
            {
                Text = "▲",
                Dock = DockStyle.Bottom,
                Height = 22,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnToggleConsole.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            btnToggleConsole.Click += (s, e) => ToggleConsole();
            consolePanel.Controls.Add(btnToggleConsole);

            if (txtConsole != null)
            {
                this.Controls.Remove(txtConsole);
                txtConsole.Dock = DockStyle.Fill;
                txtConsole.Visible = false;
                consolePanel.Controls.Add(txtConsole);
            }

            this.Controls.Add(consolePanel);
            consolePanel.BringToFront();

            if (txtConsole != null)
            {
                txtConsole.Multiline = true;
                txtConsole.ReadOnly = true;
                txtConsole.ScrollBars = RichTextBoxScrollBars.Vertical;
                txtConsole.BackColor = Color.Black;
                txtConsole.ForeColor = Color.LimeGreen;
                txtConsole.Font = new Font("Consolas", 9f, FontStyle.Regular);
                txtConsole.WordWrap = false;
                txtConsole.DetectUrls = false;
                txtConsole.BorderStyle = BorderStyle.Fixed3D;
            }
        }

        private void ToggleConsole()
        {
            consoleExpanded = !consoleExpanded;
            if (consoleExpanded)
            {
                consolePanel.Height = btnToggleConsole.Height + CONSOLE_HEIGHT;
                txtConsole.Visible = true;
                btnToggleConsole.Text = "▼";
                this.Height = formOriginalHeight + CONSOLE_HEIGHT;
            }
            else
            {
                consolePanel.Height = btnToggleConsole.Height;
                txtConsole.Visible = false;
                btnToggleConsole.Text = "▲";
                this.Height = formOriginalHeight;
            }
        }

        // ========== Steam Info ==========
        private void RetrieveSteamInfo()
        {
            try
            {
                object steamIdObj = Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", "SteamID", null);
                string steamId = steamIdObj?.ToString();

                if (!string.IsNullOrEmpty(steamId))
                    steamIdMenuItem.Text = $"Steam ID: {steamId}";
                else
                    steamIdMenuItem.Text = "Steam ID: –";
            }
            catch
            {
                steamIdMenuItem.Text = "Steam ID: –";
            }
        }

        // ========== DNS ==========
        private async Task<IPAddress> ResolveDomainAsync(string hostname)
        {
            try
            {
                if (IPAddress.TryParse(hostname, out IPAddress ip)) return ip;
                var addresses = await Dns.GetHostAddressesAsync(hostname);
                return addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            catch { return null; }
        }

        // ========== ListViews ==========
        private void ConfigureAllListViews()
        {
            ConfigureSingleListView(listViewServers);
            ConfigureSingleListView(listViewFavorites);
            ConfigureSingleListView(listViewHistory);
            ConfigureSingleListView(listViewPersonal);

            // افزودن ستون تاریخ فقط برای history (اگر قبلاً اضافه نشده باشد)
            if (listViewHistory.Columns.Count == 9)
                listViewHistory.Columns.Add("Last Connected", 170);
        }

        private void ConfigureSingleListView(ListView lv)
        {
            if (lv == null) return;
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = false;
            lv.OwnerDraw = true;
            lv.HideSelection = false;
            lv.HeaderStyle = ColumnHeaderStyle.Clickable;
            lv.BackColor = Color.FromArgb(20, 20, 20);
            lv.BorderStyle = BorderStyle.Fixed3D;
            lv.DoubleClick += Lv_DoubleClick;
            lv.ColumnClick += Lv_ColumnClick;

            if (lv.Columns.Count == 0)
            {
                lv.Columns.Add("🛡️", 35, HorizontalAlignment.Center);    // 0
                lv.Columns.Add("🔒", 35, HorizontalAlignment.Center);    // 1
                lv.Columns.Add("Server", 200);                          // 2
                lv.Columns.Add("Address", 130);                         // 3
                lv.Columns.Add("Players", 70);                          // 4
                lv.Columns.Add("Map", 100);                             // 5
                lv.Columns.Add("Ping", 60);                             // 6
                lv.Columns.Add("Tickrate", 55, HorizontalAlignment.Center); // 7
                lv.Columns.Add("Tags", 120);                            // 8
            }

            lv.DrawColumnHeader += (s, e) =>
            {
                using var brush = new SolidBrush(Color.FromArgb(40, 40, 40));
                e.Graphics.FillRectangle(brush, e.Bounds);
                e.Graphics.DrawLine(Pens.DarkGray, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font, e.Bounds,
                    Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            lv.DrawSubItem += (s, e) =>
            {
                bool isSelected = e.Item.Selected;
                if (isSelected)
                {
                    if (e.ColumnIndex == 0)
                    {
                        using (var brush = new LinearGradientBrush(e.Item.Bounds,
                            Color.FromArgb(255, 140, 0), Color.FromArgb(255, 210, 100), LinearGradientMode.Horizontal))
                        {
                            e.Graphics.FillRectangle(brush, e.Item.Bounds);
                        }
                    }
                    else
                        e.Graphics.FillRectangle(new SolidBrush(Color.Transparent), e.Bounds);
                }
                else
                    e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(20, 20, 20)), e.Bounds);

                Color foreColor = isSelected ? Color.Black : Color.White;

                if (e.ColumnIndex == 0 && e.Item.Tag is ServerConnectionData data)
                    foreColor = data.IsSecure ? Color.LimeGreen : Color.Gray;
                else if (e.ColumnIndex == 1 && e.Item.Tag is ServerConnectionData data2)
                    foreColor = data2.HasPassword ? Color.Red : Color.Gray;

                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, e.Bounds,
                    foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            };
            lv.SelectedIndexChanged += OnListViewSelectionChanged;

            ToolTip tooltip = new ToolTip();
            lv.MouseMove += (s, e) =>
            {
                ListViewItem item = lv.GetItemAt(e.X, e.Y);
                if (item != null && item.Tag is ServerConnectionData data3)
                {
                    string tip = $"{data3.Name}\n{data3.Ip}:{data3.Port}\nMap: {data3.Map}\n" +
                                 $"Players: {data3.Players}/{data3.MaxPlayers}";
                    tooltip.SetToolTip(lv, tip);
                }
                else
                {
                    tooltip.SetToolTip(lv, "");
                }
            };
        }

        private void Lv_DoubleClick(object sender, EventArgs e)
        {
            _ = LaunchProxy(true);
        }

        private void Lv_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv == null) return;

            if (e.Column == sortColumn)
                sortAscending = !sortAscending;
            else
            {
                sortColumn = e.Column;
                sortAscending = true;
            }

            lv.ListViewItemSorter = new ListViewItemComparer(e.Column, sortAscending);
            lv.Sort();
        }

        public class ListViewItemComparer : System.Collections.IComparer
        {
            private int col;
            private bool ascending;
            public ListViewItemComparer(int column, bool asc) { col = column; ascending = asc; }
            public int Compare(object x, object y)
            {
                ListViewItem a = x as ListViewItem, b = y as ListViewItem;
                if (a == null || b == null) return 0;
                string ta = a.SubItems[col].Text, tb = b.SubItems[col].Text;

                if (a.Tag is ServerConnectionData da && b.Tag is ServerConnectionData db)
                {
                    if (col == 9) // Last Connected (فقط برای history)
                        return ascending ? da.ConnectedAt.CompareTo(db.ConnectedAt)
                                         : db.ConnectedAt.CompareTo(da.ConnectedAt);
                }

                if (col == 4 || col == 6 || col == 7) // عددی
                {
                    if (int.TryParse(ta.Split('/')[0], out int na) && int.TryParse(tb.Split('/')[0], out int nb))
                        return ascending ? na.CompareTo(nb) : nb.CompareTo(na);
                    if (int.TryParse(ta, out na) && int.TryParse(tb, out nb))
                        return ascending ? na.CompareTo(nb) : nb.CompareTo(na);
                }
                return ascending ? string.Compare(ta, tb) : string.Compare(tb, ta);
            }
        }

        private void RefreshListView(ListView lv, List<ServerConnectionData> data)
        {
            if (lv == null) return;
            lv.BeginUpdate();
            lv.Items.Clear();
            bool isHistory = (lv == listViewHistory);
            foreach (var server in data)
            {
                // Cached/offline entries stay selectable so the local proxy can still be started.
                ListViewItem item = new ListViewItem(server.IsSecure ? "🛡️" : "");
                item.SubItems.Add(server.HasPassword ? "🔒" : "");
                item.SubItems.Add(server.Name ?? "Unknown");
                item.SubItems.Add($"{server.Ip}:{server.Port}");
                item.SubItems.Add($"{server.Players}/{server.MaxPlayers}");
                item.SubItems.Add(server.Map ?? "Unknown");
                item.SubItems.Add(server.Ping >= 0 ? server.Ping.ToString() : "—");
                item.SubItems.Add(server.TickRate > 0 ? server.TickRate.ToString() : "-");
                item.SubItems.Add(server.Tags ?? "Official");

                if (isHistory)
                {
                    string dateStr = server.ConnectedAt == DateTime.MinValue
                                     ? "Never"
                                     : server.ConnectedAt.ToString("yyyy-MM-dd HH:mm");
                    item.SubItems.Add(dateStr);
                }
                item.Tag = server;
                lv.Items.Add(item);
            }
            lv.EndUpdate();

            if (sortColumn != -1)
            {
                lv.ListViewItemSorter = new ListViewItemComparer(sortColumn, sortAscending);
                lv.Sort();
            }
        }

        private void OnListViewSelectionChanged(object sender, EventArgs e)
        {
            ListView lv = sender as ListView;
            if (lv == null) return;

            bool visible = (lv == listViewServers && tabControl1.SelectedTab == tabInternet) ||
                           (lv == listViewFavorites && tabControl1.SelectedTab == tabFavorites) ||
                           (lv == listViewHistory && tabControl1.SelectedTab == tabHistory) ||
                           (lv == listViewPersonal && tabControl1.SelectedTab == tabPersonal);
            if (!visible) return;

            bool hasSel = lv.SelectedItems.Count > 0;
            btnConnect.Enabled = hasSel;
            btnStartProxy.Enabled = hasSel && currentProxyProcess == null;
            btnStopProxy.Enabled = currentProxyProcess != null;
            if (btnPlayers != null) btnPlayers.Enabled = hasSel;

            if (hasSel)
            {
                var data = lv.SelectedItems[0].Tag as ServerConnectionData;
                if (data != null)
                {
                    UpdateSelectedIp(data.Ip);
                    UpdateInfoPanel(data);
                }
            }
            else
            {
                UpdateSelectedIp(null);
                UpdateInfoPanel(null);
            }
        }

        private ListView GetCurrentListView()
        {
            if (tabControl1.SelectedTab == tabFavorites) return listViewFavorites;
            if (tabControl1.SelectedTab == tabHistory) return listViewHistory;
            if (tabControl1.SelectedTab == tabPersonal) return listViewPersonal;
            return listViewServers;
        }

        private List<ServerConnectionData> GetCurrentDataList()
        {
            if (tabControl1.SelectedTab == tabFavorites) return favoriteServers;
            if (tabControl1.SelectedTab == tabHistory) return historyServers;
            if (tabControl1.SelectedTab == tabPersonal) return personalServers;
            return internetServers;
        }

        // ========== Buttons ==========
        private async Task AddServerAsync()
        {
            string input = txtServerIp.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                MessageBox.Show("Enter IP or domain.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            IPAddress ip = await ResolveDomainAsync(input);
            if (ip == null)
            {
                MessageBox.Show("Could not resolve hostname.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string serverIp = ip.ToString();
            int queryPort = (int)numGameQueryPort.Value;

            SetMessage($"Querying {serverIp}:{queryPort}...");
            var endpoint = new IPEndPoint(ip, queryPort);
            var info = await ServerQuery.GetServerDetailsAsync(endpoint);
            var rules = await ServerQuery.GetServerRulesAsync(endpoint);

            int tickRate = 0;
            string tags = "Official";
            string gameMode = "";
            if (rules != null)
            {
                tickRate = ExtractTickRate(rules);
                tags = ExtractTags(rules);
                gameMode = ExtractGameMode(rules);
            }

            ServerConnectionData newServer = new ServerConnectionData
            {
                Ip = serverIp,
                Port = queryPort,
                ForwardingPort = (int)numProxyPort.Value,
                ListeningPort = (int)numLocalPort.Value,
                Name = (info != null && !string.IsNullOrEmpty(info.Name))
                        ? info.Name : $"{input}:{queryPort} (Offline)",
                Map = (info != null) ? info.Map : "Unknown",
                Players = (info != null) ? info.Players : 0,
                MaxPlayers = (info != null) ? info.MaxPlayers : 0,
                Ping = (info != null) ? info.Ping : -1,
                HasPassword = (info != null) && info.HasPassword,
                IsSecure = (info != null) && info.IsSecure,
                TickRate = tickRate,
                Tags = tags,
                GameMode = gameMode,
                ConnectedAt = DateTime.MinValue
            };

            personalServers.Add(newServer);
            SaveAllServerLists();
            RefreshListView(listViewServers, internetServers);
            RefreshListView(listViewPersonal, personalServers);
            SetMessage($"Added: {newServer.Name}");

            if (svSearchBox != null) svSearchBox.Text = "";
            await RefreshCurrentListView(false);
        }

        private void CopyToFavorites(ServerConnectionData server)
        {
            if (!favoriteServers.Any(s => s.Ip == server.Ip && s.Port == server.Port))
            {
                favoriteServers.Add(CloneServer(server));
                SaveAllServerLists();
                RefreshListView(listViewFavorites, favoriteServers);
                SetMessage("Added to Favorites.");
            }
            else SetMessage("Already in Favorites.");
        }

        private void btnRemovePersonal_Click(object sender, EventArgs e)
        {
            ListView lv = GetCurrentListView();
            if (lv.SelectedItems.Count == 0) return;
            var sel = lv.SelectedItems[0].Tag as ServerConnectionData;
            if (sel == null) return;

            bool removed = personalServers.RemoveAll(s => s.Ip == sel.Ip && s.Port == sel.Port) > 0;
            if (removed)
            {
                SaveAllServerLists();
                RefreshListView(listViewPersonal, personalServers);
                SetMessage("Removed from personal list.");
                if (svSearchBox != null) svSearchBox.Text = "";
                _ = RefreshCurrentListView(false);
            }
            else SetMessage("Server not in personal list.");
        }

        private async Task ToggleConnectProxy()
        {
            if (currentProxyProcess != null)
                await StopProxyProcessAsync();
            else
                await LaunchProxy(true);
        }

        private async Task RefreshCurrentListView(bool fetchRemote)
        {
            _refreshCts?.Cancel();
            _refreshCts = new CancellationTokenSource();
            var ct = _refreshCts.Token;

            if (fetchRemote)
            {
                if (string.IsNullOrWhiteSpace(appSettings.ServerUpdateUrl) &&
                    string.IsNullOrWhiteSpace(appSettings.WorkshopMapUpdateUrl) &&
                    string.IsNullOrWhiteSpace(appSettings.AnnouncementUrl))
                {
                    // Remote sources are optional. Continue with the local cached list below.
                    SetMessage("Remote URLs are not configured; using cached data.");
                }

                toolStripProgressBar.Visible = true;
                toolStripProgressBar.Style = ProgressBarStyle.Marquee;
                try
                {
                    SetMessage("Fetching remote data...");
                    await FetchAndMergeRemoteServersAsync();
                    await FetchAndMergeRemoteWorkshopMapsAsync();
                    await FetchAndMergeRemoteAnnouncementAsync();
                    LoadAllServers();
                    LoadWorkshopMapsForCheck();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Remote update failed: {ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    toolStripProgressBar.Visible = false;
                }
            }

            _ignoreSearchTextChanged = true;
            if (svSearchBox != null) svSearchBox.Text = "";
            searchDebounceTimer.Stop();

            var currentLv = GetCurrentListView();
            var dataList = GetCurrentDataList();
            if (dataList == null || dataList.Count == 0)
            {
                SetMessage("No servers to refresh.");
                _ignoreSearchTextChanged = false;
                return;
            }

            string selectedKey = null;
            if (currentLv.SelectedItems.Count > 0 && currentLv.SelectedItems[0].Tag is ServerConnectionData sel)
                selectedKey = $"{sel.Ip}:{sel.Port}";

            btnRefresh.Enabled = false;
            SetMessage($"Refreshing {dataList.Count} servers...");

            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(50);
            DateTime refreshStart = DateTime.Now;

            foreach (var server in dataList.ToList())
            {
                if (ct.IsCancellationRequested) break;
                await semaphore.WaitAsync();
                if (ct.IsCancellationRequested)
                {
                    semaphore.Release();
                    break;
                }
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        ct.ThrowIfCancellationRequested();
                        server.Ping = -1;

                        var endpoint = new IPEndPoint(IPAddress.Parse(server.Ip), server.Port);
                        var info = await QueryWithCancellation(ct,
                            () => ServerQuery.GetServerDetailsAsync(endpoint));
                        var rules = await QueryWithCancellation(ct,
                            () => ServerQuery.GetServerRulesAsync(endpoint));

                        if (info != null)
                        {
                            server.Name = info.Name;
                            server.Map = info.Map;
                            server.Players = info.Players;
                            server.MaxPlayers = info.MaxPlayers;
                            server.Ping = info.Ping;
                            server.HasPassword = info.HasPassword;
                            server.IsSecure = info.IsSecure;

                            int tickRate = 0;
                            string tags = "Official";
                            string gameMode = "";
                            if (rules != null)
                            {
                                tickRate = ExtractTickRate(rules);
                                tags = ExtractTags(rules);
                                gameMode = ExtractGameMode(rules);
                            }
                            server.TickRate = tickRate;
                            server.Tags = tags;
                            server.GameMode = gameMode;
                        }
                        lastRefreshTimes[$"{server.Ip}:{server.Port}"] = DateTime.Now;
                    }
                    catch (OperationCanceledException) { }
                    catch { server.Ping = -1; }
                    finally { semaphore.Release(); }
                }));
            }

            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { }
            catch (AggregateException ae) { ae.Handle(ex => ex is OperationCanceledException); }

            if (ct.IsCancellationRequested)
            {
                _ignoreSearchTextChanged = false;
                btnRefresh.Enabled = true;
                UpdateServerCount();
                return;
            }

            RefreshListView(currentLv, dataList);

            if (!string.IsNullOrEmpty(selectedKey))
            {
                foreach (ListViewItem item in currentLv.Items)
                {
                    if (item.Tag is ServerConnectionData s && $"{s.Ip}:{s.Port}" == selectedKey)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }

            _ignoreSearchTextChanged = false;
            SetMessage("Refresh completed.");
            btnRefresh.Enabled = true;
            UpdateServerCount();

            if (tabControl1.SelectedTab == tabFavorites || tabControl1.SelectedTab == tabHistory)
                SaveAllServerLists();
        }

        private async Task<T> QueryWithCancellation<T>(CancellationToken ct, Func<Task<T>> query)
        {
            var task = query();
            var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, ct));
            ct.ThrowIfCancellationRequested();
            return await task;
        }

        // ========== Proxy ==========
        private async Task LaunchProxy(bool alsoLaunchSteam)
        {
            ListView lv = GetCurrentListView();
            if (lv.SelectedItems.Count == 0) return;
            var data = lv.SelectedItems[0].Tag as ServerConnectionData;
            if (data == null) return;

            if (!string.IsNullOrEmpty(data.Map) && data.Map.StartsWith("workshop/"))
            {
                string mapName = data.Map.Substring("workshop/".Length);
                var mapInfo = workshopMapsList.FirstOrDefault(m => m.MapName == mapName);
                if (mapInfo != null)
                {
                    string workshopPath = appSettings.WorkshopPath;
                    if (!string.IsNullOrEmpty(workshopPath) && Directory.Exists(workshopPath))
                    {
                        string mapDir = Path.Combine(workshopPath, "content", "730", mapInfo.WorkshopId.ToString());
                        if (!Directory.Exists(mapDir))
                        {
                            string msg = $"Workshop map '{mapName}' is missing.\nSize: " +
                                         $"{mapInfo.Size / 1048576.0:F1} MB\n\nDownload now?";
                            if (MessageBox.Show(msg, "Missing Map", MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question) == DialogResult.Yes)
                            {
                                var wsForm = new WorkshopMapsForm(mapInfo.WorkshopId);
                                wsForm.FormClosed += (s, ev) => wsForm.Dispose();
                                wsForm.Show(this);
                            }
                            else return;
                        }
                    }
                }
            }

            AddToHistory(data);

            btnStartProxy.Enabled = false;
            btnStopProxy.Enabled = false;
            if (btnPlayers != null) btnPlayers.Enabled = false;

            await StopProxyProcessAsync();

            try
            {
                ClearConsole();
                string target = data.Ip;
                int local = data.ListeningPort > 0 ? data.ListeningPort : (int)numLocalPort.Value;
                int comm = data.ForwardingPort > 0 ? data.ForwardingPort : (int)numProxyPort.Value;

                var psi = new ProcessStartInfo
                {
                    FileName = ".\\proxy.exe",
                    Arguments = $"client --local {local} --port {comm} --target {target}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false
                };

                Process proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                currentProxyProcess = proc;
                UpdateProxyStatus(true);

                proc.OutputDataReceived += (s, e) => AppendConsoleText(e.Data);
                proc.ErrorDataReceived += (s, e) => AppendConsoleText("[ERR] " + e.Data);
                proc.Exited += (s, ev) =>
                {
                    this.Invoke(() =>
                    {
                        if (currentProxyProcess == proc)
                        {
                            currentProxyProcess.Dispose();
                            currentProxyProcess = null;
                        }
                        UpdateProxyStatus(false);
                        AppendConsoleText("--- Proxy closed ---");
                        SetMessage("Proxy stopped.", Color.DodgerBlue);
                    });
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                AppendConsoleText($"Proxy started: --local {local} --port {comm} --target {target}");
                SetMessage($"Proxy started on localhost:{local}", Color.LimeGreen);

                if (alsoLaunchSteam)
                {
                    try
                    {
                        string uri = $"steam://rungameid/730//+connect%20localhost:{local}";
                        Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
                        SetMessage($"Steam launched. Connect to localhost:{local}", Color.LimeGreen);
                    }
                    catch (Exception steamEx)
                    {
                        SetMessage($"Failed to launch Steam: {steamEx.Message}. You can connect manually.", Color.Orange);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await Task.Delay(500);
                btnConnect.Enabled = lv.SelectedItems.Count > 0;
                btnStartProxy.Enabled = lv.SelectedItems.Count > 0 && currentProxyProcess == null;
                btnStopProxy.Enabled = currentProxyProcess != null;
                if (btnPlayers != null) btnPlayers.Enabled = true;
            }
        }

        // متد عمومی برای راه‌اندازی پروکسی از طریق PlayersForm
        public async Task LaunchProxyForServer(ServerConnectionData server)
        {
            ListView lv = GetCurrentListView();
            foreach (ListViewItem item in lv.Items)
            {
                if (item.Tag is ServerConnectionData s && s.Ip == server.Ip && s.Port == server.Port)
                {
                    item.Selected = true;
                    break;
                }
            }
            await LaunchProxy(true);
        }

        private async Task StopProxyProcessAsync()
        {
            if (currentProxyProcess == null) return;
            var processToStop = currentProxyProcess;
            currentProxyProcess = null;

            await Task.Run(() =>
            {
                try
                {
                    if (!processToStop.HasExited)
                    {
                        processToStop.Kill();
                        processToStop.WaitForExit(3000);
                    }
                }
                catch { }
                finally { processToStop.Dispose(); }
            });

            this.Invoke(() =>
            {
                UpdateProxyStatus(false);
                ListView lv = GetCurrentListView();
                btnConnect.Enabled = lv.SelectedItems.Count > 0;
                SetMessage("Proxy stopped.", Color.DodgerBlue);
            });
        }

        private void StopProxyProcess()
        {
            _ = StopProxyProcessAsync();
        }

        private void ClearConsole()
        {
            if (txtConsole.InvokeRequired)
                txtConsole.BeginInvoke(() => txtConsole.Clear());
            else
                txtConsole.Clear();
        }

        private void AppendConsoleText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (txtConsole.InvokeRequired)
            {
                txtConsole.BeginInvoke(new Action<string>(AppendConsoleText), text);
                return;
            }
            txtConsole.AppendText(text + Environment.NewLine);
            txtConsole.SelectionStart = txtConsole.Text.Length;
            txtConsole.ScrollToCaret();
        }

        private void AddToHistory(ServerConnectionData server)
        {
            var copy = CloneServer(server);
            copy.ConnectedAt = DateTime.Now;
            historyServers.Insert(0, copy);
            var duplicates = historyServers.Where(s => s.Ip == server.Ip && s.Port == server.Port).ToList();
            if (duplicates.Count > 1)
            {
                foreach (var dup in duplicates.Skip(1))
                    historyServers.Remove(dup);
            }
            SaveAllServerLists();
            RefreshListView(listViewHistory, historyServers);
        }

        private ServerConnectionData CloneServer(ServerConnectionData original)
        {
            return new ServerConnectionData
            {
                Ip = original.Ip,
                Port = original.Port,
                Name = original.Name,
                Map = original.Map,
                Players = original.Players,
                MaxPlayers = original.MaxPlayers,
                Ping = original.Ping,
                HasPassword = original.HasPassword,
                IsSecure = original.IsSecure,
                TickRate = original.TickRate,
                Tags = original.Tags,
                GameMode = original.GameMode,
                ConnectedAt = original.ConnectedAt
            };
        }

        // ========== UI Helpers ==========
        private void UpdateProxyStatus(bool active)
        {
            if (active)
            {
                lblProxyStatus.Text = "Proxy: Active";
                lblProxyStatus.ForeColor = Color.LimeGreen;
                btnStartProxy.Enabled = false;
                btnStopProxy.Enabled = true;
                btnConnect.Text = "Disconnect";
                btnConnect.BackColor = Color.FromArgb(60, 60, 60);
                btnConnect.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                btnConnect.ForeColor = Color.White;
            }
            else
            {
                lblProxyStatus.Text = "Proxy: Idle";
                lblProxyStatus.ForeColor = Color.LightGray;
                btnStartProxy.Enabled = GetCurrentListView().SelectedItems.Count > 0;
                btnStopProxy.Enabled = false;
                btnConnect.Text = "Connect";
                btnConnect.BackColor = Color.FromArgb(60, 60, 60);
                btnConnect.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                btnConnect.ForeColor = Color.White;
            }
        }

        private void UpdateSelectedIp(string ip) =>
            lblSelectedIp.Text = string.IsNullOrEmpty(ip) ? "No server selected" : $"Selected: {ip}";

        private void UpdateServerCount()
        {
            int count = GetCurrentListView().Items.Count;
            lblServerCount.Text = $"{count} server{(count != 1 ? "s" : "")}";
        }

        private void SetMessage(string text, Color? color = null)
        {
            lblMessage.Text = text;
            lblMessage.ForeColor = color ?? Color.White;
        }

        private int ExtractTickRate(Dictionary<string, string> rules)
        {
            if (rules == null) return 0;
            foreach (var key in rules.Keys)
            {
                if (key.Equals("tickrate", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("sv_tickrate", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(rules[key], out int tr)) return tr;
                }
            }
            return 0;
        }

        private string ExtractTags(Dictionary<string, string> rules)
        {
            if (rules == null) return "Official";
            foreach (var key in rules.Keys)
            {
                if (key.Equals("sv_tags", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("tags", StringComparison.OrdinalIgnoreCase))
                    return rules[key];
            }
            return "Official";
        }

        private string ExtractGameMode(Dictionary<string, string> rules)
        {
            if (rules == null) return "";
            string[] modeKeys = { "game_des", "GameMode", "sv_game_mode", "game_mode" };
            foreach (var key in modeKeys)
            {
                foreach (var ruleKey in rules.Keys)
                {
                    if (ruleKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                        return rules[ruleKey];
                }
            }
            return "";
        }

        private void UpdateInfoPanel(ServerConnectionData data)
        {
            if (data == null)
            {
                if (lblServerNameValue != null) lblServerNameValue.Text = "-";
                if (lblAddressValue != null) lblAddressValue.Text = "-";
                if (lblPlayersValue != null) lblPlayersValue.Text = "-";
                if (lblMapValue != null) lblMapValue.Text = "-";
                if (lblGameModeValue != null) lblGameModeValue.Text = "-";
                if (lblPingValue != null) lblPingValue.Text = "-";
                if (lblTagsValue != null) lblTagsValue.Text = "-";
                if (mapPicture != null) mapPicture.Image = null;
                return;
            }
            if (lblServerNameValue != null) lblServerNameValue.Text = data.Name ?? "-";
            if (lblAddressValue != null) lblAddressValue.Text = $"{data.Ip}:{data.Port}";
            if (lblPlayersValue != null) lblPlayersValue.Text = $"{data.Players}/{data.MaxPlayers}";
            if (lblMapValue != null) lblMapValue.Text = data.Map ?? "-";
            if (lblGameModeValue != null) lblGameModeValue.Text = data.GameMode ?? "–";
            if (lblPingValue != null) lblPingValue.Text = data.Ping.ToString();
            if (lblTagsValue != null) lblTagsValue.Text = data.Tags ?? "Official";

            string map = data.Map;
            if (!string.IsNullOrEmpty(map))
            {
                string img = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "maps", map + ".jpg");
                if (File.Exists(img) && mapPicture != null)
                {
                    try
                    {
                        if (mapPicture.Image != null) mapPicture.Image.Dispose();
                        mapPicture.Image = Image.FromFile(img);
                    }
                    catch { mapPicture.Image = null; }
                }
                else if (mapPicture != null) mapPicture.Image = null;
            }
            else if (mapPicture != null)
                mapPicture.Image = null;
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateServerCount();
            listViewServers.SelectedItems.Clear();
            listViewFavorites.SelectedItems.Clear();
            listViewHistory.SelectedItems.Clear();
            listViewPersonal.SelectedItems.Clear();
        }

        private void AttachCommonEvents()
        {
            btnRefresh.Click += async (s, e) => await RefreshCurrentListView(true);
            btnConnect.Click += async (s, e) => await ToggleConnectProxy();

            btnAddServer.Click += async (s, e) => await AddServerAsync();
            if (btnPlayers != null) btnPlayers.Click += btnPlayers_Click;

            if (btnCheckUpdates != null)
            {
                btnCheckUpdates.Click += (s, e) =>
                {
                    var updateForm = new UpdateForm(appSettings);
                    updateForm.ShowDialog(this);
                };
            }

            btnRemoveSelected.Click += (s, e) =>
            {
                ListView lv = GetCurrentListView();
                var list = GetCurrentDataList();
                if (lv.SelectedItems.Count == 0 || list == null) return;
                var sel = lv.SelectedItems[0].Tag as ServerConnectionData;
                if (sel == null) return;
                list.RemoveAll(srv => srv.Ip == sel.Ip && srv.Port == sel.Port);
                RefreshListView(lv, list);
                SaveAllServerLists();
                SetMessage("Server removed.");
                UpdateServerCount();
            };

            if (btnAddFavorite != null)
                btnAddFavorite.Click += (s, e) =>
                {
                    ListView lv = GetCurrentListView();
                    if (lv.SelectedItems.Count == 0) return;
                    var sel = lv.SelectedItems[0].Tag as ServerConnectionData;
                    if (sel != null) CopyToFavorites(sel);
                };

            if (btnRemovePersonal != null)
                btnRemovePersonal.Click += btnRemovePersonal_Click;

            if (svSearchBox != null)
                svSearchBox.TextChanged += (s, e) =>
                {
                    if (_ignoreSearchTextChanged) return;
                    searchDebounceTimer.Stop();
                    searchDebounceTimer.Start();
                };

            if (btnGetMaps != null)
            {
                btnGetMaps.Click -= btnGetMaps_Click;
                btnGetMaps.Click += btnGetMaps_Click;
            }
        }

        private void BtnStartProxy_Click(object sender, EventArgs e)
        {
            _ = LaunchProxy(false);
        }

        private async void BtnStopProxy_Click(object sender, EventArgs e)
        {
            await StopProxyProcessAsync();
        }

        private async void btnPlayers_Click(object sender, EventArgs e)
        {
            ListView lv = GetCurrentListView();
            if (lv.SelectedItems.Count == 0) return;
            var data = lv.SelectedItems[0].Tag as ServerConnectionData;
            if (data == null) return;

            btnPlayers.Enabled = false;
            SetMessage("Getting players...");
            try
            {
                var endpoint = new IPEndPoint(IPAddress.Parse(data.Ip), data.Port);
                var players = await ServerQuery.GetPlayersAsync(endpoint);
                // اصلاح: همیشه فرم باز شود (حتی اگر خالی باشد)
                if (players == null)
                    players = new List<PlayerInfo>();

                using (var pf = new PlayersForm(players, data))
                    pf.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to retrieve players: " + ex.Message);
            }
            finally
            {
                btnPlayers.Enabled = true;
                SetMessage("Ready");
            }
        }

        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (!IPAddress.TryParse(txtServerPublicIp.Text, out _))
            {
                MessageBox.Show("Invalid public IP.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            int intPort = (int)numServerInternalPort.Value;
            string pubIp = txtServerPublicIp.Text;
            int clPort = (int)numServerClientPort.Value;
            string args = $"server --internal {intPort} --ip {pubIp} --port {clPort}";
            string cmdArgs = $"/k title CS2ProxyServer & .\\proxy.exe {args} & pause";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    UseShellExecute = false,
                    CreateNoWindow = false
                });
                SetMessage($"Server proxy started: --internal {intPort} --ip {pubIp} --port {clPort}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnGetMaps_Click(object sender, EventArgs e)
        {
            var existing = Application.OpenForms.OfType<WorkshopMapsForm>().FirstOrDefault();
            if (existing != null)
            {
                existing.Activate();
                return;
            }
            var form = new WorkshopMapsForm();
            form.FormClosed += (s, ev) => form.Dispose();
            form.Show(this);
        }

        // ========== Visual Style ==========
        private void ApplyVisualStyle()
        {
            this.BackColor = Color.FromArgb(22, 22, 22);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            if (mainMenu != null)
            {
                mainMenu.BackColor = Color.FromArgb(35, 35, 35);
                mainMenu.ForeColor = Color.White;
                mainMenu.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                foreach (ToolStripMenuItem item in mainMenu.Items)
                {
                    item.BackColor = Color.FromArgb(35, 35, 35);
                    item.ForeColor = Color.White;
                    item.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
            }

            if (tabControl1 != null)
            {
                tabControl1.BackColor = Color.FromArgb(22, 22, 22);
                tabControl1.ForeColor = Color.White;
                tabControl1.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                foreach (TabPage page in tabControl1.TabPages)
                {
                    page.BackColor = Color.FromArgb(22, 22, 22);
                    page.ForeColor = Color.White;
                    page.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
            }

            statusStrip1.BackColor = Color.FromArgb(35, 35, 35);
            statusStrip1.ForeColor = Color.White;
            foreach (ToolStripItem item in statusStrip1.Items)
                item.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            if (topMessagePanel != null)
            {
                topMessagePanel.Paint += (s, e) =>
                {
                    using (var brush = new LinearGradientBrush(topMessagePanel.ClientRectangle,
                        Color.FromArgb(255, 210, 0), Color.FromArgb(255, 150, 0), 0f))   // افقی از چپ به راست
                    {
                        e.Graphics.FillRectangle(brush, topMessagePanel.ClientRectangle);
                    }
                };
                topMessageLabel.BackColor = Color.Transparent;
                topMessageLabel.ForeColor = Color.Black;
                topMessageLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }

            foreach (Control ctrl in GetAllControls(this))
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
                    btn.ForeColor = Color.White;
                    btn.Font = new Font("Segoe UI", 9f,
                        (btn == btnRefresh || btn == btnConnect) ? FontStyle.Bold : FontStyle.Regular);
                    btn.Paint += Btn_Paint;
                }
                else if (ctrl is TextBox tb)
                {
                    tb.BackColor = Color.FromArgb(40, 40, 40);
                    tb.ForeColor = Color.White;
                    tb.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
                else if (ctrl is NumericUpDown nud)
                {
                    nud.BackColor = Color.FromArgb(40, 40, 40);
                    nud.ForeColor = Color.White;
                    nud.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
                else if (ctrl is Label lbl && lbl != topMessageLabel)
                {
                    lbl.ForeColor = Color.White;
                    lbl.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                }
                else if (ctrl is GroupBox gb)
                {
                    gb.BackColor = Color.FromArgb(22, 22, 22);
                    gb.ForeColor = Color.White;
                    gb.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    gb.FlatStyle = FlatStyle.Flat;
                }
                else if (ctrl is Panel pnl && pnl != topMessagePanel && pnl != consolePanel)
                {
                    pnl.BackColor = Color.FromArgb(22, 22, 22);
                }
            }

            if (btnAddFavorite != null)
            {
                btnAddFavorite.ForeColor = Color.Gold;
                btnAddFavorite.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            }

            if (numServerInternalPort != null)
            {
                numServerInternalPort.BackColor = Color.FromArgb(40, 40, 40);
                numServerInternalPort.ForeColor = Color.White;
                numServerInternalPort.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            }
            if (numServerClientPort != null)
            {
                numServerClientPort.BackColor = Color.FromArgb(40, 40, 40);
                numServerClientPort.ForeColor = Color.White;
                numServerClientPort.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            }

            foreach (Label lbl in new[] { lblServerNameValue, lblAddressValue, lblPlayersValue,
                                         lblMapValue, lblPingValue, lblGameModeValue, lblTagsValue })
                if (lbl != null) lbl.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            btnConnect.Text = "Connect";
            btnConnect.BackColor = Color.FromArgb(60, 60, 60);
            btnConnect.FlatAppearance.BorderColor = Color.FromArgb(120, 120, 120);
            btnConnect.ForeColor = Color.White;
            btnConnect.FlatStyle = FlatStyle.Flat;
        }

        private void Btn_Paint(object sender, PaintEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            Rectangle rect = btn.ClientRectangle;
            using (var brush = new LinearGradientBrush(rect,
                       Color.FromArgb(60, 60, 60),
                       Color.FromArgb(90, 90, 90),
                       LinearGradientMode.Vertical))
            {
                e.Graphics.FillRectangle(brush, rect);
            }

            ControlPaint.DrawBorder(e.Graphics, rect,
                btn.FlatAppearance.BorderColor, ButtonBorderStyle.Solid);

            TextRenderer.DrawText(e.Graphics, btn.Text, btn.Font, rect,
                btn.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                yield return ctrl;
                foreach (Control child in GetAllControls(ctrl))
                    yield return child;
            }
        }
    }

    // ========== کلاس‌های کمکی ==========
    public class RemoteServerInternetContainer
    {
        public List<ServerConfig> Internet { get; set; } = new List<ServerConfig>();
    }

    public class ServerInternetContainer
    {
        public List<ServerConnectionData> Internet { get; set; } = new List<ServerConnectionData>();
    }

    public class SavedServersContainer
    {
        public List<ServerConnectionData> Personal { get; set; } = new List<ServerConnectionData>();
        public List<ServerConnectionData> Favorites { get; set; } = new List<ServerConnectionData>();
        public List<ServerConnectionData> History { get; set; } = new List<ServerConnectionData>();
    }

    public class ServerConnectionData
    {
        public string Ip { get; set; } = "";
        public int Port { get; set; }
        public int ForwardingPort { get; set; }
        public int ListeningPort { get; set; }
        public string Name { get; set; } = "";
        public string Map { get; set; } = "";
        public int Players { get; set; }
        public int MaxPlayers { get; set; }
        public int Ping { get; set; }
        public bool HasPassword { get; set; }
        public bool IsSecure { get; set; }
        public int TickRate { get; set; }
        public string Tags { get; set; } = "";
        public string GameMode { get; set; } = "";
        public DateTime ConnectedAt { get; set; } = DateTime.MinValue;
    }
}