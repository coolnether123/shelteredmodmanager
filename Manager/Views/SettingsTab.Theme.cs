using System.Drawing;
using System.Windows.Forms;

namespace Manager.Views
{
    public partial class SettingsTab
    {
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;

            if (isDark)
                ApplyDarkTheme();
            else
                ApplyLightTheme();
        }

        private void ApplyDarkTheme()
        {
            BackColor = Color.FromArgb(45, 45, 48);
            if (_scrollPanel != null) _scrollPanel.BackColor = BackColor;
            if (_contentPanel != null) _contentPanel.BackColor = BackColor;

            _themeLabel.ForeColor = Color.White;
            _darkModeCheckBox.ForeColor = Color.White;
            _autoCondenseLabel.ForeColor = Color.White;
            _autoCondenseCombo.BackColor = Color.FromArgb(60, 60, 62);
            _autoCondenseCombo.ForeColor = Color.White;
            _autoCondenseCombo.FlatStyle = FlatStyle.Flat;
            _saveBackupsLabel.ForeColor = Color.White;
            _saveBackupRetentionLabel.ForeColor = Color.White;
            _saveBackupRetentionCombo.BackColor = Color.FromArgb(60, 60, 62);
            _saveBackupRetentionCombo.ForeColor = Color.White;
            _saveBackupRetentionCombo.FlatStyle = FlatStyle.Flat;
            _saveBackupRetentionCountLabel.ForeColor = Color.White;
            _saveBackupRetentionCountNumeric.BackColor = Color.FromArgb(60, 60, 62);
            _saveBackupRetentionCountNumeric.ForeColor = Color.White;

            _nexusLabel.ForeColor = Color.White;
            _enableNexusCheckBox.ForeColor = Color.White;
            _enableExperimentalPublishCheckBox.ForeColor = Color.White;
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

            ApplyRuntimeFeatureTheme(true);

            _devModeCheckBox.ForeColor = Color.White;
            _devSettingsGroup.ForeColor = Color.White;
            _devSettingsGroup.BackColor = Color.FromArgb(50, 50, 52);
            _verboseLoggingCheckBox.ForeColor = Color.White;
            _skipHarmonyCheckBox.ForeColor = Color.White;
            _ignoreOrderCheckBox.ForeColor = Color.White;
            _includeNexusPrereleaseCheckBox.ForeColor = Color.White;

            ApplyButtonTheme(_nexusApiHelpButton, true);
            ApplyButtonTheme(_nexusApiRevealButton, true);
            ApplyButtonTheme(_resetButton, true);
            ApplyButtonTheme(_resetWindowButton, true);
        }

        private void ApplyLightTheme()
        {
            BackColor = SystemColors.Control;
            if (_scrollPanel != null) _scrollPanel.BackColor = BackColor;
            if (_contentPanel != null) _contentPanel.BackColor = BackColor;

            _themeLabel.ForeColor = SystemColors.ControlText;
            _darkModeCheckBox.ForeColor = SystemColors.ControlText;
            _autoCondenseLabel.ForeColor = SystemColors.ControlText;
            _autoCondenseCombo.BackColor = SystemColors.Window;
            _autoCondenseCombo.ForeColor = SystemColors.WindowText;
            _autoCondenseCombo.FlatStyle = FlatStyle.Standard;
            _saveBackupsLabel.ForeColor = SystemColors.ControlText;
            _saveBackupRetentionLabel.ForeColor = SystemColors.ControlText;
            _saveBackupRetentionCombo.BackColor = SystemColors.Window;
            _saveBackupRetentionCombo.ForeColor = SystemColors.WindowText;
            _saveBackupRetentionCombo.FlatStyle = FlatStyle.Standard;
            _saveBackupRetentionCountLabel.ForeColor = SystemColors.ControlText;
            _saveBackupRetentionCountNumeric.BackColor = SystemColors.Window;
            _saveBackupRetentionCountNumeric.ForeColor = SystemColors.WindowText;

            _nexusLabel.ForeColor = SystemColors.ControlText;
            _enableNexusCheckBox.ForeColor = SystemColors.ControlText;
            _enableExperimentalPublishCheckBox.ForeColor = SystemColors.ControlText;
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

            ApplyRuntimeFeatureTheme(false);

            _devModeCheckBox.ForeColor = SystemColors.ControlText;
            _devSettingsGroup.ForeColor = SystemColors.ControlText;
            _devSettingsGroup.BackColor = SystemColors.Control;
            _verboseLoggingCheckBox.ForeColor = SystemColors.ControlText;
            _skipHarmonyCheckBox.ForeColor = SystemColors.ControlText;
            _ignoreOrderCheckBox.ForeColor = SystemColors.ControlText;
            _includeNexusPrereleaseCheckBox.ForeColor = SystemColors.ControlText;

            ApplyButtonTheme(_nexusApiHelpButton, false);
            ApplyButtonTheme(_nexusApiRevealButton, false);
            ApplyButtonTheme(_resetButton, false);
            ApplyButtonTheme(_resetWindowButton, false);
        }

        private void ApplyRuntimeFeatureTheme(bool isDark)
        {
            if (_runtimeFeaturesLabel == null)
                return;

            if (isDark)
            {
                _runtimeFeaturesLabel.ForeColor = Color.White;
                _runtimeFeaturesPanel.BackColor = Color.FromArgb(50, 50, 52);
                _runtimeFeaturesEmptyLabel.ForeColor = Color.Gainsboro;
                ApplyButtonTheme(_runtimeFeaturesRefreshButton, true);
                ApplyRuntimeFeatureCheckBoxColor(Color.White);
            }
            else
            {
                _runtimeFeaturesLabel.ForeColor = SystemColors.ControlText;
                _runtimeFeaturesPanel.BackColor = SystemColors.Control;
                _runtimeFeaturesEmptyLabel.ForeColor = SystemColors.ControlText;
                ApplyButtonTheme(_runtimeFeaturesRefreshButton, false);
                ApplyRuntimeFeatureCheckBoxColor(SystemColors.ControlText);
            }
        }

        private void ApplyRuntimeFeatureCheckBoxColor(Color color)
        {
            for (int i = 0; i < _runtimeFeatureCheckBoxes.Count; i++)
            {
                CheckBox checkBox = _runtimeFeatureCheckBoxes[i];
                if (checkBox != null)
                    checkBox.ForeColor = color;
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
