using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Manager.Controls;

namespace Manager.Views
{
    public partial class SettingsTab
    {
        // Labels are dimmed with a legible colour instead of Enabled = false,
        // which WinForms paints darker than a dark background.
        private readonly HashSet<Label> _dimmedLabels = new HashSet<Label>();
        private readonly Dictionary<Label, Color> _labelColors = new Dictionary<Label, Color>();
        private bool _sectionRulesHooked;

        private Label[] SectionHeadings()
        {
            return new Label[] { _themeLabel, _savesLabel, _nexusLabel, _runtimeFeaturesLabel };
        }

        private void PaintSectionRules(object sender, PaintEventArgs e)
        {
            using (Pen rule = new Pen(ManagerTheme.Current.Border))
            {
                foreach (Label heading in SectionHeadings())
                {
                    if (heading == null || !heading.Visible)
                        continue;
                    int y = heading.Bottom + 3;
                    e.Graphics.DrawLine(rule, heading.Left, y, _contentPanel.Width - SettingsTabLayout.Margin, y);
                }
            }
        }

        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            ThemePalette palette = ManagerTheme.Current;

            ManagerTheme.Apply(this);
            BackColor = palette.Window;
            if (_scrollPanel != null) _scrollPanel.BackColor = palette.Window;
            if (_contentPanel != null) _contentPanel.BackColor = palette.Window;

            // Section headings match card titles elsewhere: tan serif over a rule.
            foreach (Label heading in SectionHeadings())
                SetLabelColor(heading, palette.AccentText);
            if (!_sectionRulesHooked && _contentPanel != null)
            {
                _contentPanel.Paint += PaintSectionRules;
                _sectionRulesHooked = true;
            }
            _separator.Visible = false;
            SetLabelColor(_autoCondenseLabel, palette.Text);
            SetLabelColor(_saveBackupRetentionLabel, palette.Text);
            SetLabelColor(_saveBackupRetentionCountLabel, palette.Text);
            SetLabelColor(_nexusOAuthStatusLabel, palette.TextMuted);
            SetLabelColor(_nexusAccountSummaryLabel, palette.Text);
            SetLabelColor(_nexusDownloadSummaryLabel, palette.TextMuted);
            SetLabelColor(_nexusDomainLabel, palette.Text);
            SetLabelColor(_managerNexusModIdLabel, palette.Text);
            SetLabelColor(_debugLogScopeLabel, palette.Text);

            // Grouped surfaces.
            _nexusAdvancedPanel.BackColor = palette.Surface;
            _separator.BackColor = palette.Border;
            _devSettingsGroup.BackColor = palette.Surface;
            _devSettingsGroup.ForeColor = palette.Text;

            ApplyRuntimeFeatureTheme(isDark);
        }

        private void ApplyRuntimeFeatureTheme(bool isDark)
        {
            if (_runtimeFeaturesLabel == null)
                return;

            ThemePalette palette = ManagerTheme.Current;
            SetLabelColor(_runtimeFeaturesLabel, palette.AccentText);
            _runtimeFeaturesPanel.BackColor = palette.Surface;
            SetLabelColor(_runtimeFeaturesEmptyLabel, palette.TextMuted);
            _runtimeFeaturesRefreshButton.Invalidate();
            for (int i = 0; i < _runtimeFeatureCheckBoxes.Count; i++)
            {
                CheckBox checkBox = _runtimeFeatureCheckBoxes[i];
                if (checkBox != null)
                    checkBox.ForeColor = palette.Text;
            }
        }

        private void SetLabelColor(Label label, Color color)
        {
            if (label == null)
                return;
            _labelColors[label] = color;
            label.ForeColor = _dimmedLabels.Contains(label) ? ManagerTheme.Current.TextDisabled : color;
        }

        private void SetLabelEnabled(Label label, bool enabled)
        {
            if (label == null)
                return;
            if (enabled)
                _dimmedLabels.Remove(label);
            else
                _dimmedLabels.Add(label);
            Color normal;
            if (!_labelColors.TryGetValue(label, out normal))
                normal = ManagerTheme.Current.Text;
            label.ForeColor = enabled ? normal : ManagerTheme.Current.TextDisabled;
        }
    }
}
