using System.Windows.Forms;
using System.Drawing;

namespace Manager
{
    public static class ThemeManager
    {
        public static class DarkTheme
        {
            public static Color Background = Color.FromArgb(45, 45, 48);
            public static Color Foreground = Color.FromArgb(255, 255, 255);
            public static Color ControlBackground = Color.FromArgb(60, 60, 60);
            public static Color ControlForeground = Color.FromArgb(255, 255, 255);
            public static Color Accent = Color.FromArgb(0, 122, 204);
            public static Color Border = Color.FromArgb(80, 80, 80);
        }

        public static class LightTheme
        {
            public static Color Background = SystemColors.Control;
            public static Color Foreground = SystemColors.ControlText;
            public static Color ControlBackground = SystemColors.Window;
            public static Color ControlForeground = SystemColors.WindowText;
            public static Color Accent = SystemColors.Highlight;
            public static Color Border = SystemColors.ControlDark;
        }

        public static bool IsDarkMode = false;

        public static void ApplyTheme(Control parentControl)
        {
            var bgColor = IsDarkMode ? DarkTheme.Background : LightTheme.Background;
            var fgColor = IsDarkMode ? DarkTheme.Foreground : LightTheme.Foreground;
            var ctlBgColor = IsDarkMode ? DarkTheme.ControlBackground : LightTheme.ControlBackground;
            var ctlFgColor = IsDarkMode ? DarkTheme.ControlForeground : LightTheme.ControlForeground;

            parentControl.BackColor = bgColor;
            parentControl.ForeColor = fgColor;

            foreach (Control control in parentControl.Controls)
            {
                // Apply to child controls recursively
                ApplyTheme(control);

                if (control is Panel || control is GroupBox || control is TabControl || control is TabPage)
                {
                    control.BackColor = bgColor;
                    control.ForeColor = fgColor;
                }
                else if (control is Button)
                {
                    var btn = (Button)control;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = IsDarkMode ? DarkTheme.Border : LightTheme.Border;
                    btn.BackColor = ctlBgColor;
                    btn.ForeColor = ctlFgColor;
                }
                else if (control is TextBox || control is RichTextBox)
                {
                    control.BackColor = ctlBgColor;
                    control.ForeColor = ctlFgColor;
                }
                else if (control is ListBox || control is ListView)
                {
                    control.BackColor = ctlBgColor;
                    control.ForeColor = ctlFgColor;
                }
                else if (control is CheckBox || control is RadioButton)
                {
                    control.ForeColor = fgColor;
                }
                else if (control is Label)
                {
                    // Special handling for links
                    if (control is LinkLabel)
                    {
                        var link = (LinkLabel)control;
                        link.LinkColor = IsDarkMode ? DarkTheme.Accent : SystemColors.HotTrack;
                        link.ActiveLinkColor = IsDarkMode ? Color.LightBlue : Color.Red;
                        link.VisitedLinkColor = IsDarkMode ? Color.Plum : Color.Purple;
                    }
                    else
                    {
                        control.ForeColor = fgColor;
                    }
                }
            }
        }
    }
}
