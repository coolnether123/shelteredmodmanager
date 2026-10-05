using System;
using System.Drawing;
using System.Windows.Forms;
using Manager.Controls;

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

        private LinkLabel _issuesLink;
        private LinkLabel _nexusModsLink;
        private LinkLabel _nexusManagerLink;

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
            this.Padding = new Padding(16, 14, 16, 12);
            this.Font = ManagerTheme.UiFont(9f);

            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel header = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 14)
            };
            _titleLabel = UiFactory.Heading("Sheltered Mod Manager", 18f);
            _versionLabel = UiFactory.Text("Version " + _appVersion, ThemeRole.Muted, 10f, false);
            _authorLabel = UiFactory.Text("Maintained by " + _author, ThemeRole.Default, 10f, false);
            header.Controls.Add(_titleLabel, 0, 0);
            header.Controls.Add(_versionLabel, 0, 1);
            header.Controls.Add(_authorLabel, 0, 2);

            CardPanel about = new CardPanel("About this manager", null);
            about.FillsHeight = true;
            about.Dock = DockStyle.Fill;
            about.Margin = new Padding(0, 0, 14, 0);
            _descriptionBox = new RichTextBox();
            _descriptionBox.Text =
                "Sheltered Mod Manager installs alongside Sheltered by Unicube and Team17. It supports Steam and GOG 32-bit builds plus Epic 64-bit builds.\n\n" +
                "Use it to install and organize mods, manage load order, track modded saves, and launch the game. It also includes a Content Workshop for creating items, recipes, and icons without writing code.\n\n" +
                "Custom scenarios are supported. Advanced in-game authoring is still a preview and is off by default.\n\n" +
                "benjaminfoo created the original mod loader in 2019. Coolnether123 has maintained it since 2025 with the original author's permission.";
            _descriptionBox.Font = ManagerTheme.UiFont(10f);
            _descriptionBox.Dock = DockStyle.Fill;
            _descriptionBox.ReadOnly = true;
            _descriptionBox.Multiline = true;
            _descriptionBox.WordWrap = true;
            _descriptionBox.BorderStyle = BorderStyle.None;
            _descriptionBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _descriptionBox.AccessibleName = "About Sheltered Mod Manager";
            about.AddBody(_descriptionBox);

            TableLayoutPanel side = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 2,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            side.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            side.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            CardPanel links = new CardPanel("Resources and community", null);
            links.Dock = DockStyle.Top;
            _issuesLink = CreateLinkLabel("GitHub Issues", IssuesLink_LinkClicked);
            _nexusModsLink = CreateLinkLabel("Nexus Mods - Sheltered", NexusModsLink_LinkClicked);
            _nexusManagerLink = CreateLinkLabel("Sheltered Mod Manager on Nexus", NexusManagerLink_LinkClicked);
            links.AddBody(_issuesLink);
            links.AddBody(_nexusModsLink);
            links.AddBody(_nexusManagerLink);

            CardPanel credits = new CardPanel("Credits and acknowledgments", null);
            credits.FillsHeight = true;
            credits.Dock = DockStyle.Fill;
            credits.Margin = new Padding(0);
            _creditsBox = new RichTextBox();
            _creditsBox.Text = "- Coolnether123: 2025 maintenance and development.\n" +
                   "- benjaminfoo: Original 2019 mod loader foundation.\n" +
                   "- Team17: Publisher of Sheltered.\n" +
                   "- Unicube: Original game developers.\n" +
                   "- NeighTools: UnityDoorstop injection framework.\n" +
                   "- Andreas Pardeike: Harmony runtime patching library.";
            _creditsBox.Font = ManagerTheme.UiFont(9.5f);
            _creditsBox.Dock = DockStyle.Fill;
            _creditsBox.ReadOnly = true;
            _creditsBox.Multiline = true;
            _creditsBox.WordWrap = true;
            _creditsBox.BorderStyle = BorderStyle.None;
            _creditsBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            _creditsBox.AccessibleName = "Credits and acknowledgments";
            credits.AddBody(_creditsBox);

            side.Controls.Add(links, 0, 0);
            side.Controls.Add(credits, 0, 1);
            root.Controls.Add(header, 0, 0);
            root.SetColumnSpan(header, 2);
            root.Controls.Add(about, 0, 1);
            root.Controls.Add(side, 1, 1);
            this.Controls.Add(root);
            this.ResumeLayout();
        }

        private LinkLabel CreateLinkLabel(string text, LinkLabelLinkClickedEventHandler handler)
        {
            LinkLabel link = new ThemedLinkLabel();
            link.Text = text;
            link.Font = ManagerTheme.UiFont(10f);
            link.AutoSize = true;
            link.Margin = new Padding(0, 0, 0, 8);
            link.Anchor = AnchorStyles.Left;
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
            ThemePalette palette = ManagerTheme.Current;
            ManagerTheme.Apply(this);
            this.BackColor = palette.Window;
            _descriptionBox.BackColor = palette.Surface;
            _descriptionBox.ForeColor = palette.Text;
            _creditsBox.BackColor = palette.Surface;
            _creditsBox.ForeColor = palette.Text;
        }
    }
}
