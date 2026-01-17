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

            int rightColumnX = 600;
            int rightColumnY = 135; // Align with description top

            _descriptionBox = new RichTextBox();
            _descriptionBox.Text =
                "Sheltered Mod Manager is a modding framework for Sheltered (Unicube/Team17).\n" +
                "It includes a plugin loader, ModAPI, and custom save tools.\n\n" +

                "Core features:\n" +
                "• Loads ModAPI plugins and Harmony patches.\n" +
                "• Resolves dependencies and load order via About.json.\n" +
                "• Adds unlimited custom save slots with save mod tracking + verification.\n" +
                "• Supports Steam (32-bit) and Epic (64-bit) builds via UnityDoorstop.\n\n" +

                "Notes:\n" +
                "• Custom saves are stored separately from vanilla saves.\n" +
                "• Deleting saves may reindex remaining slots.\n\n" +

                "Originally created by benjaminfoo (2019). Maintained by Coolnether123 (2025–present).";

            _descriptionBox.Font = new Font("Segoe UI", 10f);
            _descriptionBox.Location = new Point(20, yPos);
            _descriptionBox.Size = new Size(rightColumnX - 60, this.Height - yPos - 20); // Maintain gap to right column
            _descriptionBox.ReadOnly = true;
            _descriptionBox.BorderStyle = BorderStyle.None;
            _descriptionBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            // Links section
            _linksLabel = new Label();
            _linksLabel.Text = "Resources & Community";
            _linksLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _linksLabel.AutoSize = true;
            _linksLabel.Location = new Point(rightColumnX, rightColumnY);
            rightColumnY += 30;

            _nexusLink = new LinkLabel();
            _nexusLink.Text = "Nexus Mods Portal";
            _nexusLink.Font = new Font("Segoe UI", 10f);
            _nexusLink.AutoSize = true;
            _nexusLink.Location = new Point(rightColumnX + 10, rightColumnY);
            _nexusLink.LinkClicked += NexusLink_LinkClicked;
            rightColumnY += 25;

            _githubLink = new LinkLabel();
            _githubLink.Text = "Official GitHub Repository";
            _githubLink.Font = new Font("Segoe UI", 10f);
            _githubLink.AutoSize = true;
            _githubLink.Location = new Point(rightColumnX + 10, rightColumnY);
            _githubLink.LinkClicked += GithubLink_LinkClicked;
            rightColumnY += 25;

            _discordLink = new LinkLabel();
            _discordLink.Text = "Modding Discord Community";
            _discordLink.Font = new Font("Segoe UI", 10f);
            _discordLink.AutoSize = true;
            _discordLink.Location = new Point(rightColumnX + 10, rightColumnY);
            _discordLink.LinkClicked += DiscordLink_LinkClicked;
            rightColumnY += 50;

            // Credits section
            _creditsLabel = new Label();
            _creditsLabel.Text = "Credits & Acknowledgments";
            _creditsLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _creditsLabel.AutoSize = true;
            _creditsLabel.Location = new Point(rightColumnX, rightColumnY);
            rightColumnY += 30;

            _creditsBox = new RichTextBox();
            _creditsBox.Text = "• Unicube & Team17: Developers and publishers of Sheltered.\n" +
                   "• benjaminfoo: Architecture of the original 2019 mod loader.\n" +
                   "• NeighTools: Creators of the UnityDoorstop injection framework.\n" +
                   "• Andreas Pardeike: Developer of the Harmony patching library.\n" +
                   "• The Sheltered Modding Community: For continuous feedback and mod creation.";
            _creditsBox.Font = new Font("Segoe UI", 10f);
            _creditsBox.Location = new Point(rightColumnX, rightColumnY);
            _creditsBox.Size = new Size(this.Width - rightColumnX - 40, this.Height - rightColumnY - 20); // Dynamic width and height
            _creditsBox.ReadOnly = true;
            _creditsBox.BorderStyle = BorderStyle.None;
            _creditsBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Right;

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
