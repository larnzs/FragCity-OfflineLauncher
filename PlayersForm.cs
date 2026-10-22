using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CSserver_browser
{
    public partial class PlayersForm : Form
    {
        // ----------- Theme Colors -----------
        private readonly Color DarkBg = Color.FromArgb(22, 22, 22);
        private readonly Color RowEven = Color.FromArgb(35, 35, 35);
        private readonly Color RowOdd = Color.FromArgb(45, 45, 45);
        private readonly Color TextColor = Color.White;
        private readonly Color SubTextColor = Color.Gray;

        private ListView lvPlayers;
        private Panel titleBar;
        private Label lblTitle;
        private Button btnClose;
        private Panel headerBanner;
        private Label lblServerName;
        private Panel bottomBar;
        private Label lblIp;
        private Button btnJoin;

        private ServerConnectionData _serverData;
        private List<PlayerInfo> _players;

        public PlayersForm(List<PlayerInfo> players, ServerConnectionData serverData)
        {
            _players = players ?? new List<PlayerInfo>();
            _serverData = serverData;

            InitializeForm();
            SetupTitleBar();
            SetupHeaderBanner();
            SetupListView();
            SetupBottomBar();
            BindData();

            // جابجایی فرم با کشیدن نوار عنوان
            titleBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
            lblTitle.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
        }

        private void InitializeForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(730, 520);
            this.BackColor = DarkBg;
            this.Padding = new Padding(1); // برای خط دور فرم
            this.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(60, 60, 60), ButtonBorderStyle.Solid);
            };
            this.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
            this.ForeColor = TextColor;
        }

        // ===== نوار عنوان (بدون نام سرور، فقط X) =====
        private void SetupTitleBar()
        {
            titleBar = new Panel
            {
                Height = 32,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(35, 35, 35)
            };

            lblTitle = new Label
            {
                Text = "Players",
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Location = new Point(10, 5),
                Size = new Size(this.Width - 60, 22),
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnClose = new Button
            {
                Text = "✕",
                FlatStyle = FlatStyle.Flat,
                ForeColor = TextColor,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Size = new Size(30, 30),
                Location = new Point(this.Width - 40, 1),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 50, 50);
            btnClose.Click += (s, e) => this.Close();

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            this.Controls.Add(titleBar);
        }

        // ===== هدر نارنجی با نام سرور =====
        private void SetupHeaderBanner()
        {
            headerBanner = new Panel
            {
                Height = 55,
                Dock = DockStyle.Top,
                Padding = new Padding(12, 5, 12, 5)
            };
            headerBanner.Paint += (s, e) =>
            {
                Rectangle rect = headerBanner.ClientRectangle;
                using (var brush = new LinearGradientBrush(rect,
                    Color.FromArgb(255, 180, 0), Color.FromArgb(255, 120, 0),
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, rect);
                }
            };

            lblServerName = new Label
            {
                Text = _serverData?.Name ?? "Server",
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            headerBanner.Controls.Add(lblServerName);
            this.Controls.Add(headerBanner);
        }

        // ===== لیست بازیکنان =====
        private void SetupListView()
        {
            lvPlayers = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                OwnerDraw = true,
                FullRowSelect = true,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None,
                BackColor = DarkBg,
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                GridLines = false
            };

            lvPlayers.Columns.Add("#", 50, HorizontalAlignment.Center);
            lvPlayers.Columns.Add("Player", 250);
            lvPlayers.Columns.Add("Score", 90);
            lvPlayers.Columns.Add("Time", 120);

            lvPlayers.DrawColumnHeader += (s, e) =>
            {
                using (var brush = new SolidBrush(Color.FromArgb(50, 50, 50)))
                    e.Graphics.FillRectangle(brush, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, new Font("Segoe UI", 10f, FontStyle.Bold),
                    e.Bounds, Color.Orange, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.Graphics.DrawLine(Pens.Gray, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
            };

            lvPlayers.DrawSubItem += (s, e) =>
            {
                bool selected = e.Item.Selected;
                Color bgColor = selected ? Color.FromArgb(255, 160, 0) : (e.Item.Index % 2 == 0 ? RowEven : RowOdd);
                using (var brush = new SolidBrush(bgColor))
                    e.Graphics.FillRectangle(brush, e.Bounds);

                Color textColor = selected ? Color.Black : (e.ColumnIndex == 1 ? TextColor : SubTextColor);
                TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
                if (e.ColumnIndex == 0) flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.Item.Font, e.Bounds, textColor, flags);
            };

            this.Controls.Add(lvPlayers);
        }

        // ===== پنل پایین (IP + دکمه Join) =====
        private void SetupBottomBar()
        {
            bottomBar = new Panel
            {
                Height = 45,
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(12, 5, 12, 5)
            };

            string ipText = _serverData != null ? $"{_serverData.Ip}:{_serverData.Port}" : "Unknown";
            lblIp = new Label
            {
                Text = $"IP {ipText}",
                ForeColor = TextColor,
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                Location = new Point(12, 10),
                Size = new Size(300, 23),
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnJoin = new Button
            {
                Text = "Join the server",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.Black,
                BackColor = Color.FromArgb(255, 180, 0),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Size = new Size(140, 28),
                Location = new Point(this.Width - 160, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            btnJoin.FlatAppearance.BorderSize = 0;
            btnJoin.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 200, 50);
            btnJoin.Click += (s, e) =>
            {
                var mainForm = Application.OpenForms["Form1"] as Form1;
                if (mainForm != null && _serverData != null)
                {
                    this.Close();
                    mainForm.BeginInvoke(new Action(async () => await mainForm.LaunchProxyForServer(_serverData)));
                }
                else
                {
                    Clipboard.SetText(ipText);
                    MessageBox.Show("IP copied to clipboard. Launch CS2 and connect manually.",
                        "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            bottomBar.Controls.Add(lblIp);
            bottomBar.Controls.Add(btnJoin);
            this.Controls.Add(bottomBar);
        }

        private void BindData()
        {
            lvPlayers.BeginUpdate();
            lvPlayers.Items.Clear();

            if (_players.Count == 0)
            {
                var emptyItem = new ListViewItem("");
                emptyItem.SubItems.Add("No players online");
                emptyItem.SubItems.Add("-");
                emptyItem.SubItems.Add("-");
                lvPlayers.Items.Add(emptyItem);
            }
            else
            {
                foreach (var p in _players)
                {
                    var item = new ListViewItem(p.Index.ToString());
                    item.SubItems.Add(p.Name ?? "Unknown");
                    item.SubItems.Add(p.Score.ToString());
                    item.SubItems.Add(FormatDuration(p.Duration));
                    lvPlayers.Items.Add(item);
                }
            }
            lvPlayers.EndUpdate();
        }

        private string FormatDuration(float seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return ts.ToString(@"hh\:mm\:ss");
        }

        // Win32 برای جابجایی پنجره بدون حاشیه
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
    }
}