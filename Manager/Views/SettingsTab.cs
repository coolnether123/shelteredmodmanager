using System;
using System.Drawing;
using System.Windows.Forms;
using Manager.Core.Models;

namespace Manager.Views
{
    /// <summary>
    /// Delegate for settings changed events
    /// </summary>
    public delegate void SettingsChangedHandler(AppSettings settings);

    /// <summary>
    /// Delegate for dark mode changed events
    /// </summary>
    public delegate void DarkModeChangedHandler(bool isDark);
    public delegate void ResetWindowRequestedHandler();

    /// <summary>
    /// Settings tab - developer options and logging configuration.
    /// </summary>
    public class SettingsTab : UserControl
    {
        // Theme
        private Label _themeLabel;
        private CheckBox _darkModeCheckBox;
        
        // Save slot organization
        private Label _autoCondenseLabel;
        private ComboBox _autoCondenseCombo;

        // Nexus integration
        private Label _nexusLabel;
        private CheckBox _enableNexusCheckBox;
        private Label _nexusDomainLabel;
        private TextBox _nexusDomainTextBox;
        private Label _nexusApiKeyLabel;
        private TextBox _nexusApiKeyTextBox;
        private Label _managerNexusModIdLabel;
        private TextBox _managerNexusModIdTextBox;
        
        private Panel _separator;

        // Developer mode
        private CheckBox _devModeCheckBox;
        private GroupBox _devSettingsGroup;

        // Logging
        private CheckBox _verboseLoggingCheckBox;
        // Log Categories UI removed for v1.0 - categories hardcoded in ModAPI

        // Advanced
        private CheckBox _skipHarmonyCheckBox;
        private CheckBox _ignoreOrderCheckBox;

        // Actions
        private Button _resetButton;
        private Button _resetWindowButton;
        private Timer _saveDebounceTimer;

        // State
        private AppSettings _settings;
        private bool _isDarkMode = false;
        private bool _suppressEvents = false;

        /// <summary>
        /// Event raised when settings change
        /// </summary>
        public event SettingsChangedHandler SettingsChanged;

        /// <summary>
        /// Event raised when dark mode changes
        /// </summary>
        public event DarkModeChangedHandler DarkModeChanged;
        public event ResetWindowRequestedHandler ResetWindowRequested;

        public SettingsTab()
        {
            InitializeComponent();
            SetupSaveDebounce();
            WireEvents();
        }

