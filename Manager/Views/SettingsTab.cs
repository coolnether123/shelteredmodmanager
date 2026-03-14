using System;
using System.Drawing;
using System.Windows.Forms;
using Manager.Core.Models;

namespace Manager.Views
{
    public delegate void SettingsChangedHandler(AppSettings settings);
    public delegate void DarkModeChangedHandler(bool isDark);
    public delegate void ResetWindowRequestedHandler();

    public class SettingsTab : UserControl
    {
        private Label _themeLabel;
        private CheckBox _darkModeCheckBox;
        private Label _autoCondenseLabel;
        private ComboBox _autoCondenseCombo;
        private Label _nexusLabel;
        private CheckBox _enableNexusCheckBox;
        private Label _nexusApiKeyLabel;
        private TextBox _nexusApiKeyTextBox;
        private Button _nexusApiHelpButton;
        private Button _nexusApiRevealButton;
        private Label _nexusAccountSummaryLabel;
        private Label _nexusDownloadSummaryLabel;
        private LinkLabel _nexusAdvancedToggleLink;
        private Panel _nexusAdvancedPanel;
        private Label _nexusDomainLabel;
        private TextBox _nexusDomainTextBox;
        private Label _managerNexusModIdLabel;
        private TextBox _managerNexusModIdTextBox;
        private Panel _separator;
        private CheckBox _devModeCheckBox;
        private GroupBox _devSettingsGroup;
        private CheckBox _verboseLoggingCheckBox;
        private CheckBox _skipHarmonyCheckBox;
        private CheckBox _ignoreOrderCheckBox;
        private Button _resetButton;
        private Button _resetWindowButton;
        private Timer _saveDebounceTimer;
        private AppSettings _settings;
        private NexusAccountStatus _nexusAccountStatus;
        private bool _isDarkMode;
        private bool _suppressEvents;
        private bool _nexusApiKeyRevealed;
        private bool _skipNextNexusApiAutoHide;
        private bool _showAdvancedNexusOptions;
        private ToolTip _helpToolTip;
        private const string NexusApiKeyHelpUrl = "https://www.nexusmods.com/users/myaccount?tab=api";

        public event SettingsChangedHandler SettingsChanged;
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

