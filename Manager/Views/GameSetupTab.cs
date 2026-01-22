using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;

namespace Manager.Views
{
    /// <summary>
    /// Delegate for string path events
    /// </summary>
    public delegate void StringPathHandler(string path);
    
    /// <summary>
    /// Delegate for bool events
    /// </summary>
    public delegate void BoolEventHandler(bool value);

    /// <summary>
    /// Game Setup tab - handles game path configuration and launch.
    /// </summary>
    public class GameSetupTab : UserControl
    {
        // Path controls
        private Label _gamePathLabel;
        private TextBox _gamePathTextBox;
        private Button _browseButton;
        private Button _detectButton;
        
        private Label _modsPathLabel;
        private TextBox _modsPathTextBox;
        private Button _openModsFolderButton;
        private Button _openGameFolderButton;

        // Status
        private Panel _statusPanel;
        private Label _statusLabel;
        private Label _modsCountLabel;
        private Label _modApiVersionLabel;

        // Launch buttons
        private ActionButton _launchButton;
        private ActionButton _launchVanillaButton;

        // Log viewer
        private Label _logLabel;
        private Panel _logContainer;
        private RichTextBox _logTextBox;
        private Button _clearLogButton;
        private Button _loadGameLogButton;

        // State
        private AppSettings _settings;
        private bool _isDarkMode = false;
        private bool _isUpdating = false;
        private string _lastLoggedPath = null;

        /// <summary>
        /// Event raised when game path changes
        /// </summary>
        public event StringPathHandler GamePathChanged;

        /// <summary>
        /// Event raised when launch is requested (true = with mods, false = vanilla)
        /// </summary>
        public event BoolEventHandler LaunchRequested;

        /// <summary>
        /// Event raised when view game log is requested
        /// </summary>
        public event EventHandler ViewGameLogRequested;

        public GameSetupTab()
        {
            InitializeComponent();
            WireEvents();
        }

        public void Initialize(AppSettings settings)
        {
            _settings = settings;
            UpdateFromSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Padding = new Padding(20);

            // Game Path Section
            _gamePathLabel = new Label();
            _gamePathLabel.Text = "Sheltered Installation";
            _gamePathLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _gamePathLabel.AutoSize = true;
            _gamePathLabel.Location = new Point(20, 20);

            _gamePathTextBox = new TextBox();
            _gamePathTextBox.Font = new Font("Segoe UI", 10f);
            _gamePathTextBox.Location = new Point(20, 50);
            _gamePathTextBox.Size = new Size(500, 26);
            _gamePathTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _gamePathTextBox.BorderStyle = BorderStyle.FixedSingle;
            _gamePathTextBox.ReadOnly = false;
            _gamePathTextBox.AllowDrop = true;
            _gamePathTextBox.DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            _gamePathTextBox.DragDrop += (s, e) => 
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0) _gamePathTextBox.Text = files[0];
            };

            _browseButton = new Button();
            _browseButton.Text = "Browse...";
            _browseButton.Font = new Font("Segoe UI", 9f);
            _browseButton.Location = new Point(530, 48);
            _browseButton.Width = 90;
            _browseButton.Height = 28;
            _browseButton.FlatStyle = FlatStyle.Flat;
            _browseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            
            _detectButton = new Button();
            _detectButton.Text = "Auto-Detect";
            _detectButton.Font = new Font("Segoe UI", 9f);
            _detectButton.Location = new Point(625, 48);
            _detectButton.Width = 100;
            _detectButton.Height = 28;
            _detectButton.FlatStyle = FlatStyle.Flat;
            _detectButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // Mods Path Section
            _modsPathLabel = new Label();
            _modsPathLabel.Text = "Mods Directory";
            _modsPathLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _modsPathLabel.AutoSize = true;
            _modsPathLabel.Location = new Point(20, 90);

            _modsPathTextBox = new TextBox();
            _modsPathTextBox.Font = new Font("Segoe UI", 10f);
            _modsPathTextBox.Location = new Point(20, 115);
            _modsPathTextBox.Size = new Size(500, 26);
            _modsPathTextBox.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _modsPathTextBox.BorderStyle = BorderStyle.FixedSingle;
            _modsPathTextBox.ReadOnly = true;

