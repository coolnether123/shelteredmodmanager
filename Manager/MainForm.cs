using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core;
using Manager.Core.Models;
using Manager.Core.Services;
using Manager.Views;

namespace Manager
{
    /// <summary>
    /// Main application window for Sheltered Mod Manager.
    /// Uses separation of concerns with dedicated services and custom controls.
    /// </summary>
    public class MainForm : Form
    {
        // Header
        private PictureBox _logoBox;
        private Label _titleLabel;
        private Label _subtitleLabel;

        // Tab control
        private TabControl _tabControl;
        private TabPage _gameSetupPage;
        private TabPage _modManagerPage;
        private TabPage _contentWorkshopPage;
        private TabPage _nexusPage;
        private TabPage _nexusUploadPage;
        private TabPage _settingsPage;
        private TabPage _aboutPage;

        // Status Header
        private Label _statusLabel;
        private Label _modsCountLabel;
        private Label _modApiVersionLabel;
        private Label _nexusUpdatesLabel;
        private TableLayoutPanel _headerStatusPanel;
        private ModManagerTab _modManagerTab;
        private ContentWorkshopTab _contentWorkshopTab;
        private NexusModsTab _nexusTab;
        private NexusUploadTab _nexusUploadTab;
        private SettingsTab _settingsTab;
        private AboutTab _aboutTab;
        private const int ModLaunchVerificationTimeoutMs = 60000;

        // Services
        private SettingsService _settingsService;
        private ModDiscoveryService _discoveryService;
        private LoadOrderService _orderService;
        private NexusModsService _nexusService;
        private NexusOAuthService _nexusOAuthService;

