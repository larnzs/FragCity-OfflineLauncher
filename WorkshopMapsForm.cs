#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSserver_browser
{
    public partial class WorkshopMapsForm : Form
    {
        // ---------- Controls ----------
        private TextBox txtWorkshopPath;
        private Button btnBrowse, btnLoad, btnRefresh;
        private TextBox txtSearch;
        private System.Windows.Forms.Timer searchDebounceTimer;

        private ListView listViewMaps;
        private GroupBox grpDetails;
        private PictureBox mapPicture;
        private Label lblMapNameCaption, lblMapNameValue;
        private Label lblIdCaption, lblIdValue;
        private Label lblSizeCaption, lblSizeValue;
        private Label lblUpdatedCaption, lblUpdatedValue;
        private Label lblManifestCaption, lblManifestValue;
        private Label lblStatusCaption, lblStatusValue;
        private Button btnDownload, btnStop, btnInstallAcf, btnClose;
        private ProgressBar progressBar;
        private Label lblProgress;

        // ---------- New help components ----------
        private Label lblImportantTitle;
        private ToolTip toolTipHelp;

        // ---------- Data ----------
        private List<WorkshopMapItem> maps = new List<WorkshopMapItem>();
        private string workshopFolder = "";
        private string acfDownloadUrl = "";
        private CancellationTokenSource cts;
        private long? autoDownloadId = null;

        private System.Windows.Forms.Timer blinkTimer;

        public WorkshopMapsForm() : this(null) { }

        public WorkshopMapsForm(long? mapIdToDownload)
        {
            autoDownloadId = mapIdToDownload;
            this.Text = "CS2 Workshop Maps Manager";
            this.Size = new Size(780, 510);  // کمی ارتفاع بیشتر برای جا دادن راهنما
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = true;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ShowIcon = false;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 8f, FontStyle.Regular);

            BuildControls();
            SetupSearchDebouncer();

            LoadAcfUrl();
            btnInstallAcf.Visible = false;
            TrySetDefaultWorkshopPath();

            if (!string.IsNullOrEmpty(workshopFolder) && Directory.Exists(workshopFolder))
            {
                EnsureWorkshopStructure();
                LoadMapList();

                if (autoDownloadId.HasValue)
                {
                    var targetMap = maps.FirstOrDefault(m => m.WorkshopId == autoDownloadId.Value);
                    if (targetMap != null)
                    {
                        SelectMapInList(targetMap);
                        if (!string.IsNullOrEmpty(targetMap.DownloadUrl) && !targetMap.IsInstalled)
                            BeginInvoke(new Action(() => StartDownload(targetMap)));
                    }
                }
            }
        }

        // ========== UI ==========
        private void BuildControls()
        {
            // ---------- Important help title ----------
            lblImportantTitle = new Label
            {
                Text = "- مهم حتما بخونید -",
                Location = new Point(250, 390),
                Size = new Size(160, 22),
                ForeColor = Color.Gold,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12, FontStyle.Bold | FontStyle.Underline),
                Cursor = Cursors.Help
            };

            toolTipHelp = new ToolTip
            {
                ToolTipTitle = "workshop راهنمای فعال‌سازی مپ های",
                IsBalloon = true,
                AutoPopDelay = 30000,
                InitialDelay = 300,
                ReshowDelay = 100,
                ToolTipIcon = ToolTipIcon.Info,
                BackColor = Color.FromArgb(35, 35, 35),
                ForeColor = Color.White
            };

            string helpText =
                "برای جوین شدن به سرور با مپ ورکشاپ حتما نیاز هست برای دریافت فایل های امضا یکبار به استیم وصل بشید \n\n" +
                "درصورت امکان با استفاده از دی ان اس شکن یا قندشکن میتونید به استیم وصل بشید و به سرور جوین بدید, حتما قبل از جوین شدن فایل مپ رو از این طریق دانلود کنید\n" +
                "بعد از آنلاین شدن به سروری که مپ ورکشاپ داره وصل بشید پیام دانلود میاد واستون ولی از جایی که میتونید مپ رو از همین صفحه دانلود کنید, استیم فایل مپ رو خودش پیدا میکنه\n" +
                "مپ مورد نظرتون رو از همین صفحه دانلود کنید بعد وارد سرور بشید\n" +
                "برای دفعات بعدی اتصال دیگه نیازی به وصل شدن به استیم نیست و شما میتونید حتی به صورت آفلاین به سرور جوین بدید فقط کافیه فایل امضا رو از استیم دریافت کنید";

            toolTipHelp.SetToolTip(lblImportantTitle, helpText);

            // ---------- Workshop Folder ----------
            Label lblPath = new Label { Text = "Workshop Folder:", Location = new Point(10, 25), Size = new Size(100, 20), ForeColor = Color.White };
            txtWorkshopPath = new TextBox { Location = new Point(110, 23), Size = new Size(460, 22), ReadOnly = true, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            btnBrowse = new Button { Text = "Browse...", Location = new Point(575, 23), Size = new Size(70, 22), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) } };
            btnBrowse.Click += BtnBrowse_Click;
            btnLoad = new Button { Text = "Load", Location = new Point(650, 23), Size = new Size(70, 22), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) } };
            btnLoad.Click += BtnLoad_Click;
            btnRefresh = new Button { Text = "↻", Location = new Point(725, 23), Size = new Size(30, 24), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) }, Font = new Font("Segoe UI", 12f, FontStyle.Bold) };
            btnRefresh.Click += (s, e) => LoadMapList();

            // ---------- Search ----------
            Label lblSearch = new Label { Text = "Search:", Location = new Point(10, 55), Size = new Size(45, 20), ForeColor = Color.White };
            txtSearch = new TextBox { Location = new Point(55, 52), Size = new Size(700, 22), BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };

            // ---------- ListView ----------
            listViewMaps = new ListView { Location = new Point(10, 82), Size = new Size(490, 300), View = View.Details, FullRowSelect = true, MultiSelect = false, OwnerDraw = true, HideSelection = false, HeaderStyle = ColumnHeaderStyle.Clickable, BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            listViewMaps.Columns.Add("Map Name", 200);
            listViewMaps.Columns.Add("Workshop ID", 95);
            listViewMaps.Columns.Add("Size (MB)", 80);
            listViewMaps.Columns.Add("Status", 97);
            listViewMaps.DrawColumnHeader += (s, e) => { using var brush = new SolidBrush(Color.FromArgb(40, 40, 40)); e.Graphics.FillRectangle(brush, e.Bounds); e.Graphics.DrawLine(Pens.DarkGray, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom); TextRenderer.DrawText(e.Graphics, e.Header.Text, e.Font, e.Bounds, Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); };
            listViewMaps.DrawSubItem += (s, e) => { Color backColor = e.Item.Selected ? Color.Gold : Color.FromArgb(20, 20, 20); Color foreColor = e.Item.Selected ? Color.Black : Color.White; e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds); TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, e.Bounds, foreColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); };
            listViewMaps.SelectedIndexChanged += ListViewMaps_SelectedIndexChanged;

            // ---------- Details GroupBox ----------
            grpDetails = new GroupBox { Text = "Map Details", Location = new Point(509, 82), Size = new Size(245, 300), BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            mapPicture = new PictureBox { Location = new Point(10, 20), Size = new Size(226, 115), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(20, 20, 20), BorderStyle = BorderStyle.FixedSingle };
            int y = 145;
            lblMapNameCaption = CreateLabel("Name:", 10, y, 70); lblMapNameValue = CreateLabel("-", 95, y, 135); y += 20;
            lblIdCaption = CreateLabel("ID:", 10, y, 70); lblIdValue = CreateLabel("-", 95, y, 135); y += 20;
            lblSizeCaption = CreateLabel("Size:", 10, y, 70); lblSizeValue = CreateLabel("-", 95, y, 135); y += 20;
            lblUpdatedCaption = CreateLabel("Updated:", 10, y, 70); lblUpdatedValue = CreateLabel("-", 95, y, 135); y += 20;
            lblManifestCaption = CreateLabel("Manifest:", 10, y, 70); lblManifestValue = CreateLabel("-", 95, y, 135); y += 20;
            lblStatusCaption = CreateLabel("Status:", 10, y, 70); lblStatusValue = CreateLabel("-", 95, y, 135);

            grpDetails.Controls.AddRange(new Control[] { mapPicture, lblMapNameCaption, lblMapNameValue, lblIdCaption, lblIdValue, lblSizeCaption, lblSizeValue, lblUpdatedCaption, lblUpdatedValue, lblManifestCaption, lblManifestValue, lblStatusCaption, lblStatusValue });

            // ---------- Buttons ----------
            btnDownload = new Button { Text = "Download && Install", Location = new Point(10, 392), Size = new Size(130, 22), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) } };
            btnDownload.Click += BtnDownload_Click;
            btnStop = new Button { Text = "Stop", Location = new Point(150, 392), Size = new Size(70, 22), Enabled = false, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) } };
            btnStop.Click += BtnStop_Click;
            btnInstallAcf = new Button { Text = "Install ACF", Location = new Point(230, 392), Size = new Size(100, 22), Enabled = true, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) } };
            btnInstallAcf.Click += BtnInstallAcf_Click;
            btnClose = new Button { Text = "Close", Location = new Point(678, 392), Size = new Size(75, 22), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White, FlatAppearance = { BorderColor = Color.FromArgb(80, 80, 80) } };
            btnClose.Click += (s, e) => this.Close();

            // ---------- ProgressBar ----------
            progressBar = new ProgressBar { Location = new Point(10, 422), Size = new Size(510, 20), Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100 };
            lblProgress = new Label { Location = new Point(530, 422), Size = new Size(240, 20), Text = "0%", ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft };

            this.Controls.AddRange(new Control[] { lblImportantTitle, lblPath, txtWorkshopPath, btnBrowse, btnLoad, btnRefresh, lblSearch, txtSearch, listViewMaps, grpDetails, btnDownload, btnStop, btnInstallAcf, btnClose, progressBar, lblProgress });

            blinkTimer = new System.Windows.Forms.Timer { Interval = 600 };
            blinkTimer.Tick += (s, e) => { if (btnInstallAcf.BackColor == Color.Gold) { btnInstallAcf.BackColor = Color.FromArgb(50, 50, 50); btnInstallAcf.ForeColor = Color.White; } else { btnInstallAcf.BackColor = Color.Gold; btnInstallAcf.ForeColor = Color.Black; } };
            blinkTimer.Start();
        }

        private Label CreateLabel(string text, int x, int y, int width = 150)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 14), ForeColor = Color.White, BackColor = Color.Transparent, Font = new Font("Segoe UI", 8f, FontStyle.Regular) };
        }

        private void SetupSearchDebouncer()
        {
            searchDebounceTimer = new System.Windows.Forms.Timer { Interval = 300 };
            searchDebounceTimer.Tick += (s, e) => { searchDebounceTimer.Stop(); ApplyFilter(); };
            txtSearch.TextChanged += (s, e) => { searchDebounceTimer.Stop(); searchDebounceTimer.Start(); };
        }

        private void ApplyFilter()
        {
            if (maps == null || maps.Count == 0) return;
            string filter = txtSearch.Text.Trim().ToLowerInvariant();
            listViewMaps.BeginUpdate();
            listViewMaps.Items.Clear();
            foreach (var map in maps)
                if (string.IsNullOrEmpty(filter) || map.MapName.ToLowerInvariant().Contains(filter) || map.WorkshopId.ToString().Contains(filter))
                { var item = new ListViewItem(map.MapName); item.SubItems.Add(map.WorkshopId.ToString()); item.SubItems.Add((map.Size / 1048576.0).ToString("F1")); item.SubItems.Add(map.IsInstalled ? "Installed" : "Not Installed"); item.Tag = map; listViewMaps.Items.Add(item); }
            listViewMaps.EndUpdate();
        }

        private void TrySetDefaultWorkshopPath()
        {
            var settings = SettingsManager.Load();
            if (!string.IsNullOrEmpty(settings.WorkshopPath) && Directory.Exists(settings.WorkshopPath))
            {
                txtWorkshopPath.Text = settings.WorkshopPath;
                workshopFolder = settings.WorkshopPath;
                return;
            }

            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string steamPath = key?.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                    {
                        string ws = Path.Combine(steamPath, "steamapps", "workshop");
                        if (Directory.Exists(ws))
                        {
                            txtWorkshopPath.Text = ws;
                            workshopFolder = ws;
                        }
                    }
                }
            }
            catch { }
        }

        private void EnsureWorkshopStructure()
        {
            string c730 = Path.Combine(workshopFolder, "content", "730");
            if (!Directory.Exists(c730)) Directory.CreateDirectory(c730);
        }

        public void UpdateWorkshopPath(string newPath)
        {
            if (string.IsNullOrEmpty(newPath) || !Directory.Exists(newPath))
                return;

            txtWorkshopPath.Text = newPath;
            workshopFolder = newPath;
            EnsureWorkshopStructure();
            LoadMapList();
            LoadAcfUrl();
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select the destination workshop folder (contains content/730)";
                if (!string.IsNullOrEmpty(txtWorkshopPath.Text) && Directory.Exists(txtWorkshopPath.Text))
                    dlg.SelectedPath = txtWorkshopPath.Text;
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    txtWorkshopPath.Text = dlg.SelectedPath;
                    workshopFolder = dlg.SelectedPath;

                    var settings = SettingsManager.Load();
                    settings.WorkshopPath = workshopFolder;
                    SettingsManager.Save(settings);

                    var mainForm = Application.OpenForms.OfType<Form1>().FirstOrDefault();
                    mainForm?.ReloadSettings();

                    EnsureWorkshopStructure();
                    LoadMapList();
                }
            }
        }

        private void BtnLoad_Click(object sender, EventArgs e)
        {
            workshopFolder = txtWorkshopPath.Text.Trim();
            if (string.IsNullOrEmpty(workshopFolder) || !Directory.Exists(workshopFolder))
            {
                MessageBox.Show("Please select a valid workshop folder.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var settings = SettingsManager.Load();
            settings.WorkshopPath = workshopFolder;
            SettingsManager.Save(settings);

            var mainForm = Application.OpenForms.OfType<Form1>().FirstOrDefault();
            mainForm?.ReloadSettings();

            EnsureWorkshopStructure();
            LoadMapList();
        }

        private void LoadMapList()
        {
            if (string.IsNullOrEmpty(workshopFolder)) return;
            string jsonPath = AppDataPaths.GetFilePath("workshop_maps.json");
            if (!File.Exists(jsonPath))
            {
                MessageBox.Show($"workshop_maps.json not found at:\n{jsonPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string json = File.ReadAllText(jsonPath);
                maps = JsonSerializer.Deserialize<List<WorkshopMapItem>>(json) ?? new List<WorkshopMapItem>();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading workshop_maps.json: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string c730 = Path.Combine(workshopFolder, "content", "730");
            foreach (var m in maps)
            {
                string dir = Path.Combine(c730, m.WorkshopId.ToString());
                m.IsInstalled = Directory.Exists(dir);
                m.LocalPath = dir;
            }
            PopulateListView();
        }

        private void LoadAcfUrl()
        {
            string acfJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "acf.json");
            if (!File.Exists(acfJsonPath))
            {
                acfDownloadUrl = "";
                return;
            }

            try
            {
                string json = File.ReadAllText(acfJsonPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("acfUrl", out JsonElement urlProp))
                    {
                        acfDownloadUrl = urlProp.GetString();
                        return;
                    }
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        JsonElement first = root[0];
                        if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("acfUrl", out urlProp))
                        {
                            acfDownloadUrl = urlProp.GetString();
                            return;
                        }
                    }
                }
                acfDownloadUrl = "";
            }
            catch { acfDownloadUrl = ""; }
        }

        private void PopulateListView()
        {
            listViewMaps.Items.Clear();
            foreach (var m in maps)
            {
                var item = new ListViewItem(m.MapName);
                item.SubItems.Add(m.WorkshopId.ToString());
                item.SubItems.Add((m.Size / 1048576.0).ToString("F1"));
                item.SubItems.Add(m.IsInstalled ? "Installed" : "Not Installed");
                item.Tag = m;
                listViewMaps.Items.Add(item);
            }
            ApplyFilter();
        }

        private void ListViewMaps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewMaps.SelectedItems.Count > 0)
            {
                var map = listViewMaps.SelectedItems[0].Tag as WorkshopMapItem;
                if (map != null) ShowMapDetails(map);
            }
            else ClearDetails();
        }

        private void ShowMapDetails(WorkshopMapItem map)
        {
            lblMapNameValue.Text = map.MapName;
            lblIdValue.Text = map.WorkshopId.ToString();
            lblSizeValue.Text = $"{map.Size / 1048576.0:F1} MB";
            lblUpdatedValue.Text = map.TimeUpdated > 0 ? DateTimeOffset.FromUnixTimeSeconds(map.TimeUpdated).LocalDateTime.ToString("g") : "-";
            lblManifestValue.Text = map.Manifest ?? "-";
            lblStatusValue.Text = map.IsInstalled ? "Installed" : "Not Installed";
            if (mapPicture != null)
            {
                string img = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "maps", map.MapName + ".jpg");
                if (File.Exists(img))
                {
                    try { mapPicture.Image?.Dispose(); mapPicture.Image = Image.FromFile(img); } catch { mapPicture.Image = null; }
                }
                else mapPicture.Image = null;
            }
        }

        private void ClearDetails()
        {
            foreach (var l in new[] { lblMapNameValue, lblIdValue, lblSizeValue, lblUpdatedValue, lblManifestValue, lblStatusValue })
                l.Text = "-";
            if (mapPicture != null) mapPicture.Image = null;
        }

        private void SelectMapInList(WorkshopMapItem map)
        {
            foreach (ListViewItem item in listViewMaps.Items)
                if (item.Tag is WorkshopMapItem m && m.WorkshopId == map.WorkshopId)
                { item.Selected = true; item.EnsureVisible(); break; }
        }

        private async void BtnInstallAcf_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(acfDownloadUrl))
            {
                string acfJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "acf.json");
                MessageBox.Show(
                    $"ACF download URL is not set.\n\nMake sure the file 'acf.json' exists at:\n{acfJsonPath}\nand contains a valid 'acfUrl'.",
                    "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrEmpty(workshopFolder) || !Directory.Exists(workshopFolder))
            {
                MessageBox.Show("Load a valid workshop folder first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnInstallAcf.Enabled = false;
            btnStop.Enabled = true;
            progressBar.Value = 0;
            lblProgress.Text = "Downloading ACF...";

            cts = new CancellationTokenSource();
            var token = cts.Token;

            try
            {
                string tempFile = Path.GetTempFileName();
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(acfDownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();
                    long? totalBytes = response.Content.Headers.ContentLength;
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = File.Create(tempFile))
                    {
                        byte[] buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                            totalRead += bytesRead;
                            if (totalBytes.HasValue)
                            {
                                int pct = (int)(totalRead * 100 / totalBytes.Value);
                                progressBar.Value = pct;
                                lblProgress.Text = $"Downloading ACF... {pct}%";
                            }
                        }
                    }
                }

                string destAcf = Path.Combine(workshopFolder, "appworkshop_730.acf");
                File.Copy(tempFile, destAcf, true);
                File.Delete(tempFile);

                progressBar.Value = 100;
                lblProgress.Text = "ACF installed successfully.";
                MessageBox.Show("ACF file has been updated.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                lblProgress.Text = "Download cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to download ACF: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnInstallAcf.Enabled = true;
                btnStop.Enabled = false;
                progressBar.Value = 0;
                lblProgress.Text = "0%";
            }
        }

        private void BtnDownload_Click(object sender, EventArgs e)
        {
            if (listViewMaps.SelectedItems.Count == 0) return;
            var map = listViewMaps.SelectedItems[0].Tag as WorkshopMapItem;
            if (map != null) StartDownload(map);
        }

        private void StartDownload(WorkshopMapItem map)
        {
            if (string.IsNullOrWhiteSpace(map.DownloadUrl))
            {
                MessageBox.Show("No download URL available for this map.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(workshopFolder))
            {
                MessageBox.Show("Load a workshop folder first.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnDownload.Enabled = false;
            btnStop.Enabled = true;
            btnInstallAcf.Enabled = false;
            progressBar.Value = 0;
            lblProgress.Text = "Downloading...";

            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            // Always run the delegate so its finally block can restore the UI.
            _ = Task.Run(async () =>
            {
                string tempZip = null;
                string stagingPath = null;
                string backupPath = null;
                string destPath = null;

                try
                {
                    tempZip = Path.GetTempFileName();

                    using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
                    using (var response = await client.GetAsync(
                        map.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;

                        using (var stream = await response.Content.ReadAsStreamAsync(token))
                        using (var fileStream = File.Create(tempZip))
                        {
                            byte[] buffer = new byte[8192];
                            long totalRead = 0;
                            int bytesRead;

                            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) != 0)
                            {
                                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                                totalRead += bytesRead;

                                if (totalBytes.HasValue && totalBytes.Value > 0)
                                {
                                    int pct = Math.Clamp((int)(totalRead * 100 / totalBytes.Value), 0, 100);
                                    this.Invoke(new Action(() =>
                                    {
                                        progressBar.Value = pct;
                                        lblProgress.Text = $"Downloading... {pct}% ({totalRead / 1048576.0:F1} MB)";
                                    }));
                                }
                            }
                        }
                    }

                    token.ThrowIfCancellationRequested();
                    this.Invoke(new Action(() => lblProgress.Text = "Extracting and validating..."));

                    string workshopRoot = Path.Combine(workshopFolder, "content", "730");
                    Directory.CreateDirectory(workshopRoot);
                    destPath = Path.Combine(workshopRoot, map.WorkshopId.ToString());
                    stagingPath = Path.Combine(workshopRoot, $"{map.WorkshopId}.staging-{Guid.NewGuid():N}");
                    Directory.CreateDirectory(stagingPath);

                    // Extract into a staging directory. Do not touch the current map
                    // until the downloaded archive has been extracted successfully.
                    ZipFile.ExtractToDirectory(tempZip, stagingPath);

                    string innerDir = Path.Combine(stagingPath, map.WorkshopId.ToString());
                    if (Directory.Exists(innerDir))
                    {
                        foreach (string entry in Directory.GetFileSystemEntries(innerDir))
                        {
                            string stagedEntry = Path.Combine(stagingPath, Path.GetFileName(entry));
                            if (Directory.Exists(entry))
                                Directory.Move(entry, stagedEntry);
                            else
                                File.Move(entry, stagedEntry);
                        }

                        Directory.Delete(innerDir);
                    }

                    token.ThrowIfCancellationRequested();

                    // Atomic directory rename on the same volume keeps the old map
                    // recoverable if promotion of the staged directory fails.
                    if (Directory.Exists(destPath))
                    {
                        backupPath = Path.Combine(workshopRoot, $"{map.WorkshopId}.backup-{Guid.NewGuid():N}");
                        Directory.Move(destPath, backupPath);
                    }

                    try
                    {
                        Directory.Move(stagingPath, destPath);
                        stagingPath = null;
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(backupPath) &&
                            Directory.Exists(backupPath) &&
                            !Directory.Exists(destPath))
                        {
                            Directory.Move(backupPath, destPath);
                            backupPath = null;
                        }
                        throw;
                    }

                    if (!string.IsNullOrEmpty(backupPath) && Directory.Exists(backupPath))
                    {
                        try { Directory.Delete(backupPath, recursive: true); }
                        catch { /* A valid installation is already in place; leave backup for manual cleanup. */ }
                        backupPath = null;
                    }

                    this.Invoke(new Action(() =>
                    {
                        map.IsInstalled = true;
                        map.LocalPath = destPath;
                        PopulateListView();
                        progressBar.Style = ProgressBarStyle.Continuous;
                        progressBar.Value = 100;
                        lblProgress.Text = "Download & install completed!";
                        MessageBox.Show(
                            "Map downloaded and installed successfully.\nUse 'Install ACF' to update the ACF file separately.",
                            "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }));
                }
                catch (OperationCanceledException)
                {
                    this.Invoke(new Action(() => lblProgress.Text = "Download cancelled."));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                        MessageBox.Show($"Download/install failed: {ex.Message}\nThe previous map is preserved unless installation had already completed.",
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
                finally
                {
                    try { if (!string.IsNullOrEmpty(tempZip) && File.Exists(tempZip)) File.Delete(tempZip); }
                    catch { }

                    try
                    {
                        if (!string.IsNullOrEmpty(stagingPath) && Directory.Exists(stagingPath))
                            Directory.Delete(stagingPath, recursive: true);
                    }
                    catch { }

                    // If promotion failed after the old folder was moved, restore it.
                    try
                    {
                        if (!string.IsNullOrEmpty(backupPath) &&
                            Directory.Exists(backupPath) &&
                            !string.IsNullOrEmpty(destPath) &&
                            !Directory.Exists(destPath))
                        {
                            Directory.Move(backupPath, destPath);
                        }
                    }
                    catch { }

                    try
                    {
                        this.Invoke(new Action(() =>
                        {
                            btnDownload.Enabled = true;
                            btnStop.Enabled = false;
                            btnInstallAcf.Enabled = true;
                            progressBar.Style = ProgressBarStyle.Continuous;
                        }));
                    }
                    catch { /* The form may already be closing. */ }
                }
            });
        }

        private void BtnStop_Click(object sender, EventArgs e) => cts?.Cancel();

        protected override void OnFormClosing(FormClosingEventArgs e) { cts?.Cancel(); base.OnFormClosing(e); }
    }

    // ========== کلاس‌های داده ==========
    public class WorkshopMapItem
    {
        public long WorkshopId { get; set; }
        public long Size { get; set; }
        public long TimeUpdated { get; set; }
        public string Manifest { get; set; } = "";
        public string MapName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string LocalPath { get; set; } = "";
        public bool IsInstalled { get; set; }
    }

    public class AcfConfig
    {
        [System.Text.Json.Serialization.JsonPropertyName("acfUrl")]
        public string AcfUrl { get; set; } = "";
    }
}