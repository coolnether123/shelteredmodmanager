using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Core;
using Manager.Core.Games;
using Manager.Core.Games.Models;
using Manager.Core.Games.Services;
using Manager.Core.Models;
using Manager.Core.Services;
using Manager.Views;

namespace Manager
{
    /// <summary>
    /// Modern, refactored main form for the selected game profile.
    /// Uses separation of concerns with dedicated services and custom controls.
    /// </summary>
    public class MainForm : Form
    {
        // Header
        private PictureBox _logoBox;
        private Label _titleLabel;

        // Tab control
        private TabControl _tabControl;
        private TabPage _gameSetupPage;
        private TabPage _modManagerPage;
        private TabPage _nexusPage;
        private TabPage _settingsPage;
        private TabPage _aboutPage;

        // Status Header
        private Label _statusLabel;
        private Label _modsCountLabel;
        private Label _modApiVersionLabel;
        private Label _nexusUpdatesLabel;
        private Panel _headerStatusPanel;
        private ModManagerTab _modManagerTab;
        private NexusModsTab _nexusTab;
        private SettingsTab _settingsTab;
        private AboutTab _aboutTab;

        // Services
        private SettingsService _settingsService;
        private ModDiscoveryService _discoveryService;
        private LoadOrderService _orderService;
        private NexusModsService _nexusService;
        private GameProfileRegistry _gameProfileRegistry;
        private GameProfile _gameProfile;
        private GamePreflightService _preflightService;
        private GameProcessLauncher _processLauncher;
        private GameLaunchConfigurationService _launchConfigurationService;
        private DoorstopConfigurationService _doorstopConfigurationService;

        // State
        private AppSettings _settings;
        private Timer _restartPollTimer;
        private Panel headerPanel;
        private GameSetupTab _gameSetupTab;
        private bool _windowPlacementInitialized;
        private int _suppressSettingsReloadLogCount;
        private DateTime _lastNoChangeSettingsReloadLogUtc = DateTime.MinValue;
        private bool _startupNexusUpdateAnnouncementsPending = true;
        private int _nexusAccountRequestToken;
        private const string APP_VERSION = AppVersionInfo.Display;
        private static readonly System.Collections.Generic.Dictionary<string, string> KnownModIdMigrations =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Historical IDs seen in user loadorder files/logs.
                { "com.coolnether123.pluginconsole", "coolnether123.pluginconsole" },
                { "com.plugin.harmony.example", "coolnether123.harmonyexample" }
            };

        public MainForm()
        {
            InitializeServices();
            InitializeComponent();
            InitializeCustomResources();
            WireEvents();
        }

        private void InitializeCustomResources()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                
                ApplyGameProfileToChrome();
                
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
            _gameProfileRegistry = GameProfileRegistry.CreateDefault();
            _settingsService = new SettingsService(_gameProfileRegistry);
            _orderService = new LoadOrderService();
            _preflightService = new GamePreflightService();
            _processLauncher = new GameProcessLauncher();
            _launchConfigurationService = new GameLaunchConfigurationService();
            _doorstopConfigurationService = new DoorstopConfigurationService();
            
            // Settings loaded first to get ModAPI version path
            _settings = _settingsService.Load();
            _gameProfile = _gameProfileRegistry.Resolve(_settings.SelectedGameId);
            NormalizeSettingsForProfile();
            
            var installedApiVersions = DetectInstalledApiVersions(_settings);
            ApplyInstalledApiVersions(_settings, installedApiVersions);

