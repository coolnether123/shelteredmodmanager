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
        private Label _workspaceStatus;
        private Label _itemEditorHeading;
        private Label _recipeEditorHeading;
        private ComboBox _iconItemPicker;
        private Button _paintToolButton;
        private Button _eraseToolButton;
        private Button _pickToolButton;
        private Button _undoButton;
        private Button _redoButton;
        private Button _saveIconButton;
        private ListView _validationList;
        private Label _validationSummary;

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
            Padding = new Padding(12);

            Panel commandHeader = new Panel { Dock = DockStyle.Top, Height = 72 };
            Label title = new Label
            {
                Text = "Content Workshop",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 2)
            };
            Label subtitle = new Label
            {
                Text = "Create Sheltered items, recipes, and pixel icons without writing code.",
                AutoSize = true,
                Location = new Point(7, 34)
            };
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 650,
                Padding = new Padding(0, 12, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            actions.Controls.Add(CreateCommandButton("New project", NewProject, false));
            actions.Controls.Add(CreateCommandButton("Open", OpenProject, false));
            actions.Controls.Add(CreateCommandButton("Save", SaveProject, true));
            actions.Controls.Add(CreateCommandButton("Check", ValidateProject, false));
            actions.Controls.Add(CreateCommandButton("Export folder", ExportFolder, false));
            actions.Controls.Add(CreateCommandButton("Export ZIP", ExportZip, false));
            actions.Controls.Add(CreateCommandButton("Install", InstallLocally, false));
            commandHeader.Controls.Add(actions);
            commandHeader.Controls.Add(title);
            commandHeader.Controls.Add(subtitle);

            GroupBox identity = new GroupBox();
            identity.Text = "Project details";
            identity.Dock = DockStyle.Top;
            identity.Height = 118;
            identity.Padding = new Padding(10, 18, 10, 8);
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
            _projectPath.Font = new Font("Segoe UI", 8.25f, FontStyle.Italic);
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

            _workspaceStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Padding = new Padding(6, 5, 0, 0),
                Text = "Create or open a project to begin."
            };
            Controls.Add(_tabs);
            Controls.Add(_workspaceStatus);
            Controls.Add(identity);
            Controls.Add(commandHeader);
            SetEditorEnabled(false);
        }

        private TabPage BuildItemsPage()
        {
            TabPage page = new TabPage("Items");
            page.Padding = new Padding(8);
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Size = new Size(1000, 600),
                Panel1MinSize = 240,
                SplitterDistance = 275,
                FixedPanel = FixedPanel.Panel1
            };
            Panel listPanel = CreateLibraryPanel(
                "Item library",
                "Select an item to edit. IDs stay inside the project namespace.");
            FlowLayoutPanel buttons = CreateListActions();
            Button add = CreateSmallButton("+ Add item");
            add.Click += delegate { AddItem(); };
            Button remove = CreateSmallButton("Remove");
            remove.Click += delegate { RemoveItem(); };
            buttons.Controls.Add(add);
            buttons.Controls.Add(remove);
            _itemList = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _itemList.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    LoadSelectedItem();
            };
            listPanel.Controls.Add(_itemList);
            listPanel.Controls.Add(buttons);
            split.Panel1.Controls.Add(listPanel);

            Panel editor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
            _itemEditorHeading = CreateEditorHeading("No item selected");
            TabControl sections = new TabControl { Dock = DockStyle.Fill };

            TabPage basics = CreateSectionPage("Identity & icon");
            TableLayoutPanel basicForm = CreateEditorGrid();
            AddTextRow(basicForm, _itemFields, "id", "Item ID");
            AddTextRow(basicForm, _itemFields, "displayName", "Display name");
            AddMultilineRow(basicForm, _itemFields, "description", "Player description");
            AddChoiceRow(basicForm, _itemFields, "category", "Category", new string[]
            {
                "Normal", "Medicine", "Entertainment", "Object", "Tool", "Food", "Water",
                "Weapon", "Ammo", "Armour", "LoadCarrying", "Equipment", "Schematic",
                "Shelter", "ShelterPaint", "Meat", "Embryo", "GasMask"
            });
            AddIconRow(basicForm);
            basics.Controls.Add(basicForm);

            TabPage economy = CreateSectionPage("Inventory & crafting");
            TableLayoutPanel economyColumns = CreateTwoColumnSections();
            TableLayoutPanel inventoryForm = CreateEditorGrid();
            AddNumberRow(inventoryForm, _itemFields, "stackSize", "Maximum stack", 1, 9999, 0);
            AddNumberRow(inventoryForm, _itemFields, "tradeValue", "Trade value", 0, 1000000, 0);
            AddNumberRow(inventoryForm, _itemFields, "burnValue", "Burn value", 0, 1000000, 2);
            AddNumberRow(inventoryForm, _itemFields, "scrapValue", "Scrap value", 0, 1000000, 2);
            AddNumberRow(inventoryForm, _itemFields, "loadCarrySlots", "Carry slots", 0, 9999, 0);
            TableLayoutPanel craftingForm = CreateEditorGrid();
            AddNumberRow(craftingForm, _itemFields, "baseCraftTime", "Base craft time", 0, 1000000, 2);
            AddNumberRow(craftingForm, _itemFields, "craftStackSize", "Craft output count", 1, 9999, 0);
            AddNumberRow(craftingForm, _itemFields, "fabricationCost", "Fabrication cost", 0, 1000000, 2);
            AddNumberRow(craftingForm, _itemFields, "fabricationTime", "Fabrication time", 0, 1000000, 2);
            economyColumns.Controls.Add(CreateSectionGroup(
                "Inventory & value", "How this item stacks, trades, burns, and carries.", inventoryForm), 0, 0);
            economyColumns.Controls.Add(CreateSectionGroup(
                "Crafting & fabrication", "Static production values used by recipes.", craftingForm), 1, 0);
            economy.Controls.Add(economyColumns);

            TabPage food = CreateSectionPage("Food & recycling");
            TableLayoutPanel foodColumns = CreateTwoColumnSections();
            TableLayoutPanel foodForm = CreateEditorGrid();
            AddNumberRow(foodForm, _itemFields, "rationValue", "Ration value", 0, 1000000, 0);
            AddNumberRow(foodForm, _itemFields, "contamination", "Contamination (0-1)", 0, 1, 3);
            AddCheckRow(foodForm, _itemFields, "rawFood", "Raw food");
            AddNumberRow(foodForm, _itemFields, "cookedMultiplier", "Cooked hunger multiplier", 0.01m, 1000, 3);
            TableLayoutPanel recyclingForm = CreateEditorGrid();
            AddMultilineRow(recyclingForm, _itemFields, "recycling", "Materials");
            Label syntax = new Label
            {
                Text = "One per line: item.id=count\nExample: Metal=2",
                AutoSize = true,
                Padding = new Padding(12, 4, 0, 0)
            };
            recyclingForm.Controls.Add(syntax, 1, recyclingForm.RowCount++);
            foodColumns.Controls.Add(CreateSectionGroup(
                "Food values", "Leave these at zero for non-food items.", foodForm), 0, 0);
            foodColumns.Controls.Add(CreateSectionGroup(
                "Recycling output", "Materials returned when the item is recycled.", recyclingForm), 1, 0);
            food.Controls.Add(foodColumns);

            sections.TabPages.Add(basics);
            sections.TabPages.Add(economy);
            sections.TabPages.Add(food);
            WireDirty(_itemFields);
            editor.Controls.Add(sections);
            editor.Controls.Add(_itemEditorHeading);
            split.Panel2.Controls.Add(editor);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildRecipesPage()
        {
            TabPage page = new TabPage("Recipes");
            page.Padding = new Padding(8);
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Size = new Size(1000, 600),
                Panel1MinSize = 240,
                SplitterDistance = 275,
                FixedPanel = FixedPanel.Panel1
            };
            Panel listPanel = CreateLibraryPanel(
                "Recipe library",
                "Recipes connect a result item to a station and material cost.");
            FlowLayoutPanel buttons = CreateListActions();
            Button add = CreateSmallButton("+ Add recipe");
            add.Click += delegate { AddRecipe(); };
            Button remove = CreateSmallButton("Remove");
            remove.Click += delegate { RemoveRecipe(); };
            buttons.Controls.Add(add);
            buttons.Controls.Add(remove);
            _recipeList = new ListBox
            {
                Dock = DockStyle.Fill,
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _recipeList.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    LoadSelectedRecipe();
            };
            listPanel.Controls.Add(_recipeList);
            listPanel.Controls.Add(buttons);
            split.Panel1.Controls.Add(listPanel);

            Panel editor = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 0, 0) };
            _recipeEditorHeading = CreateEditorHeading("No recipe selected");
            TableLayoutPanel sections = CreateTwoColumnSections();
            TableLayoutPanel definitionForm = CreateEditorGrid();
            AddTextRow(definitionForm, _recipeFields, "id", "Recipe ID");
            AddTextRow(definitionForm, _recipeFields, "resultItemId", "Creates item");
            AddChoiceRow(definitionForm, _recipeFields, "station", "Crafting station",
                new string[] { "Workbench", "Laboratory", "AmmoPress" });
            AddNumberRow(definitionForm, _recipeFields, "level", "Station level", 1, 5, 0);
            AddNumberRow(definitionForm, _recipeFields, "craftTime", "Craft time (seconds)", 0.01m, 1000000, 2);
            AddCheckRow(definitionForm, _recipeFields, "unique", "Unique result");
            AddCheckRow(definitionForm, _recipeFields, "locked", "Starts locked");
            AddTextRow(definitionForm, _recipeFields, "unlockFlag", "Unlock flag");

            TableLayoutPanel ingredientsForm = CreateEditorGrid();
            AddMultilineRow(ingredientsForm, _recipeFields, "ingredients", "Materials");
            Label syntax = new Label
            {
                Text = "One per line: item.id=count\nReferences may point to this pack, vanilla items, or dependencies.",
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Padding = new Padding(12, 4, 0, 0)
            };
            ingredientsForm.Controls.Add(syntax, 1, ingredientsForm.RowCount++);
            sections.Controls.Add(CreateSectionGroup(
                "Recipe settings", "Where, when, and how the result is crafted.", definitionForm), 0, 0);
            sections.Controls.Add(CreateSectionGroup(
                "Ingredient cost", "Materials consumed for one crafting operation.", ingredientsForm), 1, 0);
            WireDirty(_recipeFields);
            editor.Controls.Add(sections);
            editor.Controls.Add(_recipeEditorHeading);
            split.Panel2.Controls.Add(editor);
            page.Controls.Add(split);
            return page;
        }

        private TabPage BuildPixelPage()
        {
            TabPage page = new TabPage("Icon Studio");
            page.Padding = new Padding(8);
            Panel sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 260,
                Padding = new Padding(0, 0, 10, 0)
            };
            GroupBox targetGroup = new GroupBox
            {
                Text = "1. Choose item",
                Dock = DockStyle.Top,
                Height = 92,
                Padding = new Padding(10, 20, 10, 8)
            };
            _iconItemPicker = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _iconItemPicker.SelectedIndexChanged += IconItemPickerChanged;
            Label targetHelp = new Label
            {
                Text = "The saved PNG will be assigned to this item.",
                Dock = DockStyle.Bottom,
                Height = 28
            };
            targetGroup.Controls.Add(_iconItemPicker);
            targetGroup.Controls.Add(targetHelp);

            GroupBox fileGroup = new GroupBox
            {
                Text = "2. Start or load",
                Dock = DockStyle.Top,
                Height = 122,
                Padding = new Padding(10, 20, 10, 8)
            };
            FlowLayoutPanel fileActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            fileActions.Controls.Add(CreateSidebarButton("Load assigned icon", LoadSelectedIcon));
            fileActions.Controls.Add(CreateSidebarButton("Import PNG...", ImportIcon));
            fileActions.Controls.Add(CreateSidebarButton("New 32 x 32", NewIcon));
            fileGroup.Controls.Add(fileActions);

            GroupBox drawingGroup = new GroupBox
            {
                Text = "3. Draw",
                Dock = DockStyle.Top,
                Height = 155,
                Padding = new Padding(10, 20, 10, 8)
            };
            FlowLayoutPanel drawingActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            _paintToolButton = CreateSidebarButton("Paint (P)", SelectPaintTool);
            _eraseToolButton = CreateSidebarButton("Erase (E)", SelectEraseTool);
            _pickToolButton = CreateSidebarButton("Pick color (I)", SelectPickTool);
            Button colorButton = CreateSidebarButton("Choose color...", ChooseColor);
            _colorSwatch = new Panel
            {
                Size = new Size(34, 30),
                Margin = new Padding(6, 3, 3, 3),
                BackColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            _colorSwatch.Click += ChooseColor;
            drawingActions.Controls.Add(_paintToolButton);
            drawingActions.Controls.Add(_eraseToolButton);
            drawingActions.Controls.Add(_pickToolButton);
            drawingActions.Controls.Add(colorButton);
            drawingActions.Controls.Add(_colorSwatch);
            drawingGroup.Controls.Add(drawingActions);

            GroupBox finishGroup = new GroupBox
            {
                Text = "4. Review and save",
                Dock = DockStyle.Top,
                Height = 122,
                Padding = new Padding(10, 20, 10, 8)
            };
            FlowLayoutPanel finishActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
            };
            _undoButton = CreateSidebarButton("Undo", delegate { _pixelCanvas.Undo(); UpdatePixelStatus(); });
            _redoButton = CreateSidebarButton("Redo", delegate { _pixelCanvas.Redo(); UpdatePixelStatus(); });
            Button fitButton = CreateSidebarButton("Fit canvas", FitIconCanvas);
            _saveIconButton = CreateSidebarButton("Save to item", SaveIcon);
            _saveIconButton.Font = new Font(_saveIconButton.Font, FontStyle.Bold);
            finishActions.Controls.Add(_undoButton);
            finishActions.Controls.Add(_redoButton);
            finishActions.Controls.Add(fitButton);
            finishActions.Controls.Add(_saveIconButton);
            finishGroup.Controls.Add(finishActions);

            sidebar.Controls.Add(finishGroup);
            sidebar.Controls.Add(drawingGroup);
            sidebar.Controls.Add(fileGroup);
            sidebar.Controls.Add(targetGroup);

            Panel status = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(8, 7, 0, 0) };
            _pixelStatus = new Label
            {
                AutoSize = true,
                Text = "32 x 32 | 12x zoom | Saved icon"
            };
            status.Controls.Add(_pixelStatus);
            Panel canvasHost = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BorderStyle = BorderStyle.FixedSingle
            };
            _pixelCanvas = new PixelEditorCanvas { Dock = DockStyle.Fill };
            _pixelCanvas.ActiveColorChanged += delegate { UpdatePixelStatus(); };
            _pixelCanvas.ActiveToolChanged += delegate { UpdateToolButtons(); };
            _pixelCanvas.DocumentChanged += delegate
            {
                UpdatePixelStatus();
                UpdateWorkspaceStatus();
            };
            canvasHost.Controls.Add(_pixelCanvas);
            page.Controls.Add(canvasHost);
            page.Controls.Add(status);
            page.Controls.Add(sidebar);
            SelectPixelTool(PixelEditorTool.Paint);
            return page;
        }

        private TabPage BuildValidationPage()
        {
            TabPage page = new TabPage("Validation");
            page.Padding = new Padding(8);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 62 };
            Label title = new Label
            {
                Text = "Check the project before export",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 4)
            };
            _validationSummary = new Label
            {
                Text = "Run check to find missing fields, broken item references, and missing icon files.",
                AutoSize = true,
                Location = new Point(6, 33)
            };
            Button check = CreateSmallButton("Run check");
            check.Location = new Point(0, 6);
            check.Dock = DockStyle.Right;
            check.Click += ValidateProject;
            header.Controls.Add(check);
            header.Controls.Add(title);
            header.Controls.Add(_validationSummary);
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
            page.Controls.Add(header);
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
            ContentWorkshopOperationResult result = _service.Save(_project);
            ShowResult(result, "Project saved.");
            if (result.Success)
                _workspaceStatus.Text = "Saved | " + _project.RootPath;
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
            if (_pixelCanvas.HasUnsavedChanges)
            {
                ShowError("Save the current Icon Studio pixels before removing an item.");
                return;
            }
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
                _itemEditorHeading.Text = "No item selected";
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
            _itemEditorHeading.Text = (item.displayName ?? "Unnamed item") + "  |  " + (item.id ?? string.Empty);
            if (_iconItemPicker != null &&
                _itemList.SelectedIndex < _iconItemPicker.Items.Count &&
                _iconItemPicker.SelectedIndex != _itemList.SelectedIndex)
            {
                _loading = true;
                _iconItemPicker.SelectedIndex = _itemList.SelectedIndex;
                _loading = false;
            }
        }

        private void LoadSelectedRecipe()
        {
            if (!_loading)
                CommitRecipeAt(_loadedRecipeIndex);
            ClearFields(_recipeFields);
            if (_project == null || _recipeList.SelectedIndex < 0)
            {
                _loadedRecipeIndex = -1;
                _recipeEditorHeading.Text = "No recipe selected";
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
            _recipeEditorHeading.Text = (recipe.id ?? "Unnamed recipe") +
                "  |  Creates " + (recipe.resultItemId ?? "(no result)");
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
            if (!CommitPendingIconEdits())
                return false;
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
            {
                UpdatePixelStatus();
                _tabs.SelectedTab = _tabs.TabPages[2];
            }
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
            else
                UpdatePixelStatus();
        }

        private void NewIcon(object sender, EventArgs e)
        {
            _pixelCanvas.CreateDocument(32, 32);
        }

        private void SaveIcon(object sender, EventArgs e)
        {
            if (!SaveIconToSelectedItem(true))
                return;
        }

        private bool CommitPendingIconEdits()
        {
            if (_pixelCanvas == null || !_pixelCanvas.HasUnsavedChanges)
                return true;
            if (_project == null || _iconItemPicker.SelectedIndex < 0)
            {
                ShowError("The Icon Studio has unsaved pixels. Select an item before saving the project.");
                return false;
            }
            return SaveIconToSelectedItem(false);
        }

        private bool SaveIconToSelectedItem(bool showSuccess)
        {
            int itemIndex = _iconItemPicker == null
                ? _itemList.SelectedIndex
                : _iconItemPicker.SelectedIndex;
            if (_project == null || itemIndex < 0 || itemIndex >= _project.Content.items.Count)
            {
                ShowError("Choose the item that should receive this icon.");
                return false;
            }
            string temp = Path.Combine(Path.GetTempPath(), "smm-icon-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                string error;
                if (!_pixelCanvas.SavePng(temp, out error))
                {
                    ShowError(error);
                    return false;
                }
                ContentPackItem item = _project.Content.items[itemIndex];
                string relative;
                ContentWorkshopOperationResult result = _service.ImportIcon(
                    _project, temp, GetAssetName(item), out relative);
                if (!result.Success)
                {
                    ShowError(result.ErrorMessage);
                    return false;
                }
                item.iconPath = relative;
                _pixelCanvas.MarkSaved();
                _project.IsDirty = true;
                if (itemIndex == _itemList.SelectedIndex)
                    SetText(_itemFields, "iconPath", relative);
                UpdatePixelStatus();
                if (showSuccess)
                    ShowResult(result, "Icon saved to the project.");
                return true;
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

        private void OpenIconStudio(object sender, EventArgs e)
        {
            if (_itemList.SelectedIndex < 0)
            {
                ShowError("Select an item before opening the Icon Studio.");
                return;
            }
            _tabs.SelectedTab = _tabs.TabPages[2];
            SyncIconItemPicker();
        }

        private void IconItemPickerChanged(object sender, EventArgs e)
        {
            if (_loading || _iconItemPicker.SelectedIndex < 0)
                return;
            if (_iconItemPicker.SelectedIndex != _itemList.SelectedIndex)
                _itemList.SelectedIndex = _iconItemPicker.SelectedIndex;
            UpdatePixelStatus();
        }

        private void SelectPaintTool(object sender, EventArgs e)
        {
            SelectPixelTool(PixelEditorTool.Paint);
        }

        private void SelectEraseTool(object sender, EventArgs e)
        {
            SelectPixelTool(PixelEditorTool.Erase);
        }

        private void SelectPickTool(object sender, EventArgs e)
        {
            SelectPixelTool(PixelEditorTool.Pick);
        }

        private void SelectPixelTool(PixelEditorTool tool)
        {
            _pixelCanvas.ActiveTool = tool;
            UpdateToolButtons();
            _pixelCanvas.Focus();
        }

        private void UpdateToolButtons()
        {
            if (_pixelCanvas == null)
                return;
            _paintToolButton.FlatStyle = _pixelCanvas.ActiveTool == PixelEditorTool.Paint
                ? FlatStyle.Popup : FlatStyle.Standard;
            _eraseToolButton.FlatStyle = _pixelCanvas.ActiveTool == PixelEditorTool.Erase
                ? FlatStyle.Popup : FlatStyle.Standard;
            _pickToolButton.FlatStyle = _pixelCanvas.ActiveTool == PixelEditorTool.Pick
                ? FlatStyle.Popup : FlatStyle.Standard;
        }

        private void FitIconCanvas(object sender, EventArgs e)
        {
            _pixelCanvas.FitZoomToClient();
            UpdatePixelStatus();
        }

        private void SyncIconItemPicker()
        {
            if (_iconItemPicker == null)
                return;
            if (_pixelCanvas != null && _pixelCanvas.HasUnsavedChanges)
                return;
            int selected = _itemList == null ? -1 : _itemList.SelectedIndex;
            _loading = true;
            _iconItemPicker.Items.Clear();
            if (_project != null)
            {
                for (int i = 0; i < _project.Content.items.Count; i++)
                {
                    ContentPackItem item = _project.Content.items[i];
                    _iconItemPicker.Items.Add((item.displayName ?? "(unnamed)") + " [" + item.id + "]");
                }
            }
            if (selected >= 0 && selected < _iconItemPicker.Items.Count)
                _iconItemPicker.SelectedIndex = selected;
            _loading = false;
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
            SyncIconItemPicker();
            UpdateWorkspaceStatus();
        }

        private void ShowValidation(ContentPackValidationResult result)
        {
            _validationList.Items.Clear();
            _validationSummary.Text = result.IsValid
                ? "Ready to export | " + FormatCount(result.WarningCount, "warning")
                : "Fix " + FormatCount(result.ErrorCount, "error") + " before exporting | " +
                    FormatCount(result.WarningCount, "warning");
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
            _pixelStatus.Text = size.Width + " x " + size.Height +
                " | " + _pixelCanvas.Zoom + "x zoom | " +
                (_pixelCanvas.HasUnsavedChanges ? "Unsaved icon changes" : "Saved icon");
            _undoButton.Enabled = _pixelCanvas.Session.History.CanUndo;
            _redoButton.Enabled = _pixelCanvas.Session.History.CanRedo;
            _saveIconButton.Enabled = _project != null && _iconItemPicker.SelectedIndex >= 0;
            _iconItemPicker.Enabled = !_pixelCanvas.HasUnsavedChanges;
            UpdateToolButtons();
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

        private static Button CreateCommandButton(
            string text, EventHandler handler, bool primary)
        {
            Button button = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(3)
            };
            if (primary)
                button.Font = new Font(button.Font, FontStyle.Bold);
            button.Click += handler;
            return button;
        }

        private static Button CreateSmallButton(string text)
        {
            return new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                Padding = new Padding(5, 0, 5, 0),
                Margin = new Padding(3)
            };
        }

        private static Button CreateSidebarButton(string text, EventHandler handler)
        {
            Button button = new Button
            {
                Text = text,
                Width = 108,
                Height = 30,
                Margin = new Padding(3)
            };
            button.Click += handler;
            return button;
        }

        private static FlowLayoutPanel CreateListActions()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                Padding = new Padding(3, 2, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
        }

        private static Panel CreateLibraryPanel(string title, string help)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 70, 8, 0) };
            Label titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(4, 4)
            };
            Label helpLabel = new Label
            {
                Text = help,
                AutoEllipsis = true,
                Location = new Point(5, 31),
                Size = new Size(255, 34)
            };
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(helpLabel);
            return panel;
        }

        private static Label CreateEditorHeading(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(4, 6, 0, 0),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                AutoEllipsis = true
            };
        }

        private static TabPage CreateSectionPage(string text)
        {
            return new TabPage(text) { Padding = new Padding(10) };
        }

        private static TableLayoutPanel CreateTwoColumnSections()
        {
            TableLayoutPanel columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(2)
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            return columns;
        }

        private static GroupBox CreateSectionGroup(
            string title, string help, Control content)
        {
            GroupBox group = new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                Margin = new Padding(6),
                Padding = new Padding(10, 42, 10, 10)
            };
            Label helpLabel = new Label
            {
                Text = help,
                Dock = DockStyle.Top,
                Height = 30,
                Padding = new Padding(4, 0, 0, 0)
            };
            content.Dock = DockStyle.Fill;
            group.Controls.Add(content);
            group.Controls.Add(helpLabel);
            return group;
        }

        private void AddIconRow(TableLayoutPanel grid)
        {
            int row = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(new Label
            {
                Text = "Item icon",
                AutoSize = true,
                Margin = new Padding(3, 7, 3, 3)
            }, 0, row);
            TableLayoutPanel iconRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                AutoSize = true,
                Margin = new Padding(0)
            };
            iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            iconRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            TextBox path = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            Button import = CreateSmallButton("Import PNG...");
            import.Click += ImportIcon;
            Button edit = CreateSmallButton("Open Icon Studio");
            edit.Click += OpenIconStudio;
            iconRow.Controls.Add(path, 0, 0);
            iconRow.Controls.Add(import, 1, 0);
            iconRow.Controls.Add(edit, 2, 0);
            grid.Controls.Add(iconRow, 1, row);
            _itemFields.Add("iconPath", path);
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
            {
                _project.IsDirty = true;
                UpdateWorkspaceStatus();
            }
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
            bool iconDirty = _pixelCanvas != null && _pixelCanvas.HasUnsavedChanges;
            if ((_project == null || !_project.IsDirty) && !iconDirty)
                return true;

            DialogResult choice = MessageBox.Show(
                this,
                iconDirty
                    ? "This project has unsaved changes, including pixels in the Icon Studio. Discard them?"
                    : "This Content Workshop project has unsaved changes. Discard them?",
                "Content Workshop",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            return choice == DialogResult.Yes;
        }

        private void UpdateWorkspaceStatus()
        {
            if (_workspaceStatus == null)
                return;
            if (_project == null)
            {
                _workspaceStatus.Text = "Create or open a project to begin.";
                return;
            }
            int itemCount = _project.Content.items == null ? 0 : _project.Content.items.Count;
            int recipeCount = _project.Content.recipes == null ? 0 : _project.Content.recipes.Count;
            _workspaceStatus.Text =
                (_project.IsDirty ? "Unsaved project" : "Saved") +
                (_pixelCanvas != null && _pixelCanvas.HasUnsavedChanges ? " + unsaved icon" : string.Empty) +
                " | " + FormatCount(itemCount, "item") + " | " + FormatCount(recipeCount, "recipe") + " | " +
                _project.RootPath;
        }

        private static string FormatCount(int count, string singular)
        {
            return count + " " + singular + (count == 1 ? string.Empty : "s");
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
