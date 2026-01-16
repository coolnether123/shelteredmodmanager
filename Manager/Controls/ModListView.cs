using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Manager.Core.Models;

namespace Manager.Controls
{
    /// <summary>
    /// Delegate for ModItem-based events (requires custom delegate for .NET 3.5)
    /// </summary>
    public delegate void ModItemEventHandler(object sender, ModItem item);

    /// <summary>
    /// Enhanced mod list with status icons, filtering, and multi-select support.
    /// </summary>
    public class ModListView : UserControl
    {
        private ListBox _listBox;
        private Label _headerLabel;
        private Label _countLabel;
        private SearchBox _searchBox;
        
        private List<ModItem> _allItems = new List<ModItem>();
        private List<ModItem> _filteredItems = new List<ModItem>();
        private string _currentFilter = string.Empty;
        private bool _showSearch = true;
        private bool _isDarkMode = false;
        private string _title = "Mods";
        
        // Status indicators
        private HashSet<string> _hardIssueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _softIssueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Raised when selection changes
        /// </summary>
        public event ModItemEventHandler SelectionChanged;
        
        /// <summary>
        /// Raised when an item is double-clicked
        /// </summary>
        public event ModItemEventHandler ItemDoubleClicked;

        /// <summary>
        /// Title displayed above the list
        /// </summary>
        public string Title
        {
            get { return _title; }
            set 
            { 
                _title = value;
                UpdateHeader(); 
            }
        }

        /// <summary>
        /// Whether to show search box
        /// </summary>
        public bool ShowSearch
        {
            get { return _showSearch; }
            set
            {
                _showSearch = value;
                _searchBox.Visible = value;
            }
        }

        /// <summary>
        /// Selected item
        /// </summary>
        public ModItem SelectedItem
        {
            get { return _listBox.SelectedItem as ModItem; }
            set
            {
                if (value != null && _filteredItems.Contains(value))
                    _listBox.SelectedItem = value;
            }
        }

        /// <summary>
        /// All selected items (multi-select)
        /// </summary>
        public IEnumerable<ModItem> SelectedItems
        {
            get 
            { 
                var result = new List<ModItem>();
                foreach (object item in _listBox.SelectedItems)
                {
                    ModItem mod = item as ModItem;
                    if (mod != null)
                        result.Add(mod);
                }
                return result;
            }
        }

        /// <summary>
        /// All items in the list (returns a copy for safety)
        /// </summary>
        public List<ModItem> Items 
        { 
            get { return new List<ModItem>(_allItems); } 
        }

        /// <summary>
        /// Number of items
        /// </summary>
        public int Count
        {
            get { return _allItems.Count; }
        }

        public ModListView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.MinimumSize = new Size(200, 150);

            // Header
            _headerLabel = new Label();
            _headerLabel.Text = "Mods";
            _headerLabel.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _headerLabel.AutoSize = false;
            _headerLabel.Height = 25;
            _headerLabel.Dock = DockStyle.Top;
            _headerLabel.TextAlign = ContentAlignment.MiddleLeft;

            // Count label (shows on right of header)
            _countLabel = new Label();
            _countLabel.Text = "(0)";
            _countLabel.Font = new Font("Segoe UI", 9f);
            _countLabel.AutoSize = true;
            _countLabel.ForeColor = Color.Gray;
            _countLabel.TextAlign = ContentAlignment.MiddleRight;

            // Search box
            _searchBox = new SearchBox();
            _searchBox.Height = 28;
            _searchBox.Dock = DockStyle.Top;
            _searchBox.Placeholder = "Type to filter...";
            _searchBox.SearchChanged += SearchBox_SearchChanged;

            // List box
            _listBox = new ListBox();
            _listBox.Dock = DockStyle.Fill;
            _listBox.Font = new Font("Segoe UI", 10f);
            _listBox.BorderStyle = BorderStyle.FixedSingle;
            _listBox.SelectionMode = SelectionMode.MultiExtended;
            _listBox.DrawMode = DrawMode.OwnerDrawFixed;
            _listBox.ItemHeight = 26;
            _listBox.IntegralHeight = false;
            _listBox.DrawItem += ListBox_DrawItem;
            _listBox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
            _listBox.DoubleClick += ListBox_DoubleClick;

            // Add controls in order
            this.Controls.Add(_listBox);
            this.Controls.Add(_searchBox);
            this.Controls.Add(_headerLabel);
            
            // Position count label
            _headerLabel.Controls.Add(_countLabel);

            this.ResumeLayout();
            this.Resize += ModListView_Resize;
        }

        private void ModListView_Resize(object sender, EventArgs e)
        {
            LayoutControls();
        }