            _openModsFolderButton = new Button();
            _openModsFolderButton.Text = "Open Folder";
            _openModsFolderButton.Font = new Font("Segoe UI", 9f);
            _openModsFolderButton.Location = new Point(530, 113);
            _openModsFolderButton.Width = 90;
            _openModsFolderButton.Height = 28;
            _openModsFolderButton.FlatStyle = FlatStyle.Flat;
            _openModsFolderButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _openGameFolderButton = new Button();
            _openGameFolderButton.Text = "Open Game Folder";
            _openGameFolderButton.Font = new Font("Segoe UI", 9f);
            _openGameFolderButton.Location = new Point(20, 155);
            _openGameFolderButton.Width = 150;
            _openGameFolderButton.Height = 28;
            _openGameFolderButton.FlatStyle = FlatStyle.Flat;

            // Status Panel
            _statusPanel = new Panel();
            _statusPanel.Location = new Point(20, 195);
            _statusPanel.Size = new Size(600, 90);
            _statusPanel.BorderStyle = BorderStyle.FixedSingle;
            _statusPanel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;

            _statusLabel = new Label();
            _statusLabel.Text = "Status: Not configured";
            _statusLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _statusLabel.AutoSize = true;
            _statusLabel.Location = new Point(15, 12);

            _modsCountLabel = new Label();
            _modsCountLabel.Text = "Active Mods: 0";
            _modsCountLabel.Font = new Font("Segoe UI", 10f);
            _modsCountLabel.AutoSize = true;
            _modsCountLabel.Location = new Point(15, 35);

            _modApiVersionLabel = new Label();
            _modApiVersionLabel.Text = "ModAPI Version: Unknown";
            _modApiVersionLabel.Font = new Font("Segoe UI", 10f);
            _modApiVersionLabel.AutoSize = true;
            _modApiVersionLabel.Location = new Point(15, 58);

            _statusPanel.Controls.Add(_statusLabel);
            _statusPanel.Controls.Add(_modsCountLabel);
            _statusPanel.Controls.Add(_modApiVersionLabel);

            // Launch Buttons
            _launchButton = new ActionButton();
            _launchButton.Text = "Launch Sheltered (Modded)";
            _launchButton.IsPrimary = true;
            _launchButton.Location = new Point(20, 200);
            _launchButton.Width = 250;
            _launchButton.Height = 45;
            _launchButton.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _launchButton.Enabled = false;

            _launchVanillaButton = new ActionButton();
            _launchVanillaButton.Text = "Launch Vanilla (No Mods)";
            _launchVanillaButton.Location = new Point(280, 200);
            _launchVanillaButton.Width = 200;
            _launchVanillaButton.Height = 45;
            _launchVanillaButton.Font = new Font("Segoe UI", 10f);
            _launchVanillaButton.Enabled = false;

            // Log section
            _logLabel = new Label();
            _logLabel.Text = "Activity Log";
            _logLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _logLabel.AutoSize = true;
            _logLabel.Location = new Point(20, 260);

            _clearLogButton = new Button();
            _clearLogButton.Text = "Clear Log";
            _clearLogButton.Font = new Font("Segoe UI", 9f);
            _clearLogButton.Location = new Point(440, 258);
            _clearLogButton.Width = 90;
            _clearLogButton.Height = 24;
            _clearLogButton.FlatStyle = FlatStyle.Flat;
            _clearLogButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            _loadGameLogButton = new Button();
            _loadGameLogButton.Text = "View Game Log";
            _loadGameLogButton.Font = new Font("Segoe UI", 9f);
            _loadGameLogButton.Location = new Point(540, 258);
            _loadGameLogButton.Width = 100;
            _loadGameLogButton.Height = 24;
            _loadGameLogButton.FlatStyle = FlatStyle.Flat;
            _loadGameLogButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // Log container with square border
            _logContainer = new Panel();
            _logContainer.Location = new Point(20, 285);
            _logContainer.Size = new Size(600, 150); // Dynamic sizing handled in Load event
            _logContainer.BorderStyle = BorderStyle.FixedSingle;
            _logContainer.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;

