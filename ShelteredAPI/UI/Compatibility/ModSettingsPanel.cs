using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization;
using UnityEngine;
using ModAPI.Core;
using ShelteredAPI.UI.Internal;
using ShelteredAPI.UI.Internal.Spine;
using ModAPI.Spine;
using ShelteredAPI.UI.Compatibility.Settings;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Panels;
using ShelteredAPI.UI.FieldManual.Widgets;
using ShelteredAPI.UI.Spine;


using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.Internal.Settings;
namespace ShelteredAPI.UI.Compatibility
{
    /// <summary>
    /// Shared runtime settings window used by ModAPI and ShelteredAPI providers.
    /// Supports presets, search, pagination, keybind pairing, and external input locking for nested dialogs.
    /// </summary>
    internal class ModSettingsPanel : MonoBehaviour
    {
        /// <summary>
        /// Raised after the settings panel finishes closing and destroys its runtime root.
        /// </summary>
        public static event Action Closed;

        private static GameObject _instance;
        private static ModSettingsPanel _activeInstance;
        private static Texture2D _whiteTexture;
        private static SettingMode? _lastClosedViewMode;
        private static int _externalInputLockCount;

        private ModEntry _currentMod;
        private SettingMode _currentViewMode = SettingMode.Simple;
        
        // UI References
        private GameObject _contentRoot;
        private GameObject _pageContentRoot;
        private GameObject _presetBarRoot;
        private GameObject _searchBarRoot;
        private UILabel _pagingLabel;
        private GameObject _simpleModeBtn;
        private GameObject _advancedModeBtn;
        private UIFont _activeBitmapFont;
        private Font _activeTtfFont;
        private FieldManualWindowChrome _chrome;
        private GameObject _pageFlipRoot;
        private BookPageNavigatorWidget _pageNavigator;
        private FieldManualBookPageTurn _pageTurn;
        private BookSearchBarWidget _searchBar;
        private readonly ModSettingsPresetController _presetController = new ModSettingsPresetController();
        private readonly ModSettingsKeybindStatusController _keybindStatusController = new ModSettingsKeybindStatusController();

        // State
        private List<List<GameObject>> _pages = new List<List<GameObject>>();
        private readonly List<string> _pageLabels = new List<string>();
        private int _currentPageIndex = 0;
        private int? _pageIndexBeforeSearch;
        private bool _isRebuilding = false;
        private bool _isClosing = false;
        private bool _inputLockedExternally = false;
        private const int MaxSearchLength = 64;

        // Colors
        private static readonly Color COLOR_HEADER = new Color(0.17f, 0.13f, 0.09f, 1f);
        private static readonly Color COLOR_TEXT = new Color(0.12f, 0.09f, 0.06f, 1f);
        private static readonly Color COLOR_SUBTEXT = new Color(0.36f, 0.30f, 0.23f, 1f);
        private static readonly Color COLOR_BTN_ACTIVE = new Color(0.88f, 0.76f, 0.63f, 1f);
        private static readonly Color COLOR_BTN_INACTIVE = new Color(0.70f, 0.60f, 0.50f, 1f);
        private static readonly Color COLOR_BTN_INACTIVE_TEXT = new Color(0.93f, 0.88f, 0.80f, 1f);
        private const int ROW_HEIGHT = 70;
        private const int BookSettingsItemsPerPage = 10;
        private const int BookSettingsRowHeight = 45;
        private const float BookSettingsStartY = 174f;
        private const float WideKeybindRowX = -420f;
        private const float BookSettingsRowX = -500f;
        private const float SettingPageLeftX = -530f;
        private const float SettingPageRightX = 80f;
        private const float ToolRowY = 222f;
        private const float FooterButtonY = -400f;
        private const float PresetBarX = 260f;
        private const int ModeButtonWidth = 96;
        private const float ModeSimpleX = 430f;
        private const float ModeAdvancedX = 536f;
        
        /// <summary>
        /// Opens the shared settings window for the supplied mod entry and rebuilds the full UI from its provider.
        /// </summary>
        /// <param name="mod">The mod whose settings provider should be displayed.</param>
        public static void Show(ModEntry mod)
        {
            MMLog.Write($"Show() requested for mod: {mod?.Id ?? "NULL"}");
            if (mod == null || mod.SettingsProvider == null) return;

            if (_instance != null) Destroy(_instance);
            
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(2, 2);
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) _whiteTexture.SetPixel(x, y, Color.white);
                _whiteTexture.filterMode = FilterMode.Point;
                _whiteTexture.wrapMode = TextureWrapMode.Clamp;
                _whiteTexture.Apply();
            }

            var root = FieldManualWindowChrome.CreateOverlayRoot("ModAPI_SettingsPanel", 50000, "ModSettingsPanel_Root");

            _instance = root;
            
            var fonts = UIFontCache.GetFonts();
            var uiFont = fonts.Bitmap;
            var ttfFont = fonts.TTF;

            var script = root.AddComponent<ModSettingsPanel>();
            script._currentMod = mod;
            script._currentViewMode = _lastClosedViewMode ?? SettingMode.Simple;
            _activeInstance = script;
            script._presetController.Initialize(mod);

