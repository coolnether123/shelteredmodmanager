using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Manager.Controls;
using Manager.Core.Models;
using Manager.Core.Services;
using ShelteredModManager.ContentPacks;
using ShelteredModManager.Shared.PixelEditing;

namespace Manager.Views
{
    /// <summary>
    /// Content Workshop: a start view for creating or opening a project, then a
    /// workspace with Project, Items, Recipes, Icon Studio, and Check &amp; export
    /// sections. Project I/O, validation, packaging, and installs are delegated
    /// to ContentWorkshopProjectService. View construction lives in
    /// ContentWorkshopTab.Layout.cs.
    /// </summary>
    public sealed partial class ContentWorkshopTab : UserControl
    {
        internal const string ExampleModId = "com.author.newcontent";

        private static readonly Regex IssuePathPattern = new Regex(
            @"^(items|recipes)\[(\d+)\](?:\.([A-Za-z]+)(?:\.([A-Za-z]+))?)?",
            RegexOptions.Compiled);

        private readonly ContentWorkshopProjectService _service;
        private ContentWorkshopProject _project;
        private string _modsPath;
        private bool _loading;
        private bool _busy;
        private int _suppressDirty;
        private int _loadedItemIndex = -1;
        private int _loadedRecipeIndex = -1;
        private bool _lastReportedDirty;
        private ContentPackValidationResult _lastValidation;
        private DateTime _lastValidationTime;
        private bool _validationStale;
        private readonly Dictionary<string, Control> _itemFields =
            new Dictionary<string, Control>(StringComparer.Ordinal);
        private readonly Dictionary<string, Control> _recipeFields =
            new Dictionary<string, Control>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fieldLabels =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Image> _thumbnails =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        // Names shown in item pickers: this pack's items, then base-game items.
        private readonly ItemCatalog _itemCatalog = new ItemCatalog();

        // Until a pack is first exported or installed, IDs follow the names people
        // type, so nobody has to invent dotted identifiers. Opened projects start
        // frozen because their IDs may already be in use by saves or other mods.
        private bool _idsFlexible;
        private bool _autoRoot;
        private bool _autoModId;
        private readonly List<ContentPackItem> _autoIdItems = new List<ContentPackItem>();
        private readonly List<ContentPackRecipe> _autoIdRecipes = new List<ContentPackRecipe>();

        /// <summary>Raised when the project or icon canvas gains or loses unsaved changes.</summary>
        public event EventHandler DirtyStateChanged;

        public ContentWorkshopTab()
            : this(new ContentWorkshopProjectService())
        {
        }

        internal ContentWorkshopTab(ContentWorkshopProjectService service)
        {
            _service = service;
            InitializeView();
            ApplyDeveloperMode();
            UpdateViewState();
        }

        /// <summary>
        /// Developer mode (from Settings) shows IDs, advanced item stats,
        /// unlock flags, folder export and a folder choice for new packs.
        /// Without it the workshop keeps to what a content pack needs.
        /// </summary>
        public bool DeveloperMode
        {
            get { return _developerMode; }
            set
            {
                if (_developerMode == value)
                    return;
                _developerMode = value;
                ApplyDeveloperMode();
            }
        }

        public string ModsPath
        {
            get { return _modsPath; }
            set
            {
                _modsPath = value;
                UpdateExportDescriptions();
            }
        }

        /// <summary>Turns on an installed pack by ID; set by the main window to reach the Mod manager.</summary>
        public Func<string, bool> EnableInstalledPack { get; set; }

        public bool HasUnsavedChanges
        {
            get
            {
                return (_project != null && _project.IsDirty) ||
                    (_pixelCanvas != null && _pixelCanvas.HasUnsavedChanges);
            }
        }

        public bool ConfirmClose()
        {
            return ConfirmDiscardChanges();
        }

        public void ApplyTheme(bool isDark)
        {
            ApplyWorkshopTheme();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!_busy)
            {
                if (keyData == (Keys.Control | Keys.S) && _project != null)
                {
                    SaveProject(this, EventArgs.Empty);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.N))
                {
                    NewProject(this, EventArgs.Empty);
                    return true;
                }
                if (keyData == (Keys.Control | Keys.O))
                {
                    OpenProject(this, EventArgs.Empty);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ------------------------------------------------------------ project

        private void NewProject(object sender, EventArgs e)
        {
            if (_busy || !ConfirmDiscardChanges())
                return;

            // Players get a pack straight away in the workshop folder; only
            // developer mode asks where to put it.
            string folder;
            if (_developerMode)
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Choose an empty folder for the new Content Workshop project.";
                    dialog.ShowNewFolderButton = true;
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    folder = dialog.SelectedPath;
                }
                if (Directory.Exists(folder) &&
                    Directory.GetFileSystemEntries(folder).Length > 0)
                {
                    ShowError("Choose an empty folder so existing files cannot be overwritten.");
                    return;
                }
            }
            else
            {
                folder = FreePackFolder(NewPackName);
            }

            DiscardIconCanvas();
            _project = _service.Create(folder, ContentWorkshopIds.ModIdFor(NewPackName, null), NewPackName);
            _hasSavedCopy = false;
            FreezeIds();
            _idsFlexible = true;
            _autoModId = true;
            _autoRoot = !_developerMode;
            ClearValidation();
            RefreshProject();
            ShowSection(_projectPage);
            _notice.Show(NoticeKind.Info,
                "Name your pack and add your name as author, then add items. Nothing is written until you save.");
            SetStatus(_autoRoot ? "New pack. It saves to " + WorkshopRoot + "." : "New project in " + folder);
            _modName.Focus();
            _modName.SelectAll();
        }

        private const string NewPackName = "New Content Pack";

