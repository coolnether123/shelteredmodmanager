using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Manager.Controls;
using ShelteredModManager.ContentPacks;
using ShelteredModManager.Shared.PixelEditing;

namespace Manager.Views
{
    // View construction for the Content Workshop. Layouts flow (table and dock
    // based) so fields never clip at the manager's minimum window size.
    public sealed partial class ContentWorkshopTab
    {
        private static readonly string[] ItemCategories = new string[]
        {
            "Normal", "Medicine", "Entertainment", "Object", "Tool", "Food", "Water",
            "Weapon", "Ammo", "Armour", "LoadCarrying", "Equipment", "Schematic",
            "Shelter", "ShelterPaint", "Meat", "Embryo", "GasMask"
        };

        private const int IssuePillWidth = 70;

        private ToolTip _toolTip;
        private bool _hasSavedCopy;
        private bool _developerMode;
        private readonly List<Control> _developerControls = new List<Control>();
        private readonly Dictionary<Control, List<Control>> _developerRows = new Dictionary<Control, List<Control>>();

        // Frame
        private TableLayoutPanel _statusBar;
        private Label _workspaceStatus;
        private Label _statusCounts;
        private Panel _startView;
        private TableLayoutPanel _startColumn;
        private ActionButton _startNewButton;
        private ActionButton _startOpenButton;
        private Panel _workspace;
        private TableLayoutPanel _projectBar;
        private Label _projectTitle;
        private Label _dirtyChip;
        private Label _projectMeta;
        private ActionButton _newButton;
        private ActionButton _openButton;
        private ActionButton _saveButton;
        private NoticeBanner _notice;
        private ThemedTabControl _tabs;
        private TabPage _projectPage;
        private TabPage _itemsPage;
        private TabPage _recipesPage;
        private TabPage _iconPage;
        private TabPage _checkPage;
        private readonly List<Control> _borderFrames = new List<Control>();
        private readonly List<Label> _overlayLabels = new List<Label>();

        // Project
        private TextBox _modId;
        private TextBox _modName;
        private TextBox _version;
        private TextBox _authors;
        private TextBox _description;
        private Label _modIdWarning;
        private TextBox _projectPathBox;

        // Items
        private ListBox _itemList;
        private Label _itemListEmpty;
        private ActionButton _removeItemButton;
        private Panel _itemEditor;
        private Panel _itemScroll;
        private Panel _itemEmpty;
        private Label _itemEmptyTitle;
        private Label _itemEmptyMessage;
        private IconPreview _itemHeaderIcon;
        private Label _itemEditorHeading;
        private Label _itemIdLabel;
        private IconPreview _itemIconPreview;

        // Recipes
        private ListBox _recipeList;
        private Label _recipeListEmpty;
        private ActionButton _removeRecipeButton;
        private Panel _recipeEditor;
        private Panel _recipeScroll;
        private Panel _recipeEmpty;
        private Label _recipeEmptyTitle;
        private Label _recipeEmptyMessage;
        private IconPreview _recipeHeaderIcon;
        private Label _recipeEditorHeading;
        private Label _recipeIdLabel;

        // Icon Studio
        private Panel _iconWorkArea;
        private Panel _iconEmpty;
        private Panel _canvasFrame;
        private ComboBox _iconItemPicker;
        private Label _iconPickerHelp;
        private ActionButton _paintToolButton;
        private ActionButton _eraseToolButton;
        private ActionButton _pickToolButton;
        private ActionButton _undoButton;
        private ActionButton _redoButton;
        private ActionButton _saveIconButton;
        private Panel _colorSwatch;
        private PixelEditorCanvas _pixelCanvas;
        private Label _pixelStatus;

        // Check & export
        private Label _validationTitle;
        private Label _validationSummary;
        private ActionButton _runCheckButton;
        private ListBox _validationList;
        private Label _validationEmpty;
        private int _validationListWidth;
        private ActionButton _installButton;
        private ActionButton _exportZipButton;
        private ActionButton _exportFolderButton;
        private Label _installDescription;

        private void InitializeView()
        {
            SuspendLayout();
            Dock = DockStyle.Fill;
            Padding = new Padding(0);
            Font = ManagerTheme.UiFont(9f);
            ManagerTheme.SetRole(this, ThemeRole.Window);
            _toolTip = new ToolTip
            {
                AutoPopDelay = 12000,
                InitialDelay = 400,
                ReshowDelay = 150,
                ShowAlways = true
            };

            BuildStatusBar();
            BuildStartView();
            BuildWorkspace();

            Controls.Add(_workspace);
            Controls.Add(_startView);
            Controls.Add(_statusBar);
            _startView.TabIndex = 0;
            _workspace.TabIndex = 1;
            _statusBar.TabIndex = 2;
            ResumeLayout(false);
        }

        // ------------------------------------------------------------ frame

