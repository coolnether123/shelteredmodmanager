using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Core.Models;
using Manager.Core.Services;

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
        private const int InfoLabelColumnWidth = 104;

        // Header section
        private PictureBox _previewImage;
        private Label _nameLabel;
        private Label _versionLabel;
        private Label _authorsLabel;
        private Label _idLabel;
        private bool _showTechnicalDetails;
        private bool _placeholderShown;

        // Info section
        private Label _dependsOnLabel;
        private Label _dependsOnValue;
        private Label _modApiLabel;
        private Label _modApiValue;
        private Label _tagsLabel;
        private Label _tagsValue;
        private LinkLabel _websiteLink;
        private Label _nexusStatusLabel;
        private LinkLabel _nexusLink;
        private Button _nexusUpdateButton;
        private Button _nexusStableButton;
        private Label _descLabel;
        private Panel _separator;

        // Description
        private RichTextBox _descriptionBox;

        // Actions
        private Button _openFolderButton;
        private TableLayoutPanel _placeholderPanel;
        private Label _placeholderTitle;
        private Label _placeholderLabel;

        private ModItem _currentMod;
        private string _installedModApiVersion;
        private bool _showNexusActionButtons = false;

        public event PathEventHandler OpenFolderClicked;
        public event PathEventHandler WebsiteClicked;
        public event EventHandler NexusUpdateRequested;
        public event EventHandler NexusStableRequested;

        public string InstalledModApiVersion
        {
            get { return _installedModApiVersion; }
            set { _installedModApiVersion = value; }
        }

        public bool ShowNexusActionButtons
        {
            get { return _showNexusActionButtons; }
            set
            {
                _showNexusActionButtons = value;
                if (_currentMod != null)
                {
                    UpdateNexusStatus(_currentMod);
                    LayoutDetailContent();
                }
            }
        }

        public ModDetailsPanel()
        {
            InitializeComponent();
            ShowPlaceholder();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.MinimumSize = Size.Empty;
            this.Padding = new Padding(12);
            this.AutoScroll = true;
            this.ResizeRedraw = true;

            // Placeholder: shown when nothing is selected, or with a status message.
            _placeholderPanel = new TableLayoutPanel();
            _placeholderPanel.Dock = DockStyle.Fill;
            _placeholderPanel.Visible = true;
            _placeholderShown = true;
            _placeholderPanel.ColumnCount = 1;
            _placeholderPanel.RowCount = 4;
            _placeholderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _placeholderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 40f));
            _placeholderPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _placeholderPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _placeholderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 60f));

            _placeholderTitle = new Label();
            _placeholderTitle.Font = ManagerTheme.Display(12f);
            _placeholderTitle.AutoSize = true;
            _placeholderTitle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _placeholderTitle.TextAlign = ContentAlignment.MiddleCenter;
            _placeholderTitle.UseMnemonic = false;
            _placeholderTitle.Margin = new Padding(12, 0, 12, 6);

            _placeholderLabel = new Label();
            _placeholderLabel.Text = "Select a mod to view details";
            _placeholderLabel.Font = ManagerTheme.UiFont(9.5f);
            _placeholderLabel.AutoSize = true;
            _placeholderLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _placeholderLabel.TextAlign = ContentAlignment.MiddleCenter;
            _placeholderLabel.UseMnemonic = false;
            _placeholderLabel.Margin = new Padding(12, 0, 12, 0);
            _placeholderPanel.Controls.Add(_placeholderTitle, 0, 1);
            _placeholderPanel.Controls.Add(_placeholderLabel, 0, 2);

            // Preview image
            _previewImage = new PictureBox();
            _previewImage.Size = new Size(180, 100);
            _previewImage.SizeMode = PictureBoxSizeMode.Zoom;
            _previewImage.Location = new Point(12, 10);
            _previewImage.BorderStyle = BorderStyle.FixedSingle;

            // Name - large title
            _nameLabel = new Label();
            _nameLabel.Font = ManagerTheme.Display(14f);
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
            _dependsOnLabel.Font = ManagerTheme.Semibold(9f);
            _dependsOnLabel.AutoSize = true;
            _dependsOnLabel.Location = new Point(12, 213);

            _dependsOnValue = new Label();
            _dependsOnValue.Font = new Font("Segoe UI", 9f);
            _dependsOnValue.AutoSize = false;
            _dependsOnValue.Location = new Point(12 + InfoLabelColumnWidth, 213);
            _dependsOnValue.Size = new Size(180, 40);
            _dependsOnValue.AutoEllipsis = true;

            // Compatibility status
            _modApiLabel = new Label();
            _modApiLabel.Text = "Compatibility:";
            _modApiLabel.Font = ManagerTheme.Semibold(9f);
            _modApiLabel.AutoSize = true;
            _modApiLabel.Location = new Point(12, 253);

            _modApiValue = new Label();
            _modApiValue.Font = new Font("Segoe UI", 9f);
            _modApiValue.AutoSize = false;
            _modApiValue.Location = new Point(12 + InfoLabelColumnWidth, 253);
            _modApiValue.Size = new Size(210, 22);

            // Tags
            _tagsLabel = new Label();
            _tagsLabel.Text = "Tags:";
            _tagsLabel.Font = ManagerTheme.Semibold(9f);
            _tagsLabel.AutoSize = true;
            _tagsLabel.Location = new Point(12, 275);

            _tagsValue = new Label();
            _tagsValue.Font = new Font("Segoe UI", 9f);
            _tagsValue.AutoSize = false;
            _tagsValue.Location = new Point(12 + InfoLabelColumnWidth, 275);
            _tagsValue.Size = new Size(230, 20);
            _tagsValue.AutoEllipsis = true;

            // Website link
            _websiteLink = new ThemedLinkLabel();
            _websiteLink.Text = "Visit website";
            _websiteLink.Font = new Font("Segoe UI", 9f);
            _websiteLink.AutoSize = true;
            _websiteLink.Location = new Point(12, 295);
            _websiteLink.Visible = false;
            _websiteLink.LinkClicked += WebsiteLink_LinkClicked;

            _nexusStatusLabel = new Label();
            _nexusStatusLabel.Font = ManagerTheme.Semibold(8.5f);
            _nexusStatusLabel.AutoSize = true;
            _nexusStatusLabel.Location = new Point(12, 315);

            _nexusLink = new ThemedLinkLabel();
            _nexusLink.Text = "Open Nexus page";
            _nexusLink.Font = new Font("Segoe UI", 9f);
            _nexusLink.AutoSize = true;
            _nexusLink.Location = new Point(12, 333);
            _nexusLink.Visible = false;
            _nexusLink.LinkClicked += NexusLink_LinkClicked;

            _nexusUpdateButton = new ActionButton { Kind = ActionButtonKind.Primary };
            _nexusUpdateButton.Text = "Update from Nexus";
            _nexusUpdateButton.AutoSize = true;
            _nexusUpdateButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _nexusUpdateButton.Visible = false;
            _nexusUpdateButton.Cursor = Cursors.Hand;
            _nexusUpdateButton.Click += NexusUpdateButton_Click;

            _nexusStableButton = new ActionButton { Kind = ActionButtonKind.Secondary };
            _nexusStableButton.Text = "Revert to stable";
            _nexusStableButton.AutoSize = true;
            _nexusStableButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _nexusStableButton.Visible = false;
            _nexusStableButton.Cursor = Cursors.Hand;
            _nexusStableButton.Click += NexusStableButton_Click;

            // Description
            _descLabel = new Label();
            _descLabel.Text = "Description";
            _descLabel.Font = ManagerTheme.Semibold(9f);
            _descLabel.AutoSize = true;
            _descLabel.Location = new Point(12, 356);

            _descriptionBox = new RichTextBox();
            _descriptionBox.Font = new Font("Segoe UI", 9f);
            _descriptionBox.Location = new Point(12, 375);
            _descriptionBox.Size = new Size(280, 150);
            _descriptionBox.ReadOnly = true;
            _descriptionBox.Multiline = true;
            _descriptionBox.WordWrap = true;
            _descriptionBox.BorderStyle = BorderStyle.None;
            _descriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            // Open folder button
            _openFolderButton = new ActionButton { Kind = ActionButtonKind.Secondary };
            _openFolderButton.Text = "Open mod folder";
            _openFolderButton.AutoSize = true;
            _openFolderButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
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
            this.Controls.Add(_nexusStatusLabel);
            this.Controls.Add(_nexusLink);
            this.Controls.Add(_nexusUpdateButton);
            this.Controls.Add(_nexusStableButton);
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

        private void NexusLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_currentMod == null)
                return;

            string url = _currentMod.NexusPageUrl;
            if (string.IsNullOrEmpty(url) && _currentMod.HasNexusReference)
            {
                url = "https://www.nexusmods.com/" + _currentMod.NexusGameDomain + "/mods/" + _currentMod.NexusModId;
            }

            if (!string.IsNullOrEmpty(url) && WebsiteClicked != null)
                WebsiteClicked(this, url);
        }

        private void NexusUpdateButton_Click(object sender, EventArgs e)
        {
            if (NexusUpdateRequested != null)
                NexusUpdateRequested(this, EventArgs.Empty);
        }

        private void NexusStableButton_Click(object sender, EventArgs e)
        {
            if (NexusStableRequested != null)
                NexusStableRequested(this, EventArgs.Empty);
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
            LayoutDetailContent();
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
            AutoScrollPosition = Point.Empty;
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
            _versionLabel.Text = FormatVersionDisplay(mod.Version);
            
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
                string[] names = new string[mod.DependsOn.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    string dependency = mod.DependsOn[i] ?? string.Empty;
                    names[i] = LoadOrderService.IsBundledDependency(dependency.Trim())
                        ? "Harmony (included with SMM)"
                        : dependency;
                }
                _dependsOnValue.Text = string.Join(", ", names);
            }
            else
            {
                _dependsOnValue.Text = "None";
            }

            // Compatibility
            UpdateModApiStatus(mod);

            // Tags
            if (mod.Tags != null && mod.Tags.Length > 0)
            {
                _tagsValue.Text = string.Join(", ", mod.Tags);
                _tagsLabel.Visible = true;
                _tagsValue.Visible = true;
            }
            else
            {
                _tagsValue.Text = string.Empty;
                _tagsLabel.Visible = false;
                _tagsValue.Visible = false;
            }

            // Website
            _websiteLink.Visible = ShouldShowWebsiteLink(mod);
            _websiteLink.Text = GetWebsiteLinkText(mod);
            UpdateNexusStatus(mod);

            // Description
            if (!string.IsNullOrEmpty(mod.Description))
            {
                _descriptionBox.Text = BuildDescriptionText(mod);
            }
            else
            {
                _descriptionBox.Text = BuildDescriptionText(mod);
            }

            LayoutDetailContent();
        }

        private void UpdateModApiStatus(ModItem mod)
        {
            if (mod.ApiCompatibility != null && mod.ApiCompatibility.Requirements.Count > 0)
            {
                string statusText = GetCompatibilityStatusText(mod.ApiCompatibility);
                Color statusColor;
                if (mod.ApiCompatibility.Severity == ApiCompatibilitySeverity.Error)
                    statusColor = ManagerTheme.Current.Danger;
                else if (mod.ApiCompatibility.Severity == ApiCompatibilitySeverity.Warning)
                    statusColor = ManagerTheme.Current.Warning;
                else
                    statusColor = ManagerTheme.Current.Success;

                _modApiValue.Text = statusText;
                _modApiValue.ForeColor = statusColor;
            }
            else if (!string.IsNullOrEmpty(mod.RequiredModApiVersion))
            {
                string statusText = mod.IsModApiCompatible ? "Compatible" : "Needs mod update";
                Color statusColor = mod.IsModApiCompatible
                    ? (ManagerTheme.Current.Success)
                    : ManagerTheme.Current.Danger;

                _modApiValue.Text = statusText;
                _modApiValue.ForeColor = statusColor;
            }
            else
            {
                _modApiValue.Text = "Not declared";
                _modApiValue.ForeColor = ManagerTheme.Current.TextMuted;
            }
        }

        private static string GetCompatibilityStatusText(ApiCompatibilityReport report)
        {
            if (report == null)
                return "Not declared";

            if (report.Severity == ApiCompatibilitySeverity.Error)
                return "Needs mod update";

            if (report.Severity == ApiCompatibilitySeverity.Warning)
                return "May need update";

            return "Compatible";
        }

        private static string BuildDescriptionText(ModItem mod)
        {
            string description = !string.IsNullOrEmpty(mod.Description)
                ? mod.Description.Trim()
                : "No description provided.";

            if (!string.IsNullOrEmpty(mod.StatusMessage))
                description += Environment.NewLine + Environment.NewLine + "Status: " + mod.StatusMessage;

            if (mod.ApiCompatibility == null || mod.ApiCompatibility.Messages.Count == 0)
                return description;

            string text = description + Environment.NewLine + Environment.NewLine + "Compatibility:";
            for (int i = 0; i < mod.ApiCompatibility.Messages.Count; i++)
                text += Environment.NewLine + "- " + mod.ApiCompatibility.Messages[i];

            return text;
        }

        private void UpdateNexusStatus(ModItem mod)
        {
            _nexusLink.Visible = false;
            _nexusUpdateButton.Visible = false;
            _nexusStableButton.Visible = false;

            if (!mod.HasNexusReference)
            {
                _nexusStatusLabel.Text = "Nexus: Not linked";
                _nexusStatusLabel.ForeColor = ManagerTheme.Current.TextMuted;
                return;
            }

            string url = mod.NexusPageUrl;
            if (string.IsNullOrEmpty(url))
            {
                url = "https://www.nexusmods.com/" + mod.NexusGameDomain + "/mods/" + mod.NexusModId;
                mod.NexusPageUrl = url;
            }

            _nexusLink.Visible = !string.IsNullOrEmpty(url);

            if (mod.HasUpdateAvailable)
            {
                string latest = string.IsNullOrEmpty(mod.NexusRemoteVersion) ? "new release" : mod.NexusRemoteVersion;
                NexusReleaseChannel channel = NexusReleaseClassifier.ClassifyVersion(mod.NexusRemoteVersion);
                string channelLabel = channel == NexusReleaseChannel.Stable
                    ? "Update"
                    : ToTitleCase(NexusReleaseClassifier.GetDisplayLabel(channel)) + " update";
                _nexusStatusLabel.Text = "Nexus: " + channelLabel + " available (" + latest + ")";
                _nexusStatusLabel.ForeColor = ManagerTheme.Current.AccentText;
                if (_showNexusActionButtons)
                {
                    _nexusUpdateButton.Text = channel == NexusReleaseChannel.Stable
                        ? "Update from Nexus"
                        : "Update to " + ToTitleCase(NexusReleaseClassifier.GetDisplayLabel(channel));
                    _nexusUpdateButton.Visible = true;
                }
                if (_showNexusActionButtons && NexusReleaseClassifier.IsPrerelease(mod.Version))
                    _nexusStableButton.Visible = true;
                return;
            }

            if (_showNexusActionButtons && NexusReleaseClassifier.IsPrerelease(mod.Version))
                _nexusStableButton.Visible = true;

            bool installedMod = !string.IsNullOrEmpty(mod.RootPath);
            if (!string.IsNullOrEmpty(mod.NexusRemoteVersion) && installedMod)
            {
                _nexusStatusLabel.Text = "Nexus: Installed and current (" + mod.NexusRemoteVersion + ")";
                _nexusStatusLabel.ForeColor = ManagerTheme.Current.Success;
                return;
            }

            if (!installedMod && !string.IsNullOrEmpty(mod.NexusRemoteVersion))
            {
                _nexusStatusLabel.Text = "Available on Nexus (" + mod.NexusRemoteVersion + ")";
                _nexusStatusLabel.ForeColor = ManagerTheme.Current.TextMuted;
                return;
            }

            _nexusStatusLabel.Text = "Nexus: Linked (" + mod.NexusGameDomain + "/" + mod.NexusModId + ")";
            _nexusStatusLabel.ForeColor = ManagerTheme.Current.TextMuted;
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length == 1)
                return value.ToUpperInvariant();

            return value.Substring(0, 1).ToUpperInvariant() + value.Substring(1);
        }

        private static string FormatVersionDisplay(string version)
        {
            string value = (version ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(value))
                return "Unknown version";

            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                return value;

            return "v" + value;
        }

        /// <summary>
        /// Shows a titled status message (empty lists, failed refreshes) instead of mod details.
        /// </summary>
        public void ShowMessage(string title, string message)
        {
            ShowPlaceholder();
            _placeholderTitle.Text = title ?? string.Empty;
            _placeholderTitle.Visible = !string.IsNullOrEmpty(title);
            _placeholderLabel.Text = message ?? string.Empty;
        }

        private void ShowPlaceholder()
        {
            _currentMod = null;
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
            _placeholderTitle.Text = "Nothing selected";
            _placeholderTitle.Visible = true;
            _placeholderLabel.Text = "Select a mod to see its details.";
            _placeholderPanel.Visible = true;
            _placeholderShown = true;
            _placeholderPanel.BringToFront();
            
            foreach (Control c in this.Controls)
            {
                if (c != _placeholderPanel)
                    c.Visible = false;
            }

            // Re-evaluate scrolling now the detail controls are hidden, so a
            // scrollbar from the previous mod does not linger on the message.
            AutoScroll = false;
            AutoScroll = true;
        }

        private void HidePlaceholder()
        {
            _placeholderPanel.Visible = false;
            _placeholderShown = false;
            _placeholderPanel.SendToBack();
            
            // Show all other controls
            _previewImage.Visible = true;
            _nameLabel.Visible = true;
            _versionLabel.Visible = true;
            _authorsLabel.Visible = true;
            _idLabel.Visible = _showTechnicalDetails;
            _separator.Visible = true;
            _dependsOnLabel.Visible = true;
            _dependsOnValue.Visible = true;
            _modApiLabel.Visible = true;
            _modApiValue.Visible = true;
            _tagsLabel.Visible = true;
            _tagsValue.Visible = true;
            _websiteLink.Visible = true;
            _nexusStatusLabel.Visible = true;
            _nexusLink.Visible = true;
            _nexusUpdateButton.Visible = true;
            _nexusStableButton.Visible = true;
            _descLabel.Visible = true;
            _descriptionBox.Visible = true;
            _openFolderButton.Visible = !string.IsNullOrEmpty(_currentMod != null ? _currentMod.RootPath : string.Empty);

            LayoutDetailContent();
        }

        private static bool ShouldShowWebsiteLink(ModItem mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Website))
                return false;

            string website = NormalizeUrl(mod.Website);
            string nexus = NormalizeUrl(mod.NexusPageUrl);
            if (!string.IsNullOrEmpty(website) && !string.IsNullOrEmpty(nexus) && string.Equals(website, nexus, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static string GetWebsiteLinkText(ModItem mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Website))
                return "Visit website";

            string website = mod.Website.Trim();
            if (website.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Open GitHub page";

            return "Visit website";
        }

        private static string NormalizeUrl(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Trim().TrimEnd('/').ToLowerInvariant();
        }

        /// <summary>Shows the mod ID line; on in developer mode only.</summary>
        public bool ShowTechnicalDetails
        {
            get { return _showTechnicalDetails; }
            set
            {
                _showTechnicalDetails = value;
                if (!_placeholderShown)
                    _idLabel.Visible = value;
                LayoutDetailContent();
            }
        }

        private void LayoutDetailContent()
        {
            // A flag, not Visible: Visible reads false whenever the tab itself is hidden.
            if (_placeholderShown)
            {
                // The placeholder fills the panel; a size left from the last mod would add a scrollbar.
                AutoScrollMinSize = Size.Empty;
                return;
            }

            // Positions include the scroll offset so a short panel scrolls instead of overlapping.
            int scrollY = AutoScrollPosition.Y;
            int left = 12;
            int top = 10 + scrollY;
            int contentWidth = Math.Max(120, this.ClientSize.Width - 24);

            bool showPreview = _previewImage.Visible && contentWidth >= 430;
            int previewWidth = 140;
            int previewHeight = 84;
            int gap = 12;

            if (showPreview)
            {
                _previewImage.Size = new Size(previewWidth, previewHeight);
                _previewImage.Location = new Point(left + contentWidth - previewWidth, top);
            }

            int textRight = showPreview ? (_previewImage.Left - gap) : (left + contentWidth);
            int textWidth = Math.Max(140, textRight - left);

            _nameLabel.Location = new Point(left, top);
            _nameLabel.Width = textWidth;

            _versionLabel.Location = new Point(left, top + 34);
            _authorsLabel.Location = new Point(left, top + 56);
            _authorsLabel.Width = textWidth;
            _idLabel.Location = new Point(left, top + 76);

            int separatorY = (_idLabel.Visible ? _idLabel.Bottom : _authorsLabel.Bottom) + 10;
            if (showPreview)
                separatorY = Math.Max(separatorY, _previewImage.Bottom + 10);

            _separator.Location = new Point(left, separatorY);
            _separator.Width = contentWidth;

            int infoY = separatorY + 10;
            _dependsOnLabel.Location = new Point(left, infoY);
            _dependsOnValue.Location = new Point(left + InfoLabelColumnWidth, infoY);
            _dependsOnValue.Width = Math.Max(70, contentWidth - InfoLabelColumnWidth);
            var measuredDepends = TextRenderer.MeasureText(
                _dependsOnValue.Text ?? string.Empty,
                _dependsOnValue.Font,
                new Size(_dependsOnValue.Width, 120),
                TextFormatFlags.WordBreak);
            _dependsOnValue.Height = Math.Max(20, Math.Min(44, measuredDepends.Height + 2));

            int modApiY = _dependsOnValue.Bottom + 8;
            _modApiLabel.Location = new Point(left, modApiY);
            _modApiValue.Location = new Point(left + InfoLabelColumnWidth, modApiY);
            _modApiValue.Width = Math.Max(80, contentWidth - InfoLabelColumnWidth);
            var measuredModApi = TextRenderer.MeasureText(
                _modApiValue.Text ?? string.Empty,
                _modApiValue.Font,
                new Size(_modApiValue.Width, 200),
                TextFormatFlags.WordBreak);
            _modApiValue.Height = Math.Max(22, Math.Min(64, measuredModApi.Height + 2));

            int tagsY = Math.Max(infoY + 56, _modApiValue.Bottom + 8);
            if (_tagsLabel.Visible && _tagsValue.Visible)
            {
                _tagsLabel.Location = new Point(left, tagsY);
                _tagsValue.Location = new Point(left + InfoLabelColumnWidth, tagsY);
                _tagsValue.Width = Math.Max(70, contentWidth - InfoLabelColumnWidth);
            }

            int linksY = (_tagsLabel.Visible && _tagsValue.Visible) ? (_tagsValue.Bottom + 4) : (_modApiValue.Bottom + 8);
            if (_websiteLink.Visible)
                _websiteLink.Location = new Point(left, linksY);

            int nexusStatusY = _websiteLink.Visible ? (linksY + 24) : linksY;
            _nexusStatusLabel.Location = new Point(left, nexusStatusY);

            int nexusLinkY = nexusStatusY + 24;
            if (_nexusLink.Visible)
                _nexusLink.Location = new Point(left, nexusLinkY);

            int nextY = _nexusLink.Visible ? (nexusLinkY + 28) : (nexusStatusY + 28);
            if (_nexusUpdateButton.Visible || _nexusStableButton.Visible)
            {
                _nexusUpdateButton.Location = new Point(left, nextY);
                _nexusStableButton.Location = new Point(_nexusUpdateButton.Right + 10, nextY);
                nextY += 38;
            }

            int descTitleY = nextY;
            _descLabel.Location = new Point(left, descTitleY);

            _descriptionBox.Location = new Point(left, descTitleY + 20);
            _descriptionBox.Width = contentWidth;

            const int minimumDescriptionHeight = 44;
            int fitTop = this.ClientSize.Height - 42 + scrollY;
            int descriptionMinimum = minimumDescriptionHeight;
            // A few pixels short: squeeze the description rather than add a scrollbar.
            int shortfall = _descriptionBox.Top + descriptionMinimum + 10 - fitTop;
            if (shortfall > 0 && shortfall <= 16)
                descriptionMinimum -= shortfall;
            int buttonTop = Math.Max(fitTop, _descriptionBox.Top + descriptionMinimum + 10);
            _openFolderButton.Left = left;
            _openFolderButton.Top = buttonTop;
            _descriptionBox.Height = Math.Max(descriptionMinimum, buttonTop - _descriptionBox.Top - 10);
            AutoScrollMinSize = new Size(0, buttonTop - scrollY + 42);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ManagerTheme.DrawOutline(e.Graphics, ClientRectangle, ManagerTheme.Current.Border);
        }

        /// <summary>
        /// Apply theme colors
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            ThemePalette palette = ManagerTheme.Current;

            this.BackColor = palette.Surface;
            _placeholderPanel.BackColor = palette.Surface;
            _placeholderTitle.ForeColor = palette.Text;
            _placeholderLabel.ForeColor = palette.TextMuted;
            _nameLabel.ForeColor = palette.Text;
            _versionLabel.ForeColor = palette.Text;
            _authorsLabel.ForeColor = palette.TextMuted;
            _idLabel.ForeColor = palette.TextMuted;
            _separator.BackColor = palette.Border;
            _dependsOnLabel.ForeColor = palette.Text;
            _dependsOnValue.ForeColor = palette.TextMuted;
            _modApiLabel.ForeColor = palette.Text;
            _tagsLabel.ForeColor = palette.Text;
            _tagsValue.ForeColor = palette.TextMuted;
            _descLabel.ForeColor = palette.Text;
            _descriptionBox.BackColor = palette.Input;
            _descriptionBox.ForeColor = palette.Text;
            ManagerTheme.ApplyNativeScrollbars(_descriptionBox);
            foreach (LinkLabel link in new LinkLabel[] { _websiteLink, _nexusLink })
            {
                link.LinkColor = palette.AccentText;
                link.ActiveLinkColor = palette.AccentText;
                link.VisitedLinkColor = palette.AccentText;
            }
            _nexusUpdateButton.Invalidate();
            _nexusStableButton.Invalidate();
            _openFolderButton.Invalidate();

            // Update ModAPI status color if showing mod
            if (_currentMod != null)
            {
                UpdateModApiStatus(_currentMod);
                UpdateNexusStatus(_currentMod);
            }
        }
    }
}