        /// <summary>Where packs are kept unless developer mode chose a folder.</summary>
        private static string WorkshopRoot
        {
            get
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(Path.Combine(documents, "Sheltered Mod Manager"), "Workshop");
            }
        }

        /// <summary>A folder under the workshop root, named after the pack, that does not exist yet.</summary>
        private static string FreePackFolder(string name)
        {
            string baseName = ContentWorkshopIds.FolderNameFor(name);
            string candidate = Path.Combine(WorkshopRoot, baseName);
            for (int n = 2; Directory.Exists(candidate) || File.Exists(candidate); n++)
                candidate = Path.Combine(WorkshopRoot, baseName + " " + n.ToString(CultureInfo.InvariantCulture));
            return candidate;
        }

        /// <summary>
        /// Until anything is written, a pack in the workshop folder follows its
        /// name, so the folder reads "Bunker Supplies" rather than "New Content Pack".
        /// </summary>
        private void FollowNameWithFolder()
        {
            if (!_autoRoot || _project == null || Directory.Exists(_project.RootPath))
                return;
            _project.RootPath = FreePackFolder(_modName.Text);
            _projectPathBox.Text = _project.RootPath;
        }

        private void OpenProject(object sender, EventArgs e)
        {
            if (_busy || !ConfirmDiscardChanges())
                return;

            string folder;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder of a content pack to open.";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(WorkshopRoot))
                    dialog.SelectedPath = WorkshopRoot;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                folder = dialog.SelectedPath;
            }
            OpenProjectAt(folder, true);
        }

        private void OpenProjectAt(string folder, bool alreadyConfirmed)
        {
            if (_busy || (!alreadyConfirmed && !ConfirmDiscardChanges()))
                return;
            ContentWorkshopProject loaded = null;
            ContentWorkshopOperationResult result = null;
            RunBusy("Opening project...", delegate { result = _service.Open(folder, out loaded); });
            if (result == null || !result.Success)
            {
                ShowError(result == null ? "The project could not be opened." : result.ErrorMessage);
                return;
            }

            DiscardIconCanvas();
            _project = loaded;
            FreezeIds();
            _autoRoot = false;
            ClearValidation();
            _notice.Clear();
            RefreshProject();
            ShowSection(_itemsPage);
            SetStatus("Opened " + ProjectDisplayName() + ".");
        }

        private void SaveProject(object sender, EventArgs e)
        {
            if (_busy || !CommitAllEditors())
                return;
            ContentWorkshopOperationResult result = null;
            RunBusy("Saving...", delegate { result = _service.Save(_project); });
            if (result == null || !result.Success)
            {
                ShowError(result == null ? "The project could not be saved." : result.ErrorMessage);
                return;
            }
            _autoRoot = false;
            if (_notice.Visible && _notice.Kind == NoticeKind.Error)
                _notice.Clear();
            RefreshLists();
            SetStatus("Saved at " + DateTime.Now.ToShortTimeString() + ".");
            UpdateDirtyState();
        }

        private void ValidateProject(object sender, EventArgs e)
        {
            if (_busy || !CommitAllEditors())
                return;
            ContentPackValidationResult result = null;
            RunBusy("Checking project...", delegate { result = _service.Validate(_project, true); });
            ShowValidation(result);
            ShowSection(_checkPage);
            SetStatus(result.IsValid
                ? "Check passed with " + FormatCount(ShownIssueCount(ContentPackValidationSeverity.Warning), "warning") + "."
                : "Check found " + FormatCount(result.ErrorCount, "error") + " to fix.");
        }

        private void ExportFolder(object sender, EventArgs e)
        {
            if (_busy || !CommitAllEditors())
                return;
            string parent;
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder that will contain the exported mod folder.";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                parent = dialog.SelectedPath;
            }
            ContentWorkshopOperationResult result = null;
            RunBusy("Exporting folder...", delegate { result = _service.ExportFolder(_project, parent); });
            FinishExport(result, "Mod folder exported.");
        }

        private void ExportZip(object sender, EventArgs e)
        {
            if (_busy || !CommitAllEditors())
                return;
            string zipPath;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "ZIP archive (*.zip)|*.zip";
                dialog.FileName = _project.ModId + ".zip";
                dialog.OverwritePrompt = false;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                zipPath = dialog.FileName;
            }
            ContentWorkshopOperationResult result = null;
            RunBusy("Exporting ZIP...", delegate { result = _service.ExportZip(_project, zipPath); });
            FinishExport(result, "ZIP exported.");
        }

        private void InstallLocally(object sender, EventArgs e)
        {
            if (_busy || !CommitAllEditors())
                return;
            string path = ModsPath;
            if (string.IsNullOrEmpty(path))
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select the Sheltered Mod Manager mods folder.";
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    path = dialog.SelectedPath;
                }
            }
            ContentWorkshopOperationResult result = null;
            RunBusy("Installing to the mods folder...", delegate { result = _service.Install(_project, path); });
            // Installing is for playing, so the pack is also turned on when the
            // Mod manager can see it.
            if (result != null && result.Success && EnableInstalledPack != null && EnableInstalledPack(_project.ModId))
                FinishExport(result, ProjectDisplayName() + " is installed and turned on. It loads the next time you start the game.");
            else
                FinishExport(result, "Content pack installed. Turn it on in the Mod manager to use it in game.");
        }

        /// <summary>
        /// Exports and installs save first and refuse invalid packs. A refusal
        /// caused by validation shows the problem list instead of a dialog.
        /// </summary>
        private void FinishExport(ContentWorkshopOperationResult result, string successMessage)
        {
            UpdateDirtyState();
            if (result != null && result.Success)
            {
                FreezeIds();
                string target = result.Path;
                _notice.Show(NoticeKind.Success, successMessage + " " + target, "Show in folder",
                    delegate { RevealInExplorer(target); });
                SetStatus(successMessage);
                return;
            }

            string error = result == null ? "The operation failed." : result.ErrorMessage;
            ContentPackValidationResult validation = _project == null ? null : _service.Validate(_project, true);
            if (validation != null && !validation.IsValid)
            {
                ShowValidation(validation);
                ShowSection(_checkPage);
                _notice.Show(NoticeKind.Error, error + " The problems are listed below.");
                SetStatus(error);
                return;
            }
            ShowError(error);
        }

        private void RevealInExplorer(string path)
        {
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                    System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception ex)
            {
                ShowError("Could not open the folder: " + ex.Message);
            }
        }

        private void OpenProjectFolder(object sender, EventArgs e)
        {
            if (_project == null || string.IsNullOrEmpty(_project.RootPath))
                return;
            try
            {
                if (!Directory.Exists(_project.RootPath))
                {
                    ShowError("The project folder does not exist yet. Save the project to create it.");
                    return;
                }
                System.Diagnostics.Process.Start("explorer.exe", _project.RootPath);
            }
            catch (Exception ex)
            {
                ShowError("Could not open the project folder: " + ex.Message);
            }
        }

        private void RunBusy(string status, MethodInvoker work)
        {
            _busy = true;
            SetStatus(status);
            UpdateCommandStates();
            UseWaitCursor = true;
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            if (_statusBar != null)
                _statusBar.Refresh();
            try
            {
                work();
            }
            finally
            {
                Cursor.Current = previous;
                UseWaitCursor = false;
                _busy = false;
                UpdateCommandStates();
            }
        }

        // ------------------------------------------------------------ items

        private void AddItem()
        {
            if (_project == null)
                return;
            CommitSelectedItem();
            ContentPackItem item = new ContentPackItem();
            item.displayName = "New Item";
            _project.Content.items.Add(item);
            item.id = ItemIdFromName(item);
            _autoIdItems.Add(item);
            MarkDirty();
            RefreshLists();
            _itemList.SelectedIndex = _project.Content.items.Count - 1;
            ShowSection(_itemsPage);
            FocusItemField("displayName");
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
            ContentPackItem item = _project.Content.items[_itemList.SelectedIndex];
            if (MessageBox.Show(this,
                    "Remove \"" + ItemName(item) + "\" from this project?\r\n\r\nRecipes that make or use it will need to be updated.",
                    "Remove item", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            _loadedItemIndex = -1;
            _autoIdItems.Remove(item);
            _project.Content.items.RemoveAt(_itemList.SelectedIndex);
            MarkDirty();
            RefreshLists();
        }

        private void AddRecipe()
        {
            if (_project == null)
                return;
            CommitSelectedRecipe();
            ContentPackRecipe recipe = new ContentPackRecipe();
            if (_project.Content.items.Count > 0)
                recipe.resultItemId = _project.Content.items[0].id;
            recipe.ingredients.Add(new ContentPackIngredient { itemId = "Wood", count = 1 });
            _project.Content.recipes.Add(recipe);
            recipe.id = RecipeIdFor(recipe);
            _autoIdRecipes.Add(recipe);
            MarkDirty();
            RefreshLists();
            _recipeList.SelectedIndex = _project.Content.recipes.Count - 1;
            ShowSection(_recipesPage);
            FocusRecipeField("resultItemId");
        }

        private void RemoveRecipe()
        {
            if (_project == null || _recipeList.SelectedIndex < 0)
                return;
            ContentPackRecipe recipe = _project.Content.recipes[_recipeList.SelectedIndex];
            if (MessageBox.Show(this,
                    "Remove the recipe for \"" + RecipeTitle(recipe) + "\"?",
                    "Remove recipe", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            _loadedRecipeIndex = -1;
            _autoIdRecipes.Remove(recipe);
            _project.Content.recipes.RemoveAt(_recipeList.SelectedIndex);
            MarkDirty();
            RefreshLists();
        }

        private void LoadSelectedItem()
        {
            if (!_loading)
                CommitItemAt(_loadedItemIndex);
            _suppressDirty++;
            try
            {
                ClearFields(_itemFields);
                if (_project == null || _itemList.SelectedIndex < 0)
                {
                    _loadedItemIndex = -1;
                }
                else
                {
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
                    SetMaterials(_itemFields, "recycling", item.recycling);
                }
            }
            finally
            {
                _suppressDirty--;
            }

            UpdateItemEditor();
            if (_iconItemPicker != null &&
                _itemList.SelectedIndex >= 0 &&
                _itemList.SelectedIndex < _iconItemPicker.Items.Count &&
                _iconItemPicker.SelectedIndex != _itemList.SelectedIndex &&
                !_pixelCanvas.HasUnsavedChanges)
            {
                _loading = true;
                _iconItemPicker.SelectedIndex = _itemList.SelectedIndex;
                _loading = false;
                UpdatePixelStatus();
            }
        }

        private void LoadSelectedRecipe()
        {
            if (!_loading)
                CommitRecipeAt(_loadedRecipeIndex);
            _suppressDirty++;
            try
            {
                ClearFields(_recipeFields);
                if (_project == null || _recipeList.SelectedIndex < 0)
                {
                    _loadedRecipeIndex = -1;
                }
                else
                {
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
                    SetMaterials(_recipeFields, "ingredients", recipe.ingredients);
                }
            }
            finally
            {
                _suppressDirty--;
            }
            UpdateRecipeEditor();
        }

        /// <summary>
        /// Copies every editor into the project model. Commit alone does not
        /// mark the project dirty; edits already did when they happened.
        /// </summary>
        private bool CommitAllEditors()
        {
            if (_project == null)
            {
                ShowError("Create or open a project first.");
                return false;
            }
            CommitIdentity();
            CommitSelectedItem();
            CommitSelectedRecipe();
            if (!CommitPendingIconEdits())
                return false;
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
            item.recycling = GetMaterials(_itemFields, "recycling");
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
            recipe.ingredients = GetMaterials(_recipeFields, "ingredients");
        }

        // ------------------------------------------------------------ editing state

        private void IdentityFieldChanged(object sender, EventArgs e)
        {
            if (_loading || _suppressDirty > 0 || _project == null)
                return;
            string oldModId = _project.About.id ?? string.Empty;
            if (sender == _modId)
                _autoModId = false;
            else if (_autoModId && (sender == _modName || sender == _authors))
                SetModIdQuietly(ContentWorkshopIds.ModIdFor(_modName.Text, _authors.Text));
            if (sender == _modName)
                FollowNameWithFolder();
            CommitIdentity();
            if (_idsFlexible && !string.Equals(oldModId, _project.About.id, StringComparison.Ordinal))
                MoveIdsToModId(oldModId, _project.About.id);
            MarkDirty();
            UpdateProjectHeader();
        }

        private void ItemFieldChanged(object sender, EventArgs e)
        {
            if (_loading || _suppressDirty > 0 || _project == null)
                return;
            // Rows are painted from the model, so a repaint shows the edit
            // without replacing the list entry (which would reset selection).
            CommitSelectedItem();
            ContentPackItem item = _loadedItemIndex >= 0 ? _project.Content.items[_loadedItemIndex] : null;
            if (item != null && sender == _itemFields["id"])
            {
                _autoIdItems.Remove(item);
                RefreshItemCatalog();
            }
            else if (item != null && sender == _itemFields["displayName"])
            {
                if (_autoIdItems.Contains(item))
                    RenameItemId(item, ItemIdFromName(item));
                RefreshItemCatalog();
            }
            MarkDirty();
            InvalidateItemRow(_loadedItemIndex);
            UpdateItemEditorHeader();
        }

        private void RecipeFieldChanged(object sender, EventArgs e)
        {
            if (_loading || _suppressDirty > 0 || _project == null)
                return;
            CommitSelectedRecipe();
            ContentPackRecipe recipe = _loadedRecipeIndex >= 0 ? _project.Content.recipes[_loadedRecipeIndex] : null;
            if (recipe != null && sender == _recipeFields["id"])
            {
                _autoIdRecipes.Remove(recipe);
            }
            else if (recipe != null && sender == _recipeFields["resultItemId"] && _autoIdRecipes.Contains(recipe))
            {
                recipe.id = RecipeIdFor(recipe);
                _suppressDirty++;
                try
                {
                    SetText(_recipeFields, "id", recipe.id);
                }
                finally
                {
                    _suppressDirty--;
                }
            }
            MarkDirty();
            InvalidateRecipeRow(_loadedRecipeIndex);
            UpdateRecipeEditorHeader();
        }

        private void CommitIdentity()
        {
            _project.About.id = _modId.Text.Trim();
            _project.About.name = _modName.Text.Trim();
            _project.About.version = _version.Text.Trim();
            _project.About.description = _description.Text.Trim();
            _project.About.authors = SplitCommaList(_authors.Text);
            _project.Content.modId = _project.About.id;
        }

        // ------------------------------------------------------------ automatic IDs

        private void SetModIdQuietly(string modId)
        {
            if (string.Equals(_modId.Text, modId, StringComparison.Ordinal))
                return;
            _suppressDirty++;
            try
            {
                _modId.Text = modId;
            }
            finally
            {
                _suppressDirty--;
            }
        }

        private string ItemIdFromName(ContentPackItem item)
        {
            string name = ContentWorkshopIds.Slug(item.displayName, "_");
            return UniqueId(_project.ModId + "." + (name.Length > 0 ? name : "item"), true, item);
        }

        private string RecipeIdFor(ContentPackRecipe recipe)
        {
            string made = ContentWorkshopIds.Slug(_itemCatalog.NameFor(recipe.resultItemId), "_");
            return UniqueId(_project.ModId + ".craft_" + (made.Length > 0 ? made : "item"), false, recipe);
        }

        /// <summary>The base ID, or the base ID with a number, not used by any other item or recipe.</summary>
        private string UniqueId(string baseId, bool items, object self)
        {
            List<string> taken = new List<string>();
            if (items)
            {
                foreach (ContentPackItem existing in _project.Content.items)
                    if (existing != null && existing != self && existing.id != null) taken.Add(existing.id);
            }
            else
            {
                foreach (ContentPackRecipe existing in _project.Content.recipes)
                    if (existing != null && existing != self && existing.id != null) taken.Add(existing.id);
            }
            return ContentWorkshopIds.Unique(baseId, taken);
        }

        /// <summary>Changes an item's ID and every recipe or recycling entry that points at it.</summary>
        private void RenameItemId(ContentPackItem item, string newId)
        {
            string oldId = item.id ?? string.Empty;
            if (string.Equals(oldId, newId, StringComparison.Ordinal))
                return;
            CommitSelectedRecipe();
            item.id = newId;
            Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            if (oldId.Length > 0)
                renamed[oldId] = newId;
            RewriteReferences(renamed);
            ReloadEditors();
        }

        /// <summary>
        /// While a new pack has never been exported, changing its pack ID moves
        /// every item and recipe ID that used the old one, and their references.
        /// </summary>
        private void MoveIdsToModId(string oldModId, string newModId)
        {
            string oldPrefix = (oldModId ?? string.Empty) + ".";
            string newPrefix = (newModId ?? string.Empty) + ".";
            CommitSelectedItem();
            CommitSelectedRecipe();
            Dictionary<string, string> renamed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ContentPackItem item in _project.Content.items)
            {
                if (item == null || item.id == null || !item.id.StartsWith(oldPrefix, StringComparison.Ordinal))
                    continue;
                string moved = newPrefix + item.id.Substring(oldPrefix.Length);
                renamed[item.id] = moved;
                item.id = moved;
            }
            foreach (ContentPackRecipe recipe in _project.Content.recipes)
            {
                if (recipe != null && recipe.id != null && recipe.id.StartsWith(oldPrefix, StringComparison.Ordinal))
                    recipe.id = newPrefix + recipe.id.Substring(oldPrefix.Length);
            }
            RewriteReferences(renamed);
            ReloadEditors();
            RefreshItemCatalog();
            _itemList.Invalidate();
            _recipeList.Invalidate();
        }

        private void RewriteReferences(Dictionary<string, string> renamed)
        {
            if (renamed.Count == 0)
                return;
            string moved;
            foreach (ContentPackRecipe recipe in _project.Content.recipes)
            {
                if (recipe == null)
                    continue;
                if (recipe.resultItemId != null && renamed.TryGetValue(recipe.resultItemId, out moved))
                    recipe.resultItemId = moved;
                RewriteMaterials(recipe.ingredients, renamed);
            }
            foreach (ContentPackItem item in _project.Content.items)
                if (item != null)
                    RewriteMaterials(item.recycling, renamed);
        }

        private static void RewriteMaterials(List<ContentPackIngredient> materials, Dictionary<string, string> renamed)
        {
            if (materials == null)
                return;
            string moved;
            foreach (ContentPackIngredient material in materials)
                if (material != null && material.itemId != null && renamed.TryGetValue(material.itemId, out moved))
                    material.itemId = moved;
        }

        /// <summary>Shows the model again in both editors without first writing the editors back.</summary>
        private void ReloadEditors()
        {
            _loadedItemIndex = -1;
            _loadedRecipeIndex = -1;
            LoadSelectedItem();
            LoadSelectedRecipe();
        }

        /// <summary>IDs are fixed once a pack leaves the workshop, since saves and other mods may use them.</summary>
        private void FreezeIds()
        {
            _idsFlexible = false;
            _autoModId = false;
            _autoIdItems.Clear();
            _autoIdRecipes.Clear();
        }

        private void MarkDirty()
        {
            if (_project == null)
                return;
            _project.IsDirty = true;
            if (_lastValidation != null && !_validationStale)
            {
                _validationStale = true;
                UpdateValidationSummary();
            }
            UpdateDirtyState();
        }

        private void UpdateDirtyState()
        {
            UpdateWorkspaceStatus();
            UpdateCommandStates();
            bool dirty = HasUnsavedChanges;
            if (dirty != _lastReportedDirty)
            {
                _lastReportedDirty = dirty;
                EventHandler handler = DirtyStateChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        // ------------------------------------------------------------ icons

        private void ImportIcon(object sender, EventArgs e)
        {
            if (_project == null || _itemList.SelectedIndex < 0)
            {
                ShowError("Select an item first.");
                return;
            }
            if (!ConfirmDiscardIconEdits())
                return;
            string source;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "PNG image (*.png)|*.png";
                dialog.Title = "Import an icon for " + ItemName(_project.Content.items[_itemList.SelectedIndex]);
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                source = dialog.FileName;
            }
            ImportIconFromFile(source);
        }

        private void ImportIconFromFile(string source)
        {
            CommitSelectedItem();
            int index = _itemList.SelectedIndex;
            string relative;
            string name = Path.GetFileNameWithoutExtension(source);
            ContentWorkshopOperationResult result =
                _service.ImportIcon(_project, source, name, out relative);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage);
                return;
            }
            _project.Content.items[index].iconPath = relative;
            MarkDirty();
            _loadedItemIndex = -1;
            LoadSelectedItem();
            InvalidateItemRow(index);
            string error;
            if (!_pixelCanvas.LoadPng(result.Path, out error))
            {
                ShowError(error);
                return;
            }
            SyncIconItemPicker();
            _pixelCanvas.FitZoomToClient();
            UpdatePixelStatus();
            SetStatus("Imported " + Path.GetFileName(source) + " as the icon for " + ItemName(_project.Content.items[index]) + ".");
        }

        private void LoadSelectedIcon(object sender, EventArgs e)
        {
            int index = _iconItemPicker != null && _iconItemPicker.SelectedIndex >= 0
                ? _iconItemPicker.SelectedIndex
                : _itemList.SelectedIndex;
            if (_project == null || index < 0 || index >= _project.Content.items.Count)
            {
                ShowError("Select an item first.");
                return;
            }
            if (!ConfirmDiscardIconEdits())
                return;
            CommitSelectedItem();
            ContentPackItem item = _project.Content.items[index];
            string normalized;
            string full;
            string error;
            if (!ContentPackPathPolicy.TryResolveAsset(
                _project.RootPath, item.iconPath, out normalized, out full, out error) ||
                !File.Exists(full))
            {
                ShowError(error ?? "This item does not have an icon file yet. Import a PNG or start a new 32 x 32 icon.");
                return;
            }
            if (!_pixelCanvas.LoadPng(full, out error))
            {
                ShowError(error);
                return;
            }
            _pixelCanvas.FitZoomToClient();
            UpdatePixelStatus();
        }

        private void NewIcon(object sender, EventArgs e)
        {
            if (!ConfirmDiscardIconEdits())
                return;
            _pixelCanvas.CreateDocument(32, 32);
            _pixelCanvas.FitZoomToClient();
            _pixelCanvas.Focus();
        }

        private void SaveIcon(object sender, EventArgs e)
        {
            if (!SaveIconToSelectedItem(true))
                return;
        }

        private bool ConfirmDiscardIconEdits()
        {
            if (_pixelCanvas == null || !_pixelCanvas.HasUnsavedChanges)
                return true;
            return MessageBox.Show(this,
                "The Icon Studio has unsaved pixels. Discard them?",
                "Icon Studio", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void DiscardIconCanvas()
        {
            if (_pixelCanvas == null)
                return;
            _pixelCanvas.CreateDocument(32, 32);
            _pixelCanvas.MarkSaved();
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
                MarkDirty();
                if (itemIndex == _loadedItemIndex)
                {
                    _suppressDirty++;
                    try { SetText(_itemFields, "iconPath", relative); }
                    finally { _suppressDirty--; }
                    UpdateItemEditor();
                }
                InvalidateItemRow(itemIndex);
                UpdatePixelStatus();
                if (showSuccess)
                    SetStatus("Icon saved to " + ItemName(item) + ". Save the project to keep the new icon path.");
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
            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.Color = _pixelCanvas.ActiveColor;
                dialog.FullOpen = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    _pixelCanvas.ActiveColor = dialog.Color;
            }
        }

        private void OpenIconStudio(object sender, EventArgs e)
        {
            if (_itemList.SelectedIndex < 0)
            {
                ShowError("Select an item before opening the Icon Studio.");
                return;
            }
            ShowSection(_iconPage);
            SyncIconItemPicker();
            if (!_pixelCanvas.HasUnsavedChanges && _project != null)
            {
                ContentPackItem item = _project.Content.items[_itemList.SelectedIndex];
                string normalized;
                string full;
                string error;
                if (ContentPackPathPolicy.TryResolveAsset(_project.RootPath, item.iconPath, out normalized, out full, out error) &&
                    File.Exists(full) && _pixelCanvas.LoadPng(full, out error))
                {
                    _pixelCanvas.FitZoomToClient();
                    UpdatePixelStatus();
                }
            }
            _pixelCanvas.Focus();
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
            _paintToolButton.Toggled = _pixelCanvas.ActiveTool == PixelEditorTool.Paint;
            _eraseToolButton.Toggled = _pixelCanvas.ActiveTool == PixelEditorTool.Erase;
            _pickToolButton.Toggled = _pixelCanvas.ActiveTool == PixelEditorTool.Pick;
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

        // ------------------------------------------------------------ refresh

        private void RefreshProject()
        {
            _loading = true;
            try
            {
                ClearThumbnails();
                if (_project != null)
                {
                    _suppressDirty++;
                    try
                    {
                        _modId.Text = _project.ModId;
                        _modName.Text = _project.About.name ?? string.Empty;
                        _version.Text = _project.About.version ?? string.Empty;
                        _description.Text = _project.About.description ?? string.Empty;
                        _authors.Text = string.Join(", ", _project.About.authors ?? new string[0]);
                        _projectPathBox.Text = _project.RootPath;
                    }
                    finally
                    {
                        _suppressDirty--;
                    }
                }
                RefreshLists();
            }
            finally
            {
                _loading = false;
            }
            UpdateViewState();
            _lastReportedDirty = !HasUnsavedChanges;
            UpdateDirtyState();
        }

        private void RefreshLists()
        {
            int itemIndex = _itemList.SelectedIndex;
            int recipeIndex = _recipeList.SelectedIndex;
            _loading = true;
            _loadedItemIndex = -1;
            _loadedRecipeIndex = -1;
            _itemList.BeginUpdate();
            _recipeList.BeginUpdate();
            _itemList.Items.Clear();
            _recipeList.Items.Clear();
            if (_project != null)
            {
                for (int i = 0; i < _project.Content.items.Count; i++)
                    _itemList.Items.Add(ItemListText(_project.Content.items[i]));
                for (int i = 0; i < _project.Content.recipes.Count; i++)
                    _recipeList.Items.Add(RecipeListText(_project.Content.recipes[i]));
                if (_itemList.Items.Count > 0)
                    _itemList.SelectedIndex = Math.Max(0, Math.Min(itemIndex, _itemList.Items.Count - 1));
                if (_recipeList.Items.Count > 0)
                    _recipeList.SelectedIndex = Math.Max(0, Math.Min(recipeIndex, _recipeList.Items.Count - 1));
            }
            _itemList.EndUpdate();
            _recipeList.EndUpdate();
            _loading = false;
            RefreshItemCatalog();
            LoadSelectedItem();
            LoadSelectedRecipe();
            SyncIconItemPicker();
            UpdateListStates();
            UpdateSectionTitles();
            UpdateWorkspaceStatus();
            UpdatePixelStatus();
        }

        /// <summary>Pickers show this pack's items by name; rebuild when items are added, removed or renamed.</summary>
        private void RefreshItemCatalog()
        {
            List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();
            if (_project != null)
            {
                foreach (ContentPackItem item in _project.Content.items)
                    if (item != null && !string.IsNullOrEmpty(item.id))
                        items.Add(new KeyValuePair<string, string>(item.id, item.displayName));
            }
            _suppressDirty++;
            try
            {
                _itemCatalog.SetPackItems(items);
            }
            finally
            {
                _suppressDirty--;
            }
        }

        // ------------------------------------------------------------ validation

        private sealed class IssueTarget
        {
            public TabPage Section;
            public int Index = -1;
            public string FieldKey;
            public string Label;
        }

        private sealed class IssueRow
        {
            public ContentPackValidationSeverity? Severity;
            public string Message;
            public IssueTarget Target;

            public string SeverityText
            {
                get
                {
                    if (!Severity.HasValue) return "Passed";
                    return Severity.Value == ContentPackValidationSeverity.Error ? "Error" : "Warning";
                }
            }

            public override string ToString()
            {
                return SeverityText + ": " + (Target != null ? Target.Label + ". " : string.Empty) + Message;
            }
        }

        private void ClearValidation()
        {
            _lastValidation = null;
            _validationStale = false;
            if (_validationList != null)
                _validationList.Items.Clear();
            UpdateValidationSummary();
        }

        private void ShowValidation(ContentPackValidationResult result)
        {
            _lastValidation = result;
            _lastValidationTime = DateTime.Now;
            _validationStale = false;
            RebuildIssueRows();
            UpdateValidationSummary();
        }

        private void RebuildIssueRows()
        {
            _validationList.BeginUpdate();
            _validationList.Items.Clear();
            if (_lastValidation != null)
            {
                // Errors block export, so they lead; each group keeps validator order.
                foreach (ContentPackValidationSeverity severity in new ContentPackValidationSeverity[]
                    { ContentPackValidationSeverity.Error, ContentPackValidationSeverity.Warning })
                {
                    for (int i = 0; i < _lastValidation.Issues.Count; i++)
                    {
                        ContentPackValidationIssue issue = _lastValidation.Issues[i];
                        if (issue.Severity != severity || !IsShownIssue(issue))
                            continue;
                        IssueRow row = new IssueRow();
                        row.Severity = issue.Severity;
                        row.Message = FriendlyIssueMessage(issue);
                        row.Target = ResolveIssueTarget(issue.Path);
                        _validationList.Items.Add(row);
                    }
                }
                if (_validationList.Items.Count == 0)
                {
                    IssueRow valid = new IssueRow();
                    valid.Message = "No problems found. The pack is ready to export.";
                    _validationList.Items.Add(valid);
                }
            }
            _validationList.EndUpdate();
            UpdateListStates();
        }

        private static readonly Regex MaterialPathPattern = new Regex(
            @"^(items|recipes)\[(\d+)\]\.(?:(recycling|ingredients)\[(\d+)\]\.itemId|(resultItemId))$",
            RegexOptions.Compiled);

        /// <summary>
        /// References to base-game items are expected, so their "external
        /// reference" warnings are not shown or counted.
        /// </summary>
        private bool IsShownIssue(ContentPackValidationIssue issue)
        {
            if (issue == null)
                return false;
            if (!string.Equals(issue.Code, "reference.external", StringComparison.Ordinal))
                return true;
            string id = ReferencedItemId(issue.Path);
            if (string.IsNullOrEmpty(id))
                return true;
            foreach (ItemChoice vanilla in VanillaItems.All)
                if (string.Equals(vanilla.Id, id, StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        private int ShownIssueCount(ContentPackValidationSeverity severity)
        {
            int count = 0;
            if (_lastValidation != null)
            {
                foreach (ContentPackValidationIssue issue in _lastValidation.Issues)
                    if (issue.Severity == severity && IsShownIssue(issue))
                        count++;
            }
            return count;
        }

        private string ReferencedItemId(string path)
        {
            Match match = MaterialPathPattern.Match(path ?? string.Empty);
            if (!match.Success || _project == null)
                return null;
            int owner = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            bool isItem = match.Groups[1].Value == "items";
            if (isItem)
            {
                if (owner >= _project.Content.items.Count || _project.Content.items[owner] == null)
                    return null;
                return MaterialAt(_project.Content.items[owner].recycling, match.Groups[4].Value);
            }
            if (owner >= _project.Content.recipes.Count || _project.Content.recipes[owner] == null)
                return null;
            ContentPackRecipe recipe = _project.Content.recipes[owner];
            if (match.Groups[5].Success)
                return recipe.resultItemId;
            return MaterialAt(recipe.ingredients, match.Groups[4].Value);
        }

        private static string MaterialAt(List<ContentPackIngredient> materials, string index)
        {
            int position;
            if (materials == null || !int.TryParse(index, NumberStyles.Integer, CultureInfo.InvariantCulture, out position))
                return null;
            return position >= 0 && position < materials.Count && materials[position] != null ? materials[position].itemId : null;
        }

        /// <summary>Plain wording for the validator's codes; unknown codes keep the validator's text.</summary>
        private string FriendlyIssueMessage(ContentPackValidationIssue issue)
        {
            string packId = _project == null ? string.Empty : _project.ModId;
            switch (issue.Code ?? string.Empty)
            {
                case "mod_id.invalid":
                    return "Use lowercase letters, numbers, dots, dashes or underscores, with at least one dot, such as yourname.packname.";
                case "item.id.invalid":
                case "recipe.id.invalid":
                    return "The ID can only use lowercase letters, numbers, dots, dashes and underscores.";
                case "item.id.namespace":
                case "recipe.id.namespace":
                    return "The ID must start with the pack ID and a dot (" + packId + ".).";
                case "item.id.duplicate":
                    return "Another item already uses this ID.";
                case "recipe.id.duplicate":
                    return "Another recipe already uses this ID.";
                case "item.display_name.required":
                    return "Give the item a name.";
                case "item.category.invalid":
                    return "Pick a category.";
                case "item.category.behavior_required":
                    return "Items in this category may need a code mod to work fully in game.";
                case "item.ration.contamination":
                    return "Contamination must be between 0 and 1.";
                case "item.raw_food.multiplier":
                    return "The cooked hunger multiplier must be more than zero.";
                case "recipe.station.invalid":
                    return "Pick a crafting station.";
                case "recipe.craft_time":
                    return "Craft time must be more than zero.";
                case "recipe.unlock_flag.missing":
                    return "This recipe starts locked but has no unlock flag, so players may never be able to craft it.";
                case "recipe.ingredients.required":
                    return "Add at least one material.";
                case "reference.required":
                    return "Choose an item.";
                case "reference.invalid":
                    return "This item name has characters the game cannot read.";
                case "reference.local_missing":
                    return "This points at an item that is no longer in this pack.";
                case "reference.external":
                    string id = ReferencedItemId(issue.Path);
                    return "\"" + (string.IsNullOrEmpty(id) ? "This item" : id) +
                        "\" is not a base-game item or one of yours, so it must come from another mod. Check the spelling.";
                case "ingredient.count":
                    return "The amount must be at least 1.";
            }
            return issue.Message;
        }

        private IssueTarget ResolveIssueTarget(string path)
        {
            IssueTarget target = new IssueTarget();
            if (string.Equals(path, "modId", StringComparison.Ordinal))
            {
                target.Section = _projectPage;
                target.FieldKey = "modId";
                target.Label = "Project › Pack ID";
                return target;
            }

            Match match = IssuePathPattern.Match(path ?? string.Empty);
            if (!match.Success)
            {
                target.Label = string.IsNullOrEmpty(path) || path == "$" ? "Project" : path;
                return target;
            }

            bool isItem = match.Groups[1].Value == "items";
            int index;
            int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
            string field = match.Groups[3].Success ? match.Groups[3].Value : null;
            string sub = match.Groups[4].Success ? match.Groups[4].Value : null;

            target.Section = isItem ? _itemsPage : _recipesPage;
            target.Index = index;
            target.FieldKey = MapIssueField(isItem, field, sub);

            string owner;
            if (isItem)
                owner = _project != null && index < _project.Content.items.Count
                    ? ItemName(_project.Content.items[index])
                    : "Item " + (index + 1);
            else
                owner = _project != null && index < _project.Content.recipes.Count
                    ? RecipeTitle(_project.Content.recipes[index])
                    : "Recipe " + (index + 1);

            string fieldLabel;
            string labelKey = (isItem ? "item." : "recipe.") + (target.FieldKey ?? string.Empty);
            if (target.FieldKey == null || !_fieldLabels.TryGetValue(labelKey, out fieldLabel))
                fieldLabel = null;
            target.Label = (isItem ? "Item: " : "Recipe: ") + owner + (fieldLabel != null ? " › " + fieldLabel : string.Empty);
            return target;
        }

        private static string MapIssueField(bool isItem, string field, string sub)
        {
            if (string.IsNullOrEmpty(field))
                return null;
            if (isItem)
            {
                if (field == "fabrication") return sub == "timeSeconds" ? "fabricationTime" : "fabricationCost";
                if (field == "ration") return sub == "contamination" ? "contamination" : "rationValue";
                if (field == "rawFood") return "cookedMultiplier";
                return field;
            }
            if (field == "craftTimeSeconds") return "craftTime";
            return field;
        }

        private void NavigateToIssue()
        {
            IssueRow row = _validationList.SelectedItem as IssueRow;
            if (row == null || row.Target == null || row.Target.Section == null || _project == null)
                return;
            IssueTarget target = row.Target;
            if (target.Section == _projectPage)
            {
                ShowSection(_projectPage);
                RevealDeveloperField(_modId);
                _modId.Focus();
                _modId.SelectAll();
                return;
            }
            if (target.Section == _itemsPage)
            {
                if (target.Index < 0 || target.Index >= _itemList.Items.Count)
                    return;
                ShowSection(_itemsPage);
                _itemList.SelectedIndex = target.Index;
                FocusItemField(target.FieldKey);
                return;
            }
            if (target.Index < 0 || target.Index >= _recipeList.Items.Count)
                return;
            ShowSection(_recipesPage);
            _recipeList.SelectedIndex = target.Index;
            FocusRecipeField(target.FieldKey);
        }

        // ------------------------------------------------------------ pixel status

        private void UpdatePixelStatus()
        {
            if (_pixelCanvas == null)
                return;
            _colorSwatch.BackColor = _pixelCanvas.ActiveColor;
            _toolTip.SetToolTip(_colorSwatch, "Current colour " + ColorTranslator.ToHtml(_pixelCanvas.ActiveColor) + ". Click to choose another.");
            Size size = _pixelCanvas.CanvasPixelSize;
            bool unsaved = _pixelCanvas.HasUnsavedChanges;
            _pixelStatus.Text = size.Width + " × " + size.Height + " px  ·  " + _pixelCanvas.Zoom + "× zoom  ·  " +
                (unsaved ? "Unsaved icon changes" : "No unsaved icon changes");
            ManagerTheme.SetRole(_pixelStatus, unsaved ? ThemeRole.Warning : ThemeRole.Muted);
            _undoButton.Enabled = _pixelCanvas.Session.History.CanUndo;
            _redoButton.Enabled = _pixelCanvas.Session.History.CanRedo;
            _saveIconButton.Enabled = _project != null && _iconItemPicker.SelectedIndex >= 0;
            _iconItemPicker.Enabled = !unsaved;
            _iconPickerHelp.Text = unsaved
                ? "Save or discard these pixels before switching items."
                : "Saving assigns the PNG to this item.";
            UpdateToolButtons();
        }

        // ------------------------------------------------------------ messages

        private void SetStatus(string text)
        {
            if (_workspaceStatus == null)
                return;
            _workspaceStatus.Text = text ?? string.Empty;
            _toolTip.SetToolTip(_workspaceStatus, _workspaceStatus.Text);
        }

        private void ShowError(string message)
        {
            string text = string.IsNullOrEmpty(message) ? "The operation failed." : message;
            if (_notice != null && _project != null)
                _notice.Show(NoticeKind.Error, text);
            SetStatus(text);
            MessageBox.Show(this, text, "Content Workshop",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Offers to save, discard, or cancel before the project is replaced
        /// or the manager closes.
        /// </summary>
        private bool ConfirmDiscardChanges()
        {
            bool iconDirty = _pixelCanvas != null && _pixelCanvas.HasUnsavedChanges;
            if ((_project == null || !_project.IsDirty) && !iconDirty)
                return true;

            if (_project == null)
            {
                return MessageBox.Show(this,
                    "The Icon Studio has unsaved pixels. Discard them?",
                    "Content Workshop", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            }

            DialogResult choice = MessageBox.Show(
                this,
                "Save changes to \"" + ProjectDisplayName() + "\" before continuing?" +
                    (iconDirty ? "\r\n\r\nThis includes unsaved pixels in the Icon Studio." : string.Empty) +
                    "\r\n\r\nYes saves, No discards the changes, Cancel keeps working.",
                "Content Workshop",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1);
            if (choice == DialogResult.Cancel)
                return false;
            if (choice == DialogResult.No)
                return true;

            if (!CommitAllEditors())
                return false;
            ContentWorkshopOperationResult result = _service.Save(_project);
            if (result == null || !result.Success)
            {
                ShowError(result == null ? "The project could not be saved." : result.ErrorMessage);
                return false;
            }
            UpdateDirtyState();
            return true;
        }

        private void UpdateWorkspaceStatus()
        {
            if (_statusCounts == null)
                return;
            if (_project == null)
            {
                _statusCounts.Text = string.Empty;
                if (_workspaceStatus != null && string.IsNullOrEmpty(_workspaceStatus.Text))
                    SetStatus("Start a new pack or open one.");
                UpdateDirtyChip();
                return;
            }
            int itemCount = _project.Content.items == null ? 0 : _project.Content.items.Count;
            int recipeCount = _project.Content.recipes == null ? 0 : _project.Content.recipes.Count;
            _statusCounts.Text = FormatCount(itemCount, "item") + "  ·  " + FormatCount(recipeCount, "recipe");
            UpdateDirtyChip();
        }

        // ------------------------------------------------------------ naming

        private string ProjectDisplayName()
        {
            string name = _modName != null ? _modName.Text.Trim() : string.Empty;
            if (name.Length == 0 && _project != null)
                name = _project.About.name ?? string.Empty;
            return name.Length > 0 ? name : "Untitled content pack";
        }

        private static string ItemName(ContentPackItem item)
        {
            if (item == null)
                return "(missing item)";
            if (!string.IsNullOrEmpty(item.displayName))
                return item.displayName;
            return string.IsNullOrEmpty(item.id) ? "(unnamed item)" : ShortId(item.id);
        }

        private static string ShortId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "(no ID)";
            int separator = id.LastIndexOf('.');
            return separator >= 0 && separator < id.Length - 1 ? id.Substring(separator + 1) : id;
        }

        private static string ItemListText(ContentPackItem item)
        {
            return ItemName(item) + " (" + (item == null ? string.Empty : item.id) + ")";
        }

        private string RecipeListText(ContentPackRecipe recipe)
        {
            return RecipeTitle(recipe);
        }

        private static string StationText(ContentPackRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.station))
                return "No station";
            string station = string.Equals(recipe.station, "AmmoPress", StringComparison.OrdinalIgnoreCase) ? "Ammo press" : recipe.station;
            return recipe.level > 1 ? station + " (level " + recipe.level + ")" : station;
        }

        /// <summary>Recipes are named after what they make.</summary>
        private string RecipeTitle(ContentPackRecipe recipe)
        {
            string id = recipe == null ? null : recipe.resultItemId;
            return string.IsNullOrEmpty(id) ? "New recipe" : ResultName(recipe);
        }

        private string ResultName(ContentPackRecipe recipe)
        {
            string id = recipe == null ? null : recipe.resultItemId;
            if (string.IsNullOrEmpty(id))
                return "nothing yet";
            if (_project != null)
            {
                foreach (ContentPackItem item in _project.Content.items)
                    if (item != null && string.Equals(item.id, id, StringComparison.Ordinal))
                        return ItemName(item);
            }
            return _itemCatalog.NameFor(id);
        }

        private ContentPackItem FindItem(string id)
        {
            if (_project == null || string.IsNullOrEmpty(id))
                return null;
            foreach (ContentPackItem item in _project.Content.items)
                if (item != null && string.Equals(item.id, id, StringComparison.Ordinal))
                    return item;
            return null;
        }

        // ------------------------------------------------------------ thumbnails

        private Image GetThumbnail(ContentPackItem item)
        {
            if (_project == null || item == null || string.IsNullOrEmpty(item.iconPath))
                return null;
            string normalized;
            string full;
            string error;
            if (!ContentPackPathPolicy.TryResolveAsset(_project.RootPath, item.iconPath, out normalized, out full, out error))
                return null;
            try
            {
                if (!File.Exists(full))
                    return null;
                string key = full + "|" + File.GetLastWriteTimeUtc(full).Ticks.ToString(CultureInfo.InvariantCulture);
                Image image;
                if (!_thumbnails.TryGetValue(key, out image))
                {
                    image = IconPreview.LoadUnlocked(full);
                    _thumbnails[key] = image;
                }
                return image;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private void ClearThumbnails()
        {
            foreach (Image image in _thumbnails.Values)
                if (image != null)
                    image.Dispose();
            _thumbnails.Clear();
        }

        // ------------------------------------------------------------ field helpers

        private static void ClearFields(Dictionary<string, Control> fields)
        {
            foreach (Control control in fields.Values)
            {
                ItemPicker picker = control as ItemPicker;
                MaterialListEditor materials = control as MaterialListEditor;
                if (picker != null)
                {
                    picker.ItemId = string.Empty;
                    continue;
                }
                if (materials != null)
                {
                    materials.Materials = null;
                    continue;
                }
                TextBox text = control as TextBox;
                ComboBox choice = control as ComboBox;
                NumericUpDown number = control as NumericUpDown;
                CheckBox check = control as CheckBox;
                if (text != null) text.Text = string.Empty;
                if (choice != null)
                {
                    if (choice.DropDownStyle == ComboBoxStyle.DropDownList)
                        choice.SelectedIndex = choice.Items.Count > 0 ? 0 : -1;
                    else
                        choice.Text = string.Empty;
                }
                if (number != null) number.Value = number.Minimum;
                if (check != null) check.Checked = false;
            }
        }

        private static void SetText(Dictionary<string, Control> fields, string key, string value)
        {
            Control field = fields[key];
            ItemPicker picker = field as ItemPicker;
            if (picker != null)
            {
                picker.ItemId = value;
                return;
            }
            TextBox text = field as TextBox;
            ComboBox choice = field as ComboBox;
            if (text != null)
            {
                text.Text = value ?? string.Empty;
            }
            else if (choice != null)
            {
                if (choice.DropDownStyle != ComboBoxStyle.DropDownList)
                {
                    choice.Text = value ?? string.Empty;
                    return;
                }
                // Keep values the choice list does not know so a commit never erases them.
                if (!string.IsNullOrEmpty(value) && !choice.Items.Contains(value))
                    choice.Items.Add(value);
                choice.SelectedItem = value;
            }
        }

        private static string GetText(Dictionary<string, Control> fields, string key)
        {
            Control field = fields[key];
            ItemPicker picker = field as ItemPicker;
            if (picker != null)
                return picker.ItemId;
            TextBox text = field as TextBox;
            ComboBox choice = field as ComboBox;
            if (text != null)
                return text.Text.Trim();
            if (choice.DropDownStyle != ComboBoxStyle.DropDownList)
                return (choice.Text ?? string.Empty).Trim();
            return (choice.SelectedItem ?? string.Empty).ToString().Trim();
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

        private static void SetMaterials(
            Dictionary<string, Control> fields, string key, List<ContentPackIngredient> values)
        {
            ((MaterialListEditor)fields[key]).Materials = values;
        }

        private static List<ContentPackIngredient> GetMaterials(Dictionary<string, Control> fields, string key)
        {
            return ((MaterialListEditor)fields[key]).Materials;
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

        private static string FormatCount(int count, string singular)
        {
            return count + " " + singular + (count == 1 ? string.Empty : "s");
        }
    }
}
