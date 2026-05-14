using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Views
{
    public partial class SettingsTab
    {
        private static class SettingsTabLayout
        {
            public const int MinContentWidth = 740;
            public const int Margin = 20;
            public const int SectionGap = 24;
            public const int BottomScrollSlack = 180;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateDynamicLayout();
        }

        private void UpdateDynamicLayout()
        {
            if (_contentPanel == null)
                return;

            Point scrollPosition = _scrollPanel != null ? _scrollPanel.AutoScrollPosition : Point.Empty;

            if (_scrollPanel != null)
                _scrollPanel.SuspendLayout();
            _contentPanel.SuspendLayout();

            try
            {
                int contentWidth = GetContentWidth();
                int x = SettingsTabLayout.Margin;
                int y = SettingsTabLayout.Margin;

                _contentPanel.Width = contentWidth;

                y = LayoutAppearanceSection(x, y);
                y = LayoutNexusSection(x, y, contentWidth);
                y = LayoutRuntimeFeaturesSection(x, y, contentWidth);
                y = LayoutDeveloperSection(x, y);
                y = LayoutActionButtons(x, y, contentWidth);

                int contentHeight = Math.Max(y, GetBottomMostControlEdge()) +
                    SettingsTabLayout.Margin +
                    SettingsTabLayout.BottomScrollSlack;
                _contentPanel.Size = new Size(contentWidth, contentHeight);

                if (_scrollPanel != null)
                {
                    _scrollPanel.AutoScrollMinSize = new Size(contentWidth, contentHeight);
                    _scrollPanel.AutoScrollPosition = new Point(-scrollPosition.X, -scrollPosition.Y);
                }
            }
            finally
            {
                _contentPanel.ResumeLayout(false);
                if (_scrollPanel != null)
                    _scrollPanel.ResumeLayout(false);
            }
        }

        private int GetContentWidth()
        {
            if (_scrollPanel == null)
                return SettingsTabLayout.MinContentWidth;

            int scrollbarReserve = SystemInformation.VerticalScrollBarWidth + 8;
            int viewportWidth = _scrollPanel.ClientSize.Width - scrollbarReserve;
            return Math.Max(SettingsTabLayout.MinContentWidth, viewportWidth);
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

        private int LayoutNexusSection(int x, int y, int contentWidth)
        {
            int usableWidth = GetUsableWidth(contentWidth);
            int summaryWidth = Math.Max(320, usableWidth - 10);

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
            _nexusAccountSummaryLabel.Width = summaryWidth;
            y += 24;

            _nexusDownloadSummaryLabel.Location = new Point(x + 10, y);
            _nexusDownloadSummaryLabel.Width = summaryWidth;
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
            _separator.Width = usableWidth;

            return y + SettingsTabLayout.SectionGap;
        }

        private int LayoutRuntimeFeaturesSection(int x, int y, int contentWidth)
        {
            int panelWidth = GetUsableWidth(contentWidth);

            _runtimeFeaturesLabel.Location = new Point(x, y);
            _runtimeFeaturesRefreshButton.Location = new Point(x + panelWidth - _runtimeFeaturesRefreshButton.Width, y - 2);
            y += 32;

            _runtimeFeaturesPanel.Location = new Point(x, y);
            _runtimeFeaturesPanel.Width = panelWidth;

            _runtimeFeaturesEmptyLabel.Width = Math.Max(260, panelWidth - 24);
            UpdateRuntimeFeatureControlWidths(panelWidth);

            return y + _runtimeFeaturesPanel.Height + SettingsTabLayout.SectionGap;
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

        private int LayoutActionButtons(int x, int y, int contentWidth)
        {
            _resetButton.Location = new Point(x, y);
            _resetWindowButton.Location = new Point(_resetButton.Right + 10, y);

            return Math.Max(_resetButton.Bottom, _resetWindowButton.Bottom);
        }

        private int GetUsableWidth(int contentWidth)
        {
            return Math.Max(300, contentWidth - (SettingsTabLayout.Margin * 2));
        }

        private int GetBottomMostControlEdge()
        {
            int bottom = 0;
            for (int i = 0; i < _contentPanel.Controls.Count; i++)
            {
                Control control = _contentPanel.Controls[i];
                if (control != null && control.Visible)
                    bottom = Math.Max(bottom, control.Bottom);
            }

            return bottom;
        }

        private void UpdateRuntimeFeatureControlWidths(int panelWidth)
        {
            int checkBoxWidth = Math.Max(260, panelWidth - 24);
            for (int i = 0; i < _runtimeFeatureCheckBoxes.Count; i++)
            {
                CheckBox checkBox = _runtimeFeatureCheckBoxes[i];
                if (checkBox != null)
                    checkBox.Width = checkBoxWidth;
            }
        }
    }
}