        public void Initialize(AppSettings settings)
        {
            _settings = settings;
            LoadFromSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Padding = new Padding(20);
            this.AutoScroll = true;

            int yPos = 20;

            // Theme section
            _themeLabel = new Label();
            _themeLabel.Text = "Appearance";
            _themeLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _themeLabel.AutoSize = true;
            _themeLabel.Location = new Point(20, yPos);
            yPos += 30;

            _darkModeCheckBox = new CheckBox();
            _darkModeCheckBox.Text = "Dark Mode";
            _darkModeCheckBox.Font = new Font("Segoe UI", 10f);
            _darkModeCheckBox.AutoSize = true;
            _darkModeCheckBox.Location = new Point(30, yPos);
            yPos += 40;

            // Auto-condense saves setting
            _autoCondenseLabel = new Label();
            _autoCondenseLabel.Text = "Auto-Organize Save Slots:";
            _autoCondenseLabel.Font = new Font("Segoe UI", 10f);
            _autoCondenseLabel.AutoSize = true;
            _autoCondenseLabel.Location = new Point(30, yPos);
            yPos += 25;
            
            _autoCondenseCombo = new ComboBox();
            _autoCondenseCombo.Font = new Font("Segoe UI", 10f);
            _autoCondenseCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _autoCondenseCombo.Items.AddRange(new object[] { "Ask each time", "Always organize", "Never organize" });
            _autoCondenseCombo.SelectedIndex = 0;
            _autoCondenseCombo.Location = new Point(30, yPos);
            _autoCondenseCombo.Width = 200;
            yPos += 45;

            // Nexus settings
            _nexusLabel = new Label();
            _nexusLabel.Text = "Nexus Mods";
            _nexusLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _nexusLabel.AutoSize = true;
            _nexusLabel.Location = new Point(20, yPos);
            yPos += 30;

            _enableNexusCheckBox = new CheckBox();
            _enableNexusCheckBox.Text = "Enable Nexus Integration";
            _enableNexusCheckBox.Font = new Font("Segoe UI", 10f);
            _enableNexusCheckBox.AutoSize = true;
            _enableNexusCheckBox.Location = new Point(30, yPos);
            yPos += 30;

            _nexusDomainLabel = new Label();
            _nexusDomainLabel.Text = "Game Domain:";
            _nexusDomainLabel.Font = new Font("Segoe UI", 10f);
            _nexusDomainLabel.AutoSize = true;
            _nexusDomainLabel.Location = new Point(30, yPos);

            _nexusDomainTextBox = new TextBox();
            _nexusDomainTextBox.Font = new Font("Segoe UI", 10f);
            _nexusDomainTextBox.Location = new Point(150, yPos - 3);
            _nexusDomainTextBox.Width = 190;
            yPos += 30;

            _managerNexusModIdLabel = new Label();
            _managerNexusModIdLabel.Text = "Manager Mod ID:";
            _managerNexusModIdLabel.Font = new Font("Segoe UI", 10f);
            _managerNexusModIdLabel.AutoSize = true;
            _managerNexusModIdLabel.Location = new Point(30, yPos);

            _managerNexusModIdTextBox = new TextBox();
            _managerNexusModIdTextBox.Font = new Font("Segoe UI", 10f);
            _managerNexusModIdTextBox.Location = new Point(150, yPos - 3);
            _managerNexusModIdTextBox.Width = 190;
            yPos += 30;

            _nexusApiKeyLabel = new Label();
            _nexusApiKeyLabel.Text = "API Key (optional):";
            _nexusApiKeyLabel.Font = new Font("Segoe UI", 10f);
            _nexusApiKeyLabel.AutoSize = true;
            _nexusApiKeyLabel.Location = new Point(30, yPos);

            _nexusApiKeyTextBox = new TextBox();
            _nexusApiKeyTextBox.Font = new Font("Segoe UI", 10f);
            _nexusApiKeyTextBox.Location = new Point(150, yPos - 3);
            _nexusApiKeyTextBox.Width = 260;
            yPos += 40;

            // Separator
            _separator = new Panel();
            _separator.BackColor = Color.LightGray;
            _separator.Height = 1;
            _separator.Location = new Point(20, yPos);
            _separator.Width = 500;
            yPos += 20;

            // Developer section
            _devModeCheckBox = new CheckBox();
            _devModeCheckBox.Text = "Developer Mode (Advanced)";
            _devModeCheckBox.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _devModeCheckBox.AutoSize = true;
            _devModeCheckBox.Location = new Point(20, yPos);
            yPos += 35;

            // Developer options group
            _devSettingsGroup = new GroupBox();
            _devSettingsGroup.Text = "Developer Options";
            _devSettingsGroup.Font = new Font("Segoe UI", 10f);
            _devSettingsGroup.Location = new Point(20, yPos);
            _devSettingsGroup.Size = new Size(500, 130);  // Reduced height since Log Categories removed
            _devSettingsGroup.Visible = false;

            _verboseLoggingCheckBox = new CheckBox();
            _verboseLoggingCheckBox.Text = "Verbose Logging (Debug Level)";
            _verboseLoggingCheckBox.Font = new Font("Segoe UI", 10f);
            _verboseLoggingCheckBox.AutoSize = true;
            _verboseLoggingCheckBox.Location = new Point(15, 25);

            // Log Categories UI removed for v1.0 - categories hardcoded in ModAPI

            // Advanced options
            _skipHarmonyCheckBox = new CheckBox();
            _skipHarmonyCheckBox.Text = "Skip Harmony Dependency Check";
            _skipHarmonyCheckBox.Font = new Font("Segoe UI", 10f);
            _skipHarmonyCheckBox.AutoSize = true;
            _skipHarmonyCheckBox.Location = new Point(15, 55);

            _ignoreOrderCheckBox = new CheckBox();
            _ignoreOrderCheckBox.Text = "Ignore Load Order Checks";
            _ignoreOrderCheckBox.Font = new Font("Segoe UI", 10f);
            _ignoreOrderCheckBox.AutoSize = true;
            _ignoreOrderCheckBox.Location = new Point(15, 85);

            _devSettingsGroup.Controls.Add(_verboseLoggingCheckBox);
            _devSettingsGroup.Controls.Add(_skipHarmonyCheckBox);
            _devSettingsGroup.Controls.Add(_ignoreOrderCheckBox);

            // Action buttons - positioned below the dev group when visible


            _resetButton = new Button();
            _resetButton.Text = "Reset to Defaults";
            _resetButton.Font = new Font("Segoe UI", 10f);
            _resetButton.Location = new Point(150, yPos + 10);  // Will be repositioned
            _resetButton.Size = new Size(140, 35);
            _resetButton.FlatStyle = FlatStyle.Flat;

            _resetWindowButton = new Button();
            _resetWindowButton.Text = "Reset Manager Window";
            _resetWindowButton.Font = new Font("Segoe UI", 10f);
            _resetWindowButton.Location = new Point(170, yPos + 10); // Will be repositioned
            _resetWindowButton.Size = new Size(190, 35);
            _resetWindowButton.FlatStyle = FlatStyle.Flat;

            // Add all controls
            this.Controls.Add(_themeLabel);
            this.Controls.Add(_darkModeCheckBox);
            this.Controls.Add(_autoCondenseLabel);
            this.Controls.Add(_autoCondenseCombo);
            this.Controls.Add(_nexusLabel);
            this.Controls.Add(_enableNexusCheckBox);
            this.Controls.Add(_nexusDomainLabel);
            this.Controls.Add(_nexusDomainTextBox);
            this.Controls.Add(_managerNexusModIdLabel);
            this.Controls.Add(_managerNexusModIdTextBox);
            this.Controls.Add(_nexusApiKeyLabel);
            this.Controls.Add(_nexusApiKeyTextBox);
            this.Controls.Add(_separator);
            this.Controls.Add(_devModeCheckBox);
            this.Controls.Add(_devSettingsGroup);
            this.Controls.Add(_resetButton);
            this.Controls.Add(_resetWindowButton);

            this.ResumeLayout();
            
            // Initial button position
            UpdateButtonPositions();
        }

