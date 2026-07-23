using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Manager.Core.Models;
using Manager.Core.Services;

namespace Manager.Views
{
    public delegate void SettingsChangedHandler(AppSettings settings);
    public delegate void DarkModeChangedHandler(bool isDark);
    public delegate void ResetWindowRequestedHandler();
    public delegate void NexusOAuthRequestedHandler();

    // Section taxonomy: where each kind of setting belongs.
    //
    // The tab is one vertical scroll of labelled sections. Each section is
    // built in a Build<Section>Controls() helper, positioned in a matching
    // Layout<Section>Section() helper (SettingsTab.Layout.cs) and coloured in
    // ApplyTheme() (SettingsTab.Theme.cs). When you add a setting, drop it in
    // the section it belongs to and follow that section's existing pattern.
    //
    //   1. Appearance      Purely visual preferences for the manager UI itself
    //                      (e.g. dark mode). No behavior, nothing destructive.
    //   2. Saves           How the manager touches the player's save files
    //                      (auto-organize slots, backup retention).
    //   3. Nexus           Nexus Mods integration: enable switch, API key,
    //                      account status, prerelease opt-in, and advanced
    //                      endpoint overrides. Everything here is gated by the
    //                      "Enable Nexus features" checkbox.
    //   4. Runtime Features Dynamic toggles registered by mods at runtime and
    //                      read back from ModAPI. This tab does not define them.
    //   5. Developer       Advanced diagnostic/safety-bypass switches, hidden
    //                      behind Developer Mode. A setting belongs here ONLY if
    //                      a normal player never needs it AND a wrong value is
    //                      harmless (logging verbosity, skipping safety checks).
    //   6. Actions         Reset buttons. Always rendered last.
    //
    // Rule of thumb: if a setting drives a user-facing feature, it lives in
    // that feature's section, not in Developer. Developer is for diagnostics
    // and deliberate safety overrides only.
    public partial class SettingsTab : UserControl
    {
        // Scroll host + inner content surface that every section is placed on.
        private Panel _scrollPanel;
        private Panel _contentPanel;

        // 1. Appearance
        private Label _themeLabel;
        private CheckBox _darkModeCheckBox;

        // 2. Saves
        private Label _savesLabel;
        private Label _autoCondenseLabel;
        private ComboBox _autoCondenseCombo;
        private Label _saveBackupRetentionLabel;
        private ComboBox _saveBackupRetentionCombo;
        private Label _saveBackupRetentionCountLabel;
        private NumericUpDown _saveBackupRetentionCountNumeric;

        // 3. Nexus
        private Label _nexusLabel;
        private CheckBox _enableNexusCheckBox;
        private Button _nexusOAuthSignInButton;
        private Button _nexusOAuthSignOutButton;
        private Label _nexusOAuthStatusLabel;
        private Label _nexusApiKeyLabel;
        private TextBox _nexusApiKeyTextBox;
        private Button _nexusApiHelpButton;
        private Button _nexusApiRevealButton;
        private Label _nexusAccountSummaryLabel;
        private Label _nexusDownloadSummaryLabel;
        private CheckBox _includeNexusPrereleaseCheckBox;
        private LinkLabel _nexusAdvancedToggleLink;
        private Panel _nexusAdvancedPanel;
        private Label _nexusDomainLabel;
        private TextBox _nexusDomainTextBox;
        private Label _managerNexusModIdLabel;
        private TextBox _managerNexusModIdTextBox;
        private Panel _separator;

        // 4. Runtime Features
        private Label _runtimeFeaturesLabel;
        private Button _runtimeFeaturesRefreshButton;
        private Panel _runtimeFeaturesPanel;
        private Label _runtimeFeaturesEmptyLabel;

        // 5. Developer
        private CheckBox _devModeCheckBox;
        private GroupBox _devSettingsGroup;
        private CheckBox _verboseLoggingCheckBox;
        private Label _debugLogScopeLabel;
        private ComboBox _debugLogScopeCombo;
        private CheckBox _skipHarmonyCheckBox;
        private CheckBox _ignoreOrderCheckBox;

        // 6. Actions
        private Button _resetButton;
        private Button _resetWindowButton;

        // Shared infrastructure
        private Timer _saveDebounceTimer;
        private AppSettings _settings;
        private NexusAccountStatus _nexusAccountStatus;
        private bool _isDarkMode;
        private bool _suppressEvents;
        private bool _nexusApiKeyRevealed;
        private bool _skipNextNexusApiAutoHide;
        private bool _showAdvancedNexusOptions;
        private bool _nexusOAuthRegistrationAvailable;
        private ToolTip _helpToolTip;
        private readonly ManagerBooleanOptionsService _runtimeOptionsService = new ManagerBooleanOptionsService();
        private readonly List<CheckBox> _runtimeFeatureCheckBoxes = new List<CheckBox>();
        private IList<ManagerBooleanOptionRecord> _runtimeOptions = new List<ManagerBooleanOptionRecord>();
        private const string NexusApiKeyHelpUrl = "https://www.nexusmods.com/settings/api-keys";

