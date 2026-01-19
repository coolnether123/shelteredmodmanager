using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Core.Models;
using Manager.Core.Services;
using Manager.Views;

namespace Manager
{
    /// <summary>
    /// Modern, refactored main form for Sheltered Mod Manager.
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
        private TabPage _settingsPage;
        private TabPage _aboutPage;

        // Status Header
        private Label _statusLabel;
        private Label _modsCountLabel;
        private Label _modApiVersionLabel;
        private Panel _headerStatusPanel;
        private ModManagerTab _modManagerTab;
        private SettingsTab _settingsTab;
        private AboutTab _aboutTab;

        // Services
        private SettingsService _settingsService;
        private ModDiscoveryService _discoveryService;
        private LoadOrderService _orderService;

        // State
        private AppSettings _settings;
        private Timer _restartPollTimer;
        private Panel headerPanel;
        private GameSetupTab _gameSetupTab;
        private const string APP_VERSION = "1.0.0";

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
        }

        private void InitializeServices()
        {
            _settingsService = new SettingsService();
            _orderService = new LoadOrderService();
            
            // Settings loaded first to get ModAPI version path
            _settings = _settingsService.Load();
            
            // Get installed ModAPI version for discovery service
            string modApiVersion = null;
            if (_settings.IsGamePathValid)
            {
                try
                {
                    string smmPath = Path.Combine(Path.GetDirectoryName(_settings.GamePath), "SMM");
                    modApiVersion = AssemblyVersionChecker.GetInstalledModApiVersion(smmPath);
                    _settings.InstalledModApiVersion = modApiVersion;
                }
                catch { }
            }
            
            _discoveryService = new ModDiscoveryService(modApiVersion);
        }

        private void InitializeComponent()
        {
            this.headerPanel = new System.Windows.Forms.Panel();
            this._logoBox = new System.Windows.Forms.PictureBox();
            this._titleLabel = new System.Windows.Forms.Label();
            this._headerStatusPanel = new System.Windows.Forms.Panel();
            this._modApiVersionLabel = new System.Windows.Forms.Label();
            this._modsCountLabel = new System.Windows.Forms.Label();
            this._statusLabel = new System.Windows.Forms.Label();
            this._tabControl = new System.Windows.Forms.TabControl();
            this._gameSetupPage = new System.Windows.Forms.TabPage();
            this._modManagerPage = new System.Windows.Forms.TabPage();
            this._modManagerTab = new Manager.Views.ModManagerTab();
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
            this.headerPanel.Size = new System.Drawing.Size(1182, 75);
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
            this._titleLabel.Text = "Sheltered Mod Manager";
            // 
            // _headerStatusPanel
            // 
            this._headerStatusPanel.Controls.Add(this._modApiVersionLabel);
            this._headerStatusPanel.Controls.Add(this._modsCountLabel);
            this._headerStatusPanel.Controls.Add(this._statusLabel);
            this._headerStatusPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this._headerStatusPanel.Location = new System.Drawing.Point(917, 10);
            this._headerStatusPanel.Name = "_headerStatusPanel";
            this._headerStatusPanel.Padding = new System.Windows.Forms.Padding(0, 0, 15, 0);
            this._headerStatusPanel.Size = new System.Drawing.Size(250, 55);
            this._headerStatusPanel.TabIndex = 2;
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
            this._tabControl.Controls.Add(this._settingsPage);
            this._tabControl.Controls.Add(this._aboutPage);
            this._tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tabControl.Font = new System.Drawing.Font("Segoe UI", 10F);
            this._tabControl.Location = new System.Drawing.Point(0, 75);
            this._tabControl.Name = "_tabControl";
            this._tabControl.Padding = new System.Drawing.Point(15, 8);
            this._tabControl.SelectedIndex = 0;
            this._tabControl.Size = new System.Drawing.Size(1182, 628);
            this._tabControl.TabIndex = 0;
            // 
            // _gameSetupPage
            // 
            this._gameSetupPage.Controls.Add(this._gameSetupTab);
            this._gameSetupPage.Location = new System.Drawing.Point(4, 42);
            this._gameSetupPage.Name = "_gameSetupPage";
            this._gameSetupPage.Size = new System.Drawing.Size(1174, 582);
            this._gameSetupPage.TabIndex = 0;
            this._gameSetupPage.Text = "Game Setup";
            // 
            // _modManagerPage
            // 
            this._modManagerPage.Controls.Add(this._modManagerTab);
            this._modManagerPage.Location = new System.Drawing.Point(4, 42);
            this._modManagerPage.Name = "_modManagerPage";
            this._modManagerPage.Size = new System.Drawing.Size(1174, 582);
            this._modManagerPage.TabIndex = 1;
            this._modManagerPage.Text = "Mod Manager";
            // 
            // _modManagerTab
            // 
            this._modManagerTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._modManagerTab.Location = new System.Drawing.Point(0, 0);
            this._modManagerTab.Name = "_modManagerTab";
            this._modManagerTab.Padding = new System.Windows.Forms.Padding(15);
            this._modManagerTab.Size = new System.Drawing.Size(1174, 582);
            this._modManagerTab.TabIndex = 0;
            // 
            // _settingsPage
            // 
            this._settingsPage.Controls.Add(this._settingsTab);
            this._settingsPage.Location = new System.Drawing.Point(4, 42);
            this._settingsPage.Name = "_settingsPage";
            this._settingsPage.Size = new System.Drawing.Size(1174, 582);
            this._settingsPage.TabIndex = 2;
            this._settingsPage.Text = "Settings";
            // 
            // _settingsTab
            // 
            this._settingsTab.AutoScroll = true;
            this._settingsTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._settingsTab.Location = new System.Drawing.Point(0, 0);
            this._settingsTab.Name = "_settingsTab";
            this._settingsTab.Padding = new System.Windows.Forms.Padding(20);
            this._settingsTab.Size = new System.Drawing.Size(1174, 582);
            this._settingsTab.TabIndex = 0;
            // 
            // _aboutPage
            // 
            this._aboutPage.Controls.Add(this._aboutTab);
            this._aboutPage.Location = new System.Drawing.Point(4, 42);
            this._aboutPage.Name = "_aboutPage";
            this._aboutPage.Size = new System.Drawing.Size(1174, 582);
            this._aboutPage.TabIndex = 3;
            this._aboutPage.Text = "About";
            // 
            // _aboutTab
            // 
            this._aboutTab.AppVersion = "1.0.0";
            this._aboutTab.Author = "Coolnether123";
            this._aboutTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._aboutTab.Location = new System.Drawing.Point(0, 0);
            this._aboutTab.Name = "_aboutTab";
            this._aboutTab.Padding = new System.Windows.Forms.Padding(20);
            this._aboutTab.Size = new System.Drawing.Size(1174, 582);
            this._aboutTab.TabIndex = 0;
            // 
            // _gameSetupTab
            // 
            this._gameSetupTab.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gameSetupTab.Location = new System.Drawing.Point(0, 0);
            this._gameSetupTab.Name = "_gameSetupTab";
            this._gameSetupTab.Padding = new System.Windows.Forms.Padding(20);
            this._gameSetupTab.Size = new System.Drawing.Size(1174, 582);
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
            this._settingsPage.ResumeLayout(false);
            this._aboutPage.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        private void WireEvents()
        {
            // Form events
            this.Load += MainForm_Load;
            this.FormClosing += MainForm_FormClosing;

            // Game setup events
            _gameSetupTab.GamePathChanged += GameSetupTab_GamePathChanged;
            _gameSetupTab.LaunchRequested += GameSetupTab_LaunchRequested;
            _gameSetupTab.ViewGameLogRequested += GameSetupTab_ViewGameLogRequested;

            // Mod manager events
            _modManagerTab.OrderSaved += ModManagerTab_OrderSaved;

            // Settings events
            _settingsTab.SettingsChanged += SettingsTab_SettingsChanged;
            _settingsTab.DarkModeChanged += SettingsTab_DarkModeChanged;

            // Tab change
            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        }

        private void GameSetupTab_ViewGameLogRequested(object sender, EventArgs e)
        {
            try
            {
                if (!_settings.IsGamePathValid) return;
                
                string gameDir = Path.GetDirectoryName(_settings.GamePath);
                // Look for mod_manager.log in SMM folder (standard location) or root
                string logPath = Path.Combine(Path.Combine(gameDir, "SMM"), "mod_manager.log");
                
                if (!File.Exists(logPath))
                {
                    // Fallback to root just in case
                    logPath = Path.Combine(gameDir, "mod_manager.log");
                }

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
            // Initialize tabs with services and settings
            _gameSetupTab.Initialize(_settings);
            _modManagerTab.Initialize(_discoveryService, _orderService, _settings);
            _settingsTab.Initialize(_settings);

            // Apply initial theme
            ApplyTheme(_settings.DarkMode);

            // Refresh mods if path is valid
            if (_settings.IsModsPathValid)
            {
                _modManagerTab.RefreshMods();
                UpdateStatusCounts();
            }

            // Start restart poll timer
            StartRestartPollTimer();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
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
        }

        private void GameSetupTab_GamePathChanged(string newPath)
        {
            if (!string.IsNullOrEmpty(newPath) && File.Exists(newPath))
            {
                _settings.GamePath = newPath;
                _settings.ModsPath = Path.Combine(Path.GetDirectoryName(newPath), "mods");
                
                // Update ModAPI version
                try
                {
                    string smmPath = Path.Combine(Path.GetDirectoryName(newPath), "SMM");
                    _settings.InstalledModApiVersion = AssemblyVersionChecker.GetInstalledModApiVersion(smmPath);
                }
                catch { }
                
                // Recreate discovery service with new version
                _discoveryService = new ModDiscoveryService(_settings.InstalledModApiVersion);
                _modManagerTab.Initialize(_discoveryService, _orderService, _settings);
                
                // Setup doorstop
                try
                {
                    // Note: Doorstop setup would be called here from ManagerGUI.LaunchAndPreflight.cs
                    // SetupDoorstop();
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
            try
            {
                if (CheckGameRunning())
                {
                    MessageBox.Show("Sheltered is already running. Please close it before launching via Manager.", 
                        "Game Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SetupDoorstop();
            }
            catch { }

            if (!PreflightCheck()) return;

            try
            {
                try
                {
                    var gameDir = Path.GetDirectoryName(_settings.GamePath);
                    var smmDir = Path.Combine(gameDir, "SMM");
                    var binDir = Path.Combine(smmDir, "bin");
                    Directory.CreateDirectory(binDir);
                    var iniPath = Path.Combine(binDir, "mod_manager.ini");
                    var lines = new System.Collections.Generic.List<string>();
                    lines.Add("# Generated by Manager at launch");
                    lines.Add("GamePath=" + (_settings.GamePath ?? string.Empty));
                    lines.Add("DarkMode=" + (_settings.DarkMode ? "True" : "False"));
                    lines.Add("DevMode=" + (_settings.DevMode ? "True" : "False"));
                    lines.Add("LogLevel=" + (_settings.LogLevel ?? "Info"));
                    
                    // LogCategories disabled in v1.0 - category filtering not currently used
                    // var catList = new System.Collections.Generic.List<string>();
                    // foreach (string cat in _settings.LogCategories)
                    //     catList.Add(cat);
                    // lines.Add("LogCategories=" + string.Join(",", catList.ToArray()));
                    lines.Add("IgnoreOrderChecks=" + (_settings.IgnoreOrderChecks ? "True" : "False"));
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
                startInfo.WorkingDirectory = Path.GetDirectoryName(_settings.GamePath);
                startInfo.UseShellExecute = false;
                System.Diagnostics.Process.Start(startInfo);
                
                _gameSetupTab.Log("Launched Sheltered with mods");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to launch: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

                string smmDir = Path.Combine(gameDir, "SMM");
                string doorstopDllRoot = Path.Combine(smmDir, "Doorstop.dll");
                string doorstopDllBin = Path.Combine(Path.Combine(smmDir, "bin"), "Doorstop.dll");
                if (!File.Exists(doorstopDllRoot) && !File.Exists(doorstopDllBin)) missing.Add("SMM/Doorstop.dll");

                string modapiDll = Path.Combine(smmDir, "ModAPI.dll");
                if (!File.Exists(modapiDll)) missing.Add("SMM/ModAPI.dll");

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


                string iniPath = Path.Combine(gameDir, "doorstop_config.ini");
                var ini = new System.Collections.Generic.List<string>();
                ini.Add("# Auto-generated by Sheltered Mod Manager");
                ini.Add("[General]");
                ini.Add("enabled=true");
                ini.Add("target_assembly=SMM\\bin\\Doorstop.dll");
                ini.Add("redirect_output_log=true");
                ini.Add("");
                ini.Add("[UnityMono]");
                ini.Add("dll_search_path_override=" + dllSearchPath);
                ini.Add("debug_enabled=false");
                ini.Add("debug_address=127.0.0.1:10000");
                ini.Add("debug_suspend=false");
                File.WriteAllLines(iniPath, ini.ToArray());

                bool is64Bit = DetectIsExe64Bit(_settings.GamePath);
                CopyWinhttpForGame(gameDir, is64Bit);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to configure Doorstop: " + ex.Message,
                    "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                _gameSetupTab.Log("Launched Sheltered (vanilla mode)");
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

        private bool DetectIsExe64Bit(string exePath)
        {
            FileStream fs = null;
            BinaryReader br = null;
            try
            {
                fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                br = new BinaryReader(fs);
                fs.Seek(0x3C, SeekOrigin.Begin);
                int peOffset = br.ReadInt32();
                fs.Seek(peOffset, SeekOrigin.Begin);
                uint peSignature = br.ReadUInt32();
                if (peSignature != 0x00004550) return false;
                ushort machine = br.ReadUInt16();
                return machine == 0x8664;
            }
            catch { return false; }
            finally
            {
                if (br != null) br.Close();
                if (fs != null) fs.Close();
            }
        }

        private void CopyWinhttpForGame(string gameDir, bool is64Bit)
        {
            try
            {
                string smmDir = Path.Combine(gameDir, "SMM");
                string doorstopDir = Path.Combine(smmDir, "Doorstop");
                string bitnessDir = is64Bit ? Path.Combine(doorstopDir, "x64") : Path.Combine(doorstopDir, "x32");
                string sourceWinhttp = Path.Combine(bitnessDir, "winhttp.dll");
                string targetWinhttp = Path.Combine(gameDir, "winhttp.dll");

                if (File.Exists(sourceWinhttp))
                {
                    File.Copy(sourceWinhttp, targetWinhttp, true);
                    _gameSetupTab.Log("Copied " + (is64Bit ? "64-bit" : "32-bit") + " winhttp.dll");
                }
            }
            catch { }
        }



        private void ModManagerTab_OrderSaved(string[] newOrder)
        {
            UpdateStatusCounts();
        }

        private void SettingsTab_SettingsChanged(AppSettings settings)
        {
            _settings = settings;
            _settingsService.Save(_settings);
            
            // Re-apply theme in case it changed
            ApplyTheme(_settings.DarkMode);
        }

        private void SettingsTab_DarkModeChanged(bool isDark)
        {
            _settings.DarkMode = isDark;
            ApplyTheme(isDark);
            _settingsService.Save(_settings);
        }

        private void UpdateStatusCounts()
        {
            if (_settings.IsModsPathValid)
            {
                var order = _orderService.ReadOrder(_settings.ModsPath);
                int count = order.Length;
                string apiVersion = _settings.InstalledModApiVersion ?? "Unknown";

                _statusLabel.Text = "Status: Ready";
                _statusLabel.ForeColor = _settings.DarkMode ? Color.LightGreen : Color.Green;
                _modsCountLabel.Text = "Active Mods: " + count;
                _modApiVersionLabel.Text = "ModAPI Version: " + apiVersion;

                // Also update the tab if it still exists/is used
                _gameSetupTab.UpdateStatus(true, count, apiVersion);
            }
            else
            {
                _statusLabel.Text = "Status: Not Ready";
                _statusLabel.ForeColor = Color.Red;
                _modsCountLabel.Text = "Active Mods: 0";
                _modApiVersionLabel.Text = "ModAPI Version: Unknown";
            }
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
                
                _statusLabel.ForeColor = Color.Green;
                _modsCountLabel.ForeColor = SystemColors.ControlText;
                _modApiVersionLabel.ForeColor = SystemColors.ControlText;

                foreach (Control c in this.Controls)
                {
                    if (c is Panel)
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
                        var modsFromManifest = manifest.lastLoadedMods ?? new ManagerLoadedModInfo[0];

                        // Extract Mod IDs
                        var newOrder = new System.Collections.Generic.List<string>();
                        foreach (var m in modsFromManifest)
                        {
                            if (!string.IsNullOrEmpty(m.modId)) newOrder.Add(m.modId);
                        }

                        // Write Load Order
                        if (!string.IsNullOrEmpty(_settings.ModsPath))
                        {
                            _orderService.SaveOrder(_settings.ModsPath, newOrder);
                            _modManagerTab.RefreshMods();
                        }

                        // Validate
                        bool safeToLaunch = true;

                        if (newOrder.Count > 0)
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