        public void SetNexusAccountStatus(NexusAccountStatus status)
        {
            _nexusAccountStatus = status;
            UpdateNexusStatusLabels();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Padding = new Padding(20);
            AutoScroll = true;

            _themeLabel = new Label();
            _themeLabel.Text = "Appearance";
            _themeLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _themeLabel.AutoSize = true;

            _darkModeCheckBox = new CheckBox();
            _darkModeCheckBox.Text = "Dark Mode";
            _darkModeCheckBox.Font = new Font("Segoe UI", 10f);
            _darkModeCheckBox.AutoSize = true;

            _autoCondenseLabel = new Label();
            _autoCondenseLabel.Text = "Auto-Organize Save Slots:";
            _autoCondenseLabel.Font = new Font("Segoe UI", 10f);
            _autoCondenseLabel.AutoSize = true;

            _autoCondenseCombo = new ComboBox();
            _autoCondenseCombo.Font = new Font("Segoe UI", 10f);
            _autoCondenseCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _autoCondenseCombo.Items.AddRange(new object[] { "Ask each time", "Always organize", "Never organize" });
            _autoCondenseCombo.SelectedIndex = 0;

            _nexusLabel = new Label();
            _nexusLabel.Text = "Nexus";
            _nexusLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _nexusLabel.AutoSize = true;

            _enableNexusCheckBox = new CheckBox();
            _enableNexusCheckBox.Text = "Enable Nexus features";
            _enableNexusCheckBox.Font = new Font("Segoe UI", 10f);
            _enableNexusCheckBox.AutoSize = true;

            _nexusApiKeyLabel = new Label();
            _nexusApiKeyLabel.Text = "Personal API Key:";
            _nexusApiKeyLabel.Font = new Font("Segoe UI", 10f);
            _nexusApiKeyLabel.AutoSize = true;

            _nexusApiKeyTextBox = new TextBox();
            _nexusApiKeyTextBox.Font = new Font("Segoe UI", 10f);
            _nexusApiKeyTextBox.Width = 230;

            _nexusApiHelpButton = new Button();
            _nexusApiHelpButton.Text = "Get API Key";
            _nexusApiHelpButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _nexusApiHelpButton.Size = new Size(95, 27);
            _nexusApiHelpButton.FlatStyle = FlatStyle.Flat;
            _nexusApiHelpButton.Cursor = Cursors.Hand;

            _nexusApiRevealButton = new Button();
            _nexusApiRevealButton.Text = "Reveal Key";
            _nexusApiRevealButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _nexusApiRevealButton.Size = new Size(95, 27);
            _nexusApiRevealButton.FlatStyle = FlatStyle.Flat;
            _nexusApiRevealButton.Cursor = Cursors.Hand;

            _nexusAccountSummaryLabel = new Label();
            _nexusAccountSummaryLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _nexusAccountSummaryLabel.AutoSize = false;
            _nexusAccountSummaryLabel.Size = new Size(680, 22);
            _nexusAccountSummaryLabel.Text = "Nexus account: not checked.";

            _nexusDownloadSummaryLabel = new Label();
            _nexusDownloadSummaryLabel.Font = new Font("Segoe UI", 9f);
            _nexusDownloadSummaryLabel.AutoSize = false;
            _nexusDownloadSummaryLabel.Size = new Size(680, 38);
            _nexusDownloadSummaryLabel.Text = "Browsing and update checks work without an API key. Direct installs need an API key and can still be limited by Nexus account and app approval.";

            _nexusAdvancedToggleLink = new LinkLabel();
            _nexusAdvancedToggleLink.Text = "Show Advanced Nexus Options";
            _nexusAdvancedToggleLink.Font = new Font("Segoe UI", 9f);
            _nexusAdvancedToggleLink.AutoSize = true;

            _nexusAdvancedPanel = new Panel();
            _nexusAdvancedPanel.BorderStyle = BorderStyle.FixedSingle;
            _nexusAdvancedPanel.Size = new Size(420, 76);
            _nexusAdvancedPanel.Visible = false;

            _nexusDomainLabel = new Label();
            _nexusDomainLabel.Text = "Game Domain:";
            _nexusDomainLabel.Font = new Font("Segoe UI", 9f);
            _nexusDomainLabel.AutoSize = true;
            _nexusDomainLabel.Location = new Point(12, 12);

            _nexusDomainTextBox = new TextBox();
            _nexusDomainTextBox.Font = new Font("Segoe UI", 9f);
            _nexusDomainTextBox.Location = new Point(130, 9);
            _nexusDomainTextBox.Width = 170;

            _managerNexusModIdLabel = new Label();
            _managerNexusModIdLabel.Text = "Manager Mod ID:";
            _managerNexusModIdLabel.Font = new Font("Segoe UI", 9f);
            _managerNexusModIdLabel.AutoSize = true;
            _managerNexusModIdLabel.Location = new Point(12, 44);

            _managerNexusModIdTextBox = new TextBox();
            _managerNexusModIdTextBox.Font = new Font("Segoe UI", 9f);
            _managerNexusModIdTextBox.Location = new Point(130, 41);
            _managerNexusModIdTextBox.Width = 170;

            _nexusAdvancedPanel.Controls.Add(_nexusDomainLabel);
            _nexusAdvancedPanel.Controls.Add(_nexusDomainTextBox);
            _nexusAdvancedPanel.Controls.Add(_managerNexusModIdLabel);
            _nexusAdvancedPanel.Controls.Add(_managerNexusModIdTextBox);

            _helpToolTip = new ToolTip();
            _helpToolTip.AutoPopDelay = 12000;
            _helpToolTip.InitialDelay = 350;
            _helpToolTip.ReshowDelay = 200;
            _helpToolTip.ShowAlways = true;
            _helpToolTip.SetToolTip(_nexusApiKeyTextBox, "Personal Nexus API key. Needed for direct downloads; browsing and update checks do not require it.");
            _helpToolTip.SetToolTip(_nexusApiHelpButton, "Open the Nexus account page where personal API keys are managed.");
            _helpToolTip.SetToolTip(_nexusApiRevealButton, "Reveal or hide the stored Nexus API key for manual editing.");
            _helpToolTip.SetToolTip(_nexusAdvancedToggleLink, "Show internal Nexus settings that most players should never need to edit.");

            _separator = new Panel();
            _separator.Height = 1;
            _separator.Width = 700;

            _devModeCheckBox = new CheckBox();
            _devModeCheckBox.Text = "Developer Mode (Advanced)";
            _devModeCheckBox.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _devModeCheckBox.AutoSize = true;

            _devSettingsGroup = new GroupBox();
            _devSettingsGroup.Text = "Developer Options";
            _devSettingsGroup.Font = new Font("Segoe UI", 10f);
            _devSettingsGroup.Size = new Size(500, 130);
            _devSettingsGroup.Visible = false;

            _verboseLoggingCheckBox = new CheckBox();
            _verboseLoggingCheckBox.Text = "Verbose Logging (Debug Level)";
            _verboseLoggingCheckBox.Font = new Font("Segoe UI", 10f);
            _verboseLoggingCheckBox.AutoSize = true;
            _verboseLoggingCheckBox.Location = new Point(15, 25);

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

            _resetButton = new Button();
            _resetButton.Text = "Reset to Defaults";
            _resetButton.Font = new Font("Segoe UI", 10f);
            _resetButton.Size = new Size(140, 35);
            _resetButton.FlatStyle = FlatStyle.Flat;

            _resetWindowButton = new Button();
            _resetWindowButton.Text = "Reset Manager Window";
            _resetWindowButton.Font = new Font("Segoe UI", 10f);
            _resetWindowButton.Size = new Size(190, 35);
            _resetWindowButton.FlatStyle = FlatStyle.Flat;

            Controls.Add(_themeLabel);
            Controls.Add(_darkModeCheckBox);
            Controls.Add(_autoCondenseLabel);
            Controls.Add(_autoCondenseCombo);
            Controls.Add(_nexusLabel);
            Controls.Add(_enableNexusCheckBox);
            Controls.Add(_nexusApiKeyLabel);
            Controls.Add(_nexusApiKeyTextBox);
            Controls.Add(_nexusApiHelpButton);
            Controls.Add(_nexusApiRevealButton);
            Controls.Add(_nexusAccountSummaryLabel);
            Controls.Add(_nexusDownloadSummaryLabel);
            Controls.Add(_nexusAdvancedToggleLink);
            Controls.Add(_nexusAdvancedPanel);
            Controls.Add(_separator);
            Controls.Add(_devModeCheckBox);
            Controls.Add(_devSettingsGroup);
            Controls.Add(_resetButton);
            Controls.Add(_resetWindowButton);
            ResumeLayout();
            UpdateDynamicLayout();
        }