        // State
        private AppSettings _settings;
        private Timer _restartPollTimer;
        private Timer _nexusProtocolPollTimer;
        private Panel headerPanel;
        private GameSetupTab _gameSetupTab;
        private bool _windowPlacementInitialized;
        private int _suppressSettingsReloadLogCount;
        private DateTime _lastNoChangeSettingsReloadLogUtc = DateTime.MinValue;
        private bool _startupNexusUpdateAnnouncementsPending = true;
        private int _nexusAccountRequestToken;
        private bool _statusReady = true;
        private bool _loaderMissing;
        private ActionButton _headerPlayButton;
        private TableLayoutPanel _headerPlayHost;
        private readonly CrashReportService _crashReports = new CrashReportService();
        private readonly ToolTip _headerToolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ShowAlways = true };
        private string _startupPreviousInstalledModApiVersion;
        private readonly string _startupNexusLink;
        private const string APP_VERSION = AppVersionInfo.Display;
        private static readonly System.Collections.Generic.Dictionary<string, string> KnownModIdMigrations =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Historical IDs seen in user loadorder files/logs.
                { "coolnether123.pluginconsole", "com.coolnether123.pluginconsole" },
                { "com.plugin.harmony.example", "coolnether123.harmonyexample" }
            };

        public MainForm()
            : this(null)
        {
        }

        public MainForm(string startupNexusLink)
        {
            _startupNexusLink = startupNexusLink;
            InitializeServices();
            InitializeComponent();
            BuildHeaderPlayButton();
            InitializeCustomResources();
            WireEvents();
        }

        /// <summary>Play sits in the header so launching is one click away on every tab (F5 anywhere).</summary>
        private void BuildHeaderPlayButton()
        {
            _headerPlayButton = new ActionButton
            {
                Kind = ActionButtonKind.Primary,
                Text = "Play",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Font = ManagerTheme.Semibold(10.5f),
                MinimumSize = new Size(112, 38),
                Padding = new Padding(18, 0, 18, 0),
                Anchor = AnchorStyles.None,
                Margin = new Padding(0),
                AccessibleName = "Play Sheltered with your mods"
            };
            _headerPlayButton.Click += HeaderPlayButton_Click;
            _headerToolTip.SetToolTip(_headerPlayButton, "Launch Sheltered with your active mods (F5)");
            _headerPlayHost = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(20, 0, 0, 0)
            };
            _headerPlayHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _headerPlayHost.Controls.Add(_headerPlayButton, 0, 0);
            // Added after the status panel, so it docks at the far right.
            headerPanel.Controls.Add(_headerPlayHost);
        }

        private void HeaderPlayButton_Click(object sender, EventArgs e)
        {
            if (_settings == null || !_settings.IsGamePathValid)
            {
                _tabControl.SelectedTab = _gameSetupPage;
                return;
            }
            GameSetupTab_LaunchRequested(true);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5)
            {
                HeaderPlayButton_Click(this, EventArgs.Empty);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UpdateHeaderPlayButton(int activeMods)
        {
            if (_headerPlayButton == null)
                return;
            bool ready = _settings != null && _settings.IsGamePathValid;
            _headerPlayButton.Text = ready ? "Play" : "Set up game";
            _headerPlayButton.Kind = ready ? ActionButtonKind.Primary : ActionButtonKind.Secondary;
            _headerToolTip.SetToolTip(_headerPlayButton, ready
                ? "Launch Sheltered with your " + FormatCount(activeMods, "active mod") + " (F5)"
                : "Choose the game in Game setup first");
        }

        private void InitializeCustomResources()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                
                // App Title
                this.Text = "Sheltered Mod Manager v" + APP_VERSION;
                
                // Tab Versions
                if (_aboutTab != null) _aboutTab.AppVersion = APP_VERSION;

                // Window Icon
                using (var stream = assembly.GetManifestResourceStream("Manager.Icon.ico"))
                {
                    if (stream != null) this.Icon = new Icon(stream);
                }

                // Logo Image
                using (var stream = assembly.GetManifestResourceStream("Manager.Icon.png"))
                {
                    if (stream != null && _logoBox != null) 
                        _logoBox.Image = new Bitmap(stream);
                }
            }
            catch { }

            // Fallback icon path so taskbar/title bar icon still appears even if resource lookup fails.
            if (this.Icon == null)
            {
                try
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch { }
            }
        }

        private void InitializeServices()
        {
            _settingsService = new SettingsService();
            _orderService = new LoadOrderService();
            
            // Settings loaded first to get ModAPI version path
            _settings = _settingsService.Load();
            _startupPreviousInstalledModApiVersion = _settings.InstalledModApiVersion;
            
            var installedApiVersions = DetectInstalledApiVersions(_settings);
            ApplyInstalledApiVersions(_settings, installedApiVersions);

            _discoveryService = new ModDiscoveryService(installedApiVersions);
            _nexusOAuthService = new NexusOAuthService(_settingsService, _settings);
            _nexusService = new NexusModsService(_nexusOAuthService);
        }

        private static Dictionary<string, string> DetectInstalledApiVersions(AppSettings settings)
        {
            if (settings == null || !settings.IsGamePathValid)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string smmPath = Path.Combine(Path.GetDirectoryName(settings.GamePath), "SMM");
                return AssemblyVersionChecker.GetInstalledApiVersions(smmPath);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void ApplyInstalledApiVersions(AppSettings settings, Dictionary<string, string> versions)
        {
            if (settings == null || versions == null)
                return;

            string version;
            if (versions.TryGetValue("ModAPI", out version))
                settings.InstalledModApiVersion = version;
            if (versions.TryGetValue("ShelteredAPI", out version))
                settings.InstalledShelteredApiVersion = version;
        }

        private void RecreateNexusService()
        {
            if (_nexusOAuthService == null)
                _nexusOAuthService = new NexusOAuthService(_settingsService, _settings);
            else
                _nexusOAuthService.UpdateSettings(_settings);
            _nexusService = new NexusModsService(_nexusOAuthService);
        }

        private void ApplyNexusAccountStatus(NexusAccountStatus status)
        {
            if (_settingsTab != null)
                _settingsTab.SetNexusAccountStatus(status);
            if (_nexusTab != null)
                _nexusTab.SetAccountStatus(status);
            if (_nexusUploadTab != null)
                _nexusUploadTab.SetAccountStatus(status);
        }

        private void RefreshNexusAccountStatusAsync(bool showPending)
        {
            if (_settings == null)
            {
                ApplyNexusAccountStatus(null);
                return;
            }

            if (!_settings.EnableNexusIntegration)
            {
                ApplyNexusAccountStatus(null);
                return;
            }

            if (!_settings.HasNexusOAuthSession)
            {
                ApplyNexusAccountStatus(NexusAccountStatus.CreateNotConfigured());
                return;
            }

            if (_nexusService == null)
            {
                ApplyNexusAccountStatus(NexusAccountStatus.CreateUnavailable("Nexus service is unavailable."));
                return;
            }

            if (showPending)
                ApplyNexusAccountStatus(NexusAccountStatus.CreateChecking());

            int token = ++_nexusAccountRequestToken;
            NexusModsService nexusService = _nexusService;

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string errorMessage;
                NexusAccountStatus status = nexusService.GetAccountStatus(out errorMessage);
                if (status == null)
                    status = NexusAccountStatus.CreateUnavailable(errorMessage);
                else if (!string.IsNullOrEmpty(errorMessage) && string.IsNullOrEmpty(status.ErrorMessage))
                    status.ErrorMessage = errorMessage;

                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (token != _nexusAccountRequestToken)
                            return;

                        ApplyNexusAccountStatus(status);
                    });
                }
                catch { }
            });
        }

        private void InitializeComponent()
        {
            this.headerPanel = new System.Windows.Forms.Panel();
            this._logoBox = new System.Windows.Forms.PictureBox();
            this._titleLabel = new System.Windows.Forms.Label();
            this._subtitleLabel = new System.Windows.Forms.Label();
            this._headerStatusPanel = new System.Windows.Forms.TableLayoutPanel();
            this._nexusUpdatesLabel = new System.Windows.Forms.Label();
            this._modApiVersionLabel = new System.Windows.Forms.Label();
            this._modsCountLabel = new System.Windows.Forms.Label();
            this._statusLabel = new System.Windows.Forms.Label();
            this._tabControl = new Manager.Controls.ThemedTabControl();
            this._gameSetupPage = new System.Windows.Forms.TabPage();
            this._modManagerPage = new System.Windows.Forms.TabPage();
            this._modManagerTab = new Manager.Views.ModManagerTab();
            this._contentWorkshopPage = new System.Windows.Forms.TabPage();
            this._contentWorkshopTab = new Manager.Views.ContentWorkshopTab();
            this._nexusPage = new System.Windows.Forms.TabPage();
            this._nexusTab = new Manager.Views.NexusModsTab();
            this._nexusUploadPage = new System.Windows.Forms.TabPage();
            this._nexusUploadTab = new Manager.Views.NexusUploadTab();
            this._settingsPage = new System.Windows.Forms.TabPage();
            this._settingsTab = new Manager.Views.SettingsTab();
            this._aboutPage = new System.Windows.Forms.TabPage();
            this._aboutTab = new Manager.Views.AboutTab();
            this._gameSetupTab = new Manager.Views.GameSetupTab();
            this.headerPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._logoBox)).BeginInit();
            this._headerStatusPanel.SuspendLayout();
            this._tabControl.SuspendLayout();
            this._gameSetupPage.SuspendLayout();
            this._modManagerPage.SuspendLayout();
            this._contentWorkshopPage.SuspendLayout();
            this._nexusPage.SuspendLayout();
            this._nexusUploadPage.SuspendLayout();
            this._settingsPage.SuspendLayout();
            this._aboutPage.SuspendLayout();
            this.SuspendLayout();
            //
            // headerPanel
            //
            this.headerPanel.Controls.Add(this._logoBox);
            this.headerPanel.Controls.Add(this._titleLabel);
            this.headerPanel.Controls.Add(this._subtitleLabel);
            this.headerPanel.Controls.Add(this._headerStatusPanel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Padding = new System.Windows.Forms.Padding(16, 8, 16, 8);
            this.headerPanel.Size = new System.Drawing.Size(1182, 80);
            this.headerPanel.TabIndex = 1;
            //
            // _logoBox
            //
            this._logoBox.Location = new System.Drawing.Point(16, 16);
            this._logoBox.Name = "_logoBox";
            this._logoBox.Size = new System.Drawing.Size(46, 46);
            this._logoBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._logoBox.TabIndex = 0;
            this._logoBox.TabStop = false;
            this._logoBox.AccessibleName = "Sheltered Mod Manager logo";
            //
            // _titleLabel
            //
            this._titleLabel.AutoSize = true;
            this._titleLabel.Font = Manager.Controls.ManagerTheme.Display(16F);
            this._titleLabel.Location = new System.Drawing.Point(72, 14);
            this._titleLabel.Name = "_titleLabel";
            this._titleLabel.TabIndex = 1;
            this._titleLabel.Text = "Sheltered Mod Manager";
            this._titleLabel.UseMnemonic = false;
            //
            // _subtitleLabel
            //
            this._subtitleLabel.AutoSize = true;
            this._subtitleLabel.Font = Manager.Controls.ManagerTheme.UiFont(9F);
            this._subtitleLabel.Location = new System.Drawing.Point(74, 44);
            this._subtitleLabel.Name = "_subtitleLabel";
            this._subtitleLabel.Text = "Version " + APP_VERSION;
            this._subtitleLabel.UseMnemonic = false;
            //
            // _headerStatusPanel
            //
            this._headerStatusPanel.AutoSize = true;
            this._headerStatusPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._headerStatusPanel.ColumnCount = 1;
            this._headerStatusPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            this._headerStatusPanel.RowCount = 4;
            this._headerStatusPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this._headerStatusPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this._headerStatusPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this._headerStatusPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            this._headerStatusPanel.Controls.Add(this._statusLabel, 0, 0);
            this._headerStatusPanel.Controls.Add(this._modsCountLabel, 0, 1);
            this._headerStatusPanel.Controls.Add(this._modApiVersionLabel, 0, 2);
            this._headerStatusPanel.Controls.Add(this._nexusUpdatesLabel, 0, 3);
            this._headerStatusPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this._headerStatusPanel.Name = "_headerStatusPanel";
            this._headerStatusPanel.Padding = new System.Windows.Forms.Padding(0);
            this._headerStatusPanel.TabIndex = 2;
            this._headerStatusPanel.AccessibleName = "Manager status";
            this._headerStatusPanel.AccessibleRole = System.Windows.Forms.AccessibleRole.StatusBar;
            //
            // _nexusUpdatesLabel
            //
            this._nexusUpdatesLabel.AutoSize = true;
            this._nexusUpdatesLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this._nexusUpdatesLabel.Font = Manager.Controls.ManagerTheme.UiFont(8.75F);
            this._nexusUpdatesLabel.Margin = new System.Windows.Forms.Padding(0);
            this._nexusUpdatesLabel.Name = "_nexusUpdatesLabel";
            this._nexusUpdatesLabel.TabIndex = 3;
            this._nexusUpdatesLabel.Text = "Nexus: not checked yet";
            this._nexusUpdatesLabel.UseMnemonic = false;
            this._nexusUpdatesLabel.Cursor = System.Windows.Forms.Cursors.Hand;
            //
            // _modApiVersionLabel
            //
            this._modApiVersionLabel.AutoSize = true;
            this._modApiVersionLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this._modApiVersionLabel.Font = Manager.Controls.ManagerTheme.UiFont(8.75F);
            this._modApiVersionLabel.Margin = new System.Windows.Forms.Padding(0);
            this._modApiVersionLabel.Name = "_modApiVersionLabel";
            this._modApiVersionLabel.TabIndex = 0;
            this._modApiVersionLabel.Text = string.Empty;
            this._modApiVersionLabel.Visible = false;
            this._modApiVersionLabel.UseMnemonic = false;
            //
            // _modsCountLabel
            //
            this._modsCountLabel.AutoSize = true;
            this._modsCountLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this._modsCountLabel.Font = Manager.Controls.ManagerTheme.UiFont(8.75F);
            this._modsCountLabel.Margin = new System.Windows.Forms.Padding(0);
            this._modsCountLabel.Name = "_modsCountLabel";
            this._modsCountLabel.TabIndex = 1;
            this._modsCountLabel.Text = string.Empty;
            this._modsCountLabel.UseMnemonic = false;
            //
            // _statusLabel
            //
            this._statusLabel.AutoSize = true;
            this._statusLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this._statusLabel.Font = Manager.Controls.ManagerTheme.Semibold(9.5F);
            this._statusLabel.ForeColor = System.Drawing.Color.LightGreen;
            this._statusLabel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 1);
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.TabIndex = 2;
            this._statusLabel.Text = "Ready to play";
            this._statusLabel.UseMnemonic = false;
            // 
            // _tabControl
            // 
            this._tabControl.Controls.Add(this._gameSetupPage);
            this._tabControl.Controls.Add(this._modManagerPage);
            this._tabControl.Controls.Add(this._contentWorkshopPage);
            this._tabControl.Controls.Add(this._nexusPage);
            this._tabControl.Controls.Add(this._nexusUploadPage);
            this._tabControl.Controls.Add(this._settingsPage);
            this._tabControl.Controls.Add(this._aboutPage);
            this._tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabControl.Font = new System.Drawing.Font("Segoe UI", 10F);
            this._tabControl.Location = new System.Drawing.Point(0, 95);
            this._tabControl.Name = "_tabControl";
            this._tabControl.Padding = new System.Drawing.Point(15, 8);
            this._tabControl.SelectedIndex = 0;
            this._tabControl.Size = new System.Drawing.Size(1182, 608);
            this._tabControl.TabIndex = 0;
            this._tabControl.AccessibleName = "Manager sections";
            // 
            // _gameSetupPage
            // 
            this._gameSetupPage.Controls.Add(this._gameSetupTab);
            this._gameSetupPage.Location = new System.Drawing.Point(4, 42);
            this._gameSetupPage.Name = "_gameSetupPage";
            this._gameSetupPage.Size = new System.Drawing.Size(1174, 562);
            this._gameSetupPage.TabIndex = 0;
            this._gameSetupPage.Text = "Game setup";
            this._gameSetupPage.UseVisualStyleBackColor = false;
            // 
            // _modManagerPage
            // 
            this._modManagerPage.Controls.Add(this._modManagerTab);
            this._modManagerPage.Location = new System.Drawing.Point(4, 42);
            this._modManagerPage.Name = "_modManagerPage";
            this._modManagerPage.Size = new System.Drawing.Size(1174, 562);
            this._modManagerPage.TabIndex = 1;
            this._modManagerPage.Text = "Mod manager";
            this._modManagerPage.UseVisualStyleBackColor = false;
            // 
            // _modManagerTab
            // 
            this._modManagerTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._modManagerTab.Location = new System.Drawing.Point(0, 0);
            this._modManagerTab.Name = "_modManagerTab";
            this._modManagerTab.Padding = new System.Windows.Forms.Padding(15);
            this._modManagerTab.Size = new System.Drawing.Size(1174, 562);
            this._modManagerTab.TabIndex = 0;
            //
            // _contentWorkshopPage
            //
            this._contentWorkshopPage.Controls.Add(this._contentWorkshopTab);
            this._contentWorkshopPage.Location = new System.Drawing.Point(4, 42);
            this._contentWorkshopPage.Name = "_contentWorkshopPage";
            this._contentWorkshopPage.Size = new System.Drawing.Size(1174, 562);
            this._contentWorkshopPage.TabIndex = 2;
            this._contentWorkshopPage.Text = "Content workshop";
            this._contentWorkshopPage.UseVisualStyleBackColor = false;
            //
            // _contentWorkshopTab
            //
            this._contentWorkshopTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contentWorkshopTab.Location = new System.Drawing.Point(0, 0);
            this._contentWorkshopTab.Name = "_contentWorkshopTab";
            this._contentWorkshopTab.Size = new System.Drawing.Size(1174, 562);
            this._contentWorkshopTab.TabIndex = 0;
            // 
            // _nexusPage
            // 
            this._nexusPage.Controls.Add(this._nexusTab);
            this._nexusPage.Location = new System.Drawing.Point(4, 42);
            this._nexusPage.Name = "_nexusPage";
            this._nexusPage.Size = new System.Drawing.Size(1174, 562);
            this._nexusPage.TabIndex = 2;
            this._nexusPage.Text = "Nexus";
            this._nexusPage.UseVisualStyleBackColor = false;
            // 
            // _nexusTab
            // 
            this._nexusTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._nexusTab.Location = new System.Drawing.Point(0, 0);
            this._nexusTab.Name = "_nexusTab";
            this._nexusTab.Padding = new System.Windows.Forms.Padding(12);
            this._nexusTab.Size = new System.Drawing.Size(1174, 562);
            this._nexusTab.TabIndex = 0;
            // 
            // _nexusUploadPage
            // 
            this._nexusUploadPage.Controls.Add(this._nexusUploadTab);
            this._nexusUploadPage.Location = new System.Drawing.Point(4, 42);
            this._nexusUploadPage.Name = "_nexusUploadPage";
            this._nexusUploadPage.Size = new System.Drawing.Size(1174, 562);
            this._nexusUploadPage.TabIndex = 3;
            this._nexusUploadPage.Text = "Publish";
            this._nexusUploadPage.UseVisualStyleBackColor = false;
            // 
            // _nexusUploadTab
            // 
            this._nexusUploadTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._nexusUploadTab.Location = new System.Drawing.Point(0, 0);
            this._nexusUploadTab.Name = "_nexusUploadTab";
            this._nexusUploadTab.Padding = new System.Windows.Forms.Padding(12);
            this._nexusUploadTab.Size = new System.Drawing.Size(1174, 562);
            this._nexusUploadTab.TabIndex = 0;
            // 
            // _settingsPage
            // 
            this._settingsPage.Controls.Add(this._settingsTab);
            this._settingsPage.Location = new System.Drawing.Point(4, 42);
            this._settingsPage.Name = "_settingsPage";
            this._settingsPage.Size = new System.Drawing.Size(1174, 562);
            this._settingsPage.TabIndex = 4;
            this._settingsPage.Text = "Settings";
            this._settingsPage.UseVisualStyleBackColor = false;
            // 
            // _settingsTab
            // 
            this._settingsTab.AutoScroll = false;
            this._settingsTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._settingsTab.Location = new System.Drawing.Point(0, 0);
            this._settingsTab.Name = "_settingsTab";
            this._settingsTab.Padding = new System.Windows.Forms.Padding(0);
            this._settingsTab.Size = new System.Drawing.Size(1174, 562);
            this._settingsTab.TabIndex = 0;
            // 
            // _aboutPage
            // 
            this._aboutPage.Controls.Add(this._aboutTab);
            this._aboutPage.Location = new System.Drawing.Point(4, 42);
            this._aboutPage.Name = "_aboutPage";
            this._aboutPage.Size = new System.Drawing.Size(1174, 562);
            this._aboutPage.TabIndex = 5;
            this._aboutPage.Text = "About";
            this._aboutPage.UseVisualStyleBackColor = false;
            // 
            // _aboutTab
            // 
            this._aboutTab.AppVersion = APP_VERSION;
            this._aboutTab.Author = "Coolnether123";
            this._aboutTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._aboutTab.Location = new System.Drawing.Point(0, 0);
            this._aboutTab.Name = "_aboutTab";
            this._aboutTab.Padding = new System.Windows.Forms.Padding(20);
            this._aboutTab.Size = new System.Drawing.Size(1174, 562);
            this._aboutTab.TabIndex = 0;
            // 
            // _gameSetupTab
            // 
            this._gameSetupTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gameSetupTab.Location = new System.Drawing.Point(0, 0);
            this._gameSetupTab.Name = "_gameSetupTab";
            this._gameSetupTab.Padding = new System.Windows.Forms.Padding(20);
            this._gameSetupTab.Size = new System.Drawing.Size(1174, 562);
            this._gameSetupTab.TabIndex = 0;
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(1182, 703);
            this.Controls.Add(this._tabControl);
            this.Controls.Add(this.headerPanel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Sheltered Mod Manager";
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._logoBox)).EndInit();
            this._headerStatusPanel.ResumeLayout(false);
            this._tabControl.ResumeLayout(false);
            this._gameSetupPage.ResumeLayout(false);
            this._modManagerPage.ResumeLayout(false);
            this._contentWorkshopPage.ResumeLayout(false);
            this._nexusPage.ResumeLayout(false);
            this._nexusUploadPage.ResumeLayout(false);
            this._settingsPage.ResumeLayout(false);
            this._aboutPage.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        private void WireEvents()
        {
            // Form events
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;
            this.Move += MainForm_Move;
            this.Resize += MainForm_Resize;
            this.ResizeEnd += MainForm_ResizeEnd;

            // Game setup events
            _gameSetupTab.GamePathChanged += GameSetupTab_GamePathChanged;
            _gameSetupTab.LaunchRequested += GameSetupTab_LaunchRequested;
            _gameSetupTab.ViewGameLogRequested += GameSetupTab_ViewGameLogRequested;

            // Mod manager events
            _modManagerTab.OrderSaved += ModManagerTab_OrderSaved;
            _modManagerTab.NexusSyncCompleted += ModManagerTab_NexusSyncCompleted;
            _nexusTab.InstallCompleted += NexusTab_InstallCompleted;
            _nexusTab.NexusActivity += NexusTab_NexusActivity;
            _nexusUploadTab.NexusActivity += NexusTab_NexusActivity;

            // Settings events
            _settingsTab.SettingsChanged += SettingsTab_SettingsChanged;
            _settingsTab.DarkModeChanged += SettingsTab_DarkModeChanged;
            _settingsTab.ResetWindowRequested += SettingsTab_ResetWindowRequested;
            _settingsTab.NexusOAuthSignInRequested += SettingsTab_NexusOAuthSignInRequested;
            _settingsTab.NexusOAuthSignOutRequested += SettingsTab_NexusOAuthSignOutRequested;
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;

            // Tab change
            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;

            // Unsaved workshop work stays visible from every tab.
            _contentWorkshopTab.DirtyStateChanged += ContentWorkshopTab_DirtyStateChanged;
            _contentWorkshopTab.EnableInstalledPack = delegate(string modId) { return _modManagerTab.EnableModById(modId); };

            // The Mod manager hands Nexus work to the Nexus tab, which owns downloads and sign-in.
            _modManagerTab.UpdateAllRequested += delegate
            {
                _tabControl.SelectedTab = _nexusPage;
                _nexusTab.StartUpdateAll();
            };
            _modManagerTab.NexusInstallRequested += delegate(List<NexusModReference> references)
            {
                _tabControl.SelectedTab = _nexusPage;
                // Nothing started (busy, signed out...): forget the list, or a later batch would re-apply it.
                if (!_nexusTab.InstallReferences(references))
                    _modManagerTab.CancelPendingList();
            };
            _nexusTab.BatchInstallFinished += delegate { _modManagerTab.ApplyPendingList(); };
            _nexusUpdatesLabel.Click += delegate
            {
                _tabControl.SelectedTab = _nexusPage;
                _nexusTab.ShowUpdates();
            };

            // The Nexus tab reuses the Settings OAuth flow; it never handles credentials itself.
            _nexusTab.SignInRequested += delegate { SettingsTab_NexusOAuthSignInRequested(); };
            _nexusTab.OpenSettingsRequested += delegate { _tabControl.SelectedTab = _settingsPage; };
            _nexusTab.AccountRefreshRequested += delegate { RefreshNexusAccountStatusAsync(true); };
            this.HandleCreated += delegate { ManagerTheme.ApplyTitleBar(this); };
        }

        private void ContentWorkshopTab_DirtyStateChanged(object sender, EventArgs e)
        {
            _contentWorkshopPage.Text = _contentWorkshopTab.HasUnsavedChanges
                ? "Content workshop •"
                : "Content workshop";
        }

        private void GameSetupTab_ViewGameLogRequested(object sender, EventArgs e)
        {
            try
            {
                if (!_settings.IsGamePathValid) return;
                
                string gameDir = Path.GetDirectoryName(_settings.GamePath);
                // All SMM-owned logs live under SMM. Do not read stale root-folder logs.
                string logPath = Path.Combine(Path.Combine(gameDir, "SMM"), "mod_manager.log");

                if (File.Exists(logPath))
                {
                    string content = File.ReadAllText(logPath);
                    _gameSetupTab.Log("--- CONTENT OF MOD_MANAGER.LOG ---");
                    _gameSetupTab.Log(content);
                    _gameSetupTab.Log("--- END OF LOG ---");
                }
                else
                {
                    _gameSetupTab.Log("Log file mod_manager.log not found.");
                }
            }
            catch (Exception ex)
            {
                _gameSetupTab.Log("Error reading log: " + ex.Message);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            FitDefaultSizeToScreen();
            ApplySavedWindowPlacement();
            _windowPlacementInitialized = true;

            // Clean up any stale staged Nexus archives/folders from previous runs.
            NexusInstallService.CleanupStartupArtifacts();
            ManagerSelfUpdateService.CleanupStartupArtifacts();

            // Initialize tabs with services and settings
            _gameSetupTab.Initialize(_settings);
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _nexusUploadTab.Initialize(_nexusService, _settings);
            _settingsTab.Initialize(_settings);
            _settingsTab.SetNexusOAuthRegistrationAvailable(_nexusOAuthService != null && _nexusOAuthService.IsRegistrationAvailable);
            _nexusTab.SetSignInAvailable(_nexusOAuthService != null && _nexusOAuthService.IsRegistrationAvailable);
            _contentWorkshopTab.ModsPath = _settings.ModsPath;
            ApplyPublishTabVisibility();
            ApplyDeveloperMode();
            RefreshNexusAccountStatusAsync(true);

            // Apply initial theme
            ApplyTheme(_settings.DarkMode);

            // Mods installed since the previous session get a "New" badge.
            _modManagerTab.NewSinceUtc = _settings.LastSessionUtc == DateTime.MinValue ? DateTime.MaxValue : _settings.LastSessionUtc;
            _settings.LastSessionUtc = DateTime.UtcNow;

            // Refresh mod state at startup (includes background Nexus sync when enabled).
            _modManagerTab.RefreshMods();
            UpdateStatusCounts();

            // On boot always run Nexus checks for manager updates and latest feed when enabled.
            if (_settings.EnableNexusIntegration)
            {
                _nexusTab.CheckManagerUpdateAsync(false);
                _nexusTab.RefreshLatestModsAsync();
            }

            // Start restart poll timer
            StartRestartPollTimer();
            StartNexusProtocolPolling();

            BeginInvoke((MethodInvoker)ShowReleaseNoticeIfNeeded);
            BeginInvoke((MethodInvoker)CheckForUnreportedCrash);
            if (!string.IsNullOrEmpty(_startupNexusLink))
                BeginInvoke((MethodInvoker)delegate { HandleNexusProtocolLink(_startupNexusLink); });
        }

        private void StartNexusProtocolPolling()
        {
            if (_nexusProtocolPollTimer != null)
                return;

            _nexusProtocolPollTimer = new Timer();
            _nexusProtocolPollTimer.Interval = 750;
            _nexusProtocolPollTimer.Tick += delegate
            {
                IList<string> links = NexusProtocolHandlerService.DequeuePendingLinks();
                for (int i = 0; i < links.Count; i++)
                    HandleNexusProtocolLink(links[i]);
            };
            _nexusProtocolPollTimer.Start();
        }

        private void HandleNexusProtocolLink(string rawLink)
        {
            if (_nexusTab == null)
                return;

            if (_tabControl != null && _nexusPage != null)
                _tabControl.SelectedTab = _nexusPage;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            _nexusTab.HandleNxmLinkAsync(rawLink);
        }

        private void ShowReleaseNoticeIfNeeded()
        {
            if (_settings == null)
                return;

            if (string.Equals(_settings.LastSeenReleaseNoticeVersion, APP_VERSION, StringComparison.OrdinalIgnoreCase))
                return;

            if (!IsUpgradeFromModApi122To20(_startupPreviousInstalledModApiVersion, _settings.InstalledModApiVersion))
                return;

            string message =
                "Sheltered Mod Manager 2.0 uses a new mod system.\n\n" +
                "Mods made for SMM 1.2.2 or the old 1.3 beta line may not work until they are rebuilt for ModAPI/ShelteredAPI 2.0.\n\n" +
                "Use 2.0 versions of mods when available, back up saves before switching, and check save/mod compatibility warnings before loading old saves.";

            MessageBox.Show(this, message, "SMM 2.0 Compatibility Notice",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);

            _settings.LastSeenReleaseNoticeVersion = APP_VERSION;
            SaveSettingsFromUi();
        }

        private static bool IsUpgradeFromModApi122To20(string previousVersion, string currentVersion)
        {
            Version previous;
            Version current;
            if (!TryParseVersion(previousVersion, out previous) || !TryParseVersion(currentVersion, out current))
                return false;

            return previous.Major == 1 &&
                   previous.Minor == 2 &&
                   previous.Build == 2 &&
                   current.Major == 2 &&
                   current.Minor == 0;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;

            string text = (value ?? string.Empty).Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(1);

            int suffixIndex = text.IndexOfAny(new char[] { '-', '+', ' ' });
            if (suffixIndex >= 0)
                text = text.Substring(0, suffixIndex);

            try
            {
                version = new Version(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void NexusTab_InstallCompleted()
        {
            // Installation changes local versions immediately; bypass Nexus cooldown cache.
            _modManagerTab.InvalidateNexusCache();
            // Re-discover local mods so installed/update status reflects the new install immediately.
            _modManagerTab.RefreshMods();
            UpdateStatusCounts();
            _nexusTab.RefreshLatestModsAsync(true);
        }

        private void NexusTab_NexusActivity(string message)
        {
            if (_gameSetupTab == null || string.IsNullOrEmpty(message))
                return;

            _gameSetupTab.Log("Nexus: " + message);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_contentWorkshopTab != null && !_contentWorkshopTab.ConfirmClose())
            {
                e.Cancel = true;
                return;
            }

            CaptureWindowPlacement();

            RefreshRuntimeWritableSettings();
            // Save settings on close
            _settingsService.Save(_settings);
            
            // Stop timer
            if (_restartPollTimer != null)
            {
                _restartPollTimer.Stop();
                _restartPollTimer.Dispose();
            }
            if (_nexusProtocolPollTimer != null)
            {
                _nexusProtocolPollTimer.Stop();
                _nexusProtocolPollTimer.Dispose();
                _nexusProtocolPollTimer = null;
            }
            NexusProtocolHandlerService.RestorePreviousHandler();
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Refresh mods when switching to mod manager tab
            if (_tabControl.SelectedTab == _modManagerPage && _settings.IsModsPathValid)
            {
                _modManagerTab.RefreshMods();
            }
            else if (_tabControl.SelectedTab == _nexusPage)
            {
                _nexusTab.RefreshLatestModsAsync();
            }
            else if (_tabControl.SelectedTab == _nexusUploadPage && _settings.IsModsPathValid)
            {
                _modManagerTab.RefreshMods();
            }
        }

        /// <summary>
        /// Developer mode (Settings) reveals IDs, logs and authoring tools across
        /// the manager; without it each tab keeps to what players need.
        /// </summary>
        private void ApplyDeveloperMode()
        {
            bool developer = _settings != null && _settings.DevMode;
            _contentWorkshopTab.DeveloperMode = developer;
            _gameSetupTab.DeveloperMode = developer;
            _modManagerTab.DeveloperMode = developer;
            _nexusTab.DeveloperMode = developer;
        }

        private void ApplyPublishTabVisibility()
        {
            if (_tabControl == null || _nexusUploadPage == null)
                return;

            bool shouldShow = _settings != null && _settings.EnableNexusIntegration && _settings.EnableExperimentalPublishTab;
            bool isShown = _tabControl.TabPages.Contains(_nexusUploadPage);

            if (shouldShow == isShown)
                return;

            if (shouldShow)
            {
                int settingsIndex = _tabControl.TabPages.Contains(_settingsPage)
                    ? _tabControl.TabPages.IndexOf(_settingsPage)
                    : _tabControl.TabPages.Count;
                _tabControl.TabPages.Insert(settingsIndex, _nexusUploadPage);
                return;
            }

            if (_tabControl.SelectedTab == _nexusUploadPage)
                _tabControl.SelectedTab = _settingsPage != null && _tabControl.TabPages.Contains(_settingsPage)
                    ? _settingsPage
                    : _gameSetupPage;

            _tabControl.TabPages.Remove(_nexusUploadPage);
        }

        private void GameSetupTab_GamePathChanged(string newPath)
        {
            if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
            {
                _settings.GamePath = newPath;
                _settings.ModsPath = Path.Combine(Path.GetDirectoryName(newPath), "mods");
                
                var installedApiVersions = DetectInstalledApiVersions(_settings);
                ApplyInstalledApiVersions(_settings, installedApiVersions);
                
                // Recreate discovery service with new version
                _discoveryService = new ModDiscoveryService(installedApiVersions);
                _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
                _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
                _nexusUploadTab.Initialize(_nexusService, _settings);
                
                // Setup doorstop
                try
                {
                }
                catch { }
                
                // Save and refresh
                _settingsService.Save(_settings);
                
                if (_settings.IsModsPathValid)
                {
                    _modManagerTab.RefreshMods();
                    UpdateStatusCounts();
                }
            }
        }

        private void GameSetupTab_LaunchRequested(bool withMods)
        {
            try
            {
                if (!_settings.IsGamePathValid)
                {
                    MessageBox.Show("Game path is not configured.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // The running game may have changed its remembered prompt choice.
                RefreshRuntimeWritableSettings();

                // Save settings before launch
                SaveSettingsFromUi();

                if (withMods)
                {
                    // Setup doorstop and launch with mods
                    // This would call into the existing ManagerGUI.LaunchAndPreflight.cs logic
                    LaunchWithMods();
                }
                else
                {
                    // Launch vanilla (disable doorstop)
                    LaunchVanilla();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch game: " + ex.Message, "Launch Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchWithMods()
        {
            if (CheckGameRunning())
            {
                MessageBox.Show("Sheltered is already running. Please close it before launching via Manager.", 
                    "Game Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int enabledModCount = ReconcileLoadOrderForLaunch();
            _gameSetupTab.Log("Launching Sheltered with " + FormatCount(enabledModCount, "mod") + ".");
            SetupDoorstop();
            if (!PreflightCheck()) return;
            DeleteLegacyRootLogs(Path.GetDirectoryName(_settings.GamePath));

            try
            {
                try
                {
                    var gameDir = Path.GetDirectoryName(_settings.GamePath);
                    var smmDir = Path.Combine(gameDir, "SMM");
                    var binDir = Path.Combine(smmDir, "bin");
                    if (!Directory.Exists(binDir)) Directory.CreateDirectory(binDir);
                    
                    var iniPath = Path.Combine(binDir, "mod_manager.ini");
                    var lines = new System.Collections.Generic.List<string>();
                    lines.Add("# Generated by Manager at launch");
                    lines.Add("GamePath=" + (_settings.GamePath ?? string.Empty));
                    lines.Add("DarkMode=" + (_settings.DarkMode ? "True" : "False"));
                    lines.Add("DevMode=" + (_settings.DevMode ? "True" : "False"));
                    lines.Add("LogLevel=" + (_settings.LogLevel ?? "Info"));
                    lines.Add("DebugLogScope=" + (_settings.DebugLogScope ?? AppSettings.DebugLogScopeMod));
                    lines.Add("AutoCondenseSaves=" + (_settings.AutoCondenseSaves ?? "ask"));
                    lines.Add("SaveBackupRetention=" + AppSettings.FormatSaveBackupRetention(_settings.SaveBackupRetention));
                    
                    // LogCategories disabled in v1.0 - category filtering not currently used
                    lines.Add("IgnoreOrderChecks=" + (_settings.IgnoreOrderChecks ? "True" : "False"));
                    lines.Add("AutoLoadSaveSlot=" + _settings.AutoLoadSaveSlot);
                    File.WriteAllLines(iniPath, lines.ToArray());
                }
                catch { }

                if (!_settings.IgnoreOrderChecks)
                {
                    try
                    {
                        var allMods = _discoveryService.DiscoverMods(_settings.ModsPath);
                        var enabledMods = _orderService.GetEnabledMods(allMods, _settings.ModsPath);
                        var validation = _orderService.ValidateOrder(enabledMods, _settings.ModsPath, _settings.SkipHarmonyDependencyCheck);

                        if (validation.HasIssues)
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("Mod dependency issues detected. Continue anyway?");
                            if (validation.HardIssueModIds.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine("Critical Issues:");
                                foreach (var id in validation.HardIssueModIds)
                                    sb.AppendLine("- " + id);
                            }
                            if (validation.SoftIssueModIds.Count > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine("Warnings:");
                                foreach (var id in validation.SoftIssueModIds)
                                    sb.AppendLine("- " + id);
                            }

                            var choice = MessageBox.Show(
                                sb.ToString(),
                                "Load Order Issues",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning,
                                MessageBoxDefaultButton.Button2);

                            if (choice != DialogResult.Yes) return;
                        }
                    }
                    catch { }
                }

                // Launch the game
                var startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = _settings.GamePath;
                string launchGameDir = Path.GetDirectoryName(_settings.GamePath);
                startInfo.WorkingDirectory = launchGameDir;
                startInfo.UseShellExecute = false;
                DateTime launchStartedAt = DateTime.Now;
                var process = System.Diagnostics.Process.Start(startInfo);
                
                StartModLaunchVerification(process, launchGameDir, launchStartedAt);
                StartCrashWatch(launchGameDir, launchStartedAt);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class ModLaunchVerificationContext
        {
            public System.Diagnostics.Process Process;
            public string GameDir;
            public DateTime LaunchStartedAt;
        }

        private sealed class ModLaunchStatus
        {
            public bool StartupComplete;
            public bool Fatal;
            public string Message;
        }

        private void StartModLaunchVerification(System.Diagnostics.Process process, string gameDir, DateTime launchStartedAt)
        {
            if (process == null || string.IsNullOrEmpty(gameDir))
            {
                _gameSetupTab.Log("SMM launch verification skipped because the game process could not be inspected.");
                return;
            }

            var context = new ModLaunchVerificationContext();
            context.Process = process;
            context.GameDir = gameDir;
            context.LaunchStartedAt = launchStartedAt;
            System.Threading.ThreadPool.QueueUserWorkItem(VerifyModLaunchWorker, context);
        }

        private void VerifyModLaunchWorker(object state)
        {
            var context = state as ModLaunchVerificationContext;
            if (context == null)
                return;

            string logPath = Path.Combine(Path.Combine(context.GameDir, "SMM"), "mod_manager.log");
            DateTime deadline = DateTime.Now.AddMilliseconds(ModLaunchVerificationTimeoutMs);
            string lastStatus = "waiting for SMM/mod_manager.log";
            bool initialProcessExitNoted = false;

            while (DateTime.Now < deadline)
            {
                string content = ReadFreshLaunchLog(logPath, context.LaunchStartedAt);
                if (!string.IsNullOrEmpty(content))
                {
                    ModLaunchStatus status = InspectModLaunchLog(content);
                    if (!string.IsNullOrEmpty(status.Message))
                        lastStatus = status.Message;

                    if (status.StartupComplete)
                    {
                        _gameSetupTab.Log(status.Message);
                        return;
                    }

                    if (status.Fatal)
                    {
                        _gameSetupTab.Log("SMM failed to load: " + status.Message);
                        return;
                    }
                }

                if (HasProcessExited(context.Process))
                {
                    if (!initialProcessExitNoted)
                    {
                        initialProcessExitNoted = true;
                        lastStatus = lastStatus + "; initial Sheltered process exited, continuing to watch SMM log";
                    }
                }

                System.Threading.Thread.Sleep(1000);
            }

            _gameSetupTab.Log("SMM launch verification timed out after 60 seconds. Last status: " + lastStatus + ". Check SMM\\mod_manager.log.");
        }

        // ------------------------------------------------------------ crash reports

        /// <summary>
        /// Watches a modded session: waits for the game to start, then for it
        /// to close, and offers the session's logs if it left a crash folder.
        /// </summary>
        private void StartCrashWatch(string gameDir, DateTime launchStartedAt)
        {
            if (string.IsNullOrEmpty(gameDir) || _settings == null || string.IsNullOrEmpty(_settings.GamePath))
                return;
            string processName = Path.GetFileNameWithoutExtension(_settings.GamePath);
            DateTime startedUtc = launchStartedAt.ToUniversalTime();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                // Steam and Epic can relaunch the game, so follow it by process name.
                DateTime giveUp = DateTime.UtcNow.AddMinutes(3);
                bool seen = false;
                while (true)
                {
                    bool running = IsProcessRunning(processName);
                    if (running)
                        seen = true;
                    else if (seen || DateTime.UtcNow > giveUp)
                        break;
                    System.Threading.Thread.Sleep(2000);
                }
                if (!seen)
                    return;
                // Unity finishes writing its crash files as the process ends.
                System.Threading.Thread.Sleep(1500);
                List<string> crashes = _crashReports.FindCrashFolders(gameDir, startedUtc.AddSeconds(-5));
                if (crashes.Count == 0 || IsDisposed || Disposing)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate { OfferCrashReport(gameDir, crashes[0], startedUtc.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1)); });
                }
                catch
                {
                    // The manager closed while the game ran.
                }
            });
        }

        /// <summary>
        /// On start, offers logs for a crash that happened while the manager
        /// was closed. The first run only records the time, so old crash
        /// folders are never offered.
        /// </summary>
        private void CheckForUnreportedCrash()
        {
            if (_settings == null || !_settings.IsGamePathValid)
                return;
            DateTime since = _settings.LastCrashCheckUtc;
            _settings.LastCrashCheckUtc = DateTime.UtcNow;
            if (since == DateTime.MinValue || CheckGameRunning())
                return;
            string gameDir = Path.GetDirectoryName(_settings.GamePath);
            List<string> crashes = _crashReports.FindCrashFolders(gameDir, since);
            if (crashes.Count == 0)
                return;
            DateTime crashedUtc;
            try { crashedUtc = Directory.GetCreationTimeUtc(crashes[0]); }
            catch { crashedUtc = DateTime.UtcNow; }
            OfferCrashReport(gameDir, crashes[0], crashedUtc.AddHours(-3), crashedUtc.AddMinutes(10));
        }

        private void OfferCrashReport(string gameDir, string crashFolder, DateTime fromUtc, DateTime toUtc)
        {
            _settings.LastCrashCheckUtc = DateTime.UtcNow;
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();

            bool includeDump;
            using (ManagerDialog dialog = new ManagerDialog(
                "Game crash",
                "Sheltered closed unexpectedly",
                "It looks like the game crashed. Copy the logs from this session so you can send them to the mod's developer or the SMM team; they show what went wrong. Logs can include folder names from your PC."))
            {
                CheckBox dump = dialog.AddCheck("Also include the crash dump (a larger file; only if a developer asks for it)", false);
                dialog.AddButton("Copy logs", DialogResult.OK, ActionButtonKind.Primary);
                dialog.AddButton("Not now", DialogResult.Cancel, ActionButtonKind.Quiet);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                includeDump = dump.Checked;
            }

            string error;
            string zipPath;
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                zipPath = _crashReports.CreateReport(gameDir, crashFolder, fromUtc, toUtc, includeDump, BuildCrashSessionSummary(), out error);
            }
            finally
            {
                Cursor.Current = previous;
            }
            if (zipPath == null)
            {
                using (ManagerDialog failed = new ManagerDialog("Game crash", "The logs couldn't be collected", error))
                {
                    failed.AddButton("Close", DialogResult.OK, ActionButtonKind.Primary);
                    failed.ShowDialog(this);
                }
                return;
            }

            bool copied = false;
            try
            {
                var files = new System.Collections.Specialized.StringCollection();
                files.Add(zipPath);
                Clipboard.SetFileDropList(files);
                copied = true;
            }
            catch
            {
                // The file is still saved; the dialog says where.
            }
            _gameSetupTab.Log("Crash logs saved to " + zipPath);

            using (ManagerDialog done = new ManagerDialog(
                "Game crash",
                copied ? "Logs copied" : "Logs saved",
                (copied ? "Paste with Ctrl+V in Discord, a forum post or an email to attach the file. " : string.Empty) +
                "It's saved as “" + Path.GetFileName(zipPath) + "” in Documents\\Sheltered Mod Manager\\Crash reports."))
            {
                done.AddButton("Done", DialogResult.OK, ActionButtonKind.Primary);
                done.AddButton("Show file", DialogResult.Yes, ActionButtonKind.Secondary);
                if (done.ShowDialog(this) == DialogResult.Yes)
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + zipPath + "\""); }
                    catch { }
                }
            }
        }

        private string BuildCrashSessionSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("Sheltered Mod Manager " + APP_VERSION);
            summary.AppendLine("Report created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            summary.AppendLine("Game: " + Path.GetFileName(_settings.GamePath ?? string.Empty));
            summary.AppendLine("Mod loader: " + FormatInstalledApiVersions(_settings));
            try
            {
                var mods = _orderService.GetEnabledMods(_discoveryService.DiscoverMods(_settings.ModsPath), _settings.ModsPath);
                summary.AppendLine("Active mods (" + mods.Count + ", in load order):");
                for (int i = 0; i < mods.Count; i++)
                    summary.AppendLine("  " + (i + 1) + ". " + mods[i].DisplayName + " (" + mods[i].Id + ") " + mods[i].Version);
            }
            catch (Exception ex)
            {
                summary.AppendLine("Active mods: could not be read (" + ex.Message + ")");
            }
            return summary.ToString();
        }

        private static bool IsProcessRunning(string processName)
        {
            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                bool running = processes.Length > 0;
                foreach (var process in processes)
                    process.Dispose();
                return running;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadFreshLaunchLog(string logPath, DateTime launchStartedAt)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return null;

                DateTime minimumWriteTime = launchStartedAt.AddSeconds(-2);
                if (File.GetLastWriteTime(logPath) < minimumWriteTime)
                    return null;

                return File.ReadAllText(logPath);
            }
            catch
            {
                return null;
            }
        }

        private static ModLaunchStatus InspectModLaunchLog(string content)
        {
            var status = new ModLaunchStatus();
            status.Message = "log created";

            if (ContainsOrdinalIgnoreCase(content, "Startup complete"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    content,
                    @"Startup complete(?: in \d+ms)?\. Loaded (\d+) plugin\(s\), (\d+) error\(s\)\.",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    status.StartupComplete = true;
                    string pluginCount = match.Groups[1].Value;
                    string errorCount = match.Groups[2].Value;
                    status.Message = "Mods loaded: " + FormatCount(pluginCount, "plugin") + ", " + FormatCount(errorCount, "error") + ".";
                    return status;
                }

                status.StartupComplete = true;
                status.Message = "Mods loaded successfully.";
                return status;
            }

            if (ContainsOrdinalIgnoreCase(content, "ERROR: ModAPI.dll not found"))
                return FatalLaunchStatus("ModAPI.dll is missing from the SMM folder.");

            if (ContainsOrdinalIgnoreCase(content, "ERROR: ModAPI.Core.PluginManager type not found"))
                return FatalLaunchStatus("ModAPI.Core.PluginManager was not found in ModAPI.dll.");

            if (ContainsOrdinalIgnoreCase(content, "ERROR: getInstance method not found"))
                return FatalLaunchStatus("PluginManager.getInstance was not found in ModAPI.dll.");

            if (ContainsOrdinalIgnoreCase(content, "ERROR: loadAssemblies method not found"))
                return FatalLaunchStatus("PluginManager.loadAssemblies was not found in ModAPI.dll.");

            if (ContainsOrdinalIgnoreCase(content, "ERROR: Camera timeout"))
                return FatalLaunchStatus("Mod support could not start. Check the SMM log for details.");

            if (ContainsOrdinalIgnoreCase(content, "Timeout: Unity log callback was not received"))
                return FatalLaunchStatus("Mod support could not start. Check the SMM log for details.");

            if (ContainsOrdinalIgnoreCase(content, "CRITICAL while creating runner"))
                return FatalLaunchStatus("Mod support could not start. Check the SMM log for details.");

            if (ContainsOrdinalIgnoreCase(content, "[Bootstrap] ERROR:"))
                return FatalLaunchStatus("Mod support could not start. Check SMM\\mod_manager.log for details.");

            if (ContainsOrdinalIgnoreCase(content, "LoadAndInitializePlugins complete"))
            {
                status.Message = "Loading mods...";
                return status;
            }

            if (ContainsOrdinalIgnoreCase(content, "Handoff to ModAPI complete"))
            {
                status.Message = "Starting mod support...";
                return status;
            }

            if (ContainsOrdinalIgnoreCase(content, "Invoking PluginManager.loadAssemblies"))
            {
                status.Message = "Loading mods...";
                return status;
            }

            if (ContainsOrdinalIgnoreCase(content, "First Unity log callback received"))
            {
                status.Message = "Starting mod support...";
                return status;
            }

            if (ContainsOrdinalIgnoreCase(content, "Registered Unity log callback"))
            {
                status.Message = "Starting mod support...";
                return status;
            }

            if (ContainsOrdinalIgnoreCase(content, "Sheltered Mod Manager starting"))
            {
                status.Message = "Starting mod support...";
                return status;
            }

            return status;
        }

        private static ModLaunchStatus FatalLaunchStatus(string message)
        {
            var status = new ModLaunchStatus();
            status.Fatal = true;
            status.Message = message;
            return status;
        }

        private static string FormatCount(int count, string singular)
        {
            return count + " " + singular + (count == 1 ? string.Empty : "s");
        }

        private static string FormatCount(string count, string singular)
        {
            int parsedCount;
            return int.TryParse(count, out parsedCount)
                ? FormatCount(parsedCount, singular)
                : count + " " + singular + "s";
        }

        private static bool ContainsOrdinalIgnoreCase(string text, string value)
        {
            if (text == null || value == null)
                return false;

            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasProcessExited(System.Diagnostics.Process process)
        {
            try
            {
                if (process == null)
                    return true;

                process.Refresh();
                return process.HasExited;
            }
            catch
            {
                return true;
            }
        }

        private static string NormalizeModId(string id)
        {
            return (id ?? string.Empty).Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Reconciles load order before launch: migrates known renamed IDs and removes entries
        /// that are no longer discoverable on disk.
        /// </summary>
        private int ReconcileLoadOrderForLaunch()
        {
            try
            {
                if (_settings == null || !_settings.IsModsPathValid) return 0;
                string orderPath = Path.Combine(_settings.ModsPath, "loadorder.json");

                var allMods = _discoveryService.DiscoverMods(_settings.ModsPath);
                var discoveredById = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mod in allMods)
                {
                    var id = NormalizeModId(mod.Id);
                    if (!string.IsNullOrEmpty(id) && !discoveredById.ContainsKey(id))
                        discoveredById[id] = mod.Id;
                }

                var existingOrder = _orderService.ReadOrder(_settings.ModsPath) ?? new string[0];
                if (existingOrder.Length == 0)
                {
                    // Critical behavior: when launching via Manager, ensure an explicit loadorder file exists.
                    // Missing file causes runtime fallback to "load all discovered mods".
                    if (!File.Exists(orderPath))
                    {
                        _orderService.SaveOrder(_settings.ModsPath, existingOrder);
                        _gameSetupTab.Log("Created explicit empty loadorder.json (0 enabled mods).");
                    }
                    return 0;
                }

                var reconciled = new System.Collections.Generic.List<string>();
                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int migrated = 0;
                int removed = 0;

                foreach (var raw in existingOrder)
                {
                    string originalNorm = NormalizeModId(raw);
                    if (string.IsNullOrEmpty(originalNorm)) continue;

                    string candidateNorm = originalNorm;
                    string migratedTo;
                    if (KnownModIdMigrations.TryGetValue(originalNorm, out migratedTo))
                    {
                        candidateNorm = NormalizeModId(migratedTo);
                        if (!string.Equals(originalNorm, candidateNorm, StringComparison.OrdinalIgnoreCase))
                            migrated++;
                    }

                    string canonicalId;
                    if (discoveredById.TryGetValue(candidateNorm, out canonicalId))
                    {
                        if (seen.Add(canonicalId))
                            reconciled.Add(canonicalId);
                    }
                    else
                    {
                        removed++;
                    }
                }

                bool changed = (migrated > 0) || (removed > 0) || (reconciled.Count != existingOrder.Length);
                if (changed)
                {
                    _orderService.SaveOrder(_settings.ModsPath, reconciled);
                    _gameSetupTab.Log(string.Format(
                        "Reconciled load order: {0} -> {1} entries ({2} migrated, {3} removed).",
                        existingOrder.Length, reconciled.Count, migrated, removed));
                }
                return reconciled.Count;
            }
            catch (Exception ex)
            {
                _gameSetupTab.Log("Load order reconciliation failed: " + ex.Message);
                return 0;
            }
        }

        private void MainForm_Move(object sender, EventArgs e)
        {
            CaptureWindowPlacement();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            CaptureWindowPlacement();
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            CaptureWindowPlacement();
        }

        private void ApplySavedWindowPlacement()
        {
            try
            {
                if (_settings == null) return;
                if (_settings.WindowWidth <= 0 || _settings.WindowHeight <= 0) return;
                if (_settings.WindowX == int.MinValue || _settings.WindowY == int.MinValue) return;

                int minWidth = Math.Max(this.MinimumSize.Width, 640);
                int minHeight = Math.Max(this.MinimumSize.Height, 480);
                Rectangle savedArea = Screen.FromPoint(new Point(_settings.WindowX, _settings.WindowY)).WorkingArea;
                int width = Math.Min(Math.Max(_settings.WindowWidth, minWidth), Math.Max(minWidth, savedArea.Width));
                int height = Math.Min(Math.Max(_settings.WindowHeight, minHeight), Math.Max(minHeight, savedArea.Height));

                var requested = new Rectangle(_settings.WindowX, _settings.WindowY, width, height);
                if (!IsRectangleVisibleOnAnyScreen(requested))
                {
                    var working = Screen.PrimaryScreen != null
                        ? Screen.PrimaryScreen.WorkingArea
                        : Screen.FromPoint(new Point(0, 0)).WorkingArea;

                    int clampedX = Math.Max(working.Left, Math.Min(requested.X, working.Right - requested.Width));
                    int clampedY = Math.Max(working.Top, Math.Min(requested.Y, working.Bottom - requested.Height));
                    requested = new Rectangle(clampedX, clampedY, requested.Width, requested.Height);
                }

                this.StartPosition = FormStartPosition.Manual;
                this.Bounds = requested;

                if (_settings.WindowMaximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
            }
            catch
            {
                // Keep default startup behavior if restoring fails.
            }
        }

        /// <summary>
        /// Keeps the default window inside the working area. On scaled displays
        /// (for example 1080p at 150%) the default size is taller than the screen.
        /// </summary>
        private void FitDefaultSizeToScreen()
        {
            try
            {
                Rectangle working = Screen.FromControl(this).WorkingArea;
                int width = Math.Max(MinimumSize.Width, Math.Min(Width, working.Width));
                int height = Math.Max(MinimumSize.Height, Math.Min(Height, working.Height));
                if (width == Width && height == Height)
                    return;
                Size = new Size(width, height);
                Location = new Point(
                    working.Left + Math.Max(0, (working.Width - width) / 2),
                    working.Top + Math.Max(0, (working.Height - height) / 2));
            }
            catch
            {
                // Keep the designer size if the screen cannot be queried.
            }
        }

        private bool IsRectangleVisibleOnAnyScreen(Rectangle rect)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(rect))
                    return true;
            }
            return false;
        }

        private void CaptureWindowPlacement()
        {
            if (_settings == null) return;
            if (!_windowPlacementInitialized) return;

            Rectangle bounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            _settings.WindowX = bounds.X;
            _settings.WindowY = bounds.Y;
            _settings.WindowWidth = bounds.Width;
            _settings.WindowHeight = bounds.Height;
            _settings.WindowMaximized = this.WindowState == FormWindowState.Maximized;
        }

        private bool CheckGameRunning()
        {
            try
            {
                string procName = Path.GetFileNameWithoutExtension(_settings.GamePath);
                var procs = System.Diagnostics.Process.GetProcessesByName(procName);
                return procs.Length > 0;
            }
            catch { return false; }
        }

        private bool PreflightCheck()
        {
            try
            {
                if (!_settings.IsGamePathValid)
                {
                    MessageBox.Show("Please select a valid Sheltered executable first.", "Launch Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                string gameDir = Path.GetDirectoryName(_settings.GamePath);
                var missing = new System.Collections.Generic.List<string>();

                string winhttpPath = Path.Combine(gameDir, "winhttp.dll");
                if (!File.Exists(winhttpPath)) missing.Add("winhttp.dll (in game folder)");
                else
                {
                    bool gameIs64Bit;
                    bool proxyIs64Bit;
                    if (TryDetectPeIs64Bit(_settings.GamePath, out gameIs64Bit)
                        && TryDetectPeIs64Bit(winhttpPath, out proxyIs64Bit)
                        && gameIs64Bit != proxyIs64Bit)
                    {
                        MessageBox.Show(
                            "Doorstop proxy architecture mismatch.\n\nSheltered.exe is "
                            + (gameIs64Bit ? "64-bit" : "32-bit")
                            + ", but winhttp.dll is "
                            + (proxyIs64Bit ? "64-bit" : "32-bit")
                            + ".\n\nSMM cannot load until the matching Doorstop proxy is installed.",
                            "Launch Blocked",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return false;
                    }
                }

                string smmDir = Path.Combine(gameDir, "SMM");
                string doorstopDllRoot = Path.Combine(smmDir, "Doorstop.dll");
                string doorstopDllBin = Path.Combine(Path.Combine(smmDir, "bin"), "Doorstop.dll");
                if (!File.Exists(doorstopDllRoot) && !File.Exists(doorstopDllBin)) missing.Add("SMM/Doorstop.dll");

                string modapiDll = Path.Combine(smmDir, "ModAPI.dll");
                if (!File.Exists(modapiDll)) missing.Add("SMM/ModAPI.dll");

                string shelteredApiDllRoot = Path.Combine(smmDir, "ShelteredAPI.dll");
                string shelteredApiDllBin = Path.Combine(Path.Combine(smmDir, "bin"), "ShelteredAPI.dll");
                if (!File.Exists(shelteredApiDllRoot) && !File.Exists(shelteredApiDllBin)) missing.Add("SMM/bin/ShelteredAPI.dll");

                if (missing.Count == 0) return true;

                string msg = "Some required files for mod injection are missing:\n\n  - "
                             + string.Join("\n  - ", missing.ToArray())
                             + "\n\nWithout these, mods will not load. Continue launching anyway?";

                var choice = MessageBox.Show(msg, "Missing Files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return choice == DialogResult.Yes;
            }
            catch { return true; }
        }

        private void SetupDoorstop()
        {
            try
            {
                if (!_settings.IsGamePathValid) return;
                string gameDir = Path.GetDirectoryName(_settings.GamePath);



                var searchPaths = new System.Collections.Generic.List<string>();
                searchPaths.Add(_settings.ModsPath);

                try
                {
                    var enabledIds = _orderService.ReadOrder(_settings.ModsPath);
                    var enabledSet = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var id in enabledIds) enabledSet.Add(id);

                    var allMods = _discoveryService.DiscoverMods(_settings.ModsPath);
                    foreach (var mod in allMods)
                    {
                        if (enabledSet.Contains(mod.Id))
                        {
                            string assembliesPath = Path.Combine(mod.RootPath, "Assemblies");
                            if (Directory.Exists(assembliesPath))
                                searchPaths.Add(assembliesPath);
                        }
                    }
                }
                catch { }

                var uniquePaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in searchPaths)
                {
                    if (!string.IsNullOrEmpty(p))
                        uniquePaths.Add(p);
                }

                var relativePaths = new System.Collections.Generic.List<string>();
                foreach (var p in uniquePaths)
                {
                    try { relativePaths.Add(GetRelativePath(gameDir, p)); } catch { relativePaths.Add(p); }
                }

                string dllSearchPath = string.Join(";", relativePaths.ToArray());

                string smmDir = Path.Combine(gameDir, "SMM");
                string doorstopTargetRelative = @"SMM\bin\Doorstop.dll";
                string doorstopBinPath = Path.Combine(Path.Combine(smmDir, "bin"), "Doorstop.dll");
                string doorstopRootPath = Path.Combine(smmDir, "Doorstop.dll");

                if (File.Exists(doorstopBinPath))
                {
                    doorstopTargetRelative = @"SMM\bin\Doorstop.dll";
                }
                else if (File.Exists(doorstopRootPath))
                {
                    doorstopTargetRelative = @"SMM\Doorstop.dll";
                }
                else
                {
                    _gameSetupTab.Log("Doorstop target assembly missing in both SMM/bin and SMM root. Launch may fail to load ModAPI.");
                }

                string iniPath = Path.Combine(gameDir, "doorstop_config.ini");
                var ini = new System.Collections.Generic.List<string>();
                ini.Add("# Auto-generated by Sheltered Mod Manager");
                ini.Add("[General]");
                ini.Add("enabled=true");
                ini.Add("target_assembly=" + doorstopTargetRelative);
                ini.Add("redirect_output_log=false");
                ini.Add("");
                ini.Add("[UnityMono]");
                ini.Add("dll_search_path_override=" + dllSearchPath);
                ini.Add("debug_enabled=false");
                ini.Add("debug_address=127.0.0.1:10000");
                ini.Add("debug_suspend=false");
                File.WriteAllLines(iniPath, ini.ToArray());

                LogDoorstopProxyStatus(gameDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to configure Doorstop: " + ex.Message,
                    "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteLegacyRootLogs(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir))
                return;

            DeleteLegacyRootLog(gameDir, "mod_manager.log");
            DeleteLegacyRootLog(gameDir, "output_log.txt");
        }

        private void DeleteLegacyRootLog(string gameDir, string fileName)
        {
            try
            {
                string path = Path.Combine(gameDir, fileName);
                if (!File.Exists(path))
                    return;

                File.Delete(path);
                _gameSetupTab.Log("Removed legacy root log: " + fileName);
            }
            catch (Exception ex)
            {
                _gameSetupTab.Log("Could not remove legacy root log '" + fileName + "': " + ex.Message);
            }
        }

        private void LaunchVanilla()
        {
            try
            {
                string gameDir = Path.GetDirectoryName(_settings.GamePath);
                string doorstopConfigPath = Path.Combine(gameDir, "doorstop_config.ini");
                
                var lines = new System.Collections.Generic.List<string>();
                lines.Add("# Auto-generated by Sheltered Mod Manager (Vanilla Mode)");
                lines.Add("[General]");
                lines.Add("enabled=false");
                lines.Add("target_assembly=SMM\\bin\\Doorstop.dll");
                File.WriteAllLines(doorstopConfigPath, lines.ToArray());

                System.Diagnostics.Process.Start(_settings.GamePath);
                _gameSetupTab.Log("Launched Sheltered without mods.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetRelativePath(string fromPath, string toPath)
        {
            Uri fromUri = new Uri(fromPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri toUri = new Uri(toPath);
            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
        }

        private bool TryDetectPeIs64Bit(string path, out bool is64Bit)
        {
            FileStream fs = null;
            BinaryReader br = null;
            is64Bit = false;
            try
            {
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                br = new BinaryReader(fs);
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();
                fs.Seek(peOffset, SeekOrigin.Begin);
                uint peSignature = br.ReadUInt32();
                if (peSignature != 0x00004550) return false;
                ushort machine = br.ReadUInt16();
                if (machine == 0x8664)
                {
                    is64Bit = true;
                    return true;
                }

                if (machine == 0x014c)
                {
                    is64Bit = false;
                    return true;
                }

                return false;
            }
            catch { return false; }
            finally
            {
                if (br != null) br.Close();
                if (fs != null) fs.Close();
            }
        }

        private void LogDoorstopProxyStatus(string gameDir)
        {
            try
            {
                string winhttpPath = Path.Combine(gameDir, "winhttp.dll");
                if (!File.Exists(winhttpPath))
                    return;

                bool gameIs64Bit;
                bool proxyIs64Bit;
                if (!TryDetectPeIs64Bit(_settings.GamePath, out gameIs64Bit)
                    || !TryDetectPeIs64Bit(winhttpPath, out proxyIs64Bit))
                {
                    _gameSetupTab.Log("Doorstop proxy present; architecture could not be verified.");
                    return;
                }
            }
            catch
            {
                _gameSetupTab.Log("Doorstop proxy present; validation failed.");
            }
        }



        private void ModManagerTab_OrderSaved(string[] newOrder)
        {
            UpdateStatusCounts();
        }

        private void ModManagerTab_NexusSyncCompleted(List<ModItem> mods, int mappedMods, int updateCount, string errorMessage)
        {
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _nexusUpdatesLabel.Text = "Nexus: couldn't check for updates";
            }
            else if (!_settings.EnableNexusIntegration)
            {
                _nexusUpdatesLabel.Text = "Nexus: off";
            }
            else
            {
                _nexusUpdatesLabel.Text = updateCount == 0
                    ? "Nexus: all up to date"
                    : "Nexus: " + updateCount + (updateCount == 1 ? " update" : " updates") + " available";
                _headerToolTip.SetToolTip(_nexusUpdatesLabel, mappedMods + (mappedMods == 1 ? " mod is" : " mods are") + " linked to Nexus. Click to see updates.");
            }

            _nexusTab.SetLastCheckedUtc(_modManagerTab.LastNexusRemoteSyncUtc);
            _nexusTab.UpdateInstalledMods(mods, mappedMods, updateCount, errorMessage);
            _nexusUploadTab.UpdateInstalledMods(mods);

            if (_startupNexusUpdateAnnouncementsPending && string.IsNullOrEmpty(errorMessage))
            {
                var source = mods ?? new List<ModItem>();
                var announced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mod in source)
                {
                    if (mod == null || !mod.HasUpdateAvailable)
                        continue;

                    string key = !string.IsNullOrEmpty(mod.Id) ? mod.Id : (mod.DisplayName ?? string.Empty);
                    if (!announced.Add(key))
                        continue;

                    string name = !string.IsNullOrEmpty(mod.DisplayName) ? mod.DisplayName : "Unknown mod";
                    if (_gameSetupTab != null)
                        _gameSetupTab.Log(name + " has an update available!");
                }

                _startupNexusUpdateAnnouncementsPending = false;
            }
        }

        private void SettingsTab_SettingsChanged(AppSettings settings)
        {
            var previous = _settings;
            _settings = settings;
            RecreateNexusService();
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _nexusUploadTab.Initialize(_nexusService, _settings);
            _contentWorkshopTab.ModsPath = _settings.ModsPath;
            ApplyPublishTabVisibility();
            ApplyDeveloperMode();
            SaveSettingsFromUi();

            LogSettingsChanges(previous, _settings, "Settings updated");
            
            // Re-apply theme in case it changed
            ApplyTheme(_settings.DarkMode);
            RefreshNexusAccountStatusAsync(true);

            if (_settings.IsModsPathValid)
                _modManagerTab.RefreshMods();
        }

        private void SettingsService_SettingsChanged(AppSettings settings)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => SettingsService_SettingsChanged(settings)));
                return;
            }

            var previous = _settings;

            // Preserve or re-detect API versions as they are derived from installed SMM files.
            string version = _settings?.InstalledModApiVersion;
            string shelteredVersion = _settings?.InstalledShelteredApiVersion;
            _settings = settings;

            // Session bookkeeping lives in memory until the next save; keep the newer values.
            if (previous != null)
            {
                if (previous.LastSessionUtc > _settings.LastSessionUtc)
                    _settings.LastSessionUtc = previous.LastSessionUtc;
                if (previous.LastCrashCheckUtc > _settings.LastCrashCheckUtc)
                    _settings.LastCrashCheckUtc = previous.LastCrashCheckUtc;
            }

            if (string.IsNullOrEmpty(_settings.InstalledModApiVersion) && !string.IsNullOrEmpty(version))
            {
                _settings.InstalledModApiVersion = version;
            }

            if (string.IsNullOrEmpty(_settings.InstalledShelteredApiVersion) && !string.IsNullOrEmpty(shelteredVersion))
            {
                _settings.InstalledShelteredApiVersion = shelteredVersion;
            }

            if ((string.IsNullOrEmpty(_settings.InstalledModApiVersion) || string.IsNullOrEmpty(_settings.InstalledShelteredApiVersion)) && _settings.IsGamePathValid)
            {
                var installedApiVersions = DetectInstalledApiVersions(_settings);
                ApplyInstalledApiVersions(_settings, installedApiVersions);
                _discoveryService = new ModDiscoveryService(installedApiVersions);
            }

            RecreateNexusService();
            
            // Re-initialize tabs with new settings
            _gameSetupTab.Initialize(_settings);
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _nexusUploadTab.Initialize(_nexusService, _settings);
            _settingsTab.Initialize(_settings);
            _settingsTab.SetNexusOAuthRegistrationAvailable(_nexusOAuthService != null && _nexusOAuthService.IsRegistrationAvailable);
            _nexusTab.SetSignInAvailable(_nexusOAuthService != null && _nexusOAuthService.IsRegistrationAvailable);
            _contentWorkshopTab.ModsPath = _settings.ModsPath;
            ApplyPublishTabVisibility();
            ApplyDeveloperMode();
            
            // Re-apply theme
            ApplyTheme(_settings.DarkMode);
            RefreshNexusAccountStatusAsync(false);
            if (_settings.IsModsPathValid)
                _modManagerTab.RefreshMods();
            UpdateStatusCounts();

            if (_suppressSettingsReloadLogCount > 0)
            {
                _suppressSettingsReloadLogCount--;
            }
            else
            {
                LogSettingsChanges(previous, _settings, "Settings reloaded from disk");
            }
        }

        private void SettingsTab_DarkModeChanged(bool isDark)
        {
            bool wasDark = _settings.DarkMode;
            _settings.DarkMode = isDark;
            ApplyTheme(isDark);
            SaveSettingsFromUi();

            if (wasDark != isDark)
            {
                _gameSetupTab.Log("Settings updated: Dark mode " + (isDark ? "enabled" : "disabled") + ".");
            }
        }

        private void SettingsTab_NexusOAuthSignInRequested()
        {
            if (_nexusOAuthService == null)
                return;

            ApplyNexusAccountStatus(NexusAccountStatus.CreateChecking());
            NexusOAuthService oauthService = _nexusOAuthService;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string errorMessage;
                bool signedIn = oauthService.SignIn(out errorMessage);
                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (signedIn)
                        {
                            RecreateNexusService();
                            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
                            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
                            _nexusUploadTab.Initialize(_nexusService, _settings);
                            _settingsTab.Initialize(_settings);
                            _settingsTab.SetNexusOAuthRegistrationAvailable(_nexusOAuthService.IsRegistrationAvailable);
                            _nexusTab.SetSignInAvailable(_nexusOAuthService.IsRegistrationAvailable);
                            RefreshNexusAccountStatusAsync(true);
                            _gameSetupTab.Log("Signed in to Nexus.");
                        }
                        else
                        {
                            ApplyNexusAccountStatus(NexusAccountStatus.CreateUnavailable(errorMessage));
                            MessageBox.Show(
                                errorMessage ?? "Nexus sign-in was not completed.",
                                "Nexus Sign In",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    });
                }
                catch
                {
                }
            });
        }

        private void SettingsTab_NexusOAuthSignOutRequested()
        {
            if (_nexusOAuthService == null)
                return;

            _nexusOAuthService.SignOut();
            RecreateNexusService();
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _nexusUploadTab.Initialize(_nexusService, _settings);
            _settingsTab.Initialize(_settings);
            _settingsTab.SetNexusOAuthRegistrationAvailable(_nexusOAuthService.IsRegistrationAvailable);
            _nexusTab.SetSignInAvailable(_nexusOAuthService.IsRegistrationAvailable);
            RefreshNexusAccountStatusAsync(true);
            _gameSetupTab.Log("Signed out of Nexus.");
        }

        private void SettingsTab_ResetWindowRequested()
        {
            try
            {
                _settings.WindowX = int.MinValue;
                _settings.WindowY = int.MinValue;
                _settings.WindowWidth = 0;
                _settings.WindowHeight = 0;
                _settings.WindowMaximized = false;

                this.WindowState = FormWindowState.Normal;
                this.Size = new Size(1182, 703);
                this.CenterToScreen();
                FitDefaultSizeToScreen();

                SaveSettingsFromUi();
                _gameSetupTab.Log("Settings updated: Manager window placement reset to default.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to reset window placement: " + ex.Message, "Reset Window",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LogSettingsChanges(AppSettings previous, AppSettings current, string prefix)
        {
            if (_gameSetupTab == null || current == null) return;
            if (previous == null)
            {
                _gameSetupTab.Log(prefix + ".");
                return;
            }

            var changes = new System.Collections.Generic.List<string>();

            if (!string.Equals(previous.GamePath, current.GamePath, StringComparison.OrdinalIgnoreCase))
                changes.Add("GamePath");
            if (!string.Equals(previous.ModsPath, current.ModsPath, StringComparison.OrdinalIgnoreCase))
                changes.Add("ModsPath");
            if (previous.DarkMode != current.DarkMode)
                changes.Add("DarkMode");
            if (previous.DevMode != current.DevMode)
                changes.Add("DevMode");
            if (!string.Equals(previous.LogLevel, current.LogLevel, StringComparison.OrdinalIgnoreCase))
                changes.Add("LogLevel");
            if (!string.Equals(previous.DebugLogScope, current.DebugLogScope, StringComparison.OrdinalIgnoreCase))
                changes.Add("DebugLogScope");
            if (previous.IgnoreOrderChecks != current.IgnoreOrderChecks)
                changes.Add("IgnoreOrderChecks");
            if (previous.SkipHarmonyDependencyCheck != current.SkipHarmonyDependencyCheck)
                changes.Add("SkipHarmonyDependencyCheck");
            if (previous.IncludeNexusPrereleaseFiles != current.IncludeNexusPrereleaseFiles)
                changes.Add("IncludeNexusPrereleaseFiles");
            if (!string.Equals(previous.AutoCondenseSaves, current.AutoCondenseSaves, StringComparison.OrdinalIgnoreCase))
                changes.Add("AutoCondenseSaves");
            if (previous.SaveBackupRetention != current.SaveBackupRetention)
                changes.Add("SaveBackupRetention");
            if (previous.AutoLoadSaveSlot != current.AutoLoadSaveSlot)
                changes.Add("AutoLoadSaveSlot");
            if (previous.EnableNexusIntegration != current.EnableNexusIntegration)
                changes.Add("EnableNexusIntegration");
            if (previous.EnableExperimentalPublishTab != current.EnableExperimentalPublishTab)
                changes.Add("EnableExperimentalPublishTab");
            if (!string.Equals(previous.LastSeenReleaseNoticeVersion, current.LastSeenReleaseNoticeVersion, StringComparison.OrdinalIgnoreCase))
                changes.Add("LastSeenReleaseNoticeVersion");
            if (!string.Equals(previous.NexusGameDomain, current.NexusGameDomain, StringComparison.OrdinalIgnoreCase))
                changes.Add("NexusGameDomain");
            if (previous.ManagerNexusModId != current.ManagerNexusModId)
                changes.Add("ManagerNexusModId");

            if (changes.Count == 0)
            {
                // File watcher notifications can fire more than once for a single write.
                // Keep logs clean by collapsing back-to-back no-change reload messages.
                if (string.Equals(prefix, "Settings reloaded from disk", StringComparison.OrdinalIgnoreCase))
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastNoChangeSettingsReloadLogUtc).TotalMilliseconds < 2000)
                        return;
                    _lastNoChangeSettingsReloadLogUtc = now;
                }

                _gameSetupTab.Log(prefix + ".");
                return;
            }

            _gameSetupTab.Log(prefix + ": " + string.Join(", ", changes.ToArray()) + ".");
        }

        private void SaveSettingsFromUi()
        {
            _suppressSettingsReloadLogCount++;
            _settingsService.Save(_settings);
        }

        private void RefreshRuntimeWritableSettings()
        {
            if (_settings == null || _settingsService == null)
                return;

            _settings.AutoCondenseSaves = _settingsService.LoadAutoCondensePreference();
        }

        private void UpdateStatusCounts()
        {
            if (_settings.IsModsPathValid)
            {
                // Only count mods that are both in load order AND discovered on disk
                var allMods = _discoveryService.DiscoverMods(_settings.ModsPath);
                var enabledMods = _orderService.GetEnabledMods(allMods, _settings.ModsPath);
                int count = enabledMods.Count;

                string apiVersion = FormatInstalledApiVersions(_settings);

                _statusReady = true;
                _statusLabel.Text = "Ready to play";
                _statusLabel.ForeColor = ManagerTheme.Current.Success;
                _modsCountLabel.Text = count == 0 ? "No mods active" : (count == 1 ? "1 mod active" : count + " mods active");
                // Loader versions are support detail: shown on hover, and only
                // surfaced in the header when a loader file is missing.
                _loaderMissing = string.IsNullOrEmpty(_settings.InstalledModApiVersion) || string.IsNullOrEmpty(_settings.InstalledShelteredApiVersion);
                _modApiVersionLabel.Text = "Mod loader not detected";
                _modApiVersionLabel.Visible = _loaderMissing;
                _modApiVersionLabel.ForeColor = ManagerTheme.Current.Warning;
                string loaderDetail = "Mod loader: " + apiVersion;
                _headerToolTip.SetToolTip(_statusLabel, loaderDetail);
                _headerToolTip.SetToolTip(_modsCountLabel, loaderDetail);
                _headerToolTip.SetToolTip(_modApiVersionLabel, loaderDetail);
                _headerStatusPanel.AccessibleDescription = loaderDetail;
                if (!_settings.EnableNexusIntegration)
                    _nexusUpdatesLabel.Text = "Nexus: off";

                // Also update the tab if it still exists/is used
                _gameSetupTab.UpdateStatus(true, count, apiVersion);
                UpdateHeaderPlayButton(count);
            }
            else
            {
                _statusReady = false;
                _statusLabel.Text = "Game not set up";
                _statusLabel.ForeColor = ManagerTheme.Current.Danger;
                _modsCountLabel.Text = "Choose the game in Game setup";
                _loaderMissing = false;
                _modApiVersionLabel.Visible = false;
                _headerToolTip.SetToolTip(_statusLabel, "Mod loader: unknown until the game is set up");
                _nexusUpdatesLabel.Text = _settings.EnableNexusIntegration ? "Nexus: not checked yet" : "Nexus: off";
                UpdateHeaderPlayButton(0);
            }
        }

        private static string FormatInstalledApiVersions(AppSettings settings)
        {
            if (settings == null)
                return "Unknown";

            string modApi = string.IsNullOrEmpty(settings.InstalledModApiVersion)
                ? "missing"
                : settings.InstalledModApiVersion;
            string shelteredApi = string.IsNullOrEmpty(settings.InstalledShelteredApiVersion)
                ? "missing"
                : settings.InstalledShelteredApiVersion;

            return "ModAPI " + modApi + " / ShelteredAPI " + shelteredApi;
        }

        private void ApplyTheme(bool isDark)
        {
            ManagerTheme.Use(isDark);
            ThemePalette palette = ManagerTheme.Current;

            this.BackColor = palette.Window;
            headerPanel.BackColor = palette.Chrome;
            _headerStatusPanel.BackColor = palette.Chrome;
            if (_headerPlayHost != null)
                _headerPlayHost.BackColor = palette.Chrome;
            _titleLabel.ForeColor = palette.Text;
            _subtitleLabel.ForeColor = palette.TextMuted;
            _statusLabel.ForeColor = _statusReady ? palette.Success : palette.Danger;
            _modsCountLabel.ForeColor = palette.TextMuted;
            _modApiVersionLabel.ForeColor = palette.Warning;
            _nexusUpdatesLabel.ForeColor = palette.AccentText;

            // The Publish page is removed from TabPages while hidden, so colour every page explicitly.
            foreach (TabPage page in new TabPage[] { _gameSetupPage, _modManagerPage, _contentWorkshopPage, _nexusPage, _nexusUploadPage, _settingsPage, _aboutPage })
            {
                page.BackColor = palette.Window;
                page.ForeColor = palette.Text;
            }
            _tabControl.Invalidate();
            ManagerTheme.ApplyTitleBar(this);

            // Apply to tabs
            _gameSetupTab.ApplyTheme(isDark);
            _modManagerTab.ApplyTheme(isDark);
            _nexusTab.ApplyTheme(isDark);
            _nexusUploadTab.ApplyTheme(isDark);
            _contentWorkshopTab.ApplyTheme(isDark);
            _settingsTab.ApplyTheme(isDark);
            _aboutTab.ApplyTheme(isDark);
        }

        private void StartRestartPollTimer()
        {
            _restartPollTimer = new Timer();
            _restartPollTimer.Interval = 2000;
            _restartPollTimer.Tick += RestartPollTimer_Tick;
            _restartPollTimer.Start();
        }

        private void RestartPollTimer_Tick(object sender, EventArgs e)
        {
            CheckAndHandleRestartRequest();
        }

        #region Restart Request Handling

        private class RestartRequest
        {
            public string Action;
            public string LoadFromManifest;
            public bool RequireExactManifest;
        }

        private class ManagerSlotManifest
        {
            public ManagerLoadedModInfo[] lastLoadedMods;
        }

        private class ManagerLoadedModInfo
        {
            public string modId;
            public string version;
        }

        private void CheckAndHandleRestartRequest()
        {
            try
            {
                if (!_settings.IsGamePathValid) return;

                // Look for restart.json in SMM/Bin
                var gameDir = Path.GetDirectoryName(_settings.GamePath);
                var restartPath = Path.Combine(Path.Combine(Path.Combine(gameDir, "SMM"), "Bin"), "restart.json");

                if (!File.Exists(restartPath)) return;

                // Found restart request
                RestartRequest req = null;
                try
                {
                    var json = File.ReadAllText(restartPath);
                    req = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<RestartRequest>(json);
                }
                catch
                {
                    // Failed to parse, delete to prevent loops
                    try { File.Delete(restartPath); } catch { }
                    return;
                }

                if (req != null && string.Equals(req.Action, "Restart", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(req.LoadFromManifest))
                {
                    if (!File.Exists(req.LoadFromManifest))
                    {
                        try { File.Delete(restartPath); } catch { }
                        return;
                    }

                    // The in-game requester writes this file before asking Unity to quit.
                    // Keep the request queued until the process exits; consuming
                    // it earlier would delete the request and LaunchWithMods would reject the
                    // relaunch because the old game process is still alive.
                    if (CheckGameRunning())
                        return;

                    // Read Manifest
                    ManagerSlotManifest manifest = null;
                    try
                    {
                        var manifestJson = File.ReadAllText(req.LoadFromManifest);
                        manifest = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<ManagerSlotManifest>(manifestJson);
                    }
                    catch
                    {
                        try { File.Delete(restartPath); } catch { }
                        return;
                    }

                    if (manifest != null)
                    {
                        bool manifestIncludesModList = manifest.lastLoadedMods != null;
                        if (req.RequireExactManifest && !manifestIncludesModList)
                        {
                            _gameSetupTab.Log("Restart request manifest was missing mod metadata - launch cancelled.");
                            try { File.Delete(restartPath); } catch { }
                            return;
                        }

                        var modsFromManifest = manifest.lastLoadedMods ?? new ManagerLoadedModInfo[0];

                        // Extract Mod IDs
                        var newOrder = new System.Collections.Generic.List<string>();
                        foreach (var m in modsFromManifest)
                        {
                            if (!string.IsNullOrEmpty(m.modId)) newOrder.Add(m.modId);
                        }

                        bool hasManifestOrder = req.RequireExactManifest ? manifestIncludesModList : newOrder.Count > 0;

                        // Write Load Order only if manifest explicitly provided mods.
                        if (hasManifestOrder && !string.IsNullOrEmpty(_settings.ModsPath))
                        {
                            _orderService.SaveOrder(_settings.ModsPath, newOrder);
                            _modManagerTab.RefreshMods();
                            UpdateStatusCounts();
                        }
                        else
                        {
                            _gameSetupTab.Log("Restart request manifest contained no mod list - keeping current load order.");
                        }

                        // Validate
                        bool safeToLaunch = true;

                        if (hasManifestOrder)
                        {
                            var allMods = _discoveryService.DiscoverMods(_settings.ModsPath);
                            var enabledMods = _orderService.GetEnabledMods(allMods, _settings.ModsPath);
                            var validation = _orderService.ValidateOrder(enabledMods, _settings.ModsPath, _settings.SkipHarmonyDependencyCheck);

                            if (validation.HasIssues)
                            {
                                safeToLaunch = false;
                                MessageBox.Show("The save's mod list has dependency issues (missing mods or cycles).\n\nPlease review the load order before launching.",
                                    "Restart Interrupted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }

                        // Delete restart file
                        try { File.Delete(restartPath); } catch { }

                        // Launch if safe
                        if (safeToLaunch)
                        {
                            _gameSetupTab.Log("ModAPI restart request - relaunching...");
                            LaunchWithMods();
                        }
                    }
                }
                else
                {
                    // Invalid request, cleanup
                    try { File.Delete(restartPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                // Log error
                _gameSetupTab.Log("Restart handling error: " + ex.Message);
            }
        }

        #endregion

    }
}

