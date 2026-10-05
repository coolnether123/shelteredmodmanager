using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    public enum TabStripStyle
    {
        /// <summary>Top-level navigation drawn on the window chrome.</summary>
        Header,
        /// <summary>In-page section navigation drawn on the page background.</summary>
        Section
    }

    /// <summary>
    /// Native tab control painted from the manager palette. The native control
    /// still owns tab sizing, keyboard navigation (Ctrl+Tab, arrows), and the
    /// accessibility tree; only drawing and the page inset change.
    /// </summary>
    public class ThemedTabControl : TabControl
    {
        private const int UnderlineHeight = 3;
        private TabStripStyle _stripStyle = TabStripStyle.Header;
        private int _hoverIndex = -1;

        public ThemedTabControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            SizeMode = TabSizeMode.Normal;
        }

        public TabStripStyle StripStyle
        {
            get { return _stripStyle; }
            set
            {
                _stripStyle = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Pages fill the control edge to edge below the strip instead of
        /// sitting inside the native 3D border.
        /// </summary>
        public override Rectangle DisplayRectangle
        {
            get
            {
                int top = StripHeight;
                return new Rectangle(0, top, Math.Max(0, Width), Math.Max(0, Height - top));
            }
        }

        private int StripHeight
        {
            get
            {
                if (TabCount == 0 || !IsHandleCreated)
                    return ItemSize.Height + 4;
                Rectangle first = GetTabRect(0);
                return first.Bottom + 2;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette palette = ManagerTheme.Current;
            Color stripBack = _stripStyle == TabStripStyle.Header ? palette.Chrome : palette.Window;
            int stripHeight = StripHeight;

            using (SolidBrush back = new SolidBrush(stripBack))
                e.Graphics.FillRectangle(back, 0, 0, Width, stripHeight);
            using (SolidBrush page = new SolidBrush(palette.Window))
                e.Graphics.FillRectangle(page, 0, stripHeight, Width, Math.Max(0, Height - stripHeight));
            using (Pen line = new Pen(palette.Border))
                e.Graphics.DrawLine(line, 0, stripHeight - 1, Width, stripHeight - 1);

            for (int i = 0; i < TabCount; i++)
                PaintTab(e.Graphics, i, palette, stripHeight);
        }

        private void PaintTab(Graphics graphics, int index, ThemePalette palette, int stripHeight)
        {
            Rectangle bounds = GetTabRect(index);
            bounds = new Rectangle(bounds.X, 0, bounds.Width, stripHeight - 1);
            bool selected = index == SelectedIndex;
            bool hover = index == _hoverIndex && !selected;

            if (hover)
            {
                using (SolidBrush brush = new SolidBrush(palette.SurfaceAlt))
                    graphics.FillRectangle(brush, bounds.X, bounds.Y + 4, bounds.Width, bounds.Height - 4);
            }

            Color textColor = selected ? palette.Text : (hover ? palette.Text : palette.TextMuted);
            if (!Enabled)
                textColor = palette.TextDisabled;
            TabPage page = TabPages[index];
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            Rectangle textBounds = new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, bounds.Height - UnderlineHeight);
            TextRenderer.DrawText(graphics, page.Text, Font, textBounds, textColor, flags);

            if (selected)
            {
                using (SolidBrush underline = new SolidBrush(palette.AccentText))
                {
                    graphics.FillRectangle(
                        underline,
                        bounds.X + 6,
                        stripHeight - 1 - UnderlineHeight,
                        Math.Max(0, bounds.Width - 12),
                        UnderlineHeight);
                }
            }

            if (selected && Focused && ShowFocusCues)
            {
                Rectangle ring = new Rectangle(bounds.X + 2, bounds.Y + 4, bounds.Width - 4, bounds.Height - 6);
                using (Pen pen = new Pen(palette.AccentText, 2f))
                    graphics.DrawRectangle(pen, ring);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hover = -1;
            for (int i = 0; i < TabCount; i++)
            {
                if (GetTabRect(i).Contains(e.Location))
                {
                    hover = i;
                    break;
                }
            }
            if (hover != _hoverIndex)
            {
                _hoverIndex = hover;
                Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate(new Rectangle(0, 0, Width, StripHeight));
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Cursor = Cursors.Default;
                Invalidate(new Rectangle(0, 0, Width, StripHeight));
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate(new Rectangle(0, 0, Width, StripHeight));
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate(new Rectangle(0, 0, Width, StripHeight));
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            TabPage page = e.Control as TabPage;
            if (page != null)
                page.TextChanged += delegate { Invalidate(); };
        }
    }
}
