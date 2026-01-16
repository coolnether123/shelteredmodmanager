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

    /// <summary>
    /// Settings tab - developer options and logging configuration.
    /// </summary>
    public class SettingsTab : UserControl
    {
        // Theme
        private Label _themeLabel;
        private CheckBox _darkModeCheckBox;
        private Panel _separator;

        // Developer mode
        private CheckBox _devModeCheckBox;
        private GroupBox _devSettingsGroup;

        // Logging
        private CheckBox _verboseLoggingCheckBox;
        private Label _logCategoriesLabel;
        private CheckedListBox _logCategoriesListBox;

        // Advanced
        private CheckBox _skipHarmonyCheckBox;
        private CheckBox _ignoreOrderCheckBox;

        // Actions
        private Button _saveButton;
        private Button _resetButton;

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

        public SettingsTab()
        {
            InitializeComponent();
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
            _devSettingsGroup.Size = new Size(500, 280);
            _devSettingsGroup.Visible = false;

            // Logging within dev group
            _verboseLoggingCheckBox = new CheckBox();
            _verboseLoggingCheckBox.Text = "Verbose Logging (Debug Level)";
            _verboseLoggingCheckBox.Font = new Font("Segoe UI", 10f);
            _verboseLoggingCheckBox.AutoSize = true;
            _verboseLoggingCheckBox.Location = new Point(15, 25);

            _logCategoriesLabel = new Label();
            _logCategoriesLabel.Text = "Log Categories:";
            _logCategoriesLabel.Font = new Font("Segoe UI", 9f);
            _logCategoriesLabel.AutoSize = true;
            _logCategoriesLabel.Location = new Point(15, 55);

            _logCategoriesListBox = new CheckedListBox();
            _logCategoriesListBox.Font = new Font("Segoe UI", 9f);
            _logCategoriesListBox.Location = new Point(15, 80);
            _logCategoriesListBox.Size = new Size(200, 120);
            _logCategoriesListBox.CheckOnClick = true;
            _logCategoriesListBox.Enabled = false;

            // Add log categories
            foreach (var cat in AppSettings.AllLogCategories)
            {
                _logCategoriesListBox.Items.Add(cat);
            }

            // Advanced options
            _skipHarmonyCheckBox = new CheckBox();
            _skipHarmonyCheckBox.Text = "Skip Harmony Dependency Check";
            _skipHarmonyCheckBox.Font = new Font("Segoe UI", 10f);
            _skipHarmonyCheckBox.AutoSize = true;
            _skipHarmonyCheckBox.Location = new Point(15, 210);

            _ignoreOrderCheckBox = new CheckBox();
            _ignoreOrderCheckBox.Text = "Ignore Load Order Checks";
            _ignoreOrderCheckBox.Font = new Font("Segoe UI", 10f);
            _ignoreOrderCheckBox.AutoSize = true;
            _ignoreOrderCheckBox.Location = new Point(15, 240);

            _devSettingsGroup.Controls.Add(_verboseLoggingCheckBox);
            _devSettingsGroup.Controls.Add(_logCategoriesLabel);
            _devSettingsGroup.Controls.Add(_logCategoriesListBox);
            _devSettingsGroup.Controls.Add(_skipHarmonyCheckBox);
            _devSettingsGroup.Controls.Add(_ignoreOrderCheckBox);

            // Action buttons - positioned below the dev group when visible
            _saveButton = new Button();
            _saveButton.Text = "Save Settings";
            _saveButton.Font = new Font("Segoe UI", 10f);
            _saveButton.Location = new Point(20, yPos + 10);  // Will be repositioned
            _saveButton.Size = new Size(120, 35);
            _saveButton.FlatStyle = FlatStyle.Flat;

            _resetButton = new Button();
            _resetButton.Text = "Reset to Defaults";
            _resetButton.Font = new Font("Segoe UI", 10f);
            _resetButton.Location = new Point(150, yPos + 10);  // Will be repositioned
            _resetButton.Size = new Size(140, 35);
            _resetButton.FlatStyle = FlatStyle.Flat;

            // Add all controls
            this.Controls.Add(_themeLabel);
            this.Controls.Add(_darkModeCheckBox);
            this.Controls.Add(_separator);
            this.Controls.Add(_devModeCheckBox);
            this.Controls.Add(_devSettingsGroup);
            this.Controls.Add(_saveButton);
            this.Controls.Add(_resetButton);

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
            _saveButton.Top = baseY;
            _resetButton.Top = baseY;
        }

        private void WireEvents()
        {
            _darkModeCheckBox.CheckedChanged += DarkModeCheckBox_CheckedChanged;
            _devModeCheckBox.CheckedChanged += DevModeCheckBox_CheckedChanged;
            _verboseLoggingCheckBox.CheckedChanged += VerboseLoggingCheckBox_CheckedChanged;
            _skipHarmonyCheckBox.CheckedChanged += SkipHarmonyCheckBox_CheckedChanged;
            _ignoreOrderCheckBox.CheckedChanged += IgnoreOrderCheckBox_CheckedChanged;
            _logCategoriesListBox.ItemCheck += LogCategoriesListBox_ItemCheck;
            _saveButton.Click += SaveButton_Click;
            _resetButton.Click += ResetButton_Click;
        }

        private void DarkModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!_suppressEvents)
            {
                _isDarkMode = _darkModeCheckBox.Checked;
                if (_settings != null) _settings.DarkMode = _isDarkMode;
                if (DarkModeChanged != null)
                    DarkModeChanged(_isDarkMode);
            }
        }

        private void DevModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _devSettingsGroup.Visible = _devModeCheckBox.Checked;
            if (_settings != null) _settings.DevMode = _devModeCheckBox.Checked;
            
            // Reposition buttons
            UpdateButtonPositions();
        }

        private void VerboseLoggingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _logCategoriesListBox.Enabled = _verboseLoggingCheckBox.Checked;
            if (_settings != null) 
                _settings.LogLevel = _verboseLoggingCheckBox.Checked ? "Debug" : "Info";
        }

        private void SkipHarmonyCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_settings != null) 
                _settings.SkipHarmonyDependencyCheck = _skipHarmonyCheckBox.Checked;
        }

        private void IgnoreOrderCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_settings != null) 
                _settings.IgnoreOrderChecks = _ignoreOrderCheckBox.Checked;
        }

        private void LogCategoriesListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (_settings != null)
            {
                // Need to use BeginInvoke because ItemCheck fires before the change is applied
                this.BeginInvoke(new MethodInvoker(UpdateLogCategories));
            }
        }

        private void UpdateLogCategories()
        {
            if (_settings == null) return;
            
            _settings.LogCategories.Clear();
            foreach (var item in _logCategoriesListBox.CheckedItems)
            {
                _settings.LogCategories.Add(item.ToString());
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            UpdateSettingsFromUI();
            if (SettingsChanged != null)
                SettingsChanged(_settings);
            MessageBox.Show("Settings saved!", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

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
                _logCategoriesListBox.Enabled = _verboseLoggingCheckBox.Checked;

                // Check matching categories
                for (int i = 0; i < _logCategoriesListBox.Items.Count; i++)
                {
                    var cat = _logCategoriesListBox.Items[i].ToString();
                    _logCategoriesListBox.SetItemChecked(i, _settings.LogCategories.Contains(cat));
                }

                _skipHarmonyCheckBox.Checked = _settings.SkipHarmonyDependencyCheck;
                _ignoreOrderCheckBox.Checked = _settings.IgnoreOrderChecks;

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

            _settings.LogCategories.Clear();
            foreach (var item in _logCategoriesListBox.CheckedItems)
            {
                _settings.LogCategories.Add(item.ToString());
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
                _themeLabel.ForeColor = Color.White;
                _darkModeCheckBox.ForeColor = Color.White;
                _devModeCheckBox.ForeColor = Color.White;
                _devSettingsGroup.ForeColor = Color.White;
                _devSettingsGroup.BackColor = Color.FromArgb(50, 50, 52);
                _verboseLoggingCheckBox.ForeColor = Color.White;
                _logCategoriesLabel.ForeColor = Color.White;
                _logCategoriesListBox.BackColor = Color.FromArgb(60, 60, 60);
                _logCategoriesListBox.ForeColor = Color.White;
                _skipHarmonyCheckBox.ForeColor = Color.White;
                _ignoreOrderCheckBox.ForeColor = Color.White;
                
                _saveButton.BackColor = Color.FromArgb(70, 70, 70);
                _saveButton.ForeColor = Color.White;
                _saveButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                
                _resetButton.BackColor = Color.FromArgb(70, 70, 70);
                _resetButton.ForeColor = Color.White;
                _resetButton.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
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
                _logCategoriesLabel.ForeColor = SystemColors.ControlText;
                _logCategoriesListBox.BackColor = SystemColors.Window;
                _logCategoriesListBox.ForeColor = SystemColors.WindowText;
                _skipHarmonyCheckBox.ForeColor = SystemColors.ControlText;
                _ignoreOrderCheckBox.ForeColor = SystemColors.ControlText;
                
                _saveButton.BackColor = SystemColors.Control;
                _saveButton.ForeColor = SystemColors.ControlText;
                _saveButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
                
                _resetButton.BackColor = SystemColors.Control;
                _resetButton.ForeColor = SystemColors.ControlText;
                _resetButton.FlatAppearance.BorderColor = SystemColors.ControlDark;
            }
        }
    }
}