            script.InitialiseAndBuild(root.transform, uiFont, ttfFont);
            script.ApplyExternalInputLock(_externalInputLockCount > 0);
        }

        /// <summary>
        /// Temporarily prevents the panel from consuming its own input while an overlay dialog or capture flow is active.
        /// Calls may be nested and must be balanced with <see cref="PopExternalInputLock"/>.
        /// </summary>
        public static void PushExternalInputLock()
        {
            _externalInputLockCount++;
            if (_activeInstance != null)
                _activeInstance.ApplyExternalInputLock(true);
        }

        /// <summary>
        /// Releases one layer of externally requested input lock previously applied through <see cref="PushExternalInputLock"/>.
        /// </summary>
        public static void PopExternalInputLock()
        {
            _externalInputLockCount = Mathf.Max(0, _externalInputLockCount - 1);
            if (_activeInstance != null)
                _activeInstance.ApplyExternalInputLock(_externalInputLockCount > 0);
        }

        private void InitialiseAndBuild(Transform root, UIFont uiFont, Font ttfFont)
        {
            MMLog.WriteDebug("InitialiseAndBuild() started");
            _activeBitmapFont = uiFont;
            _activeTtfFont = ttfFont;
            CaptureTemplates(uiFont, ttfFont);

            string title = _currentMod != null && !string.IsNullOrEmpty(_currentMod.Name)
                ? _currentMod.Name
                : "Mod Settings";
            string subtitle = _currentMod == null || string.IsNullOrEmpty(_currentMod.Version)
                ? "Mod Settings"
                : ("Mod Settings - v" + _currentMod.Version);

            _chrome = FieldManualWindowChrome.BuildBook(root.gameObject, 50000, title, subtitle);
            _pageTurn = FieldManualBookPageTurn.Attach(root.gameObject, _chrome);
            _pageFlipRoot = _chrome.Ui.CreateChild(root.gameObject, "BookPageFlipRoot", Vector3.zero);

            float toolsY = ToolRowY;

            _advancedModeBtn = CreateButton(_chrome.Regions.ContentRoot.transform, "BtnAdvanced", "Advanced", new Vector3(ModeAdvancedX, toolsY, 0), 15, COLOR_TEXT, uiFont, ttfFont, ModeButtonWidth, 34, () => SetViewMode(SettingMode.Advanced));
            _simpleModeBtn = CreateButton(_chrome.Regions.ContentRoot.transform, "BtnSimple", "Simple", new Vector3(ModeSimpleX, toolsY, 0), 15, COLOR_TEXT, uiFont, ttfFont, ModeButtonWidth, 34, () => SetViewMode(SettingMode.Simple));
            
            _presetBarRoot = new GameObject("PresetBar");
            _presetBarRoot.transform.SetParent(_chrome.Regions.ContentRoot.transform, false);
            _presetBarRoot.layer = root.gameObject.layer;
            _presetBarRoot.transform.localPosition = new Vector3(PresetBarX, toolsY, 0);

            CreateSearchBar(_chrome.Regions.ContentRoot, new Vector3(-300f, toolsY, 0));

            _contentRoot = _chrome.Regions.ContentRoot;
            _pageContentRoot = _chrome.Ui.CreateChild(_contentRoot, "SettingsPageContentRoot", Vector3.zero);

            float bottomY = FooterButtonY;

            _pageNavigator = new BookPageNavigatorWidget(_chrome.Palette, _chrome.Textures, _chrome.Ui, _pageTurn != null ? _pageTurn.Assets : null);
            _pageNavigator.Build(_chrome.Regions.FooterRoot, new Vector3(0f, bottomY, 0f),
                delegate { ChangePage(-1); },
                delegate { ChangePage(1); });

            _pagingLabel = _chrome.Ui.CreateLabel(_chrome.Regions.FooterRoot, "Paging", "Settings",
                new Vector3(0f, bottomY + 62f, 0f), 18, _chrome.Palette.Ink,
                360, 28, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _chrome.Ui.NextDepth());
            _pagingLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            _pagingLabel.effectStyle = UILabel.Effect.Outline;
            _pagingLabel.effectColor = new Color(0.86f, 0.78f, 0.56f, 0.55f);

            _keybindStatusController.Build(root, new Vector3(0, bottomY + 48, 0), uiFont, ttfFont, CreateLabel);
            ModSettingsKeybindStatusReporter.Attach(_keybindStatusController.Report);
            
            var defaultsButton = CreateButton(_chrome.Regions.FooterRoot.transform, "BtnReset", "Defaults", new Vector3(-460f, bottomY, 0), 18, Color.white, uiFont, ttfFont, 160, 58, () => OnResetClicked());
            SpineWidgetRuntime.SetTooltip(defaultsButton, "Restore every setting on this page to its default value.");

            var saveButton = CreateButton(_chrome.Regions.FooterRoot.transform, "BtnSaveAndClose", "Save & Close", new Vector3(420f, bottomY, 0), 18, Color.white, uiFont, ttfFont, 220, 58, () => OnClose());
            SpineWidgetRuntime.SetTooltip(saveButton, "Save changes and return to the previous settings screen.");

            MMLog.WriteDebug("UI Initial Construction Complete. Building Menu Content...");
            BuildMenu(uiFont, ttfFont);
            MMLog.WriteDebug($"UI Built for {_currentMod.Id}. Total settings: {_pages.Sum(p => p.Count)}");
        }

        private void Update()
        {
            _keybindStatusController.Update();

            if (!_inputLockedExternally && _searchBar != null)
                _searchBar.HandleInput(delegate
                {
                    HandleSearchFilterChanged();
                });

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (_inputLockedExternally)
                    return;

                // Esc is consumed by keybind capture while listening.
                if (KeybindCaptureListener.ShouldBlockEscapeClose())
                    return;
                OnClose();
            }

            HandlePageInput();
        }

        private void OnResetClicked()
        {
            if (_currentMod == null || _currentMod.SettingsProvider == null) return;
            _currentMod.SettingsProvider.ResetToDefaults();
            
            _presetController.CaptureCurrentSettingsAsCustom();
            _presetController.ClearOverride();

            // Auto-save after reset
            if (_currentMod.SettingsProvider is ISettingsProvider2 sp3) sp3.Save();

            _keybindStatusController.Report("All controls restored to defaults.", false);
            BuildMenu(_activeBitmapFont, _activeTtfFont);
        }

        private void OnClose()
        {
            if (_isClosing) return;
            _isClosing = true;

            _lastClosedViewMode = _currentViewMode;
            FlushPendingSettingInputs();
            SaveCurrentSettings();
            ModSettingsKeybindStatusReporter.Detach(_keybindStatusController.Report);
            if (_chrome != null) { _chrome.Dispose(); _chrome = null; }

            Destroy(_instance);
            _instance = null;
            if (_activeInstance == this) _activeInstance = null;
            RaiseClosed();
        }

        private void OnDestroy()
        {
            if (_isClosing) return;

            // If this panel is destroyed outside the normal close flow (scene change/reopen),
            // persist pending edits so settings are not lost.
            FlushPendingSettingInputs();
            SaveCurrentSettings();
            ModSettingsKeybindStatusReporter.Detach(_keybindStatusController.Report);
            if (_chrome != null) { _chrome.Dispose(); _chrome = null; }

            if (_instance == gameObject) _instance = null;
            if (_activeInstance == this) _activeInstance = null;
            RaiseClosed();
        }

        private static void RaiseClosed()
        {
            var handler = Closed;
            if (handler != null)
                handler();
        }

        private void ApplyExternalInputLock(bool locked)
        {
            _inputLockedExternally = locked;

            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null) continue;
                c.enabled = !locked;
            }
        }

        private void CreateSearchBar(GameObject parent, Vector3 localPosition)
        {
            _searchBar = new BookSearchBarWidget(_chrome.Palette, _chrome.Textures, _chrome.Ui, MaxSearchLength);
            _searchBarRoot = _searchBar.Build(parent, "SearchBar", localPosition, "Search settings...");
            MMLog.WriteInfo("[ModSettingsPanel] Search using manual input mode.");
        }

        private GameObject GetPageContentRoot()
        {
            return _pageContentRoot != null ? _pageContentRoot : _contentRoot;
        }
        
        private void CaptureTemplates(UIFont uiFont, Font ttfFont)
        {
            try {
                var allSliders = Resources.FindObjectsOfTypeAll<UISlider>();
                SpineWidgetFactory.SliderTemplate = allSliders.FirstOrDefault(s => s.gameObject.activeInHierarchy)?.gameObject ?? allSliders.FirstOrDefault()?.gameObject;
            } catch { }

            var tpl = new GameObject("ProceduralButtonTemplate");
            tpl.layer = gameObject.layer;
            tpl.transform.SetParent(transform, false);
            tpl.transform.localPosition = new Vector3(90000, 0, 0);
            var bg = tpl.AddComponent<UITexture>();
            bg.mainTexture = _whiteTexture; bg.width = 100; bg.height = 40; bg.depth = 200; bg.color = COLOR_BTN_ACTIVE;
            var lblGo = new GameObject("Label"); lblGo.layer = gameObject.layer; lblGo.transform.SetParent(tpl.transform, false);
            var lbl = lblGo.AddComponent<UILabel>(); lbl.text = "BUTTON"; lbl.fontSize = 16; lbl.color = COLOR_TEXT; lbl.alignment = NGUIText.Alignment.Center; lbl.depth = 201;
            if (uiFont != null) lbl.bitmapFont = uiFont; if (ttfFont != null) lbl.trueTypeFont = ttfFont;
            var col = tpl.AddComponent<BoxCollider>(); col.size = new Vector3(100, 40, 1);
            var btn = tpl.AddComponent<UIButton>(); btn.tweenTarget = tpl;
            SpineWidgetFactory.ButtonTemplate = tpl;
        }

        private void SetViewMode(SettingMode mode)
        {
            _currentViewMode = mode;
            BuildMenu(_activeBitmapFont, _activeTtfFont);
        }

        private void ChangePage(int delta)
        {
            if (_inputLockedExternally || KeybindCaptureListener.HasActiveCapture())
                return;

            if (_pageTurn != null)
            {
                _pageTurn.TryTurn(
                    delta,
                    GetPageContentRoot(),
                    _pageFlipRoot != null ? _pageFlipRoot : GetPageContentRoot(),
                    _pagingLabel != null ? _pagingLabel.gameObject : (_pageNavigator != null ? _pageNavigator.PageLabelRoot : null),
                    CanChangePage,
                    CommitPageChange,
                    UpdatePageVisibility);
                return;
            }

            if (!CanChangePage(delta))
                return;

            CommitPageChange(delta);
            UpdatePageVisibility();
        }

        private void HandlePageInput()
        {
            if (_pageTurn != null)
                _pageTurn.HandlePageInput(_pages.Count, IsPageInputBlocked, ChangePage);
        }

        private bool IsPageInputBlocked()
        {
            return _inputLockedExternally || KeybindCaptureListener.HasActiveCapture();
        }

        private bool CanChangePage(int delta)
        {
            if (delta < 0)
                return _currentPageIndex > 0;
            if (delta > 0)
                return _currentPageIndex < _pages.Count - 1;

            return false;
        }

        private void CommitPageChange(int delta)
        {
            _currentPageIndex = Mathf.Clamp(_currentPageIndex + delta, 0, Mathf.Max(0, _pages.Count - 1));
        }

        private void BuildMenu(UIFont uiFont, Font ttfFont, bool keepPage = false)
        {
            if (_isRebuilding) return;
            _isRebuilding = true;

            try
            {
                foreach (var page in _pages) foreach (var go in page) Destroy(go);
                _pages.Clear();
                _pageLabels.Clear();
            
            foreach(Transform child in _presetBarRoot.transform) Destroy(child.gameObject);

            var provider = _currentMod.SettingsProvider;
            var settings = provider.GetSettingsObject();
            List<SettingDefinition> allDefs;
            try 
            {
                allDefs = provider.GetSettings().ToList(); 
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"Failed to retrieve settings for {_currentMod.Id}: {ex}");
                allDefs = new List<SettingDefinition>(); // Fallback
            }

            // 1. Preset Management
            _presetController.RefreshAvailablePresets(allDefs);
            _presetController.UpdateCurrentPresetState(settings, allDefs);
            BuildPresetCycleWidget(uiFont, ttfFont, settings, allDefs);

            // 1a. View Mode Toggle Visibility
            // Manual repro validation:
            // - One Simple + one Advanced => buttons visible; Simple hides Advanced-only; Advanced shows it.
            // - All entries visible in both views => buttons hidden.
            bool hasSimpleVisible = allDefs.Any(d => d.ShowInSimpleView);
            bool hasAdvancedVisible = allDefs.Any(d => d.ShowInAdvancedView);
            bool showToggles = hasSimpleVisible && hasAdvancedVisible &&
                               allDefs.Any(d => d.ShowInSimpleView != d.ShowInAdvancedView);

            _simpleModeBtn.SetActive(showToggles);
            _advancedModeBtn.SetActive(showToggles);

            if (showToggles)
            {
                UpdateButtonState(_simpleModeBtn, true, _currentViewMode == SettingMode.Simple);
                UpdateButtonState(_advancedModeBtn, true, _currentViewMode == SettingMode.Advanced);
            }

            // 2. Setting Filtering
            var hierarchy = new SettingsHierarchy(allDefs);
            SettingsViewMode viewMode = (_currentViewMode == SettingMode.Simple) ? SettingsViewMode.Simple : SettingsViewMode.Advanced;
            
            // Pass the search filter directly to get flattening if you want hierarchy-aware search,
            // OR apply it post-flattening like you are doing.
            // Current Issue: Search happens AFTER simple/advanced filtering.
            
            var visible = hierarchy.GetFlattenedForView(viewMode, settings).ToList();
            
            // Inject Category Headers if not searching
            if (string.IsNullOrEmpty(SearchFilter))
            {
                var withCategories = new List<SettingDefinition>();
                string lastCategory = null;
                foreach (var def in visible)
                {
                    if (!string.IsNullOrEmpty(def.Category) && def.Category != lastCategory)
                    {
                        withCategories.Add(new SettingDefinition { 
                            Id = "CatHeader_" + def.Category, 
                            Label = def.Category.ToUpper(), 
                            Type = SettingType.Header,
                            HeaderColor = new Color(0.7f, 0.9f, 1f)
                        });
                        lastCategory = def.Category;
                    }
                    else if (string.IsNullOrEmpty(def.Category))
                    {
                        lastCategory = null;
                    }
                    withCategories.Add(def);
                }
                visible = withCategories;
            }
            else
            {
                // Apply Search Filter
                visible = visible.Where(d => MatchesSearch(d, SearchFilter)).ToList();
            }

            if (provider is ICustomSettingsUI custom)
            {
                var customRoot = new GameObject("CustomUI");
                customRoot.transform.SetParent(GetPageContentRoot().transform, false);
                customRoot.transform.localPosition = Vector3.zero;
                custom.DrawSettings(customRoot, (float)_chrome.Regions.ContentRectLocal.width - 60f, (float)_chrome.Regions.ContentRectLocal.height - 60f);
                _pages.Add(new List<GameObject> { customRoot });
                _pageLabels.Add(BuildPageLabel(_currentMod != null ? _currentMod.Name : "Custom Settings", 1, 1));
            }
            else
            {
                bool useWideKeybindLayout = ShouldUseWideKeybindLayout(visible, allDefs);
                if (useWideKeybindLayout)
                {
                    // Keybind rows are wide; use a single-column layout and fewer rows per page
                    // so page controls at the bottom remain unobstructed.
                    CreatePaginatedGrid(visible, allDefs, settings, 8, 1, 50, 145f, true);
                }
                else
                {
                    CreatePaginatedGrid(visible, allDefs, settings, BookSettingsItemsPerPage, 1, BookSettingsRowHeight, BookSettingsStartY, false);
                }
            }

            if (!keepPage) _currentPageIndex = 0;
            else _currentPageIndex = Mathf.Clamp(_currentPageIndex, 0, Mathf.Max(0, _pages.Count - 1));

            UpdatePageVisibility();
            }
            finally
            {
                _isRebuilding = false;
            }
        }

        private static bool ShouldUseWideKeybindLayout(List<SettingDefinition> visibleItems, List<SettingDefinition> allDefs)
        {
            return ModSettingsKeybindLayout.ShouldUseWideKeybindLayout(visibleItems, allDefs);
        }

        private void BuildPresetCycleWidget(UIFont uiFont, Font ttfFont, object settings, List<SettingDefinition> allDefs)
        {
            // Clear existing preset bar
            foreach(Transform child in _presetBarRoot.transform) Destroy(child.gameObject);

            // Hide the preset strip entirely for mods that do not define presets
            // (custom-only settings without Easy/Normal/Hard, etc.).
            if (!_presetController.HasPresets)
            {
                _presetBarRoot.SetActive(false);
                return;
            }

            _presetBarRoot.SetActive(true);
            
            float boxW = 170;
            float boxH = 40;
            float arrowSize = 40;

            // 1. Left Arrow
            CreateButton(_presetBarRoot.transform, "PresetPrev", "<", 
                new Vector3(-(boxW/2 + arrowSize/2 + 10), 0, 0), 20, Color.white, uiFont, ttfFont, (int)arrowSize, (int)boxH, () => {
                    CyclePreset(-1, allDefs, settings, uiFont, ttfFont);
                });

            // 2. Preset Name Box
            UITexture presetBackground = _chrome.Ui.CreateQuad(_presetBarRoot, "PresetDisplay", _whiteTexture, Vector3.zero, (int)boxW, (int)boxH, COLOR_BTN_INACTIVE, _chrome.Ui.NextDepth());
            var lbl = _chrome.Ui.CreateLabel(_presetBarRoot, "PresetLabel", _presetController.CurrentPresetName.ToUpper(), Vector3.zero, 18, COLOR_TEXT, (int)boxW - 20, (int)boxH - 6, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _chrome.Ui.NextDepth());
            lbl.alignment = NGUIText.Alignment.Center;
            lbl.overflowMethod = UILabel.Overflow.ShrinkContent;
            
            if (_presetController.CurrentPresetName == "Custom")
                presetBackground.color = COLOR_SUBTEXT;
            else
                presetBackground.color = COLOR_BTN_INACTIVE;

            // 3. Right Arrow
            CreateButton(_presetBarRoot.transform, "PresetNext", ">", 
                new Vector3(boxW/2 + arrowSize/2 + 10, 0, 0), 20, Color.white, uiFont, ttfFont, (int)arrowSize, (int)boxH, () => {
                    CyclePreset(1, allDefs, settings, uiFont, ttfFont);
                });
        }

        private void FlushPendingSettingInputs()
        {
            try
            {
                if (_contentRoot == null) return;

                var inputs = _contentRoot.GetComponentsInChildren<UIInput>(true);
                for (int i = 0; i < inputs.Length; i++)
                {
                    var input = inputs[i];
                    if (input == null || !input.isSelected) continue;
                    EventDelegate.Execute(input.onSubmit);
                    input.RemoveFocus();
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ModSettingsPanel] Failed to flush pending input values: " + ex.Message);
            }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                if (_currentMod != null && _currentMod.SettingsProvider is ISettingsProvider2 sp2)
                {
                    sp2.Save();
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModSettingsPanel] Save failed while closing panel: " + ex.Message);
            }
        }

        private void CyclePreset(int delta, List<SettingDefinition> allDefs, object settings, UIFont uiFont, Font ttfFont)
        {
            if (_presetController.CyclePreset(delta, allDefs, settings))
                BuildMenu(uiFont, ttfFont, true);
        }

        private void UpdateCurrentPresetState(object settings, List<SettingDefinition> allDefs)
        {
            _presetController.UpdateCurrentPresetState(settings, allDefs);
        }

        private void CreatePaginatedGrid(
            List<SettingDefinition> visibleItems,
            List<SettingDefinition> allDefs,
            object data,
            int itemsPerPage,
            int columns,
            int rowHeight,
            float startY,
            bool useWideKeybindLayout)
        {
            if (visibleItems == null) visibleItems = new List<SettingDefinition>();
            if (allDefs == null) allDefs = new List<SettingDefinition>();
            if (itemsPerPage <= 0) itemsPerPage = 18;
            if (columns <= 0) columns = 1;
            if (rowHeight <= 0) rowHeight = ROW_HEIGHT;

            var hierarchy = new SettingsHierarchy(allDefs);
            var displayEntries = BuildDisplayEntries(visibleItems, allDefs, useWideKeybindLayout);

            var pagedEntries = BuildSectionedDisplayEntryPages(displayEntries, itemsPerPage);
            ModSettingsKeybindWidgetBuilder keybindBuilder = useWideKeybindLayout ? CreateKeybindWidgetBuilder(data) : null;
            ModSettingsBookWidgetRenderer bookRenderer = useWideKeybindLayout
                ? null
                : new ModSettingsBookWidgetRenderer(_chrome, _whiteTexture, _activeBitmapFont, _activeTtfFont, this);

            for (int i = 0; i < pagedEntries.Count; i++)
            {
                var pageItems = new List<GameObject>();
                SettingsPageSegment page = pagedEntries[i];
                var segment = page.Entries;
                int renderedRows = 0;

                if (useWideKeybindLayout && columns == 1 && segment.Count > 0)
                {
                    GameObject header = keybindBuilder.CreateColumnHeaderWidget();
                    header.transform.localPosition = new Vector3(WideKeybindRowX, startY + 34f, 0);
                    pageItems.Add(header);
                    foreach (var w in header.GetComponentsInChildren<UIWidget>(true)) w.depth += 100;
                }

                for (int j = 0; j < segment.Count; j++)
                {
                    var entry = segment[j];
                    if (entry == null || entry.Primary == null) continue;

                    int col = renderedRows % columns;
                    int row = renderedRows / columns;

                    float x;
                    if (useWideKeybindLayout && columns == 1)
                    {
                        x = WideKeybindRowX;
                    }
                    else if (columns == 1)
                    {
                        x = BookSettingsRowX;
                    }
                    else if (columns == 2)
                    {
                        x = (col == 0) ? SettingPageLeftX : SettingPageRightX;
                    }
                    else
                    {
                        x = -420f + (col * 300f);
                    }

                    float y = startY - (row * rowHeight);

                    GameObject widget;
                    bool isSectionHeader = IsSectionHeaderEntry(entry);

                    if (useWideKeybindLayout && isSectionHeader)
                    {
                        widget = keybindBuilder.CreateSectionHeaderWidget(entry.Primary);
                    }
                    else if (entry.Secondary != null)
                    {
                        widget = keybindBuilder.CreateDualKeybindWidget(entry.Primary, entry.Secondary);
                    }
                    else if (!useWideKeybindLayout)
                    {
                        widget = bookRenderer.CreateWidget(GetPageContentRoot(), entry.Primary, data, isSectionHeader);
                    }
                    else
                    {
                        widget = SpineWidgetFactory.CreateWidget(entry.Primary, GetPageContentRoot().transform, data, this);
                    }

                    if (widget != null)
                    {
                        widget.transform.localPosition = new Vector3(x, y, 0);
                        if (useWideKeybindLayout)
                            ModSettingsKeybindWidgetBuilder.NormalizeWideKeybindWidgetAlignment(widget, entry);
                        ModSettingsBookWidgetRenderer.ApplyStyle(widget, isSectionHeader);
                        pageItems.Add(widget);
                        foreach (var w in widget.GetComponentsInChildren<UIWidget>(true)) w.depth += 100;

                        // Use hierarchy to check if any ancestor disables this widget.
                        UpdateWidgetEnabled(widget, !hierarchy.IsDisabledByAncestor(entry.Primary, data));
                        renderedRows += isSectionHeader && columns > 1
                            ? columns - col
                            : 1;
                    }
                }
                _pages.Add(pageItems);
                _pageLabels.Add(page.Title);
            }

            if (_pages.Count == 0 || (_pages.Count == 1 && _pages[0].Count == 0 && !string.IsNullOrEmpty(SearchFilter)))
            {
                // Handle no search results.
                if (_pages.Count == 0)
                {
                    _pages.Add(new List<GameObject>());
                    _pageLabels.Add(BuildPageLabel("No Results", 1, 1));
                }
            }
        }

        private ModSettingsKeybindWidgetBuilder CreateKeybindWidgetBuilder(object settingsObject)
        {
            return new ModSettingsKeybindWidgetBuilder(
                GetPageContentRoot(),
                _currentMod != null ? _currentMod.SettingsProvider : null,
                settingsObject,
                _whiteTexture,
                _activeBitmapFont,
                _activeTtfFont,
                COLOR_TEXT,
                COLOR_SUBTEXT,
                CreateLabel,
                CreateButton,
                ApplySettingValue,
                OnSettingChanged);
        }

        private List<ModSettingsKeybindDisplayEntry> BuildDisplayEntries(List<SettingDefinition> visibleItems, List<SettingDefinition> allDefs, bool pairKeybinds)
        {
            return ModSettingsKeybindLayout.BuildDisplayEntries(visibleItems, allDefs, pairKeybinds);
        }

        private sealed class SettingsPageSegment
        {
            public readonly string Title;
            public readonly List<ModSettingsKeybindDisplayEntry> Entries;

            public SettingsPageSegment(string title, List<ModSettingsKeybindDisplayEntry> entries)
            {
                Title = title;
                Entries = entries ?? new List<ModSettingsKeybindDisplayEntry>();
            }
        }

        private sealed class SettingsSection
        {
            public readonly string Title;
            public readonly ModSettingsKeybindDisplayEntry Header;
            public readonly bool HasExplicitHeader;
            public readonly List<ModSettingsKeybindDisplayEntry> Body = new List<ModSettingsKeybindDisplayEntry>();

            public SettingsSection(string title, ModSettingsKeybindDisplayEntry header, bool hasExplicitHeader)
            {
                Title = NormalizePageTitle(title);
                Header = header ?? CreateSyntheticHeader(Title);
                HasExplicitHeader = hasExplicitHeader;
            }
        }

        private List<SettingsPageSegment> BuildSectionedDisplayEntryPages(List<ModSettingsKeybindDisplayEntry> displayEntries, int itemsPerPage)
        {
            var pages = new List<SettingsPageSegment>();
            if (displayEntries == null || displayEntries.Count == 0)
                return pages;

            if (itemsPerPage <= 0)
                itemsPerPage = 1;

            List<SettingsSection> sections = BuildSettingsSections(displayEntries);
            int bodyItemsPerPage = Math.Max(1, itemsPerPage - 1);

            for (int i = 0; i < sections.Count; i++)
            {
                SettingsSection section = sections[i];
                int pageCount = Math.Max(1, (int)Math.Ceiling(section.Body.Count / (double)bodyItemsPerPage));

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var entries = new List<ModSettingsKeybindDisplayEntry>();
                    entries.Add(section.Header);

                    int start = pageIndex * bodyItemsPerPage;
                    int take = Math.Min(bodyItemsPerPage, section.Body.Count - start);
                    for (int j = 0; j < take; j++)
                    {
                        entries.Add(section.Body[start + j]);
                    }

                    pages.Add(new SettingsPageSegment(BuildPageLabel(section.Title, pageIndex + 1, pageCount), entries));
                }
            }

            return pages;
        }

        private static List<SettingsSection> BuildSettingsSections(List<ModSettingsKeybindDisplayEntry> displayEntries)
        {
            var sections = new List<SettingsSection>();
            SettingsSection current = null;

            for (int i = 0; i < displayEntries.Count; i++)
            {
                ModSettingsKeybindDisplayEntry entry = displayEntries[i];
                if (entry == null || entry.Primary == null)
                    continue;

                if (IsSectionHeaderEntry(entry))
                {
                    current = new SettingsSection(GetEntryTitle(entry), entry, true);
                    sections.Add(current);
                    continue;
                }

                string sectionTitle = GetEntryTitle(entry);
                if (current == null || (!current.HasExplicitHeader && !string.Equals(current.Title, NormalizePageTitle(sectionTitle), StringComparison.OrdinalIgnoreCase)))
                {
                    current = new SettingsSection(sectionTitle, null, false);
                    sections.Add(current);
                }

                current.Body.Add(entry);
            }

            return sections;
        }

        private void HandleSearchFilterChanged()
        {
            bool isSearching = !string.IsNullOrEmpty(SearchFilter);
            if (isSearching)
            {
                if (!_pageIndexBeforeSearch.HasValue)
                    _pageIndexBeforeSearch = _currentPageIndex;

                BuildMenu(_activeBitmapFont, _activeTtfFont, true);
                return;
            }

            if (_pageIndexBeforeSearch.HasValue)
            {
                _currentPageIndex = _pageIndexBeforeSearch.Value;
                _pageIndexBeforeSearch = null;
            }

            BuildMenu(_activeBitmapFont, _activeTtfFont, true);
        }

        private string SearchFilter
        {
            get { return _searchBar != null ? (_searchBar.Filter ?? string.Empty) : string.Empty; }
        }

        private static bool MatchesSearch(SettingDefinition def, string filter)
        {
            if (def == null)
                return false;
            if (string.IsNullOrEmpty(filter))
                return true;

            return ContainsSearch(def.Label, filter)
                || ContainsSearch(def.Id, filter)
                || ContainsSearch(def.Tooltip, filter)
                || ContainsSearch(def.Category, filter)
                || ContainsSearch(def.FieldName, filter);
        }

        private static bool ContainsSearch(string value, string filter)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ModSettingsKeybindDisplayEntry CreateSyntheticHeader(string title)
        {
            return new ModSettingsKeybindDisplayEntry(
                new SettingDefinition
                {
                    Id = "GeneratedHeader_" + NormalizePageTitle(title).Replace(" ", "_"),
                    Label = NormalizePageTitle(title).ToUpperInvariant(),
                    Type = SettingType.Header,
                    HeaderColor = new Color(0.17f, 0.13f, 0.09f, 1f)
                },
                null);
        }

        private static string GetEntryTitle(ModSettingsKeybindDisplayEntry entry)
        {
            if (entry == null || entry.Primary == null)
                return "Settings";

            if (!string.IsNullOrEmpty(entry.Primary.Label) && IsSectionHeaderEntry(entry))
                return entry.Primary.Label;

            if (!string.IsNullOrEmpty(entry.Primary.Category))
                return entry.Primary.Category;

            return "Settings";
        }

        private static string NormalizePageTitle(string title)
        {
            return string.IsNullOrEmpty(title) ? "Settings" : title.Trim();
        }

        private static string BuildPageLabel(string title, int pageNumber, int pageCount)
        {
            string normalizedTitle = NormalizePageTitle(title);
            return pageCount > 1
                ? normalizedTitle + " (" + pageNumber + "/" + pageCount + ")"
                : normalizedTitle;
        }

        private static bool IsSectionHeaderEntry(ModSettingsKeybindDisplayEntry entry)
        {
            return ModSettingsKeybindLayout.IsSectionHeaderEntry(entry);
        }

        private static bool ApplySettingValue(SettingDefinition def, object settingsObject, object newValue)
        {
            return ModSettingsKeybindRuntime.ApplySettingValue(def, settingsObject, newValue);
        }

        public void RefreshDependents(string changedId)
        {
             var provider = _currentMod.SettingsProvider;
             var settings = provider.GetSettingsObject();
             var allDefs = provider.GetSettings().ToList();
             // _currentMod.SettingsProvider.LoadSettings(); // Don't reload, we just changed it in memory!
             UpdateCurrentPresetState(settings, allDefs);
             
              // Refresh UI states
              var fonts = UIFontCache.GetFonts();
              BuildMenu(fonts.Bitmap, fonts.TTF, true);
        }

        public void OnSettingChanged()
        {
            // Any manual edit IMMEDIATELY makes this a Custom state
            _presetController.MarkCurrentStateAsCustom();

            if (_currentMod == null) return;

            var settings = _currentMod.SettingsProvider.GetSettingsObject();
            var provider = _currentMod.SettingsProvider;
            var allDefs = provider.GetSettings().ToList();
            
            UpdateCurrentPresetState(settings, allDefs);
            
            // Re-draw just the preset widget to reflect the name change (e.g. to CUSTOM)
            var fonts = UIFontCache.GetFonts();
            BuildPresetCycleWidget(fonts.Bitmap, fonts.TTF, settings, allDefs);
        }

        private void UpdateWidgetEnabled(GameObject widget, bool enabled)
        {
            foreach (var w in widget.GetComponentsInChildren<UIWidget>(true)) w.alpha = enabled ? 1f : 0.4f;
            foreach (var c in widget.GetComponentsInChildren<Collider>(true)) c.enabled = enabled;
        }

        private void UpdatePageVisibility()
        {
            for (int i = 0; i < _pages.Count; i++) {
                bool active = (i == _currentPageIndex);
                foreach (var go in _pages[i]) go.SetActive(active);
            }

            bool hasPages = _pages.Count > 0;
            bool showPaging = _pages.Count > 1;
            
            if (_pagingLabel != null) {
                _pagingLabel.gameObject.SetActive(hasPages);
                _pagingLabel.text = GetCurrentPageLabel();
            }
            if (_pageNavigator != null)
                _pageNavigator.UpdateState(_currentPageIndex, showPaging ? _pages.Count : 1);
        }

        private string GetCurrentPageLabel()
        {
            if (_pageLabels != null
                && _currentPageIndex >= 0
                && _currentPageIndex < _pageLabels.Count
                && !string.IsNullOrEmpty(_pageLabels[_currentPageIndex]))
            {
                return _pageLabels[_currentPageIndex];
            }

            return (_currentPageIndex + 1) + "/" + Math.Max(1, _pages.Count);
        }



        // --- Helpers ---
        private void UpdateButtonState(GameObject btnGO, bool allowed, bool active)
        {
            if(!btnGO) return;
            var btn = btnGO.GetComponent<UIButton>();
            var lbl = btnGO.GetComponentInChildren<UILabel>();
            if (btn != null)
                btn.isEnabled = allowed;

            BoxCollider collider = btnGO.GetComponent<BoxCollider>();
            if (collider != null)
                collider.enabled = allowed;

            UIWidget backgroundWidget = btnGO.GetComponent<UITexture>();
            if (backgroundWidget == null)
            {
                UIWidget[] widgets = btnGO.GetComponentsInChildren<UIWidget>(true);
                for (int i = 0; i < widgets.Length; i++)
                {
                    if (widgets[i] != null && !(widgets[i] is UILabel))
                    {
                        backgroundWidget = widgets[i];
                        break;
                    }
                }
            }
            if (backgroundWidget)
                backgroundWidget.color = ResolveModeButtonBackgroundColor(allowed, active);
            if (lbl)
                lbl.color = ResolveModeButtonTextColor(allowed, active);
        }

        private static Color ResolveModeButtonBackgroundColor(bool allowed, bool active)
        {
            if (!allowed)
                return Color.Lerp(COLOR_BTN_INACTIVE, Color.black, 0.25f);

            return active ? COLOR_BTN_ACTIVE : COLOR_BTN_INACTIVE;
        }

        private static Color ResolveModeButtonTextColor(bool allowed, bool active)
        {
            if (!allowed)
                return Color.gray;

            return active ? COLOR_TEXT : COLOR_BTN_INACTIVE_TEXT;
        }

        private UILabel CreateLabel(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int depth)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer; go.transform.localPosition = pos;
            var label = go.AddComponent<UILabel>(); label.text = text; label.fontSize = fontSize; label.color = color; label.depth = depth;
            label.overflowMethod = UILabel.Overflow.ResizeFreely; label.bitmapFont = uiFont; label.trueTypeFont = ttfFont;
            return label;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int w, int h, Action onClick)
        {
            if (_chrome == null || _chrome.Buttons == null || parent == null)
                return null;

            return _chrome.Buttons.Build(parent.gameObject, name, text, pos, w, h, fontSize, onClick);
        }

        internal static class ReflectionHelper
        {
            public static object GetValue(SettingDefinition def, object obj)
            {
                if (obj == null || string.IsNullOrEmpty(def.FieldName)) return null;
                var type = obj.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                
                var field = type.GetField(def.FieldName, flags);
                if (field != null) return field.GetValue(obj);
                
                var prop = type.GetProperty(def.FieldName, flags);
                if (prop != null) return prop.GetValue(obj, null);
                
                return null;
            }

            public static void SetValue(SettingDefinition def, object obj, object val)
            {
                if (obj == null || string.IsNullOrEmpty(def.FieldName)) return;
                var type = obj.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                // Handle Fields
                var field = type.GetField(def.FieldName, flags);
                if (field != null)
                {
                    try
                    {
                        var converted = ConvertValue(val, field.FieldType);
                        field.SetValue(obj, converted);
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError($"[Spine] Reflection error setting field {def.FieldName}: {ex.Message}");
                    }
                    return;
                }

                // Handle Properties
                var prop = type.GetProperty(def.FieldName, flags);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        var converted = ConvertValue(val, prop.PropertyType);
                        prop.SetValue(obj, converted, null);
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError($"[Spine] Reflection error setting property {def.FieldName}: {ex.Message}");
                    }
                }
            }

            private static object ConvertValue(object val, Type targetType)
            {
                if (val == null) return null;
                if (targetType.IsAssignableFrom(val.GetType())) return val;
                
                try
                {
                    if (targetType == typeof(float)) return Convert.ToSingle(val, CultureInfo.InvariantCulture);
                    if (targetType == typeof(int)) return Convert.ToInt32(val, CultureInfo.InvariantCulture);
                    if (targetType == typeof(bool)) return Convert.ToBoolean(val);
                    if (targetType == typeof(string)) return Convert.ToString(val, CultureInfo.InvariantCulture);
                    return Convert.ChangeType(val, targetType, CultureInfo.InvariantCulture);
                }
                catch { return val; }
            }

            public static bool ReadParentBool(string condition, object settings)
            {
                if (string.IsNullOrEmpty(condition) || settings == null) return true;
                try
                {
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    var field = settings.GetType().GetField(condition, flags);
                    if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(settings);
                    
                    var prop = settings.GetType().GetProperty(condition, flags);
                    if (prop != null && prop.PropertyType == typeof(bool)) return (bool)prop.GetValue(settings, null);
                }
                catch { }
                return true;
            }
        }
    }
}