        private void UpdateButtonPositions()
        {
            int baseY = _devModeCheckBox.Bottom + 15;
            if (_devSettingsGroup.Visible)
            {
                baseY = _devSettingsGroup.Bottom + 15;
            }
            _resetButton.Top = baseY;
            _resetButton.Left = 20; // Align to left
            _resetWindowButton.Top = baseY;
            _resetWindowButton.Left = _resetButton.Right + 10;
        }

        private void SetNexusInputsEnabled(bool enabled)
        {
            _nexusDomainLabel.Enabled = enabled;
            _nexusDomainTextBox.Enabled = enabled;
            _nexusApiKeyLabel.Enabled = enabled;
            _nexusApiKeyTextBox.Enabled = enabled;
            _managerNexusModIdLabel.Enabled = enabled;
            _managerNexusModIdTextBox.Enabled = enabled;
        }

        private void SetupSaveDebounce()
        {
            _saveDebounceTimer = new Timer();
            _saveDebounceTimer.Interval = 500; // 500ms delay
            _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;
        }

        private void TriggerSave()
        {
            if (_suppressEvents) return;
            
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
        }

        private void SaveDebounceTimer_Tick(object sender, EventArgs e)
        {
            _saveDebounceTimer.Stop();
            UpdateSettingsFromUI();
            
            if (SettingsChanged != null)
                SettingsChanged(_settings);
        }

