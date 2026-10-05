using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Manager.Controls
{
    internal static class InputChrome
    {
        public static Color BorderColor(Control control)
        {
            ThemePalette palette = ManagerTheme.Current;
            if (!control.Enabled)
                return palette.Border;
            return control.ContainsFocus ? palette.AccentText : palette.BorderStrong;
        }

        public static void DrawChevron(Graphics g, Rectangle bounds, bool up, Color color)
        {
            int cx = bounds.Left + bounds.Width / 2;
            int cy = bounds.Top + bounds.Height / 2;
            Point[] points = up
                ? new Point[] { new Point(cx - 3, cy + 1), new Point(cx, cy - 2), new Point(cx + 3, cy + 1) }
                : new Point[] { new Point(cx - 3, cy - 1), new Point(cx, cy + 2), new Point(cx + 3, cy - 1) };
            using (Pen pen = new Pen(color, 1.6f))
                g.DrawLines(pen, points);
        }
    }

    /// <summary>
    /// Text box whose one-pixel frame uses the palette and turns to the
    /// accent colour while focused, instead of the system's light border.
    /// </summary>
    public class ThemedTextBox : TextBox
    {
        private const int WmNcPaint = 0x0085;
        private const int WmPrint = 0x0317;
        private const long PrfNonClient = 0x2;

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        public ThemedTextBox()
        {
            BorderStyle = BorderStyle.FixedSingle;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (BorderStyle != BorderStyle.FixedSingle)
                return;
            if (m.Msg == WmNcPaint)
            {
                PaintFrame();
            }
            else if (m.Msg == WmPrint && (m.LParam.ToInt64() & PrfNonClient) != 0 && m.WParam != IntPtr.Zero)
            {
                // Printed renders (window capture, some redraw paths) skip WM_NCPAINT.
                using (Graphics g = Graphics.FromHdc(m.WParam))
                    DrawFrame(g);
            }
        }

        private void DrawFrame(Graphics g)
        {
            using (Pen pen = new Pen(InputChrome.BorderColor(this)))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            PaintFrame();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            PaintFrame();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            PaintFrame();
        }

        private void PaintFrame()
        {
            if (!IsHandleCreated || BorderStyle != BorderStyle.FixedSingle)
                return;
            IntPtr dc = GetWindowDC(Handle);
            if (dc == IntPtr.Zero)
                return;
            try
            {
                using (Graphics g = Graphics.FromHdc(dc))
                    DrawFrame(g);
            }
            finally
            {
                ReleaseDC(Handle, dc);
            }
        }
    }

    /// <summary>
    /// Padded frame around a borderless text box. Visual styles draw text box
    /// borders themselves, so the palette border and focus colour live here.
    /// </summary>
    public class InputFrame : Panel
    {
        private readonly TextBoxBase _inner;
        private bool _syncingBack;

        public InputFrame(TextBoxBase inner)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            _inner = inner;
            bool multiline = inner.Multiline;
            inner.BorderStyle = BorderStyle.None;
            inner.Margin = new Padding(0);
            inner.Dock = DockStyle.Fill;
            Padding = multiline ? new Padding(6, 4, 1, 4) : new Padding(6, 5, 6, 3);
            Height = (multiline ? inner.Height : inner.PreferredHeight) + Padding.Vertical;
            MinimumSize = new Size(0, Height);
            Cursor = Cursors.IBeam;
            Controls.Add(inner);
            inner.GotFocus += delegate { Invalidate(); };
            inner.LostFocus += delegate { Invalidate(); };
            inner.EnabledChanged += delegate { SyncBackColor(); };
            inner.ReadOnlyChanged += delegate { SyncBackColor(); };
            inner.BackColorChanged += delegate { SyncBackColor(); };
            Click += delegate { _inner.Focus(); };
            SyncBackColor();
        }

        public TextBoxBase Inner
        {
            get { return _inner; }
        }

        /// <summary>
        /// Read-only fields (paths the manager fills in) sit on a raised plate
        /// and disabled ones sink into the card, so neither invites typing.
        /// </summary>
        private void SyncBackColor()
        {
            if (_syncingBack)
                return;
            _syncingBack = true;
            try
            {
                ThemePalette palette = ManagerTheme.Current;
                if (!_inner.Enabled)
                    _inner.BackColor = palette.Surface;
                else if (_inner.ReadOnly)
                    _inner.BackColor = palette.SurfaceAlt;
                else if (_inner.BackColor == palette.Surface || _inner.BackColor == palette.SurfaceAlt)
                    _inner.BackColor = palette.Input;
                BackColor = _inner.BackColor;
                Invalidate();
            }
            finally
            {
                _syncingBack = false;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (SolidBrush fill = new SolidBrush(_inner.BackColor))
                e.Graphics.FillRectangle(fill, ClientRectangle);
            Color border = !_inner.Enabled || (_inner.ReadOnly && !_inner.Focused)
                ? ManagerTheme.Current.Border
                : (_inner.Focused ? ManagerTheme.Current.AccentText : ManagerTheme.Current.BorderStrong);
            using (Pen pen = new Pen(border))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (_inner.Focused)
            {
                // A second inner line makes focus visible at a glance.
                using (Pen inner = new Pen(Color.FromArgb(110, border)))
                    e.Graphics.DrawRectangle(inner, 1, 1, Width - 3, Height - 3);
            }
        }
    }

    /// <summary>
    /// Flat combo box with a palette frame and drop button, so the system's
    /// white arrow button never appears on the dark palette.
    /// </summary>
    public class ThemedComboBox : ComboBox
    {
        private const int WmPaint = 0x000F;

        public ThemedComboBox()
        {
            FlatStyle = FlatStyle.Flat;
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WmPaint)
                PaintChrome();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            ClearIdleSelection();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClearIdleSelection();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            ClearIdleSelection();
        }

        /// <summary>
        /// Editable combos select their whole text when resized or filled in
        /// code, which paints a system-blue block on an unfocused field.
        /// </summary>
        private void ClearIdleSelection()
        {
            if (!IsHandleCreated || Focused || DropDownStyle == ComboBoxStyle.DropDownList)
                return;
            if (SelectionLength > 0)
                Select(Text.Length, 0);
        }

        private void PaintChrome()
        {
            if (!IsHandleCreated)
                return;
            ThemePalette palette = ManagerTheme.Current;
            int buttonWidth = SystemInformation.HorizontalScrollBarArrowWidth + 2;
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                Rectangle button = new Rectangle(Width - buttonWidth - 1, 1, buttonWidth, Height - 2);
                using (SolidBrush fill = new SolidBrush(Enabled ? palette.SurfaceAlt : palette.Surface))
                    g.FillRectangle(fill, button);
                using (Pen divider = new Pen(palette.Border))
                    g.DrawLine(divider, button.Left, button.Top, button.Left, button.Bottom - 1);
                InputChrome.DrawChevron(g, button, false, Enabled ? palette.TextMuted : palette.TextDisabled);
                using (Pen pen = new Pen(InputChrome.BorderColor(this)))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    /// <summary>
    /// Numeric field with a palette frame and themed spin buttons.
    /// </summary>
    public class ThemedNumericUpDown : NumericUpDown
    {
        public ThemedNumericUpDown()
        {
            BorderStyle = BorderStyle.FixedSingle;
            if (Controls.Count > 0)
                Controls[0].Paint += PaintSpinButtons;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(InputChrome.BorderColor(this)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            Invalidate();
        }

        private void PaintSpinButtons(object sender, PaintEventArgs e)
        {
            Control buttons = (Control)sender;
            ThemePalette palette = ManagerTheme.Current;
            Rectangle bounds = buttons.ClientRectangle;
            using (SolidBrush fill = new SolidBrush(Enabled ? palette.SurfaceAlt : palette.Surface))
                e.Graphics.FillRectangle(fill, bounds);
            using (Pen divider = new Pen(palette.Border))
            {
                e.Graphics.DrawLine(divider, 0, 0, 0, bounds.Bottom);
                e.Graphics.DrawLine(divider, 0, bounds.Height / 2, bounds.Right, bounds.Height / 2);
            }
            Color arrow = Enabled ? palette.TextMuted : palette.TextDisabled;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            InputChrome.DrawChevron(e.Graphics, new Rectangle(0, 0, bounds.Width, bounds.Height / 2), true, arrow);
            InputChrome.DrawChevron(e.Graphics, new Rectangle(0, bounds.Height / 2, bounds.Width, bounds.Height - bounds.Height / 2), false, arrow);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        }
    }

    /// <summary>
    /// Link label that paints its disabled state as plain dimmed text. WinForms
    /// draws disabled links etched, which doubles the text on dark surfaces.
    /// </summary>
    public class ThemedLinkLabel : LinkLabel
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled)
            {
                base.OnPaint(e);
                return;
            }
            Color back = BackColor.A < 255 && Parent != null ? Parent.BackColor : BackColor;
            using (SolidBrush fill = new SolidBrush(back))
                e.Graphics.FillRectangle(fill, ClientRectangle);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            Rectangle bounds = new Rectangle(Padding.Left, Padding.Top, Width - Padding.Horizontal, Height - Padding.Vertical);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, ManagerTheme.Current.TextDisabled, flags);
        }
    }
}