        private void LayoutControls()
        {
            _countLabel.Location = new Point(_headerLabel.Width - _countLabel.Width - 5, 4);
        }

        private void UpdateHeader()
        {
            _headerLabel.Text = _title;
            UpdateCountLabel();
        }

        private void SearchBox_SearchChanged(object sender, string text)
        {
            ApplyFilter(text);
        }

        private void UpdateCountLabel()
        {
            int total = _allItems.Count;
            int filtered = _filteredItems.Count;
            
            if (string.IsNullOrEmpty(_currentFilter))
                _countLabel.Text = "(" + total + ")";
            else
                _countLabel.Text = "(" + filtered + "/" + total + ")";
            
            LayoutControls();
        }

        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ModItem selected = _listBox.SelectedItem as ModItem;
            if (SelectionChanged != null)
                SelectionChanged(this, selected);
        }

        private void ListBox_DoubleClick(object sender, EventArgs e)
        {
            ModItem selected = _listBox.SelectedItem as ModItem;
            if (selected != null && ItemDoubleClicked != null)
                ItemDoubleClicked(this, selected);
        }

        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            ModItem mod = _listBox.Items[e.Index] as ModItem;
            if (mod == null) return;

            e.DrawBackground();

            // Determine colors
            Color bgColor = e.BackColor;
            Color textColor = e.ForeColor;
            
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            if (!isSelected)
            {
                bgColor = _isDarkMode ? Color.FromArgb(45, 45, 48) : SystemColors.Window;
                textColor = _isDarkMode ? Color.White : SystemColors.WindowText;
            }

            using (var brush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            // Draw status icon
            string icon = "OK";
            Color iconColor = _isDarkMode ? Color.LightGreen : Color.Green;
            
            if (_hardIssueIds.Contains(mod.Id) || mod.Status == ModStatus.Error || mod.Status == ModStatus.MissingDependency)
            {
                icon = "X";
                iconColor = Color.Red;
            }
            else if (_softIssueIds.Contains(mod.Id) || mod.Status == ModStatus.Warning || mod.Status == ModStatus.VersionMismatch)
            {
                icon = "!";
                iconColor = Color.Orange;
            }

            // Draw icon
            using (var iconBrush = new SolidBrush(iconColor))
            {
                e.Graphics.DrawString(icon, new Font("Segoe UI", 9f, FontStyle.Bold), iconBrush, 
                    new PointF(e.Bounds.X + 5, e.Bounds.Y + 4));
            }

            // Draw mod name
            using (var textBrush = new SolidBrush(textColor))
            {
                e.Graphics.DrawString(mod.DisplayName, e.Font, textBrush, 
                    new PointF(e.Bounds.X + 28, e.Bounds.Y + 4));
            }

            e.DrawFocusRectangle();
        }

        /// <summary>
        /// Set the items to display
        /// </summary>
        public void SetItems(IEnumerable<ModItem> items)
        {
            _allItems.Clear();
            foreach (var item in items)
                _allItems.Add(item);
            
            ApplyFilter(_currentFilter);
        }

        /// <summary>
        /// Set issue indicators
        /// </summary>
        public void SetIssues(HashSet<string> hardIssues, HashSet<string> softIssues)
        {
            _hardIssueIds = hardIssues ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _softIssueIds = softIssues ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _listBox.Invalidate();
        }

        private void ApplyFilter(string filter)
        {
            _currentFilter = filter ?? string.Empty;
            _filteredItems.Clear();

            if (string.IsNullOrEmpty(_currentFilter))
            {
                foreach (var item in _allItems)
                    _filteredItems.Add(item);
            }
            else
            {
                string lowerFilter = _currentFilter.ToLowerInvariant();
                foreach (var item in _allItems)
                {
                    if (item.DisplayName.ToLowerInvariant().Contains(lowerFilter) ||
                        item.Id.ToLowerInvariant().Contains(lowerFilter))
                    {
                        _filteredItems.Add(item);
                    }
                }
            }

            _listBox.BeginUpdate();
            _listBox.Items.Clear();
            foreach (var item in _filteredItems)
                _listBox.Items.Add(item);
            _listBox.EndUpdate();

            UpdateCountLabel();
        }

        /// <summary>
        /// Apply theme colors
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;

            if (isDark)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                _headerLabel.ForeColor = Color.White;
                _listBox.BackColor = Color.FromArgb(45, 45, 48);
                _listBox.ForeColor = Color.White;
            }
            else
            {
                this.BackColor = SystemColors.Control;
                _headerLabel.ForeColor = SystemColors.ControlText;
                _listBox.BackColor = SystemColors.Window;
                _listBox.ForeColor = SystemColors.WindowText;
            }

            _searchBox.ApplyTheme(isDark);
            _listBox.Invalidate();
        }
    }
}