        private void WireEvents()
        {
            _darkModeCheckBox.CheckedChanged += DarkModeCheckBox_CheckedChanged;
            _devModeCheckBox.CheckedChanged += DevModeCheckBox_CheckedChanged;
            _verboseLoggingCheckBox.CheckedChanged += VerboseLoggingCheckBox_CheckedChanged;
            _skipHarmonyCheckBox.CheckedChanged += SkipHarmonyCheckBox_CheckedChanged;
            _ignoreOrderCheckBox.CheckedChanged += IgnoreOrderCheckBox_CheckedChanged;
            _autoCondenseCombo.SelectedIndexChanged += AutoCondenseCombo_SelectedIndexChanged;
            _enableNexusCheckBox.CheckedChanged += EnableNexusCheckBox_CheckedChanged;
            _nexusDomainTextBox.TextChanged += NexusDomainTextBox_TextChanged;
            _nexusApiKeyTextBox.TextChanged += NexusApiKeyTextBox_TextChanged;
            _managerNexusModIdTextBox.TextChanged += ManagerNexusModIdTextBox_TextChanged;
            _resetButton.Click += ResetButton_Click;
            _resetWindowButton.Click += ResetWindowButton_Click;
        }

        private void AutoCondenseCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null) return;

            string choice = "ask";
            if (_autoCondenseCombo.SelectedIndex == 1) choice = "yes";
            else if (_autoCondenseCombo.SelectedIndex == 2) choice = "no";

