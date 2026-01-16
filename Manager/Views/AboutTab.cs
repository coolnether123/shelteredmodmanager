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
        private Label _titleLabel;
        private Label _versionLabel;
        private Label _authorLabel;
        private RichTextBox _descriptionBox;
        private Label _linksLabel;
        
        private LinkLabel _nexusLink;
        private LinkLabel _githubLink;
        private LinkLabel _discordLink;
        
        private Label _creditsLabel;
        private RichTextBox _creditsBox;

        private bool _isDarkMode = false;
        private string _appVersion = "1.0.0";
        private string _author = "Coolnether123";

        public string AppVersion 
        { 
            get { return _appVersion; } 
            set { _appVersion = value; } 
        }
        
        public string Author 
        { 
            get { return _author; } 
            set { _author = value; } 
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

            // Title
            _titleLabel = new Label();
            _titleLabel.Text = "Sheltered Mod Manager";
            _titleLabel.Font = new Font("Segoe UI", 18f, FontStyle.Bold);
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(20, yPos);
            yPos += 45;

            // Version
            _versionLabel = new Label();
            _versionLabel.Text = "Version " + _appVersion;
            _versionLabel.Font = new Font("Segoe UI", 11f);
            _versionLabel.ForeColor = Color.Gray;
            _versionLabel.AutoSize = true;
            _versionLabel.Location = new Point(20, yPos);
            yPos += 30;

            // Author
            _authorLabel = new Label();
            _authorLabel.Text = "by " + _author;
            _authorLabel.Font = new Font("Segoe UI", 10f);
            _authorLabel.AutoSize = true;
            _authorLabel.Location = new Point(20, yPos);
            yPos += 40;

            // Description
            _descriptionBox = new RichTextBox();
            _descriptionBox.Text = "A comprehensive mod loader and manager for Sheltered. " +
                   "Enables loading custom mods, managing load order, and provides a powerful API for mod developers.\n\n" +
                   "Features:\n" +
                   "- Mod discovery and load order management\n" +
                   "- Dependency resolution\n" +
                   "- ModAPI version compatibility checking\n" +
                   "- Unlimited save slots with verification\n" +
                   "- Dark mode support";
            _descriptionBox.Font = new Font("Segoe UI", 10f);
            _descriptionBox.Location = new Point(20, yPos);
            _descriptionBox.Size = new Size(500, 140);
            _descriptionBox.ReadOnly = true;
            _descriptionBox.BorderStyle = BorderStyle.None;
            yPos += 160;

            // Links section
            _linksLabel = new Label();
            _linksLabel.Text = "Links";
            _linksLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _linksLabel.AutoSize = true;
            _linksLabel.Location = new Point(20, yPos);
            yPos += 30;

            _nexusLink = new LinkLabel();
            _nexusLink.Text = "Nexus Mods";
            _nexusLink.Font = new Font("Segoe UI", 10f);
            _nexusLink.AutoSize = true;
            _nexusLink.Location = new Point(30, yPos);
            _nexusLink.LinkClicked += NexusLink_LinkClicked;
            yPos += 25;

            _githubLink = new LinkLabel();
            _githubLink.Text = "GitHub Repository";
            _githubLink.Font = new Font("Segoe UI", 10f);
            _githubLink.AutoSize = true;
            _githubLink.Location = new Point(30, yPos);
            _githubLink.LinkClicked += GithubLink_LinkClicked;
            yPos += 25;

            _discordLink = new LinkLabel();
            _discordLink.Text = "Discord Community";
            _discordLink.Font = new Font("Segoe UI", 10f);
            _discordLink.AutoSize = true;
            _discordLink.Location = new Point(30, yPos);
            _discordLink.LinkClicked += DiscordLink_LinkClicked;
            yPos += 40;

            // Credits section
            _creditsLabel = new Label();
            _creditsLabel.Text = "Credits & Thanks";
            _creditsLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _creditsLabel.AutoSize = true;
            _creditsLabel.Location = new Point(20, yPos);
            yPos += 30;

            _creditsBox = new RichTextBox();
            _creditsBox.Text = "- Team17 - Sheltered game\n" +
                   "- NeighTools - Unity Doorstop\n" +
                   "- Pardeike - Harmony patching library\n" +
                   "- The Sheltered modding community";
            _creditsBox.Font = new Font("Segoe UI", 10f);
            _creditsBox.Location = new Point(20, yPos);
            _creditsBox.Size = new Size(500, 100);
            _creditsBox.ReadOnly = true;
            _creditsBox.BorderStyle = BorderStyle.None;

            // Add controls
            this.Controls.Add(_titleLabel);
            this.Controls.Add(_versionLabel);
            this.Controls.Add(_authorLabel);
            this.Controls.Add(_descriptionBox);
            this.Controls.Add(_linksLabel);
            this.Controls.Add(_nexusLink);
            this.Controls.Add(_githubLink);
            this.Controls.Add(_discordLink);
            this.Controls.Add(_creditsLabel);
            this.Controls.Add(_creditsBox);

            this.ResumeLayout();
        }

        private void NexusLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl("https://www.nexusmods.com/sheltered/mods/");
        }

        private void GithubLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl("https://github.com/coolnether123/shelteredmodmanager");
        }

        private void DiscordLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            OpenUrl("https://discord.gg/sheltered-mods");
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
        /// Apply theme
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
                
                _nexusLink.LinkColor = Color.LightBlue;
                _githubLink.LinkColor = Color.LightBlue;
                _discordLink.LinkColor = Color.LightBlue;
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
                
                _nexusLink.LinkColor = SystemColors.HotTrack;
                _githubLink.LinkColor = SystemColors.HotTrack;
                _discordLink.LinkColor = SystemColors.HotTrack;
            }
        }
    }
}
