using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Builders for the small set of control shapes the manager views share,
    /// so labels, fields, and buttons look and behave the same everywhere.
    /// </summary>
    public static class UiFactory
    {
        public static Label Text(string text)
        {
            return Text(text, ThemeRole.Default, 9f, false);
        }

        public static Label Text(string text, ThemeRole role, float size, bool semibold)
        {
            Label label = new Label
            {
                Text = text ?? string.Empty,
                AutoSize = true,
                Font = semibold ? ManagerTheme.Semibold(size) : ManagerTheme.UiFont(size),
                Margin = new Padding(0, 0, 0, 4),
                UseMnemonic = false
            };
            if (role != ThemeRole.Default)
                ManagerTheme.SetRole(label, role);
            return label;
        }

        /// <summary>Muted helper text that wraps inside table layouts.</summary>
        public static Label Help(string text)
        {
            Label label = Text(text, ThemeRole.Muted, 8.75f, false);
            label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            label.Margin = new Padding(0, 2, 0, 8);
            return label;
        }

        public static Label Heading(string text, float size)
        {
            Label label = Text(text, ThemeRole.Heading, size, true);
            label.Font = ManagerTheme.Display(size);
            label.Margin = new Padding(0, 0, 0, 2);
            return label;
        }

        public static ActionButton Button(string text, ActionButtonKind kind, EventHandler click)
        {
            ActionButton button = new ActionButton
            {
                Text = text,
                Kind = kind,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 8, 0)
            };
            if (click != null)
                button.Click += click;
            return button;
        }

        /// <summary>A left-aligned row of buttons that wraps instead of clipping.</summary>
        public static FlowLayoutPanel ButtonRow(params Control[] buttons)
        {
            FlowLayoutPanel row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };
            for (int i = 0; i < buttons.Length; i++)
            {
                Control button = buttons[i];
                button.Margin = new Padding(0, 0, 8, 6);
                row.Controls.Add(button);
            }
            return row;
        }

        /// <summary>Two-column label/editor grid that grows with its rows.</summary>
        public static TableLayoutPanel FormGrid()
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return grid;
        }

        /// <summary>
        /// Adds a labelled editor row. The label's text becomes the editor's
        /// accessible name, and the label precedes it in tab order so its
        /// mnemonic moves focus to the editor.
        /// </summary>
        public static T AddField<T>(TableLayoutPanel grid, string label, T editor, string help) where T : Control
        {
            int row = grid.RowCount;
            grid.RowCount = row + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label caption = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Margin = new Padding(0, 7, 16, 4),
                Font = ManagerTheme.UiFont(9f)
            };
            if (editor.Margin == new Padding(3))
                editor.Margin = new Padding(0, 3, 0, 3);
            if (string.IsNullOrEmpty(editor.AccessibleName))
                editor.AccessibleName = StripMnemonic(label);
            TextBoxBase text = editor as TextBoxBase;
            grid.Controls.Add(caption, 0, row);
            grid.Controls.Add(text != null ? Framed(text) : editor, 1, row);

            if (!string.IsNullOrEmpty(help))
            {
                grid.RowCount = row + 2;
                grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                Label helpLabel = Help(help);
                helpLabel.Margin = new Padding(0, 0, 0, 6);
                grid.Controls.Add(helpLabel, 1, row + 1);
                if (string.IsNullOrEmpty(editor.AccessibleDescription))
                    editor.AccessibleDescription = help;
            }
            return editor;
        }

        public static TextBox TextField()
        {
            return new ThemedTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = ManagerTheme.UiFont(9.5f)
            };
        }

        /// <summary>
        /// Puts a text box inside an <see cref="InputFrame"/> and moves its
        /// layout settings to the frame. Add the returned control to the
        /// layout; keep using the text box itself for text and events.
        /// </summary>
        public static Control Framed(TextBoxBase box)
        {
            InputFrame existing = box.Parent as InputFrame;
            if (existing != null)
                return existing;
            DockStyle dock = box.Dock;
            AnchorStyles anchor = box.Anchor;
            Padding margin = box.Margin;
            int width = box.Width;
            InputFrame frame = new InputFrame(box);
            frame.Margin = margin;
            frame.Width = width;
            frame.Location = box.Location;
            if (dock != DockStyle.None)
                frame.Dock = dock;
            else
                frame.Anchor = anchor;
            return frame;
        }

        public static TextBox MultilineField(int lines)
        {
            TextBox box = TextField();
            box.Multiline = true;
            box.AcceptsReturn = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.WordWrap = true;
            box.Height = Math.Max(2, lines) * 18 + 8;
            return box;
        }

        public static NumericUpDown NumberField(decimal minimum, decimal maximum, int decimals)
        {
            return new ThemedNumericUpDown
            {
                Anchor = AnchorStyles.Left,
                Width = 140,
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimals,
                BorderStyle = BorderStyle.FixedSingle,
                Font = ManagerTheme.UiFont(9.5f),
                ThousandsSeparator = false
            };
        }

        public static ComboBox ChoiceField(string[] choices)
        {
            ComboBox box = new ThemedComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                Font = ManagerTheme.UiFont(9.5f),
                MaxDropDownItems = 18
            };
            if (choices != null)
                box.Items.AddRange(choices);
            return box;
        }

        public static string StripMnemonic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace("&&", "\u0001").Replace("&", string.Empty).Replace("\u0001", "&").TrimEnd(':');
        }
    }
}