        public event SettingsChangedHandler SettingsChanged;
        public event DarkModeChangedHandler DarkModeChanged;
        public event ResetWindowRequestedHandler ResetWindowRequested;
        public event NexusOAuthRequestedHandler NexusOAuthSignInRequested;
        public event NexusOAuthRequestedHandler NexusOAuthSignOutRequested;

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

        public void SetNexusOAuthRegistrationAvailable(bool available)
        {
            _nexusOAuthRegistrationAvailable = available;
            UpdateNexusOAuthControls();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Padding = new Padding(0);
            AutoScroll = false;

            _scrollPanel = new Panel();
            _scrollPanel.Dock = DockStyle.Fill;
            _scrollPanel.AutoScroll = true;
            _scrollPanel.BorderStyle = BorderStyle.None;

            _contentPanel = new Panel();
            _contentPanel.Location = new Point(0, 0);
            _contentPanel.Size = new Size(SettingsTabLayout.MinContentWidth, 600);
            _contentPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            _scrollPanel.SuspendLayout();
            _contentPanel.SuspendLayout();

            // Tooltip host is created first: section builders attach hints as they
            // create controls, keeping each hint next to the control it describes.
            _helpToolTip = new ToolTip();
            _helpToolTip.AutoPopDelay = 12000;
            _helpToolTip.InitialDelay = 350;
            _helpToolTip.ReshowDelay = 200;
            _helpToolTip.ShowAlways = true;

            // Build each section top-to-bottom in the same order it renders.
            BuildAppearanceControls();
            BuildSavesControls();
            BuildNexusControls();
            BuildRuntimeFeaturesControls();
            BuildDeveloperControls();
            BuildActionControls();

            AddSectionControlsToContentPanel();

            _scrollPanel.Controls.Add(_contentPanel);
            Controls.Add(_scrollPanel);
            _contentPanel.ResumeLayout(false);
            _scrollPanel.ResumeLayout(false);
            ResumeLayout(false);
            UpdateDynamicLayout();
        }

        // 1. Appearance
        // Manager-UI look only. Add purely cosmetic toggles here.
        private void BuildAppearanceControls()
        {
            _themeLabel = new Label();
            _themeLabel.Text = "Appearance";
            _themeLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _themeLabel.AutoSize = true;

            _darkModeCheckBox = new CheckBox();
            _darkModeCheckBox.Text = "Dark Mode";
            _darkModeCheckBox.Font = new Font("Segoe UI", 10f);
            _darkModeCheckBox.AutoSize = true;
        }

        // 2. Saves
        // Anything that changes how the manager treats the player's save
        // files: slot organization and pre-overwrite backup retention.
        private void BuildSavesControls()
        {
            _savesLabel = new Label();
            _savesLabel.Text = "Saves";
            _savesLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _savesLabel.AutoSize = true;

            _autoCondenseLabel = new Label();
            _autoCondenseLabel.Text = "Auto-Organize Save Slots:";
            _autoCondenseLabel.Font = new Font("Segoe UI", 10f);
            _autoCondenseLabel.AutoSize = true;

            _autoCondenseCombo = new ComboBox();
            _autoCondenseCombo.Font = new Font("Segoe UI", 10f);
            _autoCondenseCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _autoCondenseCombo.Items.AddRange(new object[] { "Ask each time", "Always organize", "Never organize" });
            _autoCondenseCombo.SelectedIndex = 0;

            _saveBackupRetentionLabel = new Label();
            _saveBackupRetentionLabel.Text = "Backup Retention:";
            _saveBackupRetentionLabel.Font = new Font("Segoe UI", 10f);
            _saveBackupRetentionLabel.AutoSize = true;

            _saveBackupRetentionCombo = new ComboBox();
            _saveBackupRetentionCombo.Font = new Font("Segoe UI", 10f);
            _saveBackupRetentionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _saveBackupRetentionCombo.Items.AddRange(new object[] { "Keep limited snapshots", "No automatic backups", "Keep all snapshots" });
            _saveBackupRetentionCombo.SelectedIndex = 0;
            _helpToolTip.SetToolTip(_saveBackupRetentionCombo, "Controls automatic pre-overwrite save snapshots stored under Mods/ModAPI/Backups/Saves.");

            _saveBackupRetentionCountLabel = new Label();
            _saveBackupRetentionCountLabel.Text = "Snapshots per save:";
            _saveBackupRetentionCountLabel.Font = new Font("Segoe UI", 10f);
            _saveBackupRetentionCountLabel.AutoSize = true;

            _saveBackupRetentionCountNumeric = new NumericUpDown();
            _saveBackupRetentionCountNumeric.Font = new Font("Segoe UI", 10f);
            _saveBackupRetentionCountNumeric.Minimum = 1;
            _saveBackupRetentionCountNumeric.Maximum = 999;
            _saveBackupRetentionCountNumeric.Value = AppSettings.DefaultSaveBackupRetention;
            _saveBackupRetentionCountNumeric.Width = 80;
            _helpToolTip.SetToolTip(_saveBackupRetentionCountNumeric, "Number of unpinned snapshots to keep for each save timeline.");
        }

