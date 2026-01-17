using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;
using Manager.Core.Services;

namespace Manager.Views
{
    /// <summary>
    /// Delegate for string array events
    /// </summary>
    public delegate void StringArrayHandler(string[] items);

    /// <summary>
    /// The Mod Manager tab - handles enabling, disabling, and ordering mods.
    /// Separated from main form for maintainability.
    /// </summary>
    public class ModManagerTab : UserControl
    {
        // Controls
        private ModListView _availableList;
        private ModListView _enabledList;
        private ModDetailsPanel _detailsPanel;
        
        private ActionButton _enableButton;
        private ActionButton _disableButton;
        private ActionButton _moveUpButton;
        private ActionButton _moveDownButton;
        private ActionButton _saveOrderButton;
        private Panel _buttonPanel;

        // Services
        private ModDiscoveryService _discoveryService;
        private LoadOrderService _orderService;

        // State
        private AppSettings _settings;
        private List<ModItem> _allMods = new List<ModItem>();
        private bool _orderDirty = false;
        private bool _isDarkMode = false;

        /// <summary>
        /// Event raised when order is saved
        /// </summary>
        public event StringArrayHandler OrderSaved;

        public ModManagerTab()
        {
            InitializeComponent();
            WireEvents();
            ButtonPanel_Resize(null, EventArgs.Empty);
        }

        /// <summary>
        /// Initialize with services and settings
        /// </summary>
        public void Initialize(ModDiscoveryService discoveryService, LoadOrderService orderService, AppSettings settings)
        {
            _discoveryService = discoveryService;
            _orderService = orderService;
            _settings = settings;
            
            _detailsPanel.InstalledModApiVersion = settings.InstalledModApiVersion;
        }

