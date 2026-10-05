using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
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
    public delegate void NexusSyncCompletedHandler(List<ModItem> mods, int mappedMods, int updateCount, string errorMessage);

    /// <summary>Asks the Nexus tab to install mods by reference, for example from an imported mod list.</summary>
    public delegate void NexusInstallRequestHandler(List<NexusModReference> references);

    /// <summary>
    /// The Mod manager tab: one list of installed mods with on/off switches
    /// and a drag-to-reorder load order, plus profiles, sharing, undo, drop to
    /// install, and one-click fixes for problems.
    /// </summary>
    public class ModManagerTab : UserControl
    {
        private const int MaximumUndoSteps = 30;

        // Controls
        private ModSwitchList _modList;
        private ModDetailsPanel _detailsPanel;
        private SearchBox _searchBox;
        private ActionButton _profileButton;
        private ActionButton _shareButton;
        private ActionButton _refreshNexusButton;
        private NoticeBanner _problemBanner;
        private NoticeBanner _actionBanner;
        private ContextMenuStrip _profileMenu;
        private ContextMenuStrip _shareMenu;
        private TableLayoutPanel _layout;
        private ToolTip _toolTip;
        private System.Windows.Forms.Timer _actionBannerTimer;

        // Services
        private ModDiscoveryService _discoveryService;
        private LoadOrderService _orderService;
        private NexusModsService _nexusService;
        private readonly NexusReferenceResolver _nexusResolver = new NexusReferenceResolver();
        private readonly ModListFileService _listService = new ModListFileService();
        private readonly NexusInstallService _installService = new NexusInstallService();

        // State
        private AppSettings _settings;
        private List<ModItem> _allMods = new List<ModItem>();
        private List<ModItem> _enabledMods = new List<ModItem>();
        private List<ModItem> _disabledMods = new List<ModItem>();
        private LoadOrderValidation _validation;
        private readonly HashSet<string> _newModIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime _newSinceUtc = DateTime.MaxValue;
        private readonly List<UndoStep> _undo = new List<UndoStep>();
        private string _dismissedProblem;
        private ModListFile _pendingList;
        private bool _isDarkMode = false;
        private int _nexusRefreshToken = 0;
        private DateTime _lastNexusRemoteSyncUtc = DateTime.MinValue;
        private int _cachedMappedMods = 0;
        private int _cachedUpdateCount = 0;
        private string _cachedNexusError = null;
        private bool _hasNexusSyncCache = false;
        private bool _lastIncludeNexusPrereleaseFiles = false;
        private string _lastNexusCacheSignature = string.Empty;
        private readonly Dictionary<string, NexusSyncCacheEntry> _nexusStateByModId = new Dictionary<string, NexusSyncCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan NexusSyncCooldown = TimeSpan.FromMinutes(5);

        private sealed class NexusSyncCacheEntry
        {
            public string ReferenceKey;
            public string RemoteVersion;
            public string RemoteSummary;
            public DateTime? RemoteUpdatedAtUtc;
            public string PageUrl;
            public bool HasUpdateAvailable;
        }

        /// <summary>The load order (and active profile) before one change, so it can be undone.</summary>
        private sealed class UndoStep
        {
            public string[] Order;
            public string ActiveProfile;
            public string Description;
        }

        /// <summary>
        /// Event raised when order is saved
        /// </summary>
        public event StringArrayHandler OrderSaved;
        public event NexusSyncCompletedHandler NexusSyncCompleted;

        /// <summary>Raised when the user asks to install every available update.</summary>
        public event EventHandler UpdateAllRequested;

        /// <summary>Raised when an imported list or profile needs mods installed from Nexus.</summary>
        public event NexusInstallRequestHandler NexusInstallRequested;

        public DateTime LastNexusRemoteSyncUtc { get { return _lastNexusRemoteSyncUtc; } }

        public ModManagerTab()
        {
            InitializeComponent();
            WireEvents();
        }

        /// <summary>Developer mode shows technical details such as mod IDs.</summary>
        public bool DeveloperMode
        {
            get { return _detailsPanel.ShowTechnicalDetails; }
            set { _detailsPanel.ShowTechnicalDetails = value; }
        }

        /// <summary>Mods installed after this time (the previous session) get a "New" badge.</summary>
        public DateTime NewSinceUtc
        {
            get { return _newSinceUtc; }
            set { _newSinceUtc = value; }
        }

        /// <summary>
        /// Initialize with services and settings
        /// </summary>
        public void Initialize(ModDiscoveryService discoveryService, LoadOrderService orderService, AppSettings settings, NexusModsService nexusService)
        {
            bool nexusServiceChanged = !object.ReferenceEquals(_nexusService, nexusService);
            _discoveryService = discoveryService;
            _orderService = orderService;
            _settings = settings;
            _nexusService = nexusService;

            bool includeNexusPrereleaseFiles = settings != null && settings.IncludeNexusPrereleaseFiles;
            string nexusCacheSignature = BuildNexusCacheSignature(settings);
            if (_hasNexusSyncCache && (
                nexusServiceChanged ||
                _lastIncludeNexusPrereleaseFiles != includeNexusPrereleaseFiles ||
                !string.Equals(_lastNexusCacheSignature, nexusCacheSignature, StringComparison.Ordinal)))
            {
                InvalidateNexusCache();
            }
            _lastIncludeNexusPrereleaseFiles = includeNexusPrereleaseFiles;
            _lastNexusCacheSignature = nexusCacheSignature;

            _detailsPanel.InstalledModApiVersion = settings.InstalledModApiVersion;
            UpdateProfileButton();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Name = "ModManagerTab";
            Padding = new Padding(16, 14, 16, 14);
            Font = ManagerTheme.UiFont(9f);
            _toolTip = new ToolTip { AutoPopDelay = 12000, InitialDelay = 400, ShowAlways = true };

            // Toolbar: search, profiles, sharing, update check.
            _searchBox = new SearchBox
            {
                Dock = DockStyle.Fill,
                Height = 30,
                Margin = new Padding(0, 1, 10, 1),
                Placeholder = "Search mods"
            };
            _searchBox.SetFieldName("Search mods");
            _profileButton = UiFactory.Button("Profiles ▾", ActionButtonKind.Secondary, delegate { ShowProfileMenu(); });
            _profileButton.AccessibleName = "Profiles";
            _shareButton = UiFactory.Button("Share ▾", ActionButtonKind.Secondary, delegate { ThemedMenu.ShowBelow(_shareMenu, _shareButton); });
            _shareButton.AccessibleName = "Share mod list";
            _refreshNexusButton = UiFactory.Button("Check for updates", ActionButtonKind.Quiet, delegate { RefreshNexusStatusAsync(true); });
            _refreshNexusButton.AccessibleName = "Check Nexus for mod updates";
            _profileButton.Margin = new Padding(0, 0, 8, 0);
            _shareButton.Margin = new Padding(0, 0, 8, 0);
            _refreshNexusButton.Margin = new Padding(0);
            _toolTip.SetToolTip(_profileButton, "Save and switch between sets of mods");
            _toolTip.SetToolTip(_shareButton, "Copy, save or use a list of mods");

            TableLayoutPanel toolbar = new TableLayoutPanel
            {
                ColumnCount = 4,
                RowCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0, 0, 0, 10)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.Controls.Add(_searchBox, 0, 0);
            toolbar.Controls.Add(_profileButton, 1, 0);
            toolbar.Controls.Add(_shareButton, 2, 0);
            toolbar.Controls.Add(_refreshNexusButton, 3, 0);

            _problemBanner = new NoticeBanner { Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
            _actionBanner = new NoticeBanner { Dock = DockStyle.Top, Margin = new Padding(0, 8, 0, 0) };

            _modList = new ModSwitchList
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                AllowDrop = true,
                EmptyText = "No mods installed yet.\r\nDrop a mod's .zip here to install it."
            };

            TableLayoutPanel listColumn = new TableLayoutPanel
            {
                ColumnCount = 1,
                RowCount = 4,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            listColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            listColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            listColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            listColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            listColumn.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            listColumn.Controls.Add(toolbar, 0, 0);
            listColumn.Controls.Add(_problemBanner, 0, 1);
            listColumn.Controls.Add(_modList, 0, 2);
            listColumn.Controls.Add(_actionBanner, 0, 3);
            // The list is the first stop for the keyboard; Ctrl+F jumps to search.
            _modList.TabIndex = 0;
            toolbar.TabIndex = 1;
            _problemBanner.TabIndex = 2;
            _actionBanner.TabIndex = 3;

            _detailsPanel = new ModDetailsPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(16, 0, 0, 0),
                MinimumSize = Size.Empty,
                Padding = new Padding(12),
                Name = "_detailsPanel"
            };

            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _layout.Controls.Add(listColumn, 0, 0);
            _layout.Controls.Add(_detailsPanel, 1, 0);
            Controls.Add(_layout);

            _shareMenu = ThemedMenu.Create();
            _shareMenu.Items.Add("Copy mod list", null, delegate { CopyModList(); });
            _shareMenu.Items.Add("Save mod list to a file...", null, delegate { ExportModList(); });
            _shareMenu.Items.Add(new ToolStripSeparator());
            _shareMenu.Items.Add("Use a mod list from a file...", null, delegate { ImportModList(); });

            _profileMenu = ThemedMenu.Create();
            _profileMenu.ShowCheckMargin = true;

            _actionBannerTimer = new System.Windows.Forms.Timer { Interval = 12000 };
            _actionBannerTimer.Tick += delegate
            {
                _actionBannerTimer.Stop();
                if (_actionBanner.Kind != NoticeKind.Error)
                    _actionBanner.Clear();
            };
            ResumeLayout(false);
        }

        private void WireEvents()
        {
            _modList.SelectionChanged += delegate(object sender, ModItem mod)
            {
                _detailsPanel.ShowMod(mod);
            };
            _modList.ToggleRequested += ModList_ToggleRequested;
            _modList.MoveRequested += ModList_MoveRequested;
            _modList.DragEnter += ModList_DragEnter;
            _modList.DragOver += ModList_DragEnter;
            _modList.DragLeave += delegate { _modList.FileDropActive = false; };
            _modList.DragDrop += ModList_DragDrop;
            _searchBox.SearchChanged += delegate(object sender, string text) { _modList.SetFilter(text); };
            _problemBanner.Dismissed += delegate { _dismissedProblem = _problemBanner.Message; };

            _detailsPanel.OpenFolderClicked += DetailsPanel_OpenFolderClicked;
            _detailsPanel.WebsiteClicked += DetailsPanel_WebsiteClicked;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z) && !(ActiveControl is TextBoxBase) && !_searchBox.ContainsFocus)
            {
                Undo();
                return true;
            }
            if (keyData == (Keys.Control | Keys.F))
            {
                _searchBox.Focus();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
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

        // ------------------------------------------------------------ refresh

        /// <summary>
        /// Refresh mod lists from disk
        /// </summary>
        public void RefreshMods()
        {
            RefreshMods(true);
        }

        private void RefreshMods(bool rediscover)
        {
            if (_settings == null || !_settings.IsModsPathValid || _discoveryService == null)
                return;

            if (rediscover)
                _allMods = _discoveryService.DiscoverMods(_settings.ModsPath);

            // Split into enabled/disabled
            var enabled = _orderService.GetEnabledMods(_allMods, _settings.ModsPath);
            var disabled = _orderService.GetDisabledMods(_allMods, _settings.ModsPath);

            // Validate and update status
            var validation = _orderService.ValidateOrder(enabled, _settings.ModsPath, _settings.SkipHarmonyDependencyCheck);

            foreach (var mod in _allMods)
            {
                if (mod.ApiCompatibility.Severity == ApiCompatibilitySeverity.Error)
                {
                    mod.Status = ModStatus.VersionMismatch;
                    mod.StatusMessage = mod.ApiCompatibility.Summary;
                }
                else if (!mod.HasValidAbout)
                {
                    mod.Status = ModStatus.Warning;
                    mod.StatusMessage = "Missing About.json";
                }
                else if (mod.ApiCompatibility.Severity == ApiCompatibilitySeverity.Warning)
                {
                    mod.Status = ModStatus.Warning;
                    mod.StatusMessage = mod.ApiCompatibility.Summary;
                }
                else
                {
                    mod.Status = mod.HasUpdateAvailable ? ModStatus.UpdateAvailable : ModStatus.Ok;
                    mod.StatusMessage = null;
                }
            }

            foreach (var mod in enabled)
            {
                if (validation.HardIssueModIds.Contains(mod.Id))
                {
                    mod.Status = ModStatus.Error;
                    string missingDependency = FindMissingDependencyMessage(validation.MissingDependencies, mod.Id);
                    mod.StatusMessage = !string.IsNullOrEmpty(missingDependency)
                        ? missingDependency
                        : "Missing dependency or load order issue";
                }
                else if (validation.SoftIssueModIds.Contains(mod.Id))
                {
                    mod.Status = ModStatus.Warning;
                    mod.StatusMessage = "Load order may not be optimal";
                }
            }

            _enabledMods = enabled;
            _disabledMods = disabled;
            _validation = validation;
            if (rediscover)
                UpdateNewMods();

            ModItem selected = _modList.SelectedMod;
            _modList.SetMods(enabled, disabled, validation.HardIssueModIds, validation.SoftIssueModIds,
                delegate(ModItem mod) { return _newModIds.Contains(mod.Id); });
            if (selected != null)
                _modList.SelectMod(selected.Id);
            if (_modList.SelectedMod == null)
                _detailsPanel.ShowMod(null);

            UpdateProblemBanner();
            UpdateButtonStates();
            UpdateProfileButton();
            if (rediscover)
                RefreshNexusStatusAsync();
        }

        /// <summary>A mod is new when its folder was created after the previous session.</summary>
        private void UpdateNewMods()
        {
            _newModIds.Clear();
            if (_newSinceUtc == DateTime.MaxValue)
                return;
            foreach (ModItem mod in _allMods)
            {
                try
                {
                    if (!string.IsNullOrEmpty(mod.RootPath) && Directory.GetCreationTimeUtc(mod.RootPath) > _newSinceUtc)
                        _newModIds.Add(mod.Id);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private void UpdateButtonStates()
        {
            bool ready = _settings != null && _settings.IsModsPathValid;
            _refreshNexusButton.Enabled = _allMods.Count > 0 && _settings != null && _settings.EnableNexusIntegration;
            _profileButton.Enabled = ready;
            _shareButton.Enabled = ready;
        }

        // ------------------------------------------------------------ changes and undo

        private void ModList_ToggleRequested(object sender, ModItem mod, bool turnOn)
        {
            if (mod == null || _settings == null)
                return;
            List<string> order = CurrentOrder();
            order.RemoveAll(delegate(string id) { return string.Equals(id, mod.Id, StringComparison.OrdinalIgnoreCase); });
            if (turnOn)
                order.Add(mod.Id);
            ApplyOrder(order, (turnOn ? "Turned on " : "Turned off ") + mod.DisplayName + ".", mod.Id);
        }

        private void ModList_MoveRequested(object sender, ModItem mod, int activeIndex)
        {
            if (mod == null || _settings == null)
                return;
            List<string> order = CurrentOrder();
            bool wasActive = order.RemoveAll(delegate(string id) { return string.Equals(id, mod.Id, StringComparison.OrdinalIgnoreCase); }) > 0;
            order.Insert(Math.Max(0, Math.Min(activeIndex, order.Count)), mod.Id);
            string description = wasActive
                ? "Moved " + mod.DisplayName + " to position " + (Math.Min(activeIndex, order.Count - 1) + 1) + "."
                : "Turned on " + mod.DisplayName + ".";
            ApplyOrder(order, description, mod.Id);
        }

        /// <summary>Active mod IDs in load order, as shown in the list.</summary>
        private List<string> CurrentOrder()
        {
            List<string> order = new List<string>();
            foreach (ModItem mod in _enabledMods)
                order.Add(mod.Id);
            return order;
        }

        /// <summary>
        /// Saves a new load order, remembering the old one for Undo. Entries
        /// for mods that are not installed are kept, as the file always did.
        /// </summary>
        private void ApplyOrder(IList<string> activeIds, string description, string selectId)
        {
            if (_settings == null || !_settings.IsModsPathValid)
                return;
            string[] previous = _orderService.ReadOrder(_settings.ModsPath);

            HashSet<string> installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModItem mod in _allMods)
                installed.Add(mod.Id);
            // Each ID once: two folders can share an ID, and the file lists mods, not folders.
            List<string> next = new List<string>();
            HashSet<string> listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in activeIds)
                if (!string.IsNullOrEmpty(id) && listed.Add(id))
                    next.Add(id);
            foreach (string id in previous)
                if (!installed.Contains(id) && listed.Add(id))
                    next.Add(id);

            // Only a change that was saved can be undone.
            if (!SaveOrder(next))
                return;
            PushUndo(previous, description);
            AfterOrderChanged(description, selectId, true);
        }

        private bool SaveOrder(IList<string> order)
        {
            try
            {
                _orderService.SaveOrder(_settings.ModsPath, order);
                return true;
            }
            catch (Exception ex)
            {
                ShowAction(NoticeKind.Error, "The load order could not be saved: " + ex.Message, null, null);
                return false;
            }
        }

        private void AfterOrderChanged(string description, string selectId, bool offerUndo)
        {
            RefreshMods(false);
            if (!string.IsNullOrEmpty(selectId))
                _modList.SelectMod(selectId);
            SyncActiveProfile();
            if (!string.IsNullOrEmpty(description))
            {
                if (offerUndo)
                    ShowAction(NoticeKind.Info, description, "Undo", delegate { Undo(); });
                else
                    ShowAction(NoticeKind.Info, description, null, null);
            }
            if (OrderSaved != null)
                OrderSaved(_enabledMods.Select(m => m.Id).ToArray());
        }

        private void PushUndo(string[] order, string description)
        {
            UndoStep step = new UndoStep();
            step.Order = order ?? new string[0];
            step.ActiveProfile = ActiveProfile();
            step.Description = description;
            _undo.Add(step);
            if (_undo.Count > MaximumUndoSteps)
                _undo.RemoveAt(0);
        }

        /// <summary>Restores the load order from before the last change (Ctrl+Z).</summary>
        public void Undo()
        {
            if (_undo.Count == 0 || _settings == null)
            {
                ShowAction(NoticeKind.Info, "Nothing to undo.", null, null);
                return;
            }
            UndoStep step = _undo[_undo.Count - 1];
            // The step stays available if the restore could not be saved.
            if (!SaveOrder(step.Order))
                return;
            _undo.RemoveAt(_undo.Count - 1);
            RestoreActiveProfile(step.ActiveProfile);
            RefreshMods(false);
            SyncActiveProfile();
            string message = "Undid: " + (step.Description ?? "last change").TrimEnd('.') + ".";
            if (_undo.Count > 0)
                ShowAction(NoticeKind.Info, message, "Undo more", delegate { Undo(); });
            else
                ShowAction(NoticeKind.Info, message, null, null);
            if (OrderSaved != null)
                OrderSaved(_enabledMods.Select(m => m.Id).ToArray());
        }

        private void ShowAction(NoticeKind kind, string message, string actionText, EventHandler action)
        {
            _actionBanner.Show(kind, message, actionText, action);
            _actionBannerTimer.Stop();
            if (kind != NoticeKind.Error)
                _actionBannerTimer.Start();
        }

        /// <summary>
        /// Turns on an installed mod by ID, for example right after the
        /// Content Workshop installs a pack. Returns false when the mod is not
        /// in this manager's mods folder.
        /// </summary>
        public bool EnableModById(string modId)
        {
            if (_settings == null || !_settings.IsModsPathValid || _orderService == null || string.IsNullOrEmpty(modId))
                return false;

            RefreshMods();
            ModItem mod = FindMod(modId);
            if (mod == null)
                return false;
            if (_enabledMods.Contains(mod))
                return true;
            List<string> order = CurrentOrder();
            order.Add(mod.Id);
            ApplyOrder(order, "Turned on " + mod.DisplayName + ".", mod.Id);
            return true;
        }

        private ModItem FindMod(string modId)
        {
            foreach (ModItem mod in _allMods)
                if (string.Equals(mod.Id, modId, StringComparison.OrdinalIgnoreCase))
                    return mod;
            return null;
        }

        // ------------------------------------------------------------ problems and fixes

        /// <summary>
        /// Shows the most important problem with a button that fixes it:
        /// a missing or switched-off dependency, then load order, then updates.
        /// </summary>
        private void UpdateProblemBanner()
        {
            string message = null;
            string actionText = null;
            EventHandler action = null;
            NoticeKind kind = NoticeKind.Warning;

            DependencyProblem dependency = FirstDependencyProblem();
            if (dependency != null)
            {
                ModItem owner = FindMod(dependency.ModId);
                ModItem needed = FindMod(dependency.DependencyId);
                string ownerName = owner != null ? owner.DisplayName : dependency.ModId;
                kind = NoticeKind.Error;
                if (dependency.NeedsNewerVersion)
                {
                    message = ownerName + " needs a newer version of " + (needed != null ? needed.DisplayName : dependency.DependencyId) + ".";
                    if (needed != null && needed.HasNexusReference && needed.HasUpdateAvailable)
                    {
                        actionText = "Update all";
                        action = delegate { RequestUpdateAll(); };
                    }
                }
                else if (needed != null)
                {
                    message = ownerName + " needs " + needed.DisplayName + ", which is turned off.";
                    actionText = "Turn on " + needed.DisplayName;
                    ModItem target = needed;
                    action = delegate { TurnOnDependency(target); };
                }
                else
                {
                    message = ownerName + " needs " + dependency.DependencyId + ", which isn't installed.";
                    if (_settings != null && _settings.EnableNexusIntegration)
                    {
                        actionText = "Find on Nexus";
                        string search = dependency.DependencyId;
                        action = delegate { OpenNexusSearch(search); };
                    }
                }
                int more = CountDependencyProblems() - 1;
                if (more > 0)
                    message += " (" + more + " more " + (more == 1 ? "problem" : "problems") + ")";
            }
            else if (_validation != null && _validation.HasIssues)
            {
                message = "Some mods are loading in an order their authors say won't work.";
                actionText = "Fix load order";
                action = delegate { FixLoadOrder(); };
            }
            else if (CountModsWithUpdates(_allMods) > 0)
            {
                int updates = CountModsWithUpdates(_allMods);
                kind = NoticeKind.Info;
                message = updates == 1 ? "1 update is available on Nexus." : updates + " updates are available on Nexus.";
                actionText = "Update all";
                action = delegate { RequestUpdateAll(); };
            }

            if (message == null || string.Equals(message, _dismissedProblem, StringComparison.Ordinal))
            {
                _problemBanner.Clear();
                return;
            }
            _problemBanner.Show(kind, message, actionText, action);
        }

        private DependencyProblem FirstDependencyProblem()
        {
            if (_validation == null)
                return null;
            foreach (DependencyProblem problem in _validation.DependencyProblems)
                if (problem != null && !string.IsNullOrEmpty(problem.DependencyId))
                    return problem;
            return null;
        }

        private int CountDependencyProblems()
        {
            int count = 0;
            if (_validation != null)
                foreach (DependencyProblem problem in _validation.DependencyProblems)
                    if (problem != null && !string.IsNullOrEmpty(problem.DependencyId))
                        count++;
            return count;
        }

        /// <summary>Turns a dependency on just ahead of the first mod that needs it.</summary>
        private void TurnOnDependency(ModItem dependency)
        {
            List<string> order = CurrentOrder();
            int insertAt = order.Count;
            foreach (DependencyProblem problem in _validation.DependencyProblems)
            {
                if (!string.Equals(problem.DependencyId, dependency.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                int index = order.FindIndex(delegate(string id) { return string.Equals(id, problem.ModId, StringComparison.OrdinalIgnoreCase); });
                if (index >= 0 && index < insertAt)
                    insertAt = index;
            }
            order.Insert(insertAt, dependency.Id);
            ApplyOrder(order, "Turned on " + dependency.DisplayName + ".", dependency.Id);
        }

        /// <summary>Reorders active mods so every "load before/after" rule is met, moving as little as possible.</summary>
        public void FixLoadOrder()
        {
            List<string> fixedOrder = LoadOrderService.FixOrder(_enabledMods);
            if (SameOrder(fixedOrder, CurrentOrder()))
            {
                ShowAction(NoticeKind.Info, "The load order already meets every mod's rules.", null, null);
                return;
            }
            ApplyOrder(fixedOrder, "Fixed the load order.", _modList.SelectedMod != null ? _modList.SelectedMod.Id : null);
        }

        private static bool SameOrder(IList<string> a, IList<string> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            return true;
        }

        private void OpenNexusSearch(string text)
        {
            string domain = _settings != null && !string.IsNullOrEmpty(_settings.NexusGameDomain) ? _settings.NexusGameDomain : "sheltered";
            string url = "https://www.nexusmods.com/" + Uri.EscapeDataString(domain) + "/search/?gsearch=" +
                Uri.EscapeDataString(text ?? string.Empty) + "&gsearchtype=mods";
            try { System.Diagnostics.Process.Start(url); }
            catch { }
        }

        private void RequestUpdateAll()
        {
            if (UpdateAllRequested != null)
                UpdateAllRequested(this, EventArgs.Empty);
        }

        // ------------------------------------------------------------ drop to install

        private static List<string> DroppedPackages(IDataObject data)
        {
            List<string> packages = new List<string>();
            string[] paths = data != null && data.GetDataPresent(DataFormats.FileDrop) ? data.GetData(DataFormats.FileDrop) as string[] : null;
            if (paths == null)
                return packages;
            foreach (string path in paths)
            {
                if (Directory.Exists(path) || (File.Exists(path) && string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)))
                    packages.Add(path);
            }
            return packages;
        }

        private void ModList_DragEnter(object sender, DragEventArgs e)
        {
            bool accepted = _settings != null && _settings.IsModsPathValid && DroppedPackages(e.Data).Count > 0;
            e.Effect = accepted ? DragDropEffects.Copy : DragDropEffects.None;
            _modList.FileDropActive = accepted;
        }

        private void ModList_DragDrop(object sender, DragEventArgs e)
        {
            _modList.FileDropActive = false;
            List<string> packages = DroppedPackages(e.Data);
            if (packages.Count == 0 || _settings == null || !_settings.IsModsPathValid)
                return;
            // Let the drag source finish before any dialog appears.
            BeginInvoke((MethodInvoker)delegate { InstallPackages(packages); });
        }

        /// <summary>Installs dropped .zip files or mod folders and turns each one on.</summary>
        public void InstallPackages(IList<string> packages)
        {
            List<string> installed = new List<string>();
            List<string> failures = new List<string>();
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (string package in packages)
                {
                    string modId;
                    string error;
                    NexusInstallResult result = _installService.InstallLocalPackage(package, _settings.ModsPath, ConfirmReplace, out modId, out error);
                    if (result != null && !string.IsNullOrEmpty(modId))
                        installed.Add(modId);
                    else if (!string.IsNullOrEmpty(error))
                        failures.Add(Path.GetFileName(package.TrimEnd('\\', '/')) + ": " + error);
                }
            }
            finally
            {
                Cursor.Current = previous;
            }

            RefreshMods();
            if (installed.Count > 0)
            {
                List<string> order = CurrentOrder();
                List<string> names = new List<string>();
                foreach (string id in installed)
                {
                    ModItem mod = FindMod(id);
                    names.Add(mod != null ? mod.DisplayName : id);
                    if (!order.Exists(delegate(string existing) { return string.Equals(existing, id, StringComparison.OrdinalIgnoreCase); }))
                        order.Add(id);
                }
                string description = "Installed " + JoinNames(names) + " and turned " + (names.Count == 1 ? "it" : "them") + " on.";
                ApplyOrder(order, description, installed[installed.Count - 1]);
            }
            if (failures.Count > 0)
                ShowAction(NoticeKind.Error, (installed.Count > 0 ? "Some mods could not be installed. " : string.Empty) + string.Join(" ", failures.ToArray()), null, null);
        }

        private bool ConfirmReplace(string modName, string existingPath)
        {
            ModItem existing = null;
            foreach (ModItem mod in _allMods)
                if (string.Equals(mod.RootPath, existingPath, StringComparison.OrdinalIgnoreCase))
                    existing = mod;
            string current = existing != null && !string.IsNullOrEmpty(existing.Version) ? " (version " + existing.Version + ")" : string.Empty;
            using (ManagerDialog dialog = new ManagerDialog(
                "Replace mod",
                "Replace " + (modName ?? "this mod") + "?",
                (modName ?? "This mod") + current + " is already installed. Replace it with the one you dropped? The current copy is kept in the mods folder's _smm_backup folder."))
            {
                dialog.AddButton("Replace", DialogResult.OK, ActionButtonKind.Primary);
                dialog.AddButton("Cancel", DialogResult.Cancel, ActionButtonKind.Quiet);
                return dialog.ShowDialog(FindForm()) == DialogResult.OK;
            }
        }

        // ------------------------------------------------------------ profiles

        private string ActiveProfile()
        {
            return _settings != null && _settings.IsModsPathValid ? _listService.GetActiveProfile(_settings.ModsPath) : null;
        }

        private void UpdateProfileButton()
        {
            if (_profileButton == null)
                return;
            string active = ActiveProfile();
            _profileButton.Text = string.IsNullOrEmpty(active) ? "Profiles ▾" : "Profile: " + active + " ▾";
            _profileButton.AccessibleName = string.IsNullOrEmpty(active) ? "Profiles" : "Profile: " + active;
        }

        private void ShowProfileMenu()
        {
            if (_settings == null || !_settings.IsModsPathValid)
                return;
            _profileMenu.Items.Clear();
            string active = ActiveProfile();
            List<string> profiles = _listService.ListProfiles(_settings.ModsPath);
            foreach (string name in profiles)
            {
                string profile = name;
                ToolStripMenuItem item = new ToolStripMenuItem(profile, null, delegate { SwitchProfile(profile); });
                item.Checked = string.Equals(profile, active, StringComparison.OrdinalIgnoreCase);
                _profileMenu.Items.Add(item);
            }
            if (profiles.Count == 0)
                _profileMenu.Items.Add(new ToolStripMenuItem("No profiles yet") { Enabled = false });
            _profileMenu.Items.Add(new ToolStripSeparator());
            _profileMenu.Items.Add("Save current mods as a new profile...", null, delegate { SaveNewProfile(); });
            if (!string.IsNullOrEmpty(active))
            {
                _profileMenu.Items.Add("Rename “" + active + "”...", null, delegate { RenameProfile(active); });
                _profileMenu.Items.Add("Delete “" + active + "”", null, delegate { DeleteProfile(active); });
                _profileMenu.Items.Add("Stop using profiles", null, delegate
                {
                    _listService.SetActiveProfile(_settings.ModsPath, null);
                    UpdateProfileButton();
                    ShowAction(NoticeKind.Info, "Changes are no longer saved to a profile.", null, null);
                });
            }
            ThemedMenu.ShowBelow(_profileMenu, _profileButton);
        }

        private void SaveNewProfile()
        {
            string name = AskForName("New profile", "Save these mods as a profile",
                "A profile remembers which mods are on and their order. Switch profiles to change your whole setup in one click.",
                "Profile name", SuggestProfileName());
            if (name == null)
                return;
            try
            {
                _listService.SaveProfile(_settings.ModsPath, name, _enabledMods);
                _listService.SetActiveProfile(_settings.ModsPath, name);
            }
            catch (Exception ex)
            {
                ShowAction(NoticeKind.Error, "The profile could not be saved: " + ex.Message, null, null);
                return;
            }
            UpdateProfileButton();
            ShowAction(NoticeKind.Success, "Saved the profile “" + name + "”. Changes you make are saved to it.", null, null);
        }

        private void RenameProfile(string oldName)
        {
            string name = AskForName("Rename profile", "Rename “" + oldName + "”", null, "Profile name", oldName);
            if (name == null || string.Equals(name, oldName, StringComparison.Ordinal))
                return;
            string error;
            if (!_listService.RenameProfile(_settings.ModsPath, oldName, name, out error))
            {
                ShowAction(NoticeKind.Error, error, null, null);
                return;
            }
            UpdateProfileButton();
            ShowAction(NoticeKind.Success, "Renamed the profile to “" + name + "”.", null, null);
        }

        private void DeleteProfile(string name)
        {
            using (ManagerDialog dialog = new ManagerDialog("Delete profile", "Delete “" + name + "”?",
                "Your mods stay as they are; only the saved profile is removed."))
            {
                dialog.AddButton("Delete profile", DialogResult.OK, ActionButtonKind.Danger);
                dialog.AddButton("Cancel", DialogResult.Cancel, ActionButtonKind.Quiet);
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;
            }
            try
            {
                _listService.DeleteProfile(_settings.ModsPath, name);
            }
            catch (Exception ex)
            {
                ShowAction(NoticeKind.Error, "The profile could not be deleted: " + ex.Message, null, null);
                return;
            }
            UpdateProfileButton();
            ShowAction(NoticeKind.Info, "Deleted the profile “" + name + "”.", null, null);
        }

        private void SwitchProfile(string name)
        {
            ModListFile profile;
            string error;
            if (!_listService.TryLoadProfile(_settings.ModsPath, name, out profile, out error))
            {
                ShowAction(NoticeKind.Error, error, null, null);
                return;
            }
            // Save the profile being left so it keeps any last changes.
            RefreshMods();
            SyncActiveProfile();
            List<string> order = InstalledOrderOf(profile);
            string[] previousOrder = _orderService.ReadOrder(_settings.ModsPath);
            if (!SaveOrder(order))
                return;
            PushUndo(previousOrder, "Switched to the profile “" + name + "”.");
            _listService.SetActiveProfile(_settings.ModsPath, name);
            RefreshMods(false);
            UpdateProfileButton();
            if (OrderSaved != null)
                OrderSaved(_enabledMods.Select(m => m.Id).ToArray());
            OfferMissingMods(profile, "Switched to “" + name + "”.");
        }

        /// <summary>After any change, the active profile is updated to match.</summary>
        private void SyncActiveProfile()
        {
            string active = ActiveProfile();
            if (string.IsNullOrEmpty(active))
                return;
            try
            {
                _listService.SaveProfile(_settings.ModsPath, active, _enabledMods);
            }
            catch
            {
                // The load order itself was saved; a stale profile is not worth an error here.
            }
        }

        private void RestoreActiveProfile(string name)
        {
            try
            {
                _listService.SetActiveProfile(_settings.ModsPath, string.IsNullOrEmpty(name) || !_listService.ProfileExists(_settings.ModsPath, name) ? null : name);
            }
            catch
            {
            }
            UpdateProfileButton();
        }

        private string SuggestProfileName()
        {
            for (int n = 1; ; n++)
            {
                string candidate = n == 1 ? "My mods" : "My mods " + n;
                if (!_listService.ProfileExists(_settings.ModsPath, candidate))
                    return candidate;
            }
        }

        private string AskForName(string windowTitle, string heading, string message, string label, string value)
        {
            string current = value;
            while (true)
            {
                using (ManagerDialog dialog = new ManagerDialog(windowTitle, heading, message))
                {
                    TextBox input = dialog.AddTextInput(label, current);
                    dialog.AddButton("Save", DialogResult.OK, ActionButtonKind.Primary);
                    dialog.AddButton("Cancel", DialogResult.Cancel, ActionButtonKind.Quiet);
                    if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                        return null;
                    current = input.Text.Trim();
                }
                string error;
                if (ModListFileService.IsValidProfileName(current, out error))
                    return current;
                message = error;
            }
        }

        // ------------------------------------------------------------ sharing

        private void CopyModList()
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("My Sheltered mods (" + _enabledMods.Count + ", in load order):");
            for (int i = 0; i < _enabledMods.Count; i++)
            {
                ModItem mod = _enabledMods[i];
                text.Append(i + 1).Append(". ").Append(mod.DisplayName);
                if (!string.IsNullOrEmpty(mod.Version) && mod.Version != "Unknown")
                    text.Append(' ').Append(mod.Version);
                if (!string.IsNullOrEmpty(mod.NexusPageUrl))
                    text.Append(" - ").Append(mod.NexusPageUrl);
                text.AppendLine();
            }
            try
            {
                Clipboard.SetText(text.ToString());
                ShowAction(NoticeKind.Success, "Copied your mod list. Paste it anywhere with Ctrl+V.", null, null);
            }
            catch (Exception ex)
            {
                ShowAction(NoticeKind.Error, "The clipboard is busy: " + ex.Message, null, null);
            }
        }

        private void ExportModList()
        {
            string path;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = ModListFileService.FileFilter;
                dialog.FileName = "My Sheltered mods" + ModListFileService.FileExtension;
                dialog.Title = "Save your mod list";
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;
                path = dialog.FileName;
            }
            try
            {
                _listService.Save(path, _listService.Build(Path.GetFileNameWithoutExtension(path), _enabledMods));
            }
            catch (Exception ex)
            {
                ShowAction(NoticeKind.Error, "The mod list could not be saved: " + ex.Message, null, null);
                return;
            }
            string saved = path;
            ShowAction(NoticeKind.Success, "Saved your mod list. Send the file to a friend; they can use it from Share in their Mod manager.",
                "Show file", delegate
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + saved + "\""); }
                    catch { }
                });
        }

        private void ImportModList()
        {
            string path;
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = ModListFileService.FileFilter;
                dialog.Title = "Use a mod list";
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;
                path = dialog.FileName;
            }
            ModListFile list;
            string error;
            if (!_listService.TryLoad(path, out list, out error))
            {
                ShowAction(NoticeKind.Error, error, null, null);
                return;
            }

            List<string> lines = new List<string>();
            List<NexusModReference> downloads = MissingNexusReferences(list);
            int installedCount = 0;
            int unavailable = 0;
            foreach (ModListEntry entry in list.mods)
            {
                string name = string.IsNullOrEmpty(entry.name) ? entry.id : entry.name;
                if (FindMod(entry.id) != null)
                {
                    installedCount++;
                    lines.Add("✓  " + name);
                }
                else if (CanDownload(entry))
                    lines.Add("↓  " + name + "  (will be installed from Nexus)");
                else
                {
                    unavailable++;
                    lines.Add("✗  " + name + "  (not installed, and not on Nexus)");
                }
            }

            string message = installedCount == 1
                ? "Turns on the 1 installed mod from this list and turns the others off. You can undo this."
                : "Turns on the " + installedCount + " installed mods from this list in its order, and turns the others off. You can undo this.";
            if (unavailable > 0)
                message += " " + unavailable + (unavailable == 1 ? " mod has" : " mods have") + " to be installed by hand.";
            using (ManagerDialog dialog = new ManagerDialog("Use mod list",
                "Use “" + (string.IsNullOrEmpty(list.name) ? Path.GetFileNameWithoutExtension(path) : list.name) + "”?",
                message))
            {
                dialog.AddLines(lines);
                ActionButton install = null;
                if (downloads.Count > 0)
                    install = dialog.AddButton("Use list and install " + downloads.Count + " from Nexus", DialogResult.Yes, ActionButtonKind.Primary);
                dialog.AddButton(downloads.Count > 0 ? "Use without installing" : "Use list", DialogResult.OK,
                    downloads.Count > 0 ? ActionButtonKind.Secondary : ActionButtonKind.Primary);
                dialog.AddButton("Cancel", DialogResult.Cancel, ActionButtonKind.Quiet);
                DialogResult choice = dialog.ShowDialog(FindForm());
                if (choice != DialogResult.OK && choice != DialogResult.Yes)
                    return;

                ApplyOrder(InstalledOrderOf(list), "Now using the mod list “" + (list.name ?? "list") + "”.", null);
                if (choice == DialogResult.Yes && install != null)
                {
                    _pendingList = list;
                    if (NexusInstallRequested != null)
                        NexusInstallRequested(downloads);
                }
            }
        }

        /// <summary>
        /// After Nexus installs for an imported list finish, the list's order
        /// is applied again so the newly installed mods join in the right place.
        /// </summary>
        public void ApplyPendingList()
        {
            ModListFile list = _pendingList;
            _pendingList = null;
            if (list == null || _settings == null || !_settings.IsModsPathValid)
                return;
            RefreshMods();
            ApplyOrder(InstalledOrderOf(list), "Turned on the mods installed from the list.", null);
        }

        /// <summary>Called when the Nexus installs for an imported list did not start.</summary>
        public void CancelPendingList()
        {
            _pendingList = null;
        }

        private void OfferMissingMods(ModListFile list, string prefix)
        {
            int missing = 0;
            foreach (ModListEntry entry in list.mods)
                if (FindMod(entry.id) == null)
                    missing++;
            List<NexusModReference> downloads = MissingNexusReferences(list);
            if (missing == 0)
            {
                ShowAction(NoticeKind.Info, prefix, "Undo", delegate { Undo(); });
                return;
            }
            string message = prefix + " " + missing + (missing == 1 ? " mod in it isn't installed." : " mods in it aren't installed.");
            if (downloads.Count > 0 && NexusInstallRequested != null)
            {
                ModListFile pending = list;
                ShowAction(NoticeKind.Warning, message, "Install from Nexus", delegate
                {
                    _pendingList = pending;
                    NexusInstallRequested(downloads);
                });
            }
            else
            {
                ShowAction(NoticeKind.Warning, message, "Undo", delegate { Undo(); });
            }
        }

        private List<string> InstalledOrderOf(ModListFile list)
        {
            List<string> order = new List<string>();
            foreach (string id in ModListFileService.OrderOf(list))
            {
                ModItem mod = FindMod(id);
                if (mod != null && !order.Contains(mod.Id))
                    order.Add(mod.Id);
            }
            return order;
        }

        private bool CanDownload(ModListEntry entry)
        {
            string domain = _settings != null && !string.IsNullOrEmpty(_settings.NexusGameDomain) ? _settings.NexusGameDomain : "sheltered";
            return _settings != null && _settings.EnableNexusIntegration && entry.nexusModId > 0 &&
                string.Equals(entry.nexusGameDomain, domain, StringComparison.OrdinalIgnoreCase);
        }

        private List<NexusModReference> MissingNexusReferences(ModListFile list)
        {
            List<NexusModReference> references = new List<NexusModReference>();
            foreach (ModListEntry entry in list.mods)
            {
                if (FindMod(entry.id) != null || !CanDownload(entry))
                    continue;
                NexusModReference reference = new NexusModReference { GameDomain = entry.nexusGameDomain, ModId = entry.nexusModId };
                if (!references.Exists(delegate(NexusModReference existing) { return existing.ModId == reference.ModId; }))
                    references.Add(reference);
            }
            return references;
        }

        private static string JoinNames(List<string> names)
        {
            if (names.Count <= 1)
                return names.Count == 1 ? names[0] : string.Empty;
            if (names.Count == 2)
                return names[0] + " and " + names[1];
            return names.Count + " mods";
        }

        public List<ModItem> GetAllModsSnapshot()
        {
            return new List<ModItem>(_allMods);
        }

        public void InvalidateNexusCache()
        {
            _nexusRefreshToken++;
            _lastNexusRemoteSyncUtc = DateTime.MinValue;
            _cachedMappedMods = 0;
            _cachedUpdateCount = 0;
            _cachedNexusError = null;
            _hasNexusSyncCache = false;
            _nexusStateByModId.Clear();
        }

        private void RefreshNexusStatusAsync()
        {
            RefreshNexusStatusAsync(false);
        }

        private void RefreshNexusStatusAsync(bool forceRefresh)
        {
            if (_allMods == null || _allMods.Count == 0)
            {
                _nexusStateByModId.Clear();
                _cachedMappedMods = 0;
                _cachedUpdateCount = 0;
                _cachedNexusError = null;
                _hasNexusSyncCache = true;
                NotifyNexusSync(0, 0, null);
                return;
            }

            if (forceRefresh)
            {
                InvalidateNexusCache();
                if (_nexusService != null)
                    _nexusService.ClearCachedResponses();
            }

            bool withinCooldown = !forceRefresh && IsWithinNexusSyncCooldown();

            // Resolve local references immediately so details panel can show mapping info.
            var referencesByModId = new Dictionary<string, NexusModReference>(StringComparer.OrdinalIgnoreCase);
            var unresolvedMods = new List<ModItem>();
            string fallbackDomain = (_settings != null && !string.IsNullOrEmpty(_settings.NexusGameDomain))
                ? _settings.NexusGameDomain
                : "sheltered";

            foreach (var mod in _allMods)
            {
                if (mod == null) continue;

                var reference = _nexusResolver.Resolve(mod, fallbackDomain);
                if (reference != null && reference.IsValid)
                {
                    mod.NexusGameDomain = reference.GameDomain;
                    mod.NexusModId = reference.ModId;
                    mod.NexusPageUrl = "https://www.nexusmods.com/" + reference.GameDomain + "/mods/" + reference.ModId;
                    referencesByModId[mod.Id] = reference;

                    if (withinCooldown)
                    {
                        ApplyCachedNexusState(mod, reference);
                    }
                    else
                    {
                        mod.HasUpdateAvailable = false;
                        mod.NexusRemoteVersion = string.Empty;
                        mod.NexusRemoteSummary = string.Empty;
                        mod.NexusRemoteUpdatedAtUtc = null;
                    }
                }
                else
                {
                    mod.NexusGameDomain = string.Empty;
                    mod.NexusModId = 0;
                    mod.NexusPageUrl = string.Empty;
                    mod.HasUpdateAvailable = false;
                    mod.NexusRemoteVersion = string.Empty;
                    mod.NexusRemoteSummary = string.Empty;
                    mod.NexusRemoteUpdatedAtUtc = null;
                    _nexusStateByModId.Remove(mod.Id);
                    unresolvedMods.Add(mod);
                }

                ApplyUpdateStatus(mod, mod.HasUpdateAvailable);
            }

            InvalidateModListsAndDetails();

            if (withinCooldown)
            {
                int updates = CountModsWithUpdates(_allMods);
                _cachedMappedMods = referencesByModId.Count;
                _cachedUpdateCount = updates;
                _cachedNexusError = null;
                _hasNexusSyncCache = true;
                NotifyNexusSync(_cachedMappedMods, _cachedUpdateCount, _cachedNexusError);
                return;
            }

            bool nexusEnabled = _settings != null && _settings.EnableNexusIntegration;
            if (!nexusEnabled || _nexusService == null || (referencesByModId.Count == 0 && unresolvedMods.Count == 0))
            {
                NotifyNexusSync(referencesByModId.Count, 0, null);
                return;
            }

            var requestRefs = new List<NexusModReference>(referencesByModId.Values);
            int token = ++_nexusRefreshToken;
            DateTime syncTimestampUtc = DateTime.UtcNow;

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error;
                var remoteByRef = _nexusService.GetModsByReferences(requestRefs, out error);
                var prereleaseFilesByRef = GetPrereleaseFilesByReference(referencesByModId, remoteByRef, _settings != null && _settings.IncludeNexusPrereleaseFiles, ref error);

                if (unresolvedMods.Count > 0)
                {
                    string feedError;
                    var feed = _nexusService.GetLatestMods(fallbackDomain, 200, out feedError);

                    if (feed != null && feed.Count > 0)
                    {
                        var nameBuckets = new Dictionary<string, List<NexusRemoteMod>>(StringComparer.OrdinalIgnoreCase);
                        foreach (var remote in feed)
                        {
                            if (remote == null || remote.ModId <= 0) continue;
                            string key = NormalizeNameKey(remote.Name);
                            if (string.IsNullOrEmpty(key)) continue;

                            List<NexusRemoteMod> bucket;
                            if (!nameBuckets.TryGetValue(key, out bucket))
                            {
                                bucket = new List<NexusRemoteMod>();
                                nameBuckets[key] = bucket;
                            }
                            bucket.Add(remote);
                        }

                        foreach (var local in unresolvedMods)
                        {
                            if (referencesByModId.ContainsKey(local.Id))
                                continue;

                            string localKey = NormalizeNameKey(local.DisplayName);
                            if (string.IsNullOrEmpty(localKey))
                                localKey = NormalizeNameKey(local.Id);
                            if (string.IsNullOrEmpty(localKey))
                                continue;

                            List<NexusRemoteMod> candidates;
                            if (!nameBuckets.TryGetValue(localKey, out candidates) || candidates.Count != 1)
                                continue;

                            var inferred = candidates[0];
                            var inferredRef = new NexusModReference
                            {
                                GameDomain = inferred.GameDomain,
                                ModId = inferred.ModId
                            };

                            referencesByModId[local.Id] = inferredRef;
                            remoteByRef[inferredRef.Key] = inferred;
                            TryAddPrereleaseFile(prereleaseFilesByRef, inferredRef, inferred, _settings != null && _settings.IncludeNexusPrereleaseFiles, ref error);
                            TryPersistNexusSidecar(local, inferredRef);
                        }
                    }

                    foreach (var local in unresolvedMods)
                    {
                        if (referencesByModId.ContainsKey(local.Id))
                            continue;

                        string searchName = !string.IsNullOrEmpty(local.DisplayName) ? local.DisplayName : local.Id;
                        if (string.IsNullOrEmpty(searchName))
                            continue;

                        string searchError;
                        var searchResults = _nexusService.FindModsByName(fallbackDomain, searchName, 10, out searchError);
                        if (!string.IsNullOrEmpty(searchError))
                            continue;

                        if (searchResults == null || searchResults.Count == 0)
                            continue;

                        NexusRemoteMod exact = null;
                        string localKey = NormalizeNameKey(searchName);
                        int exactCount = 0;

                        foreach (var candidate in searchResults)
                        {
                            if (candidate == null || candidate.ModId <= 0) continue;
                            if (!string.Equals(NormalizeNameKey(candidate.Name), localKey, StringComparison.OrdinalIgnoreCase))
                                continue;

                            exact = candidate;
                            exactCount++;
                        }

                        if (exactCount != 1 || exact == null)
                            continue;

                        var inferred = new NexusModReference
                        {
                            GameDomain = exact.GameDomain,
                            ModId = exact.ModId
                        };

                        referencesByModId[local.Id] = inferred;
                        remoteByRef[inferred.Key] = exact;
                        TryAddPrereleaseFile(prereleaseFilesByRef, inferred, exact, _settings != null && _settings.IncludeNexusPrereleaseFiles, ref error);
                        TryPersistNexusSidecar(local, inferred);
                    }
                }

                if (IsDisposed || Disposing)
                    return;

                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (token != _nexusRefreshToken)
                            return;

                        int updates = 0;

                        foreach (var mod in _allMods)
                        {
                            NexusModReference reference;
                            if (!referencesByModId.TryGetValue(mod.Id, out reference))
                                continue;

                            mod.NexusGameDomain = reference.GameDomain;
                            mod.NexusModId = reference.ModId;

                            NexusRemoteMod remote;
                            if (!remoteByRef.TryGetValue(reference.Key, out remote))
                            {
                                mod.HasUpdateAvailable = false;
                                mod.NexusRemoteVersion = string.Empty;
                                mod.NexusRemoteSummary = string.Empty;
                                mod.NexusRemoteUpdatedAtUtc = null;
                                ApplyUpdateStatus(mod, false);
                                _nexusStateByModId.Remove(mod.Id);
                                continue;
                            }

                            NexusRemoteModFile prereleaseFile;
                            prereleaseFilesByRef.TryGetValue(reference.Key, out prereleaseFile);

                            mod.NexusRemoteVersion = GetEffectiveRemoteVersion(mod, remote, prereleaseFile);
                            mod.NexusRemoteSummary = remote.Summary ?? string.Empty;
                            mod.NexusRemoteUpdatedAtUtc = GetEffectiveRemoteUpdatedAt(remote, prereleaseFile, mod.NexusRemoteVersion);
                            mod.NexusPageUrl = remote.GetPageUrl();

                            bool updateAvailable = NexusVersionComparer.IsRemoteNewer(mod.Version, mod.NexusRemoteVersion);
                            mod.HasUpdateAvailable = updateAvailable;
                            if (updateAvailable)
                            {
                                updates++;
                            }

                            ApplyUpdateStatus(mod, updateAvailable);
                            CacheNexusState(mod, reference);
                        }

                        InvalidateModListsAndDetails();
                        _lastNexusRemoteSyncUtc = syncTimestampUtc;
                        _cachedMappedMods = referencesByModId.Count;
                        _cachedUpdateCount = updates;
                        _cachedNexusError = error;
                        _hasNexusSyncCache = true;
                        NotifyNexusSync(referencesByModId.Count, updates, error);
                    });
                }
                catch
                {
                    // UI already gone, ignore.
                }
            });
        }

        private Dictionary<string, NexusRemoteModFile> GetPrereleaseFilesByReference(
            Dictionary<string, NexusModReference> referencesByModId,
            Dictionary<string, NexusRemoteMod> remoteByRef,
            bool includePrereleaseFiles,
            ref string error)
        {
            var result = new Dictionary<string, NexusRemoteModFile>(StringComparer.OrdinalIgnoreCase);
            if (!includePrereleaseFiles || referencesByModId == null || remoteByRef == null || _nexusService == null)
                return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in referencesByModId.Values)
            {
                if (reference == null || !reference.IsValid || seen.Contains(reference.Key))
                    continue;

                seen.Add(reference.Key);

                NexusRemoteMod remote;
                if (!remoteByRef.TryGetValue(reference.Key, out remote))
                    continue;

                TryAddPrereleaseFile(result, reference, remote, includePrereleaseFiles, ref error);
            }

            return result;
        }

        private void TryAddPrereleaseFile(
            Dictionary<string, NexusRemoteModFile> prereleaseFilesByRef,
            NexusModReference reference,
            NexusRemoteMod remote,
            bool includePrereleaseFiles,
            ref string error)
        {
            if (!includePrereleaseFiles || prereleaseFilesByRef == null || reference == null || !reference.IsValid || remote == null || _nexusService == null)
                return;

            if (prereleaseFilesByRef.ContainsKey(reference.Key))
                return;

            string fileError;
            var files = _nexusService.GetModFiles(reference.GameDomain, reference.ModId, out fileError);
            if (!string.IsNullOrEmpty(fileError))
            {
                if (string.IsNullOrEmpty(error))
                    error = fileError;
                return;
            }

            var prereleaseFile = _nexusService.SelectPreferredPrereleaseInstallFile(files);
            if (prereleaseFile != null)
                prereleaseFilesByRef[reference.Key] = prereleaseFile;
        }

        private static string GetEffectiveRemoteVersion(ModItem mod, NexusRemoteMod remote, NexusRemoteModFile prereleaseFile)
        {
            string remoteVersion = remote != null ? (remote.Version ?? string.Empty) : string.Empty;
            if (!ShouldPreferPrereleaseFile(mod, remoteVersion, prereleaseFile))
                return remoteVersion;

            return prereleaseFile.Version ?? string.Empty;
        }

        private static DateTime? GetEffectiveRemoteUpdatedAt(NexusRemoteMod remote, NexusRemoteModFile prereleaseFile, string effectiveVersion)
        {
            DateTime? remoteUpdatedAt = remote != null ? remote.UpdatedAtUtc : null;
            if (prereleaseFile == null || string.IsNullOrEmpty(prereleaseFile.Version))
                return remoteUpdatedAt;

            if (!string.Equals(NexusVersionComparer.Normalize(prereleaseFile.Version), NexusVersionComparer.Normalize(effectiveVersion), StringComparison.OrdinalIgnoreCase))
                return remoteUpdatedAt;

            return prereleaseFile.UploadedAtUtc ?? remoteUpdatedAt;
        }

        private static bool ShouldPreferPrereleaseFile(ModItem mod, string remoteVersion, NexusRemoteModFile prereleaseFile)
        {
            if (mod == null || prereleaseFile == null || string.IsNullOrEmpty(prereleaseFile.Version))
                return false;

            if (!NexusVersionComparer.IsRemoteNewer(mod.Version, prereleaseFile.Version))
                return false;

            if (!string.IsNullOrEmpty(remoteVersion) && NexusVersionComparer.CompareVersions(prereleaseFile.Version, remoteVersion) < 0)
                return false;

            return true;
        }

        private void NotifyNexusSync(int mappedMods, int updateCount, string errorMessage)
        {
            if (NexusSyncCompleted != null)
            {
                NexusSyncCompleted(new List<ModItem>(_allMods), mappedMods, updateCount, errorMessage);
            }
        }

        private void InvalidateModListsAndDetails()
        {
            _modList.RefreshRows();

            var selected = _modList.SelectedMod;
            if (selected != null)
                _detailsPanel.ShowMod(selected);
            UpdateProblemBanner();
        }

        private static string NormalizeNameKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var chars = (value ?? string.Empty).Trim().ToLowerInvariant().ToCharArray();
            var filtered = new System.Text.StringBuilder(chars.Length);
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (char.IsLetterOrDigit(c))
                    filtered.Append(c);
            }
            return filtered.ToString();
        }

        private static string FindMissingDependencyMessage(List<string> missingDependencies, string modId)
        {
            if (missingDependencies == null || string.IsNullOrEmpty(modId))
                return string.Empty;

            string prefix = "Mod '" + modId + "' has a missing hard dependency:";
            for (int i = 0; i < missingDependencies.Count; i++)
            {
                string message = missingDependencies[i];
                if (string.IsNullOrEmpty(message))
                    continue;

                if (message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return message;
            }

            return string.Empty;
        }

        private bool IsWithinNexusSyncCooldown()
        {
            if (!_hasNexusSyncCache || _lastNexusRemoteSyncUtc <= DateTime.MinValue)
                return false;

            return (DateTime.UtcNow - _lastNexusRemoteSyncUtc) < NexusSyncCooldown;
        }

        private static string BuildNexusCacheSignature(AppSettings settings)
        {
            if (settings == null)
                return string.Empty;

            return settings.EnableNexusIntegration.ToString() + "|" +
                (settings.NexusGameDomain ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                settings.IncludeNexusPrereleaseFiles.ToString() + "|" +
                (settings.ModsPath ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                settings.HasNexusOAuthSession.ToString();
        }

        private static int CountModsWithUpdates(List<ModItem> mods)
        {
            if (mods == null || mods.Count == 0)
                return 0;

            int count = 0;
            foreach (var mod in mods)
            {
                if (mod != null && mod.HasUpdateAvailable)
                    count++;
            }
            return count;
        }

        private void ApplyCachedNexusState(ModItem mod, NexusModReference reference)
        {
            if (mod == null || reference == null || !reference.IsValid)
                return;

            NexusSyncCacheEntry cached;
            if (!_nexusStateByModId.TryGetValue(mod.Id, out cached) || cached == null)
                return;

            if (!string.Equals(cached.ReferenceKey, reference.Key, StringComparison.OrdinalIgnoreCase))
            {
                _nexusStateByModId.Remove(mod.Id);
                return;
            }

            mod.NexusRemoteVersion = cached.RemoteVersion ?? string.Empty;
            mod.NexusRemoteSummary = cached.RemoteSummary ?? string.Empty;
            mod.NexusRemoteUpdatedAtUtc = cached.RemoteUpdatedAtUtc;
            mod.NexusPageUrl = !string.IsNullOrEmpty(cached.PageUrl)
                ? cached.PageUrl
                : "https://www.nexusmods.com/" + reference.GameDomain + "/mods/" + reference.ModId;
            mod.HasUpdateAvailable = NexusVersionComparer.IsRemoteNewer(
                mod.Version,
                cached.RemoteVersion);
        }

        private void CacheNexusState(ModItem mod, NexusModReference reference)
        {
            if (mod == null || reference == null || !reference.IsValid)
                return;

            var entry = new NexusSyncCacheEntry();
            entry.ReferenceKey = reference.Key;
            entry.RemoteVersion = mod.NexusRemoteVersion ?? string.Empty;
            entry.RemoteSummary = mod.NexusRemoteSummary ?? string.Empty;
            entry.RemoteUpdatedAtUtc = mod.NexusRemoteUpdatedAtUtc;
            entry.PageUrl = mod.NexusPageUrl ?? string.Empty;
            entry.HasUpdateAvailable = mod.HasUpdateAvailable;
            _nexusStateByModId[mod.Id] = entry;
        }

        private static void ApplyUpdateStatus(ModItem mod, bool hasUpdateAvailable)
        {
            if (mod == null)
                return;

            if (hasUpdateAvailable)
            {
                if (mod.Status == ModStatus.Ok)
                    mod.Status = ModStatus.UpdateAvailable;
                return;
            }

            if (mod.Status == ModStatus.UpdateAvailable)
                mod.Status = ModStatus.Ok;
        }

        private static void TryPersistNexusSidecar(ModItem mod, NexusModReference reference)
        {
            if (mod == null || reference == null || !reference.IsValid)
                return;

            if (string.IsNullOrEmpty(mod.RootPath) || !Directory.Exists(mod.RootPath))
                return;

            try
            {
                string aboutPath = Path.Combine(mod.RootPath, "About");
                if (!Directory.Exists(aboutPath))
                    Directory.CreateDirectory(aboutPath);

                string sidecarPath = Path.Combine(aboutPath, "Nexus.json");
                var sidecar = new Dictionary<string, object>();
                sidecar["gameDomain"] = reference.GameDomain;
                sidecar["modId"] = reference.ModId;

                string json = new JavaScriptSerializer().Serialize(sidecar);
                File.WriteAllText(sidecarPath, json);
            }
            catch
            {
                // Ignore persistence errors; runtime mapping still works.
            }
        }


        /// <summary>
        /// Apply theme
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            _isDarkMode = isDark;
            ThemePalette palette = ManagerTheme.Current;

            BackColor = palette.Window;
            _layout.BackColor = BackColor;
            ManagerTheme.Apply(_layout);
            _modList.ApplyTheme();
            _searchBox.ApplyTheme(isDark);
            _detailsPanel.ApplyTheme(isDark);
            _problemBanner.ApplyTheme();
            _actionBanner.ApplyTheme();
            ThemedMenu.ApplyTheme(_shareMenu);
            ThemedMenu.ApplyTheme(_profileMenu);
        }
    }
}