        // 3. Nexus
        // Nexus Mods integration. The enable checkbox gates every other
        // control in this section (see SetNexusInputsEnabled). Rarely-touched
        // endpoint overrides live behind the "Advanced" collapsible panel.
        private void BuildNexusControls()
        {
            _nexusLabel = new Label();
            _nexusLabel.Text = "Nexus";
            _nexusLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _nexusLabel.AutoSize = true;

            _enableNexusCheckBox = new CheckBox();
            _enableNexusCheckBox.Text = "Enable Nexus features";
            _enableNexusCheckBox.Font = new Font("Segoe UI", 10f);
            _enableNexusCheckBox.AutoSize = true;

            _nexusOAuthSignInButton = new Button();
            _nexusOAuthSignInButton.Text = "Sign in with Nexus";
            _nexusOAuthSignInButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _nexusOAuthSignInButton.Size = new Size(150, 29);
            _nexusOAuthSignInButton.FlatStyle = FlatStyle.Flat;
            _nexusOAuthSignInButton.Cursor = Cursors.Hand;
            _helpToolTip.SetToolTip(_nexusOAuthSignInButton, "Sign in through Nexus OAuth using PKCE and a temporary loopback callback.");

            _nexusOAuthSignOutButton = new Button();
            _nexusOAuthSignOutButton.Text = "Sign out";
            _nexusOAuthSignOutButton.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _nexusOAuthSignOutButton.Size = new Size(95, 29);
            _nexusOAuthSignOutButton.FlatStyle = FlatStyle.Flat;
            _nexusOAuthSignOutButton.Cursor = Cursors.Hand;

            _nexusOAuthStatusLabel = new Label();
            _nexusOAuthStatusLabel.Font = new Font("Segoe UI", 8.5f);
            _nexusOAuthStatusLabel.AutoSize = false;
            _nexusOAuthStatusLabel.Size = new Size(680, 35);
            _nexusOAuthStatusLabel.Text = "OAuth registration is pending Nexus approval.";

            _nexusApiKeyLabel = new Label();
            _nexusApiKeyLabel.Text = "Legacy API Key:";
            _nexusApiKeyLabel.Font = new Font("Segoe UI", 10f);
            _nexusApiKeyLabel.AutoSize = true;

            _nexusApiKeyTextBox = new TextBox();
            _nexusApiKeyTextBox.Font = new Font("Segoe UI", 10f);
            _nexusApiKeyTextBox.Width = 230;
            _helpToolTip.SetToolTip(_nexusApiKeyTextBox, "Optional personal API key fallback. OAuth sign-in is preferred after Nexus registers the application.");

            _nexusApiHelpButton = new Button();
            _nexusApiHelpButton.Text = "Get API Key";
            _nexusApiHelpButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _nexusApiHelpButton.Size = new Size(95, 27);
            _nexusApiHelpButton.FlatStyle = FlatStyle.Flat;
            _nexusApiHelpButton.Cursor = Cursors.Hand;
            _helpToolTip.SetToolTip(_nexusApiHelpButton, "Open the Nexus account page where personal API keys are managed.");

            _nexusApiRevealButton = new Button();
            _nexusApiRevealButton.Text = "Reveal Key";
            _nexusApiRevealButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _nexusApiRevealButton.Size = new Size(95, 27);
            _nexusApiRevealButton.FlatStyle = FlatStyle.Flat;
            _nexusApiRevealButton.Cursor = Cursors.Hand;
            _helpToolTip.SetToolTip(_nexusApiRevealButton, "Reveal or hide the stored Nexus API key for manual editing.");

            _nexusAccountSummaryLabel = new Label();
            _nexusAccountSummaryLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _nexusAccountSummaryLabel.AutoSize = false;
            _nexusAccountSummaryLabel.Size = new Size(680, 22);
            _nexusAccountSummaryLabel.Text = "Nexus account: not checked.";

            _nexusDownloadSummaryLabel = new Label();
            _nexusDownloadSummaryLabel.Font = new Font("Segoe UI", 9f);
            _nexusDownloadSummaryLabel.AutoSize = false;
            _nexusDownloadSummaryLabel.Size = new Size(680, 38);
            _nexusDownloadSummaryLabel.Text = "Browsing and update checks work without sign-in. Direct installs require OAuth or a legacy API key and may still require Nexus download authorization.";

            // Update-channel opt-in: lives with Nexus (not Developer) because it
            // changes which Nexus files surface as available updates.
            _includeNexusPrereleaseCheckBox = new CheckBox();
            _includeNexusPrereleaseCheckBox.Text = "Include Nexus beta/prerelease files in update checks";
            _includeNexusPrereleaseCheckBox.Font = new Font("Segoe UI", 10f);
            _includeNexusPrereleaseCheckBox.AutoSize = true;
            _helpToolTip.SetToolTip(_includeNexusPrereleaseCheckBox, "Also inspect Nexus file versions so beta/prerelease uploads can appear as updates.");

            _nexusAdvancedToggleLink = new LinkLabel();
            _nexusAdvancedToggleLink.Text = "Show Advanced Nexus Options";
            _nexusAdvancedToggleLink.Font = new Font("Segoe UI", 9f);
            _nexusAdvancedToggleLink.AutoSize = true;
            _helpToolTip.SetToolTip(_nexusAdvancedToggleLink, "Show internal Nexus settings that most players should never need to edit.");

            // Advanced panel: endpoint overrides most users never touch.
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

            // Divider closing out the Nexus section.
            _separator = new Panel();
            _separator.Height = 1;
            _separator.Width = 700;
        }

