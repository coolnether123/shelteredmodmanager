using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Views
{
    /// <summary>
    /// About tab - credits and links.
    /// </summary>
    public class AboutTab : UserControl
    {
        private const string IssuesUrl = "https://github.com/coolnether123/shelteredmodmanager/issues";
        private const string NexusModsUrl = "https://www.nexusmods.com/games/sheltered";
        private const string NexusManagerUrl = "https://www.nexusmods.com/sheltered/mods/1";

        private Label _titleLabel;
        private Label _versionLabel;
        private Label _authorLabel;
        private RichTextBox _descriptionBox;
        private Label _linksLabel;

        private LinkLabel _issuesLink;
        private LinkLabel _nexusModsLink;
        private LinkLabel _nexusManagerLink;

        private Label _creditsLabel;
        private RichTextBox _creditsBox;

        private bool _isDarkMode = false;
        private string _appVersion = "1.0.0";
        private string _author = "Coolnether123";

        public string AppVersion
        {
            get { return _appVersion; }
            set
            {
                _appVersion = value;
                if (_versionLabel != null)
                    _versionLabel.Text = "Version " + (_appVersion ?? string.Empty);
            }
        }

        public string Author
        {
            get { return _author; }
            set
            {
                _author = value;
                if (_authorLabel != null)
                    _authorLabel.Text = "Maintained by " + (_author ?? string.Empty);
            }
        }

        public AboutTab()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Padding = new Padding(20);

            int yPos = 20;

            _titleLabel = new Label();
            _titleLabel.Text = "Sheltered Mod Manager";
            _titleLabel.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(20, yPos);
            yPos += 45;

            _versionLabel = new Label();
            _versionLabel.Text = "Version " + _appVersion;
            _versionLabel.Font = new Font("Segoe UI", 11f);
            _versionLabel.ForeColor = Color.Gray;
            _versionLabel.AutoSize = true;
            _versionLabel.Location = new Point(20, yPos);
            yPos += 30;

            _authorLabel = new Label();
            _authorLabel.Text = "Maintained by " + _author;
            _authorLabel.Font = new Font("Segoe UI", 10f);
            _authorLabel.AutoSize = true;
            _authorLabel.Location = new Point(20, yPos);
            yPos += 40;

            int rightColumnX = 600;
            int rightColumnY = 135;

            _descriptionBox = new RichTextBox();
            _descriptionBox.Text =
                "Sheltered Mod Manager is a modding framework for Sheltered by Unicube and Team17. It installs non-destructively alongside the game and supports Steam/GOG 32-bit builds and Epic 64-bit builds.\n\n" +

                "Core features:\n" +
                "- Plugin loader with dependency resolution and load order management.\n" +
                "- Unlimited save slots for vanilla scenarios with mod tracking and verification.\n" +
                "- Desktop and in-game mod managers.\n" +
                "- Rebindable Sheltered and mod-defined keybindings.\n" +

                "Custom scenario support:\n" +
                "- Custom scenario browser, XML scenario packs, triggers, scheduled effects, and win/loss runtime support.\n" +
                "- Advanced in-game authoring is an opt-in preview and defaults off.\n\n" +

                "Developer API:\n" +
                "- ModAPI.dll provides the neutral modding framework surface.\n" +
                "- ShelteredAPI.dll provides Sheltered content, saves, UI, input, events, actors, scenarios, and Harmony integration.\n" +
                "- ModManagerBase, attribute settings, Spine settings UI, isolated persistence, event bus, and runtime inspector (F9).\n\n" +

                "Originally created by benjaminfoo in 2019. Maintained by Coolnether123 from 2025 to present with the original author's permission.";

            _descriptionBox.Font = new Font("Segoe UI", 10f);
            _descriptionBox.Location = new Point(20, yPos);
            _descriptionBox.Size = new Size(rightColumnX - 60, this.Height - yPos - 20);
            _descriptionBox.ReadOnly = true;
            _descriptionBox.Multiline = true;
            _descriptionBox.WordWrap = true;
            _descriptionBox.BorderStyle = BorderStyle.None;
            _descriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _descriptionBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            _linksLabel = new Label();
            _linksLabel.Text = "Resources & Community";
            _linksLabel.UseMnemonic = false;
            _linksLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _linksLabel.AutoSize = true;
            _linksLabel.Location = new Point(rightColumnX, rightColumnY);
            rightColumnY += 30;

            _issuesLink = CreateLinkLabel("GitHub Issues", rightColumnX + 10, rightColumnY, IssuesLink_LinkClicked);
            rightColumnY += 25;

            _nexusModsLink = CreateLinkLabel("Nexus Mods - Sheltered", rightColumnX + 10, rightColumnY, NexusModsLink_LinkClicked);
            rightColumnY += 25;

            _nexusManagerLink = CreateLinkLabel("Sheltered Mod Manager on Nexus", rightColumnX + 10, rightColumnY, NexusManagerLink_LinkClicked);
            rightColumnY += 50;

            _creditsLabel = new Label();
            _creditsLabel.Text = "Credits & Acknowledgments";
            _creditsLabel.UseMnemonic = false;
            _creditsLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _creditsLabel.AutoSize = true;
            _creditsLabel.Location = new Point(rightColumnX, rightColumnY);
            rightColumnY += 30;

            _creditsBox = new RichTextBox();
            _creditsBox.Text = "- Coolnether123: 2025 maintenance and development.\n" +
                   "- benjaminfoo: Original 2019 mod loader foundation.\n" +
                   "- Team17: Publisher of Sheltered.\n" +
                   "- Unicube: Original game developers.\n" +
                   "- NeighTools: UnityDoorstop injection framework.\n" +
                   "- Andreas Pardeike: Harmony runtime patching library.";
            _creditsBox.Font = new Font("Segoe UI", 10f);
            _creditsBox.Location = new Point(rightColumnX, rightColumnY);
            _creditsBox.Size = new Size(this.Width - rightColumnX - 40, this.Height - rightColumnY - 20);
            _creditsBox.ReadOnly = true;
            _creditsBox.Multiline = true;
            _creditsBox.WordWrap = true;
            _creditsBox.BorderStyle = BorderStyle.None;
            _creditsBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _creditsBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;

            this.Controls.Add(_titleLabel);
            this.Controls.Add(_versionLabel);
            this.Controls.Add(_authorLabel);
            this.Controls.Add(_descriptionBox);
            this.Controls.Add(_linksLabel);
            this.Controls.Add(_issuesLink);
            this.Controls.Add(_nexusModsLink);
            this.Controls.Add(_nexusManagerLink);
            this.Controls.Add(_creditsLabel);
            this.Controls.Add(_creditsBox);

            this.ResumeLayout();
        }

        private LinkLabel CreateLinkLabel(string text, int x, int y, LinkLabelLinkClickedEventHandler handler)
        {
            LinkLabel link = new LinkLabel();
            link.Text = text;
            link.Font = new Font("Segoe UI", 10f);
            link.AutoSize = true;
            link.Location = new Point(x, y);
            link.LinkClicked += handler;
            return link;
        }

        private void IssuesLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl(IssuesUrl);
        }

        private void NexusModsLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl(NexusModsUrl);
        }

        private void NexusManagerLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl(NexusManagerUrl);
        }

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open URL: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Apply theme.
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;

            if (isDark)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                _titleLabel.ForeColor = Color.White;
                _versionLabel.ForeColor = Color.Gray;
                _authorLabel.ForeColor = Color.LightGray;
                _descriptionBox.BackColor = Color.FromArgb(45, 45, 48);
                _descriptionBox.ForeColor = Color.White;
                _linksLabel.ForeColor = Color.White;
                _creditsLabel.ForeColor = Color.White;
                _creditsBox.BackColor = Color.FromArgb(45, 45, 48);
                _creditsBox.ForeColor = Color.White;

                _issuesLink.LinkColor = Color.LightBlue;
                _nexusModsLink.LinkColor = Color.LightBlue;
                _nexusManagerLink.LinkColor = Color.LightBlue;
            }
            else
            {
                this.BackColor = SystemColors.Control;
                _titleLabel.ForeColor = SystemColors.ControlText;
                _versionLabel.ForeColor = Color.Gray;
                _authorLabel.ForeColor = SystemColors.ControlText;
                _descriptionBox.BackColor = SystemColors.Control;
                _descriptionBox.ForeColor = SystemColors.ControlText;
                _linksLabel.ForeColor = SystemColors.ControlText;
                _creditsLabel.ForeColor = SystemColors.ControlText;
                _creditsBox.BackColor = SystemColors.Control;
                _creditsBox.ForeColor = SystemColors.ControlText;

                _issuesLink.LinkColor = SystemColors.HotTrack;
                _nexusModsLink.LinkColor = SystemColors.HotTrack;
                _nexusManagerLink.LinkColor = SystemColors.HotTrack;
            }
        }
    }
}