            _logTextBox = new RichTextBox();
            _logTextBox.Location = new Point(0, 0);
            _logTextBox.Dock = DockStyle.Fill;
            _logTextBox.Font = new Font("Consolas", 9f);
            _logTextBox.ReadOnly = true;
            _logTextBox.BorderStyle = BorderStyle.None;
            _logTextBox.ScrollBars = RichTextBoxScrollBars.Vertical;

            _logContainer.Controls.Add(_logTextBox);

            // Add controls
            this.Controls.Add(_gamePathLabel);
            this.Controls.Add(_gamePathTextBox);
            this.Controls.Add(_browseButton);
            this.Controls.Add(_detectButton);
            this.Controls.Add(_modsPathLabel);
            this.Controls.Add(_modsPathTextBox);
            this.Controls.Add(_openModsFolderButton);
            this.Controls.Add(_openGameFolderButton);
            // _statusPanel removed as it's now in the header
            this.Controls.Add(_launchButton);
            this.Controls.Add(_launchVanillaButton);
            this.Controls.Add(_logLabel);
            this.Controls.Add(_clearLogButton);
            this.Controls.Add(_loadGameLogButton);
            this.Controls.Add(_logContainer);

            this.ResumeLayout();
        }

        private void WireEvents()
        {
            _browseButton.Click += BrowseButton_Click;
            _detectButton.Click += DetectGameButton_Click;
            _openModsFolderButton.Click += OpenModsFolderButton_Click;
            _openGameFolderButton.Click += OpenGameFolderButton_Click;
            _launchButton.Click += LaunchButton_Click;
            _launchVanillaButton.Click += LaunchVanillaButton_Click;
            _gamePathTextBox.TextChanged += GamePathTextBox_TextChanged;
            _clearLogButton.Click += ClearLogButton_Click;
            _loadGameLogButton.Click += LoadGameLogButton_Click;
            this.Load += GameSetupTab_Load;
        }

        private void GameSetupTab_Load(object sender, EventArgs e)
        {
            // Recalculate log container width now that parent is sized
            _logContainer.Width = this.ClientSize.Width - 40;
        }

        private void LoadGameLogButton_Click(object sender, EventArgs e)
        {
            if (ViewGameLogRequested != null)
                ViewGameLogRequested(this, EventArgs.Empty);
        }

        private void ClearLogButton_Click(object sender, EventArgs e)
        {
            _logTextBox.Clear();
        }

        /// <summary>
        /// Add a message to the activity log
        /// </summary>
        public void Log(string message)
        {
            if (_logTextBox.InvokeRequired)
            {
                _logTextBox.Invoke(new MethodInvoker(delegate { Log(message); }));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            bool atBottom = _logTextBox.SelectionStart == _logTextBox.TextLength;
            _logTextBox.AppendText("[" + timestamp + "] " + message + "\n");
            
            if (atBottom)
                _logTextBox.ScrollToCaret();
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            BrowseForGame();
        }

        private void BrowseForGame()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Locate Sheltered.exe";
                dialog.Filter = "Sheltered Executable|Sheltered.exe;ShelteredWindows64_EOS.exe|All Executables|*.exe";
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _gamePathTextBox.Text = dialog.FileName;
                    Log("Game path set via browser: " + dialog.FileName);
                }
            }
        }

        private void DetectGameButton_Click(object sender, EventArgs e)
        {
            Log("Searching for Sheltered installation...");
            var settingsService = new Manager.Core.Services.SettingsService();
            string detected = settingsService.Load().GamePath; // This triggers auto-detect if current is invalid
            
            if (!string.IsNullOrEmpty(detected) && File.Exists(detected))
            {
                _gamePathTextBox.Text = detected;
                Log("Game found at: " + detected);
            }
            else
            {
                MessageBox.Show("Could not find Sheltered automatically. Please use 'Browse' to locate Sheltered.exe.", 
                    "Detection Failed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void GamePathTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_isUpdating) return;

            string path = _gamePathTextBox.Text;
            if (path != null) path = path.Trim();
            
            if (string.IsNullOrEmpty(path))
            {
                _modsPathTextBox.Text = string.Empty;
                if (_settings != null) _settings.GamePath = string.Empty;
                UpdateStatus(false);
                return;
            }

            // If a directory was provided, try to find the executable within it
            if (Directory.Exists(path) && !File.Exists(path))
            {
                string[] possibleExes = { "Sheltered.exe", "ShelteredWindows64_EOS.exe" };
                foreach (var exe in possibleExes)
                {
                    string fullPath = Path.Combine(path, exe);
                    if (File.Exists(fullPath))
                    {
                        path = fullPath;
                        _isUpdating = true;
                        _gamePathTextBox.Text = path;
                        _isUpdating = false;
                        break;
                    }
                }
            }

            if (File.Exists(path))
            {
                string gameDir = Path.GetDirectoryName(path);
                string modsPath = Path.Combine(gameDir, "mods");
                
                _modsPathTextBox.Text = modsPath;
                
                if (_settings != null)
                {
                    _settings.GamePath = path;
                    _settings.ModsPath = modsPath;
                }
                
                UpdateStatus(true);
                if (GamePathChanged != null)
                    GamePathChanged(path);
            }
            else
            {
                _modsPathTextBox.Text = string.Empty;
                UpdateStatus(false);
            }
        }

        private void OpenModsFolderButton_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_modsPathTextBox.Text))
            {
                try
                {
                    if (!Directory.Exists(_modsPathTextBox.Text))
                        Directory.CreateDirectory(_modsPathTextBox.Text);
                    
                    System.Diagnostics.Process.Start("explorer.exe", _modsPathTextBox.Text);
                    Log("Opened mods folder");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to open folder: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void OpenGameFolderButton_Click(object sender, EventArgs e)
        {
            if (_settings == null) return;

            if (!_settings.IsGamePathValid)
            {
                BrowseForGame();
                return;
            }

            string path = _gamePathTextBox.Text;
            if (string.IsNullOrEmpty(path)) 
            {
                _settings.GamePath = string.Empty; 
                BrowseForGame();
                return;
            }

            try
            {
                string dir = null;
                if (File.Exists(path))
                    dir = Path.GetDirectoryName(path);
                else if (Directory.Exists(path))
                    dir = path;

                if (dir != null)
                {
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                    Log("Opened game folder: " + dir);
                }
                else
                {
                    Log("Cannot open folder: Path is invalid.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open folder: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LaunchButton_Click(object sender, EventArgs e)
        {
            Log("Launching Sheltered with mods...");
            if (LaunchRequested != null)
                LaunchRequested(true);
        }

        private void LaunchVanillaButton_Click(object sender, EventArgs e)
        {
            Log("Launching Sheltered (vanilla)...");
            if (LaunchRequested != null)
                LaunchRequested(false);
        }

        public void UpdateFromSettings()
        {
            if (_settings == null) return;

            _isUpdating = true;
            try
            {
                _gamePathTextBox.Text = _settings.GamePath ?? string.Empty;
                _modsPathTextBox.Text = _settings.ModsPath ?? string.Empty;
            }
            finally
            {
                _isUpdating = false;
            }
            
            UpdateStatus(_settings.IsGamePathValid);

            if (_settings.IsGamePathValid && _settings.GamePath != _lastLoggedPath)
            {
                Log("Loaded configuration - game found");
                _lastLoggedPath = _settings.GamePath;
            }
        }

        /// <summary>
        /// Update the status display
        /// </summary>
        public void UpdateStatus(bool isValid, int enabledModCount, string modApiVersion)
        {
            if (isValid)
            {
                _statusLabel.Text = "Status: Ready";
                _statusLabel.ForeColor = _isDarkMode ? Color.LightGreen : Color.Green;
                _launchButton.Enabled = true;
                _launchVanillaButton.Enabled = true;
                _openGameFolderButton.Text = "Open Game Folder";
                _openGameFolderButton.Width = 150;
            }
            else
            {
                _statusLabel.Text = "Status: Game not found";
                _statusLabel.ForeColor = Color.Red;
                _launchButton.Enabled = false;
                _launchVanillaButton.Enabled = false;
                _openGameFolderButton.Text = "Set Game Path (.exe)";
                _openGameFolderButton.Width = 180;
            }

            _modsCountLabel.Text = "Active Mods: " + enabledModCount;
            _modApiVersionLabel.Text = "ModAPI Version: " + (modApiVersion ?? "Unknown");
        }

        /// <summary>
        /// Update status with defaults
        /// </summary>
        public void UpdateStatus(bool isValid)
        {
            UpdateStatus(isValid, 0, null);
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
                _gamePathLabel.ForeColor = Color.White;
                _gamePathTextBox.BackColor = Color.FromArgb(60, 60, 60);
                _gamePathTextBox.ForeColor = Color.White;
                _modsPathLabel.ForeColor = Color.White;
                _modsPathTextBox.BackColor = Color.FromArgb(60, 60, 60);
                _modsPathTextBox.ForeColor = Color.White;
                _statusPanel.BackColor = Color.FromArgb(50, 50, 52);
                _modsCountLabel.ForeColor = Color.LightGray;
                _modApiVersionLabel.ForeColor = Color.LightGray;
                _logLabel.ForeColor = Color.White;
                _logContainer.BackColor = Color.FromArgb(60, 60, 60);
                _logTextBox.BackColor = Color.FromArgb(30, 30, 32);
                _logTextBox.ForeColor = Color.LightGray;
                
                ApplyDarkThemeToButton(_browseButton);
                ApplyDarkThemeToButton(_detectButton);
                ApplyDarkThemeToButton(_openModsFolderButton);
                ApplyDarkThemeToButton(_openGameFolderButton);
                ApplyDarkThemeToButton(_clearLogButton);
                ApplyDarkThemeToButton(_loadGameLogButton);
            }
            else
            {
                this.BackColor = SystemColors.Control;
                _gamePathLabel.ForeColor = SystemColors.ControlText;
                _gamePathTextBox.BackColor = SystemColors.Window;
                _gamePathTextBox.ForeColor = SystemColors.WindowText;
                _modsPathLabel.ForeColor = SystemColors.ControlText;
                _modsPathTextBox.BackColor = SystemColors.Window;
                _modsPathTextBox.ForeColor = SystemColors.WindowText;
                _statusPanel.BackColor = Color.FromArgb(250, 250, 250);
                _modsCountLabel.ForeColor = SystemColors.ControlText;
                _modApiVersionLabel.ForeColor = SystemColors.ControlText;
                _logLabel.ForeColor = SystemColors.ControlText;
                _logContainer.BackColor = SystemColors.ControlDark;
                _logTextBox.BackColor = SystemColors.Window;
                _logTextBox.ForeColor = SystemColors.WindowText;
                
                ApplyLightThemeToButton(_browseButton);
                ApplyLightThemeToButton(_detectButton);
                ApplyLightThemeToButton(_openModsFolderButton);
                ApplyLightThemeToButton(_openGameFolderButton);
                ApplyLightThemeToButton(_clearLogButton);
                ApplyLightThemeToButton(_loadGameLogButton);
            }

            _launchButton.ApplyTheme(isDark);
            _launchVanillaButton.ApplyTheme(isDark);
        }

        private void ApplyDarkThemeToButton(Button btn)
        {
            btn.BackColor = Color.FromArgb(70, 70, 70);
            btn.ForeColor = Color.White;
            btn.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
        }

        private void ApplyLightThemeToButton(Button btn)
        {
            btn.BackColor = SystemColors.Control;
            btn.ForeColor = SystemColors.ControlText;
            btn.FlatAppearance.BorderColor = SystemColors.ControlDark;
        }
    }
}