        // 4. Runtime Features
        // Mod-registered toggles loaded from ModAPI at runtime. The checkboxes
        // themselves are created dynamically in RebuildRuntimeFeatureControls.
        private void BuildRuntimeFeaturesControls()
        {
            _runtimeFeaturesLabel = new Label();
            _runtimeFeaturesLabel.Text = "Runtime Features";
            _runtimeFeaturesLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _runtimeFeaturesLabel.AutoSize = true;

            _runtimeFeaturesRefreshButton = new Button();
            _runtimeFeaturesRefreshButton.Text = "Refresh";
            _runtimeFeaturesRefreshButton.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _runtimeFeaturesRefreshButton.Size = new Size(82, 27);
            _runtimeFeaturesRefreshButton.FlatStyle = FlatStyle.Flat;
            _runtimeFeaturesRefreshButton.Cursor = Cursors.Hand;

            _runtimeFeaturesPanel = new Panel();
            _runtimeFeaturesPanel.BorderStyle = BorderStyle.FixedSingle;
            _runtimeFeaturesPanel.Size = new Size(700, 70);

            _runtimeFeaturesEmptyLabel = new Label();
            _runtimeFeaturesEmptyLabel.Text = "No runtime feature toggles have been registered yet. Launch the game once to let ModAPI create them.";
            _runtimeFeaturesEmptyLabel.Font = new Font("Segoe UI", 9f);
            _runtimeFeaturesEmptyLabel.AutoSize = false;
            _runtimeFeaturesEmptyLabel.Size = new Size(660, 36);
            _runtimeFeaturesEmptyLabel.Location = new Point(12, 12);
            _runtimeFeaturesPanel.Controls.Add(_runtimeFeaturesEmptyLabel);
        }

        // 5. Developer
        // Diagnostics and safety-check bypasses, hidden until Dev Mode is on.
        // Only put a setting here if a normal player never needs it and a wrong
        // value cannot corrupt saves or mods (logging, dependency/order skips).
        private void BuildDeveloperControls()
        {
            _devModeCheckBox = new CheckBox();
            _devModeCheckBox.Text = "Developer Mode (Advanced)";
            _devModeCheckBox.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _devModeCheckBox.AutoSize = true;

            _devSettingsGroup = new GroupBox();
            _devSettingsGroup.Text = "Developer Options";
            _devSettingsGroup.Font = new Font("Segoe UI", 10f);
            _devSettingsGroup.Size = new Size(500, 160);
            _devSettingsGroup.Visible = false;

            _verboseLoggingCheckBox = new CheckBox();
            _verboseLoggingCheckBox.Text = "Debug Logging";
            _verboseLoggingCheckBox.Font = new Font("Segoe UI", 10f);
            _verboseLoggingCheckBox.AutoSize = true;
            _verboseLoggingCheckBox.Location = new Point(15, 25);

            _debugLogScopeLabel = new Label();
            _debugLogScopeLabel.Text = "Debug Scope";
            _debugLogScopeLabel.Font = new Font("Segoe UI", 9f);
            _debugLogScopeLabel.AutoSize = true;
            _debugLogScopeLabel.Location = new Point(35, 57);

            _debugLogScopeCombo = new ComboBox();
            _debugLogScopeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _debugLogScopeCombo.Font = new Font("Segoe UI", 9f);
            _debugLogScopeCombo.Items.Add("Mod logs only");
            _debugLogScopeCombo.Items.Add("All logs");
            _debugLogScopeCombo.SelectedIndex = 0;
            _debugLogScopeCombo.Location = new Point(130, 53);
            _debugLogScopeCombo.Size = new Size(170, 25);
            _helpToolTip.SetToolTip(_debugLogScopeCombo, "Mod logs only keeps plugin debug output and suppresses ModAPI/ShelteredAPI framework debug noise. All logs includes loader, patch, UI, save, and framework debug traces.");

            _skipHarmonyCheckBox = new CheckBox();
            _skipHarmonyCheckBox.Text = "Skip Harmony Dependency Check";
            _skipHarmonyCheckBox.Font = new Font("Segoe UI", 10f);
            _skipHarmonyCheckBox.AutoSize = true;
            _skipHarmonyCheckBox.Location = new Point(15, 88);

            _ignoreOrderCheckBox = new CheckBox();
            _ignoreOrderCheckBox.Text = "Ignore Load Order Checks";
            _ignoreOrderCheckBox.Font = new Font("Segoe UI", 10f);
            _ignoreOrderCheckBox.AutoSize = true;
            _ignoreOrderCheckBox.Location = new Point(15, 118);

            _devSettingsGroup.Controls.Add(_verboseLoggingCheckBox);
            _devSettingsGroup.Controls.Add(_debugLogScopeLabel);
            _devSettingsGroup.Controls.Add(_debugLogScopeCombo);
            _devSettingsGroup.Controls.Add(_skipHarmonyCheckBox);
            _devSettingsGroup.Controls.Add(_ignoreOrderCheckBox);
        }

