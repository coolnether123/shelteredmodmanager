using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Manager.Controls
{
    public enum ActionButtonKind
    {
        /// <summary>Neutral action with a visible outline.</summary>
        Secondary,
        /// <summary>The one action a surface is built around.</summary>
        Primary,
        /// <summary>Low-emphasis action that only shows a fill on hover.</summary>
        Quiet,
        /// <summary>Destructive or irreversible action.</summary>
        Danger
    }

    /// <summary>
    /// Palette-painted button used across the manager. Disabled buttons stay
    /// legible and visibly inactive, and keyboard focus draws a ring.
    /// </summary>
    public class ActionButton : Button
    {
        private const int Radius = 2;
        private ActionButtonKind _kind = ActionButtonKind.Secondary;
        private bool _toggled;
        private bool _isToggle;
        private bool _hovered;
        private bool _pressed;

        public ActionButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Font = ManagerTheme.Semibold(9f);
            Cursor = Cursors.Hand;
            Height = 32;
            Padding = new Padding(10, 0, 10, 0);
        }

        public ActionButtonKind Kind
        {
            get { return _kind; }
            set
            {
                _kind = value;
                Invalidate();
            }
        }

        /// <summary>Compatibility shim for callers written against the old primary flag.</summary>
        public bool IsPrimary
        {
            get { return _kind == ActionButtonKind.Primary; }
            set { Kind = value ? ActionButtonKind.Primary : ActionButtonKind.Secondary; }
        }

        /// <summary>Marks this button as a two-state toggle (tools, view switches).</summary>
        public bool IsToggle
        {
            get { return _isToggle; }
            set
            {
                _isToggle = value;
                AccessibleRole = value ? AccessibleRole.CheckButton : AccessibleRole.Default;
                Invalidate();
            }
        }

        public bool Toggled
        {
            get { return _toggled; }
            set
            {
                if (_toggled == value)
                    return;
                _toggled = value;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            }
        }

        /// <summary>Kept for existing callers; colours now come from <see cref="ManagerTheme"/>.</summary>
        public void ApplyTheme(bool isDark)
        {
            Invalidate();
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size text = TextRenderer.MeasureText(Text ?? string.Empty, Font, Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            int width = text.Width + Padding.Horizontal + 16;
            int height = Math.Max(32, text.Height + 14);
            return new Size(Math.Max(width, MinimumSize.Width), Math.Max(height, MinimumSize.Height));
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            if (mevent.Button == MouseButtons.Left)
            {
                _pressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _pressed = false;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled)
            {
                _hovered = false;
                _pressed = false;
            }
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            ThemePalette palette = ManagerTheme.Current;
            Graphics g = pevent.Graphics;
            Color parentBack = Parent != null ? Parent.BackColor : palette.Window;
            g.Clear(parentBack);

            Color fill;
            Color border;
            Color text;
            ResolveColors(palette, parentBack, out fill, out border, out text);

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = ManagerTheme.RoundedRectangle(bounds, Radius))
            {
                if (fill.A > 0)
                {
                    using (SolidBrush brush = new SolidBrush(fill))
                        g.FillPath(brush, path);
                }
                if (border.A > 0)
                {
                    using (Pen pen = new Pen(border))
                        g.DrawPath(pen, path);
                }
            }

            // A one-pixel bevel echoes the raised buttons in the game.
            if (Enabled && _kind != ActionButtonKind.Quiet && !(_isToggle && _toggled))
            {
                g.SmoothingMode = SmoothingMode.Default;
                using (Pen light = new Pen(Color.FromArgb(palette.IsDark ? 34 : 110, 255, 255, 255)))
                    g.DrawLine(light, 2, 1, Width - 3, 1);
                using (Pen shade = new Pen(Color.FromArgb(palette.IsDark ? 70 : 28, 0, 0, 0)))
                    g.DrawLine(shade, 2, Height - 2, Width - 3, Height - 2);
                g.SmoothingMode = SmoothingMode.AntiAlias;
            }

            if (Focused && ShowFocusCues && Enabled)
            {
                Color ring = _kind == ActionButtonKind.Primary || _kind == ActionButtonKind.Danger
                    ? palette.OnAccent
                    : palette.AccentText;
                Rectangle inner = new Rectangle(2, 2, Width - 5, Height - 5);
                using (GraphicsPath ringPath = ManagerTheme.RoundedRectangle(inner, Radius - 1))
                using (Pen pen = new Pen(ring, 2f))
                    g.DrawPath(pen, ringPath);
            }
            g.SmoothingMode = SmoothingMode.Default;

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (!ShowKeyboardCues)
                flags |= TextFormatFlags.HidePrefix;
            Rectangle textBounds = new Rectangle(
                Padding.Left / 2, 0, Math.Max(0, Width - Padding.Horizontal / 2), Height);
            TextRenderer.DrawText(g, Text, Font, textBounds, text, flags);
        }

        private void ResolveColors(ThemePalette palette, Color parentBack, out Color fill, out Color border, out Color text)
        {
            if (!Enabled)
            {
                fill = _kind == ActionButtonKind.Quiet ? Color.Transparent : palette.SurfaceAlt;
                border = _kind == ActionButtonKind.Quiet ? Color.Transparent : palette.Border;
                text = palette.TextDisabled;
                return;
            }

            if (_isToggle && _toggled)
            {
                fill = palette.Selection;
                border = palette.AccentText;
                text = palette.SelectionText;
                return;
            }

            switch (_kind)
            {
                case ActionButtonKind.Primary:
                    fill = _pressed ? palette.AccentPressed : (_hovered ? palette.AccentHover : palette.Accent);
                    border = palette.AccentPressed;
                    text = palette.OnAccent;
                    break;
                case ActionButtonKind.Danger:
                    fill = _hovered || _pressed ? palette.DangerFillHover : palette.DangerFill;
                    border = fill;
                    text = palette.OnAccent;
                    break;
                case ActionButtonKind.Quiet:
                    fill = _pressed ? palette.SurfaceHover : (_hovered ? palette.SurfaceAlt : Color.Transparent);
                    border = _hovered ? palette.Border : Color.Transparent;
                    text = palette.Text;
                    break;
                default:
                    fill = _pressed ? palette.Border : (_hovered ? palette.SurfaceHover : palette.SurfaceAlt);
                    border = palette.BorderStrong;
                    text = palette.Text;
                    break;
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new ActionButtonAccessibleObject(this);
        }

        private sealed class ActionButtonAccessibleObject : ButtonBaseAccessibleObject
        {
            private readonly ActionButton _owner;

            public ActionButtonAccessibleObject(ActionButton owner)
                : base(owner)
            {
                _owner = owner;
            }

            public override AccessibleStates State
            {
                get
                {
                    AccessibleStates state = base.State;
                    if (_owner.IsToggle && _owner.Toggled)
                        state |= AccessibleStates.Checked;
                    return state;
                }
            }
        }
    }
}
