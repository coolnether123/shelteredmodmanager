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
            this.SuspendLayout();
            this.Padding = new Padding(15);

            // LEFT: Available mods list
            _availableList = new ModListView();
            _availableList.Title = "Available Mods";
            _availableList.ShowSearch = true;
            _availableList.Dock = DockStyle.Left;
            _availableList.Width = 280;

            // CENTER: Action buttons
            _buttonPanel = new Panel();
            _buttonPanel.Dock = DockStyle.Left;
            _buttonPanel.Width = 130;
            _buttonPanel.Padding = new Padding(10, 80, 10, 10);

            _enableButton = new ActionButton();
            _enableButton.Text = "Add >>";
            _enableButton.IsPrimary = true;
            _enableButton.Width = 110;
            _enableButton.Height = 35;
            _enableButton.Top = 120;

            _disableButton = new ActionButton();
            _disableButton.Text = "<< Remove";
            _disableButton.Width = 110;
            _disableButton.Height = 35;
            _disableButton.Top = 165;

            _moveUpButton = new ActionButton();
            _moveUpButton.Text = "Move Up";
            _moveUpButton.Width = 110;
            _moveUpButton.Height = 35;
            _moveUpButton.Top = 230;

            _moveDownButton = new ActionButton();
            _moveDownButton.Text = "Move Down";
            _moveDownButton.Width = 110;
            _moveDownButton.Height = 35;
            _moveDownButton.Top = 275;

            _saveOrderButton = new ActionButton();
            _saveOrderButton.Text = "Save Order";
            _saveOrderButton.IsPrimary = true;
            _saveOrderButton.Width = 110;
            _saveOrderButton.Height = 35;
            _saveOrderButton.Top = 340;
            _saveOrderButton.Enabled = false;

            _buttonPanel.Controls.Add(_enableButton);
            _buttonPanel.Controls.Add(_disableButton);
            _buttonPanel.Controls.Add(_moveUpButton);
            _buttonPanel.Controls.Add(_moveDownButton);
            _buttonPanel.Controls.Add(_saveOrderButton);

            // CENTER-RIGHT: Enabled mods list
            _enabledList = new ModListView();
            _enabledList.Title = "Active Load Order";
            _enabledList.ShowSearch = true;
            _enabledList.Dock = DockStyle.Left;
            _enabledList.Width = 280;

            // RIGHT: Details panel
            _detailsPanel = new ModDetailsPanel();
            _detailsPanel.Dock = DockStyle.Fill;
            _detailsPanel.MinimumSize = new Size(300, 400);

            // Add in order (right to left for docking to work correctly)
            this.Controls.Add(_detailsPanel);
            this.Controls.Add(_enabledList);
            this.Controls.Add(_buttonPanel);
            this.Controls.Add(_availableList);

            this.ResumeLayout();
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