        private void SetupSaveDebounce()
        {
            _saveDebounceTimer = new Timer();
            _saveDebounceTimer.Interval = 500;
            _saveDebounceTimer.Tick += SaveDebounceTimer_Tick;
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
            _nexusApiKeyTextBox.KeyDown += NexusApiKeyTextBox_KeyDown;
            _nexusApiKeyTextBox.Leave += NexusApiKeyTextBox_Leave;
            _managerNexusModIdTextBox.TextChanged += ManagerNexusModIdTextBox_TextChanged;
            _nexusApiHelpButton.Click += NexusApiHelpButton_Click;
            _nexusApiRevealButton.MouseDown += NexusApiRevealButton_MouseDown;
            _nexusApiRevealButton.Click += NexusApiRevealButton_Click;
            _nexusAdvancedToggleLink.LinkClicked += NexusAdvancedToggleLink_LinkClicked;
            _resetButton.Click += ResetButton_Click;
            _resetWindowButton.Click += ResetWindowButton_Click;
        }

        private void TriggerSave()
        {
            if (_suppressEvents)
                return;

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

        private void UpdateDynamicLayout()
        {
            int x = 20;
            int y = 20;
            y = LayoutAppearanceSection(x, y);
            y = LayoutNexusSection(x, y);
            y = LayoutDeveloperSection(x, y);
            LayoutActionButtons(x, y);
        }

        private int LayoutAppearanceSection(int x, int y)
        {
            _themeLabel.Location = new Point(x, y);
            y += 30;
            _darkModeCheckBox.Location = new Point(x + 10, y);
            y += 42;
            _autoCondenseLabel.Location = new Point(x + 10, y);
            y += 24;
            _autoCondenseCombo.Location = new Point(x + 10, y);
            _autoCondenseCombo.Width = 240;
            return y + 52;
        }

        private int LayoutNexusSection(int x, int y)
        {
            _nexusLabel.Location = new Point(x, y);
            y += 30;
            _enableNexusCheckBox.Location = new Point(x + 10, y);
            y += 32;
            _nexusApiKeyLabel.Location = new Point(x + 10, y + 4);
            _nexusApiKeyTextBox.Location = new Point(x + 155, y);
            _nexusApiHelpButton.Location = new Point(x + 395, y - 1);
            _nexusApiRevealButton.Location = new Point(x + 495, y - 1);
            y += 38;
            _nexusAccountSummaryLabel.Location = new Point(x + 10, y);
            y += 24;
            _nexusDownloadSummaryLabel.Location = new Point(x + 10, y);
            y += 44;
            _nexusAdvancedToggleLink.Location = new Point(x + 10, y);
            y += 24;
            _nexusAdvancedPanel.Location = new Point(x + 10, y);
            _nexusAdvancedPanel.Visible = _showAdvancedNexusOptions;
            if (_showAdvancedNexusOptions)
                y += _nexusAdvancedPanel.Height + 14;
            else
                y += 6;
            _separator.Location = new Point(x, y);
            return y + 24;
        }

        private int LayoutDeveloperSection(int x, int y)
        {
            _devModeCheckBox.Location = new Point(x, y);
            y += 36;
            _devSettingsGroup.Location = new Point(x, y);
            _devSettingsGroup.Visible = _devModeCheckBox.Checked;
            if (_devSettingsGroup.Visible)
                y += _devSettingsGroup.Height + 15;
            return y;
        }

        private void LayoutActionButtons(int x, int y)
        {
            _resetButton.Location = new Point(x, y);
            _resetWindowButton.Location = new Point(_resetButton.Right + 10, y);
        }

        private void SetNexusInputsEnabled(bool enabled)
        {
            _nexusApiKeyLabel.Enabled = enabled;
            _nexusApiKeyTextBox.Enabled = enabled;
            _nexusApiHelpButton.Enabled = enabled;
            _nexusApiRevealButton.Enabled = enabled && !string.IsNullOrEmpty(_settings != null ? _settings.NexusApiKey : string.Empty);
            _nexusAccountSummaryLabel.Enabled = enabled;
            _nexusDownloadSummaryLabel.Enabled = enabled;
            _nexusAdvancedToggleLink.Enabled = enabled;
            _nexusAdvancedPanel.Enabled = enabled;
            _nexusDomainLabel.Enabled = enabled;
            _nexusDomainTextBox.Enabled = enabled;
            _managerNexusModIdLabel.Enabled = enabled;
            _managerNexusModIdTextBox.Enabled = enabled;
        }

        private void LoadFromSettings()
        {
            if (_settings == null)
                return;

            _suppressEvents = true;
            try
            {
                _darkModeCheckBox.Checked = _settings.DarkMode;
                _devModeCheckBox.Checked = _settings.DevMode;
                _devSettingsGroup.Visible = _settings.DevMode;
                _verboseLoggingCheckBox.Checked = string.Equals(_settings.LogLevel, "Debug", StringComparison.OrdinalIgnoreCase);
                _skipHarmonyCheckBox.Checked = _settings.SkipHarmonyDependencyCheck;
                _ignoreOrderCheckBox.Checked = _settings.IgnoreOrderChecks;

                string condensePref = (_settings.AutoCondenseSaves ?? "ask").ToLowerInvariant();
                if (condensePref == "yes" || condensePref == "true") _autoCondenseCombo.SelectedIndex = 1;
                else if (condensePref == "no" || condensePref == "false") _autoCondenseCombo.SelectedIndex = 2;
                else _autoCondenseCombo.SelectedIndex = 0;

                _enableNexusCheckBox.Checked = _settings.EnableNexusIntegration;
                _nexusDomainTextBox.Text = _settings.NexusGameDomain ?? "sheltered";
                _managerNexusModIdTextBox.Text = _settings.ManagerNexusModId > 0 ? _settings.ManagerNexusModId.ToString() : string.Empty;
                _nexusApiKeyRevealed = false;
                ApplyNexusApiKeyDisplayMode();
                SetNexusInputsEnabled(_enableNexusCheckBox.Checked);
                UpdateNexusStatusLabels();
                UpdateDynamicLayout();
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void UpdateSettingsFromUI()
        {
            if (_settings == null)
                return;

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
            if (IsNexusApiKeyEditable())
                _settings.NexusApiKey = (_nexusApiKeyTextBox.Text ?? string.Empty).Trim();

            int managerModId;
            if (int.TryParse((_managerNexusModIdTextBox.Text ?? string.Empty).Trim(), out managerModId) && managerModId >= 0)
                _settings.ManagerNexusModId = managerModId;
            else
                _settings.ManagerNexusModId = 0;
        }

        private void UpdateNexusStatusLabels()
        {
            if (_nexusAccountStatus == null)
            {
                _nexusAccountSummaryLabel.Text = "Nexus account: not checked yet.";
                _nexusDownloadSummaryLabel.Text = "Browsing and update checks work without an API key. Direct installs need an API key and can still be limited by Nexus account and app approval.";
                return;
            }

            string summary = _nexusAccountStatus.Summary;
            if (!string.IsNullOrEmpty(_nexusAccountStatus.DownloadPreference) || !string.IsNullOrEmpty(_nexusAccountStatus.DownloadLocation))
            {
                summary += " Preference: " +
                    (!string.IsNullOrEmpty(_nexusAccountStatus.DownloadPreference) ? _nexusAccountStatus.DownloadPreference : "unknown") +
                    (!string.IsNullOrEmpty(_nexusAccountStatus.DownloadLocation) ? (" via " + _nexusAccountStatus.DownloadLocation) : string.Empty) +
                    ".";
            }

            _nexusAccountSummaryLabel.Text = "Nexus account: " + summary;

            string detail = _nexusAccountStatus.DirectDownloadSummary;
            if (!string.IsNullOrEmpty(_nexusAccountStatus.ErrorMessage))
                detail += " Details: " + _nexusAccountStatus.ErrorMessage;
            _nexusDownloadSummaryLabel.Text = detail;
        }

        private void ApplyNexusApiKeyDisplayMode()
        {
            if (_settings == null)
                return;

            string stored = (_settings.NexusApiKey ?? string.Empty).Trim();
            bool hasStored = stored.Length > 0;
            bool previousSuppress = _suppressEvents;

            _suppressEvents = true;
            try
            {
                if (!hasStored)
                {
                    _nexusApiKeyTextBox.ReadOnly = false;
                    _nexusApiKeyTextBox.UseSystemPasswordChar = false;
                    _nexusApiKeyTextBox.Text = string.Empty;
                    _nexusApiRevealButton.Text = "Reveal Key";
                    _nexusApiRevealButton.Enabled = false;
                    return;
                }

                if (_nexusApiKeyRevealed)
                {
                    _nexusApiKeyTextBox.ReadOnly = false;
                    _nexusApiKeyTextBox.UseSystemPasswordChar = false;
                    _nexusApiKeyTextBox.Text = stored;
                    _nexusApiRevealButton.Text = "Hide Key";
                    _nexusApiRevealButton.Enabled = true;
                }
                else
                {
                    _nexusApiKeyTextBox.ReadOnly = true;
                    _nexusApiKeyTextBox.UseSystemPasswordChar = true;
                    _nexusApiKeyTextBox.Text = stored;
                    _nexusApiRevealButton.Text = "Reveal Key";
                    _nexusApiRevealButton.Enabled = true;
                }
            }
            finally
            {
                _suppressEvents = previousSuppress;
            }
        }

        private bool HasStoredNexusApiKey()
        {
            return _settings != null && !string.IsNullOrEmpty((_settings.NexusApiKey ?? string.Empty).Trim());
        }

        private bool IsNexusApiKeyEditable()
        {
            return _settings != null && (_nexusApiKeyRevealed || !HasStoredNexusApiKey());
        }

        private void DarkModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
                return;

            _isDarkMode = _darkModeCheckBox.Checked;
            if (_settings != null)
                _settings.DarkMode = _isDarkMode;
            if (DarkModeChanged != null)
                DarkModeChanged(_isDarkMode);
            TriggerSave();
        }

        private void DevModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_settings != null)
                _settings.DevMode = _devModeCheckBox.Checked;
            UpdateDynamicLayout();
            TriggerSave();
        }