        private void BuildStatusBar()
        {
            _statusBar = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                ColumnCount = 2,
                RowCount = 1,
                Height = 30,
                Padding = new Padding(16, 0, 16, 0),
                Margin = new Padding(0),
                AccessibleRole = AccessibleRole.StatusBar,
                AccessibleName = "Content Workshop status"
            };
            _statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _statusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _workspaceStatus = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Margin = new Padding(0),
                Font = ManagerTheme.UiFont(8.75f),
                Text = "Start a new pack or open one."
            };
            _statusCounts = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(12, 0, 0, 0),
                UseMnemonic = false,
                Font = ManagerTheme.UiFont(8.75f)
            };
            ManagerTheme.SetRole(_statusBar, ThemeRole.Chrome);
            ManagerTheme.SetRole(_workspaceStatus, ThemeRole.Muted);
            ManagerTheme.SetRole(_statusCounts, ThemeRole.Muted);
            _statusBar.Controls.Add(_workspaceStatus, 0, 0);
            _statusBar.Controls.Add(_statusCounts, 1, 0);
            _statusBar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(ManagerTheme.Current.Border))
                    e.Graphics.DrawLine(pen, 0, 0, _statusBar.Width, 0);
            };
        }

        private void BuildStartView()
        {
            _startView = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            ManagerTheme.SetRole(_startView, ThemeRole.Window);

            _startColumn = new TableLayoutPanel
            {
                ColumnCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _startColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 600f));

            PixelSprite crate = new PixelSprite { PixelScale = 5, Margin = new Padding(0, 4, 22, 0), Anchor = AnchorStyles.Top | AnchorStyles.Left };
            Label title = UiFactory.Heading("Content Workshop", 20f);
            Label lead = UiFactory.Text(
                "Make items, recipes and pixel icons for Sheltered, then pack them as a mod you can install or share.",
                ThemeRole.Muted, 10f, false);
            lead.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            lead.Margin = new Padding(0, 4, 0, 16);

            _startNewButton = UiFactory.Button("New pack", ActionButtonKind.Primary, NewProject);
            _startOpenButton = UiFactory.Button("Open pack...", ActionButtonKind.Secondary, OpenProject);
            foreach (ActionButton button in new ActionButton[] { _startNewButton, _startOpenButton })
            {
                button.Font = ManagerTheme.Semibold(10f);
                button.Padding = new Padding(18, 0, 18, 0);
                button.MinimumSize = new Size(0, 38);
            }
            _toolTip.SetToolTip(_startNewButton, "Start a new content pack (Ctrl+N)");
            _toolTip.SetToolTip(_startOpenButton, "Open a pack from another folder (Ctrl+O)");
            FlowLayoutPanel actions = UiFactory.ButtonRow(_startNewButton, _startOpenButton);
            actions.Margin = new Padding(0, 0, 0, 18);

            // Packs already in the workshop folder open with one click.
            _recentTitle = UiFactory.Text("YOUR PACKS", ThemeRole.Accent, 9.5f, false);
            _recentTitle.Font = ManagerTheme.Display(9.5f);
            _recentTitle.AccessibleName = "Your packs";
            _recentTitle.Margin = new Padding(0, 0, 0, 4);
            _recentPacks = CreateStack();
            _recentPacks.Dock = DockStyle.None;
            _recentPacks.Anchor = AnchorStyles.Left | AnchorStyles.Top;

            // Crate on the left, words and actions on the right.
            TableLayoutPanel text = CreateStack();
            text.Dock = DockStyle.Fill;
            AddStackRow(text, title);
            AddStackRow(text, lead);
            AddStackRow(text, actions);
            AddStackRow(text, _recentTitle);
            AddStackRow(text, _recentPacks);
            _startColumn.ColumnCount = 2;
            _startColumn.ColumnStyles.Insert(0, new ColumnStyle(SizeType.AutoSize));
            _startColumn.RowCount = 1;
            _startColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _startColumn.Controls.Add(crate, 0, 0);
            _startColumn.Controls.Add(text, 1, 0);
            _startView.Controls.Add(_startColumn);
            // Position during the layout pass so the scroll range is computed from final bounds.
            _startView.Layout += delegate { LayoutStartView(); };
            _startView.VisibleChanged += delegate { LayoutStartView(); };
        }

        private Label _recentTitle;
        private TableLayoutPanel _recentPacks;

        /// <summary>Lists packs in the workshop folder, newest first, as one-click buttons.</summary>
        private void RefreshRecentPacks()
        {
            if (_recentPacks == null)
                return;
            List<KeyValuePair<DateTime, string>> packs = new List<KeyValuePair<DateTime, string>>();
            try
            {
                if (Directory.Exists(WorkshopRoot))
                {
                    foreach (string folder in Directory.GetDirectories(WorkshopRoot))
                    {
                        string about = Path.Combine(Path.Combine(folder, "About"), "About.json");
                        if (File.Exists(about))
                            packs.Add(new KeyValuePair<DateTime, string>(File.GetLastWriteTimeUtc(about), folder));
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            packs.Sort(delegate(KeyValuePair<DateTime, string> a, KeyValuePair<DateTime, string> b) { return b.Key.CompareTo(a.Key); });

            _recentPacks.SuspendLayout();
            List<Control> old = new List<Control>();
            foreach (Control control in _recentPacks.Controls)
                old.Add(control);
            _recentPacks.Controls.Clear();
            _recentPacks.RowStyles.Clear();
            _recentPacks.RowCount = 0;
            foreach (Control control in old)
                control.Dispose();
            for (int i = 0; i < packs.Count && i < 6; i++)
            {
                string folder = packs[i].Value;
                ActionButton open = UiFactory.Button(Path.GetFileName(folder), ActionButtonKind.Quiet, delegate { OpenProjectAt(folder, false); });
                open.Margin = new Padding(0, 0, 0, 2);
                open.Anchor = AnchorStyles.Left;
                _toolTip.SetToolTip(open, "Open " + folder);
                AddStackRow(_recentPacks, open);
            }
            _recentPacks.ResumeLayout(true);
            _recentTitle.Visible = packs.Count > 0;
            _recentPacks.Visible = packs.Count > 0;
        }

        private bool _layingOutStart;

        private void LayoutStartView()
        {
            if (_startColumn == null || _startView == null || !_startView.Visible || _layingOutStart)
                return;
            _layingOutStart = true;
            try
            {
                int available = _startView.ClientSize.Width - 48;
                int width = Math.Max(320, Math.Min(680, available));
                Control crate = _startColumn.GetControlFromPosition(0, 0);
                int crateWidth = crate == null ? 0 : crate.Width + crate.Margin.Horizontal;
                int textWidth = Math.Max(200, width - crateWidth);
                if (_startColumn.ColumnStyles[1].Width != textWidth)
                    _startColumn.ColumnStyles[1] = new ColumnStyle(SizeType.Absolute, textWidth);
                Size preferred = _startColumn.GetPreferredSize(new Size(width, 0));
                int left = Math.Max(24, (_startView.ClientSize.Width - width) / 2);
                int top = Math.Max(24, (_startView.ClientSize.Height - preferred.Height) / 3);
                _startColumn.SetBounds(
                    left + _startView.AutoScrollPosition.X,
                    top + _startView.AutoScrollPosition.Y,
                    width,
                    preferred.Height);
                _startView.AutoScrollMinSize = new Size(0, top + preferred.Height + 24);
            }
            finally
            {
                _layingOutStart = false;
            }
        }

        private void BuildWorkspace()
        {
            _workspace = new Panel { Dock = DockStyle.Fill, Visible = false };
            ManagerTheme.SetRole(_workspace, ThemeRole.Window);
            BuildProjectBar();
            _notice = new NoticeBanner { Dock = DockStyle.Top };

            _tabs = new ThemedTabControl
            {
                Dock = DockStyle.Fill,
                StripStyle = TabStripStyle.Section,
                Padding = new Point(14, 7),
                Font = ManagerTheme.UiFont(9.5f),
                AccessibleName = "Content Workshop sections"
            };
            _projectPage = BuildProjectPage();
            _itemsPage = BuildItemsPage();
            _recipesPage = BuildRecipesPage();
            _iconPage = BuildIconPage();
            _checkPage = BuildCheckPage();
            _tabs.TabPages.AddRange(new TabPage[] { _projectPage, _itemsPage, _recipesPage, _iconPage, _checkPage });
            _tabs.SelectedIndexChanged += delegate { OnSectionChanged(); };

            _workspace.Controls.Add(_tabs);
            _workspace.Controls.Add(_notice);
            _workspace.Controls.Add(_projectBar);
            _projectBar.TabIndex = 0;
            _notice.TabIndex = 1;
            _tabs.TabIndex = 2;
        }

        private void BuildProjectBar()
        {
            _projectBar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(16, 10, 16, 8),
                Margin = new Padding(0)
            };
            _projectBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _projectBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _projectBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _projectBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _projectBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _projectTitle = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 28,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ManagerTheme.Display(13f),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            _dirtyChip = new Label
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Font = ManagerTheme.Semibold(9f),
                Margin = new Padding(12, 0, 16, 0),
                UseMnemonic = false
            };
            _projectMeta = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 20,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ManagerTheme.UiFont(9f),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            ManagerTheme.SetRole(_projectTitle, ThemeRole.Heading);
            ManagerTheme.SetRole(_projectMeta, ThemeRole.Muted);

            _newButton = UiFactory.Button("New pack", ActionButtonKind.Quiet, NewProject);
            _openButton = UiFactory.Button("Open...", ActionButtonKind.Quiet, OpenProject);
            _saveButton = UiFactory.Button("Save", ActionButtonKind.Primary, SaveProject);
            _saveButton.MinimumSize = new Size(88, 32);
            _toolTip.SetToolTip(_newButton, "Start a new project in an empty folder (Ctrl+N)");
            _toolTip.SetToolTip(_openButton, "Open another Content Workshop project (Ctrl+O)");
            _toolTip.SetToolTip(_saveButton, "Save the project to its folder (Ctrl+S)");
            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0),
                Anchor = AnchorStyles.Right
            };
            _newButton.Margin = new Padding(0, 0, 4, 0);
            _openButton.Margin = new Padding(0, 0, 12, 0);
            _saveButton.Margin = new Padding(0);
            actions.Controls.Add(_newButton);
            actions.Controls.Add(_openButton);
            actions.Controls.Add(_saveButton);

            _projectBar.Controls.Add(_projectTitle, 0, 0);
            _projectBar.Controls.Add(_dirtyChip, 1, 0);
            _projectBar.Controls.Add(actions, 2, 0);
            _projectBar.SetRowSpan(actions, 2);
            _projectBar.Controls.Add(_projectMeta, 0, 1);
            _projectBar.SetColumnSpan(_projectMeta, 2);
            _projectBar.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(ManagerTheme.Current.Border))
                    e.Graphics.DrawLine(pen, 0, _projectBar.Height - 1, _projectBar.Width, _projectBar.Height - 1);
            };
        }

        // ------------------------------------------------------------ project page

        private TabPage BuildProjectPage()
        {
            TabPage page = CreatePage("Project");
            Panel scroll = CreateScrollHost();
            TableLayoutPanel stack = CreateStack();
            stack.MaximumSize = new Size(860, 0);

            CardPanel details = new CardPanel("Project details", null);
            TableLayoutPanel grid = UiFactory.FormGrid();
            _modName = UiFactory.AddField(grid, "Pack &name", UiFactory.TextField(), null);
            _authors = UiFactory.AddField(grid, "&Authors", UiFactory.TextField(), "Your name. Separate several with commas.");
            TextBox version = UiFactory.TextField();
            version.Dock = DockStyle.None;
            version.Anchor = AnchorStyles.Left;
            version.Width = 160;
            _version = UiFactory.AddField(grid, "&Version", version, null);
            _description = UiFactory.AddField(grid, "&Description", UiFactory.MultilineField(4), null);
            int idRow = grid.Controls.Count;
            _modId = UiFactory.AddField(grid, "Pack &ID", UiFactory.TextField(),
                "Filled in from your name and the pack name. Keep it the same once people use the pack.");
            MarkDeveloperRow(_modId, grid, idRow);
            _modIdWarning = UiFactory.Text(string.Empty, ThemeRole.Warning, 9f, true);
            _modIdWarning.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _modIdWarning.Margin = new Padding(0, 0, 0, 8);
            _modIdWarning.Visible = false;
            AddGridRow(grid, _modIdWarning);
            details.AddBody(grid);

            CardPanel folder = new CardPanel("Saved in", null);
            TableLayoutPanel folderRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _projectPathBox = UiFactory.TextField();
            _projectPathBox.ReadOnly = true;
            _projectPathBox.AccessibleName = "Project folder path";
            _projectPathBox.Margin = new Padding(0, 3, 8, 3);
            ActionButton openFolder = UiFactory.Button("Open folder", ActionButtonKind.Secondary, OpenProjectFolder);
            openFolder.Margin = new Padding(0);
            folderRow.Controls.Add(UiFactory.Framed(_projectPathBox), 0, 0);
            folderRow.Controls.Add(openFolder, 1, 0);
            folder.AddBody(folderRow);

            AddStackRow(stack, details);
            AddStackRow(stack, folder);
            scroll.Controls.Add(stack);
            page.Controls.Add(scroll);

            foreach (TextBox field in new TextBox[] { _modId, _modName, _version, _authors, _description })
                field.TextChanged += IdentityFieldChanged;
            return page;
        }

        // ------------------------------------------------------------ items page

        private TabPage BuildItemsPage()
        {
            TabPage page = CreatePage("Items");
            SplitContainer split = CreateSplit();

            ActionButton add = UiFactory.Button("Add item", ActionButtonKind.Secondary, delegate { AddItem(); });
            _removeItemButton = UiFactory.Button("Remove", ActionButtonKind.Quiet, delegate { RemoveItem(); });
            _toolTip.SetToolTip(_removeItemButton, "Remove the selected item from the project");
            _itemList = CreateLibraryList("Items in this project");
            _itemList.DrawItem += DrawItemRow;
            _itemList.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    LoadSelectedItem();
                UpdateCommandStates();
            };
            _itemListEmpty = CreateOverlayLabel("No items yet.\r\nUse Add item to create your first one.");
            split.Panel1.Controls.Add(BuildLibrary("Items", null, _itemList, _itemListEmpty, add, _removeItemButton));

            _itemEditor = new Panel { Dock = DockStyle.Fill };
            _itemHeaderIcon = new IconPreview { Size = new Size(44, 44), Margin = new Padding(0, 0, 12, 0), EmptyText = string.Empty };
            _itemEditorHeading = CreateEditorHeading();
            _itemIdLabel = CreateEditorSubheading();
            Control header = BuildEditorHeader(_itemHeaderIcon, _itemEditorHeading, _itemIdLabel);

            _itemScroll = CreateScrollHost();
            _itemScroll.Padding = new Padding(0, 0, 6, 8);
            CardColumns cards = new CardColumns { Dock = DockStyle.Top, TwoColumnMinWidth = 700 };

            CardPanel identity = new CardPanel("Identity", null);
            TableLayoutPanel identityGrid = UiFactory.FormGrid();
            AddItemField(identityGrid, "displayName", "&Name", UiFactory.TextField(), null);
            AddItemField(identityGrid, "description", "&Description", UiFactory.MultilineField(3), "Shown to players in game.");
            AddItemField(identityGrid, "category", "&Category", UiFactory.ChoiceField(ItemCategories), null);
            AddItemField(identityGrid, "id", "Item &ID", UiFactory.TextField(), "Filled in from the name.", true);
            identity.AddBody(identityGrid);

            CardPanel icon = new CardPanel("Icon", "Square PNG images work best, ideally 32 by 32 pixels.");
            icon.AddBody(BuildIconRow());

            CardPanel inventory = new CardPanel("Inventory & value", null);
            TableLayoutPanel inventoryGrid = UiFactory.FormGrid();
            AddItemField(inventoryGrid, "stackSize", "Stack size", UiFactory.NumberField(1, 9999, 0), null);
            AddItemField(inventoryGrid, "tradeValue", "Trade value", UiFactory.NumberField(0, 1000000, 0), null);
            AddItemField(inventoryGrid, "burnValue", "Burn value", UiFactory.NumberField(0, 1000000, 2), null, true);
            AddItemField(inventoryGrid, "scrapValue", "Scrap value", UiFactory.NumberField(0, 1000000, 2), null, true);
            AddItemField(inventoryGrid, "loadCarrySlots", "Carry slots", UiFactory.NumberField(0, 9999, 0), null, true);
            inventory.AddBody(inventoryGrid);

            CardPanel crafting = DeveloperOnly(new CardPanel("Crafting & fabrication", null));
            TableLayoutPanel craftingGrid = UiFactory.FormGrid();
            AddItemField(craftingGrid, "baseCraftTime", "Base craft time", UiFactory.NumberField(0, 1000000, 2), null);
            AddItemField(craftingGrid, "craftStackSize", "Craft output count", UiFactory.NumberField(1, 9999, 0), null);
            AddItemField(craftingGrid, "fabricationCost", "Fabrication cost", UiFactory.NumberField(0, 1000000, 2), null);
            AddItemField(craftingGrid, "fabricationTime", "Fabrication time", UiFactory.NumberField(0, 1000000, 2), null);
            crafting.AddBody(craftingGrid);

            CardPanel food = new CardPanel("Food", null);
            _foodCard = food;
            TableLayoutPanel foodGrid = UiFactory.FormGrid();
            AddItemField(foodGrid, "rationValue", "Food value", UiFactory.NumberField(0, 1000000, 0), null);
            AddItemField(foodGrid, "contamination", "Contamination (0 to 1)", UiFactory.NumberField(0, 1, 3), null, true);
            AddItemField(foodGrid, "rawFood", "Raw food", CreateCheck(), null, true);
            AddItemField(foodGrid, "cookedMultiplier", "Cooked hunger multiplier", UiFactory.NumberField(0.01m, 1000, 3), null, true);
            food.AddBody(foodGrid);

            CardPanel recycling = new CardPanel("Recycles into", "What players get back when they recycle this item.");
            MaterialListEditor recycled = new MaterialListEditor(_itemCatalog, "material");
            recycling.AddBody(recycled);
            _itemFields.Add("recycling", recycled);
            _fieldLabels["item.recycling"] = "Recycles into";

            cards.AddCard(identity);
            cards.AddCard(icon);
            cards.AddCard(inventory);
            cards.AddCard(crafting);
            cards.AddCard(food);
            cards.AddCard(recycling);
            _itemScroll.Controls.Add(cards);
            _itemEditor.Controls.Add(_itemScroll);
            _itemEditor.Controls.Add(header);
            header.TabIndex = 0;
            _itemScroll.TabIndex = 1;

            _itemEmpty = CreateEmptyState("Add item", delegate { AddItem(); }, out _itemEmptyTitle, out _itemEmptyMessage);
            Panel editorHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 0, 0) };
            editorHost.Controls.Add(_itemEditor);
            editorHost.Controls.Add(_itemEmpty);
            split.Panel2.Controls.Add(editorHost);
            page.Controls.Add(split);
            WireFields(_itemFields, ItemFieldChanged);
            return page;
        }

        private Control BuildIconRow()
        {
            TableLayoutPanel row = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _itemIconPreview = new IconPreview { Size = new Size(72, 72), Margin = new Padding(0, 0, 14, 0) };

            TableLayoutPanel details = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            TextBox path = UiFactory.TextField();
            path.ReadOnly = true;
            path.AccessibleName = "Icon file";
            path.Margin = new Padding(0, 0, 0, 8);
            ActionButton import = UiFactory.Button("Import PNG...", ActionButtonKind.Secondary, ImportIcon);
            ActionButton edit = UiFactory.Button("Edit in Icon Studio", ActionButtonKind.Secondary, OpenIconStudio);
            AddStackRow(details, DeveloperOnly(UiFactory.Framed(path)));
            AddStackRow(details, UiFactory.ButtonRow(import, edit));
            row.Controls.Add(_itemIconPreview, 0, 0);
            row.Controls.Add(details, 1, 0);
            _itemFields.Add("iconPath", path);
            _fieldLabels["item.iconPath"] = "Icon";
            return row;
        }

        // ------------------------------------------------------------ recipes page

        private TabPage BuildRecipesPage()
        {
            TabPage page = CreatePage("Recipes");
            SplitContainer split = CreateSplit();

            ActionButton add = UiFactory.Button("Add recipe", ActionButtonKind.Secondary, delegate { AddRecipe(); });
            _removeRecipeButton = UiFactory.Button("Remove", ActionButtonKind.Quiet, delegate { RemoveRecipe(); });
            _toolTip.SetToolTip(_removeRecipeButton, "Remove the selected recipe from the project");
            _recipeList = CreateLibraryList("Recipes in this project");
            _recipeList.DrawItem += DrawRecipeRow;
            _recipeList.SelectedIndexChanged += delegate
            {
                if (!_loading)
                    LoadSelectedRecipe();
                UpdateCommandStates();
            };
            _recipeListEmpty = CreateOverlayLabel("No recipes yet.");
            split.Panel1.Controls.Add(BuildLibrary("Recipes", null, _recipeList, _recipeListEmpty, add, _removeRecipeButton));

            _recipeEditor = new Panel { Dock = DockStyle.Fill };
            _recipeHeaderIcon = new IconPreview { Size = new Size(44, 44), Margin = new Padding(0, 0, 12, 0), EmptyText = string.Empty };
            _recipeEditorHeading = CreateEditorHeading();
            _recipeIdLabel = CreateEditorSubheading();
            Control header = BuildEditorHeader(_recipeHeaderIcon, _recipeEditorHeading, _recipeIdLabel);

            _recipeScroll = CreateScrollHost();
            _recipeScroll.Padding = new Padding(0, 0, 6, 8);
            CardColumns cards = new CardColumns { Dock = DockStyle.Top, TwoColumnMinWidth = 700 };

            CardPanel settings = new CardPanel("Recipe", null);
            TableLayoutPanel grid = UiFactory.FormGrid();
            ItemPicker result = new ItemPicker(_itemCatalog) { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            AddRecipeField(grid, "resultItemId", "&Makes", result, null);
            AddRecipeField(grid, "station", "Crafted &at", UiFactory.ChoiceField(new string[] { "Workbench", "Laboratory", "AmmoPress" }), null);
            AddRecipeField(grid, "level", "Station level", UiFactory.NumberField(1, 5, 0), null);
            AddRecipeField(grid, "craftTime", "Craft time (seconds)", UiFactory.NumberField(0.01m, 1000000, 2), null);
            AddRecipeField(grid, "unique", "Unique result", CreateCheck(), null, true);
            CheckBox locked = CreateCheck();
            AddRecipeField(grid, "locked", "Starts locked", locked, null, true);
            TextBox unlockFlag = UiFactory.TextField();
            AddRecipeField(grid, "unlockFlag", "Unlock flag", unlockFlag, null, true);
            // The flag only matters for locked recipes; the stored value is kept either way.
            unlockFlag.Enabled = false;
            locked.CheckedChanged += delegate { unlockFlag.Enabled = locked.Checked; };
            AddRecipeField(grid, "id", "Recipe &ID", UiFactory.TextField(), "Filled in from what it makes.", true);
            settings.AddBody(grid);

            CardPanel ingredients = new CardPanel("Materials needed", null);
            MaterialListEditor needed = new MaterialListEditor(_itemCatalog, "material");
            ingredients.AddBody(needed);
            _recipeFields.Add("ingredients", needed);
            _fieldLabels["recipe.ingredients"] = "Materials needed";

            cards.AddCard(settings);
            cards.AddCard(ingredients);
            _recipeScroll.Controls.Add(cards);
            _recipeEditor.Controls.Add(_recipeScroll);
            _recipeEditor.Controls.Add(header);
            header.TabIndex = 0;
            _recipeScroll.TabIndex = 1;

            _recipeEmpty = CreateEmptyState("Add recipe", delegate { AddRecipe(); }, out _recipeEmptyTitle, out _recipeEmptyMessage);
            Panel editorHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 0, 0) };
            editorHost.Controls.Add(_recipeEditor);
            editorHost.Controls.Add(_recipeEmpty);
            split.Panel2.Controls.Add(editorHost);
            page.Controls.Add(split);
            WireFields(_recipeFields, RecipeFieldChanged);
            return page;
        }

        // ------------------------------------------------------------ icon studio

        private TabPage BuildIconPage()
        {
            TabPage page = CreatePage("Icon Studio");
            _iconWorkArea = new Panel { Dock = DockStyle.Fill };

            Panel sidebar = CreateScrollHost();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = 268;
            sidebar.Padding = new Padding(0, 0, 14, 8);
            TableLayoutPanel stack = CreateStack();

            CardPanel target = new CardPanel("Item", null);
            _iconItemPicker = UiFactory.ChoiceField(null);
            _iconItemPicker.AccessibleName = "Item that receives the icon";
            _iconItemPicker.Margin = new Padding(0, 0, 0, 4);
            _iconItemPicker.SelectedIndexChanged += IconItemPickerChanged;
            _iconPickerHelp = UiFactory.Help("Saving assigns the PNG to this item.");
            target.AddBody(_iconItemPicker);
            target.AddBody(_iconPickerHelp);

            CardPanel start = new CardPanel("Start from", null);
            start.AddBody(FullWidthButton("Load assigned icon", ActionButtonKind.Secondary, LoadSelectedIcon));
            start.AddBody(FullWidthButton("Import PNG...", ActionButtonKind.Secondary, ImportIcon));
            start.AddBody(FullWidthButton("New blank 32 x 32", ActionButtonKind.Secondary, NewIcon));

            CardPanel draw = new CardPanel("Draw", "P paint, E erase, I pick. Ctrl+scroll zooms.");
            _paintToolButton = ToolButton("Paint", "Paint tool (P)", SelectPaintTool);
            _eraseToolButton = ToolButton("Erase", "Erase tool (E)", SelectEraseTool);
            _pickToolButton = ToolButton("Pick", "Pick color tool (I)", SelectPickTool);
            draw.AddBody(EqualColumns(_paintToolButton, _eraseToolButton, _pickToolButton));
            _colorSwatch = new Panel
            {
                Size = new Size(38, 32),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0),
                AccessibleName = "Current color",
                AccessibleRole = AccessibleRole.Graphic
            };
            _colorSwatch.Click += ChooseColor;
            TableLayoutPanel colorRow = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 6, 0, 0)
            };
            colorRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            colorRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            ActionButton chooseColor = UiFactory.Button("Choose color...", ActionButtonKind.Secondary, ChooseColor);
            chooseColor.AutoSize = false;
            chooseColor.Dock = DockStyle.Fill;
            chooseColor.Margin = new Padding(0);
            colorRow.Controls.Add(_colorSwatch, 0, 0);
            colorRow.Controls.Add(chooseColor, 1, 0);
            draw.AddBody(colorRow);

            CardPanel finish = new CardPanel("Finish", null);
            _undoButton = ToolButton("Undo", "Undo (Ctrl+Z)", delegate { _pixelCanvas.Undo(); UpdatePixelStatus(); });
            _redoButton = ToolButton("Redo", "Redo (Ctrl+Y)", delegate { _pixelCanvas.Redo(); UpdatePixelStatus(); });
            ActionButton fit = ToolButton("Fit", "Zoom the icon to fit the canvas", FitIconCanvas);
            _undoButton.IsToggle = false;
            _redoButton.IsToggle = false;
            fit.IsToggle = false;
            finish.AddBody(EqualColumns(_undoButton, _redoButton, fit));
            _saveIconButton = FullWidthButton("Save to item", ActionButtonKind.Primary, SaveIcon);
            _saveIconButton.Margin = new Padding(0, 8, 0, 0);
            finish.AddBody(_saveIconButton);

            AddStackRow(stack, target);
            AddStackRow(stack, start);
            AddStackRow(stack, draw);
            AddStackRow(stack, finish);
            sidebar.Controls.Add(stack);

            Panel canvasArea = new Panel { Dock = DockStyle.Fill };
            _canvasFrame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            _borderFrames.Add(_canvasFrame);
            _pixelCanvas = new PixelEditorCanvas
            {
                Dock = DockStyle.Fill,
                AccessibleName = "Icon canvas",
                AccessibleDescription = "Click or drag to draw. P paint, E erase, I pick color, Ctrl+Z undo, Ctrl+Y redo, Ctrl+scroll zoom."
            };
            _pixelCanvas.ActiveColorChanged += delegate { UpdatePixelStatus(); };
            _pixelCanvas.ActiveToolChanged += delegate { UpdateToolButtons(); };
            _pixelCanvas.DocumentChanged += delegate
            {
                UpdatePixelStatus();
                UpdateDirtyState();
            };
            _canvasFrame.Controls.Add(_pixelCanvas);

            TableLayoutPanel status = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                ColumnCount = 2,
                RowCount = 1,
                Height = 30,
                Margin = new Padding(0)
            };
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            status.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _pixelStatus = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                UseMnemonic = false,
                Margin = new Padding(0),
                Font = ManagerTheme.UiFont(9f)
            };
            Label hint = UiFactory.Text("Ctrl+Z undo  ·  Ctrl+Y redo", ThemeRole.Muted, 8.75f, false);
            hint.Anchor = AnchorStyles.Right;
            hint.Margin = new Padding(12, 0, 0, 0);
            status.Controls.Add(_pixelStatus, 0, 0);
            status.Controls.Add(hint, 1, 0);

            canvasArea.Controls.Add(_canvasFrame);
            canvasArea.Controls.Add(status);
            _iconWorkArea.Controls.Add(canvasArea);
            _iconWorkArea.Controls.Add(sidebar);
            sidebar.TabIndex = 0;
            canvasArea.TabIndex = 1;

            Label emptyTitle;
            Label emptyMessage;
            _iconEmpty = CreateEmptyState("Go to Items", delegate { ShowSection(_itemsPage); }, out emptyTitle, out emptyMessage);
            emptyTitle.Text = "No items to draw for";
            emptyMessage.Text = "Icons are saved to an item. Add an item first, then come back to draw its icon.";

            page.Controls.Add(_iconWorkArea);
            page.Controls.Add(_iconEmpty);
            SelectPixelTool(PixelEditorTool.Paint);
            return page;
        }

        // ------------------------------------------------------------ check & export

        private TabPage BuildCheckPage()
        {
            TabPage page = CreatePage("Install & share");
            TableLayoutPanel columns = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            CardPanel validation = new CardPanel(null, null);
            validation.FillsHeight = true;
            validation.Dock = DockStyle.Fill;
            validation.Margin = new Padding(0, 0, 14, 0);

            TableLayoutPanel header = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            TableLayoutPanel headings = CreateStack();
            headings.Dock = DockStyle.Fill;
            _validationTitle = UiFactory.Heading("Not checked yet", 12f);
            _validationSummary = UiFactory.Help(string.Empty);
            AddStackRow(headings, _validationTitle);
            AddStackRow(headings, _validationSummary);
            _runCheckButton = UiFactory.Button("Run check", ActionButtonKind.Primary, ValidateProject);
            _runCheckButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _runCheckButton.Margin = new Padding(12, 0, 0, 0);
            _toolTip.SetToolTip(_runCheckButton, "Check for missing fields, broken item references, and missing icon files");
            header.Controls.Add(headings, 0, 0);
            header.Controls.Add(_runCheckButton, 1, 0);
            validation.AddBody(header);

            _validationList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawVariable,
                IntegralHeight = false,
                Font = ManagerTheme.UiFont(9f),
                AccessibleName = "Check results",
                AccessibleDescription = "Press Enter or double-click a result to open the field it refers to."
            };
            _validationList.MeasureItem += MeasureIssueRow;
            _validationList.DrawItem += DrawIssueRow;
            _validationList.DoubleClick += delegate { NavigateToIssue(); };
            _validationList.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    NavigateToIssue();
                    e.Handled = true;
                }
            };
            _validationList.Resize += delegate
            {
                if (_validationList.ClientSize.Width != _validationListWidth && _validationList.Items.Count > 0)
                {
                    _validationListWidth = _validationList.ClientSize.Width;
                    int selected = _validationList.SelectedIndex;
                    RebuildIssueRows();
                    if (selected >= 0 && selected < _validationList.Items.Count)
                        _validationList.SelectedIndex = selected;
                }
            };
            _validationEmpty = CreateOverlayLabel("Run a check to find anything missing before you share the pack.");
            Panel listFrame = CreateFrame(_validationList, _validationEmpty);
            listFrame.Margin = new Padding(0, 10, 0, 0);
            listFrame.MinimumSize = new Size(0, 90);
            validation.AddBody(listFrame);

            CardPanel export = new CardPanel("Share", "The pack is saved and checked first.");
            export.Dock = DockStyle.Top;
            export.Margin = new Padding(0);
            Panel exportScroll = CreateScrollHost();
            exportScroll.Margin = new Padding(0);
            _installButton = FullWidthButton("Install to my game", ActionButtonKind.Secondary, InstallLocally);
            _installDescription = UiFactory.Help(string.Empty);
            _exportZipButton = FullWidthButton("Save as ZIP...", ActionButtonKind.Secondary, ExportZip);
            Label zipHelp = UiFactory.Help("One file to share or upload to Nexus.");
            _exportFolderButton = DeveloperOnly(FullWidthButton("Export folder...", ActionButtonKind.Secondary, ExportFolder));
            Label folderHelp = DeveloperOnly(UiFactory.Help("Copies the finished mod folder to a location you choose."));
            export.AddBody(_installButton);
            export.AddBody(_installDescription);
            export.AddBody(_exportZipButton);
            export.AddBody(zipHelp);
            export.AddBody(_exportFolderButton);
            export.AddBody(folderHelp);

            exportScroll.Controls.Add(export);
            columns.Controls.Add(validation, 0, 0);
            columns.Controls.Add(exportScroll, 1, 0);
            page.Controls.Add(columns);
            return page;
        }

        // ------------------------------------------------------------ state → view

        private void UpdateViewState()
        {
            bool hasProject = _project != null;
            if (!hasProject)
                RefreshRecentPacks();
            _startView.Visible = !hasProject;
            _workspace.Visible = hasProject;
            if (!hasProject)
            {
                _notice.Clear();
                SetStatus("Start a new pack or open one.");
            }
            else if (_workspaceStatus.Text == "Start a new pack or open one.")
            {
                SetStatus("Editing " + ProjectDisplayName() + ".");
            }
            UpdateProjectHeader();
            UpdateSectionTitles();
            UpdateListStates();
            UpdateValidationSummary();
            UpdateExportDescriptions();
            UpdateWorkspaceStatus();
            UpdateCommandStates();
            LayoutStartView();
        }

        private void UpdateProjectHeader()
        {
            if (_projectTitle == null)
                return;
            if (_project == null)
            {
                _projectTitle.Text = string.Empty;
                _projectMeta.Text = string.Empty;
                _modIdWarning.Visible = false;
                return;
            }
            _projectTitle.Text = ProjectDisplayName();
            string id = _modId.Text.Trim();
            string version = _version.Text.Trim();
            string[] people = SplitCommaList(_authors.Text);
            _projectMeta.Text = (version.Length > 0 ? "Version " + version : "No version yet") +
                (people.Length > 0 ? "   ·   by " + string.Join(", ", people) : string.Empty);
            _toolTip.SetToolTip(_projectMeta, "Pack ID: " + (id.Length > 0 ? id : "none") + "\r\nFolder: " + _project.RootPath);
            bool placeholder = id.StartsWith(Manager.Core.Services.ContentWorkshopIds.PlaceholderOwner + ".", StringComparison.Ordinal);
            bool needsId = id.Length == 0 || placeholder || string.Equals(id, ExampleModId, StringComparison.Ordinal);
            if (id.Length == 0)
                _modIdWarning.Text = "The pack needs an ID before it can be saved.";
            else if (placeholder)
                _modIdWarning.Text = "Add your name under Authors to finish the pack ID.";
            else
                _modIdWarning.Text = "This is the example ID. Replace it before you share the pack.";
            _modIdWarning.Visible = needsId;
        }

        private void UpdateDirtyChip()
        {
            if (_dirtyChip == null)
                return;
            if (_project == null)
            {
                _dirtyChip.Text = string.Empty;
                return;
            }
            if (!_project.IsDirty)
                _hasSavedCopy = true;
            bool dirty = HasUnsavedChanges;
            if (dirty)
                _dirtyChip.Text = _hasSavedCopy ? "Unsaved changes" : "Not saved yet";
            else
                _dirtyChip.Text = "All changes saved";
            _dirtyChip.AccessibleName = _dirtyChip.Text;
            ManagerTheme.SetRole(_dirtyChip, dirty ? ThemeRole.Warning : ThemeRole.Muted);
        }

        private void UpdateCommandStates()
        {
            if (_saveButton == null)
                return;
            bool idle = !_busy;
            bool hasProject = _project != null;
            _newButton.Enabled = idle;
            _openButton.Enabled = idle;
            _startNewButton.Enabled = idle;
            _startOpenButton.Enabled = idle;
            _saveButton.Enabled = idle && hasProject && HasUnsavedChanges;
            _runCheckButton.Enabled = idle && hasProject;
            _installButton.Enabled = idle && hasProject;
            _exportZipButton.Enabled = idle && hasProject;
            _exportFolderButton.Enabled = idle && hasProject;
            _removeItemButton.Enabled = idle && hasProject && _itemList.SelectedIndex >= 0;
            _removeRecipeButton.Enabled = idle && hasProject && _recipeList.SelectedIndex >= 0;
        }

        private void UpdateSectionTitles()
        {
            if (_tabs == null)
                return;
            int items = _project == null ? 0 : _project.Content.items.Count;
            int recipes = _project == null ? 0 : _project.Content.recipes.Count;
            SetPageText(_itemsPage, "Items (" + items + ")");
            SetPageText(_recipesPage, "Recipes (" + recipes + ")");
            string check = "Install & share";
            if (_lastValidation != null && !_lastValidation.IsValid)
                check += " (" + FormatCount(_lastValidation.ErrorCount, "error") + ")";
            SetPageText(_checkPage, check);
        }

        private static void SetPageText(TabPage page, string text)
        {
            if (page != null && page.Text != text)
                page.Text = text;
        }

        private void UpdateListStates()
        {
            if (_itemList == null)
                return;
            bool hasProject = _project != null;
            int itemCount = hasProject ? _project.Content.items.Count : 0;
            _itemListEmpty.Visible = hasProject && _itemList.Items.Count == 0;
            _recipeListEmpty.Text = itemCount == 0
                ? "No recipes yet.\r\nAdd an item first, then a recipe that makes it."
                : "No recipes yet.\r\nUse Add recipe to create one.";
            _recipeListEmpty.Visible = hasProject && _recipeList.Items.Count == 0;
            _validationEmpty.Visible = _validationList.Items.Count == 0;
            bool canDraw = hasProject && itemCount > 0;
            _iconEmpty.Visible = !canDraw;
            _iconWorkArea.Visible = canDraw;
            UpdateItemEditor();
            UpdateRecipeEditor();
        }

        private void UpdateItemEditor()
        {
            if (_itemEditor == null)
                return;
            bool hasItem = _project != null && _loadedItemIndex >= 0 && _loadedItemIndex < _project.Content.items.Count;
            _itemEditor.Visible = hasItem;
            _itemEmpty.Visible = !hasItem;
            bool none = _project == null || _project.Content.items.Count == 0;
            _itemEmptyTitle.Text = none ? "No items yet" : "No item selected";
            _itemEmptyMessage.Text = none
                ? "Add an item to start. Each item needs a display name, a category, and an icon."
                : "Choose an item from the list to edit it, or add a new one.";
            LayoutEmptyState(_itemEmpty);
            if (!hasItem)
                return;
            UpdateItemEditorHeader();
            Image thumbnail = GetThumbnail(_project.Content.items[_loadedItemIndex]);
            _itemIconPreview.Image = thumbnail == null ? null : new Bitmap(thumbnail);
            _itemHeaderIcon.Image = thumbnail == null ? null : new Bitmap(thumbnail);
        }

        private void UpdateItemEditorHeader()
        {
            if (_project == null || _loadedItemIndex < 0 || _loadedItemIndex >= _project.Content.items.Count)
                return;
            ContentPackItem item = _project.Content.items[_loadedItemIndex];
            UpdateFoodCard(item);
            _itemEditorHeading.Text = ItemName(item);
            _itemIdLabel.Text = string.IsNullOrEmpty(item.category) ? "Pick a category below" : item.category;
        }

        private void UpdateRecipeEditor()
        {
            if (_recipeEditor == null)
                return;
            bool hasRecipe = _project != null && _loadedRecipeIndex >= 0 && _loadedRecipeIndex < _project.Content.recipes.Count;
            _recipeEditor.Visible = hasRecipe;
            _recipeEmpty.Visible = !hasRecipe;
            bool none = _project == null || _project.Content.recipes.Count == 0;
            _recipeEmptyTitle.Text = none ? "No recipes yet" : "No recipe selected";
            _recipeEmptyMessage.Text = none
                ? "Recipes connect one of your items to a crafting station and a material cost."
                : "Choose a recipe from the list to edit it, or add a new one.";
            LayoutEmptyState(_recipeEmpty);
            if (hasRecipe)
                UpdateRecipeEditorHeader();
        }

        private void UpdateRecipeEditorHeader()
        {
            if (_project == null || _loadedRecipeIndex < 0 || _loadedRecipeIndex >= _project.Content.recipes.Count)
                return;
            ContentPackRecipe recipe = _project.Content.recipes[_loadedRecipeIndex];
            _recipeEditorHeading.Text = RecipeTitle(recipe);
            _recipeIdLabel.Text = string.IsNullOrEmpty(recipe.station)
                ? "Pick a crafting station below"
                : "Crafted at the " + StationText(recipe);
            Image thumbnail = GetThumbnail(FindItem(recipe.resultItemId));
            _recipeHeaderIcon.Image = thumbnail == null ? null : new Bitmap(thumbnail);
        }

        private void UpdateValidationSummary()
        {
            if (_validationTitle == null)
                return;
            ThemeRole titleRole = ThemeRole.Heading;
            string summary;
            if (_lastValidation == null)
            {
                _validationTitle.Text = "Not checked yet";
                summary = "Looks for anything missing before you install or share. Double-click a result to jump to it.";
            }
            else
            {
                if (_lastValidation.IsValid)
                {
                    _validationTitle.Text = "Ready to export";
                    titleRole = ThemeRole.Success;
                }
                else
                {
                    _validationTitle.Text = FormatCount(_lastValidation.ErrorCount, "error") + " to fix";
                    titleRole = ThemeRole.Danger;
                }
                summary = FormatCount(_lastValidation.ErrorCount, "error") + ", " +
                    FormatCount(ShownIssueCount(ContentPackValidationSeverity.Warning), "warning") + ". Checked at " +
                    _lastValidationTime.ToShortTimeString() + ".";
                if (_validationStale)
                    summary += " You have edited the project since, so run the check again.";
                else if (!_lastValidation.IsValid)
                    summary += " Double-click a result to jump to it.";
            }
            _validationSummary.Text = summary;
            ManagerTheme.SetRole(_validationTitle, titleRole);
            bool passedAndCurrent = _lastValidation != null && _lastValidation.IsValid && !_validationStale;
            _runCheckButton.Kind = passedAndCurrent ? ActionButtonKind.Secondary : ActionButtonKind.Primary;
            _runCheckButton.Text = _lastValidation == null ? "Run check" : "Check again";
            _installButton.Kind = passedAndCurrent ? ActionButtonKind.Primary : ActionButtonKind.Secondary;
            UpdateSectionTitles();
        }

        private void UpdateExportDescriptions()
        {
            if (_installDescription == null)
                return;
            _installDescription.Text = string.IsNullOrEmpty(_modsPath)
                ? "Set up the game in Game setup first, or choose a mods folder when asked."
                : "Adds the pack to your mods folder and turns it on.";
            _toolTip.SetToolTip(_installButton, string.IsNullOrEmpty(_modsPath)
                ? "Choose a mods folder, then install the pack there"
                : "Install into " + _modsPath);
        }

        private void OnSectionChanged()
        {
            if (_tabs.SelectedTab == _iconPage)
            {
                SyncIconItemPicker();
                UpdatePixelStatus();
            }
        }

        private void ShowSection(TabPage page)
        {
            if (page != null && _tabs.SelectedTab != page)
                _tabs.SelectedTab = page;
        }

        private void FocusItemField(string key)
        {
            Control field;
            if (key != null && _itemFields.TryGetValue(key, out field))
                RevealDeveloperField(field);
            FocusField(_itemFields, key, _itemScroll);
        }

        private void FocusRecipeField(string key)
        {
            Control field;
            if (key != null && _recipeFields.TryGetValue(key, out field))
                RevealDeveloperField(field);
            FocusField(_recipeFields, key, _recipeScroll);
        }

        private static void FocusField(Dictionary<string, Control> fields, string key, ScrollableControl scroll)
        {
            Control field;
            if (string.IsNullOrEmpty(key) || !fields.TryGetValue(key, out field))
                return;
            if (scroll != null)
                scroll.ScrollControlIntoView(field);
            MaterialListEditor materials = field as MaterialListEditor;
            if (materials != null)
            {
                materials.FocusEntry(0);
                return;
            }
            field.Focus();
            TextBox text = field as TextBox;
            if (text != null)
                text.SelectAll();
        }

        private void InvalidateItemRow(int index)
        {
            if (index >= 0 && index < _itemList.Items.Count)
                _itemList.Invalidate(_itemList.GetItemRectangle(index));
        }

        private void InvalidateRecipeRow(int index)
        {
            if (index >= 0 && index < _recipeList.Items.Count)
                _recipeList.Invalidate(_recipeList.GetItemRectangle(index));
        }

        private void ApplyWorkshopTheme()
        {
            ThemePalette palette = ManagerTheme.Current;
            ManagerTheme.Apply(this);
            for (int i = 0; i < _borderFrames.Count; i++)
                _borderFrames[i].BackColor = palette.Border;
            for (int i = 0; i < _overlayLabels.Count; i++)
                _overlayLabels[i].BackColor = palette.Input;
            _pixelCanvas.BackColor = palette.IsDark ? Color.FromArgb(24, 25, 28) : Color.FromArgb(222, 225, 230);
            _colorSwatch.BackColor = _pixelCanvas.ActiveColor;
            _notice.ApplyTheme();
            UpdateDirtyChip();
            UpdateValidationSummary();
            UpdatePixelStatus();
            Invalidate(true);
        }

        // ------------------------------------------------------------ row painting

        private void DrawItemRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || _project == null || e.Index >= _project.Content.items.Count)
                return;
            ContentPackItem item = _project.Content.items[e.Index];
            string secondary = string.IsNullOrEmpty(item.category) ? "No category yet" : item.category;
            DrawLibraryRow(e, _itemList, GetThumbnail(item), ItemName(item), secondary);
        }

        private void DrawRecipeRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || _project == null || e.Index >= _project.Content.recipes.Count)
                return;
            ContentPackRecipe recipe = _project.Content.recipes[e.Index];
            int materials = recipe.ingredients == null ? 0 : recipe.ingredients.Count;
            DrawLibraryRow(e, _recipeList, GetThumbnail(FindItem(recipe.resultItemId)), RecipeTitle(recipe),
                StationText(recipe) + "   ·   " + FormatCount(materials, "material"));
        }

        private static void DrawLibraryRow(DrawItemEventArgs e, ListBox list, Image icon, string primary, string secondary)
        {
            ThemePalette palette = ManagerTheme.Current;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected ? palette.Selection : palette.Input;
            Color text = selected ? palette.SelectionText : palette.Text;
            Color muted = selected ? palette.SelectionText : palette.TextMuted;
            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            Rectangle iconBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + (e.Bounds.Height - 32) / 2, 32, 32);
            if (icon != null)
            {
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(icon, iconBounds);
                e.Graphics.InterpolationMode = InterpolationMode.Default;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Default;
            }
            else
            {
                using (SolidBrush well = new SolidBrush(palette.SurfaceAlt))
                    e.Graphics.FillRectangle(well, iconBounds);
                ManagerTheme.DrawOutline(e.Graphics, iconBounds, palette.Border);
            }

            int textLeft = iconBounds.Right + 10;
            int textWidth = Math.Max(10, e.Bounds.Right - textLeft - 8);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, primary, ManagerTheme.Semibold(9.5f),
                new Rectangle(textLeft, e.Bounds.Y + 5, textWidth, 20), text, flags);
            TextRenderer.DrawText(e.Graphics, secondary, ManagerTheme.UiFont(8.5f),
                new Rectangle(textLeft, e.Bounds.Y + 24, textWidth, 17), muted, flags);

            using (Pen divider = new Pen(palette.Border))
                e.Graphics.DrawLine(divider, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus && list.Focused)
            {
                using (Pen focus = new Pen(palette.AccentText, 2f))
                    e.Graphics.DrawRectangle(focus, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 3, e.Bounds.Height - 3);
            }
        }

        private void MeasureIssueRow(object sender, MeasureItemEventArgs e)
        {
            IssueRow row = _validationList.Items[e.Index] as IssueRow;
            if (row == null)
                return;
            int width = Math.Max(120, _validationList.ClientSize.Width - IssuePillWidth - 36);
            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            int height = 16;
            if (row.Target != null && !string.IsNullOrEmpty(row.Target.Label))
                height += TextRenderer.MeasureText(row.Target.Label, ManagerTheme.Semibold(9f), new Size(width, 0), flags).Height;
            height += TextRenderer.MeasureText(row.Message ?? string.Empty, ManagerTheme.UiFont(9f), new Size(width, 0), flags).Height;
            e.ItemHeight = Math.Min(255, Math.Max(40, height + 4));
        }

        private void DrawIssueRow(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _validationList.Items.Count)
                return;
            IssueRow row = _validationList.Items[e.Index] as IssueRow;
            if (row == null)
                return;
            ThemePalette palette = ManagerTheme.Current;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = selected ? palette.Selection : palette.Input;
            Color text = selected ? palette.SelectionText : palette.Text;
            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            Color pill = !row.Severity.HasValue
                ? palette.Success
                : (row.Severity.Value == ContentPackValidationSeverity.Error ? palette.Danger : palette.Warning);
            Rectangle pillBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 9, IssuePillWidth, 20);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = ManagerTheme.RoundedRectangle(pillBounds, 4))
            using (SolidBrush pillBrush = new SolidBrush(pill))
                e.Graphics.FillPath(pillBrush, path);
            e.Graphics.SmoothingMode = SmoothingMode.Default;
            TextRenderer.DrawText(e.Graphics, row.SeverityText.ToUpperInvariant(), ManagerTheme.Semibold(8f), pillBounds,
                palette.IsDark ? palette.Chrome : Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            int left = pillBounds.Right + 14;
            int width = Math.Max(40, e.Bounds.Right - left - 12);
            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            int y = e.Bounds.Y + 8;
            if (row.Target != null && !string.IsNullOrEmpty(row.Target.Label))
            {
                Size location = TextRenderer.MeasureText(row.Target.Label, ManagerTheme.Semibold(9f), new Size(width, 0), flags);
                TextRenderer.DrawText(e.Graphics, row.Target.Label, ManagerTheme.Semibold(9f),
                    new Rectangle(left, y, width, location.Height), text, flags);
                y += location.Height;
            }
            TextRenderer.DrawText(e.Graphics, row.Message ?? string.Empty, ManagerTheme.UiFont(9f),
                new Rectangle(left, y, width, Math.Max(1, e.Bounds.Bottom - y - 4)), text, flags);

            using (Pen divider = new Pen(palette.Border))
                e.Graphics.DrawLine(divider, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus && _validationList.Focused)
            {
                using (Pen focus = new Pen(palette.AccentText, 2f))
                    e.Graphics.DrawRectangle(focus, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 3, e.Bounds.Height - 3);
            }
        }

        // ------------------------------------------------------------ builders

        private static TabPage CreatePage(string text)
        {
            TabPage page = new TabPage(text)
            {
                Padding = new Padding(16, 14, 16, 12),
                UseVisualStyleBackColor = false
            };
            return page;
        }

        private static Panel CreateScrollHost()
        {
            return new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        }

        private static TableLayoutPanel CreateStack()
        {
            TableLayoutPanel stack = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            return stack;
        }

        private static void AddStackRow(TableLayoutPanel stack, Control control)
        {
            int row = stack.RowCount;
            stack.RowCount = row + 1;
            stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            if (control.Dock == DockStyle.None && control.Anchor == (AnchorStyles.Top | AnchorStyles.Left) &&
                !(control is FlowLayoutPanel) && !(control is Label))
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            stack.Controls.Add(control, 0, row);
        }

        private static void AddGridRow(TableLayoutPanel grid, Control control)
        {
            int row = grid.RowCount;
            grid.RowCount = row + 1;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(control, 1, row);
        }

        private static SplitContainer CreateSplit()
        {
            SplitContainer split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Size = new Size(1000, 600),
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 6,
                TabStop = false
            };
            split.Panel1MinSize = 220;
            split.Panel2MinSize = 300;
            split.SplitterDistance = 290;
            return split;
        }

        private ListBox CreateLibraryList(string accessibleName)
        {
            return new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 46,
                IntegralHeight = false,
                Font = ManagerTheme.UiFont(9.5f),
                AccessibleName = accessibleName
            };
        }

        private Label CreateOverlayLabel(string text)
        {
            Label label = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                UseMnemonic = false,
                Padding = new Padding(16),
                Visible = false,
                Font = ManagerTheme.UiFont(9f)
            };
            ManagerTheme.SetRole(label, ThemeRole.Muted);
            _overlayLabels.Add(label);
            return label;
        }

        private Panel CreateFrame(Control content, Control overlay)
        {
            Panel frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            frame.Controls.Add(content);
            if (overlay != null)
            {
                frame.Controls.Add(overlay);
                overlay.BringToFront();
            }
            _borderFrames.Add(frame);
            return frame;
        }

        private Control BuildLibrary(string title, string description, ListBox list, Label empty, ActionButton add, ActionButton remove)
        {
            TableLayoutPanel library = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            library.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            library.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            library.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            library.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            library.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Label heading = UiFactory.Heading(title, 11f);
            Label help = UiFactory.Help(description);
            help.Visible = !string.IsNullOrEmpty(description);
            Panel frame = CreateFrame(list, empty);
            frame.Margin = new Padding(0, 2, 0, 8);
            FlowLayoutPanel buttons = UiFactory.ButtonRow(add, remove);
            library.Controls.Add(heading, 0, 0);
            library.Controls.Add(help, 0, 1);
            library.Controls.Add(frame, 0, 2);
            library.Controls.Add(buttons, 0, 3);
            return library;
        }

        private static Label CreateEditorHeading()
        {
            Label label = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 26,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.BottomLeft,
                Font = ManagerTheme.Display(13f),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            ManagerTheme.SetRole(label, ThemeRole.Heading);
            return label;
        }

        private static Label CreateEditorSubheading()
        {
            Label label = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Height = 20,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft,
                Font = ManagerTheme.UiFont(9f),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            ManagerTheme.SetRole(label, ThemeRole.Muted);
            return label;
        }

        private static Control BuildEditorHeader(IconPreview icon, Label heading, Label subheading)
        {
            TableLayoutPanel header = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 0, 0, 12),
                Margin = new Padding(0)
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            header.Controls.Add(icon, 0, 0);
            header.SetRowSpan(icon, 2);
            header.Controls.Add(heading, 1, 0);
            header.Controls.Add(subheading, 1, 1);
            return header;
        }

        private Panel CreateEmptyState(string actionText, EventHandler action, out Label title, out Label message)
        {
            Panel host = new Panel { Dock = DockStyle.Fill, Visible = false };
            TableLayoutPanel column = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };
            column.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            title = UiFactory.Heading(string.Empty, 13f);
            message = UiFactory.Text(string.Empty, ThemeRole.Muted, 9.5f, false);
            message.MaximumSize = new Size(380, 0);
            message.Margin = new Padding(0, 4, 0, 14);
            ActionButton button = UiFactory.Button(actionText, ActionButtonKind.Primary, action);
            AddStackRow(column, title);
            AddStackRow(column, message);
            AddStackRow(column, UiFactory.ButtonRow(button));
            host.Controls.Add(column);
            host.Resize += delegate { LayoutEmptyState(host); };
            host.VisibleChanged += delegate { LayoutEmptyState(host); };
            return host;
        }

        private static void LayoutEmptyState(Panel host)
        {
            if (host == null || host.Controls.Count == 0 || !host.Visible)
                return;
            Control column = host.Controls[0];
            Size preferred = column.GetPreferredSize(Size.Empty);
            column.Size = preferred;
            column.Location = new Point(
                Math.Max(0, (host.ClientSize.Width - preferred.Width) / 2),
                Math.Max(8, (host.ClientSize.Height - preferred.Height) / 3));
        }

        private static ActionButton FullWidthButton(string text, ActionButtonKind kind, EventHandler click)
        {
            ActionButton button = UiFactory.Button(text, kind, click);
            button.AutoSize = false;
            button.Height = 34;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 0, 0, 6);
            return button;
        }

        private ActionButton ToolButton(string text, string tip, EventHandler click)
        {
            ActionButton button = UiFactory.Button(text, ActionButtonKind.Secondary, click);
            button.IsToggle = true;
            button.AutoSize = false;
            button.Height = 32;
            button.Dock = DockStyle.Fill;
            button.Padding = new Padding(4, 0, 4, 0);
            button.AccessibleName = tip;
            _toolTip.SetToolTip(button, tip);
            return button;
        }

        private static TableLayoutPanel EqualColumns(params Control[] controls)
        {
            TableLayoutPanel row = new TableLayoutPanel
            {
                ColumnCount = controls.Length,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };
            for (int i = 0; i < controls.Length; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / controls.Length));
                controls[i].Margin = new Padding(0, 0, i == controls.Length - 1 ? 0 : 6, 0);
                row.Controls.Add(controls[i], i, 0);
            }
            return row;
        }

        private static CheckBox CreateCheck()
        {
            return new ThemedCheckBox
            {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 0, 6)
            };
        }

        private void AddItemField(TableLayoutPanel grid, string key, string label, Control editor, string help)
        {
            AddItemField(grid, key, label, editor, help, false);
        }

        private void AddItemField(TableLayoutPanel grid, string key, string label, Control editor, string help, bool developer)
        {
            int first = grid.Controls.Count;
            UiFactory.AddField(grid, label, editor, help);
            if (developer)
                MarkDeveloperRow(editor, grid, first);
            _itemFields.Add(key, editor);
            _fieldLabels["item." + key] = UiFactory.StripMnemonic(label);
        }

        private void AddRecipeField(TableLayoutPanel grid, string key, string label, Control editor, string help)
        {
            AddRecipeField(grid, key, label, editor, help, false);
        }

        private void AddRecipeField(TableLayoutPanel grid, string key, string label, Control editor, string help, bool developer)
        {
            int first = grid.Controls.Count;
            UiFactory.AddField(grid, label, editor, help);
            if (developer)
                MarkDeveloperRow(editor, grid, first);
            _recipeFields.Add(key, editor);
            _fieldLabels["recipe." + key] = UiFactory.StripMnemonic(label);
        }

        private CardPanel _foodCard;

        /// <summary>Food settings only show for food items (or items that already have a food value).</summary>
        private void UpdateFoodCard(ContentPackItem item)
        {
            if (_foodCard == null)
                return;
            string category = item == null ? string.Empty : (item.category ?? string.Empty);
            bool food = category == "Food" || category == "Water" || category == "Meat" ||
                (item != null && item.ration != null && item.ration.value > 0);
            _foodCard.Visible = _developerMode || food;
        }

        // ------------------------------------------------------------ developer mode

        /// <summary>
        /// Registers the controls a field just added (label, editor, help) so
        /// they only show in developer mode.
        /// </summary>
        private void MarkDeveloperRow(Control editor, TableLayoutPanel grid, int firstControl)
        {
            List<Control> row = new List<Control>();
            for (int i = firstControl; i < grid.Controls.Count; i++)
                row.Add(grid.Controls[i]);
            _developerRows[editor] = row;
            _developerControls.AddRange(row);
        }

        private T DeveloperOnly<T>(T control) where T : Control
        {
            _developerControls.Add(control);
            return control;
        }

        private void ApplyDeveloperMode()
        {
            for (int i = 0; i < _developerControls.Count; i++)
                _developerControls[i].Visible = _developerMode;
            if (_project != null && _loadedItemIndex >= 0 && _loadedItemIndex < _project.Content.items.Count)
                UpdateFoodCard(_project.Content.items[_loadedItemIndex]);
        }

        /// <summary>
        /// A check result can point at a developer field; show that field so the
        /// problem can be fixed without switching modes.
        /// </summary>
        private void RevealDeveloperField(Control field)
        {
            if (_developerMode || field == null)
                return;
            List<Control> row;
            if (_developerRows.TryGetValue(field, out row))
                foreach (Control control in row)
                    control.Visible = true;
            for (Control parent = field.Parent; parent != null; parent = parent.Parent)
                if (_developerControls.Contains(parent))
                    parent.Visible = true;
        }

        private static void WireFields(Dictionary<string, Control> fields, EventHandler handler)
        {
            foreach (Control control in fields.Values)
            {
                ItemPicker picker = control as ItemPicker;
                MaterialListEditor materials = control as MaterialListEditor;
                if (picker != null)
                {
                    picker.ItemChanged += handler;
                    continue;
                }
                if (materials != null)
                {
                    materials.ValueChanged += handler;
                    continue;
                }
                TextBox text = control as TextBox;
                ComboBox choice = control as ComboBox;
                NumericUpDown number = control as NumericUpDown;
                CheckBox check = control as CheckBox;
                if (text != null) text.TextChanged += handler;
                if (choice != null)
                {
                    choice.SelectedIndexChanged += handler;
                    if (choice.DropDownStyle != ComboBoxStyle.DropDownList)
                        choice.TextChanged += handler;
                }
                if (number != null) number.ValueChanged += handler;
                if (check != null) check.CheckedChanged += handler;
            }
        }
    }
}
