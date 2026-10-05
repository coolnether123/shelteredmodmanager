using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Themed replacement for message boxes that need more than a sentence:
    /// a heading, a message, an optional list, check box or text field, and
    /// buttons. Build it, then call <see cref="Form.ShowDialog(IWin32Window)"/>.
    /// </summary>
    public sealed class ManagerDialog : Form
    {
        private readonly TableLayoutPanel _body;
        private readonly FlowLayoutPanel _buttons;
        private bool _hasPrimary;

        public ManagerDialog(string windowTitle, string heading, string message)
        {
            Text = windowTitle ?? "Sheltered Mod Manager";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Font = ManagerTheme.UiFont(9f);
            Padding = new Padding(0);
            KeyPreview = true;

            _body = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(22, 18, 22, 16),
                Margin = new Padding(0),
                Dock = DockStyle.Fill,
                MaximumSize = new Size(560, 0)
            };
            _body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 516f));

            if (!string.IsNullOrEmpty(heading))
            {
                Label title = UiFactory.Heading(heading, 13f);
                title.MaximumSize = new Size(516, 0);
                title.Margin = new Padding(0, 0, 0, 8);
                AddRow(title);
            }
            if (!string.IsNullOrEmpty(message))
            {
                Label text = UiFactory.Text(message, ThemeRole.Default, 9.5f, false);
                text.MaximumSize = new Size(516, 0);
                text.Margin = new Padding(0, 0, 0, 12);
                AddRow(text);
            }

            _buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                WrapContents = false,
                Margin = new Padding(0, 6, 0, 0),
                Padding = new Padding(0)
            };
            ManagerTheme.SetRole(this, ThemeRole.Window);
            Controls.Add(_body);
            HandleCreated += delegate { ManagerTheme.ApplyTitleBar(this); };
        }

        /// <summary>A read-only list, one entry per line, that scrolls when long.</summary>
        public TextBox AddLines(IList<string> lines)
        {
            // Entries may span several lines (a name, then a detail line).
            int visualLines = 0;
            if (lines != null)
                foreach (string line in lines)
                    visualLines += 1 + (line ?? string.Empty).Split('\n').Length - 1;
            TextBox box = UiFactory.MultilineField(Math.Max(2, Math.Min(10, visualLines)));
            box.ReadOnly = true;
            box.WordWrap = true;
            box.Text = lines == null ? string.Empty : string.Join(Environment.NewLine, ToArray(lines));
            box.AccessibleName = "Details";
            Control frame = UiFactory.Framed(box);
            frame.Dock = DockStyle.None;
            frame.Width = 516;
            frame.Margin = new Padding(0, 0, 0, 12);
            AddRow(frame);
            return box;
        }

        public CheckBox AddCheck(string text, bool isChecked)
        {
            ThemedCheckBox check = new ThemedCheckBox
            {
                Text = text,
                Checked = isChecked,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            AddRow(check);
            return check;
        }

        public TextBox AddTextInput(string label, string value)
        {
            Label caption = UiFactory.Text(label, ThemeRole.Default, 9f, false);
            caption.Margin = new Padding(0, 0, 0, 4);
            AddRow(caption);
            TextBox box = UiFactory.TextField();
            box.Text = value ?? string.Empty;
            box.AccessibleName = UiFactory.StripMnemonic(label);
            Control frame = UiFactory.Framed(box);
            frame.Dock = DockStyle.None;
            frame.Width = 516;
            frame.Margin = new Padding(0, 0, 0, 12);
            AddRow(frame);
            Shown += delegate { box.Focus(); box.SelectAll(); };
            return box;
        }

        /// <summary>
        /// Adds a button. Buttons appear right to left in the order added; the
        /// first primary one answers Enter, and a Cancel result answers Esc.
        /// </summary>
        public ActionButton AddButton(string text, DialogResult result, ActionButtonKind kind)
        {
            ActionButton button = UiFactory.Button(text, kind, null);
            button.DialogResult = result;
            button.Margin = new Padding(8, 0, 0, 0);
            button.MinimumSize = new Size(88, 32);
            _buttons.Controls.Add(button);
            if (kind == ActionButtonKind.Primary && !_hasPrimary)
            {
                _hasPrimary = true;
                AcceptButton = button;
            }
            if (result == DialogResult.Cancel || result == DialogResult.No && CancelButton == null)
                CancelButton = button;
            return button;
        }

        protected override void OnLoad(EventArgs e)
        {
            AddRow(_buttons);
            ManagerTheme.Apply(this);
            BackColor = ManagerTheme.Current.Window;
            base.OnLoad(e);
            // The first primary button (added first, shown rightmost) starts focused.
            if (AcceptButton is Control)
                ActiveControl = (Control)AcceptButton;
        }

        private void AddRow(Control control)
        {
            int row = _body.RowCount;
            _body.RowCount = row + 1;
            _body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _body.Controls.Add(control, 0, row);
        }

        private static string[] ToArray(IList<string> lines)
        {
            string[] result = new string[lines.Count];
            lines.CopyTo(result, 0);
            return result;
        }
    }
}
