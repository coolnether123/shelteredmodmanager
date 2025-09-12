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
        /// <param name="disposing">True, wenn verwaltete Ressourcen gel�scht werden sollen; andernfalls False.</param>
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
        /// Erforderliche Methode f�r die Designerunterst�tzung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor ge�ndert werden.
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
            this.grpDetails = new System.Windows.Forms.GroupBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.pbPreview = new System.Windows.Forms.PictureBox();
            this.lblName = new System.Windows.Forms.Label();
            this.lblId = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblAuthors = new System.Windows.Forms.Label();
            this.rtbDescription = new System.Windows.Forms.RichTextBox();
            this.lblTags = new System.Windows.Forms.Label();
            this.lblWebsite = new System.Windows.Forms.Label();
            this.lblDependsOn = new System.Windows.Forms.Label();
            this.btnApplySettings = new System.Windows.Forms.Button();
            this.btnResetSettings = new System.Windows.Forms.Button();
            this.grpAboutInspector = new System.Windows.Forms.GroupBox();
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
            this.btnMoveUpEnabled = new System.Windows.Forms.Button();
            this.btnMoveDownEnabled = new System.Windows.Forms.Button();
            this.btnSaveOrder = new System.Windows.Forms.Button();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.harmonyLink = new System.Windows.Forms.LinkLabel();
            this.label6 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.doorstopLink = new System.Windows.Forms.LinkLabel();
            this.shelteredLink = new System.Windows.Forms.LinkLabel();
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.chkDevMode = new System.Windows.Forms.CheckBox();
            this.grpDevSettings = new System.Windows.Forms.GroupBox();
            this.lblLogLevel = new System.Windows.Forms.Label();
            this.cmbLogLevel = new System.Windows.Forms.ComboBox();
            this.lblLogCategories = new System.Windows.Forms.Label();
            this.clbLogCategories = new System.Windows.Forms.CheckedListBox();
            this.chkIgnoreOrderChecks = new System.Windows.Forms.CheckBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.panelSettings = new System.Windows.Forms.Panel();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.grpDetails.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbPreview)).BeginInit();
            this.grpAboutInspector.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.grpSettings.SuspendLayout();
            this.grpDevSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl1.Location = new System.Drawing.Point(20, 180);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1375, 556);
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
            this.tabPage1.Location = new System.Drawing.Point(4, 29);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(15);
            this.tabPage1.Size = new System.Drawing.Size(1367, 523);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Game Setup";
            this.tabPage1.Click += new System.EventHandler(this.tabPage1_Click);
            // 
            // uiOpenGameDir
            // 
            this.uiOpenGameDir.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiOpenGameDir.Location = new System.Drawing.Point(380, 220);
            this.uiOpenGameDir.Name = "uiOpenGameDir";
            this.uiOpenGameDir.Size = new System.Drawing.Size(340, 35);
            this.uiOpenGameDir.TabIndex = 17;
            this.uiOpenGameDir.Text = "Open Game Directory";
            this.uiOpenGameDir.UseVisualStyleBackColor = true;
            this.uiOpenGameDir.Click += new System.EventHandler(this.uiOpenGameDir_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(15, 120);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(112, 25);
            this.label5.TabIndex = 16;
            this.label5.Text = "Mods Path:";
            // 
            // uiModsPath
            // 
            this.uiModsPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uiModsPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.uiModsPath.Enabled = false;
            this.uiModsPath.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.uiModsPath.Location = new System.Drawing.Point(18, 145);
            this.uiModsPath.Name = "uiModsPath";
            this.uiModsPath.Size = new System.Drawing.Size(702, 30);
            this.uiModsPath.TabIndex = 15;
            this.uiModsPath.Text = "None";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(15, 25);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(252, 25);
            this.label4.TabIndex = 14;
            this.label4.Text = "Game Path (Sheltered.exe):";
            // 
            // uiGamePath
            // 
            this.uiGamePath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uiGamePath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.uiGamePath.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.uiGamePath.Location = new System.Drawing.Point(18, 50);
            this.uiGamePath.Name = "uiGamePath";
            this.uiGamePath.Size = new System.Drawing.Size(702, 30);
            this.uiGamePath.TabIndex = 12;
            this.uiGamePath.Text = "None";
            this.uiGamePath.TextChanged += new System.EventHandler(this.uiGamePath_TextChanged_1);
            // 
            // uiLaunchButton
            // 
            this.uiLaunchButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.uiLaunchButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.uiLaunchButton.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiLaunchButton.ForeColor = System.Drawing.Color.White;
            this.uiLaunchButton.Location = new System.Drawing.Point(18, 310);
            this.uiLaunchButton.Name = "uiLaunchButton";
            this.uiLaunchButton.Size = new System.Drawing.Size(702, 45);
            this.uiLaunchButton.TabIndex = 11;
            this.uiLaunchButton.Text = "Launch Sheltered with Mods";
            this.uiLaunchButton.UseVisualStyleBackColor = false;
            this.uiLaunchButton.Click += new System.EventHandler(this.onLaunchClicked);
            // 
            // uiLocateButton
            // 
            this.uiLocateButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiLocateButton.Location = new System.Drawing.Point(18, 220);
            this.uiLocateButton.Name = "uiLocateButton";
            this.uiLocateButton.Size = new System.Drawing.Size(340, 35);
            this.uiLocateButton.TabIndex = 10;
            this.uiLocateButton.Text = "Browse for Sheltered.exe";
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
            this.label1.Size = new System.Drawing.Size(0, 25);
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
            this.tabPage3.Controls.Add(this.grpDetails);
            this.tabPage3.Controls.Add(this.btnApplySettings);
            this.tabPage3.Controls.Add(this.btnResetSettings);
            this.tabPage3.Controls.Add(this.grpAboutInspector);
            this.tabPage3.Controls.Add(this.btnMoveUpEnabled);
            this.tabPage3.Controls.Add(this.btnMoveDownEnabled);
            this.tabPage3.Controls.Add(this.btnSaveOrder);
            this.tabPage3.Location = new System.Drawing.Point(4, 29);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(15);
            this.tabPage3.Size = new System.Drawing.Size(1367, 523);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Mod Manager";
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.button2.Location = new System.Drawing.Point(290, 185);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(120, 35);
            this.button2.TabIndex = 5;
            this.button2.Text = "<- Disable";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // button1
            // 
            this.button1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.button1.Location = new System.Drawing.Point(290, 140);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(120, 35);
            this.button1.TabIndex = 4;
            this.button1.Text = "Enable ->";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // uiInstaledLabel
            // 
            this.uiInstaledLabel.AutoSize = true;
            this.uiInstaledLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.uiInstaledLabel.Location = new System.Drawing.Point(425, 20);
            this.uiInstaledLabel.Name = "uiInstaledLabel";
            this.uiInstaledLabel.Size = new System.Drawing.Size(138, 25);
            this.uiInstaledLabel.TabIndex = 3;
            this.uiInstaledLabel.Text = "Enabled Mods";
            this.uiInstaledLabel.Click += new System.EventHandler(this.uiInstaledLabel_Click);
            // 
            // uiAvailableLabel
            // 
            this.uiAvailableLabel.AutoSize = true;
            this.uiAvailableLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.uiAvailableLabel.Location = new System.Drawing.Point(20, 20);
            this.uiAvailableLabel.Name = "uiAvailableLabel";
            this.uiAvailableLabel.Size = new System.Drawing.Size(143, 25);
            this.uiAvailableLabel.TabIndex = 2;
            this.uiAvailableLabel.Text = "Disabled Mods";
            // 
            // uiInstalledModsListView
            // 
            this.uiInstalledModsListView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.uiInstalledModsListView.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.uiInstalledModsListView.FormattingEnabled = true;
            this.uiInstalledModsListView.ItemHeight = 23;
            this.uiInstalledModsListView.Location = new System.Drawing.Point(430, 45);
            this.uiInstalledModsListView.Name = "uiInstalledModsListView";
            this.uiInstalledModsListView.Size = new System.Drawing.Size(250, 464);
            this.uiInstalledModsListView.TabIndex = 1;
            this.uiInstalledModsListView.SelectedIndexChanged += new System.EventHandler(this.uiInstalledModsListView_SelectedIndexChanged);
            this.uiInstalledModsListView.DoubleClick += new System.EventHandler(this.uiInstalledModsListView_DoubleClick);
            // 
            // uiAvailbleModsListView
            // 
            this.uiAvailbleModsListView.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.uiAvailbleModsListView.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.uiAvailbleModsListView.FormattingEnabled = true;
            this.uiAvailbleModsListView.ItemHeight = 23;
            this.uiAvailbleModsListView.Location = new System.Drawing.Point(20, 45);
            this.uiAvailbleModsListView.Name = "uiAvailbleModsListView";
            this.uiAvailbleModsListView.Size = new System.Drawing.Size(250, 464);
            this.uiAvailbleModsListView.TabIndex = 0;
            this.uiAvailbleModsListView.SelectedIndexChanged += new System.EventHandler(this.uiAvailbleModsListView_SelectedIndexChanged);
            this.uiAvailbleModsListView.DoubleClick += new System.EventHandler(this.uiAvailbleModsListView_DoubleClick);
            // 
            // grpDetails
            // 
            this.grpDetails.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpDetails.Controls.Add(this.label10);
            this.grpDetails.Controls.Add(this.label7);
            this.grpDetails.Controls.Add(this.label8);
            this.grpDetails.Controls.Add(this.label9);
            this.grpDetails.Controls.Add(this.label11);
            this.grpDetails.Controls.Add(this.label12);
            this.grpDetails.Controls.Add(this.label13);
            this.grpDetails.Controls.Add(this.pbPreview);
            this.grpDetails.Controls.Add(this.lblName);
            this.grpDetails.Controls.Add(this.lblId);
            this.grpDetails.Controls.Add(this.lblVersion);
            this.grpDetails.Controls.Add(this.lblAuthors);
            this.grpDetails.Controls.Add(this.rtbDescription);
            this.grpDetails.Controls.Add(this.lblTags);
            this.grpDetails.Controls.Add(this.lblWebsite);
            this.grpDetails.Controls.Add(this.lblDependsOn);
            this.grpDetails.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpDetails.Location = new System.Drawing.Point(720, 20);
            this.grpDetails.Name = "grpDetails";
            this.grpDetails.Size = new System.Drawing.Size(631, 497);
            this.grpDetails.TabIndex = 0;
            this.grpDetails.TabStop = false;
            this.grpDetails.Text = "Mod Details";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(12, 93);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(70, 23);
            this.label10.TabIndex = 15;
            this.label10.Text = "authors";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.label7.Location = new System.Drawing.Point(13, 26);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(49, 17);
            this.label7.TabIndex = 8;
            this.label7.Text = "Name";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(137, 224);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(28, 23);
            this.label8.TabIndex = 9;
            this.label8.Text = "ID";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(12, 61);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(68, 23);
            this.label9.TabIndex = 10;
            this.label9.Text = "Version";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(12, 225);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(100, 23);
            this.label11.TabIndex = 12;
            this.label11.Text = "description";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(10, 142);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(44, 23);
            this.label12.TabIndex = 13;
            this.label12.Text = "tags";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(10, 180);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(72, 23);
            this.label13.TabIndex = 14;
            this.label13.Text = "website";
            // 
            // pbPreview
            // 
            this.pbPreview.Location = new System.Drawing.Point(546, 20);
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
            this.lblName.Location = new System.Drawing.Point(87, 25);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(47, 17);
            this.lblName.TabIndex = 1;
            this.lblName.Text = "name";
            // 
            // lblId
            // 
            this.lblId.AutoSize = true;
            this.lblId.Location = new System.Drawing.Point(189, 224);
            this.lblId.Name = "lblId";
            this.lblId.Size = new System.Drawing.Size(26, 23);
            this.lblId.TabIndex = 2;
            this.lblId.Text = "id";
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.Location = new System.Drawing.Point(86, 61);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(67, 23);
            this.lblVersion.TabIndex = 3;
            this.lblVersion.Text = "version";
            // 
            // lblAuthors
            // 
            this.lblAuthors.AutoSize = true;
            this.lblAuthors.Location = new System.Drawing.Point(86, 93);
            this.lblAuthors.Name = "lblAuthors";
            this.lblAuthors.Size = new System.Drawing.Size(70, 23);
            this.lblAuthors.TabIndex = 4;
            this.lblAuthors.Text = "authors";
            // 
            // rtbDescription
            // 
            this.rtbDescription.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rtbDescription.BackColor = System.Drawing.Color.White;
            this.rtbDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.rtbDescription.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.rtbDescription.Location = new System.Drawing.Point(15, 250);
            this.rtbDescription.Name = "rtbDescription";
            this.rtbDescription.ReadOnly = true;
            this.rtbDescription.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.rtbDescription.Size = new System.Drawing.Size(601, 227);
            this.rtbDescription.TabIndex = 5;
            this.rtbDescription.Text = "";
            // 
            // lblTags
            // 
            this.lblTags.AutoSize = true;
            this.lblTags.Location = new System.Drawing.Point(60, 142);
            this.lblTags.Name = "lblTags";
            this.lblTags.Size = new System.Drawing.Size(44, 23);
            this.lblTags.TabIndex = 6;
            this.lblTags.Text = "tags";
            // 
            // lblWebsite
            // 
            this.lblWebsite.AutoSize = true;
            this.lblWebsite.Location = new System.Drawing.Point(88, 180);
            this.lblWebsite.Name = "lblWebsite";
            this.lblWebsite.Size = new System.Drawing.Size(72, 23);
            this.lblWebsite.TabIndex = 7;
            this.lblWebsite.Text = "website";
            // 
            // lblDependsOn
            // 
            this.lblDependsOn.AutoSize = true;
            this.lblDependsOn.Location = new System.Drawing.Point(12, 120);
            this.lblDependsOn.Name = "lblDependsOn";
            this.lblDependsOn.Size = new System.Drawing.Size(110, 23);
            this.lblDependsOn.TabIndex = 16;
            this.lblDependsOn.Text = "Depends on:";
            // 
            // btnApplySettings
            // 
            this.btnApplySettings.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnApplySettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnApplySettings.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnApplySettings.ForeColor = System.Drawing.Color.White;
            this.btnApplySettings.Location = new System.Drawing.Point(1016, 755);
            this.btnApplySettings.Name = "btnApplySettings";
            this.btnApplySettings.Size = new System.Drawing.Size(80, 30);
            this.btnApplySettings.TabIndex = 3;
            this.btnApplySettings.Text = "Apply";
            this.btnApplySettings.UseVisualStyleBackColor = false;
            this.btnApplySettings.Click += new System.EventHandler(this.btnApplySettings_Click);
            // 
            // btnResetSettings
            // 
            this.btnResetSettings.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnResetSettings.Location = new System.Drawing.Point(1106, 755);
            this.btnResetSettings.Name = "btnResetSettings";
            this.btnResetSettings.Size = new System.Drawing.Size(80, 30);
            this.btnResetSettings.TabIndex = 4;
            this.btnResetSettings.Text = "Reset";
            this.btnResetSettings.UseVisualStyleBackColor = true;
            this.btnResetSettings.Click += new System.EventHandler(this.btnResetSettings_Click);
            // 
            // grpAboutInspector
            // 
            this.grpAboutInspector.Controls.Add(this.chkHasId);
            this.grpAboutInspector.Controls.Add(this.chkHasName);
            this.grpAboutInspector.Controls.Add(this.chkHasVersion);
            this.grpAboutInspector.Controls.Add(this.chkHasAuthors);
            this.grpAboutInspector.Controls.Add(this.chkHasDescription);
            this.grpAboutInspector.Controls.Add(this.chkHasEntryType);
            this.grpAboutInspector.Controls.Add(this.chkHasDependsOn);
            this.grpAboutInspector.Controls.Add(this.chkHasLoadBefore);
            this.grpAboutInspector.Controls.Add(this.chkHasLoadAfter);
            this.grpAboutInspector.Controls.Add(this.chkHasTags);
            this.grpAboutInspector.Controls.Add(this.chkHasWebsite);
            this.grpAboutInspector.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.grpAboutInspector.Location = new System.Drawing.Point(276, 276);
            this.grpAboutInspector.Name = "grpAboutInspector";
            this.grpAboutInspector.Size = new System.Drawing.Size(148, 241);
            this.grpAboutInspector.TabIndex = 6;
            this.grpAboutInspector.TabStop = false;
            this.grpAboutInspector.Text = "About Inspector";
            this.grpAboutInspector.Visible = false;
            // 
            // chkHasId
            // 
            this.chkHasId.Location = new System.Drawing.Point(10, 20);
            this.chkHasId.Name = "chkHasId";
            this.chkHasId.Size = new System.Drawing.Size(120, 25);
            this.chkHasId.TabIndex = 0;
            this.chkHasId.Text = "id";
            // 
            // chkHasName
            // 
            this.chkHasName.Location = new System.Drawing.Point(10, 40);
            this.chkHasName.Name = "chkHasName";
            this.chkHasName.Size = new System.Drawing.Size(120, 25);
            this.chkHasName.TabIndex = 1;
            this.chkHasName.Text = "name";
            // 
            // chkHasVersion
            // 
            this.chkHasVersion.Location = new System.Drawing.Point(10, 60);
            this.chkHasVersion.Name = "chkHasVersion";
            this.chkHasVersion.Size = new System.Drawing.Size(120, 25);
            this.chkHasVersion.TabIndex = 2;
            this.chkHasVersion.Text = "version";
            // 
            // chkHasAuthors
            // 
            this.chkHasAuthors.Location = new System.Drawing.Point(10, 80);
            this.chkHasAuthors.Name = "chkHasAuthors";
            this.chkHasAuthors.Size = new System.Drawing.Size(120, 25);
            this.chkHasAuthors.TabIndex = 3;
            this.chkHasAuthors.Text = "authors";
            // 
            // chkHasDescription
            // 
            this.chkHasDescription.Location = new System.Drawing.Point(10, 100);
            this.chkHasDescription.Name = "chkHasDescription";
            this.chkHasDescription.Size = new System.Drawing.Size(120, 25);
            this.chkHasDescription.TabIndex = 4;
            this.chkHasDescription.Text = "description";
            // 
            // chkHasEntryType
            // 
            this.chkHasEntryType.Location = new System.Drawing.Point(10, 120);
            this.chkHasEntryType.Name = "chkHasEntryType";
            this.chkHasEntryType.Size = new System.Drawing.Size(120, 25);
            this.chkHasEntryType.TabIndex = 5;
            this.chkHasEntryType.Text = "entryType";
            // 
            // chkHasDependsOn
            // 
            this.chkHasDependsOn.Location = new System.Drawing.Point(10, 140);
            this.chkHasDependsOn.Name = "chkHasDependsOn";
            this.chkHasDependsOn.Size = new System.Drawing.Size(120, 25);
            this.chkHasDependsOn.TabIndex = 6;
            this.chkHasDependsOn.Text = "dependsOn";
            // 
            // chkHasLoadBefore
            // 
            this.chkHasLoadBefore.Location = new System.Drawing.Point(10, 160);
            this.chkHasLoadBefore.Name = "chkHasLoadBefore";
            this.chkHasLoadBefore.Size = new System.Drawing.Size(120, 25);
            this.chkHasLoadBefore.TabIndex = 7;
            this.chkHasLoadBefore.Text = "loadBefore";
            // 
            // chkHasLoadAfter
            // 
            this.chkHasLoadAfter.Location = new System.Drawing.Point(10, 180);
            this.chkHasLoadAfter.Name = "chkHasLoadAfter";
            this.chkHasLoadAfter.Size = new System.Drawing.Size(120, 25);
            this.chkHasLoadAfter.TabIndex = 8;
            this.chkHasLoadAfter.Text = "loadAfter";
            // 
            // chkHasTags
            // 
            this.chkHasTags.Location = new System.Drawing.Point(10, 200);
            this.chkHasTags.Name = "chkHasTags";
            this.chkHasTags.Size = new System.Drawing.Size(120, 25);
            this.chkHasTags.TabIndex = 9;
            this.chkHasTags.Text = "tags";
            // 
            // chkHasWebsite
            // 
            this.chkHasWebsite.Location = new System.Drawing.Point(10, 220);
            this.chkHasWebsite.Name = "chkHasWebsite";
            this.chkHasWebsite.Size = new System.Drawing.Size(120, 25);
            this.chkHasWebsite.TabIndex = 10;
            this.chkHasWebsite.Text = "website";
            // 
            // btnMoveUpEnabled
            // 
            this.btnMoveUpEnabled.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnMoveUpEnabled.Location = new System.Drawing.Point(290, 50);
            this.btnMoveUpEnabled.Name = "btnMoveUpEnabled";
            this.btnMoveUpEnabled.Size = new System.Drawing.Size(120, 30);
            this.btnMoveUpEnabled.TabIndex = 6;
            this.btnMoveUpEnabled.Text = "^ Move Up";
            this.btnMoveUpEnabled.UseVisualStyleBackColor = true;
            this.btnMoveUpEnabled.Click += new System.EventHandler(this.btnMoveUpEnabled_Click);
            // 
            // btnMoveDownEnabled
            // 
            this.btnMoveDownEnabled.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.btnMoveDownEnabled.Location = new System.Drawing.Point(290, 85);
            this.btnMoveDownEnabled.Name = "btnMoveDownEnabled";
            this.btnMoveDownEnabled.Size = new System.Drawing.Size(120, 30);
            this.btnMoveDownEnabled.TabIndex = 7;
            this.btnMoveDownEnabled.Text = "v Move Down";
            this.btnMoveDownEnabled.UseVisualStyleBackColor = true;
            this.btnMoveDownEnabled.Click += new System.EventHandler(this.btnMoveDownEnabled_Click);
            // 
            // btnSaveOrder
            // 
            this.btnSaveOrder.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this.btnSaveOrder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveOrder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.btnSaveOrder.ForeColor = System.Drawing.Color.White;
            this.btnSaveOrder.Location = new System.Drawing.Point(290, 235);
            this.btnSaveOrder.Name = "btnSaveOrder";
            this.btnSaveOrder.Size = new System.Drawing.Size(120, 35);
            this.btnSaveOrder.TabIndex = 8;
            this.btnSaveOrder.Text = "Sort Order";
            this.btnSaveOrder.UseVisualStyleBackColor = false;
            this.btnSaveOrder.Click += new System.EventHandler(this.btnSaveOrder_Click);
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
            this.tabPage2.Location = new System.Drawing.Point(4, 29);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(15);
            this.tabPage2.Size = new System.Drawing.Size(1367, 523);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "About";
            // 
            // harmonyLink
            // 
            this.harmonyLink.AutoSize = true;
            this.harmonyLink.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.harmonyLink.Location = new System.Drawing.Point(20, 200);
            this.harmonyLink.Name = "harmonyLink";
            this.harmonyLink.Size = new System.Drawing.Size(324, 23);
            this.harmonyLink.TabIndex = 5;
            this.harmonyLink.TabStop = true;
            this.harmonyLink.Text = "?? https://github.com/pardeike/Harmony";
            this.harmonyLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked_1);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(20, 170);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(120, 32);
            this.label6.TabIndex = 4;
            this.label6.Text = "Harmony";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(20, 100);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(191, 32);
            this.label3.TabIndex = 3;
            this.label3.Text = "Unity DoorStop";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(20, 30);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(121, 32);
            this.label2.TabIndex = 2;
            this.label2.Text = "Sheltered";
            // 
            // doorstopLink
            // 
            this.doorstopLink.AutoSize = true;
            this.doorstopLink.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.doorstopLink.Location = new System.Drawing.Point(20, 130);
            this.doorstopLink.Name = "doorstopLink";
            this.doorstopLink.Size = new System.Drawing.Size(384, 23);
            this.doorstopLink.TabIndex = 1;
            this.doorstopLink.TabStop = true;
            this.doorstopLink.Text = "?? https://github.com/NeighTools/UnityDoorstop";
            this.doorstopLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel2_LinkClicked);
            // 
            // shelteredLink
            // 
            this.shelteredLink.AutoSize = true;
            this.shelteredLink.Font = new System.Drawing.Font("Segoe UI", 10F);
            this.shelteredLink.Location = new System.Drawing.Point(20, 60);
            this.shelteredLink.Name = "shelteredLink";
            this.shelteredLink.Size = new System.Drawing.Size(462, 23);
            this.shelteredLink.TabIndex = 0;
            this.shelteredLink.TabStop = true;
            this.shelteredLink.Text = "?? https://store.steampowered.com/app/356040/Sheltered/";
            this.shelteredLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel1_LinkClicked);
            // 
            // grpSettings
            // 
            this.grpSettings.Controls.Add(this.chkDevMode);
            this.grpSettings.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpSettings.Location = new System.Drawing.Point(369, 15);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(294, 117);
            this.grpSettings.TabIndex = 1;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Mod Settings WIP";
            // 
            // chkDevMode
            // 
            this.chkDevMode.AutoSize = true;
            this.chkDevMode.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.chkDevMode.Location = new System.Drawing.Point(6, 29);
            this.chkDevMode.Name = "chkDevMode";
            this.chkDevMode.Size = new System.Drawing.Size(227, 24);
            this.chkDevMode.TabIndex = 5;
            this.chkDevMode.Text = " Developer Mode (Advanced)";
            this.chkDevMode.UseVisualStyleBackColor = true;
            this.chkDevMode.CheckedChanged += new System.EventHandler(this.chkDevMode_CheckedChanged);
            // 
            // grpDevSettings
            // 
            this.grpDevSettings.Controls.Add(this.lblLogLevel);
            this.grpDevSettings.Controls.Add(this.cmbLogLevel);
            this.grpDevSettings.Controls.Add(this.lblLogCategories);
            this.grpDevSettings.Controls.Add(this.clbLogCategories);
            this.grpDevSettings.Controls.Add(this.chkIgnoreOrderChecks);
            this.grpDevSettings.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.grpDevSettings.Location = new System.Drawing.Point(825, 15);
            this.grpDevSettings.Name = "grpDevSettings";
            this.grpDevSettings.Size = new System.Drawing.Size(563, 180);
            this.grpDevSettings.TabIndex = 2;
            this.grpDevSettings.TabStop = false;
            this.grpDevSettings.Text = "Developer Settings WIP";
            this.grpDevSettings.Visible = false;
            // 
            // lblLogLevel
            // 
            this.lblLogLevel.AutoSize = true;
            this.lblLogLevel.Location = new System.Drawing.Point(10, 25);
            this.lblLogLevel.Name = "lblLogLevel";
            this.lblLogLevel.Size = new System.Drawing.Size(91, 23);
            this.lblLogLevel.TabIndex = 0;
            this.lblLogLevel.Text = "Log Level:";
            // 
            // cmbLogLevel
            // 
            this.cmbLogLevel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLogLevel.Location = new System.Drawing.Point(120, 22);
            this.cmbLogLevel.Name = "cmbLogLevel";
            this.cmbLogLevel.Size = new System.Drawing.Size(121, 31);
            this.cmbLogLevel.TabIndex = 1;
            // 
            // lblLogCategories
            // 
            this.lblLogCategories.AutoSize = true;
            this.lblLogCategories.Location = new System.Drawing.Point(10, 55);
            this.lblLogCategories.Name = "lblLogCategories";
            this.lblLogCategories.Size = new System.Drawing.Size(135, 23);
            this.lblLogCategories.TabIndex = 2;
            this.lblLogCategories.Text = "Log Categories:";
            // 
            // clbLogCategories
            // 
            this.clbLogCategories.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.clbLogCategories.CheckOnClick = true;
            this.clbLogCategories.Location = new System.Drawing.Point(151, 55);
            this.clbLogCategories.Name = "clbLogCategories";
            this.clbLogCategories.Size = new System.Drawing.Size(200, 102);
            this.clbLogCategories.TabIndex = 3;
            // 
            // chkIgnoreOrderChecks
            // 
            this.chkIgnoreOrderChecks.AutoSize = true;
            this.chkIgnoreOrderChecks.Location = new System.Drawing.Point(249, 12);
            this.chkIgnoreOrderChecks.Name = "chkIgnoreOrderChecks";
            this.chkIgnoreOrderChecks.Size = new System.Drawing.Size(275, 27);
            this.chkIgnoreOrderChecks.TabIndex = 4;
            this.chkIgnoreOrderChecks.Text = "Ignore order checks (dev only)";
            this.chkIgnoreOrderChecks.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox1.Image = global::Manager.Properties.Resources.logo1;
            this.pictureBox1.Location = new System.Drawing.Point(20, 15);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(343, 159);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 11;
            this.pictureBox1.TabStop = false;
            // 
            // panelSettings
            // 
            this.panelSettings.AutoScroll = true;
            this.panelSettings.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.panelSettings.Location = new System.Drawing.Point(885, 24);
            this.panelSettings.Name = "panelSettings";
            this.panelSettings.Size = new System.Drawing.Size(310, 90);
            this.panelSettings.TabIndex = 2;
            // 
            // ManagerGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1408, 749);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.grpSettings);
            this.Controls.Add(this.grpDevSettings);
            this.Controls.Add(this.panelSettings);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(800, 620);
            this.Name = "ManagerGUI";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Sheltered Mod Manager v0.6";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.grpDetails.ResumeLayout(false);
            this.grpDetails.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbPreview)).EndInit();
            this.grpAboutInspector.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            this.grpDevSettings.ResumeLayout(false);
            this.grpDevSettings.PerformLayout();
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
        private System.Windows.Forms.GroupBox grpDetails;
        private System.Windows.Forms.PictureBox pbPreview;
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.Label lblId;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblAuthors;
        private System.Windows.Forms.RichTextBox rtbDescription;
        private System.Windows.Forms.Label lblTags;
        private System.Windows.Forms.Label lblWebsite;
        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.Button btnApplySettings;
        private System.Windows.Forms.Button btnResetSettings;
        private System.Windows.Forms.CheckBox chkDevMode;
        private System.Windows.Forms.GroupBox grpAboutInspector;
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
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label lblDependsOn;
        private System.Windows.Forms.Panel panelSettings;
        private System.Windows.Forms.GroupBox grpDevSettings;
        private System.Windows.Forms.ComboBox cmbLogLevel;
        private System.Windows.Forms.CheckedListBox clbLogCategories;
        private System.Windows.Forms.CheckBox chkIgnoreOrderChecks;
        private System.Windows.Forms.Label lblLogLevel;
        private System.Windows.Forms.Label lblLogCategories;
    }
}
