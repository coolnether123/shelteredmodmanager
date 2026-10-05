using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Colour tokens for one manager theme. Views read tokens instead of
    /// hard-coding colours so both themes stay legible and consistent.
    /// </summary>
    public sealed class ThemePalette
    {
        public bool IsDark;
        public Color Chrome;          // Window header and main tab strip.
        public Color Window;          // Page background.
        public Color Surface;         // Cards and grouped content.
        public Color SurfaceAlt;      // Secondary button fill and quiet wells.
        public Color SurfaceHover;    // Hover fill for quiet and secondary controls.
        public Color Input;           // Text fields and lists.
        public Color Border;
        public Color BorderStrong;
        public Color Text;
        public Color TextMuted;
        public Color TextDisabled;
        public Color Accent;          // Primary button fill.
        public Color AccentHover;
        public Color AccentPressed;
        public Color AccentText;      // Accent used as text, underline, and focus ring.
        public Color OnAccent;        // Text drawn on an accent fill.
        public Color Selection;
        public Color SelectionText;
        public Color Success;
        public Color Warning;
        public Color Danger;
        public Color SuccessBack;
        public Color WarningBack;
        public Color DangerBack;
        public Color InfoBack;
        public Color DangerFill;
        public Color DangerFillHover;

        // Dark: neutral charcoal panels with the in-game accents: tan serif
        // headings and rules, rust buttons, green confirmations.
        public static readonly ThemePalette Dark = new ThemePalette
        {
            IsDark = true,
            Chrome = Color.FromArgb(25, 26, 28),
            Window = Color.FromArgb(32, 33, 36),
            Surface = Color.FromArgb(41, 42, 46),
            SurfaceAlt = Color.FromArgb(50, 52, 57),
            SurfaceHover = Color.FromArgb(62, 65, 71),
            Input = Color.FromArgb(24, 25, 28),
            Border = Color.FromArgb(60, 63, 69),
            BorderStrong = Color.FromArgb(98, 102, 110),
            Text = Color.FromArgb(234, 232, 227),
            TextMuted = Color.FromArgb(170, 170, 168),
            TextDisabled = Color.FromArgb(120, 122, 126),
            Accent = Color.FromArgb(122, 72, 51),
            AccentHover = Color.FromArgb(140, 84, 60),
            AccentPressed = Color.FromArgb(100, 58, 41),
            AccentText = Color.FromArgb(224, 198, 142),
            OnAccent = Color.FromArgb(247, 238, 220),
            Selection = Color.FromArgb(80, 58, 43),
            SelectionText = Color.FromArgb(247, 238, 220),
            Success = Color.FromArgb(125, 196, 107),
            Warning = Color.FromArgb(227, 176, 75),
            Danger = Color.FromArgb(224, 115, 92),
            SuccessBack = Color.FromArgb(31, 42, 24),
            WarningBack = Color.FromArgb(51, 40, 15),
            DangerBack = Color.FromArgb(58, 29, 23),
            InfoBack = Color.FromArgb(45, 47, 52),
            DangerFill = Color.FromArgb(156, 59, 44),
            DangerFillHover = Color.FromArgb(176, 70, 53)
        };

        // Light: neutral light grey panels with the same rust and tan accents.
        public static readonly ThemePalette Light = new ThemePalette
        {
            IsDark = false,
            Chrome = Color.FromArgb(226, 227, 229),
            Window = Color.FromArgb(238, 239, 241),
            Surface = Color.FromArgb(250, 250, 251),
            SurfaceAlt = Color.FromArgb(231, 232, 235),
            SurfaceHover = Color.FromArgb(218, 220, 224),
            Input = Color.White,
            Border = Color.FromArgb(208, 211, 216),
            BorderStrong = Color.FromArgb(148, 153, 161),
            Text = Color.FromArgb(33, 34, 37),
            TextMuted = Color.FromArgb(92, 96, 103),
            TextDisabled = Color.FromArgb(126, 130, 137),
            Accent = Color.FromArgb(122, 74, 54),
            AccentHover = Color.FromArgb(140, 86, 65),
            AccentPressed = Color.FromArgb(101, 60, 44),
            AccentText = Color.FromArgb(138, 75, 46),
            OnAccent = Color.FromArgb(255, 248, 236),
            Selection = Color.FromArgb(238, 222, 200),
            SelectionText = Color.FromArgb(33, 34, 37),
            Success = Color.FromArgb(53, 106, 38),
            Warning = Color.FromArgb(138, 90, 0),
            Danger = Color.FromArgb(168, 50, 31),
            SuccessBack = Color.FromArgb(227, 237, 214),
            WarningBack = Color.FromArgb(246, 231, 193),
            DangerBack = Color.FromArgb(244, 220, 211),
            InfoBack = Color.FromArgb(232, 234, 238),
            DangerFill = Color.FromArgb(156, 59, 44),
            DangerFillHover = Color.FromArgb(136, 48, 35)
        };
    }

    /// <summary>
    /// How <see cref="ManagerTheme.Apply"/> colours a control that is not
    /// self-painting. Unregistered controls use their type's default.
    /// </summary>
    public enum ThemeRole
    {
        Default,
        Window,
        Chrome,
        Card,
        Well,
        Heading,
        Muted,
        Success,
        Warning,
        Danger,
        Accent
    }

    /// <summary>
    /// Shared theme state, fonts, and the recursive colour pass used by every
    /// manager view.
    /// </summary>
    public static class ManagerTheme
    {
        public const string FontFamily = "Segoe UI";
        public const string SemiboldFamily = "Segoe UI Semibold";
        public const string DisplayFamily = "Georgia";

        private static readonly Dictionary<Control, ThemeRole> Roles = new Dictionary<Control, ThemeRole>();
        private static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>(StringComparer.Ordinal);

        private static ThemePalette _current = ThemePalette.Dark;

        public static ThemePalette Current
        {
            get { return _current; }
        }

        public static bool IsDark
        {
            get { return _current.IsDark; }
        }

        public static void Use(bool dark)
        {
            _current = dark ? ThemePalette.Dark : ThemePalette.Light;
        }

        public static Font UiFont(float size)
        {
            return GetFont(FontFamily, size, FontStyle.Regular);
        }

        public static Font UiFont(float size, FontStyle style)
        {
            return GetFont(FontFamily, size, style);
        }

        public static Font Semibold(float size)
        {
            return GetFont(SemiboldFamily, size, FontStyle.Regular);
        }

        /// <summary>
        /// Heading face. Georgia Bold is the closest face that ships with every
        /// Windows install to the game's slab-serif headings.
        /// </summary>
        public static Font Display(float size)
        {
            return GetFont(DisplayFamily, size, FontStyle.Bold);
        }

        private static Font GetFont(string family, float size, FontStyle style)
        {
            string key = family + "|" + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
            Font font;
            if (!Fonts.TryGetValue(key, out font))
            {
                font = new Font(family, size, style);
                Fonts[key] = font;
            }
            return font;
        }

        public static void SetRole(Control control, ThemeRole role)
        {
            if (control == null)
                return;
            if (!Roles.ContainsKey(control))
                control.Disposed += delegate { Roles.Remove(control); };
            Roles[control] = role;
            ApplyRole(control, role, _current);
        }

        public static ThemeRole GetRole(Control control)
        {
            ThemeRole role;
            return control != null && Roles.TryGetValue(control, out role) ? role : ThemeRole.Default;
        }

        public static Color StatusColor(ThemeRole role)
        {
            switch (role)
            {
                case ThemeRole.Success: return _current.Success;
                case ThemeRole.Warning: return _current.Warning;
                case ThemeRole.Danger: return _current.Danger;
                case ThemeRole.Accent: return _current.AccentText;
                case ThemeRole.Muted: return _current.TextMuted;
                default: return _current.Text;
            }
        }

        /// <summary>
        /// Colours <paramref name="root"/> and its descendants from the current
        /// palette. Self-painting controls only need an invalidate.
        /// </summary>
        public static void Apply(Control root)
        {
            if (root == null)
                return;
            ApplyRecursive(root, _current);
            root.Invalidate(true);
        }

        private static void ApplyRecursive(Control control, ThemePalette palette)
        {
            ThemeRole role = GetRole(control);
            if (role != ThemeRole.Default)
                ApplyRole(control, role, palette);
            else
                ApplyDefault(control, palette);

            if (control is ActionButton || control is PixelEditorCanvas)
                return;
            for (int i = 0; i < control.Controls.Count; i++)
                ApplyRecursive(control.Controls[i], palette);
        }

        private static void ApplyRole(Control control, ThemeRole role, ThemePalette palette)
        {
            switch (role)
            {
                case ThemeRole.Window:
                    control.BackColor = palette.Window;
                    control.ForeColor = palette.Text;
                    break;
                case ThemeRole.Chrome:
                    control.BackColor = palette.Chrome;
                    control.ForeColor = palette.Text;
                    break;
                case ThemeRole.Card:
                    control.BackColor = palette.Surface;
                    control.ForeColor = palette.Text;
                    break;
                case ThemeRole.Well:
                    control.BackColor = palette.SurfaceAlt;
                    control.ForeColor = palette.Text;
                    break;
                case ThemeRole.Heading:
                    control.ForeColor = palette.Text;
                    break;
                default:
                    control.ForeColor = StatusColor(role);
                    break;
            }
            ApplyInputChrome(control, palette);
        }

        private static void ApplyDefault(Control control, ThemePalette palette)
        {
            LinkLabel link = control as LinkLabel;
            if (link != null)
            {
                link.LinkColor = palette.AccentText;
                link.ActiveLinkColor = palette.AccentText;
                link.VisitedLinkColor = palette.AccentText;
                link.DisabledLinkColor = palette.TextDisabled;
                return;
            }

            if (control is TextBoxBase || control is ListBox || control is ListView ||
                control is ComboBox || control is UpDownBase || control is TreeView)
            {
                control.BackColor = palette.Input;
                control.ForeColor = palette.Text;
                ApplyInputChrome(control, palette);
                return;
            }

            if (control is Label || control is CheckBox || control is RadioButton)
            {
                // Labels inherit BackColor from their container; only text colour is set.
                control.ForeColor = palette.Text;
                return;
            }

            ActionButton action = control as ActionButton;
            if (action != null)
            {
                action.Invalidate();
                return;
            }

            Button button = control as Button;
            if (button != null)
            {
                StyleLegacyButton(button, palette);
                return;
            }

            if (control is SplitContainer)
            {
                control.BackColor = palette.Window;
                return;
            }

            if (control is SplitterPanel || control is TabPage)
            {
                control.BackColor = palette.Window;
                control.ForeColor = palette.Text;
            }
            ApplyInputChrome(control, palette);
        }

        private static void ApplyInputChrome(Control control, ThemePalette palette)
        {
            ComboBox combo = control as ComboBox;
            if (combo != null)
                combo.FlatStyle = FlatStyle.Flat;

            if (control is Form)
                return;
            if (control is TextBoxBase || control is ListBox || control is ListView ||
                control is TreeView || control is ScrollableControl)
            {
                ApplyNativeScrollbars(control);
            }
        }

        /// <summary>
        /// Styles plain WinForms buttons that have not been migrated to
        /// <see cref="ActionButton"/>.
        /// </summary>
        public static void StyleLegacyButton(Button button, ThemePalette palette)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.BackColor = palette.SurfaceAlt;
            button.ForeColor = palette.Text;
            button.FlatAppearance.BorderColor = palette.BorderStrong;
            button.FlatAppearance.MouseOverBackColor = palette.SurfaceHover;
            button.FlatAppearance.MouseDownBackColor = palette.Border;
        }

        // ------------------------------------------------------------ native

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmUseImmersiveDarkModeLegacy = 19;
        private const uint SwpFrameChanged = 0x0020 | 0x0001 | 0x0002 | 0x0004 | 0x0010;

        private static readonly Dictionary<Control, bool> PendingScrollbarThemes = new Dictionary<Control, bool>();

        /// <summary>
        /// Uses the Windows 10 dark scrollbar theme on dark palettes. Older
        /// Windows builds ignore the unknown theme name and keep the default.
        /// </summary>
        public static void ApplyNativeScrollbars(Control control)
        {
            if (control == null)
                return;
            if (!control.IsHandleCreated)
            {
                if (PendingScrollbarThemes.ContainsKey(control))
                    return;
                PendingScrollbarThemes[control] = true;
                control.HandleCreated += delegate
                {
                    PendingScrollbarThemes.Remove(control);
                    SetScrollbarTheme(control);
                };
                control.Disposed += delegate { PendingScrollbarThemes.Remove(control); };
                return;
            }
            SetScrollbarTheme(control);
        }

        private static void SetScrollbarTheme(Control control)
        {
            try
            {
                SetWindowTheme(control.Handle, _current.IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        /// <summary>
        /// Matches the window title bar to the palette on Windows 10 1809+.
        /// </summary>
        public static void ApplyTitleBar(Form form)
        {
            if (form == null || !form.IsHandleCreated)
                return;
            try
            {
                int value = _current.IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkMode, ref value, 4) != 0)
                    DwmSetWindowAttribute(form.Handle, DwmUseImmersiveDarkModeLegacy, ref value, 4);
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpFrameChanged);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        // ------------------------------------------------------------ drawing

        public static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int diameter = Math.Max(1, radius * 2);
            if (radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Paints the 1px card outline used by grouped content.
        /// </summary>
        public static void DrawOutline(Graphics graphics, Rectangle bounds, Color color)
        {
            using (Pen pen = new Pen(color))
                graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
    }
}