            _discoveryService = new ModDiscoveryService(_gameProfile, installedApiVersions);
            _nexusService = new NexusModsService(_settings.NexusApiKey, _gameProfile.ManagerTitle);
        }

        private Dictionary<string, string> DetectInstalledApiVersions(AppSettings settings)
        {
            if (settings == null || !settings.IsGamePathValid)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string gameDir = Path.GetDirectoryName(settings.GamePath);
                string runtimePath = _gameProfile.RuntimeLayout.GetRuntimePath(gameDir);
                return AssemblyVersionChecker.GetInstalledApiVersions(runtimePath, _gameProfile.GetApiAssemblyNames());
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void ApplyGameProfileToChrome()
        {
            if (_gameProfile == null)
                return;

            this.Text = _gameProfile.ManagerTitle + " v" + APP_VERSION;
            if (_titleLabel != null)
                _titleLabel.Text = _gameProfile.ManagerTitle;
            if (_aboutTab != null)
                _aboutTab.ApplyGameProfile(_gameProfile);
            if (_gameSetupTab != null)
                _gameSetupTab.SetGameProfiles(_gameProfileRegistry.GetAll(), _gameProfile);
            if (_settingsTab != null)
                _settingsTab.ApplyGameProfile(_gameProfile);
        }

        private static void ApplyInstalledApiVersions(AppSettings settings, Dictionary<string, string> versions)
        {
            if (settings == null || versions == null)
                return;

            settings.InstalledApiVersions = new Dictionary<string, string>(versions, StringComparer.OrdinalIgnoreCase);

            string version;
            if (versions.TryGetValue("ModAPI", out version))
                settings.InstalledModApiVersion = version;
            else
                settings.InstalledModApiVersion = string.Empty;
            if (versions.TryGetValue("ShelteredAPI", out version))
                settings.InstalledShelteredApiVersion = version;
            else
                settings.InstalledShelteredApiVersion = string.Empty;
        }

        private void NormalizeSettingsForProfile()
        {
            if (_settings == null || _gameProfile == null)
                return;

            _settings.SelectedGameId = _gameProfile.Id;
            if (_settings.IsGamePathValid)
                _settings.ModsPath = _gameProfile.GetModsPath(_settings.GamePath);

            if (string.IsNullOrEmpty(_settings.NexusGameDomain))
                _settings.NexusGameDomain = _gameProfile.DefaultNexusGameDomain ?? string.Empty;

            if (_settings.ManagerNexusModId <= 0 && _gameProfile.DefaultManagerNexusModId > 0)
                _settings.ManagerNexusModId = _gameProfile.DefaultManagerNexusModId;
        }

        private void RecreateNexusService()
        {
            _nexusService = new NexusModsService(
                _settings != null ? _settings.NexusApiKey : string.Empty,
                _gameProfile != null ? _gameProfile.ManagerTitle : "Mod Manager");
        }

        private void ApplyNexusAccountStatus(NexusAccountStatus status)
        {
            if (_settingsTab != null)
                _settingsTab.SetNexusAccountStatus(status);
            if (_nexusTab != null)
                _nexusTab.SetAccountStatus(status);
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

            if (string.IsNullOrEmpty(_settings.NexusApiKey))
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
            this._headerStatusPanel = new System.Windows.Forms.Panel();
            this._nexusUpdatesLabel = new System.Windows.Forms.Label();
            this._modApiVersionLabel = new System.Windows.Forms.Label();
            this._modsCountLabel = new System.Windows.Forms.Label();
            this._statusLabel = new System.Windows.Forms.Label();
            this._tabControl = new System.Windows.Forms.TabControl();
            this._gameSetupPage = new System.Windows.Forms.TabPage();
            this._modManagerPage = new System.Windows.Forms.TabPage();
            this._modManagerTab = new Manager.Views.ModManagerTab();
            this._nexusPage = new System.Windows.Forms.TabPage();
            this._nexusTab = new Manager.Views.NexusModsTab();
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
            this._nexusPage.SuspendLayout();
            this._settingsPage.SuspendLayout();
            this._aboutPage.SuspendLayout();
            this.SuspendLayout();
            // 
            // headerPanel
            // 
            this.headerPanel.Controls.Add(this._logoBox);
            this.headerPanel.Controls.Add(this._titleLabel);
            this.headerPanel.Controls.Add(this._headerStatusPanel);
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Padding = new System.Windows.Forms.Padding(15, 10, 15, 10);
            this.headerPanel.Size = new System.Drawing.Size(1182, 95);
            this.headerPanel.TabIndex = 1;
            // 
            // _logoBox
            // 
            this._logoBox.Location = new System.Drawing.Point(15, 5);
            this._logoBox.Name = "_logoBox";
            this._logoBox.Size = new System.Drawing.Size(50, 50);
            this._logoBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this._logoBox.TabIndex = 0;
            this._logoBox.TabStop = false;
            // 
            // _titleLabel
            // 
            this._titleLabel.AutoSize = true;
            this._titleLabel.Font = new System.Drawing.Font("Segoe UI", 16F, System.Drawing.FontStyle.Bold);
            this._titleLabel.Location = new System.Drawing.Point(75, 15);
            this._titleLabel.Name = "_titleLabel";
            this._titleLabel.Size = new System.Drawing.Size(329, 37);
            this._titleLabel.TabIndex = 1;
            this._titleLabel.Text = "Mod Manager";
            // 
            // _headerStatusPanel
            // 
            this._headerStatusPanel.Controls.Add(this._nexusUpdatesLabel);
            this._headerStatusPanel.Controls.Add(this._modApiVersionLabel);
            this._headerStatusPanel.Controls.Add(this._modsCountLabel);
            this._headerStatusPanel.Controls.Add(this._statusLabel);
            this._headerStatusPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this._headerStatusPanel.Location = new System.Drawing.Point(917, 10);
            this._headerStatusPanel.Name = "_headerStatusPanel";
            this._headerStatusPanel.Padding = new System.Windows.Forms.Padding(0, 0, 15, 0);
            this._headerStatusPanel.Size = new System.Drawing.Size(250, 75);
            this._headerStatusPanel.TabIndex = 2;
            // 
            // _nexusUpdatesLabel
            // 
            this._nexusUpdatesLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this._nexusUpdatesLabel.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this._nexusUpdatesLabel.Location = new System.Drawing.Point(0, 56);
            this._nexusUpdatesLabel.Name = "_nexusUpdatesLabel";
            this._nexusUpdatesLabel.Size = new System.Drawing.Size(235, 18);
            this._nexusUpdatesLabel.TabIndex = 3;
            this._nexusUpdatesLabel.Text = "Nexus Updates: --";
            this._nexusUpdatesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // _modApiVersionLabel
            // 
            this._modApiVersionLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this._modApiVersionLabel.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this._modApiVersionLabel.Location = new System.Drawing.Point(0, 38);
            this._modApiVersionLabel.Name = "_modApiVersionLabel";
            this._modApiVersionLabel.Size = new System.Drawing.Size(235, 18);
            this._modApiVersionLabel.TabIndex = 0;
            this._modApiVersionLabel.Text = "ModAPI Version: Unknown";
            this._modApiVersionLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // _modsCountLabel
            // 
            this._modsCountLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this._modsCountLabel.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this._modsCountLabel.Location = new System.Drawing.Point(0, 20);
            this._modsCountLabel.Name = "_modsCountLabel";
            this._modsCountLabel.Size = new System.Drawing.Size(235, 18);
            this._modsCountLabel.TabIndex = 1;
            this._modsCountLabel.Text = "Active Mods: 0";
            this._modsCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // _statusLabel
            // 
            this._statusLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this._statusLabel.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);
            this._statusLabel.ForeColor = System.Drawing.Color.LightGreen;
            this._statusLabel.Location = new System.Drawing.Point(0, 0);
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.Size = new System.Drawing.Size(235, 20);
            this._statusLabel.TabIndex = 2;
            this._statusLabel.Text = "Status: Ready";
            this._statusLabel.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // _tabControl
            // 
            this._tabControl.Controls.Add(this._gameSetupPage);
            this._tabControl.Controls.Add(this._modManagerPage);
            this._tabControl.Controls.Add(this._nexusPage);
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
            // 
            // _gameSetupPage
            // 
            this._gameSetupPage.Controls.Add(this._gameSetupTab);
            this._gameSetupPage.Location = new System.Drawing.Point(4, 42);
            this._gameSetupPage.Name = "_gameSetupPage";
            this._gameSetupPage.Size = new System.Drawing.Size(1174, 562);
            this._gameSetupPage.TabIndex = 0;
            this._gameSetupPage.Text = "Game Setup";
            // 
            // _modManagerPage
            // 
            this._modManagerPage.Controls.Add(this._modManagerTab);
            this._modManagerPage.Location = new System.Drawing.Point(4, 42);
            this._modManagerPage.Name = "_modManagerPage";
            this._modManagerPage.Size = new System.Drawing.Size(1174, 562);
            this._modManagerPage.TabIndex = 1;
            this._modManagerPage.Text = "Mod Manager";
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
            // _nexusPage
            // 
            this._nexusPage.Controls.Add(this._nexusTab);
            this._nexusPage.Location = new System.Drawing.Point(4, 42);
            this._nexusPage.Name = "_nexusPage";
            this._nexusPage.Size = new System.Drawing.Size(1174, 562);
            this._nexusPage.TabIndex = 2;
            this._nexusPage.Text = "Nexus";
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
            // _settingsPage
            // 
            this._settingsPage.Controls.Add(this._settingsTab);
            this._settingsPage.Location = new System.Drawing.Point(4, 42);
            this._settingsPage.Name = "_settingsPage";
            this._settingsPage.Size = new System.Drawing.Size(1174, 562);
            this._settingsPage.TabIndex = 3;
            this._settingsPage.Text = "Settings";
            // 
            // _settingsTab
            // 
            this._settingsTab.AutoScroll = true;
            this._settingsTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._settingsTab.Location = new System.Drawing.Point(0, 0);
            this._settingsTab.Name = "_settingsTab";
            this._settingsTab.Padding = new System.Windows.Forms.Padding(20);
            this._settingsTab.Size = new System.Drawing.Size(1174, 562);
            this._settingsTab.TabIndex = 0;
            // 
            // _aboutPage
            // 
            this._aboutPage.Controls.Add(this._aboutTab);
            this._aboutPage.Location = new System.Drawing.Point(4, 42);
            this._aboutPage.Name = "_aboutPage";
            this._aboutPage.Size = new System.Drawing.Size(1174, 562);
            this._aboutPage.TabIndex = 4;
            this._aboutPage.Text = "About";
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
            this.Text = "Mod Manager";
            this.headerPanel.ResumeLayout(false);
            this.headerPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._logoBox)).EndInit();
            this._headerStatusPanel.ResumeLayout(false);
            this._tabControl.ResumeLayout(false);
            this._gameSetupPage.ResumeLayout(false);
            this._modManagerPage.ResumeLayout(false);
            this._nexusPage.ResumeLayout(false);
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
            _gameSetupTab.GameProfileChanged += GameSetupTab_GameProfileChanged;
            _gameSetupTab.GamePathChanged += GameSetupTab_GamePathChanged;
            _gameSetupTab.LaunchRequested += GameSetupTab_LaunchRequested;
            _gameSetupTab.ViewGameLogRequested += GameSetupTab_ViewGameLogRequested;

            // Mod manager events
            _modManagerTab.OrderSaved += ModManagerTab_OrderSaved;
            _modManagerTab.NexusSyncCompleted += ModManagerTab_NexusSyncCompleted;
            _nexusTab.InstallCompleted += NexusTab_InstallCompleted;
            _nexusTab.NexusActivity += NexusTab_NexusActivity;

            // Settings events
            _settingsTab.SettingsChanged += SettingsTab_SettingsChanged;
            _settingsTab.DarkModeChanged += SettingsTab_DarkModeChanged;
            _settingsTab.ResetWindowRequested += SettingsTab_ResetWindowRequested;
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;

            // Tab change
            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        }

        private void GameSetupTab_ViewGameLogRequested(object sender, EventArgs e)
        {
            try
            {
                if (!_settings.IsGamePathValid) return;
                
                string gameDir = Path.GetDirectoryName(_settings.GamePath);
                string logPath = null;
                string[] relativeLogPaths = _gameProfile.LogFileRelativePaths ?? new string[0];
                for (int i = 0; i < relativeLogPaths.Length; i++)
                {
                    string candidate = Path.Combine(gameDir, relativeLogPaths[i]);
                    if (File.Exists(candidate))
                    {
                        logPath = candidate;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                {
                    string content = File.ReadAllText(logPath);
                    _gameSetupTab.Log("--- CONTENT OF " + Path.GetFileName(logPath).ToUpperInvariant() + " ---");
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
            ApplySavedWindowPlacement();
            _windowPlacementInitialized = true;

            // Clean up any stale staged Nexus archives/folders from previous runs.
            NexusInstallService.CleanupStartupArtifacts();

            // Initialize tabs with services and settings
            ApplyGameProfileToChrome();
            _gameSetupTab.Initialize(_settings);
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _settingsTab.Initialize(_settings);
            _settingsTab.ApplyGameProfile(_gameProfile);
            RefreshNexusAccountStatusAsync(true);

            // Apply initial theme
            ApplyTheme(_settings.DarkMode);

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
            CaptureWindowPlacement();

            // Save settings on close
            _settingsService.Save(_settings);
            
            // Stop timer
            if (_restartPollTimer != null)
            {
                _restartPollTimer.Stop();
                _restartPollTimer.Dispose();
            }
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
        }

        private void GameSetupTab_GameProfileChanged(string profileId)
        {
            GameProfile previousProfile = _gameProfile;
            _gameProfile = _gameProfileRegistry.Resolve(profileId);
            string previousDefaultDomain = previousProfile != null ? previousProfile.DefaultNexusGameDomain : string.Empty;

            _settings.SelectedGameId = _gameProfile.Id;
            if (_settings.IsGamePathValid)
                _settings.ModsPath = _gameProfile.GetModsPath(_settings.GamePath);

            if (string.IsNullOrEmpty(_settings.NexusGameDomain) ||
                string.Equals(_settings.NexusGameDomain, previousDefaultDomain, StringComparison.OrdinalIgnoreCase))
            {
                _settings.NexusGameDomain = _gameProfile.DefaultNexusGameDomain ?? string.Empty;
            }

            if (_settings.ManagerNexusModId <= 0 ||
                (previousProfile != null && _settings.ManagerNexusModId == previousProfile.DefaultManagerNexusModId))
            {
                _settings.ManagerNexusModId = _gameProfile.DefaultManagerNexusModId;
            }

            var installedApiVersions = DetectInstalledApiVersions(_settings);
            ApplyInstalledApiVersions(_settings, installedApiVersions);
            _discoveryService = new ModDiscoveryService(_gameProfile, installedApiVersions);
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _settingsTab.Initialize(_settings);
            ApplyGameProfileToChrome();
            SaveSettingsFromUi();
            UpdateStatusCounts();
            _gameSetupTab.Log("Profile applied: " + _gameProfile.DisplayName + ".");
        }

        private void GameSetupTab_GamePathChanged(string newPath)
        {
            if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
            {
                _settings.GamePath = newPath;
                _settings.ModsPath = _gameProfile.GetModsPath(newPath);
                
                var installedApiVersions = DetectInstalledApiVersions(_settings);
                ApplyInstalledApiVersions(_settings, installedApiVersions);
                
                // Recreate discovery service with new version
                _discoveryService = new ModDiscoveryService(_gameProfile, installedApiVersions);
                _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
                _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
                
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

                // Save settings before launch
                _settingsService.Save(_settings);

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
            if (_processLauncher.IsGameRunning(_settings))
            {
                MessageBox.Show(_gameProfile.DisplayName + " is already running. Please close it before launching via Manager.", 
                    "Game Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ReconcileLoadOrderForLaunch();
            _doorstopConfigurationService.ConfigureModded(_gameProfile, _settings, BuildDoorstopAssemblySearchPaths(), LogGameSetupMessage);
            if (!PreflightCheck()) return;

            try
            {
                try
                {
                    _launchConfigurationService.WriteLaunchConfiguration(_gameProfile, _settings);
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

                _processLauncher.Launch(_settings);
                
                _gameSetupTab.Log("Launched " + _gameProfile.DisplayName + " with mods");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string NormalizeModId(string id)
        {
            return (id ?? string.Empty).Trim().ToLowerInvariant();
        }

        private IEnumerable<string> BuildDoorstopAssemblySearchPaths()
        {
            List<string> searchPaths = new List<string>();
            if (_settings == null)
                return searchPaths;

            searchPaths.Add(_settings.ModsPath);

            try
            {
                string[] enabledIds = _orderService.ReadOrder(_settings.ModsPath);
                HashSet<string> enabledSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string id in enabledIds)
                    enabledSet.Add(id);

                List<ModItem> allMods = _discoveryService.DiscoverMods(_settings.ModsPath);
                foreach (ModItem mod in allMods)
                {
                    if (mod == null || !enabledSet.Contains(mod.Id))
                        continue;

                    string assembliesPath = Path.Combine(mod.RootPath, "Assemblies");
                    if (Directory.Exists(assembliesPath))
                        searchPaths.Add(assembliesPath);
                }
            }
            catch { }

            return searchPaths;
        }

        private void LogGameSetupMessage(string message)
        {
            if (_gameSetupTab != null && !string.IsNullOrEmpty(message))
                _gameSetupTab.Log(message);
        }

        /// <summary>
        /// Reconciles load order before launch: migrates known renamed IDs and removes entries
        /// that are no longer discoverable on disk.
        /// </summary>
        private void ReconcileLoadOrderForLaunch()
        {
            try
            {
                if (_settings == null || !_settings.IsModsPathValid) return;
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
                    _gameSetupTab.Log("Launch diagnostics: load order is empty (0 enabled mods).");
                    return;
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
                        {
                            migrated++;
                            _gameSetupTab.Log("Migrated load order ID: " + originalNorm + " -> " + candidateNorm);
                        }
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
                        _gameSetupTab.Log("Removed missing mod from load order: " + raw);
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

                _gameSetupTab.Log("Launch diagnostics: enabled mods in load order = " + reconciled.Count);
            }
            catch (Exception ex)
            {
                _gameSetupTab.Log("Load order reconciliation failed: " + ex.Message);
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
                int width = Math.Max(_settings.WindowWidth, minWidth);
                int height = Math.Max(_settings.WindowHeight, minHeight);

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

        private bool PreflightCheck()
        {
            try
            {
                if (!_settings.IsGamePathValid)
                {
                    MessageBox.Show("Please select a valid " + _gameProfile.DisplayName + " executable first.", "Launch Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                var missing = _preflightService.GetMissingRuntimeFiles(_gameProfile, _settings);

                if (missing.Count == 0) return true;

                string msg = "Some required files for mod injection are missing:\n\n  - "
                             + string.Join("\n  - ", missing.ToArray())
                             + "\n\nWithout these, mods will not load. Continue launching anyway?";

                var choice = MessageBox.Show(msg, "Missing Files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return choice == DialogResult.Yes;
            }
            catch { return true; }
        }

        private void LaunchVanilla()
        {
            try
            {
                _doorstopConfigurationService.ConfigureVanilla(_gameProfile, _settings);
                _processLauncher.Launch(_settings);
                _gameSetupTab.Log("Launched " + _gameProfile.DisplayName + " (vanilla mode)");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                _nexusUpdatesLabel.Text = "Nexus Updates: check failed";
            }
            else if (!_settings.EnableNexusIntegration)
            {
                _nexusUpdatesLabel.Text = "Nexus Updates: disabled";
            }
            else
            {
                _nexusUpdatesLabel.Text = "Nexus Updates: " + updateCount + " (" + mappedMods + " linked)";
            }

            _nexusTab.SetLastCheckedUtc(_modManagerTab.LastNexusRemoteSyncUtc);
            _nexusTab.UpdateInstalledMods(mods, mappedMods, updateCount, errorMessage);

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
            _gameProfile = _gameProfileRegistry.Resolve(_settings.SelectedGameId);
            NormalizeSettingsForProfile();
            RecreateNexusService();
            var installedApiVersions = DetectInstalledApiVersions(_settings);
            ApplyInstalledApiVersions(_settings, installedApiVersions);
            _discoveryService = new ModDiscoveryService(_gameProfile, installedApiVersions);
            ApplyGameProfileToChrome();
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
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

            // Preserve or re-detect API versions as they are derived from installed runtime files.
            Dictionary<string, string> previousApiVersions = _settings != null && _settings.InstalledApiVersions != null
                ? new Dictionary<string, string>(_settings.InstalledApiVersions, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _settings = settings;
            _gameProfile = _gameProfileRegistry.Resolve(_settings.SelectedGameId);
            NormalizeSettingsForProfile();

            if ((_settings.InstalledApiVersions == null || _settings.InstalledApiVersions.Count == 0) && previousApiVersions.Count > 0)
            {
                ApplyInstalledApiVersions(_settings, previousApiVersions);
            }

            if (MissingKnownApiVersions(_settings) && _settings.IsGamePathValid)
            {
                var installedApiVersions = DetectInstalledApiVersions(_settings);
                ApplyInstalledApiVersions(_settings, installedApiVersions);
                _discoveryService = new ModDiscoveryService(_gameProfile, installedApiVersions);
            }

            RecreateNexusService();
            
            // Re-initialize tabs with new settings
            ApplyGameProfileToChrome();
            _gameSetupTab.Initialize(_settings);
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings, _nexusService);
            _nexusTab.Initialize(_nexusService, _settings, APP_VERSION);
            _settingsTab.Initialize(_settings);
            _settingsTab.ApplyGameProfile(_gameProfile);
            
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

            if (!string.Equals(previous.SelectedGameId, current.SelectedGameId, StringComparison.OrdinalIgnoreCase))
                changes.Add("SelectedGameId");
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
            if (previous.IgnoreOrderChecks != current.IgnoreOrderChecks)
                changes.Add("IgnoreOrderChecks");
            if (previous.SkipHarmonyDependencyCheck != current.SkipHarmonyDependencyCheck)
                changes.Add("SkipHarmonyDependencyCheck");
            if (!string.Equals(previous.AutoCondenseSaves, current.AutoCondenseSaves, StringComparison.OrdinalIgnoreCase))
                changes.Add("AutoCondenseSaves");
            if (previous.AutoLoadSaveSlot != current.AutoLoadSaveSlot)
                changes.Add("AutoLoadSaveSlot");
            if (previous.EnableNexusIntegration != current.EnableNexusIntegration)
                changes.Add("EnableNexusIntegration");
            if (!string.Equals(previous.NexusGameDomain, current.NexusGameDomain, StringComparison.OrdinalIgnoreCase))
                changes.Add("NexusGameDomain");
            if (!string.Equals(previous.NexusApiKey, current.NexusApiKey, StringComparison.Ordinal))
                changes.Add("NexusApiKey");
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

        private bool MissingKnownApiVersions(AppSettings settings)
        {
            if (settings == null || _gameProfile == null)
                return false;

            string[] apiNames = _gameProfile.GetApiAssemblyNames();
            if (apiNames.Length == 0)
                return false;

            Dictionary<string, string> versions = settings.InstalledApiVersions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < apiNames.Length; i++)
            {
                string version;
                if (!versions.TryGetValue(apiNames[i], out version) || string.IsNullOrEmpty(version))
                    return true;
            }

            return false;
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

                _statusLabel.Text = "Status: Ready";
                _statusLabel.ForeColor = _settings.DarkMode ? Color.LightGreen : Color.Green;
                _modsCountLabel.Text = "Active Mods: " + count;
                _modApiVersionLabel.Text = "API Versions: " + apiVersion;
                if (!_settings.EnableNexusIntegration)
                    _nexusUpdatesLabel.Text = "Nexus Updates: disabled";

                // Also update the tab if it still exists/is used
                _gameSetupTab.UpdateStatus(true, count, apiVersion);
            }
            else
            {
                _statusLabel.Text = "Status: Not Ready";
                _statusLabel.ForeColor = Color.Red;
                _modsCountLabel.Text = "Active Mods: 0";
                _modApiVersionLabel.Text = "API Versions: Unknown";
                _nexusUpdatesLabel.Text = _settings.EnableNexusIntegration ? "Nexus Updates: --" : "Nexus Updates: disabled";
            }
        }

        private string FormatInstalledApiVersions(AppSettings settings)
        {
            if (settings == null)
                return "Unknown";

            string[] apiNames = _gameProfile != null ? _gameProfile.GetApiAssemblyNames() : new string[] { "ModAPI" };
            if (apiNames.Length == 0)
                return "None";

            List<string> parts = new List<string>();
            Dictionary<string, string> versions = settings.InstalledApiVersions ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < apiNames.Length; i++)
            {
                string apiName = apiNames[i];
                string version;
                if (!versions.TryGetValue(apiName, out version) || string.IsNullOrEmpty(version))
                    version = "missing";

                parts.Add(apiName + " " + version);
            }

            return string.Join(" / ", parts.ToArray());
        }

        private void ApplyTheme(bool isDark)
        {
            // Apply theme to form
            if (isDark)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                
                // Header (includes logo and title)
                _titleLabel.ForeColor = Color.White;
                
                // Status Header specific colors
                _headerStatusPanel.BackColor = Color.Transparent; // Keep it clean in the header
                _statusLabel.ForeColor = Color.LightGreen;
                _modsCountLabel.ForeColor = Color.LightGray;
                _modApiVersionLabel.ForeColor = Color.LightGray;
                _nexusUpdatesLabel.ForeColor = Color.LightGray;

                foreach (Control c in this.Controls)
                {
                    if (c is Panel && c != _headerStatusPanel) // General panels
                    {
                        c.BackColor = Color.FromArgb(45, 45, 48);
                        foreach (Control child in c.Controls)
                        {
                            if (child is Label && child != _statusLabel && child != _modsCountLabel && child != _modApiVersionLabel) 
                                child.ForeColor = Color.White;
                        }
                    }
                    if (c is TabControl)
                    {
                        foreach (TabPage page in ((TabControl)c).TabPages)
                        page.BackColor = Color.FromArgb(45, 45, 48);
                    }
                }
            }
            else
            {
                this.BackColor = SystemColors.Control;
                _titleLabel.ForeColor = SystemColors.ControlText;
                _headerStatusPanel.BackColor = Color.Transparent;
                
                _statusLabel.ForeColor = Color.Green;
                _modsCountLabel.ForeColor = SystemColors.ControlText;
                _modApiVersionLabel.ForeColor = SystemColors.ControlText;
                _nexusUpdatesLabel.ForeColor = SystemColors.ControlText;

                foreach (Control c in this.Controls)
                {
                    if (c is Panel && c != _headerStatusPanel)
                    {
                        c.BackColor = SystemColors.Control;
                        foreach (Control child in c.Controls)
                        {
                            if (child is Label) child.ForeColor = SystemColors.ControlText;
                        }
                    }
                    if (c is TabControl)
                    {
                        foreach (TabPage page in ((TabControl)c).TabPages)
                        {
                            page.BackColor = SystemColors.Control;
                        }
                    }
                }
            }

            // Apply to tabs
            _gameSetupTab.ApplyTheme(isDark);
            _modManagerTab.ApplyTheme(isDark);
            _nexusTab.ApplyTheme(isDark);
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

                var gameDir = Path.GetDirectoryName(_settings.GamePath);
                var restartPath = Path.Combine(_gameProfile.RuntimeLayout.GetBinPath(gameDir), "restart.json");

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

        private void _gameSetupTab_Load(object sender, EventArgs e)
        {

        }
    }
}

