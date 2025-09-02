namespace Manager
{
    partial class ManagerGUI
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ManagerGUI));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.uiOpenGameDir = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.uiModsPath = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.uiGamePath = new System.Windows.Forms.TextBox();
            this.uiLaunchButton = new System.Windows.Forms.Button();
            this.uiLocateButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.uiInstaledLabel = new System.Windows.Forms.Label();
            this.uiAvailableLabel = new System.Windows.Forms.Label();
            this.uiInstalledModsListView = new System.Windows.Forms.ListBox();
            this.uiAvailbleModsListView = new System.Windows.Forms.ListBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.harmonyLink = new System.Windows.Forms.LinkLabel();
            this.label6 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.doorstopLink = new System.Windows.Forms.LinkLabel();
            this.shelteredLink = new System.Windows.Forms.LinkLabel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            // Create Details tab and child controls (Coolnether123)
            this.tabPageDetails = new System.Windows.Forms.TabPage();
            this.grpDetails = new System.Windows.Forms.GroupBox();
            this.pbPreview = new System.Windows.Forms.PictureBox();
            this.lblName = new System.Windows.Forms.Label();
            this.lblId = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblAuthors = new System.Windows.Forms.Label();
            this.lblDescription = new System.Windows.Forms.Label();
            this.lblTags = new System.Windows.Forms.Label();
            this.lblWebsite = new System.Windows.Forms.Label();
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.panelSettings = new System.Windows.Forms.Panel();
            this.btnApplySettings = new System.Windows.Forms.Button();
            this.btnResetSettings = new System.Windows.Forms.Button();
            this.chkDevMode = new System.Windows.Forms.CheckBox();
            this.grpManifestInspector = new System.Windows.Forms.GroupBox();
            this.chkHasId = new System.Windows.Forms.CheckBox();
            this.chkHasName = new System.Windows.Forms.CheckBox();
            this.chkHasVersion = new System.Windows.Forms.CheckBox();
            this.chkHasAuthors = new System.Windows.Forms.CheckBox();
            this.chkHasDescription = new System.Windows.Forms.CheckBox();
            this.chkHasEntryType = new System.Windows.Forms.CheckBox();
            this.chkHasDependsOn = new System.Windows.Forms.CheckBox();
            this.chkHasLoadBefore = new System.Windows.Forms.CheckBox();
            this.chkHasLoadAfter = new System.Windows.Forms.CheckBox();
            this.chkHasTags = new System.Windows.Forms.CheckBox();
            this.chkHasWebsite = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPageDetails);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(13, 244);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(461, 255);
            this.tabControl1.TabIndex = 10;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage1.Controls.Add(this.uiOpenGameDir);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.uiModsPath);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.uiGamePath);
            this.tabPage1.Controls.Add(this.uiLaunchButton);
            this.tabPage1.Controls.Add(this.uiLocateButton);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(453, 229);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Game";
            this.tabPage1.Click += new System.EventHandler(this.tabPage1_Click);
            // 
            // uiOpenGameDir
            // 
            this.uiOpenGameDir.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiOpenGameDir.Location = new System.Drawing.Point(227, 169);
            this.uiOpenGameDir.Name = "uiOpenGameDir";
            this.uiOpenGameDir.Size = new System.Drawing.Size(220, 25);
            this.uiOpenGameDir.TabIndex = 17;
            this.uiOpenGameDir.Text = "Open Game Directory";
            this.uiOpenGameDir.UseVisualStyleBackColor = true;
            this.uiOpenGameDir.Click += new System.EventHandler(this.uiOpenGameDir_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(8, 80);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(76, 17);
            this.label5.TabIndex = 16;
            this.label5.Text = "Mods-Path";
            // 
            // uiModsPath
            // 
            this.uiModsPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.uiModsPath.Enabled = false;
            this.uiModsPath.Location = new System.Drawing.Point(11, 100);
            this.uiModsPath.Name = "uiModsPath";
            this.uiModsPath.Size = new System.Drawing.Size(436, 20);
            this.uiModsPath.TabIndex = 15;
            this.uiModsPath.Text = "None";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(8, 15);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(204, 17);
            this.label4.TabIndex = 14;
            this.label4.Text = "Sheltered-Path (Sheltered.exe)";
            // 
            // uiGamePath
            // 
            this.uiGamePath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.uiGamePath.Location = new System.Drawing.Point(11, 34);
            this.uiGamePath.Name = "uiGamePath";
            this.uiGamePath.Size = new System.Drawing.Size(436, 20);
            this.uiGamePath.TabIndex = 12;
            this.uiGamePath.Text = "None";
            this.uiGamePath.TextChanged += new System.EventHandler(this.uiGamePath_TextChanged_1);
            // 
            // uiLaunchButton
            // 
            this.uiLaunchButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiLaunchButton.Location = new System.Drawing.Point(11, 200);
            this.uiLaunchButton.Name = "uiLaunchButton";
            this.uiLaunchButton.Size = new System.Drawing.Size(436, 23);
            this.uiLaunchButton.TabIndex = 11;
            this.uiLaunchButton.Text = "Launch Game";
            this.uiLaunchButton.UseVisualStyleBackColor = true;
            this.uiLaunchButton.Click += new System.EventHandler(this.onLaunchClicked);
            // 
            // uiLocateButton
            // 
            this.uiLocateButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiLocateButton.Location = new System.Drawing.Point(11, 169);
            this.uiLocateButton.Name = "uiLocateButton";
            this.uiLocateButton.Size = new System.Drawing.Size(210, 25);
            this.uiLocateButton.TabIndex = 10;
            this.uiLocateButton.Text = "Locate Game Directory";
            this.uiLocateButton.UseVisualStyleBackColor = true;
            this.uiLocateButton.Click += new System.EventHandler(this.onLocate);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.SystemColors.Control;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(11, 6);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(0, 20);
            this.label1.TabIndex = 9;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.button2);
            this.tabPage3.Controls.Add(this.button1);
            this.tabPage3.Controls.Add(this.uiInstaledLabel);
            this.tabPage3.Controls.Add(this.uiAvailableLabel);
            this.tabPage3.Controls.Add(this.uiInstalledModsListView);
            this.tabPage3.Controls.Add(this.uiAvailbleModsListView);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Size = new System.Drawing.Size(453, 229);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Mods";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(190, 207);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(104, 23);
            this.button2.TabIndex = 5;
            this.button2.Text = "<=";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(190, 178);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(104, 23);
            this.button1.TabIndex = 4;
            this.button1.Text = "=>";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // btnMoveUpEnabled
            // 
            this.btnMoveUpEnabled = new System.Windows.Forms.Button();
            this.btnMoveUpEnabled.Location = new System.Drawing.Point(190, 149);
            this.btnMoveUpEnabled.Name = "btnMoveUpEnabled";
            this.btnMoveUpEnabled.Size = new System.Drawing.Size(104, 23);
            this.btnMoveUpEnabled.TabIndex = 6;
            this.btnMoveUpEnabled.Text = "Move Up";
            this.btnMoveUpEnabled.UseVisualStyleBackColor = true;
            this.btnMoveUpEnabled.Click += new System.EventHandler(this.btnMoveUpEnabled_Click);
            this.tabPage3.Controls.Add(this.btnMoveUpEnabled);
            // 
            // btnMoveDownEnabled
            // 
            this.btnMoveDownEnabled = new System.Windows.Forms.Button();
            this.btnMoveDownEnabled.Location = new System.Drawing.Point(190, 120);
            this.btnMoveDownEnabled.Name = "btnMoveDownEnabled";
            this.btnMoveDownEnabled.Size = new System.Drawing.Size(104, 23);
            this.btnMoveDownEnabled.TabIndex = 7;
            this.btnMoveDownEnabled.Text = "Move Down";
            this.btnMoveDownEnabled.UseVisualStyleBackColor = true;
            this.btnMoveDownEnabled.Click += new System.EventHandler(this.btnMoveDownEnabled_Click);
            this.tabPage3.Controls.Add(this.btnMoveDownEnabled);
            // 
            // btnSaveOrder
            // 
            this.btnSaveOrder = new System.Windows.Forms.Button();
            this.btnSaveOrder.Location = new System.Drawing.Point(190, 91);
            this.btnSaveOrder.Name = "btnSaveOrder";
            this.btnSaveOrder.Size = new System.Drawing.Size(104, 23);
            this.btnSaveOrder.TabIndex = 8;
            this.btnSaveOrder.Text = "Save Order";
            this.btnSaveOrder.UseVisualStyleBackColor = true;
            this.btnSaveOrder.Click += new System.EventHandler(this.btnSaveOrder_Click);
            this.tabPage3.Controls.Add(this.btnSaveOrder);
            // 
            // uiInstaledLabel
            // 
            this.uiInstaledLabel.AutoSize = true;
            this.uiInstaledLabel.Location = new System.Drawing.Point(311, 12);
            this.uiInstaledLabel.Name = "uiInstaledLabel";
            this.uiInstaledLabel.Size = new System.Drawing.Size(78, 13);
            this.uiInstaledLabel.TabIndex = 3;
            this.uiInstaledLabel.Text = "Installed Mods:";
            this.uiInstaledLabel.Click += new System.EventHandler(this.uiInstaledLabel_Click);
            // 
            // uiAvailableLabel
            // 
            this.uiAvailableLabel.AutoSize = true;
            this.uiAvailableLabel.Location = new System.Drawing.Point(11, 12);
            this.uiAvailableLabel.Name = "uiAvailableLabel";
            this.uiAvailableLabel.Size = new System.Drawing.Size(79, 13);
            this.uiAvailableLabel.TabIndex = 2;
            this.uiAvailableLabel.Text = "Available Mods";
            // 
            // uiInstalledModsListView
            // 
            this.uiInstalledModsListView.FormattingEnabled = true;
            this.uiInstalledModsListView.Location = new System.Drawing.Point(300, 31);
            this.uiInstalledModsListView.Name = "uiInstalledModsListView";
            this.uiInstalledModsListView.Size = new System.Drawing.Size(169, 199);
            this.uiInstalledModsListView.TabIndex = 1;
            this.uiInstalledModsListView.SelectedIndexChanged += new System.EventHandler(this.uiInstalledModsListView_SelectedIndexChanged);
            this.uiInstalledModsListView.DoubleClick += new System.EventHandler(this.uiInstalledModsListView_DoubleClick);
            // 
            // uiAvailbleModsListView
            // 
            this.uiAvailbleModsListView.FormattingEnabled = true;
            this.uiAvailbleModsListView.Location = new System.Drawing.Point(11, 31);
            this.uiAvailbleModsListView.Name = "uiAvailbleModsListView";
            this.uiAvailbleModsListView.Size = new System.Drawing.Size(173, 199);
            this.uiAvailbleModsListView.TabIndex = 0;
            this.uiAvailbleModsListView.SelectedIndexChanged += new System.EventHandler(this.uiAvailbleModsListView_SelectedIndexChanged);
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.harmonyLink);
            this.tabPage2.Controls.Add(this.label6);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.label2);
            this.tabPage2.Controls.Add(this.doorstopLink);
            this.tabPage2.Controls.Add(this.shelteredLink);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(453, 229);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "About";
            // 
            // harmonyLink
            // 
            this.harmonyLink.AutoSize = true;
            this.harmonyLink.Location = new System.Drawing.Point(8, 139);
            this.harmonyLink.Name = "harmonyLink";
            this.harmonyLink.Size = new System.Drawing.Size(188, 13);
            this.harmonyLink.TabIndex = 5;
            this.harmonyLink.TabStop = true;
            this.harmonyLink.Text = "https://github.com/pardeike/Harmony";
            this.harmonyLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked_1);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(8, 122);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(72, 17);
            this.label6.TabIndex = 4;
            this.label6.Text = "Harmony";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(8, 67);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(118, 17);
            this.label3.TabIndex = 3;
            this.label3.Text = "Unity DoorStop";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(8, 12);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(78, 17);
            this.label2.TabIndex = 2;
            this.label2.Text = "Sheltered";
            // 
            // doorstopLink
            // 
            this.doorstopLink.AutoSize = true;
            this.doorstopLink.Location = new System.Drawing.Point(8, 84);
            this.doorstopLink.Name = "doorstopLink";
            this.doorstopLink.Size = new System.Drawing.Size(226, 13);
            this.doorstopLink.TabIndex = 1;
            this.doorstopLink.TabStop = true;
            this.doorstopLink.Text = "https://github.com/NeighTools/UnityDoorstop";
            this.doorstopLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel2_LinkClicked);
            // 
            // shelteredLink
            // 
            this.shelteredLink.AutoSize = true;
            this.shelteredLink.Location = new System.Drawing.Point(8, 29);
            this.shelteredLink.Name = "shelteredLink";
            this.shelteredLink.Size = new System.Drawing.Size(280, 13);
            this.shelteredLink.TabIndex = 0;
            this.shelteredLink.TabStop = true;
            this.shelteredLink.Text = "https://store.steampowered.com/app/356040/Sheltered/";
            this.shelteredLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::Manager.Properties.Resources.logo1;
            this.pictureBox1.Location = new System.Drawing.Point(13, 13);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(461, 215);
            this.pictureBox1.TabIndex = 11;
            this.pictureBox1.TabStop = false;
            // 
            // tabPageDetails
            // 
            this.tabPageDetails.Location = new System.Drawing.Point(4, 22);
            this.tabPageDetails.Name = "tabPageDetails";
            this.tabPageDetails.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageDetails.Size = new System.Drawing.Size(453, 229);
            this.tabPageDetails.TabIndex = 3;
            this.tabPageDetails.Text = "Details";
            this.tabPageDetails.UseVisualStyleBackColor = true;
            // 
            // grpDetails
            // 
            this.grpDetails.Location = new System.Drawing.Point(8, 6);
            this.grpDetails.Name = "grpDetails";
            this.grpDetails.Size = new System.Drawing.Size(290, 105);
            this.grpDetails.TabIndex = 0;
            this.grpDetails.TabStop = false;
            this.grpDetails.Text = "Details";
            // 
            // pbPreview
            // 
            this.pbPreview.Location = new System.Drawing.Point(14, 20);
            this.pbPreview.Name = "pbPreview";
            this.pbPreview.Size = new System.Drawing.Size(64, 64);
            this.pbPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbPreview.TabIndex = 0;
            this.pbPreview.TabStop = false;
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.lblName.Location = new System.Drawing.Point(90, 10);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(36, 13);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "name";
            // 
            // lblId
            // 
            this.lblId.AutoSize = true;
            this.lblId.Location = new System.Drawing.Point(90, 25);
            this.lblId.Name = "lblId";
            this.lblId.Size = new System.Drawing.Size(16, 13);
            this.lblId.TabIndex = 2;
            this.lblId.Text = "id";
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.Location = new System.Drawing.Point(90, 40);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(41, 13);
            this.lblVersion.TabIndex = 3;
            this.lblVersion.Text = "version";
            // 
            // lblAuthors
            // 
            this.lblAuthors.AutoSize = true;
            this.lblAuthors.Location = new System.Drawing.Point(90, 55);
            this.lblAuthors.Name = "lblAuthors";
            this.lblAuthors.Size = new System.Drawing.Size(43, 13);
            this.lblAuthors.TabIndex = 4;
            this.lblAuthors.Text = "authors";
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(90, 70);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(62, 13);
            this.lblDescription.TabIndex = 5;
            this.lblDescription.Text = "description";
            // 
            // lblTags
            // 
            this.lblTags.AutoSize = true;
            this.lblTags.Location = new System.Drawing.Point(90, 85);
            this.lblTags.Name = "lblTags";
            this.lblTags.Size = new System.Drawing.Size(28, 13);
            this.lblTags.TabIndex = 6;
            this.lblTags.Text = "tags";
            // 
            // lblWebsite
            // 
            this.lblWebsite.AutoSize = true;
            this.lblWebsite.Location = new System.Drawing.Point(90, 100);
            this.lblWebsite.Name = "lblWebsite";
            this.lblWebsite.Size = new System.Drawing.Size(50, 13);
            this.lblWebsite.TabIndex = 7;
            this.lblWebsite.Text = "website";
            // 
            // grpSettings
            // 
            this.grpSettings.Location = new System.Drawing.Point(8, 117);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(290, 106);
            this.grpSettings.TabIndex = 1;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Settings";
            // 
            // panelSettings
            // 
            this.panelSettings.AutoScroll = true;
            this.panelSettings.Location = new System.Drawing.Point(14, 136);
            this.panelSettings.Name = "panelSettings";
            this.panelSettings.Size = new System.Drawing.Size(276, 63);
            this.panelSettings.TabIndex = 2;
            // 
            // btnApplySettings
            // 
            this.btnApplySettings.Location = new System.Drawing.Point(164, 205);
            this.btnApplySettings.Name = "btnApplySettings";
            this.btnApplySettings.Size = new System.Drawing.Size(62, 18);
            this.btnApplySettings.TabIndex = 3;
            this.btnApplySettings.Text = "Apply";
            this.btnApplySettings.UseVisualStyleBackColor = true;
            this.btnApplySettings.Click += new System.EventHandler(this.btnApplySettings_Click);
            // 
            // btnResetSettings
            // 
            this.btnResetSettings.Location = new System.Drawing.Point(232, 205);
            this.btnResetSettings.Name = "btnResetSettings";
            this.btnResetSettings.Size = new System.Drawing.Size(62, 18);
            this.btnResetSettings.TabIndex = 4;
            this.btnResetSettings.Text = "Reset";
            this.btnResetSettings.UseVisualStyleBackColor = true;
            this.btnResetSettings.Click += new System.EventHandler(this.btnResetSettings_Click);
            // 
            // chkDevMode
            // 
            this.chkDevMode.AutoSize = true;
            this.chkDevMode.Location = new System.Drawing.Point(306, 6);
            this.chkDevMode.Name = "chkDevMode";
            this.chkDevMode.Size = new System.Drawing.Size(136, 17);
            this.chkDevMode.TabIndex = 5;
            this.chkDevMode.Text = "Developer Mode (More)";
            this.chkDevMode.UseVisualStyleBackColor = true;
            this.chkDevMode.CheckedChanged += new System.EventHandler(this.chkDevMode_CheckedChanged);
            // 
            // grpManifestInspector
            // 
            this.grpManifestInspector.Location = new System.Drawing.Point(306, 29);
            this.grpManifestInspector.Name = "grpManifestInspector";
            this.grpManifestInspector.Size = new System.Drawing.Size(141, 194);
            this.grpManifestInspector.TabIndex = 6;
            this.grpManifestInspector.TabStop = false;
            this.grpManifestInspector.Text = "Manifest Inspector";
            this.grpManifestInspector.Visible = false;
            // Add checkboxes
            this.chkHasId.Location = new System.Drawing.Point(10, 20);
            this.chkHasId.Size = new System.Drawing.Size(120, 16);
            this.chkHasId.Text = "id";
            this.grpManifestInspector.Controls.Add(this.chkHasId);
            this.chkHasName.Location = new System.Drawing.Point(10, 40);
            this.chkHasName.Size = new System.Drawing.Size(120, 16);
            this.chkHasName.Text = "name";
            this.grpManifestInspector.Controls.Add(this.chkHasName);
            this.chkHasVersion.Location = new System.Drawing.Point(10, 60);
            this.chkHasVersion.Size = new System.Drawing.Size(120, 16);
            this.chkHasVersion.Text = "version";
            this.grpManifestInspector.Controls.Add(this.chkHasVersion);
            this.chkHasAuthors.Location = new System.Drawing.Point(10, 80);
            this.chkHasAuthors.Size = new System.Drawing.Size(120, 16);
            this.chkHasAuthors.Text = "authors";
            this.grpManifestInspector.Controls.Add(this.chkHasAuthors);
            this.chkHasDescription.Location = new System.Drawing.Point(10, 100);
            this.chkHasDescription.Size = new System.Drawing.Size(120, 16);
            this.chkHasDescription.Text = "description";
            this.grpManifestInspector.Controls.Add(this.chkHasDescription);
            this.chkHasEntryType.Location = new System.Drawing.Point(10, 120);
            this.chkHasEntryType.Size = new System.Drawing.Size(120, 16);
            this.chkHasEntryType.Text = "entryType";
            this.grpManifestInspector.Controls.Add(this.chkHasEntryType);
            this.chkHasDependsOn.Location = new System.Drawing.Point(10, 140);
            this.chkHasDependsOn.Size = new System.Drawing.Size(120, 16);
            this.chkHasDependsOn.Text = "dependsOn";
            this.grpManifestInspector.Controls.Add(this.chkHasDependsOn);
            this.chkHasLoadBefore.Location = new System.Drawing.Point(10, 160);
            this.chkHasLoadBefore.Size = new System.Drawing.Size(120, 16);
            this.chkHasLoadBefore.Text = "loadBefore";
            this.grpManifestInspector.Controls.Add(this.chkHasLoadBefore);
            this.chkHasLoadAfter.Location = new System.Drawing.Point(10, 180);
            this.chkHasLoadAfter.Size = new System.Drawing.Size(120, 16);
            this.chkHasLoadAfter.Text = "loadAfter";
            this.grpManifestInspector.Controls.Add(this.chkHasLoadAfter);
            this.chkHasTags.Location = new System.Drawing.Point(10, 200);
            this.chkHasTags.Size = new System.Drawing.Size(120, 16);
            this.chkHasTags.Text = "tags";
            this.grpManifestInspector.Controls.Add(this.chkHasTags);
            // not all fit; ignore overflowing positions in small tab
            this.chkHasWebsite.Location = new System.Drawing.Point(10, 220);
            this.chkHasWebsite.Size = new System.Drawing.Size(120, 16);
            this.chkHasWebsite.Text = "website";
            this.grpManifestInspector.Controls.Add(this.chkHasWebsite);
            // 
            // Add controls to containers
            // 
            this.grpDetails.Controls.Add(this.pbPreview);
            this.grpDetails.Controls.Add(this.lblName);
            this.grpDetails.Controls.Add(this.lblId);
            this.grpDetails.Controls.Add(this.lblVersion);
            this.grpDetails.Controls.Add(this.lblAuthors);
            this.grpDetails.Controls.Add(this.lblDescription);
            this.grpDetails.Controls.Add(this.lblTags);
            this.grpDetails.Controls.Add(this.lblWebsite);
            this.tabPageDetails.Controls.Add(this.grpDetails);
            this.tabPageDetails.Controls.Add(this.grpSettings);
            this.tabPageDetails.Controls.Add(this.panelSettings);
            this.tabPageDetails.Controls.Add(this.btnApplySettings);
            this.tabPageDetails.Controls.Add(this.btnResetSettings);
            this.tabPageDetails.Controls.Add(this.chkDevMode);
            this.tabPageDetails.Controls.Add(this.grpManifestInspector);
            // 
            // ManagerGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 511);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.tabControl1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximumSize = new System.Drawing.Size(500, 550);
            this.Name = "ManagerGUI";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Sheltered Mod Manager v0.3";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TextBox uiGamePath;
        private System.Windows.Forms.Button uiLaunchButton;
        private System.Windows.Forms.Button uiLocateButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.LinkLabel doorstopLink;
        private System.Windows.Forms.LinkLabel shelteredLink;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Label uiInstaledLabel;
        private System.Windows.Forms.Label uiAvailableLabel;
        private System.Windows.Forms.ListBox uiInstalledModsListView;
        private System.Windows.Forms.ListBox uiAvailbleModsListView;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox uiModsPath;
        private System.Windows.Forms.LinkLabel harmonyLink;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button uiOpenGameDir;
        private System.Windows.Forms.Button btnMoveUpEnabled;
        private System.Windows.Forms.Button btnMoveDownEnabled;
        private System.Windows.Forms.Button btnSaveOrder;
        private System.Windows.Forms.TabPage tabPageDetails;
        private System.Windows.Forms.GroupBox grpDetails;
        private System.Windows.Forms.PictureBox pbPreview;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.Label lblId;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblAuthors;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Label lblTags;
        private System.Windows.Forms.Label lblWebsite;
        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.Panel panelSettings;
        private System.Windows.Forms.Button btnApplySettings;
        private System.Windows.Forms.Button btnResetSettings;
        private System.Windows.Forms.CheckBox chkDevMode;
        private System.Windows.Forms.GroupBox grpManifestInspector;
        private System.Windows.Forms.CheckBox chkHasId;
        private System.Windows.Forms.CheckBox chkHasName;
        private System.Windows.Forms.CheckBox chkHasVersion;
        private System.Windows.Forms.CheckBox chkHasAuthors;
        private System.Windows.Forms.CheckBox chkHasDescription;
        private System.Windows.Forms.CheckBox chkHasEntryType;
        private System.Windows.Forms.CheckBox chkHasDependsOn;
        private System.Windows.Forms.CheckBox chkHasLoadBefore;
        private System.Windows.Forms.CheckBox chkHasLoadAfter;
        private System.Windows.Forms.CheckBox chkHasTags;
        private System.Windows.Forms.CheckBox chkHasWebsite;
    }
}

