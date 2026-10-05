using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Manager.Core.Models;

namespace Manager.Controls
{
    public delegate void ModToggleHandler(object sender, ModItem mod, bool turnOn);

    /// <summary>Asks for a mod to sit at <paramref name="activeIndex"/> in the load order, turning it on if needed.</summary>
    public delegate void ModMoveHandler(object sender, ModItem mod, int activeIndex);

    /// <summary>
    /// One list for every installed mod: active mods first, in load order,
    /// then the ones that are off. Each row has an on/off switch, and active
    /// rows drag to reorder; dragging a mod below "Not active" turns it off.
    /// Keyboard: Space toggles, Delete turns off, Ctrl+Up/Down moves.
    /// </summary>
    public sealed class ModSwitchList : UserControl
    {
        private const int HeaderHeight = 30;
        private const int RowHeight = 36;
        private const int SwitchWidth = 34;
        private const int SwitchHeight = 18;

        private sealed class Row
        {
            public bool IsHeader;
            public bool IsActiveHeader;
            public string Header;
            public int Count;
            public ModItem Mod;
            public bool Active;
            public int Position;
            public string Status;

            public override string ToString()
            {
                if (IsHeader)
                    return Header + ", " + Count;
                string text = Mod.DisplayName + (Active ? ", on, loads " + Ordinal(Position) : ", off");
                if (!string.IsNullOrEmpty(Status))
                    text += ", " + Status;
                return text;
            }
        }

        private readonly ListBox _list;
        private readonly Panel _frame;
        private readonly Label _emptyLabel;
        private readonly List<ModItem> _active = new List<ModItem>();
        private readonly List<ModItem> _inactive = new List<ModItem>();
        private HashSet<string> _hardIssues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _softIssues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Predicate<ModItem> _isNew;
        private string _filter = string.Empty;
        private bool _rebuilding;
        private int _lastIndex = -1;

        // Drag to reorder.
        private int _pressIndex = -1;
        private Point _pressPoint;
        private bool _dragging;
        private int _dropSlot = -1;
        private bool _dropToInactive;
        private bool _fileDropActive;

        public event ModItemEventHandler SelectionChanged;
        public event ModToggleHandler ToggleRequested;
        public event ModMoveHandler MoveRequested;

        public ModSwitchList()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawVariable,
                IntegralHeight = false,
                SelectionMode = SelectionMode.One,
                Font = ManagerTheme.UiFont(10f),
                AccessibleName = "Mods",
                AccessibleDescription = "Space turns the selected mod on or off. Ctrl+Up and Ctrl+Down change its load order."
            };
            _list.MeasureItem += List_MeasureItem;
            _list.DrawItem += List_DrawItem;
            _list.SelectedIndexChanged += List_SelectedIndexChanged;
            _list.MouseDown += List_MouseDown;
            _list.MouseMove += List_MouseMove;
            _list.MouseUp += List_MouseUp;
            _list.MouseDoubleClick += List_MouseDoubleClick;
            _list.KeyDown += List_KeyDown;
            _list.Resize += delegate { _list.Invalidate(); };