        private void InitializeComponent()
        {
            this._availableList = new Manager.Controls.ModListView();
            this._buttonPanel = new System.Windows.Forms.Panel();
            this._enableButton = new Manager.Controls.ActionButton();
            this._disableButton = new Manager.Controls.ActionButton();
            this._moveUpButton = new Manager.Controls.ActionButton();
            this._moveDownButton = new Manager.Controls.ActionButton();
            this._saveOrderButton = new Manager.Controls.ActionButton();
            this._enabledList = new Manager.Controls.ModListView();
            this._detailsPanel = new Manager.Controls.ModDetailsPanel();
            this._buttonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // _availableList
            // 
            this._availableList.Dock = System.Windows.Forms.DockStyle.Left;
            this._availableList.Location = new System.Drawing.Point(15, 15);
            this._availableList.MinimumSize = new System.Drawing.Size(200, 150);
            this._availableList.Name = "_availableList";
            this._availableList.SelectedItem = null;
            this._availableList.ShowSearch = true;
            this._availableList.Size = new System.Drawing.Size(280, 150);
            this._availableList.TabIndex = 3;
            this._availableList.Title = "Available Mods";
            // 
            // _buttonPanel
            // 
            this._buttonPanel.Controls.Add(this._enableButton);
            this._buttonPanel.Controls.Add(this._disableButton);
            this._buttonPanel.Controls.Add(this._moveUpButton);
            this._buttonPanel.Controls.Add(this._moveDownButton);
            this._buttonPanel.Controls.Add(this._saveOrderButton);
            this._buttonPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this._buttonPanel.Location = new System.Drawing.Point(295, 15);
            this._buttonPanel.MinimumSize = new System.Drawing.Size(130, 300);
            this._buttonPanel.Name = "_buttonPanel";
            this._buttonPanel.Size = new System.Drawing.Size(130, 300);
            this._buttonPanel.TabIndex = 2;
            // 
            // _enableButton
            // 
            this._enableButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this._enableButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this._enableButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(100)))), ((int)(((byte)(180)))));
            this._enableButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._enableButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._enableButton.ForeColor = System.Drawing.Color.White;
            this._enableButton.IsPrimary = true;
            this._enableButton.Location = new System.Drawing.Point(0, 0);
            this._enableButton.MinimumSize = new System.Drawing.Size(100, 35);
            this._enableButton.Name = "_enableButton";
            this._enableButton.Size = new System.Drawing.Size(110, 35);
            this._enableButton.TabIndex = 0;
            this._enableButton.Text = "Add >>";
            this._enableButton.UseVisualStyleBackColor = false;
            // 
            // _disableButton
            // 
            this._disableButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this._disableButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this._disableButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this._disableButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._disableButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._disableButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this._disableButton.IsPrimary = false;
            this._disableButton.Location = new System.Drawing.Point(0, 0);
            this._disableButton.MinimumSize = new System.Drawing.Size(100, 35);
            this._disableButton.Name = "_disableButton";
            this._disableButton.Size = new System.Drawing.Size(110, 35);
            this._disableButton.TabIndex = 1;
            this._disableButton.Text = "<< Remove";
            this._disableButton.UseVisualStyleBackColor = false;
            // 
            // _moveUpButton
            // 
            this._moveUpButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this._moveUpButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this._moveUpButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this._moveUpButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._moveUpButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._moveUpButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this._moveUpButton.IsPrimary = false;
            this._moveUpButton.Location = new System.Drawing.Point(0, 0);
            this._moveUpButton.MinimumSize = new System.Drawing.Size(100, 35);
            this._moveUpButton.Name = "_moveUpButton";
            this._moveUpButton.Size = new System.Drawing.Size(110, 35);
            this._moveUpButton.TabIndex = 2;
            this._moveUpButton.Text = "Move Up";
            this._moveUpButton.UseVisualStyleBackColor = false;
            // 
            // _moveDownButton
            // 
            this._moveDownButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this._moveDownButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this._moveDownButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this._moveDownButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._moveDownButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._moveDownButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this._moveDownButton.IsPrimary = false;
            this._moveDownButton.Location = new System.Drawing.Point(0, 0);
            this._moveDownButton.MinimumSize = new System.Drawing.Size(100, 35);
            this._moveDownButton.Name = "_moveDownButton";
            this._moveDownButton.Size = new System.Drawing.Size(110, 35);
            this._moveDownButton.TabIndex = 3;
            this._moveDownButton.Text = "Move Down";
            this._moveDownButton.UseVisualStyleBackColor = false;
            // 
            // _saveOrderButton
            // 
            this._saveOrderButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
            this._saveOrderButton.Cursor = System.Windows.Forms.Cursors.Hand;
            this._saveOrderButton.Enabled = false;
            this._saveOrderButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(100)))), ((int)(((byte)(180)))));
            this._saveOrderButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._saveOrderButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this._saveOrderButton.ForeColor = System.Drawing.Color.White;
            this._saveOrderButton.IsPrimary = true;
            this._saveOrderButton.Location = new System.Drawing.Point(0, 0);
            this._saveOrderButton.MinimumSize = new System.Drawing.Size(100, 35);
            this._saveOrderButton.Name = "_saveOrderButton";
            this._saveOrderButton.Size = new System.Drawing.Size(110, 35);
            this._saveOrderButton.TabIndex = 4;
            this._saveOrderButton.Text = "Save Order";
            this._saveOrderButton.UseVisualStyleBackColor = false;
            // 
            // _enabledList
            // 
            this._enabledList.Dock = System.Windows.Forms.DockStyle.Left;
            this._enabledList.Location = new System.Drawing.Point(425, 15);
            this._enabledList.MinimumSize = new System.Drawing.Size(200, 150);
            this._enabledList.Name = "_enabledList";
            this._enabledList.SelectedItem = null;
            this._enabledList.ShowSearch = true;
            this._enabledList.Size = new System.Drawing.Size(280, 150);
            this._enabledList.TabIndex = 1;
            this._enabledList.Title = "Active Load Order";
            // 
            // _detailsPanel
            // 
            this._detailsPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._detailsPanel.InstalledModApiVersion = null;
            this._detailsPanel.Location = new System.Drawing.Point(705, 15);
            this._detailsPanel.MinimumSize = new System.Drawing.Size(300, 400);
            this._detailsPanel.Name = "_detailsPanel";
            this._detailsPanel.Padding = new System.Windows.Forms.Padding(12);
            this._detailsPanel.Size = new System.Drawing.Size(300, 400);
            this._detailsPanel.TabIndex = 0;
            // 
            // ModManagerTab
            // 
            this.Controls.Add(this._detailsPanel);
            this.Controls.Add(this._enabledList);
            this.Controls.Add(this._buttonPanel);
            this.Controls.Add(this._availableList);
            this.Name = "ModManagerTab";
            this.Padding = new System.Windows.Forms.Padding(15);
            this._buttonPanel.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        private void WireEvents()
        {
            // Selection changes
            _availableList.SelectionChanged += AvailableList_SelectionChanged;
            _enabledList.SelectionChanged += EnabledList_SelectionChanged;

            // Double-click to enable/disable
            _availableList.ItemDoubleClicked += AvailableList_ItemDoubleClicked;
            _enabledList.ItemDoubleClicked += EnabledList_ItemDoubleClicked;

            // Button actions
            _enableButton.Click += EnableButton_Click;
            _disableButton.Click += DisableButton_Click;
            _moveUpButton.Click += MoveUpButton_Click;
            _moveDownButton.Click += MoveDownButton_Click;
            _saveOrderButton.Click += SaveOrderButton_Click;

            // Open folder
            _detailsPanel.OpenFolderClicked += DetailsPanel_OpenFolderClicked;
            _detailsPanel.WebsiteClicked += DetailsPanel_WebsiteClicked;

            // Resize handling
            _buttonPanel.Resize += ButtonPanel_Resize;
        }

        private void ButtonPanel_Resize(object sender, EventArgs e)
        {
            // Center buttons vertically in the panel
            int buttonHeight = 35;
            int spacing = 10;
            int groupSpacing = 20; // Extra space between button groups
            
            // Calculate total height: 2 buttons + gap + 2 buttons + gap + 1 button
            int totalHeight = (buttonHeight * 5) + (spacing * 2) + (groupSpacing * 2);
            int startY = Math.Max(10, (_buttonPanel.Height - totalHeight) / 2);
            int centerX = Math.Max(0, (_buttonPanel.Width - 110) / 2);
            
            // Position Add/Remove group
            _enableButton.Location = new Point(centerX, startY);
            _disableButton.Location = new Point(centerX, startY + buttonHeight + spacing);
            
            // Position Move Up/Down group (with extra spacing)
            int moveGroupY = startY + (buttonHeight * 2) + spacing + groupSpacing;
            _moveUpButton.Location = new Point(centerX, moveGroupY);
            _moveDownButton.Location = new Point(centerX, moveGroupY + buttonHeight + spacing);
            
            // Position Save Order button (with extra spacing)
            int saveY = moveGroupY + (buttonHeight * 2) + spacing + groupSpacing;
            _saveOrderButton.Location = new Point(centerX, saveY);
        }

        private void AvailableList_SelectionChanged(object sender, ModItem mod)
        {
            _detailsPanel.ShowMod(mod);
            UpdateButtonStates();
        }

        private void EnabledList_SelectionChanged(object sender, ModItem mod)
        {
            _detailsPanel.ShowMod(mod);
            UpdateButtonStates();
        }

        private void AvailableList_ItemDoubleClicked(object sender, ModItem mod)
        {
            EnableSelectedMods();
        }

        private void EnabledList_ItemDoubleClicked(object sender, ModItem mod)
        {
            DisableSelectedMods();
        }

        private void EnableButton_Click(object sender, EventArgs e)
        {
            EnableSelectedMods();
        }

        private void DisableButton_Click(object sender, EventArgs e)
        {
            DisableSelectedMods();
        }

        private void MoveUpButton_Click(object sender, EventArgs e)
        {
            MoveSelectedUp();
        }

        private void MoveDownButton_Click(object sender, EventArgs e)
        {
            MoveSelectedDown();
        }

        private void SaveOrderButton_Click(object sender, EventArgs e)
        {
            SaveOrder();
        }

        private void DetailsPanel_OpenFolderClicked(object sender, string path)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", path); }
            catch { }
        }

        private void DetailsPanel_WebsiteClicked(object sender, string url)
        {
            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        /// <summary>
        /// Refresh mod lists from disk
        /// </summary>
        public void RefreshMods()
        {
            if (_settings == null || !_settings.IsModsPathValid || _discoveryService == null)
                return;

            // Discover all mods
            _allMods = _discoveryService.DiscoverMods(_settings.ModsPath);

            // Split into enabled/disabled
            var enabled = _orderService.GetEnabledMods(_allMods, _settings.ModsPath);
            var disabled = _orderService.GetDisabledMods(_allMods, _settings.ModsPath);

            // Validate and update status
            var validation = _orderService.ValidateOrder(enabled, _settings.ModsPath, _settings.SkipHarmonyDependencyCheck);
            
            foreach (var mod in enabled)
            {
                if (validation.HardIssueModIds.Contains(mod.Id))
                {
                    mod.Status = ModStatus.Error;
                    mod.StatusMessage = "Missing dependency or load order issue";
                }
                else if (validation.SoftIssueModIds.Contains(mod.Id))
                {
                    mod.Status = ModStatus.Warning;
                    mod.StatusMessage = "Load order may not be optimal";
                }
                else
                {
                    mod.Status = ModStatus.Ok;
                }
            }

            // Update lists
            _availableList.SetItems(disabled);
            _enabledList.SetItems(enabled);
            _enabledList.SetIssues(validation.HardIssueModIds, validation.SoftIssueModIds);

            _orderDirty = false;
            UpdateButtonStates();
        }

        private void EnableSelectedMods()
        {
            var selected = new List<ModItem>(_availableList.SelectedItems);
            if (selected.Count == 0) return;

            foreach (var mod in selected)
            {
                _orderService.EnableMod(_settings.ModsPath, mod.Id);
            }

            RefreshMods();
            MarkOrderDirty();
            
            // Raise event to update status counts
            if (OrderSaved != null)
            {
                var enabledIds = _enabledList.Items.Select(m => m.Id).ToArray();
                OrderSaved(enabledIds);
            }
        }

        private void DisableSelectedMods()
        {
            var selected = new List<ModItem>(_enabledList.SelectedItems);
            if (selected.Count == 0) return;

            foreach (var mod in selected)
            {
                _orderService.DisableMod(_settings.ModsPath, mod.Id);
            }

            RefreshMods();
            MarkOrderDirty();
            
            // Raise event to update status counts
            if (OrderSaved != null)
            {
                var enabledIds = _enabledList.Items.Select(m => m.Id).ToArray();
                OrderSaved(enabledIds);
            }
        }

        private void MoveSelectedUp()
        {
            var selected = _enabledList.SelectedItem;
            if (selected == null) return;

            _orderService.MoveUp(_settings.ModsPath, selected.Id);
            RefreshMods();
            MarkOrderDirty();

            // Re-select the moved item
            foreach (ModItem m in _enabledList.Items)
            {
                if (m.Id == selected.Id)
                {
                    _enabledList.SelectedItem = m;
                    break;
                }
            }
        }

        private void MoveSelectedDown()
        {
            var selected = _enabledList.SelectedItem;
            if (selected == null) return;

            _orderService.MoveDown(_settings.ModsPath, selected.Id);
            RefreshMods();
            MarkOrderDirty();

            // Re-select the moved item
            foreach (ModItem m in _enabledList.Items)
            {
                if (m.Id == selected.Id)
                {
                    _enabledList.SelectedItem = m;
                    break;
                }
            }
        }

        private void SaveOrder()
        {
            try
            {
                var items = _enabledList.Items;
                var enabledIds = new List<string>();
                foreach (var m in items)
                {
                    enabledIds.Add(m.Id);
                }
                
                _orderService.SaveOrder(_settings.ModsPath, enabledIds);
                
                _orderDirty = false;
                UpdateButtonStates();
                
                if (OrderSaved != null)
                    OrderSaved(enabledIds.ToArray());
                
                MessageBox.Show("Load order saved successfully!", "Saved", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save load order: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MarkOrderDirty()
        {
            _orderDirty = true;
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasAvailableSelection = false;
            foreach (ModItem m in _availableList.SelectedItems)
            {
                hasAvailableSelection = true;
                break;
            }
            _enableButton.Enabled = hasAvailableSelection;
            
            bool hasEnabledSelection = false;
            foreach (ModItem m in _enabledList.SelectedItems)
            {
                hasEnabledSelection = true;
                break;
            }
            _disableButton.Enabled = hasEnabledSelection;
            
            var enabledSelected = _enabledList.SelectedItem;
            var enabledItems = _enabledList.Items;
            
            int selectedIndex = -1;
            if (enabledSelected != null)
            {
                for (int i = 0; i < enabledItems.Count; i++)
                {
                    if (enabledItems[i].Id == enabledSelected.Id)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            
            _moveUpButton.Enabled = enabledSelected != null && selectedIndex > 0;
            _moveDownButton.Enabled = enabledSelected != null && selectedIndex >= 0 && selectedIndex < enabledItems.Count - 1;
            
            _saveOrderButton.Enabled = _orderDirty;
            _saveOrderButton.Text = _orderDirty ? "Save Order*" : "Save Order";
        }

        /// <summary>
        /// Apply theme
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            
            this.BackColor = isDark 
                ? Color.FromArgb(45, 45, 48) 
                : SystemColors.Control;

            _buttonPanel.BackColor = this.BackColor;

            _availableList.ApplyTheme(isDark);
            _enabledList.ApplyTheme(isDark);
            _detailsPanel.ApplyTheme(isDark);
            
            _enableButton.ApplyTheme(isDark);
            _disableButton.ApplyTheme(isDark);
            _moveUpButton.ApplyTheme(isDark);
            _moveDownButton.ApplyTheme(isDark);
            _saveOrderButton.ApplyTheme(isDark);
        }
    }
}
