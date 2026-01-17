using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Delegate for string-based events (requires custom delegate for .NET 3.5)
    /// </summary>
    public delegate void StringEventHandler(object sender, string value);

    public class SearchBox : UserControl
    {
        private TextBox _textBox;
        private Button _clearButton;
        private Label _iconLabel;
        private string _placeholder = "Search mods...";
        private bool _showingPlaceholder = true;
        private Timer _debounceTimer;
        
        /// <summary>
        /// Raised when search text changes (debounced by 300ms)
        /// </summary>
        public event StringEventHandler SearchChanged;
        
        /// <summary>
        /// Raised immediately when search text changes
        /// </summary>
        public event StringEventHandler SearchTextChanged;

        public string SearchText
        {
            get { return _showingPlaceholder ? string.Empty : _textBox.Text; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    ShowPlaceholder();
                }
                else
                {
                    HidePlaceholder();
                    _textBox.Text = value;
                }
            }
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value;
                if (_showingPlaceholder)
                    _textBox.Text = _placeholder;
            }
        }

        public SearchBox()
        {
            InitializeComponent();
            SetupDebounce();
        }

        private void InitializeComponent()
        {
            this.Height = 28;
            this.MinimumSize = new Size(150, 28);
            this.Padding = new Padding(0);

            // Search icon
            _iconLabel = new Label();
            _iconLabel.Text = "Q";
            _iconLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _iconLabel.AutoSize = false;
            _iconLabel.Size = new Size(24, 24);
            _iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            _iconLabel.Location = new Point(2, 2);

            // Text input
            _textBox = new TextBox();
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Font = new Font("Segoe UI", 10f);
            _textBox.Location = new Point(28, 5);
            _textBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _textBox.Enter += TextBox_Enter;
            _textBox.Leave += TextBox_Leave;
            _textBox.TextChanged += TextBox_TextChanged;
            _textBox.KeyDown += TextBox_KeyDown;

            // Clear button
            _clearButton = new Button();
            _clearButton.Text = "X";
            _clearButton.FlatStyle = FlatStyle.Flat;
            _clearButton.Size = new Size(20, 20);
            _clearButton.Cursor = Cursors.Hand;
            _clearButton.Visible = false;
            _clearButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _clearButton.FlatAppearance.BorderSize = 0;
            _clearButton.Click += ClearButton_Click;

            this.Controls.Add(_iconLabel);
            this.Controls.Add(_textBox);
            this.Controls.Add(_clearButton);

            // Initial layout
            ShowPlaceholder();
            this.Resize += SearchBox_Resize;
        }

        private void SearchBox_Resize(object sender, EventArgs e)
        {
            _textBox.Width = this.Width - 55;
            _clearButton.Location = new Point(this.Width - 24, 4);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // Draw border
            using (var pen = new Pen(Color.FromArgb(200, 200, 200), 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void SetupDebounce()
        {
            _debounceTimer = new Timer();
            _debounceTimer.Interval = 300;
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            if (SearchChanged != null)
                SearchChanged(this, SearchText);
        }

        private void TextBox_Enter(object sender, EventArgs e)
        {
            if (_showingPlaceholder)
            {
                HidePlaceholder();
            }
        }

        private void TextBox_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_textBox.Text))
            {
                ShowPlaceholder();
            }
        }

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            if (!_showingPlaceholder)
            {
                _clearButton.Visible = !string.IsNullOrEmpty(_textBox.Text);
                if (SearchTextChanged != null)
                    SearchTextChanged(this, _textBox.Text);
                
                // Restart debounce timer
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Clear();
                e.Handled = true;
            }
        }

        private void ClearButton_Click(object sender, EventArgs e)
        {
            Clear();
        }

        public void Clear()
        {
            _textBox.Text = string.Empty;
            _clearButton.Visible = false;
            ShowPlaceholder();
            if (SearchChanged != null)
                SearchChanged(this, string.Empty);
        }

        private void ShowPlaceholder()
        {
            _showingPlaceholder = true;
            _textBox.Text = _placeholder;
            _textBox.ForeColor = Color.Gray;
        }

        private void HidePlaceholder()
        {
            _showingPlaceholder = false;
            if (_textBox.Text == _placeholder)
                _textBox.Text = string.Empty;
            _textBox.ForeColor = this.ForeColor;
        }

        /// <summary>
        /// Apply theme colors
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                this.BackColor = Color.FromArgb(60, 60, 60);
                _textBox.BackColor = Color.FromArgb(60, 60, 60);
                _textBox.ForeColor = _showingPlaceholder ? Color.Gray : Color.White;
                _iconLabel.ForeColor = Color.LightGray;
                _clearButton.BackColor = Color.FromArgb(60, 60, 60);
                _clearButton.ForeColor = Color.LightGray;
            }
            else
            {
                this.BackColor = Color.White;
                _textBox.BackColor = Color.White;
                _textBox.ForeColor = _showingPlaceholder ? Color.Gray : Color.Black;
                _iconLabel.ForeColor = Color.Gray;
                _clearButton.BackColor = Color.White;
                _clearButton.ForeColor = Color.Gray;
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && _debounceTimer != null)
            {
                _debounceTimer.Dispose();
                _debounceTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}