        // 6. Actions
        // Destructive/reset buttons. Always rendered last.
        private void BuildActionControls()
        {
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
        }

        // Registers every section's controls on the scroll surface, in render
        // order. Section-internal children (dev group, advanced panel, runtime
        // panel) are already parented inside their own containers above.
        private void AddSectionControlsToContentPanel()
        {
            // 1. Appearance
            _contentPanel.Controls.Add(_themeLabel);
            _contentPanel.Controls.Add(_darkModeCheckBox);

            // 2. Saves
            _contentPanel.Controls.Add(_savesLabel);
            _contentPanel.Controls.Add(_autoCondenseLabel);
            _contentPanel.Controls.Add(_autoCondenseCombo);
            _contentPanel.Controls.Add(_saveBackupRetentionLabel);
            _contentPanel.Controls.Add(_saveBackupRetentionCombo);
            _contentPanel.Controls.Add(_saveBackupRetentionCountLabel);
            _contentPanel.Controls.Add(_saveBackupRetentionCountNumeric);

            // 3. Nexus
            _contentPanel.Controls.Add(_nexusLabel);
            _contentPanel.Controls.Add(_enableNexusCheckBox);
            _contentPanel.Controls.Add(_nexusOAuthSignInButton);
            _contentPanel.Controls.Add(_nexusOAuthSignOutButton);
            _contentPanel.Controls.Add(_nexusOAuthStatusLabel);
            _contentPanel.Controls.Add(_nexusApiKeyLabel);
            _contentPanel.Controls.Add(_nexusApiKeyTextBox);
            _contentPanel.Controls.Add(_nexusApiHelpButton);
            _contentPanel.Controls.Add(_nexusApiRevealButton);
            _contentPanel.Controls.Add(_nexusAccountSummaryLabel);
            _contentPanel.Controls.Add(_nexusDownloadSummaryLabel);
            _contentPanel.Controls.Add(_includeNexusPrereleaseCheckBox);
            _contentPanel.Controls.Add(_nexusAdvancedToggleLink);
            _contentPanel.Controls.Add(_nexusAdvancedPanel);
            _contentPanel.Controls.Add(_separator);

            // 4. Runtime Features
            _contentPanel.Controls.Add(_runtimeFeaturesLabel);
            _contentPanel.Controls.Add(_runtimeFeaturesRefreshButton);
            _contentPanel.Controls.Add(_runtimeFeaturesPanel);

            // 5. Developer
            _contentPanel.Controls.Add(_devModeCheckBox);
            _contentPanel.Controls.Add(_devSettingsGroup);

            // 6. Actions
            _contentPanel.Controls.Add(_resetButton);
            _contentPanel.Controls.Add(_resetWindowButton);
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
            _debugLogScopeCombo.SelectedIndexChanged += DebugLogScopeCombo_SelectedIndexChanged;
            _skipHarmonyCheckBox.CheckedChanged += SkipHarmonyCheckBox_CheckedChanged;
            _ignoreOrderCheckBox.CheckedChanged += IgnoreOrderCheckBox_CheckedChanged;
            _includeNexusPrereleaseCheckBox.CheckedChanged += IncludeNexusPrereleaseCheckBox_CheckedChanged;
            _autoCondenseCombo.SelectedIndexChanged += AutoCondenseCombo_SelectedIndexChanged;
            _saveBackupRetentionCombo.SelectedIndexChanged += SaveBackupRetentionCombo_SelectedIndexChanged;
            _saveBackupRetentionCountNumeric.ValueChanged += SaveBackupRetentionCountNumeric_ValueChanged;
            _enableNexusCheckBox.CheckedChanged += EnableNexusCheckBox_CheckedChanged;
            _nexusOAuthSignInButton.Click += NexusOAuthSignInButton_Click;
            _nexusOAuthSignOutButton.Click += NexusOAuthSignOutButton_Click;
            _nexusDomainTextBox.TextChanged += NexusDomainTextBox_TextChanged;
            _nexusApiKeyTextBox.TextChanged += NexusApiKeyTextBox_TextChanged;
            _nexusApiKeyTextBox.KeyDown += NexusApiKeyTextBox_KeyDown;
            _nexusApiKeyTextBox.Leave += NexusApiKeyTextBox_Leave;
            _managerNexusModIdTextBox.TextChanged += ManagerNexusModIdTextBox_TextChanged;
            _nexusApiHelpButton.Click += NexusApiHelpButton_Click;
            _nexusApiRevealButton.MouseDown += NexusApiRevealButton_MouseDown;
            _nexusApiRevealButton.Click += NexusApiRevealButton_Click;
            _nexusAdvancedToggleLink.LinkClicked += NexusAdvancedToggleLink_LinkClicked;
            _runtimeFeaturesRefreshButton.Click += RuntimeFeaturesRefreshButton_Click;
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

        private bool ShouldIgnoreSettingsEvent()
        {
            return _suppressEvents || _settings == null;
        }

        private void SaveDebounceTimer_Tick(object sender, EventArgs e)
        {
            _saveDebounceTimer.Stop();
            UpdateSettingsFromUI();
            if (SettingsChanged != null)
                SettingsChanged(_settings);
        }

        private void SetNexusInputsEnabled(bool enabled)
        {
            _nexusOAuthSignInButton.Enabled = enabled && _nexusOAuthRegistrationAvailable &&
                (_settings == null || !_settings.HasNexusOAuthSession);
            _nexusOAuthSignOutButton.Enabled = enabled && _settings != null && _settings.HasNexusOAuthSession;
            _nexusOAuthStatusLabel.Enabled = enabled;
            _nexusApiKeyLabel.Enabled = enabled;
            _nexusApiKeyTextBox.Enabled = enabled;
            _nexusApiHelpButton.Enabled = enabled;
            _nexusApiRevealButton.Enabled = enabled && !string.IsNullOrEmpty(_settings != null ? _settings.NexusApiKey : string.Empty);
            _nexusAccountSummaryLabel.Enabled = enabled;
            _nexusDownloadSummaryLabel.Enabled = enabled;
            _includeNexusPrereleaseCheckBox.Enabled = enabled;
            _nexusAdvancedToggleLink.Enabled = enabled;
            _nexusAdvancedPanel.Enabled = enabled;
            _nexusDomainLabel.Enabled = enabled;
            _nexusDomainTextBox.Enabled = enabled;
            _managerNexusModIdLabel.Enabled = enabled;
            _managerNexusModIdTextBox.Enabled = enabled;
        }

        private void LoadRuntimeOptions()
        {
            _runtimeOptions = _runtimeOptionsService.Load();
            RebuildRuntimeFeatureControls();
        }

        private void RebuildRuntimeFeatureControls()
        {
            for (int i = 0; i < _runtimeFeatureCheckBoxes.Count; i++)
            {
                CheckBox checkBox = _runtimeFeatureCheckBoxes[i];
                if (checkBox != null)
                {
                    _runtimeFeaturesPanel.Controls.Remove(checkBox);
                    checkBox.Dispose();
                }
            }

            _runtimeFeatureCheckBoxes.Clear();
            int optionCount = _runtimeOptions != null ? _runtimeOptions.Count : 0;
            _runtimeFeaturesEmptyLabel.Visible = optionCount == 0;

            int y = 10;
            for (int i = 0; i < optionCount; i++)
            {
                ManagerBooleanOptionRecord option = _runtimeOptions[i];
                if (option == null || string.IsNullOrEmpty(option.id))
                    continue;

                CheckBox checkBox = new CheckBox();
                checkBox.Text = BuildRuntimeOptionText(option);
                checkBox.Checked = option.value;
                checkBox.Tag = option;
                checkBox.AutoSize = false;
                checkBox.AutoEllipsis = true;
                checkBox.Size = new Size(Math.Max(260, _runtimeFeaturesPanel.Width - 24), 24);
                checkBox.Font = new Font("Segoe UI", 9.5f);
                checkBox.Location = new Point(12, y);
                checkBox.CheckedChanged += RuntimeFeatureCheckBox_CheckedChanged;
                _helpToolTip.SetToolTip(checkBox, BuildRuntimeOptionTooltip(option));
                _runtimeFeaturesPanel.Controls.Add(checkBox);
                _runtimeFeatureCheckBoxes.Add(checkBox);
                y += 28;
            }

            _runtimeFeaturesPanel.Height = optionCount == 0 ? 70 : Math.Max(54, y + 10);
            ApplyRuntimeFeatureTheme(_isDarkMode);
        }

        private static string BuildRuntimeOptionText(ManagerBooleanOptionRecord option)
        {
            string label = !string.IsNullOrEmpty(option.label) ? option.label : option.id;
            if (!string.IsNullOrEmpty(option.owner))
                label = option.owner + ": " + label;
            if (option.requiresRestart)
                label += " (restart required)";
            return label;
        }

        private static string BuildRuntimeOptionTooltip(ManagerBooleanOptionRecord option)
        {
            string tooltip = option != null ? (option.description ?? string.Empty) : string.Empty;
            if (option != null && option.requiresRestart)
            {
                if (tooltip.Length > 0)
                    tooltip += "\n\n";
                tooltip += "Restart the game for changes to take effect.";
            }
            return tooltip;
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
                ApplyDebugLogScopeToUi(_settings.DebugLogScope);
                _skipHarmonyCheckBox.Checked = _settings.SkipHarmonyDependencyCheck;
                _ignoreOrderCheckBox.Checked = _settings.IgnoreOrderChecks;
                _includeNexusPrereleaseCheckBox.Checked = _settings.IncludeNexusPrereleaseFiles;

                string condensePref = (_settings.AutoCondenseSaves ?? "ask").ToLowerInvariant();
                if (condensePref == "yes" || condensePref == "true") _autoCondenseCombo.SelectedIndex = 1;
                else if (condensePref == "no" || condensePref == "false") _autoCondenseCombo.SelectedIndex = 2;
                else _autoCondenseCombo.SelectedIndex = 0;

                ApplySaveBackupRetentionToUi(_settings.SaveBackupRetention);
                _enableNexusCheckBox.Checked = _settings.EnableNexusIntegration;
                _nexusDomainTextBox.Text = _settings.NexusGameDomain ?? "sheltered";
                _managerNexusModIdTextBox.Text = _settings.ManagerNexusModId > 0 ? _settings.ManagerNexusModId.ToString() : string.Empty;
                _nexusApiKeyRevealed = false;
                ApplyNexusApiKeyDisplayMode();
                SetNexusInputsEnabled(_enableNexusCheckBox.Checked);
                UpdateNexusStatusLabels();
                UpdateNexusOAuthControls();
                LoadRuntimeOptions();
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
            _settings.DebugLogScope = ReadDebugLogScopeFromUi();
            _settings.SkipHarmonyDependencyCheck = _skipHarmonyCheckBox.Checked;
            _settings.IgnoreOrderChecks = _ignoreOrderCheckBox.Checked;
            _settings.IncludeNexusPrereleaseFiles = _includeNexusPrereleaseCheckBox.Checked;

            string choice = "ask";
            if (_autoCondenseCombo.SelectedIndex == 1) choice = "yes";
            else if (_autoCondenseCombo.SelectedIndex == 2) choice = "no";
            _settings.AutoCondenseSaves = choice;
            _settings.SaveBackupRetention = ReadSaveBackupRetentionFromUi();

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

        private void ApplySaveBackupRetentionToUi(int retention)
        {
            bool previousSuppress = _suppressEvents;
            _suppressEvents = true;
            try
            {
                if (retention == AppSettings.SaveBackupRetentionDisabled)
                {
                    _saveBackupRetentionCombo.SelectedIndex = 1;
                }
                else if (retention < 0)
                {
                    _saveBackupRetentionCombo.SelectedIndex = 2;
                }
                else
                {
                    _saveBackupRetentionCombo.SelectedIndex = 0;
                    _saveBackupRetentionCountNumeric.Value = Math.Max(
                        _saveBackupRetentionCountNumeric.Minimum,
                        Math.Min(_saveBackupRetentionCountNumeric.Maximum, retention));
                }

                UpdateSaveBackupRetentionInputs();
            }
            finally
            {
                _suppressEvents = previousSuppress;
            }
        }

        private int ReadSaveBackupRetentionFromUi()
        {
            if (_saveBackupRetentionCombo.SelectedIndex == 1)
                return AppSettings.SaveBackupRetentionDisabled;
            if (_saveBackupRetentionCombo.SelectedIndex == 2)
                return AppSettings.SaveBackupRetentionAlways;
            return (int)_saveBackupRetentionCountNumeric.Value;
        }

        private void ApplyDebugLogScopeToUi(string scope)
        {
            string normalized = AppSettings.NormalizeDebugLogScope(scope);
            _debugLogScopeCombo.SelectedIndex = normalized == AppSettings.DebugLogScopeAll ? 1 : 0;
            UpdateDebugLogScopeInputs();
        }

        private string ReadDebugLogScopeFromUi()
        {
            return _debugLogScopeCombo.SelectedIndex == 1
                ? AppSettings.DebugLogScopeAll
                : AppSettings.DebugLogScopeMod;
        }

        private void UpdateSaveBackupRetentionInputs()
        {
            bool limited = _saveBackupRetentionCombo.SelectedIndex == 0;
            _saveBackupRetentionCountLabel.Enabled = limited;
            _saveBackupRetentionCountNumeric.Enabled = limited;
        }

        private void UpdateDebugLogScopeInputs()
        {
            bool enabled = _verboseLoggingCheckBox.Checked;
            _debugLogScopeLabel.Enabled = enabled;
            _debugLogScopeCombo.Enabled = enabled;
        }

        private void UpdateNexusStatusLabels()
        {
            if (_nexusAccountStatus == null)
            {
                _nexusAccountSummaryLabel.Text = "Nexus account: not checked yet.";
                _nexusDownloadSummaryLabel.Text = "Browsing and update checks work without sign-in. Direct installs require OAuth or a legacy API key and may still require Nexus download authorization.";
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

        private void UpdateNexusOAuthControls()
        {
            if (_nexusOAuthSignInButton == null)
                return;

            bool enabled = _settings == null || _settings.EnableNexusIntegration;
            bool hasSession = _settings != null && _settings.HasNexusOAuthSession;
            _nexusOAuthSignInButton.Enabled = enabled && _nexusOAuthRegistrationAvailable && !hasSession;
            _nexusOAuthSignOutButton.Enabled = enabled && hasSession;

            if (hasSession)
            {
                _nexusOAuthStatusLabel.Text = "OAuth session stored securely with Windows DPAPI.";
            }
            else if (_nexusOAuthRegistrationAvailable)
            {
                _nexusOAuthStatusLabel.Text = "OAuth is ready. Sign in opens Nexus in your browser and returns only to 127.0.0.1.";
            }
            else
            {
                _nexusOAuthStatusLabel.Text = "OAuth callback ready at http://127.0.0.1:52147/callback; Nexus client registration is pending.";
            }
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
            if (ShouldIgnoreSettingsEvent())
                return;

            _isDarkMode = _darkModeCheckBox.Checked;
            _settings.DarkMode = _isDarkMode;
            if (DarkModeChanged != null)
                DarkModeChanged(_isDarkMode);
            TriggerSave();
        }

        private void DevModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.DevMode = _devModeCheckBox.Checked;
            UpdateDynamicLayout();
            TriggerSave();
        }

        private void VerboseLoggingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.LogLevel = _verboseLoggingCheckBox.Checked ? "Debug" : "Info";
            _settings.DebugLogScope = ReadDebugLogScopeFromUi();
            UpdateDebugLogScopeInputs();
            TriggerSave();
        }

        private void DebugLogScopeCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.DebugLogScope = ReadDebugLogScopeFromUi();
            TriggerSave();
        }

        private void SkipHarmonyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.SkipHarmonyDependencyCheck = _skipHarmonyCheckBox.Checked;
            TriggerSave();
        }