            _settings.AutoCondenseSaves = choice;
            TriggerSave();
        }

        private void EnableNexusCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_settings != null)
                _settings.EnableNexusIntegration = _enableNexusCheckBox.Checked;

            SetNexusInputsEnabled(_enableNexusCheckBox.Checked);
            TriggerSave();
        }

        private void NexusDomainTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null) return;

            string domain = (_nexusDomainTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _settings.NexusGameDomain = domain;
            TriggerSave();
        }

        private void NexusApiKeyTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null) return;

            _settings.NexusApiKey = (_nexusApiKeyTextBox.Text ?? string.Empty).Trim();
            TriggerSave();
        }

        private void ManagerNexusModIdTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null) return;

            string raw = (_managerNexusModIdTextBox.Text ?? string.Empty).Trim();
            if (raw.Length == 0)
            {
                _settings.ManagerNexusModId = 0;
                TriggerSave();
                return;
            }

            int parsed;
            if (int.TryParse(raw, out parsed) && parsed >= 0)
            {
                _settings.ManagerNexusModId = parsed;
                TriggerSave();
            }
        }

        private void DarkModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!_suppressEvents)
            {
                _isDarkMode = _darkModeCheckBox.Checked;
                if (_settings != null) _settings.DarkMode = _isDarkMode;
                if (DarkModeChanged != null)
                    DarkModeChanged(_isDarkMode);
                
                TriggerSave();
            }
        }

        private void DevModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _devSettingsGroup.Visible = _devModeCheckBox.Checked;
            if (_settings != null) _settings.DevMode = _devModeCheckBox.Checked;
            
            // Reposition buttons
            UpdateButtonPositions();
            TriggerSave();
        }

        private void VerboseLoggingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            // Log Categories UI removed - just update LogLevel setting
            if (_settings != null) 
                _settings.LogLevel = _verboseLoggingCheckBox.Checked ? "Debug" : "Info";
            
            TriggerSave();
        }

        private void SkipHarmonyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_settings != null) 
                _settings.SkipHarmonyDependencyCheck = _skipHarmonyCheckBox.Checked;
            
            TriggerSave();
        }

        private void IgnoreOrderCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_settings != null) 
                _settings.IgnoreOrderChecks = _ignoreOrderCheckBox.Checked;
            
            TriggerSave();
        }

        // Log Categories UI removed for v1.0 - categories hardcoded in ModAPI
        // private void LogCategoriesListBox_ItemCheck removed
        // private void UpdateLogCategories removed



        private void ResetButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Reset all settings to defaults?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _settings = new AppSettings();
                LoadFromSettings();
                if (SettingsChanged != null)
                    SettingsChanged(_settings);
            }
        }

        private void ResetWindowButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Reset manager window size and position to default?", "Reset Window",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            if (ResetWindowRequested != null)
                ResetWindowRequested();
        }

        private void LoadFromSettings()
        {
            if (_settings == null) return;

            _suppressEvents = true;
            try
            {
                _darkModeCheckBox.Checked = _settings.DarkMode;
                _devModeCheckBox.Checked = _settings.DevMode;
                _devSettingsGroup.Visible = _settings.DevMode;
                
                bool isDebug = false;
                if (_settings.LogLevel != null)
                    isDebug = string.Equals(_settings.LogLevel, "Debug", StringComparison.OrdinalIgnoreCase);
                
                _verboseLoggingCheckBox.Checked = isDebug;
                // Log Categories UI removed - no listbox to populate

                _skipHarmonyCheckBox.Checked = _settings.SkipHarmonyDependencyCheck;
                _ignoreOrderCheckBox.Checked = _settings.IgnoreOrderChecks;

                // Load AutoCondense setting
                string condensePref = (_settings.AutoCondenseSaves ?? "ask").ToLowerInvariant();
                if (condensePref == "yes" || condensePref == "true") _autoCondenseCombo.SelectedIndex = 1;
                else if (condensePref == "no" || condensePref == "false") _autoCondenseCombo.SelectedIndex = 2;
                else _autoCondenseCombo.SelectedIndex = 0; // ask

                _enableNexusCheckBox.Checked = _settings.EnableNexusIntegration;
                _nexusDomainTextBox.Text = _settings.NexusGameDomain ?? "sheltered";
                _nexusApiKeyTextBox.Text = _settings.NexusApiKey ?? string.Empty;
                _managerNexusModIdTextBox.Text = _settings.ManagerNexusModId > 0 ? _settings.ManagerNexusModId.ToString() : string.Empty;
                SetNexusInputsEnabled(_enableNexusCheckBox.Checked);

                // Update button positions
                UpdateButtonPositions();
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void UpdateSettingsFromUI()
        {
            if (_settings == null) return;

            _settings.DarkMode = _darkModeCheckBox.Checked;
            _settings.DevMode = _devModeCheckBox.Checked;
            _settings.LogLevel = _verboseLoggingCheckBox.Checked ? "Debug" : "Info";
            _settings.SkipHarmonyDependencyCheck = _skipHarmonyCheckBox.Checked;
            _settings.IgnoreOrderChecks = _ignoreOrderCheckBox.Checked;

            string choice = "ask";
            if (_autoCondenseCombo.SelectedIndex == 1) choice = "yes";
            else if (_autoCondenseCombo.SelectedIndex == 2) choice = "no";
            _settings.AutoCondenseSaves = choice;

            _settings.EnableNexusIntegration = _enableNexusCheckBox.Checked;
            _settings.NexusGameDomain = (_nexusDomainTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            _settings.NexusApiKey = (_nexusApiKeyTextBox.Text ?? string.Empty).Trim();

            int managerModId;
            if (int.TryParse((_managerNexusModIdTextBox.Text ?? string.Empty).Trim(), out managerModId) && managerModId >= 0)
                _settings.ManagerNexusModId = managerModId;
            else
                _settings.ManagerNexusModId = 0;
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
                _themeLabel.ForeColor = Color.White;
                _darkModeCheckBox.ForeColor = Color.White;
                _devModeCheckBox.ForeColor = Color.White;
                _devSettingsGroup.ForeColor = Color.White;
                _devSettingsGroup.BackColor = Color.FromArgb(50, 50, 52);
                _verboseLoggingCheckBox.ForeColor = Color.White;
                _skipHarmonyCheckBox.ForeColor = Color.White;
                _ignoreOrderCheckBox.ForeColor = Color.White;
                
                _autoCondenseLabel.ForeColor = Color.White;
                _autoCondenseCombo.BackColor = Color.FromArgb(60, 60, 62);
                _autoCondenseCombo.ForeColor = Color.White;
                _autoCondenseCombo.FlatStyle = FlatStyle.Flat;

                _nexusLabel.ForeColor = Color.White;
                _enableNexusCheckBox.ForeColor = Color.White;
                _nexusDomainLabel.ForeColor = Color.White;
                _nexusDomainTextBox.BackColor = Color.FromArgb(60, 60, 62);
                _nexusDomainTextBox.ForeColor = Color.White;
                _managerNexusModIdLabel.ForeColor = Color.White;
                _managerNexusModIdTextBox.BackColor = Color.FromArgb(60, 60, 62);
                _managerNexusModIdTextBox.ForeColor = Color.White;
                _nexusApiKeyLabel.ForeColor = Color.White;
                _nexusApiKeyTextBox.BackColor = Color.FromArgb(60, 60, 62);
                _nexusApiKeyTextBox.ForeColor = Color.White;
                
                _resetButton.BackColor = Color.FromArgb(70, 70, 70);
                _resetButton.ForeColor = Color.White;
                _resetButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                _resetWindowButton.BackColor = Color.FromArgb(70, 70, 70);
                _resetWindowButton.ForeColor = Color.White;
                _resetWindowButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                this.BackColor = SystemColors.Control;
                _themeLabel.ForeColor = SystemColors.ControlText;
                _darkModeCheckBox.ForeColor = SystemColors.ControlText;
                _devModeCheckBox.ForeColor = SystemColors.ControlText;
                _devSettingsGroup.ForeColor = SystemColors.ControlText;
                _devSettingsGroup.BackColor = SystemColors.Control;
                _verboseLoggingCheckBox.ForeColor = SystemColors.ControlText;
                _skipHarmonyCheckBox.ForeColor = SystemColors.ControlText;
                _ignoreOrderCheckBox.ForeColor = SystemColors.ControlText;

                _autoCondenseLabel.ForeColor = SystemColors.ControlText;
                _autoCondenseCombo.BackColor = SystemColors.Window;
                _autoCondenseCombo.ForeColor = SystemColors.WindowText;
                _autoCondenseCombo.FlatStyle = FlatStyle.Standard;

                _nexusLabel.ForeColor = SystemColors.ControlText;
                _enableNexusCheckBox.ForeColor = SystemColors.ControlText;
                _nexusDomainLabel.ForeColor = SystemColors.ControlText;
                _nexusDomainTextBox.BackColor = SystemColors.Window;
                _nexusDomainTextBox.ForeColor = SystemColors.WindowText;
                _managerNexusModIdLabel.ForeColor = SystemColors.ControlText;
                _managerNexusModIdTextBox.BackColor = SystemColors.Window;
                _managerNexusModIdTextBox.ForeColor = SystemColors.WindowText;
                _nexusApiKeyLabel.ForeColor = SystemColors.ControlText;
                _nexusApiKeyTextBox.BackColor = SystemColors.Window;
                _nexusApiKeyTextBox.ForeColor = SystemColors.WindowText;
                
                _resetButton.BackColor = SystemColors.Control;
                _resetButton.ForeColor = SystemColors.ControlText;
                _resetButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
                _resetWindowButton.BackColor = SystemColors.Control;
                _resetWindowButton.ForeColor = SystemColors.ControlText;
                _resetWindowButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }
        }
    }
}
