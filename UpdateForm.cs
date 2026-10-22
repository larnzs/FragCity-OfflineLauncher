using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CSserver_browser
{
    public partial class UpdateForm : Form
    {
        private AppSettings appSettings;
        private ProgressBar? progressBar;   // یا progressBar = null!;
        private Button? btnCheck;
        private Label? lblStatus;

        public UpdateForm(AppSettings settings)
        {
            appSettings = settings;
            InitializeFormComponents();
            ApplyVisualStyle();
        }

        private void InitializeFormComponents()
        {
            this.Text = "Check for Updates";
            this.Size = new Size(450, 200);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };

            lblStatus = new Label
            {
                Text = "Click 'Check' to look for updates.",
                AutoSize = false,
                Size = new Size(400, 40),
                Location = new Point(10, 10)
            };
            mainPanel.Controls.Add(lblStatus);

            btnCheck = new Button
            {
                Text = "Check Now",
                Size = new Size(130, 32),
                Location = new Point(145, 70)
            };
            btnCheck.Click += async (s, e) => await PerformUpdateCheck();
            mainPanel.Controls.Add(btnCheck);

            progressBar = new ProgressBar
            {
                Size = new Size(300, 20),
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Location = new Point(10, 120)
            };
            mainPanel.Controls.Add(progressBar);

            this.Controls.Add(mainPanel);
        }

        private void ApplyVisualStyle()
        {
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            if (this.Controls[0] is Panel mainPanel)
            {
                mainPanel.BackColor = Color.FromArgb(30, 30, 30);
                mainPanel.ForeColor = Color.White;
                mainPanel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            }

            lblStatus.ForeColor = Color.White;
            lblStatus.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            btnCheck.FlatStyle = FlatStyle.Flat;
            btnCheck.BackColor = Color.FromArgb(50, 50, 50);
            btnCheck.ForeColor = Color.White;
            btnCheck.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnCheck.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        }

        private async Task PerformUpdateCheck()
        {
            string checkUrl = appSettings.UpdateCheckUrl;
            if (string.IsNullOrEmpty(checkUrl))
            {
                MessageBox.Show("Update check URL is not configured.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnCheck.Enabled = false;
            progressBar.Visible = true;
            lblStatus.Text = "Checking for updates...";

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
                {
                    string json = await client.GetStringAsync(checkUrl);
                    var latestInfo = JsonSerializer.Deserialize<LatestVersionInfo>(json);
                    if (latestInfo == null || string.IsNullOrEmpty(latestInfo.Version))
                    {
                        lblStatus.Text = "Invalid server response.";
                        return;
                    }

                    Version current = new Version(appSettings.CurrentVersion);
                    Version latest = new Version(latestInfo.Version);
                    if (latest <= current)
                    {
                        lblStatus.Text = "You are up-to-date!";
                        MessageBox.Show("You already have the latest version.", "No Update", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (string.IsNullOrEmpty(latestInfo.DownloadUrl))
                    {
                        lblStatus.Text = "Download URL missing.";
                        return;
                    }

                    var result = MessageBox.Show($"A new version ({latestInfo.Version}) is available.\n\nDownload and extract now?",
                        "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result != DialogResult.Yes)
                    {
                        lblStatus.Text = "Update cancelled.";
                        return;
                    }

                    lblStatus.Text = "Downloading update...";
                    string tempZip = Path.GetTempFileName();
                    using (var dlClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
                    {
                        var response = await dlClient.GetAsync(latestInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = File.Create(tempZip))
                        {
                            await stream.CopyToAsync(fileStream);
                        }
                    }

                    lblStatus.Text = "Extracting update...";
                    string updateFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdatePackage");
                    if (Directory.Exists(updateFolder))
                        Directory.Delete(updateFolder, true);
                    ZipFile.ExtractToDirectory(tempZip, updateFolder);
                    File.Delete(tempZip);

                    appSettings.CurrentVersion = latestInfo.Version;
                    SettingsManager.Save(appSettings);

                    lblStatus.Text = "Update extracted successfully.";
                    MessageBox.Show($"Update extracted to:\n{updateFolder}\n\nClose the program and replace your files manually, then restart.",
                        "Update Ready", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    this.Close();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Update failed.";
                MessageBox.Show($"Update process failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnCheck.Enabled = true;
                progressBar.Visible = false;
            }
        }

        private class LatestVersionInfo
        {
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
        }
    }
}