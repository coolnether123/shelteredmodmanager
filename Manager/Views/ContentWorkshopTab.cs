using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;
using Manager.Core.Services;
using ShelteredModManager.ContentPacks;
using ShelteredModManager.Shared.PixelEditing;

namespace Manager.Views
{
    /// <summary>
    /// Thin Content Workshop preview surface. Project I/O, validation, packaging and
    /// installs are delegated to ContentWorkshopProjectService.
    /// </summary>
    public sealed class ContentWorkshopTab : UserControl
    {
        private readonly ContentWorkshopProjectService _service;
        private ContentWorkshopProject _project;
        private bool _loading;
        private int _loadedItemIndex = -1;
        private int _loadedRecipeIndex = -1;

        private TextBox _modId;
        private TextBox _modName;
        private TextBox _version;
        private TextBox _authors;
        private TextBox _description;
        private Label _projectPath;
        private TabControl _tabs;
        private ListBox _itemList;
        private ListBox _recipeList;
        private readonly Dictionary<string, Control> _itemFields =
            new Dictionary<string, Control>(StringComparer.Ordinal);
        private readonly Dictionary<string, Control> _recipeFields =
            new Dictionary<string, Control>(StringComparer.Ordinal);
        private PixelEditorCanvas _pixelCanvas;
        private Panel _colorSwatch;
        private Label _pixelStatus;
        private ListView _validationList;

        public string ModsPath { get; set; }

        public ContentWorkshopTab()
            : this(new ContentWorkshopProjectService())
        {
        }

        internal ContentWorkshopTab(ContentWorkshopProjectService service)
        {
            _service = service;
            InitializeView();
        }

        public bool ConfirmClose()
        {
            return ConfirmDiscardChanges();
        }

        public void ApplyTheme(bool isDark)
        {
            Color background = isDark ? Color.FromArgb(45, 45, 48) : SystemColors.Control;
            Color surface = isDark ? Color.FromArgb(37, 37, 38) : SystemColors.Window;
            Color foreground = isDark ? Color.Gainsboro : SystemColors.ControlText;
            ApplyThemeRecursive(this, background, surface, foreground);
            Invalidate(true);
        }

        private void InitializeView()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(10);

            ToolStrip actions = new ToolStrip();
            actions.GripStyle = ToolStripGripStyle.Hidden;
            actions.Dock = DockStyle.Top;
            actions.Items.Add(CreateAction("New", NewProject));
            actions.Items.Add(CreateAction("Open", OpenProject));
            actions.Items.Add(CreateAction("Save", SaveProject));
            actions.Items.Add(new ToolStripSeparator());
            actions.Items.Add(CreateAction("Validate", ValidateProject));
            actions.Items.Add(CreateAction("Export Folder", ExportFolder));
            actions.Items.Add(CreateAction("Export ZIP", ExportZip));
            actions.Items.Add(CreateAction("Install Locally", InstallLocally));

            Panel identity = new Panel();
            identity.Dock = DockStyle.Top;
            identity.Height = 120;
            identity.Padding = new Padding(4);
            TableLayoutPanel identityGrid = new TableLayoutPanel();
            identityGrid.Dock = DockStyle.Fill;
            identityGrid.ColumnCount = 6;
            identityGrid.RowCount = 3;
            identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            identityGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            _modId = AddIdentityField(identityGrid, "Mod ID", 0, 0);
            _modName = AddIdentityField(identityGrid, "Name", 2, 0);
            _version = AddIdentityField(identityGrid, "Version", 4, 0);
            _authors = AddIdentityField(identityGrid, "Authors", 0, 1);
            identityGrid.SetColumnSpan(_authors, 3);
            _description = AddIdentityField(identityGrid, "Description", 0, 2);
            identityGrid.SetColumnSpan(_description, 5);
            _projectPath = new Label();
            _projectPath.Text = "No project open";
            _projectPath.AutoEllipsis = true;
            _projectPath.Dock = DockStyle.Fill;
            _projectPath.TextAlign = ContentAlignment.MiddleLeft;
            identityGrid.Controls.Add(_projectPath, 4, 1);
            identityGrid.SetColumnSpan(_projectPath, 2);
            identity.Controls.Add(identityGrid);
            _modId.TextChanged += EditorChanged;
            _modName.TextChanged += EditorChanged;
            _version.TextChanged += EditorChanged;
            _authors.TextChanged += EditorChanged;
            _description.TextChanged += EditorChanged;

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.TabPages.Add(BuildItemsPage());
            _tabs.TabPages.Add(BuildRecipesPage());
            _tabs.TabPages.Add(BuildPixelPage());
            _tabs.TabPages.Add(BuildValidationPage());