        private void IgnoreOrderCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.IgnoreOrderChecks = _ignoreOrderCheckBox.Checked;
            TriggerSave();
        }

        private void IncludeNexusPrereleaseCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.IncludeNexusPrereleaseFiles = _includeNexusPrereleaseCheckBox.Checked;
            TriggerSave();
        }

        private void AutoCondenseCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            string choice = "ask";
            if (_autoCondenseCombo.SelectedIndex == 1) choice = "yes";
            else if (_autoCondenseCombo.SelectedIndex == 2) choice = "no";
            _settings.AutoCondenseSaves = choice;
            TriggerSave();
        }

        private void SaveBackupRetentionCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSaveBackupRetentionInputs();
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.SaveBackupRetention = ReadSaveBackupRetentionFromUi();
            TriggerSave();
        }

        private void SaveBackupRetentionCountNumeric_ValueChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.SaveBackupRetention = ReadSaveBackupRetentionFromUi();
            TriggerSave();
        }

        private void EnableNexusCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;

            _settings.EnableNexusIntegration = _enableNexusCheckBox.Checked;
            SetNexusInputsEnabled(_enableNexusCheckBox.Checked);
            TriggerSave();
        }

        private void NexusOAuthSignInButton_Click(object sender, EventArgs e)
        {
            if (NexusOAuthSignInRequested != null)
                NexusOAuthSignInRequested();
        }

        private void NexusOAuthSignOutButton_Click(object sender, EventArgs e)
        {
            if (_settings == null || !_settings.HasNexusOAuthSession)
                return;

            if (MessageBox.Show(
                "Sign out of Nexus in Sheltered Mod Manager?",
                "Nexus Sign Out",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            if (NexusOAuthSignOutRequested != null)
                NexusOAuthSignOutRequested();
        }

        private void NexusDomainTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent())
                return;
            _settings.NexusGameDomain = (_nexusDomainTextBox.Text ?? string.Empty).Trim().ToLowerInvariant();
            TriggerSave();
        }

        private void NexusApiKeyTextBox_TextChanged(object sender, EventArgs e)
        {
            if (ShouldIgnoreSettingsEvent() || !IsNexusApiKeyEditable())
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
            if (ShouldIgnoreSettingsEvent() || !IsNexusApiKeyEditable())
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
            if (ShouldIgnoreSettingsEvent())
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

        private void RuntimeFeaturesRefreshButton_Click(object sender, EventArgs e)
        {
            LoadRuntimeOptions();
            UpdateDynamicLayout();
        }

        private void RuntimeFeatureCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressEvents)
                return;

            CheckBox checkBox = sender as CheckBox;
            ManagerBooleanOptionRecord option = checkBox != null ? checkBox.Tag as ManagerBooleanOptionRecord : null;
            if (option == null || string.IsNullOrEmpty(option.id))
                return;

            option.value = checkBox.Checked;
            _runtimeOptionsService.SetBool(option.id, option.value);
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

    }
}
