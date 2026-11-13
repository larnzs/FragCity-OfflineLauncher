#nullable disable

namespace CSserver_browser
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            btnRefresh = new Button();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel_ServerCount = new ToolStripStatusLabel();
            toolStripStatusLabel_Message = new ToolStripStatusLabel();
            btnConnect = new Button();
            label1 = new Label();
            txtServerIp = new TextBox();
            btnAddServer = new Button();
            btnRemoveSelected = new Button();
            groupBox1 = new GroupBox();
            label2 = new Label();
            numGameQueryPort = new NumericUpDown();
            label3 = new Label();
            numProxyPort = new NumericUpDown();
            label4 = new Label();
            numLocalPort = new NumericUpDown();
            groupBox2 = new GroupBox();
            txtServerPublicIp = new TextBox();
            label12 = new Label();
            label13 = new Label();
            numServerInternalPort = new NumericUpDown();
            label14 = new Label();
            numServerClientPort = new NumericUpDown();
            btnStartServer = new Button();
            mapPicture = new PictureBox();
            label5 = new Label();
            lblServerNameValue = new Label();
            lblAddressValue = new Label();
            lblPlayersValue = new Label();
            lblMapValue = new Label();
            lblPingValue = new Label();
            lblGameModeValue = new Label();
            lblTagsValue = new Label();
            label6 = new Label();
            label7 = new Label();
            label8 = new Label();
            label9 = new Label();
            label10 = new Label();
            label11 = new Label();
            infoPanel1 = new GroupBox();
            listViewServers = new ListView();
            colShield = new ColumnHeader();
            colLock = new ColumnHeader();
            colServer = new ColumnHeader();
            coAddress = new ColumnHeader();
            coPlayers = new ColumnHeader();
            coMap = new ColumnHeader();
            coPing = new ColumnHeader();
            colTick = new ColumnHeader();
            colTags = new ColumnHeader();
            listViewFavorites = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            columnHeader3 = new ColumnHeader();
            columnHeader4 = new ColumnHeader();
            columnHeader5 = new ColumnHeader();
            columnHeader6 = new ColumnHeader();
            columnHeader7 = new ColumnHeader();
            columnHeader8 = new ColumnHeader();
            columnHeader9 = new ColumnHeader();
            listViewHistory = new ListView();
            columnHeader10 = new ColumnHeader();
            columnHeader11 = new ColumnHeader();
            columnHeader12 = new ColumnHeader();
            columnHeader13 = new ColumnHeader();
            columnHeader14 = new ColumnHeader();
            columnHeader15 = new ColumnHeader();
            columnHeader16 = new ColumnHeader();
            columnHeader17 = new ColumnHeader();
            columnHeader18 = new ColumnHeader();
            listViewPersonal = new ListView();
            columnHeader19 = new ColumnHeader();
            columnHeader20 = new ColumnHeader();
            columnHeader21 = new ColumnHeader();
            columnHeader22 = new ColumnHeader();
            columnHeader23 = new ColumnHeader();
            columnHeader24 = new ColumnHeader();
            columnHeader25 = new ColumnHeader();
            columnHeader26 = new ColumnHeader();
            columnHeader27 = new ColumnHeader();
            btnRemovePersonal = new Button();
            btnCheckUpdates = new Button();
            btnAddFavorite = new Button();
            tabControl1 = new TabControl();
            tabInternet = new TabPage();
            tabFavorites = new TabPage();
            tabHistory = new TabPage();
            tabPersonal = new TabPage();
            Search = new Label();
            svSearchBox = new TextBox();
            btnPlayers = new Button();
            btnGetMaps = new Button();
            txtConsole = new RichTextBox();
            statusStrip1.SuspendLayout();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numGameQueryPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numProxyPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numLocalPort).BeginInit();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numServerInternalPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numServerClientPort).BeginInit();
            ((System.ComponentModel.ISupportInitialize)mapPicture).BeginInit();
            infoPanel1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabInternet.SuspendLayout();
            tabFavorites.SuspendLayout();
            tabHistory.SuspendLayout();
            tabPersonal.SuspendLayout();
            SuspendLayout();
            // 
            // btnRefresh
            // 
            btnRefresh.Location = new Point(858, 552);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(103, 23);
            btnRefresh.TabIndex = 1;
            btnRefresh.Text = "Refresh Servers";
            btnRefresh.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel_ServerCount, toolStripStatusLabel_Message });
            statusStrip1.Location = new Point(0, 605);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1085, 22);
            statusStrip1.SizingGrip = false;
            statusStrip1.TabIndex = 2;
            // 
            // toolStripStatusLabel_ServerCount
            // 
            toolStripStatusLabel_ServerCount.Name = "toolStripStatusLabel_ServerCount";
            toolStripStatusLabel_ServerCount.Size = new Size(0, 17);
            // 
            // toolStripStatusLabel_Message
            // 
            toolStripStatusLabel_Message.Name = "toolStripStatusLabel_Message";
            toolStripStatusLabel_Message.Size = new Size(0, 17);
            // 
            // btnConnect
            // 
            btnConnect.Enabled = false;
            btnConnect.Location = new Point(967, 552);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(103, 23);
            btnConnect.TabIndex = 3;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.ForeColor = Color.Gainsboro;
            label1.Location = new Point(6, 23);
            label1.Name = "label1";
            label1.Size = new Size(58, 15);
            label1.TabIndex = 4;
            label1.Text = "Server IP :";
            // 
            // txtServerIp
            // 
            txtServerIp.Location = new Point(90, 20);
            txtServerIp.Name = "txtServerIp";
            txtServerIp.Size = new Size(150, 23);
            txtServerIp.TabIndex = 5;
            // 
            // btnAddServer
            // 
            btnAddServer.ForeColor = Color.White;
            btnAddServer.Location = new Point(8, 137);
            btnAddServer.Name = "btnAddServer";
            btnAddServer.Size = new Size(232, 23);
            btnAddServer.TabIndex = 6;
            btnAddServer.Text = "Add Server";
            btnAddServer.UseVisualStyleBackColor = true;
            // 
            // btnRemoveSelected
            // 
            btnRemoveSelected.ForeColor = SystemColors.Window;
            btnRemoveSelected.Location = new Point(531, 552);
            btnRemoveSelected.Name = "btnRemoveSelected";
            btnRemoveSelected.Size = new Size(103, 23);
            btnRemoveSelected.TabIndex = 7;
            btnRemoveSelected.Text = "Remove Server";
            btnRemoveSelected.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            groupBox1.BackColor = Color.Transparent;
            groupBox1.Controls.Add(txtServerIp);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(numGameQueryPort);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(numProxyPort);
            groupBox1.Controls.Add(label4);
            groupBox1.Controls.Add(numLocalPort);
            groupBox1.Controls.Add(btnAddServer);
            groupBox1.ForeColor = SystemColors.Window;
            groupBox1.Location = new Point(572, 60);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(246, 168);
            groupBox1.TabIndex = 8;
            groupBox1.TabStop = false;
            groupBox1.Text = "Add Server";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.ForeColor = Color.Gainsboro;
            label2.Location = new Point(6, 51);
            label2.Name = "label2";
            label2.Size = new Size(105, 15);
            label2.TabIndex = 16;
            label2.Text = "Server Query Port :";
            // 
            // numGameQueryPort
            // 
            numGameQueryPort.Location = new Point(167, 49);
            numGameQueryPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numGameQueryPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numGameQueryPort.Name = "numGameQueryPort";
            numGameQueryPort.Size = new Size(73, 23);
            numGameQueryPort.TabIndex = 15;
            numGameQueryPort.Value = new decimal(new int[] { 27015, 0, 0, 0 });
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.ForeColor = Color.Gainsboro;
            label3.Location = new Point(6, 80);
            label3.Name = "label3";
            label3.Size = new Size(86, 15);
            label3.TabIndex = 11;
            label3.Text = "Listening Port :";
            // 
            // numProxyPort
            // 
            numProxyPort.Location = new Point(167, 78);
            numProxyPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numProxyPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numProxyPort.Name = "numProxyPort";
            numProxyPort.Size = new Size(73, 23);
            numProxyPort.TabIndex = 12;
            numProxyPort.Value = new decimal(new int[] { 27017, 0, 0, 0 });
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.ForeColor = Color.Gainsboro;
            label4.Location = new Point(8, 108);
            label4.Name = "label4";
            label4.Size = new Size(98, 15);
            label4.TabIndex = 13;
            label4.Text = "Forwarding Port :";
            // 
            // numLocalPort
            // 
            numLocalPort.Location = new Point(167, 106);
            numLocalPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numLocalPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numLocalPort.Name = "numLocalPort";
            numLocalPort.Size = new Size(73, 23);
            numLocalPort.TabIndex = 14;
            numLocalPort.Value = new decimal(new int[] { 27016, 0, 0, 0 });
            // 
            // groupBox2
            // 
            groupBox2.BackColor = Color.Transparent;
            groupBox2.Controls.Add(txtServerPublicIp);
            groupBox2.Controls.Add(label12);
            groupBox2.Controls.Add(label13);
            groupBox2.Controls.Add(numServerInternalPort);
            groupBox2.Controls.Add(label14);
            groupBox2.Controls.Add(numServerClientPort);
            groupBox2.Controls.Add(btnStartServer);
            groupBox2.ForeColor = Color.Red;
            groupBox2.Location = new Point(825, 60);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(246, 168);
            groupBox2.TabIndex = 17;
            groupBox2.TabStop = false;
            groupBox2.Text = "Host Server";
            // 
            // txtServerPublicIp
            // 
            txtServerPublicIp.Location = new Point(90, 22);
            txtServerPublicIp.Name = "txtServerPublicIp";
            txtServerPublicIp.Size = new Size(150, 23);
            txtServerPublicIp.TabIndex = 5;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.ForeColor = Color.Gainsboro;
            label12.Location = new Point(6, 53);
            label12.Name = "label12";
            label12.Size = new Size(78, 15);
            label12.TabIndex = 16;
            label12.Text = "Internal Port :";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.ForeColor = Color.Gainsboro;
            label13.Location = new Point(6, 25);
            label13.Name = "label13";
            label13.Size = new Size(59, 15);
            label13.TabIndex = 4;
            label13.Text = "Public IP :";
            // 
            // numServerInternalPort
            // 
            numServerInternalPort.Location = new Point(167, 51);
            numServerInternalPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numServerInternalPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numServerInternalPort.Name = "numServerInternalPort";
            numServerInternalPort.Size = new Size(73, 23);
            numServerInternalPort.TabIndex = 15;
            numServerInternalPort.Value = new decimal(new int[] { 27015, 0, 0, 0 });
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.ForeColor = Color.Gainsboro;
            label14.Location = new Point(6, 82);
            label14.Name = "label14";
            label14.Size = new Size(80, 15);
            label14.TabIndex = 11;
            label14.Text = "External Port :";
            // 
            // numServerClientPort
            // 
            numServerClientPort.Location = new Point(167, 80);
            numServerClientPort.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            numServerClientPort.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            numServerClientPort.Name = "numServerClientPort";
            numServerClientPort.Size = new Size(73, 23);
            numServerClientPort.TabIndex = 12;
            numServerClientPort.Value = new decimal(new int[] { 27016, 0, 0, 0 });
            // 
            // btnStartServer
            // 
            btnStartServer.ForeColor = Color.White;
            btnStartServer.Location = new Point(6, 137);
            btnStartServer.Name = "btnStartServer";
            btnStartServer.Size = new Size(234, 23);
            btnStartServer.TabIndex = 6;
            btnStartServer.Text = "Start Server";
            btnStartServer.UseVisualStyleBackColor = true;
            btnStartServer.Click += btnStartServer_Click;
            // 
            // mapPicture
            // 
            mapPicture.BackColor = Color.Transparent;
            mapPicture.Location = new Point(295, 8);
            mapPicture.Name = "mapPicture";
            mapPicture.Size = new Size(251, 160);
            mapPicture.SizeMode = PictureBoxSizeMode.Zoom;
            mapPicture.TabIndex = 0;
            mapPicture.TabStop = false;
            // 
            // label5
            // 
            label5.ForeColor = Color.Gainsboro;
            label5.Location = new Point(12, 23);
            label5.Name = "label5";
            label5.Size = new Size(51, 20);
            label5.TabIndex = 10;
            label5.Text = "Server :";
            // 
            // lblServerNameValue
            // 
            lblServerNameValue.Location = new Point(69, 23);
            lblServerNameValue.Name = "lblServerNameValue";
            lblServerNameValue.Size = new Size(184, 20);
            lblServerNameValue.TabIndex = 10;
            lblServerNameValue.Text = "-";
            // 
            // lblAddressValue
            // 
            lblAddressValue.Location = new Point(69, 43);
            lblAddressValue.Name = "lblAddressValue";
            lblAddressValue.Size = new Size(184, 20);
            lblAddressValue.TabIndex = 11;
            lblAddressValue.Text = "-";
            // 
            // lblPlayersValue
            // 
            lblPlayersValue.Location = new Point(69, 63);
            lblPlayersValue.Name = "lblPlayersValue";
            lblPlayersValue.Size = new Size(184, 20);
            lblPlayersValue.TabIndex = 12;
            lblPlayersValue.Text = "-";
            // 
            // lblMapValue
            // 
            lblMapValue.Location = new Point(69, 83);
            lblMapValue.Name = "lblMapValue";
            lblMapValue.Size = new Size(184, 20);
            lblMapValue.TabIndex = 13;
            lblMapValue.Text = "-";
            // 
            // lblPingValue
            // 
            lblPingValue.Location = new Point(69, 103);
            lblPingValue.Name = "lblPingValue";
            lblPingValue.Size = new Size(184, 20);
            lblPingValue.TabIndex = 14;
            lblPingValue.Text = "-";
            // 
            // lblGameModeValue
            // 
            lblGameModeValue.Location = new Point(69, 123);
            lblGameModeValue.Name = "lblGameModeValue";
            lblGameModeValue.Size = new Size(184, 20);
            lblGameModeValue.TabIndex = 15;
            lblGameModeValue.Text = "-";
            // 
            // lblTagsValue
            // 
            lblTagsValue.Location = new Point(69, 141);
            lblTagsValue.Name = "lblTagsValue";
            lblTagsValue.Size = new Size(184, 20);
            lblTagsValue.TabIndex = 16;
            lblTagsValue.Text = "-";
            // 
            // label6
            // 
            label6.ForeColor = Color.Gainsboro;
            label6.Location = new Point(12, 43);
            label6.Name = "label6";
            label6.Size = new Size(51, 20);
            label6.TabIndex = 17;
            label6.Text = "Address :";
            // 
            // label7
            // 
            label7.ForeColor = Color.Gainsboro;
            label7.Location = new Point(12, 63);
            label7.Name = "label7";
            label7.Size = new Size(51, 20);
            label7.TabIndex = 18;
            label7.Text = "Players :";
            // 
            // label8
            // 
            label8.ForeColor = Color.Gainsboro;
            label8.Location = new Point(12, 83);
            label8.Name = "label8";
            label8.Size = new Size(51, 20);
            label8.TabIndex = 19;
            label8.Text = "Map :";
            // 
            // label9
            // 
            label9.ForeColor = Color.Gainsboro;
            label9.Location = new Point(12, 103);
            label9.Name = "label9";
            label9.Size = new Size(51, 20);
            label9.TabIndex = 20;
            label9.Text = "Ping :";
            // 
            // label10
            // 
            label10.ForeColor = Color.Gainsboro;
            label10.Location = new Point(12, 122);
            label10.Name = "label10";
            label10.Size = new Size(51, 21);
            label10.TabIndex = 21;
            label10.Text = "Mode :";
            // 
            // label11
            // 
            label11.ForeColor = Color.Gainsboro;
            label11.Location = new Point(12, 141);
            label11.Name = "label11";
            label11.Size = new Size(51, 20);
            label11.TabIndex = 22;
            label11.Text = "Tags :";
            // 
            // infoPanel1
            // 
            infoPanel1.BackColor = Color.Transparent;
            infoPanel1.Controls.Add(label11);
            infoPanel1.Controls.Add(label5);
            infoPanel1.Controls.Add(label10);
            infoPanel1.Controls.Add(mapPicture);
            infoPanel1.Controls.Add(label9);
            infoPanel1.Controls.Add(lblServerNameValue);
            infoPanel1.Controls.Add(label8);
            infoPanel1.Controls.Add(lblAddressValue);
            infoPanel1.Controls.Add(label7);
            infoPanel1.Controls.Add(lblPlayersValue);
            infoPanel1.Controls.Add(label6);
            infoPanel1.Controls.Add(lblMapValue);
            infoPanel1.Controls.Add(lblTagsValue);
            infoPanel1.Controls.Add(lblPingValue);
            infoPanel1.Controls.Add(lblGameModeValue);
            infoPanel1.ForeColor = SystemColors.Window;
            infoPanel1.Location = new Point(12, 60);
            infoPanel1.Name = "infoPanel1";
            infoPanel1.Size = new Size(554, 168);
            infoPanel1.TabIndex = 17;
            infoPanel1.TabStop = false;
            infoPanel1.Text = "Server Info";
            // 
            // listViewServers
            // 
            listViewServers.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            listViewServers.Columns.AddRange(new ColumnHeader[] { colShield, colLock, colServer, coAddress, coPlayers, coMap, coPing, colTick, colTags });
            listViewServers.FullRowSelect = true;
            listViewServers.Location = new Point(0, 0);
            listViewServers.MultiSelect = false;
            listViewServers.Name = "listViewServers";
            listViewServers.Size = new Size(1050, 275);
            listViewServers.TabIndex = 0;
            listViewServers.UseCompatibleStateImageBehavior = false;
            listViewServers.View = View.Details;
            // 
            // colShield
            // 
            colShield.Text = "🛡";
            colShield.Width = 25;
            // 
            // colLock
            // 
            colLock.Text = "🔒";
            colLock.TextAlign = HorizontalAlignment.Center;
            colLock.Width = 25;
            // 
            // colServer
            // 
            colServer.Text = "Server";
            colServer.Width = 250;
            // 
            // coAddress
            // 
            coAddress.Tag = "";
            coAddress.Text = "Address";
            coAddress.Width = 130;
            // 
            // coPlayers
            // 
            coPlayers.Text = "Players";
            // 
            // coMap
            // 
            coMap.Text = "Map";
            coMap.Width = 100;
            // 
            // coPing
            // 
            coPing.Text = "Ping";
            coPing.Width = 50;
            // 
            // colTick
            // 
            colTick.Text = "Tickrate";
            // 
            // colTags
            // 
            colTags.Text = "Tags";
            colTags.Width = 346;
            // 
            // listViewFavorites
            // 
            listViewFavorites.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            listViewFavorites.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3, columnHeader4, columnHeader5, columnHeader6, columnHeader7, columnHeader8, columnHeader9 });
            listViewFavorites.FullRowSelect = true;
            listViewFavorites.Location = new Point(0, 0);
            listViewFavorites.MultiSelect = false;
            listViewFavorites.Name = "listViewFavorites";
            listViewFavorites.Size = new Size(1050, 276);
            listViewFavorites.TabIndex = 1;
            listViewFavorites.UseCompatibleStateImageBehavior = false;
            listViewFavorites.View = View.Details;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "🛡";
            columnHeader1.Width = 25;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "🔒";
            columnHeader2.TextAlign = HorizontalAlignment.Center;
            columnHeader2.Width = 25;
            // 
            // columnHeader3
            // 
            columnHeader3.Text = "Server";
            columnHeader3.Width = 250;
            // 
            // columnHeader4
            // 
            columnHeader4.Tag = "";
            columnHeader4.Text = "Address";
            columnHeader4.Width = 130;
            // 
            // columnHeader5
            // 
            columnHeader5.Text = "Players";
            // 
            // columnHeader6
            // 
            columnHeader6.Text = "Map";
            columnHeader6.Width = 100;
            // 
            // columnHeader7
            // 
            columnHeader7.Text = "Ping";
            columnHeader7.Width = 50;
            // 
            // columnHeader8
            // 
            columnHeader8.Text = "Tickrate";
            // 
            // columnHeader9
            // 
            columnHeader9.Text = "Tags";
            columnHeader9.Width = 346;
            // 
            // listViewHistory
            // 
            listViewHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            listViewHistory.Columns.AddRange(new ColumnHeader[] { columnHeader10, columnHeader11, columnHeader12, columnHeader13, columnHeader14, columnHeader15, columnHeader16, columnHeader17, columnHeader18 });
            listViewHistory.FullRowSelect = true;
            listViewHistory.Location = new Point(0, 0);
            listViewHistory.MultiSelect = false;
            listViewHistory.Name = "listViewHistory";
            listViewHistory.Size = new Size(1050, 276);
            listViewHistory.TabIndex = 21;
            listViewHistory.UseCompatibleStateImageBehavior = false;
            listViewHistory.View = View.Details;
            // 
            // columnHeader10
            // 
            columnHeader10.Text = "🛡";
            columnHeader10.Width = 25;
            // 
            // columnHeader11
            // 
            columnHeader11.Text = "🔒";
            columnHeader11.TextAlign = HorizontalAlignment.Center;
            columnHeader11.Width = 25;
            // 
            // columnHeader12
            // 
            columnHeader12.Text = "Server";
            columnHeader12.Width = 250;
            // 
            // columnHeader13
            // 
            columnHeader13.Tag = "";
            columnHeader13.Text = "Address";
            columnHeader13.Width = 130;
            // 
            // columnHeader14
            // 
            columnHeader14.Text = "Players";
            // 
            // columnHeader15
            // 
            columnHeader15.Text = "Map";
            columnHeader15.Width = 100;
            // 
            // columnHeader16
            // 
            columnHeader16.Text = "Ping";
            columnHeader16.Width = 50;
            // 
            // columnHeader17
            // 
            columnHeader17.Text = "Tickrate";
            // 
            // columnHeader18
            // 
            columnHeader18.Text = "Tags";
            columnHeader18.Width = 200;
            // 
            // listViewPersonal
            // 
            listViewPersonal.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            listViewPersonal.Columns.AddRange(new ColumnHeader[] { columnHeader19, columnHeader20, columnHeader21, columnHeader22, columnHeader23, columnHeader24, columnHeader25, columnHeader26, columnHeader27 });
            listViewPersonal.FullRowSelect = true;
            listViewPersonal.Location = new Point(0, 0);
            listViewPersonal.MultiSelect = false;
            listViewPersonal.Name = "listViewPersonal";
            listViewPersonal.Size = new Size(1050, 276);
            listViewPersonal.TabIndex = 22;
            listViewPersonal.UseCompatibleStateImageBehavior = false;
            listViewPersonal.View = View.Details;
            // 
            // columnHeader19
            // 
            columnHeader19.Text = "🛡";
            columnHeader19.Width = 25;
            // 
            // columnHeader20
            // 
            columnHeader20.Text = "🔒";
            columnHeader20.TextAlign = HorizontalAlignment.Center;
            columnHeader20.Width = 25;
            // 
            // columnHeader21
            // 
            columnHeader21.Text = "Server";
            columnHeader21.Width = 250;
            // 
            // columnHeader22
            // 
            columnHeader22.Tag = "";
            columnHeader22.Text = "Address";
            columnHeader22.Width = 130;
            // 
            // columnHeader23
            // 
            columnHeader23.Text = "Players";
            // 
            // columnHeader24
            // 
            columnHeader24.Text = "Map";
            columnHeader24.Width = 100;
            // 
            // columnHeader25
            // 
            columnHeader25.Text = "Ping";
            columnHeader25.Width = 50;
            // 
            // columnHeader26
            // 
            columnHeader26.Text = "Tickrate";
            // 
            // columnHeader27
            // 
            columnHeader27.Text = "Tags";
            columnHeader27.Width = 346;
            // 
            // btnRemovePersonal
            // 
            btnRemovePersonal.ForeColor = Color.Red;
            btnRemovePersonal.Location = new Point(0, 1);
            btnRemovePersonal.Name = "btnRemovePersonal";
            btnRemovePersonal.Size = new Size(103, 23);
            btnRemovePersonal.TabIndex = 20;
            btnRemovePersonal.Text = "Remove Server";
            btnRemovePersonal.UseVisualStyleBackColor = true;
            btnRemovePersonal.Visible = false;
            // 
            // btnCheckUpdates
            // 
            btnCheckUpdates.Location = new Point(12, 552);
            btnCheckUpdates.Name = "btnCheckUpdates";
            btnCheckUpdates.Size = new Size(177, 23);
            btnCheckUpdates.TabIndex = 21;
            btnCheckUpdates.Text = "Check for updates";
            btnCheckUpdates.UseVisualStyleBackColor = true;
            // 
            // btnAddFavorite
            // 
            btnAddFavorite.Location = new Point(749, 552);
            btnAddFavorite.Name = "btnAddFavorite";
            btnAddFavorite.Size = new Size(103, 23);
            btnAddFavorite.TabIndex = 22;
            btnAddFavorite.Text = "Add to Favorites";
            btnAddFavorite.UseVisualStyleBackColor = true;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabInternet);
            tabControl1.Controls.Add(tabFavorites);
            tabControl1.Controls.Add(tabHistory);
            tabControl1.Controls.Add(tabPersonal);
            tabControl1.Location = new Point(12, 243);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1058, 303);
            tabControl1.TabIndex = 23;
            // 
            // tabInternet
            // 
            tabInternet.Controls.Add(listViewServers);
            tabInternet.Location = new Point(4, 24);
            tabInternet.Name = "tabInternet";
            tabInternet.Padding = new Padding(3);
            tabInternet.Size = new Size(1050, 275);
            tabInternet.TabIndex = 0;
            tabInternet.Text = "All";
            tabInternet.UseVisualStyleBackColor = true;
            // 
            // tabFavorites
            // 
            tabFavorites.Controls.Add(listViewFavorites);
            tabFavorites.Location = new Point(4, 24);
            tabFavorites.Name = "tabFavorites";
            tabFavorites.Padding = new Padding(3);
            tabFavorites.Size = new Size(1050, 275);
            tabFavorites.TabIndex = 1;
            tabFavorites.Text = "Favorites";
            tabFavorites.UseVisualStyleBackColor = true;
            // 
            // tabHistory
            // 
            tabHistory.Controls.Add(listViewHistory);
            tabHistory.Location = new Point(4, 24);
            tabHistory.Name = "tabHistory";
            tabHistory.Padding = new Padding(3);
            tabHistory.Size = new Size(1050, 275);
            tabHistory.TabIndex = 2;
            tabHistory.Text = "History";
            tabHistory.UseVisualStyleBackColor = true;
            // 
            // tabPersonal
            // 
            tabPersonal.Controls.Add(listViewPersonal);
            tabPersonal.Location = new Point(4, 24);
            tabPersonal.Name = "tabPersonal";
            tabPersonal.Padding = new Padding(3);
            tabPersonal.Size = new Size(1050, 275);
            tabPersonal.TabIndex = 3;
            tabPersonal.Text = "Added Servers";
            tabPersonal.UseVisualStyleBackColor = true;
            // 
            // Search
            // 
            Search.AutoSize = true;
            Search.Location = new Point(773, 238);
            Search.Name = "Search";
            Search.Size = new Size(42, 15);
            Search.TabIndex = 25;
            Search.Text = "Search";
            // 
            // svSearchBox
            // 
            svSearchBox.Location = new Point(825, 234);
            svSearchBox.Multiline = true;
            svSearchBox.Name = "svSearchBox";
            svSearchBox.Size = new Size(246, 24);
            svSearchBox.TabIndex = 26;
            // 
            // btnPlayers
            // 
            btnPlayers.Location = new Point(640, 552);
            btnPlayers.Name = "btnPlayers";
            btnPlayers.Size = new Size(103, 23);
            btnPlayers.TabIndex = 27;
            btnPlayers.Text = "Show Player list";
            btnPlayers.UseVisualStyleBackColor = true;
            btnPlayers.Click += btnPlayers_Click;
            // 
            // btnGetMaps
            // 
            btnGetMaps.Location = new Point(195, 552);
            btnGetMaps.Name = "btnGetMaps";
            btnGetMaps.Size = new Size(177, 23);
            btnGetMaps.TabIndex = 28;
            btnGetMaps.Text = "Download Workshop Maps";
            btnGetMaps.UseVisualStyleBackColor = true;
            btnGetMaps.Click += btnGetMaps_Click;
            // 
            // txtConsole
            // 
            txtConsole.Location = new Point(12, 30);
            txtConsole.Name = "txtConsole";
            txtConsole.Size = new Size(1059, 23);
            txtConsole.TabIndex = 29;
            txtConsole.Text = "";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Gray;
            ClientSize = new Size(1085, 627);
            Controls.Add(txtConsole);
            Controls.Add(btnGetMaps);
            Controls.Add(btnPlayers);
            Controls.Add(svSearchBox);
            Controls.Add(Search);
            Controls.Add(btnRemoveSelected);
            Controls.Add(tabControl1);
            Controls.Add(btnAddFavorite);
            Controls.Add(btnCheckUpdates);
            Controls.Add(btnRemovePersonal);
            Controls.Add(infoPanel1);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(btnConnect);
            Controls.Add(statusStrip1);
            Controls.Add(btnRefresh);
            ForeColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "Form1";
            Text = "FragCity";
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numGameQueryPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)numProxyPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)numLocalPort).EndInit();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numServerInternalPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)numServerClientPort).EndInit();
            ((System.ComponentModel.ISupportInitialize)mapPicture).EndInit();
            infoPanel1.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabInternet.ResumeLayout(false);
            tabFavorites.ResumeLayout(false);
            tabHistory.ResumeLayout(false);
            tabPersonal.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private Button btnRefresh;
        private StatusStrip statusStrip1;
        private Button btnConnect;
        private Label label1;
        private TextBox txtServerIp;
        private Button btnAddServer;
        private Button btnRemoveSelected;
        private GroupBox groupBox1;
        private Label label3;
        private NumericUpDown numProxyPort;
        private Label label4;
        private NumericUpDown numLocalPort;
        private NumericUpDown numGameQueryPort;
        private Label label2;
        private ToolStripStatusLabel toolStripStatusLabel_ServerCount;
        private ToolStripStatusLabel toolStripStatusLabel_Message;
        private GroupBox groupBox2;
        private TextBox txtServerPublicIp;
        private Label label12;
        private Label label13;
        private NumericUpDown numServerInternalPort;
        private Label label14;
        private NumericUpDown numServerClientPort;
        private Button btnStartServer;
        private PictureBox mapPicture;
        private Label label5;
        private Label lblServerNameValue;
        private Label lblAddressValue;
        private Label lblPlayersValue;
        private Label lblMapValue;
        private Label lblPingValue;
        private Label lblGameModeValue;
        private Label lblTagsValue;
        private Label label6;
        private Label label7;
        private Label label8;
        private Label label9;
        private Label label10;
        private Label label11;
        private GroupBox infoPanel1;
        private ListView listViewServers;
        private ColumnHeader colShield;
        private ColumnHeader colLock;
        private ColumnHeader colServer;
        private ColumnHeader coAddress;
        private ColumnHeader coPlayers;
        private ColumnHeader coMap;
        private ColumnHeader coPing;
        private ColumnHeader colTick;
        private ColumnHeader colTags;
        private Button btnRemovePersonal;
        private ListView listViewFavorites;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
        private ColumnHeader columnHeader3;
        private ColumnHeader columnHeader4;
        private ColumnHeader columnHeader5;
        private ColumnHeader columnHeader6;
        private ColumnHeader columnHeader7;
        private ColumnHeader columnHeader8;
        private ColumnHeader columnHeader9;
        private ListView listViewHistory;
        private ColumnHeader columnHeader10;
        private ColumnHeader columnHeader11;
        private ColumnHeader columnHeader12;
        private ColumnHeader columnHeader13;
        private ColumnHeader columnHeader14;
        private ColumnHeader columnHeader15;
        private ColumnHeader columnHeader16;
        private ColumnHeader columnHeader17;
        private ColumnHeader columnHeader18;
        private ListView listViewPersonal;
        private ColumnHeader columnHeader19;
        private ColumnHeader columnHeader20;
        private ColumnHeader columnHeader21;
        private ColumnHeader columnHeader22;
        private ColumnHeader columnHeader23;
        private ColumnHeader columnHeader24;
        private ColumnHeader columnHeader25;
        private ColumnHeader columnHeader26;
        private ColumnHeader columnHeader27;
        private Button btnCheckUpdates;
        private Button btnAddFavorite;
        private TabControl tabControl1;
        private TabPage tabInternet;
        private TabPage tabFavorites;
        private TabPage tabHistory;
        private TabPage tabPersonal;
        private Label Search;
        private TextBox svSearchBox;
        private Button btnPlayers;
        private Button btnGetMaps;
        private RichTextBox txtConsole;
    }
}