        private void VerboseLoggingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
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

        private void AutoCondenseCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null)
                return;

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
            if (_suppressEvents || _settings == null)
                return;
            _settings.NexusGameDomain = (_nexusDomainTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            TriggerSave();
        }

        private void NexusApiKeyTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null || !IsNexusApiKeyEditable())
                return;
            _settings.NexusApiKey = (_nexusApiKeyTextBox.Text ?? string.Empty).Trim();
            _nexusApiRevealButton.Enabled = !string.IsNullOrEmpty(_settings.NexusApiKey);
            TriggerSave();
        }

        private void NexusApiKeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (_suppressEvents || _settings == null || !IsNexusApiKeyEditable())
                return;

            _settings.NexusApiKey = (_nexusApiKeyTextBox.Text ?? string.Empty).Trim();
            _nexusApiRevealButton.Enabled = !string.IsNullOrEmpty(_settings.NexusApiKey);
            TriggerSave();
            if (!string.IsNullOrEmpty(_settings.NexusApiKey))
            {
                _nexusApiKeyRevealed = false;
                ApplyNexusApiKeyDisplayMode();
            }
        }

        private void NexusApiKeyTextBox_Leave(object sender, EventArgs e)
        {
            if (_settings == null || !_nexusApiKeyRevealed)
                return;
            if (_skipNextNexusApiAutoHide)
            {
                _skipNextNexusApiAutoHide = false;
                return;
            }
            if (!string.IsNullOrEmpty(_settings.NexusApiKey))
            {
                _nexusApiKeyRevealed = false;
                ApplyNexusApiKeyDisplayMode();
            }
        }

        private void ManagerNexusModIdTextBox_TextChanged(object sender, EventArgs e)
        {
            if (_suppressEvents || _settings == null)
                return;

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

        private void NexusApiHelpButton_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(NexusApiKeyHelpUrl);
            }
            catch
            {
                MessageBox.Show("Unable to open the Nexus API page automatically.\n\nOpen this URL manually:\n" + NexusApiKeyHelpUrl,
                    "Nexus API Key Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void NexusApiRevealButton_MouseDown(object sender, MouseEventArgs e)
        {
            _skipNextNexusApiAutoHide = true;
        }

        private void NexusApiRevealButton_Click(object sender, EventArgs e)
        {
            if (_settings == null)
                return;
            if (string.IsNullOrEmpty(_settings.NexusApiKey))
            {
                _nexusApiKeyRevealed = true;
                ApplyNexusApiKeyDisplayMode();
                _nexusApiKeyTextBox.Focus();
                return;
            }

            _nexusApiKeyRevealed = !_nexusApiKeyRevealed;
            ApplyNexusApiKeyDisplayMode();
            if (_nexusApiKeyRevealed)
                _nexusApiKeyTextBox.Focus();
        }

        private void NexusAdvancedToggleLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _showAdvancedNexusOptions = !_showAdvancedNexusOptions;
            _nexusAdvancedToggleLink.Text = _showAdvancedNexusOptions ? "Hide Advanced Nexus Options" : "Show Advanced Nexus Options";
            UpdateDynamicLayout();
        }

        private void ResetButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Reset all settings to defaults?", "Confirm Reset",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _settings = new AppSettings();
            _nexusAccountStatus = null;
            LoadFromSettings();
            if (SettingsChanged != null)
                SettingsChanged(_settings);
        }

        private void ResetWindowButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Reset manager window size and position to default?", "Reset Window",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (ResetWindowRequested != null)
                ResetWindowRequested();
        }

        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            if (isDark)
            {
                BackColor = Color.FromArgb(45, 45, 48);
                _themeLabel.ForeColor = Color.White;
                _darkModeCheckBox.ForeColor = Color.White;
                _autoCondenseLabel.ForeColor = Color.White;
                _autoCondenseCombo.BackColor = Color.FromArgb(60, 60, 62);
                _autoCondenseCombo.ForeColor = Color.White;
                _autoCondenseCombo.FlatStyle = FlatStyle.Flat;
                _nexusLabel.ForeColor = Color.White;
                _enableNexusCheckBox.ForeColor = Color.White;
                _nexusApiKeyLabel.ForeColor = Color.White;
                _nexusApiKeyTextBox.BackColor = Color.FromArgb(60, 60, 62);
                _nexusApiKeyTextBox.ForeColor = Color.White;
                _nexusAccountSummaryLabel.ForeColor = Color.White;
                _nexusDownloadSummaryLabel.ForeColor = Color.Gainsboro;
                _nexusAdvancedToggleLink.LinkColor = Color.LightBlue;
                _nexusAdvancedPanel.BackColor = Color.FromArgb(50, 50, 52);
                _nexusDomainLabel.ForeColor = Color.White;
                _nexusDomainTextBox.BackColor = Color.FromArgb(60, 60, 62);
                _nexusDomainTextBox.ForeColor = Color.White;
                _managerNexusModIdLabel.ForeColor = Color.White;
                _managerNexusModIdTextBox.BackColor = Color.FromArgb(60, 60, 62);
                _managerNexusModIdTextBox.ForeColor = Color.White;
                _separator.BackColor = Color.FromArgb(92, 92, 96);
                _devModeCheckBox.ForeColor = Color.White;
                _devSettingsGroup.ForeColor = Color.White;
                _devSettingsGroup.BackColor = Color.FromArgb(50, 50, 52);
                _verboseLoggingCheckBox.ForeColor = Color.White;
                _skipHarmonyCheckBox.ForeColor = Color.White;
                _ignoreOrderCheckBox.ForeColor = Color.White;
                ApplyButtonTheme(_nexusApiHelpButton, true);
                ApplyButtonTheme(_nexusApiRevealButton, true);
                ApplyButtonTheme(_resetButton, true);
                ApplyButtonTheme(_resetWindowButton, true);
            }
            else
            {
                BackColor = SystemColors.Control;
                _themeLabel.ForeColor = SystemColors.ControlText;
                _darkModeCheckBox.ForeColor = SystemColors.ControlText;
                _autoCondenseLabel.ForeColor = SystemColors.ControlText;
                _autoCondenseCombo.BackColor = SystemColors.Window;
                _autoCondenseCombo.ForeColor = SystemColors.WindowText;
                _autoCondenseCombo.FlatStyle = FlatStyle.Standard;
                _nexusLabel.ForeColor = SystemColors.ControlText;
                _enableNexusCheckBox.ForeColor = SystemColors.ControlText;
                _nexusApiKeyLabel.ForeColor = SystemColors.ControlText;
                _nexusApiKeyTextBox.BackColor = SystemColors.Window;
                _nexusApiKeyTextBox.ForeColor = SystemColors.WindowText;
                _nexusAccountSummaryLabel.ForeColor = SystemColors.ControlText;
                _nexusDownloadSummaryLabel.ForeColor = SystemColors.ControlText;
                _nexusAdvancedToggleLink.LinkColor = SystemColors.HotTrack;
                _nexusAdvancedPanel.BackColor = SystemColors.Control;
                _nexusDomainLabel.ForeColor = SystemColors.ControlText;
                _nexusDomainTextBox.BackColor = SystemColors.Window;
                _nexusDomainTextBox.ForeColor = SystemColors.WindowText;
                _managerNexusModIdLabel.ForeColor = SystemColors.ControlText;
                _managerNexusModIdTextBox.BackColor = SystemColors.Window;
                _managerNexusModIdTextBox.ForeColor = SystemColors.WindowText;
                _separator.BackColor = SystemColors.ControlDark;
                _devModeCheckBox.ForeColor = SystemColors.ControlText;
                _devSettingsGroup.ForeColor = SystemColors.ControlText;
                _devSettingsGroup.BackColor = SystemColors.Control;
                _verboseLoggingCheckBox.ForeColor = SystemColors.ControlText;
                _skipHarmonyCheckBox.ForeColor = SystemColors.ControlText;
                _ignoreOrderCheckBox.ForeColor = SystemColors.ControlText;
                ApplyButtonTheme(_nexusApiHelpButton, false);
                ApplyButtonTheme(_nexusApiRevealButton, false);
                ApplyButtonTheme(_resetButton, false);
                ApplyButtonTheme(_resetWindowButton, false);
            }
        }

        private static void ApplyButtonTheme(Button button, bool isDark)
        {
            if (button == null)
                return;
            if (isDark)
            {
                button.BackColor = Color.FromArgb(70, 70, 70);
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
            }
            else
            {
                button.BackColor = SystemColors.Control;
                button.ForeColor = SystemColors.ControlText;
                button.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }
        }
    }
}
