using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Check box painted from the palette: a sunken box that fills with the
    /// rust accent when ticked, and text that stays legible when disabled.
    /// The system glyph is a bright white square on the dark palette, and
    /// WinForms darkens disabled text until it disappears.
    /// </summary>
    public class ThemedCheckBox : CheckBox
    {
        private const int BoxSize = 13;
        private const int TextGap = 4;
        private bool _hover;

        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            // Room for the slightly wider gap between the painted box and the text.
            Size size = base.GetPreferredSize(proposedSize);
            if (AutoSize)
                size.Width += 2;
            return size;
        }

        protected override void OnMouseEnter(EventArgs eventargs)
        {
            base.OnMouseEnter(eventargs);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs eventargs)
        {
            base.OnMouseLeave(eventargs);
            _hover = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            ThemePalette palette = ManagerTheme.Current;
            Color back = BackColor;
            if (back.A < 255 && Parent != null)
                back = Parent.BackColor;
            using (SolidBrush fill = new SolidBrush(back))
                g.FillRectangle(fill, ClientRectangle);

            Rectangle box = new Rectangle(0, (Height - BoxSize) / 2, BoxSize - 1, BoxSize - 1);
            bool ticked = CheckState != CheckState.Unchecked;
            Color boxFill;
            Color boxBorder;
            Color mark;
            if (!Enabled)
            {
                boxFill = palette.Surface;
                boxBorder = palette.Border;
                mark = palette.TextDisabled;
            }
            else if (ticked)
            {
                boxFill = _hover ? palette.AccentHover : palette.Accent;
                boxBorder = Focused ? palette.AccentText : palette.AccentPressed;
                mark = palette.OnAccent;
            }
            else
            {
                boxFill = palette.Input;
                boxBorder = _hover || Focused ? palette.AccentText : palette.BorderStrong;
                mark = palette.OnAccent;
            }
            using (SolidBrush fill = new SolidBrush(boxFill))
                g.FillRectangle(fill, box);
            using (Pen border = new Pen(boxBorder))
                g.DrawRectangle(border, box);

            if (CheckState == CheckState.Checked)
            {
                SmoothingMode previous = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen tick = new Pen(mark, 1.8f))
                {
                    tick.StartCap = LineCap.Round;
                    tick.EndCap = LineCap.Round;
                    tick.LineJoin = LineJoin.Round;
                    g.DrawLines(tick, new PointF[]
                    {
                        new PointF(box.Left + 3f, box.Top + 6.5f),
                        new PointF(box.Left + 5.5f, box.Top + 9f),
                        new PointF(box.Left + 9.5f, box.Top + 3.5f)
                    });
                }
                g.SmoothingMode = previous;
            }
            else if (CheckState == CheckState.Indeterminate)
            {
                using (SolidBrush dash = new SolidBrush(mark))
                    g.FillRectangle(dash, box.Left + 3, box.Top + 5, box.Width - 5, 2);
            }

            Rectangle textBounds = new Rectangle(BoxSize + TextGap, 0, Math.Max(0, Width - BoxSize - TextGap), Height);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak;
            if (!ShowKeyboardCues)
                flags |= TextFormatFlags.HidePrefix;
            Color text = Enabled ? ForeColor : palette.TextDisabled;
            TextRenderer.DrawText(g, Text, Font, textBounds, text, flags);

            if (Focused && ShowFocusCues && Text.Length > 0)
            {
                Size measured = TextRenderer.MeasureText(g, Text, Font, textBounds.Size, flags);
                Rectangle focus = new Rectangle(
                    textBounds.Left - 2,
                    Math.Max(0, (Height - measured.Height) / 2 - 1),
                    Math.Min(textBounds.Width + 1, measured.Width + 3),
                    Math.Min(Height - 1, measured.Height + 1));
                using (Pen pen = new Pen(palette.AccentText))
                {
                    pen.DashStyle = DashStyle.Dot;
                    g.DrawRectangle(pen, focus);
                }
            }
        }
    }
}