            _emptyLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                Visible = false,
                Font = ManagerTheme.UiFont(9.5f),
                Padding = new Padding(24)
            };
            _emptyLabel.DragEnter += delegate(object sender, DragEventArgs e) { OnDragEnter(e); };
            _emptyLabel.DragOver += delegate(object sender, DragEventArgs e) { OnDragOver(e); };
            _emptyLabel.DragDrop += delegate(object sender, DragEventArgs e) { OnDragDrop(e); };
            _emptyLabel.DragLeave += delegate(object sender, EventArgs e) { OnDragLeave(e); };

            _frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            _frame.Controls.Add(_list);
            _frame.Controls.Add(_emptyLabel);
            _emptyLabel.BringToFront();
            Controls.Add(_frame);

            // Files dropped on the list reach this control's drag events.
            foreach (Control target in new Control[] { _list, _frame })
            {
                target.DragEnter += delegate(object sender, DragEventArgs e) { OnDragEnter(e); };
                target.DragOver += delegate(object sender, DragEventArgs e) { OnDragOver(e); };
                target.DragDrop += delegate(object sender, DragEventArgs e) { OnDragDrop(e); };
                target.DragLeave += delegate(object sender, EventArgs e) { OnDragLeave(e); };
            }
            EmptyText = "No mods installed yet.";
        }

        public string EmptyText { get; set; }

        public override bool AllowDrop
        {
            get { return base.AllowDrop; }
            set
            {
                base.AllowDrop = value;
                _list.AllowDrop = value;
                _frame.AllowDrop = value;
                _emptyLabel.AllowDrop = value;
            }
        }

        /// <summary>Highlights the list while files are dragged over it.</summary>
        public bool FileDropActive
        {
            get { return _fileDropActive; }
            set
            {
                if (_fileDropActive == value)
                    return;
                _fileDropActive = value;
                ApplyFrameColor();
                UpdateEmptyState();
            }
        }

        public ModItem SelectedMod
        {
            get
            {
                Row row = _list.SelectedItem as Row;
                return row != null && !row.IsHeader ? row.Mod : null;
            }
        }

        public int ActiveCount
        {
            get { return _active.Count; }
        }

        public void SetFilter(string text)
        {
            _filter = (text ?? string.Empty).Trim();
            Rebuild();
        }

        public void SetMods(
            IList<ModItem> active,
            IList<ModItem> inactive,
            HashSet<string> hardIssues,
            HashSet<string> softIssues,
            Predicate<ModItem> isNew)
        {
            _active.Clear();
            _inactive.Clear();
            if (active != null) _active.AddRange(active);
            if (inactive != null) _inactive.AddRange(inactive);
            _hardIssues = hardIssues ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _softIssues = softIssues ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _isNew = isNew;
            Rebuild();
        }

        public void SelectMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return;
            for (int i = 0; i < _list.Items.Count; i++)
            {
                Row row = (Row)_list.Items[i];
                if (!row.IsHeader && string.Equals(row.Mod.Id, modId, StringComparison.OrdinalIgnoreCase))
                {
                    _list.SelectedIndex = i;
                    return;
                }
            }
        }

        public void FocusList()
        {
            _list.Focus();
        }

        public void RefreshRows()
        {
            _list.Invalidate();
        }

        public void ApplyTheme()
        {
            ThemePalette palette = ManagerTheme.Current;
            BackColor = palette.Window;
            _list.BackColor = palette.Input;
            _list.ForeColor = palette.Text;
            _emptyLabel.BackColor = palette.Input;
            _emptyLabel.ForeColor = palette.TextMuted;
            ApplyFrameColor();
            ManagerTheme.ApplyNativeScrollbars(_list);
            _list.Invalidate();
        }

        private void ApplyFrameColor()
        {
            ThemePalette palette = ManagerTheme.Current;
            _frame.Padding = new Padding(_fileDropActive ? 2 : 1);
            _frame.BackColor = _fileDropActive ? palette.AccentText : palette.Border;
        }

        // ------------------------------------------------------------ rows

        private void Rebuild()
        {
            string selectedId = SelectedMod != null ? SelectedMod.Id : null;
            int top = _list.Items.Count > 0 ? _list.TopIndex : 0;
            _rebuilding = true;
            _list.BeginUpdate();
            try
            {
                _list.Items.Clear();
                List<Row> activeRows = new List<Row>();
                for (int i = 0; i < _active.Count; i++)
                    if (Matches(_active[i]))
                        activeRows.Add(CreateRow(_active[i], true, i + 1));
                List<Row> inactiveRows = new List<Row>();
                for (int i = 0; i < _inactive.Count; i++)
                    if (Matches(_inactive[i]))
                        inactiveRows.Add(CreateRow(_inactive[i], false, 0));

                if (_active.Count + _inactive.Count > 0)
                {
                    _list.Items.Add(new Row { IsHeader = true, IsActiveHeader = true, Header = "ACTIVE · LOADS TOP TO BOTTOM", Count = _active.Count });
                    foreach (Row row in activeRows)
                        _list.Items.Add(row);
                    _list.Items.Add(new Row { IsHeader = true, Header = "NOT ACTIVE", Count = _inactive.Count });
                    foreach (Row row in inactiveRows)
                        _list.Items.Add(row);
                }

                if (_list.Items.Count > 0)
                    _list.TopIndex = Math.Min(top, _list.Items.Count - 1);
                if (!string.IsNullOrEmpty(selectedId))
                    SelectMod(selectedId);
                UpdateEmptyState();
            }
            finally
            {
                _list.EndUpdate();
                _rebuilding = false;
            }

            List_SelectedIndexChanged(_list, EventArgs.Empty);
        }

        private Row CreateRow(ModItem mod, bool active, int position)
        {
            Row row = new Row { Mod = mod, Active = active, Position = position };
            string status;
            Color unused;
            StatusFor(mod, false, out status, out unused);
            row.Status = status;
            if (_isNew != null && _isNew(mod))
                row.Status = string.IsNullOrEmpty(row.Status) ? "new" : row.Status + ", new";
            return row;
        }

        private bool Matches(ModItem mod)
        {
            return _filter.Length == 0 ||
                (mod.DisplayName ?? string.Empty).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (mod.Id ?? string.Empty).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateEmptyState()
        {
            bool none = _active.Count + _inactive.Count == 0;
            bool noMatch = !none && _filter.Length > 0 && CountModRows() == 0;
            if (_fileDropActive)
                _emptyLabel.Text = "Drop the .zip to install it";
            else if (none)
                _emptyLabel.Text = EmptyText;
            else
                _emptyLabel.Text = "No mods match “" + _filter + "”.";
            _emptyLabel.Visible = none || noMatch || (_fileDropActive && none);
            _emptyLabel.ForeColor = _fileDropActive ? ManagerTheme.Current.AccentText : ManagerTheme.Current.TextMuted;
        }

        private int CountModRows()
        {
            int count = 0;
            foreach (Row row in _list.Items)
                if (!row.IsHeader)
                    count++;
            return count;
        }

        private void StatusFor(ModItem mod, bool selected, out string status, out Color color)
        {
            ThemePalette palette = ManagerTheme.Current;
            status = null;
            color = palette.TextMuted;
            bool hasUpdate = mod.HasUpdateAvailable || mod.Status == ModStatus.UpdateAvailable;
            if (_hardIssues.Contains(mod.Id) || mod.Status == ModStatus.Error || mod.Status == ModStatus.MissingDependency)
            {
                status = "Problem";
                color = palette.Danger;
            }
            else if (_softIssues.Contains(mod.Id) || mod.Status == ModStatus.Warning || mod.Status == ModStatus.VersionMismatch)
            {
                status = "Warning";
                color = palette.Warning;
            }
            else if (hasUpdate)
            {
                status = "Update";
                color = palette.AccentText;
            }
            if (selected)
                color = palette.SelectionText;
        }

        // ------------------------------------------------------------ painting

        private void List_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            Row row = e.Index >= 0 && e.Index < _list.Items.Count ? (Row)_list.Items[e.Index] : null;
            e.ItemHeight = row != null && row.IsHeader ? HeaderHeight : RowHeight;
        }

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count)
                return;
            Row row = (Row)_list.Items[e.Index];
            if (row.IsHeader)
                DrawHeader(e, row);
            else
                DrawModRow(e, row);
        }

        private void DrawHeader(DrawItemEventArgs e, Row row)
        {
            ThemePalette palette = ManagerTheme.Current;
            bool target = _dragging && _dropToInactive && !row.IsActiveHeader;
            using (SolidBrush fill = new SolidBrush(target ? palette.Selection : palette.Surface))
                e.Graphics.FillRectangle(fill, e.Bounds);
            Rectangle text = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
            string header = target ? "DROP HERE TO TURN IT OFF" : row.Header;
            TextRenderer.DrawText(e.Graphics, header, ManagerTheme.Display(8.5f), text,
                target ? palette.SelectionText : palette.AccentText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, row.Count.ToString(), ManagerTheme.UiFont(8.5f), text, palette.TextMuted,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            using (Pen rule = new Pen(palette.Border))
                e.Graphics.DrawLine(rule, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            // An empty active section still needs somewhere to drop.
            if (row.IsActiveHeader && _dragging && !_dropToInactive && _dropSlot == 0 && _active.Count == 0)
                DrawDropLine(e.Graphics, e.Bounds.X, e.Bounds.Bottom - 2, e.Bounds.Width);
        }

        private void DrawModRow(DrawItemEventArgs e, Row row)
        {
            ThemePalette palette = ManagerTheme.Current;
            Graphics g = e.Graphics;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool dragged = _dragging && _pressIndex == e.Index;
            Color back = selected ? palette.Selection : palette.Input;
            Color text = selected ? palette.SelectionText : (row.Active ? palette.Text : palette.TextMuted);
            if (dragged)
                text = palette.TextDisabled;
            using (SolidBrush fill = new SolidBrush(back))
                g.FillRectangle(fill, e.Bounds);

            int x = e.Bounds.X + 10;
            int cy = e.Bounds.Y + e.Bounds.Height / 2;
            if (row.Active)
            {
                // Grip: two columns of dots, a quiet "drag me".
                using (SolidBrush dots = new SolidBrush(selected ? palette.SelectionText : palette.BorderStrong))
                {
                    for (int r = -1; r <= 1; r++)
                    {
                        g.FillRectangle(dots, x, cy + r * 4 - 1, 2, 2);
                        g.FillRectangle(dots, x + 4, cy + r * 4 - 1, 2, 2);
                    }
                }
                TextRenderer.DrawText(g, row.Position.ToString(), ManagerTheme.UiFont(8.5f),
                    new Rectangle(x + 8, e.Bounds.Y, 22, e.Bounds.Height),
                    selected ? palette.SelectionText : palette.TextMuted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            x += 36;

            DrawSwitch(g, SwitchBounds(e.Bounds), row.Active, selected);
            x = SwitchBounds(e.Bounds).Right + 12;

            string status;
            Color statusColor;
            StatusFor(row.Mod, selected, out status, out statusColor);
            int right = e.Bounds.Right - 10;
            if (status != null)
            {
                Font statusFont = ManagerTheme.UiFont(8.5f, FontStyle.Bold);
                Size size = TextRenderer.MeasureText(g, status, statusFont, Size.Empty, TextFormatFlags.NoPadding);
                Rectangle statusBounds = new Rectangle(right - size.Width, e.Bounds.Y, size.Width + 2, e.Bounds.Height);
                TextRenderer.DrawText(g, status, statusFont, statusBounds, statusColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (SolidBrush dot = new SolidBrush(statusColor))
                    g.FillEllipse(dot, statusBounds.Left - 12, cy - 4, 8, 8);
                g.SmoothingMode = SmoothingMode.Default;
                right = statusBounds.Left - 20;
            }

            bool isNew = _isNew != null && _isNew(row.Mod);
            Font nameFont = ManagerTheme.UiFont(10f);
            int badgeWidth = isNew ? 38 : 0;
            int nameWidth = Math.Max(0, right - x - badgeWidth);
            Size nameSize = TextRenderer.MeasureText(g, row.Mod.DisplayName, nameFont, new Size(nameWidth, e.Bounds.Height),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, row.Mod.DisplayName, nameFont, new Rectangle(x, e.Bounds.Y, nameWidth, e.Bounds.Height), text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            if (isNew)
            {
                Rectangle badge = new Rectangle(x + Math.Min(nameSize.Width, nameWidth) + 8, cy - 8, 32, 16);
                Color badgeColor = selected ? palette.SelectionText : palette.AccentText;
                using (GraphicsPath path = ManagerTheme.RoundedRectangle(badge, 3))
                using (Pen pen = new Pen(badgeColor))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawPath(pen, path);
                    g.SmoothingMode = SmoothingMode.Default;
                }
                TextRenderer.DrawText(g, "NEW", ManagerTheme.UiFont(7f, FontStyle.Bold), badge, badgeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            using (Pen divider = new Pen(palette.Border))
                g.DrawLine(divider, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            if (_dragging && !_dropToInactive && row.Active)
            {
                if (_dropSlot == row.Position - 1)
                    DrawDropLine(g, e.Bounds.X, e.Bounds.Y, e.Bounds.Width);
                else if (_dropSlot == _active.Count && row.Position == _active.Count)
                    DrawDropLine(g, e.Bounds.X, e.Bounds.Bottom - 2, e.Bounds.Width);
            }

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus && _list.Focused)
            {
                using (Pen focus = new Pen(palette.AccentText))
                    g.DrawRectangle(focus, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 2);
            }
        }

        private static Rectangle SwitchBounds(Rectangle row)
        {
            return new Rectangle(row.X + 46, row.Y + (row.Height - SwitchHeight) / 2, SwitchWidth, SwitchHeight);
        }

        private static void DrawSwitch(Graphics g, Rectangle bounds, bool on, bool selected)
        {
            ThemePalette palette = ManagerTheme.Current;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath track = ManagerTheme.RoundedRectangle(new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1), bounds.Height / 2))
            {
                using (SolidBrush fill = new SolidBrush(on ? palette.Accent : palette.SurfaceAlt))
                    g.FillPath(fill, track);
                using (Pen border = new Pen(on ? palette.AccentPressed : (selected ? palette.SelectionText : palette.BorderStrong)))
                    g.DrawPath(border, track);
            }
            int knob = bounds.Height - 6;
            int knobX = on ? bounds.Right - knob - 4 : bounds.X + 3;
            using (SolidBrush fill = new SolidBrush(on ? palette.OnAccent : palette.TextMuted))
                g.FillEllipse(fill, knobX, bounds.Y + 3, knob, knob);
            g.SmoothingMode = SmoothingMode.Default;
        }

        /// <summary>Insertion marker: a bold line with a dot at its start, where the mod will land.</summary>
        private static void DrawDropLine(Graphics g, int x, int y, int width)
        {
            Color color = ManagerTheme.Current.AccentText;
            using (SolidBrush fill = new SolidBrush(color))
            {
                g.FillRectangle(fill, x + 8, y, width - 12, 3);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(fill, x + 3, y - 3, 9, 9);
                g.SmoothingMode = SmoothingMode.Default;
            }
        }

        // ------------------------------------------------------------ selection

        private void List_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_rebuilding)
                return;
            int index = _list.SelectedIndex;
            Row row = index >= 0 ? (Row)_list.Items[index] : null;
            if (row != null && row.IsHeader)
            {
                // Headers are labels; step past them in the direction of travel.
                int step = index >= _lastIndex ? 1 : -1;
                int next = FindModRow(index, step);
                if (next < 0)
                    next = FindModRow(index, -step);
                if (next >= 0)
                {
                    _list.SelectedIndex = next;
                    return;
                }
            }
            _lastIndex = index;
            if (SelectionChanged != null)
                SelectionChanged(this, SelectedMod);
        }

        private int FindModRow(int from, int step)
        {
            for (int i = from + step; i >= 0 && i < _list.Items.Count; i += step)
                if (!((Row)_list.Items[i]).IsHeader)
                    return i;
            return -1;
        }

        // ------------------------------------------------------------ mouse

        private void List_MouseDown(object sender, MouseEventArgs e)
        {
            _pressIndex = -1;
            if (e.Button != MouseButtons.Left || e.Clicks > 1)
                return;
            int index = _list.IndexFromPoint(e.Location);
            if (index < 0 || index >= _list.Items.Count)
                return;
            Row row = (Row)_list.Items[index];
            if (row.IsHeader)
                return;
            Rectangle bounds = _list.GetItemRectangle(index);
            Rectangle hit = SwitchBounds(bounds);
            hit.Inflate(6, 8);
            if (hit.Contains(e.Location))
            {
                RaiseToggle(row.Mod, !row.Active);
                return;
            }
            _pressIndex = index;
            _pressPoint = e.Location;
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            if (_pressIndex < 0 || e.Button != MouseButtons.Left)
            {
                UpdateHoverCursor(e.Location);
                return;
            }
            if (!_dragging)
            {
                Size drag = SystemInformation.DragSize;
                if (Math.Abs(e.Y - _pressPoint.Y) < drag.Height && Math.Abs(e.X - _pressPoint.X) < drag.Width)
                    return;
                _dragging = true;
                _list.Cursor = Cursors.SizeNS;
            }

            // Scroll when dragging near the edges.
            if (e.Y < 14 && _list.TopIndex > 0)
                _list.TopIndex--;
            else if (e.Y > _list.ClientSize.Height - 14 && _list.TopIndex < _list.Items.Count - 1)
                _list.TopIndex++;

            UpdateDropTarget(e.Location);
            _list.Invalidate();
        }

        private void List_MouseUp(object sender, MouseEventArgs e)
        {
            if (!_dragging)
            {
                _pressIndex = -1;
                return;
            }
            Row dragged = _pressIndex >= 0 && _pressIndex < _list.Items.Count ? (Row)_list.Items[_pressIndex] : null;
            bool toInactive = _dropToInactive;
            int slot = _dropSlot;
            EndDrag();
            if (dragged == null || dragged.IsHeader)
                return;

            if (toInactive)
            {
                if (dragged.Active)
                    RaiseToggle(dragged.Mod, false);
                return;
            }
            if (slot < 0)
                return;
            int target = slot;
            if (dragged.Active)
            {
                int current = dragged.Position - 1;
                if (target > current)
                    target--;
                if (target == current)
                    return;
            }
            if (MoveRequested != null)
                MoveRequested(this, dragged.Mod, target);
        }

        private void List_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            int index = _list.IndexFromPoint(e.Location);
            if (index < 0 || index >= _list.Items.Count)
                return;
            Row row = (Row)_list.Items[index];
            Rectangle hit = SwitchBounds(_list.GetItemRectangle(index));
            hit.Inflate(6, 8);
            // The first click of a double-click on the switch already toggled it.
            if (!row.IsHeader && !hit.Contains(e.Location))
                RaiseToggle(row.Mod, !row.Active);
        }

        private void UpdateHoverCursor(Point location)
        {
            int index = _list.IndexFromPoint(location);
            Cursor cursor = Cursors.Default;
            if (index >= 0 && index < _list.Items.Count && !((Row)_list.Items[index]).IsHeader)
            {
                Rectangle hit = SwitchBounds(_list.GetItemRectangle(index));
                hit.Inflate(6, 8);
                if (hit.Contains(location))
                    cursor = Cursors.Hand;
            }
            if (_list.Cursor != cursor)
                _list.Cursor = cursor;
        }

        private void UpdateDropTarget(Point location)
        {
            _dropToInactive = false;
            _dropSlot = -1;
            int index = _list.IndexFromPoint(location);
            if (location.Y < 0)
                index = _list.TopIndex;
            if (index < 0 || index >= _list.Items.Count)
                index = _list.Items.Count - 1;
            if (index < 0)
                return;

            Row row = (Row)_list.Items[index];
            if (row.IsHeader)
            {
                if (row.IsActiveHeader)
                    _dropSlot = 0;
                else if (location.Y > _list.GetItemRectangle(index).Y + HeaderHeight / 2)
                    _dropToInactive = true;
                else
                    _dropSlot = _active.Count;
                return;
            }
            if (!row.Active)
            {
                _dropToInactive = true;
                return;
            }
            Rectangle bounds = _list.GetItemRectangle(index);
            bool lowerHalf = location.Y > bounds.Y + bounds.Height / 2;
            _dropSlot = lowerHalf ? row.Position : row.Position - 1;
            // With a filter on, positions still refer to the full load order.
        }

        private void EndDrag()
        {
            _dragging = false;
            _pressIndex = -1;
            _dropSlot = -1;
            _dropToInactive = false;
            _list.Cursor = Cursors.Default;
            _list.Invalidate();
        }

        // ------------------------------------------------------------ keyboard

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _dragging)
            {
                EndDrag();
                e.Handled = true;
                return;
            }
            Row row = _list.SelectedItem as Row;
            if (row == null || row.IsHeader)
                return;
            if (e.KeyCode == Keys.Space && !e.Control)
            {
                RaiseToggle(row.Mod, !row.Active);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete && row.Active)
            {
                RaiseToggle(row.Mod, false);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.Control && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && row.Active)
            {
                int target = row.Position - 1 + (e.KeyCode == Keys.Up ? -1 : 1);
                if (target >= 0 && target < _active.Count && MoveRequested != null)
                    MoveRequested(this, row.Mod, target);
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        private void RaiseToggle(ModItem mod, bool turnOn)
        {
            if (ToggleRequested != null)
                ToggleRequested(this, mod, turnOn);
        }

        private static string Ordinal(int value)
        {
            int lastTwo = value % 100;
            string suffix = "th";
            if (lastTwo < 11 || lastTwo > 13)
            {
                switch (value % 10)
                {
                    case 1: suffix = "st"; break;
                    case 2: suffix = "nd"; break;
                    case 3: suffix = "rd"; break;
                }
            }
            return value + suffix;
        }
    }
}
