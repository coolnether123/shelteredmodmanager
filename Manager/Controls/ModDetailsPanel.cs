using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Core.Models;

namespace Manager.Controls
{
    /// <summary>
    /// Delegate for string path events (requires custom delegate for .NET 3.5)
    /// </summary>
    public delegate void PathEventHandler(object sender, string path);

    /// <summary>
    /// Panel displaying details about a selected mod.
    /// Shows preview image, metadata, description, and status.
    /// </summary>
    public class ModDetailsPanel : UserControl
    {
        // Header section
        private PictureBox _previewImage;
        private Label _nameLabel;
        private Label _versionLabel;
        private Label _authorsLabel;
        private Label _idLabel;

        // Info section
        private Label _dependsOnLabel;
        private Label _dependsOnValue;
        private Label _modApiLabel;
        private Label _modApiValue;
        private Label _tagsLabel;
        private Label _tagsValue;
        private LinkLabel _websiteLink;
        private Label _descLabel;
        private Panel _separator;

        // Description
        private RichTextBox _descriptionBox;

        // Actions
        private Button _openFolderButton;
        private Panel _placeholderPanel;
        private Label _placeholderLabel;

        private ModItem _currentMod;
        private bool _isDarkMode = false;
        private string _installedModApiVersion;

        public event PathEventHandler OpenFolderClicked;
        public event PathEventHandler WebsiteClicked;

        public string InstalledModApiVersion
        {
            get { return _installedModApiVersion; }
            set { _installedModApiVersion = value; }
        }

        public ModDetailsPanel()
        {
            InitializeComponent();
            ShowPlaceholder();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.MinimumSize = new Size(300, 400);
            this.Padding = new Padding(12);

            // Create placeholder panel
            _placeholderPanel = new Panel();
            _placeholderPanel.Dock = DockStyle.Fill;
            _placeholderPanel.Visible = true;

            _placeholderLabel = new Label();
            _placeholderLabel.Text = "Select a mod to view details";
            _placeholderLabel.Font = new Font("Segoe UI", 11f);
            _placeholderLabel.ForeColor = Color.Gray;
            _placeholderLabel.AutoSize = false;
            _placeholderLabel.TextAlign = ContentAlignment.MiddleCenter;
            _placeholderLabel.Dock = DockStyle.Fill;
            _placeholderPanel.Controls.Add(_placeholderLabel);

            // Preview image
            _previewImage = new PictureBox();
            _previewImage.Size = new Size(180, 100);
            _previewImage.SizeMode = PictureBoxSizeMode.Zoom;
            _previewImage.Location = new Point(12, 10);
            _previewImage.BorderStyle = BorderStyle.FixedSingle;

            // Name - large title
            _nameLabel = new Label();
            _nameLabel.Font = new Font("Segoe UI", 14f, FontStyle.Bold);
            _nameLabel.AutoSize = false;
            _nameLabel.Location = new Point(12, 115);
            _nameLabel.Size = new Size(280, 30);
            _nameLabel.AutoEllipsis = true;

            // Version + Authors
            _versionLabel = new Label();
            _versionLabel.Font = new Font("Segoe UI", 10f);
            _versionLabel.AutoSize = true;
            _versionLabel.Location = new Point(12, 145);

            _authorsLabel = new Label();
            _authorsLabel.Font = new Font("Segoe UI", 9f);
            _authorsLabel.ForeColor = Color.Gray;
            _authorsLabel.AutoSize = false;
            _authorsLabel.Location = new Point(12, 165);
            _authorsLabel.Size = new Size(280, 20);
            _authorsLabel.AutoEllipsis = true;

            // ID (smaller, dimmed)
            _idLabel = new Label();
            _idLabel.Font = new Font("Segoe UI", 8f);
            _idLabel.ForeColor = Color.DimGray;
            _idLabel.AutoSize = true;
            _idLabel.Location = new Point(12, 183);

            // Separator
            _separator = new Panel();
            _separator.BackColor = Color.LightGray;
            _separator.Height = 1;
            _separator.Location = new Point(12, 203);
            _separator.Width = 280;

            // Depends On
            _dependsOnLabel = new Label();
            _dependsOnLabel.Text = "Dependencies:";
            _dependsOnLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _dependsOnLabel.AutoSize = true;
            _dependsOnLabel.Location = new Point(12, 213);

            _dependsOnValue = new Label();
            _dependsOnValue.Font = new Font("Segoe UI", 9f);
            _dependsOnValue.AutoSize = false;
            _dependsOnValue.Location = new Point(110, 213);
            _dependsOnValue.Size = new Size(180, 40);
            _dependsOnValue.AutoEllipsis = true;

            // ModAPI Status
            _modApiLabel = new Label();
            _modApiLabel.Text = "ModAPI:";
            _modApiLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _modApiLabel.AutoSize = true;
            _modApiLabel.Location = new Point(12, 253);

            _modApiValue = new Label();
            _modApiValue.Font = new Font("Segoe UI", 9f);
            _modApiValue.AutoSize = true;
            _modApiValue.Location = new Point(75, 253);

            // Tags
            _tagsLabel = new Label();
            _tagsLabel.Text = "Tags:";
            _tagsLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _tagsLabel.AutoSize = true;
            _tagsLabel.Location = new Point(12, 275);

            _tagsValue = new Label();
            _tagsValue.Font = new Font("Segoe UI", 9f);
            _tagsValue.AutoSize = false;
            _tagsValue.Location = new Point(55, 275);
            _tagsValue.Size = new Size(230, 20);
            _tagsValue.AutoEllipsis = true;

            // Website link
            _websiteLink = new LinkLabel();
            _websiteLink.Text = "Visit Website";
            _websiteLink.Font = new Font("Segoe UI", 9f);
            _websiteLink.AutoSize = true;
            _websiteLink.Location = new Point(12, 295);
            _websiteLink.Visible = false;
            _websiteLink.LinkClicked += WebsiteLink_LinkClicked;

            // Description
            _descLabel = new Label();
            _descLabel.Text = "Description";
            _descLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _descLabel.AutoSize = true;
            _descLabel.Location = new Point(12, 318);

            _descriptionBox = new RichTextBox();
            _descriptionBox.Font = new Font("Segoe UI", 9f);
            _descriptionBox.Location = new Point(12, 335);
            _descriptionBox.Size = new Size(280, 150);
            _descriptionBox.ReadOnly = true;
            _descriptionBox.Multiline = true;
            _descriptionBox.WordWrap = true;
            _descriptionBox.BorderStyle = BorderStyle.FixedSingle;
            _descriptionBox.ScrollBars = RichTextBoxScrollBars.ForcedVertical;

            // Open folder button
            _openFolderButton = new Button();
            _openFolderButton.Text = "Open Mod Folder";
            _openFolderButton.Font = new Font("Segoe UI", 9f);
            _openFolderButton.FlatStyle = FlatStyle.Flat;
            _openFolderButton.Size = new Size(140, 28);
            _openFolderButton.Location = new Point(12, 525);
            _openFolderButton.Cursor = Cursors.Hand;
            _openFolderButton.Click += OpenFolderButton_Click;

            // Add all controls
            this.Controls.Add(_placeholderPanel);
            this.Controls.Add(_previewImage);
            this.Controls.Add(_nameLabel);
            this.Controls.Add(_versionLabel);
            this.Controls.Add(_authorsLabel);
            this.Controls.Add(_idLabel);
            this.Controls.Add(_separator);
            this.Controls.Add(_dependsOnLabel);
            this.Controls.Add(_dependsOnValue);
            this.Controls.Add(_modApiLabel);
            this.Controls.Add(_modApiValue);
            this.Controls.Add(_tagsLabel);
            this.Controls.Add(_tagsValue);
            this.Controls.Add(_websiteLink);
            this.Controls.Add(_descLabel);
            this.Controls.Add(_descriptionBox);
            this.Controls.Add(_openFolderButton);

            this.ResumeLayout();
            this.Resize += ModDetailsPanel_Resize;
        }

