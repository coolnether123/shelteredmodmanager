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
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.label4 = new System.Windows.Forms.Label();
            this.uiGamePath = new System.Windows.Forms.TextBox();
            this.uiLaunchButton = new System.Windows.Forms.Button();
            this.uiLocateButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.doorstopLink = new System.Windows.Forms.LinkLabel();
            this.shelteredLink = new System.Windows.Forms.LinkLabel();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.uiAvailbleModsListView = new System.Windows.Forms.ListBox();
            this.uiInstalledModsListView = new System.Windows.Forms.ListBox();
            this.uiAvailableLabel = new System.Windows.Forms.Label();
            this.uiInstaledLabel = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.uiModsPath = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = global::Manager.Properties.Resources.logo;
            this.pictureBox1.Location = new System.Drawing.Point(28, 12);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(458, 190);
            this.pictureBox1.TabIndex = 9;
            this.pictureBox1.TabStop = false;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Location = new System.Drawing.Point(13, 221);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(492, 279);
            this.tabControl1.TabIndex = 10;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
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
            this.tabPage1.Size = new System.Drawing.Size(484, 253);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Game";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 18);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(151, 13);
            this.label4.TabIndex = 14;
            this.label4.Text = "Sheltered-Path (Sheltered.exe)";
            // 
            // uiGamePath
            // 
            this.uiGamePath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.uiGamePath.Location = new System.Drawing.Point(71, 36);
            this.uiGamePath.Name = "uiGamePath";
            this.uiGamePath.Size = new System.Drawing.Size(398, 20);
            this.uiGamePath.TabIndex = 12;
            this.uiGamePath.Text = "None";
            this.uiGamePath.TextChanged += new System.EventHandler(this.uiGamePath_TextChanged_1);
            // 
            // uiLaunchButton
            // 
            this.uiLaunchButton.Location = new System.Drawing.Point(10, 224);
            this.uiLaunchButton.Name = "uiLaunchButton";
            this.uiLaunchButton.Size = new System.Drawing.Size(464, 23);
            this.uiLaunchButton.TabIndex = 11;
            this.uiLaunchButton.Text = "Launch";
            this.uiLaunchButton.UseVisualStyleBackColor = true;
            this.uiLaunchButton.Click += new System.EventHandler(this.onLaunchClicked);
            // 
            // uiLocateButton
            // 
            this.uiLocateButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uiLocateButton.Location = new System.Drawing.Point(10, 35);
            this.uiLocateButton.Name = "uiLocateButton";
            this.uiLocateButton.Size = new System.Drawing.Size(56, 22);
            this.uiLocateButton.TabIndex = 10;
            this.uiLocateButton.Text = "Locate";
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
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.label2);
            this.tabPage2.Controls.Add(this.doorstopLink);
            this.tabPage2.Controls.Add(this.shelteredLink);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(484, 253);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "About";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(8, 56);
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
            this.doorstopLink.Location = new System.Drawing.Point(8, 73);
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
            this.tabPage3.Size = new System.Drawing.Size(484, 253);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Mods";
            // 
            // uiAvailbleModsListView
            // 
            this.uiAvailbleModsListView.FormattingEnabled = true;
            this.uiAvailbleModsListView.Location = new System.Drawing.Point(11, 31);
            this.uiAvailbleModsListView.Name = "uiAvailbleModsListView";
            this.uiAvailbleModsListView.Size = new System.Drawing.Size(173, 199);
            this.uiAvailbleModsListView.TabIndex = 0;
            // 
            // uiInstalledModsListView
            // 
            this.uiInstalledModsListView.FormattingEnabled = true;
            this.uiInstalledModsListView.Location = new System.Drawing.Point(300, 31);
            this.uiInstalledModsListView.Name = "uiInstalledModsListView";
            this.uiInstalledModsListView.Size = new System.Drawing.Size(169, 199);
            this.uiInstalledModsListView.TabIndex = 1;
            this.uiInstalledModsListView.SelectedIndexChanged += new System.EventHandler(this.uiInstalledModsListView_SelectedIndexChanged);
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
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(190, 207);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(104, 23);
            this.button2.TabIndex = 5;
            this.button2.Text = "<=";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // uiModsPath
            // 
            this.uiModsPath.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.uiModsPath.Location = new System.Drawing.Point(11, 100);
            this.uiModsPath.Name = "uiModsPath";
            this.uiModsPath.Size = new System.Drawing.Size(458, 20);
            this.uiModsPath.TabIndex = 15;
            this.uiModsPath.Text = "None";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(8, 84);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(58, 13);
            this.label5.TabIndex = 16;
            this.label5.Text = "Mods-Path";
            // 
            // ManagerGUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(514, 512);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.pictureBox1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "ManagerGUI";
            this.Text = "Sheltered Mod Manager";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.PictureBox pictureBox1;
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
    }
}

