using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// A ledger-style section: tan uppercase serif title over a thin rule, the
    /// way the game's own dialogs head their columns. Body controls stack
    /// beneath; the section grows to fit them unless <see cref="FillsHeight"/>
    /// hands the remaining height to the last row.
    /// </summary>
    public class CardPanel : TableLayoutPanel
    {
        private readonly Label _title;
        private readonly Label _description;
        private string _titleText;
        private bool _fillsHeight;

        public CardPanel(string title, string description)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ColumnCount = 1;
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16, 12, 16, 14);
            Margin = new Padding(0, 0, 0, 12);
            ManagerTheme.SetRole(this, ThemeRole.Card);

            _title = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Font = ManagerTheme.Display(9.5f),
                Margin = new Padding(0, 0, 0, 10),
                UseMnemonic = false
            };
            _description = new Label
            {
                Text = description ?? string.Empty,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Font = ManagerTheme.UiFont(9f),
                Margin = new Padding(0, 0, 0, 8),
                UseMnemonic = false,
                Visible = !string.IsNullOrEmpty(description)
            };
            Title = title;
            ManagerTheme.SetRole(_title, ThemeRole.Accent);
            ManagerTheme.SetRole(_description, ThemeRole.Muted);
            AddRow(_title, false);
            AddRow(_description, false);
        }

        public string Title
        {
            get { return _titleText; }
            set
            {
                _titleText = value ?? string.Empty;
                _title.Text = _titleText.ToUpperInvariant();
                _title.AccessibleName = _titleText;
                _title.Visible = _titleText.Length > 0;
            }
        }

        public string Description
        {
            get { return _description.Text; }
            set
            {
                _description.Text = value ?? string.Empty;
                _description.Visible = !string.IsNullOrEmpty(value);
            }
        }

        /// <summary>When true the card is docked/sized by its parent and its last row stretches.</summary>
        public bool FillsHeight
        {
            get { return _fillsHeight; }
            set
            {
                _fillsHeight = value;
                AutoSize = !value;
                UpdateRowStyles();
            }
        }

        public T AddBody<T>(T control) where T : Control
        {
            if (control.Dock == DockStyle.None && control.Anchor == (AnchorStyles.Top | AnchorStyles.Left))
                control.Dock = DockStyle.Fill;
            AddRow(control, true);
            return control;
        }

        private void AddRow(Control control, bool body)
        {
            int row = RowCount;
            RowCount = row + 1;
            RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(control, 0, row);
            if (body)
                UpdateRowStyles();
        }

        private void UpdateRowStyles()
        {
            for (int i = 0; i < RowStyles.Count; i++)
            {
                bool last = i == RowStyles.Count - 1 && i >= 2;
                RowStyles[i] = last && _fillsHeight
                    ? new RowStyle(SizeType.Percent, 100f)
                    : new RowStyle(SizeType.AutoSize);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ThemePalette palette = ManagerTheme.Current;
            ManagerTheme.DrawOutline(e.Graphics, ClientRectangle, palette.Border);
            if (_title.Visible)
            {
                int y = _title.Bottom + 4;
                using (Pen rule = new Pen(palette.Border))
                    e.Graphics.DrawLine(rule, Padding.Left, y, Width - Padding.Right, y);
            }
        }
    }
}