            Controls.Add(_tabs);
            Controls.Add(identity);
            Controls.Add(actions);
            SetEditorEnabled(false);
        }

        private TabPage BuildItemsPage()
        {
            TabPage page = new TabPage("Items");
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.SplitterDistance = 250;
            Panel listPanel = new Panel { Dock = DockStyle.Fill };
            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight
            };
            Button add = new Button { Text = "Add Item", AutoSize = true };
            add.Click += delegate { AddItem(); };
            Button remove = new Button { Text = "Remove", AutoSize = true };
            remove.Click += delegate { RemoveItem(); };
            buttons.Controls.Add(add);
            buttons.Controls.Add(remove);
            _itemList = new ListBox { Dock = DockStyle.Fill };
            _itemList.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    LoadSelectedItem();
            };
            listPanel.Controls.Add(_itemList);
            listPanel.Controls.Add(buttons);
            split.Panel1.Controls.Add(listPanel);

            TableLayoutPanel form = CreateEditorGrid();
            AddTextRow(form, _itemFields, "id", "ID");
            AddTextRow(form, _itemFields, "displayName", "Display name");
            AddMultilineRow(form, _itemFields, "description", "Description");
            AddTextRow(form, _itemFields, "iconPath", "Icon path");
            AddChoiceRow(form, _itemFields, "category", "Category", new string[]
            {
                "Normal", "Medicine", "Entertainment", "Object", "Tool", "Food", "Water",
                "Weapon", "Ammo", "Armour", "LoadCarrying", "Equipment", "Schematic",
                "Shelter", "ShelterPaint", "Meat", "Embryo", "GasMask"
            });
            AddNumberRow(form, _itemFields, "stackSize", "Stack size", 1, 9999, 0);
            AddNumberRow(form, _itemFields, "tradeValue", "Trade value", 0, 1000000, 0);
            AddNumberRow(form, _itemFields, "burnValue", "Burn value", 0, 1000000, 2);
            AddNumberRow(form, _itemFields, "scrapValue", "Scrap value", 0, 1000000, 2);
            AddNumberRow(form, _itemFields, "baseCraftTime", "Base craft time", 0, 1000000, 2);
            AddNumberRow(form, _itemFields, "craftStackSize", "Craft stack size", 1, 9999, 0);
            AddNumberRow(form, _itemFields, "fabricationCost", "Fabrication cost", 0, 1000000, 2);
            AddNumberRow(form, _itemFields, "fabricationTime", "Fabrication time", 0, 1000000, 2);
            AddNumberRow(form, _itemFields, "rationValue", "Ration value", 0, 1000000, 0);
            AddNumberRow(form, _itemFields, "contamination", "Contamination (0–1)", 0, 1, 3);
            AddNumberRow(form, _itemFields, "loadCarrySlots", "Load carry slots", 0, 9999, 0);
            AddCheckRow(form, _itemFields, "rawFood", "Raw food");
            AddNumberRow(form, _itemFields, "cookedMultiplier", "Cooked hunger multiplier", 0.01m, 1000, 3);
            AddMultilineRow(form, _itemFields, "recycling", "Recycling (item=count)");
            WireDirty(_itemFields);
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(form);
            split.Panel2.Controls.Add(scroll);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildRecipesPage()
        {
            TabPage page = new TabPage("Recipes");
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 250
            };
            Panel listPanel = new Panel { Dock = DockStyle.Fill };
            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36
            };
            Button add = new Button { Text = "Add Recipe", AutoSize = true };
            add.Click += delegate { AddRecipe(); };
            Button remove = new Button { Text = "Remove", AutoSize = true };
            remove.Click += delegate { RemoveRecipe(); };
            buttons.Controls.Add(add);
            buttons.Controls.Add(remove);
            _recipeList = new ListBox { Dock = DockStyle.Fill };
            _recipeList.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    LoadSelectedRecipe();
            };
            listPanel.Controls.Add(_recipeList);
            listPanel.Controls.Add(buttons);
            split.Panel1.Controls.Add(listPanel);

            TableLayoutPanel form = CreateEditorGrid();
            AddTextRow(form, _recipeFields, "id", "ID");
            AddTextRow(form, _recipeFields, "resultItemId", "Result item ID");
            AddChoiceRow(form, _recipeFields, "station", "Station",
                new string[] { "Workbench", "Laboratory", "AmmoPress" });
            AddNumberRow(form, _recipeFields, "level", "Level", 1, 5, 0);
            AddNumberRow(form, _recipeFields, "craftTime", "Craft time", 0.01m, 1000000, 2);
            AddCheckRow(form, _recipeFields, "unique", "Unique");
            AddCheckRow(form, _recipeFields, "locked", "Locked");
            AddTextRow(form, _recipeFields, "unlockFlag", "Unlock flag");
            AddMultilineRow(form, _recipeFields, "ingredients", "Ingredients (item=count)");
            WireDirty(_recipeFields);
            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(form);
            split.Panel2.Controls.Add(scroll);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildPixelPage()
        {
            TabPage page = new TabPage("Icon Pixel Editor");
            ToolStrip tools = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };
            tools.Items.Add(CreateAction("Load Selected Icon", LoadSelectedIcon));
            tools.Items.Add(CreateAction("Import PNG", ImportIcon));
            tools.Items.Add(CreateAction("New 32×32", NewIcon));
            tools.Items.Add(CreateAction("Save to Selected Item", SaveIcon));
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(CreateAction("Paint", delegate { _pixelCanvas.ActiveTool = PixelEditorTool.Paint; }));
            tools.Items.Add(CreateAction("Erase", delegate { _pixelCanvas.ActiveTool = PixelEditorTool.Erase; }));
            tools.Items.Add(CreateAction("Pick", delegate { _pixelCanvas.ActiveTool = PixelEditorTool.Pick; }));
            tools.Items.Add(CreateAction("Color", ChooseColor));
            tools.Items.Add(CreateAction("Undo", delegate { _pixelCanvas.Undo(); }));
            tools.Items.Add(CreateAction("Redo", delegate { _pixelCanvas.Redo(); }));

            Panel status = new Panel { Dock = DockStyle.Bottom, Height = 30 };
            _colorSwatch = new Panel
            {
                Location = new Point(5, 5),
                Size = new Size(20, 20),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            _pixelStatus = new Label
            {
                Location = new Point(32, 6),
                AutoSize = true,
                Text = "32×32 • Ctrl+wheel zoom • Ctrl+Z/Ctrl+Y history"
            };
            status.Controls.Add(_colorSwatch);
            status.Controls.Add(_pixelStatus);
            _pixelCanvas = new PixelEditorCanvas { Dock = DockStyle.Fill };
            _pixelCanvas.ActiveColorChanged += delegate { UpdatePixelStatus(); };
            _pixelCanvas.DocumentChanged += delegate
            {
                if (_project != null)
                    _project.IsDirty = true;
                UpdatePixelStatus();
            };
            page.Controls.Add(_pixelCanvas);
            page.Controls.Add(status);
            page.Controls.Add(tools);
            return page;
        }

        private TabPage BuildValidationPage()
        {
            TabPage page = new TabPage("Validation");
            _validationList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _validationList.Columns.Add("Severity", 80);
            _validationList.Columns.Add("Location", 240);
            _validationList.Columns.Add("Message", 700);
            page.Controls.Add(_validationList);
            return page;
        }

        private void NewProject(object sender, EventArgs e)
        {
            if (!ConfirmDiscardChanges())
                return;

            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Choose an empty folder for the Content Workshop project";
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            if (Directory.Exists(dialog.SelectedPath) &&
                Directory.GetFileSystemEntries(dialog.SelectedPath).Length > 0)
            {
                ShowError("Choose an empty folder so existing files cannot be overwritten.");
                return;
            }
            _project = _service.Create(dialog.SelectedPath, "com.author.newcontent", "New Content Pack");
            RefreshProject();
        }

        private void OpenProject(object sender, EventArgs e)
        {
            if (!ConfirmDiscardChanges())
                return;

            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Select a Content Workshop mod folder";
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            ContentWorkshopProject loaded;
            ContentWorkshopOperationResult result = _service.Open(dialog.SelectedPath, out loaded);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                return;
            }
            _project = loaded;
            RefreshProject();
        }

        private void SaveProject(object sender, EventArgs e)
        {
            if (!CommitAllEditors())
                return;
            ShowResult(_service.Save(_project), "Project saved.");
            RefreshLists();
        }

        private void ValidateProject(object sender, EventArgs e)
        {
            if (!CommitAllEditors())
                return;
            ContentPackValidationResult result = _service.Validate(_project, true);
            ShowValidation(result);
            _tabs.SelectedTab = _tabs.TabPages[3];
        }

        private void ExportFolder(object sender, EventArgs e)
        {
            if (!CommitAllEditors())
                return;
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "Choose the parent folder for the exported mod";
            if (dialog.ShowDialog(this) == DialogResult.OK)
                ShowResult(_service.ExportFolder(_project, dialog.SelectedPath), "Mod folder exported.");
        }

        private void ExportZip(object sender, EventArgs e)
        {
            if (!CommitAllEditors())
                return;
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "ZIP archive (*.zip)|*.zip",
                FileName = _project.ModId + ".zip"
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                ShowResult(_service.ExportZip(_project, dialog.FileName), "ZIP exported.");
        }

        private void InstallLocally(object sender, EventArgs e)
        {
            if (!CommitAllEditors())
                return;
            string path = ModsPath;
            if (string.IsNullOrEmpty(path))
            {
                FolderBrowserDialog dialog = new FolderBrowserDialog();
                dialog.Description = "Select the Sheltered Mod Manager mods folder";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                path = dialog.SelectedPath;
            }
            ShowResult(_service.Install(_project, path), "Content pack installed.");
        }

        private void AddItem()
        {
            if (_project == null)
                return;
            CommitSelectedItem();
            ContentPackItem item = new ContentPackItem();
            item.id = _project.ModId + ".new_item_" + (_project.Content.items.Count + 1);
            item.displayName = "New Item";
            _project.Content.items.Add(item);
            _project.IsDirty = true;
            RefreshLists();
            _itemList.SelectedIndex = _project.Content.items.Count - 1;
        }

        private void RemoveItem()
        {
            if (_project == null || _itemList.SelectedIndex < 0)
                return;
            _project.Content.items.RemoveAt(_itemList.SelectedIndex);
            _project.IsDirty = true;
            RefreshLists();
        }

        private void AddRecipe()
        {
            if (_project == null)
                return;
            CommitSelectedRecipe();
            ContentPackRecipe recipe = new ContentPackRecipe();
            recipe.id = _project.ModId + ".new_recipe_" + (_project.Content.recipes.Count + 1);
            if (_project.Content.items.Count > 0)
                recipe.resultItemId = _project.Content.items[0].id;
            recipe.ingredients.Add(new ContentPackIngredient { itemId = "Wood", count = 1 });
            _project.Content.recipes.Add(recipe);
            _project.IsDirty = true;
            RefreshLists();
            _recipeList.SelectedIndex = _project.Content.recipes.Count - 1;
        }

        private void RemoveRecipe()
        {
            if (_project == null || _recipeList.SelectedIndex < 0)
                return;
            _project.Content.recipes.RemoveAt(_recipeList.SelectedIndex);
            _project.IsDirty = true;
            RefreshLists();
        }

        private void LoadSelectedItem()
        {
            if (!_loading)
                CommitItemAt(_loadedItemIndex);
            ClearFields(_itemFields);
            if (_project == null || _itemList.SelectedIndex < 0)
            {
                _loadedItemIndex = -1;
                return;
            }
            _loadedItemIndex = _itemList.SelectedIndex;
            ContentPackItem item = _project.Content.items[_itemList.SelectedIndex];
            SetText(_itemFields, "id", item.id);
            SetText(_itemFields, "displayName", item.displayName);
            SetText(_itemFields, "description", item.description);
            SetText(_itemFields, "iconPath", item.iconPath);
            SetText(_itemFields, "category", item.category);
            SetNumber(_itemFields, "stackSize", item.stackSize);
            SetNumber(_itemFields, "tradeValue", item.tradeValue);
            SetNumber(_itemFields, "burnValue", item.burnValue);
            SetNumber(_itemFields, "scrapValue", item.scrapValue);
            SetNumber(_itemFields, "baseCraftTime", item.baseCraftTime);
            SetNumber(_itemFields, "craftStackSize", item.craftStackSize);
            SetNumber(_itemFields, "fabricationCost", item.fabrication == null ? 0 : item.fabrication.cost);
            SetNumber(_itemFields, "fabricationTime", item.fabrication == null ? 0 : item.fabrication.timeSeconds);
            SetNumber(_itemFields, "rationValue", item.ration == null ? 0 : item.ration.value);
            SetNumber(_itemFields, "contamination", item.ration == null ? 0 : item.ration.contamination);
            SetNumber(_itemFields, "loadCarrySlots", item.loadCarrySlots);
            SetCheck(_itemFields, "rawFood", item.rawFood != null && item.rawFood.enabled);
            SetNumber(_itemFields, "cookedMultiplier",
                item.rawFood == null ? 1.1f : item.rawFood.cookedHungerMultiplier);
            SetText(_itemFields, "recycling", FormatIngredients(item.recycling));
        }

        private void LoadSelectedRecipe()
        {
            if (!_loading)
                CommitRecipeAt(_loadedRecipeIndex);
            ClearFields(_recipeFields);
            if (_project == null || _recipeList.SelectedIndex < 0)
            {
                _loadedRecipeIndex = -1;
                return;
            }
            _loadedRecipeIndex = _recipeList.SelectedIndex;
            ContentPackRecipe recipe = _project.Content.recipes[_recipeList.SelectedIndex];
            SetText(_recipeFields, "id", recipe.id);
            SetText(_recipeFields, "resultItemId", recipe.resultItemId);
            SetText(_recipeFields, "station", recipe.station);
            SetNumber(_recipeFields, "level", recipe.level);
            SetNumber(_recipeFields, "craftTime", recipe.craftTimeSeconds);
            SetCheck(_recipeFields, "unique", recipe.unique);
            SetCheck(_recipeFields, "locked", recipe.locked);
            SetText(_recipeFields, "unlockFlag", recipe.unlockFlag);
            SetText(_recipeFields, "ingredients", FormatIngredients(recipe.ingredients));
        }

        private bool CommitAllEditors()
        {
            if (_project == null)
            {
                ShowError("Create or open a project first.");
                return false;
            }
            _project.About.id = _modId.Text.Trim();
            _project.About.name = _modName.Text.Trim();
            _project.About.version = _version.Text.Trim();
            _project.About.description = _description.Text.Trim();
            _project.About.authors = SplitCommaList(_authors.Text);
            _project.Content.modId = _project.About.id;
            CommitSelectedItem();
            CommitSelectedRecipe();
            _project.IsDirty = true;
            return true;
        }

        private void CommitSelectedItem()
        {
            CommitItemAt(_loadedItemIndex);
        }

        private void CommitItemAt(int index)
        {
            if (_project == null || index < 0 ||
                index >= _project.Content.items.Count)
                return;
            ContentPackItem item = _project.Content.items[index];
            item.id = GetText(_itemFields, "id");
            item.displayName = GetText(_itemFields, "displayName");
            item.description = GetText(_itemFields, "description");
            item.iconPath = GetText(_itemFields, "iconPath");
            item.category = GetText(_itemFields, "category");
            item.stackSize = GetInt(_itemFields, "stackSize");
            item.tradeValue = GetInt(_itemFields, "tradeValue");
            item.burnValue = GetFloat(_itemFields, "burnValue");
            item.scrapValue = GetFloat(_itemFields, "scrapValue");
            item.baseCraftTime = GetFloat(_itemFields, "baseCraftTime");
            item.craftStackSize = GetInt(_itemFields, "craftStackSize");
            item.fabrication = item.fabrication ?? new ContentPackFabrication();
            item.fabrication.cost = GetFloat(_itemFields, "fabricationCost");
            item.fabrication.timeSeconds = GetFloat(_itemFields, "fabricationTime");
            item.ration = item.ration ?? new ContentPackRation();
            item.ration.value = GetInt(_itemFields, "rationValue");
            item.ration.contamination = GetFloat(_itemFields, "contamination");
            item.loadCarrySlots = GetInt(_itemFields, "loadCarrySlots");
            item.rawFood = item.rawFood ?? new ContentPackRawFood();
            item.rawFood.enabled = GetCheck(_itemFields, "rawFood");
            item.rawFood.cookedHungerMultiplier = GetFloat(_itemFields, "cookedMultiplier");
            item.recycling = ParseIngredients(GetText(_itemFields, "recycling"));
        }

        private void CommitSelectedRecipe()
        {
            CommitRecipeAt(_loadedRecipeIndex);
        }

        private void CommitRecipeAt(int index)
        {
            if (_project == null || index < 0 ||
                index >= _project.Content.recipes.Count)
                return;
            ContentPackRecipe recipe = _project.Content.recipes[index];
            recipe.id = GetText(_recipeFields, "id");
            recipe.resultItemId = GetText(_recipeFields, "resultItemId");
            recipe.station = GetText(_recipeFields, "station");
            recipe.level = GetInt(_recipeFields, "level");
            recipe.craftTimeSeconds = GetFloat(_recipeFields, "craftTime");
            recipe.unique = GetCheck(_recipeFields, "unique");
            recipe.locked = GetCheck(_recipeFields, "locked");
            recipe.unlockFlag = GetText(_recipeFields, "unlockFlag");
            recipe.ingredients = ParseIngredients(GetText(_recipeFields, "ingredients"));
        }

        private void ImportIcon(object sender, EventArgs e)
        {
            if (_project == null || _itemList.SelectedIndex < 0)
            {
                ShowError("Select an item first.");
                return;
            }
            OpenFileDialog dialog = new OpenFileDialog { Filter = "PNG image (*.png)|*.png" };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            string relative;
            string name = Path.GetFileNameWithoutExtension(dialog.FileName);
            ContentWorkshopOperationResult result =
                _service.ImportIcon(_project, dialog.FileName, name, out relative);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                return;
            }
            _project.Content.items[_itemList.SelectedIndex].iconPath = relative;
            LoadSelectedItem();
            string error;
            if (!_pixelCanvas.LoadPng(result.Path, out error))
                ShowError(error);
            else
                _tabs.SelectedTab = _tabs.TabPages[2];
        }

        private void LoadSelectedIcon(object sender, EventArgs e)
        {
            if (_project == null || _itemList.SelectedIndex < 0)
            {
                ShowError("Select an item first.");
                return;
            }
            CommitSelectedItem();
            ContentPackItem item = _project.Content.items[_itemList.SelectedIndex];
            string normalized;
            string full;
            string error;
            if (!ContentPackPathPolicy.TryResolveAsset(
                _project.RootPath, item.iconPath, out normalized, out full, out error) ||
                !File.Exists(full))
            {
                ShowError(error ?? "The selected item's icon was not found.");
                return;
            }
            if (!_pixelCanvas.LoadPng(full, out error))
                ShowError(error);
        }

        private void NewIcon(object sender, EventArgs e)
        {
            _pixelCanvas.CreateDocument(32, 32);
        }

        private void SaveIcon(object sender, EventArgs e)
        {
            if (_project == null || _itemList.SelectedIndex < 0)
            {
                ShowError("Select an item first.");
                return;
            }
            string temp = Path.Combine(Path.GetTempPath(), "smm-icon-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                string error;
                if (!_pixelCanvas.SavePng(temp, out error))
                {
                    ShowError(error);
                    return;
                }
                ContentPackItem item = _project.Content.items[_itemList.SelectedIndex];
                string relative;
                ContentWorkshopOperationResult result = _service.ImportIcon(
                    _project, temp, GetAssetName(item), out relative);
                if (!result.Success)
                {
                    ShowError(result.ErrorMessage);
                    return;
                }
                item.iconPath = relative;
                _project.IsDirty = true;
                LoadSelectedItem();
                ShowResult(result, "Icon saved to the project.");
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
            }
        }

        private void ChooseColor(object sender, EventArgs e)
        {
            ColorDialog dialog = new ColorDialog
            {
                Color = _pixelCanvas.ActiveColor,
                FullOpen = true
            };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                _pixelCanvas.ActiveColor = dialog.Color;
        }

        private void RefreshProject()
        {
            _loading = true;
            try
            {
                SetEditorEnabled(_project != null);
                if (_project == null)
                    return;
                _modId.Text = _project.ModId;
                _modName.Text = _project.About.name ?? string.Empty;
                _version.Text = _project.About.version ?? string.Empty;
                _description.Text = _project.About.description ?? string.Empty;
                _authors.Text = string.Join(", ", _project.About.authors ?? new string[0]);
                _projectPath.Text = _project.RootPath;
                RefreshLists();
            }
            finally
            {
                _loading = false;
            }
            LoadSelectedItem();
            LoadSelectedRecipe();
        }

        private void RefreshLists()
        {
            int itemIndex = _itemList.SelectedIndex;
            int recipeIndex = _recipeList.SelectedIndex;
            _loading = true;
            _loadedItemIndex = -1;
            _loadedRecipeIndex = -1;
            _itemList.Items.Clear();
            _recipeList.Items.Clear();
            if (_project != null)
            {
                for (int i = 0; i < _project.Content.items.Count; i++)
                {
                    ContentPackItem item = _project.Content.items[i];
                    _itemList.Items.Add((item.displayName ?? "(unnamed)") + "  [" + item.id + "]");
                }
                for (int i = 0; i < _project.Content.recipes.Count; i++)
                {
                    ContentPackRecipe recipe = _project.Content.recipes[i];
                    _recipeList.Items.Add((recipe.id ?? "(unnamed)") + " → " + recipe.resultItemId);
                }
                if (_itemList.Items.Count > 0)
                    _itemList.SelectedIndex = Math.Max(0, Math.Min(itemIndex, _itemList.Items.Count - 1));
                if (_recipeList.Items.Count > 0)
                    _recipeList.SelectedIndex = Math.Max(0, Math.Min(recipeIndex, _recipeList.Items.Count - 1));
            }
            _loading = false;
            LoadSelectedItem();
            LoadSelectedRecipe();
        }

        private void ShowValidation(ContentPackValidationResult result)
        {
            _validationList.Items.Clear();
            for (int i = 0; i < result.Issues.Count; i++)
            {
                ContentPackValidationIssue issue = result.Issues[i];
                ListViewItem row = new ListViewItem(issue.Severity.ToString());
                row.SubItems.Add(issue.Path);
                row.SubItems.Add(issue.Message);
                row.ForeColor = issue.Severity == ContentPackValidationSeverity.Error
                    ? Color.Firebrick
                    : Color.DarkGoldenrod;
                _validationList.Items.Add(row);
            }
            if (result.Issues.Count == 0)
            {
                ListViewItem valid = new ListViewItem("Valid");
                valid.SubItems.Add(string.Empty);
                valid.SubItems.Add("No validation issues found.");
                valid.ForeColor = Color.ForestGreen;
                _validationList.Items.Add(valid);
            }
        }

        private void UpdatePixelStatus()
        {
            _colorSwatch.BackColor = _pixelCanvas.ActiveColor;
            Size size = _pixelCanvas.CanvasPixelSize;
            _pixelStatus.Text = size.Width + "×" + size.Height +
                " • Zoom " + _pixelCanvas.Zoom + "× • " +
                (_pixelCanvas.Session.Dirty ? "Unsaved icon changes" : "Saved icon");
        }

        private void SetEditorEnabled(bool enabled)
        {
            _tabs.Enabled = enabled;
            _modId.Enabled = enabled;
            _modName.Enabled = enabled;
            _version.Enabled = enabled;
            _authors.Enabled = enabled;
            _description.Enabled = enabled;
        }

        private static ToolStripButton CreateAction(string text, EventHandler handler)
        {
            ToolStripButton button = new ToolStripButton(text);
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.Click += handler;
            return button;
        }

        private static TextBox AddIdentityField(
            TableLayoutPanel grid, string label, int column, int row)
        {
            Label caption = new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left
            };
            TextBox field = new TextBox { Dock = DockStyle.Fill };
            grid.Controls.Add(caption, column, row);
            grid.Controls.Add(field, column + 1, row);
            return field;
        }

        private static TableLayoutPanel CreateEditorGrid()
        {
            TableLayoutPanel grid = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(10)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return grid;
        }

        private static void AddTextRow(
            TableLayoutPanel grid, Dictionary<string, Control> fields, string key, string label)
        {
            AddRow(grid, fields, key, label, new TextBox { Dock = DockStyle.Fill });
        }

        private static void AddMultilineRow(
            TableLayoutPanel grid, Dictionary<string, Control> fields, string key, string label)
        {
            AddRow(grid, fields, key, label, new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Height = 65
            });
        }

        private static void AddChoiceRow(
            TableLayoutPanel grid,
            Dictionary<string, Control> fields,
            string key,
            string label,
            string[] choices)
        {
            ComboBox box = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            box.Items.AddRange(choices);
            AddRow(grid, fields, key, label, box);
        }

        private static void AddNumberRow(
            TableLayoutPanel grid,
            Dictionary<string, Control> fields,
            string key,
            string label,
            decimal minimum,
            decimal maximum,
            int decimals)
        {
            NumericUpDown number = new NumericUpDown
            {
                Dock = DockStyle.Left,
                Width = 180,
                Minimum = minimum,
                Maximum = maximum,
                DecimalPlaces = decimals
            };
            AddRow(grid, fields, key, label, number);
        }

        private static void AddCheckRow(
            TableLayoutPanel grid, Dictionary<string, Control> fields, string key, string label)
        {
            AddRow(grid, fields, key, label, new CheckBox { AutoSize = true });
        }

        private static void AddRow(
            TableLayoutPanel grid,
            Dictionary<string, Control> fields,
            string key,
            string label,
            Control editor)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(3, 7, 3, 3)
            }, 0, row);
            grid.Controls.Add(editor, 1, row);
            fields.Add(key, editor);
        }

        private void WireDirty(Dictionary<string, Control> fields)
        {
            foreach (Control control in fields.Values)
            {
                TextBox text = control as TextBox;
                ComboBox choice = control as ComboBox;
                NumericUpDown number = control as NumericUpDown;
                CheckBox check = control as CheckBox;
                if (text != null) text.TextChanged += EditorChanged;
                if (choice != null) choice.SelectedIndexChanged += EditorChanged;
                if (number != null) number.ValueChanged += EditorChanged;
                if (check != null) check.CheckedChanged += EditorChanged;
            }
        }

        private void EditorChanged(object sender, EventArgs e)
        {
            if (!_loading && _project != null)
                _project.IsDirty = true;
        }

        private static void ClearFields(Dictionary<string, Control> fields)
        {
            foreach (Control control in fields.Values)
            {
                TextBox text = control as TextBox;
                ComboBox choice = control as ComboBox;
                NumericUpDown number = control as NumericUpDown;
                CheckBox check = control as CheckBox;
                if (text != null) text.Text = string.Empty;
                if (choice != null) choice.SelectedIndex = choice.Items.Count > 0 ? 0 : -1;
                if (number != null) number.Value = number.Minimum;
                if (check != null) check.Checked = false;
            }
        }

        private static void SetText(Dictionary<string, Control> fields, string key, string value)
        {
            Control field = fields[key];
            TextBox text = field as TextBox;
            ComboBox choice = field as ComboBox;
            if (text != null)
                text.Text = value ?? string.Empty;
            else if (choice != null)
                choice.SelectedItem = value;
        }

        private static string GetText(Dictionary<string, Control> fields, string key)
        {
            Control field = fields[key];
            TextBox text = field as TextBox;
            ComboBox choice = field as ComboBox;
            return (text != null ? text.Text : (choice.SelectedItem ?? string.Empty).ToString()).Trim();
        }

        private static void SetNumber(
            Dictionary<string, Control> fields, string key, double value)
        {
            NumericUpDown number = (NumericUpDown)fields[key];
            decimal converted = (decimal)value;
            number.Value = Math.Max(number.Minimum, Math.Min(number.Maximum, converted));
        }

        private static int GetInt(Dictionary<string, Control> fields, string key)
        {
            return decimal.ToInt32(((NumericUpDown)fields[key]).Value);
        }

        private static float GetFloat(Dictionary<string, Control> fields, string key)
        {
            return decimal.ToSingle(((NumericUpDown)fields[key]).Value);
        }

        private static void SetCheck(
            Dictionary<string, Control> fields, string key, bool value)
        {
            ((CheckBox)fields[key]).Checked = value;
        }

        private static bool GetCheck(Dictionary<string, Control> fields, string key)
        {
            return ((CheckBox)fields[key]).Checked;
        }

        private static string FormatIngredients(List<ContentPackIngredient> values)
        {
            if (values == null)
                return string.Empty;
            StringBuilder output = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == null)
                    continue;
                if (output.Length > 0)
                    output.AppendLine();
                output.Append(values[i].itemId);
                output.Append('=');
                output.Append(values[i].count.ToString(CultureInfo.InvariantCulture));
            }
            return output.ToString();
        }

        private static List<ContentPackIngredient> ParseIngredients(string value)
        {
            List<ContentPackIngredient> result = new List<ContentPackIngredient>();
            string[] lines = (value ?? string.Empty).Replace("\r", string.Empty).Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;
                string[] pair = line.Split(new char[] { '=' }, 2);
                int count;
                if (pair.Length != 2 || !int.TryParse(pair[1].Trim(), out count))
                    count = 0;
                result.Add(new ContentPackIngredient
                {
                    itemId = pair[0].Trim(),
                    count = count
                });
            }
            return result;
        }

        private static string[] SplitCommaList(string value)
        {
            string[] raw = (value ?? string.Empty).Split(',');
            List<string> result = new List<string>();
            for (int i = 0; i < raw.Length; i++)
            {
                string entry = raw[i].Trim();
                if (entry.Length > 0)
                    result.Add(entry);
            }
            return result.ToArray();
        }

        private static string GetAssetName(ContentPackItem item)
        {
            string id = item == null ? string.Empty : (item.id ?? string.Empty);
            int separator = id.LastIndexOf('.');
            return separator >= 0 ? id.Substring(separator + 1) : id;
        }

        private void ShowResult(ContentWorkshopOperationResult result, string success)
        {
            if (result == null || !result.Success)
                ShowError(result == null ? "The operation failed." : result.ErrorMessage);
            else
                MessageBox.Show(this, success + "\r\n\r\n" + result.Path,
                    "Content Workshop", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "Content Workshop",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private bool ConfirmDiscardChanges()
        {
            if (_project == null || !_project.IsDirty)
                return true;

            DialogResult choice = MessageBox.Show(
                this,
                "This Content Workshop project has unsaved changes. Discard them?",
                "Content Workshop",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            return choice == DialogResult.Yes;
        }

        private static void ApplyThemeRecursive(
            Control root,
            Color background,
            Color surface,
            Color foreground)
        {
            root.ForeColor = foreground;
            if (root is TextBox || root is ListBox || root is ListView ||
                root is ComboBox || root is NumericUpDown)
            {
                root.BackColor = surface;
            }
            else if (!(root is PixelEditorCanvas))
            {
                root.BackColor = background;
            }

            ToolStrip strip = root as ToolStrip;
            if (strip != null)
            {
                strip.BackColor = background;
                strip.ForeColor = foreground;
                for (int i = 0; i < strip.Items.Count; i++)
                {
                    strip.Items[i].BackColor = background;
                    strip.Items[i].ForeColor = foreground;
                }
            }

            for (int i = 0; i < root.Controls.Count; i++)
                ApplyThemeRecursive(root.Controls[i], background, surface, foreground);
        }
    }
}
