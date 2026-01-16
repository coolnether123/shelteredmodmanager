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

        // Tab contents
        private GameSetupTab _gameSetupTab;
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
                        _logoBox.Image = Image.FromStream(stream);
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
            this.SuspendLayout();

            // Form settings
            this.Text = "Sheltered Mod Manager";
            this.Size = new Size(1200, 750);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9f);
            
            // Header panel with logo
            var headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 75;
            headerPanel.Padding = new Padding(15, 10, 15, 10);

            _logoBox = new PictureBox();
            _logoBox.Size = new Size(50, 50);
            _logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            _logoBox.Location = new Point(15, 5);
            
            _titleLabel = new Label();
            _titleLabel.Text = "Sheltered Mod Manager";
            _titleLabel.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _titleLabel.AutoSize = true;
            _titleLabel.Location = new Point(75, 15);

            headerPanel.Controls.Add(_logoBox);
            headerPanel.Controls.Add(_titleLabel);

            _headerStatusPanel = new Panel();
            _headerStatusPanel.Dock = DockStyle.Right;
            _headerStatusPanel.Width = 250;
            _headerStatusPanel.Padding = new Padding(0, 0, 15, 0);

            _statusLabel = new Label();
            _statusLabel.Text = "Status: Ready";
            _statusLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _statusLabel.ForeColor = Color.LightGreen;
            _statusLabel.Height = 20;
            _statusLabel.Dock = DockStyle.Top;
            _statusLabel.TextAlign = ContentAlignment.BottomRight;

            _modsCountLabel = new Label();
            _modsCountLabel.Text = "Active Mods: 0";
            _modsCountLabel.Font = new Font("Segoe UI", 8.5f);
            _modsCountLabel.Height = 18;
            _modsCountLabel.Dock = DockStyle.Top;
            _modsCountLabel.TextAlign = ContentAlignment.MiddleRight;

            _modApiVersionLabel = new Label();
            _modApiVersionLabel.Text = "ModAPI Version: Unknown";
            _modApiVersionLabel.Font = new Font("Segoe UI", 8.5f);
            _modApiVersionLabel.Height = 18;
            _modApiVersionLabel.Dock = DockStyle.Top;
            _modApiVersionLabel.TextAlign = ContentAlignment.MiddleRight;

            // Add in REVERSE order for Dock.Top to stack them correctly: Status at top
            _headerStatusPanel.Controls.Add(_modApiVersionLabel);
            _headerStatusPanel.Controls.Add(_modsCountLabel);
            _headerStatusPanel.Controls.Add(_statusLabel);
            headerPanel.Controls.Add(_headerStatusPanel);

            // Tab control
            _tabControl = new TabControl();
            _tabControl.Dock = DockStyle.Fill;
            _tabControl.Font = new Font("Segoe UI", 10f);
            _tabControl.Padding = new Point(15, 8);

            // Create tab pages
            _gameSetupPage = new TabPage("Game Setup");
            _modManagerPage = new TabPage("Mod Manager");
            _settingsPage = new TabPage("Settings");
            _aboutPage = new TabPage("About");

            // Create tab contents
            _gameSetupTab = new GameSetupTab();
            _gameSetupTab.Dock = DockStyle.Fill;
            
            _modManagerTab = new ModManagerTab();
            _modManagerTab.Dock = DockStyle.Fill;
            
            _settingsTab = new SettingsTab();
            _settingsTab.Dock = DockStyle.Fill;
            
            _aboutTab = new AboutTab();
            _aboutTab.Dock = DockStyle.Fill;

            // Add contents to pages
            _gameSetupPage.Controls.Add(_gameSetupTab);
            _modManagerPage.Controls.Add(_modManagerTab);
            _settingsPage.Controls.Add(_settingsTab);
            _aboutPage.Controls.Add(_aboutTab);

            // Add pages to tab control
            _tabControl.TabPages.Add(_gameSetupPage);
            _tabControl.TabPages.Add(_modManagerPage);
            _tabControl.TabPages.Add(_settingsPage);
            _tabControl.TabPages.Add(_aboutPage);

            // Add to form
            this.Controls.Add(_tabControl);
            this.Controls.Add(headerPanel);

            this.ResumeLayout();
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
                    Directory.CreateDirectory(smmDir);
                    var iniPath = Path.Combine(smmDir, "mod_manager.ini");
                    var lines = new System.Collections.Generic.List<string>();
                    lines.Add("# Generated by Manager at launch");
                    lines.Add("GamePath=" + (_settings.GamePath ?? string.Empty));
                    lines.Add("DarkMode=" + (_settings.DarkMode ? "True" : "False"));
                    lines.Add("DevMode=" + (_settings.DevMode ? "True" : "False"));
                    lines.Add("LogLevel=" + (_settings.LogLevel ?? "Info"));
                    
                    var catList = new System.Collections.Generic.List<string>();
                    foreach (string cat in _settings.LogCategories)
                        catList.Add(cat);
                    lines.Add("LogCategories=" + string.Join(",", catList.ToArray()));
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
    }
}