        private void WebsiteLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_currentMod != null && !string.IsNullOrEmpty(_currentMod.Website))
            {
                if (WebsiteClicked != null)
                    WebsiteClicked(this, _currentMod.Website);
            }
        }

        private void OpenFolderButton_Click(object sender, EventArgs e)
        {
            if (_currentMod != null)
            {
                if (OpenFolderClicked != null)
                    OpenFolderClicked(this, _currentMod.RootPath);
            }
        }

        private void ModDetailsPanel_Resize(object sender, EventArgs e)
        {
            int contentWidth = this.Width - 24;
            
            _nameLabel.Width = contentWidth;
            _authorsLabel.Width = contentWidth;
            _dependsOnValue.Width = contentWidth - 110;
            _tagsValue.Width = contentWidth - 55;
            _descriptionBox.Width = contentWidth;
            _descriptionBox.Height = Math.Max(50, this.Height - 395);
            _openFolderButton.Top = this.Height - 42;
        }

        /// <summary>
        /// Display details for a mod
        /// </summary>
        public void ShowMod(ModItem mod)
        {
            if (mod == null)
            {
                ShowPlaceholder();
                return;
            }

            _currentMod = mod;
            HidePlaceholder();

            // Preview image
            try
            {
                if (!string.IsNullOrEmpty(mod.PreviewPath) && File.Exists(mod.PreviewPath))
                {
                    _previewImage.ImageLocation = mod.PreviewPath;
                    _previewImage.Visible = true;
                }
                else
                {
                    _previewImage.Image = null;
                    _previewImage.Visible = false;
                }
            }
            catch
            {
                _previewImage.Visible = false;
            }

            // Basic info
            _nameLabel.Text = mod.DisplayName;
            _versionLabel.Text = "v" + mod.Version;
            
            if (mod.Authors != null && mod.Authors.Length > 0)
            {
                _authorsLabel.Text = "by " + string.Join(", ", mod.Authors);
            }
            else
            {
                _authorsLabel.Text = "Author unknown";
            }
            
            _idLabel.Text = "ID: " + mod.Id;

            // Dependencies
            if (mod.DependsOn != null && mod.DependsOn.Length > 0)
            {
                _dependsOnValue.Text = string.Join(", ", mod.DependsOn);
            }
            else
            {
                _dependsOnValue.Text = "None";
            }

            // ModAPI compatibility
            UpdateModApiStatus(mod);

            // Tags
            if (mod.Tags != null && mod.Tags.Length > 0)
            {
                _tagsValue.Text = string.Join(", ", mod.Tags);
            }
            else
            {
                _tagsValue.Text = "None";
            }

            // Website
            _websiteLink.Visible = !string.IsNullOrEmpty(mod.Website);

            // Description
            if (!string.IsNullOrEmpty(mod.Description))
            {
                _descriptionBox.Text = mod.Description;
            }
            else
            {
                _descriptionBox.Text = "No description provided.";
            }
        }

        private void UpdateModApiStatus(ModItem mod)
        {
            if (!string.IsNullOrEmpty(mod.RequiredModApiVersion))
            {
                string statusIcon = mod.IsModApiCompatible ? "OK" : "!";
                Color statusColor = mod.IsModApiCompatible 
                    ? (_isDarkMode ? Color.LightGreen : Color.Green)
                    : Color.Orange;
                
                _modApiValue.Text = mod.RequiredModApiVersion + " " + statusIcon;
                _modApiValue.ForeColor = statusColor;

                if (!mod.IsModApiCompatible && !string.IsNullOrEmpty(_installedModApiVersion))
                {
                    _modApiValue.Text += " (Installed: " + _installedModApiVersion + ")";
                }
            }
            else
            {
                _modApiValue.Text = _installedModApiVersion ?? "Unknown";
                _modApiValue.ForeColor = _isDarkMode ? Color.LightGray : Color.Gray;
            }
        }

        private void ShowPlaceholder()
        {
            _currentMod = null;
            _placeholderPanel.Visible = true;
            _placeholderPanel.BringToFront();
            
            foreach (Control c in this.Controls)
            {
                if (c != _placeholderPanel)
                    c.Visible = false;
            }
        }

        private void HidePlaceholder()
        {
            _placeholderPanel.Visible = false;
            _placeholderPanel.SendToBack();
            
            // Show all other controls
            _previewImage.Visible = true;
            _nameLabel.Visible = true;
            _versionLabel.Visible = true;
            _authorsLabel.Visible = true;
            _idLabel.Visible = true;
            _separator.Visible = true;
            _dependsOnLabel.Visible = true;
            _dependsOnValue.Visible = true;
            _modApiLabel.Visible = true;
            _modApiValue.Visible = true;
            _tagsLabel.Visible = true;
            _tagsValue.Visible = true;
            _websiteLink.Visible = true;
            _descLabel.Visible = true;
            _descriptionBox.Visible = true;
            _openFolderButton.Visible = true;
        }

        /// <summary>
        /// Apply theme colors
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;

            if (isDark)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                _placeholderLabel.ForeColor = Color.Gray;
                _nameLabel.ForeColor = Color.White;
                _versionLabel.ForeColor = Color.LightGray;
                _authorsLabel.ForeColor = Color.Gray;
                _idLabel.ForeColor = Color.DimGray;
                _dependsOnLabel.ForeColor = Color.White;
                _dependsOnValue.ForeColor = Color.LightGray;
                _modApiLabel.ForeColor = Color.White;
                _tagsLabel.ForeColor = Color.White;
                _tagsValue.ForeColor = Color.LightGray;
                _descLabel.ForeColor = Color.White;
                _descriptionBox.BackColor = Color.FromArgb(60, 60, 60);
                _descriptionBox.ForeColor = Color.White;
                _openFolderButton.BackColor = Color.FromArgb(60, 60, 60);
                _openFolderButton.ForeColor = Color.White;
                _websiteLink.LinkColor = Color.LightBlue;
            }
            else
            {
                this.BackColor = SystemColors.Control;
                _placeholderLabel.ForeColor = Color.Gray;
                _nameLabel.ForeColor = SystemColors.ControlText;
                _versionLabel.ForeColor = SystemColors.ControlText;
                _authorsLabel.ForeColor = Color.Gray;
                _idLabel.ForeColor = Color.DimGray;
                _dependsOnLabel.ForeColor = SystemColors.ControlText;
                _dependsOnValue.ForeColor = SystemColors.ControlText;
                _modApiLabel.ForeColor = SystemColors.ControlText;
                _tagsLabel.ForeColor = SystemColors.ControlText;
                _tagsValue.ForeColor = SystemColors.ControlText;
                _descLabel.ForeColor = SystemColors.ControlText;
                _descriptionBox.BackColor = SystemColors.Window;
                _descriptionBox.ForeColor = SystemColors.WindowText;
                _openFolderButton.BackColor = SystemColors.Control;
                _openFolderButton.ForeColor = SystemColors.ControlText;
                _websiteLink.LinkColor = SystemColors.HotTrack;
            }

            // Update ModAPI status color if showing mod
            if (_currentMod != null)
                UpdateModApiStatus(_currentMod);
        }
    }
}
