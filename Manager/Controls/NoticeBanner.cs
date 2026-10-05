using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Manager.Controls
{
    public enum NoticeKind
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Inline, dismissible status message. Kind is conveyed by a glyph and
    /// the message text, not by colour alone.
    /// </summary>
    public class NoticeBanner : TableLayoutPanel
    {
        private const int GlyphSize = 18;
        private readonly Label _message;
        private readonly LinkLabel _action;
        private readonly ActionButton _dismiss;
        private NoticeKind _kind = NoticeKind.Info;
        private EventHandler _actionHandler;

        /// <summary>Raised when the user closes the banner with Dismiss.</summary>
        public event EventHandler Dismissed;

        public NoticeBanner()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ColumnCount = 3;
            RowCount = 1;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Padding = new Padding(GlyphSize + 22, 6, 6, 6);
            Margin = new Padding(0);
            AccessibleRole = AccessibleRole.Alert;

            _message = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 5, 12, 5),
                Font = ManagerTheme.UiFont(9f),
                UseMnemonic = false
            };
            _action = new LinkLabel
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 5, 12, 5),
                Font = ManagerTheme.Semibold(9f),
                Visible = false,
                TabStop = true
            };
            _action.LinkClicked += delegate
            {
                if (_actionHandler != null)
                    _actionHandler(this, EventArgs.Empty);
            };
            _dismiss = new ActionButton
            {
                Kind = ActionButtonKind.Quiet,
                Text = "Dismiss",
                AutoSize = true,
                Margin = new Padding(0),
                Anchor = AnchorStyles.Right,
                AccessibleName = "Dismiss message"
            };
            _dismiss.Click += delegate
            {
                Clear();
                if (Dismissed != null)
                    Dismissed(this, EventArgs.Empty);
            };

            Controls.Add(_message, 0, 0);
            Controls.Add(_action, 1, 0);
            Controls.Add(_dismiss, 2, 0);
            Visible = false;
        }

        public NoticeKind Kind
        {
            get { return _kind; }
        }

        public string Message
        {
            get { return _message.Text; }
        }

        public void Show(NoticeKind kind, string message)
        {
            Show(kind, message, null, null);
        }

        public void Show(NoticeKind kind, string message, string actionText, EventHandler action)
        {
            _kind = kind;
            _message.Text = message ?? string.Empty;
            _actionHandler = action;
            _action.Text = actionText ?? string.Empty;
            _action.Visible = !string.IsNullOrEmpty(actionText) && action != null;
            AccessibleName = KindWord(kind) + ": " + _message.Text;
            ApplyColors();
            Visible = true;
            AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        }

        public void Clear()
        {
            _actionHandler = null;
            Visible = false;
        }

        public void ApplyTheme()
        {
            ApplyColors();
        }

        private void ApplyColors()
        {
            ThemePalette palette = ManagerTheme.Current;
            BackColor = BackFor(palette, _kind);
            _message.ForeColor = palette.Text;
            _action.LinkColor = palette.AccentText;
            _action.ActiveLinkColor = palette.AccentText;
            _action.VisitedLinkColor = palette.AccentText;
            _dismiss.Invalidate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ThemePalette palette = ManagerTheme.Current;
            Color accent = AccentFor(palette, _kind);
            using (SolidBrush stripe = new SolidBrush(accent))
                e.Graphics.FillRectangle(stripe, 0, 0, 4, Height);

            Rectangle glyph = new Rectangle(14, (Height - GlyphSize) / 2, GlyphSize, GlyphSize);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush circle = new SolidBrush(accent))
                e.Graphics.FillEllipse(circle, glyph);
            Color mark = palette.IsDark ? palette.Chrome : Color.White;
            if (_kind == NoticeKind.Success)
            {
                using (Pen pen = new Pen(mark, 2f))
                {
                    e.Graphics.DrawLines(pen, new Point[]
                    {
                        new Point(glyph.X + 5, glyph.Y + 9),
                        new Point(glyph.X + 8, glyph.Y + 12),
                        new Point(glyph.X + 13, glyph.Y + 6)
                    });
                }
            }
            else
            {
                string symbol = _kind == NoticeKind.Info ? "i" : "!";
                TextRenderer.DrawText(e.Graphics, symbol, ManagerTheme.UiFont(9f, FontStyle.Bold), glyph, mark,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            e.Graphics.SmoothingMode = SmoothingMode.Default;
        }

        private static string KindWord(NoticeKind kind)
        {
            switch (kind)
            {
                case NoticeKind.Success: return "Success";
                case NoticeKind.Warning: return "Warning";
                case NoticeKind.Error: return "Error";
                default: return "Information";
            }
        }

        private static Color BackFor(ThemePalette palette, NoticeKind kind)
        {
            switch (kind)
            {
                case NoticeKind.Success: return palette.SuccessBack;
                case NoticeKind.Warning: return palette.WarningBack;
                case NoticeKind.Error: return palette.DangerBack;
                default: return palette.InfoBack;
            }
        }

        private static Color AccentFor(ThemePalette palette, NoticeKind kind)
        {
            switch (kind)
            {
                case NoticeKind.Success: return palette.Success;
                case NoticeKind.Warning: return palette.Warning;
                case NoticeKind.Error: return palette.Danger;
                default: return palette.AccentText;
            }
        }
    }
}
