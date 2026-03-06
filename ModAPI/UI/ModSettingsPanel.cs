using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Globalization; // Added for CultureInfo
using System.Text;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Spine;
using ModAPI.Spine.UI;

namespace ModAPI.UI
{
    public class ModSettingsPanel : MonoBehaviour
    {
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
        private GameObject _presetBarRoot;
        private GameObject _searchBarRoot;
        private UILabel _modNameLabel;
        private UILabel _modVersionLabel;
        private UILabel _pagingLabel;
        private GameObject _simpleModeBtn;
        private GameObject _advancedModeBtn;
        private GameObject _prevBtn;
        private GameObject _nextBtn;
        private UILabel _customIndicatorLabel;
        private UIInput _searchInput;
        private UILabel _searchDisplayLabel;
        private GameObject _searchInputRoot;
        private bool _manualSearchEnabled;
        private bool _searchHasFocus;
        private UIFont _activeBitmapFont;
        private Font _activeTtfFont;

        // State
        private List<List<GameObject>> _pages = new List<List<GameObject>>();
        private int _currentPageIndex = 0;
        private GameObject _sliderTemplate;
        private GameObject _buttonTemplate;
        private List<string> _availablePresets = new List<string>();
        private string _currentPresetName = "Custom";
        private string _searchFilter = "";
        private string _presetOverride = null;
        private string _customSnapshotJson = null;
        private bool _isRebuilding = false;
        private bool _isClosing = false;
        private bool _inputLockedExternally = false;
        private const int MaxSearchLength = 64;

        // Colors
        private static readonly Color COLOR_HEADER = new Color(0.9f, 0.85f, 0.7f);
        private static readonly Color COLOR_TEXT = Color.white;
        private static readonly Color COLOR_SUBTEXT = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color COLOR_BTN_ACTIVE = new Color(0.44f, 0.32f, 0.24f, 1f);
        private static readonly Color COLOR_BTN_INACTIVE = new Color(0.25f, 0.2f, 0.15f, 1f);
        private static readonly Color COLOR_PRESET_MATCH = new Color(0.2f, 0.62f, 0.2f, 1f);
        private static readonly Color COLOR_CUSTOM = new Color(0.7f, 0.5f, 0.1f, 1f);

        // Layout
        private const int WINDOW_WIDTH = 1200;
        private const int WINDOW_HEIGHT = 900;
        private const int ROW_HEIGHT = 70;
        private const float WideKeybindRowX = -420f;
        private const float SectionHeaderLocalX = 76f;
        
        public static void Show(ModEntry mod)
        {
            MMLog.Write($"Show() requested for mod: {mod?.Id ?? "NULL"}");
            if (mod == null || mod.SettingsProvider == null) return;

            if (_instance != null) Destroy(_instance);
            UIFontCache.RefreshIfMissing();

            var panel = UIUtil.EnsureOverlayPanel("ModAPI_SettingsPanel", 50000);
            
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(2, 2);
                for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) _whiteTexture.SetPixel(x, y, Color.white);
                _whiteTexture.filterMode = FilterMode.Point;
                _whiteTexture.wrapMode = TextureWrapMode.Clamp;
                _whiteTexture.Apply();
            }

            var root = new GameObject("ModSettingsPanel_Root");
            root.transform.SetParent(panel.transform, false);
            root.layer = panel.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;

            _instance = root;
            
            var fonts = UIFontCache.GetFonts();
            var uiFont = fonts.Bitmap;
            var ttfFont = fonts.TTF;

            var script = root.AddComponent<ModSettingsPanel>();
            script._currentMod = mod;
            script._currentViewMode = _lastClosedViewMode ?? SettingMode.Simple;
            _activeInstance = script;

            // Initial snapshot of settings as "Custom" state
            if (mod.SettingsProvider is ISettingsProvider2 sp2) script._customSnapshotJson = sp2.SerializeToJson();

            script.InitialiseAndBuild(root.transform, uiFont, ttfFont);
            script.ApplyExternalInputLock(_externalInputLockCount > 0);
        }

        public static void PushExternalInputLock()
        {
            _externalInputLockCount++;
            if (_activeInstance != null)
                _activeInstance.ApplyExternalInputLock(true);
        }

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

            // Backgrounds - Lowered opacity for transparency
            CreateTexturedBox(root, "DarkOverlay", Vector3.zero, 3000, 3000, new Color(0f, 0f, 0f, 0.4f), 0, true);
            // Window BG reduced alpha to 0.85f from 0.98f
            CreateTexturedBox(root, "WindowBackground", Vector3.zero, WINDOW_WIDTH, WINDOW_HEIGHT, new Color(0.15f, 0.12f, 0.1f, 0.95f), 10, false);
            CreateTexturedBox(root, "WindowBorder", Vector3.zero, WINDOW_WIDTH + 4, WINDOW_HEIGHT + 4, new Color(0.5f, 0.4f, 0.3f, 1f), 9, false);

            float topY = WINDOW_HEIGHT / 2 - 40;
            float leftX = -WINDOW_WIDTH / 2 + 70;
            float rightX = WINDOW_WIDTH / 2 - 40;

            // 1. Title (Top Left)
            // Name Label
            _modNameLabel = CreateLabel(root, "Title", "MOD NAME", new Vector3(leftX + 40, topY + 10, 0), 28, COLOR_HEADER, uiFont, ttfFont, 600);
            _modNameLabel.alignment = NGUIText.Alignment.Left;
            _modNameLabel.pivot = UIWidget.Pivot.Left;
            _modNameLabel.transform.localPosition = new Vector3(leftX + 40, topY + 10, 0);

            // Version Label (Created separately to position under name)
            var versionLabel = CreateLabel(root, "Version", "v1.3", new Vector3(leftX + 40, topY - 20, 0), 18, COLOR_SUBTEXT, uiFont, ttfFont, 600);
            versionLabel.alignment = NGUIText.Alignment.Left;
            versionLabel.pivot = UIWidget.Pivot.Left;
            versionLabel.transform.localPosition = new Vector3(leftX + 40, topY - 20, 0);
            // Store ref if needed, or just find it by name later if dynamic updates required (mostly static per open)
            _modVersionLabel = versionLabel;

            // 2. View Mode Toggle (Top Right)
            // Advanced is rightmost, Simple is to its left
            _advancedModeBtn = CreateButton(root, "BtnAdvanced", "ADVANCED", new Vector3(rightX - 60, topY, 0), 16, Color.white, uiFont, ttfFont, 120, 35, () => SetViewMode(SettingMode.Advanced));
            _simpleModeBtn = CreateButton(root, "BtnSimple", "SIMPLE", new Vector3(rightX - 190, topY, 0), 16, Color.white, uiFont, ttfFont, 120, 35, () => SetViewMode(SettingMode.Simple));
            
            // 3. Center Area (Presets & Search)
            // Preset Bar (Top Center)
            _presetBarRoot = new GameObject("PresetBar");
            _presetBarRoot.transform.SetParent(root, false);
            _presetBarRoot.layer = root.gameObject.layer;
            _presetBarRoot.transform.localPosition = new Vector3(0, topY - 10, 0); // Roughly align with title row

            // Search Bar (Below Presets, Middle)
            _searchBarRoot = new GameObject("SearchBar");
            _searchBarRoot.transform.SetParent(root, false);
            _searchBarRoot.layer = root.gameObject.layer;
            _searchBarRoot.transform.localPosition = new Vector3(0, topY - 55, 0);
            
            CreateSearchBar(_searchBarRoot.transform, uiFont, ttfFont);

            // Content Root
            _contentRoot = new GameObject("ContentRoot");
            _contentRoot.transform.SetParent(root, false);
            _contentRoot.layer = root.gameObject.layer;
            _contentRoot.transform.localPosition = Vector3.zero;

            float bottomY = -WINDOW_HEIGHT / 2 + 50;

            // Paging Buttons (Bottom Center)
            _prevBtn = CreateButton(root, "BtnPrev", "<", new Vector3(-60, bottomY, 0), 20, Color.white, uiFont, ttfFont, 50, 40, () => ChangePage(-1));
            _nextBtn = CreateButton(root, "BtnNext", ">", new Vector3(60, bottomY, 0), 20, Color.white, uiFont, ttfFont, 50, 40, () => ChangePage(1));
            _pagingLabel = CreateLabel(root, "Paging", "1/1", new Vector3(0, bottomY, 0), 18, COLOR_SUBTEXT, uiFont, ttfFont, 100);
            _pagingLabel.alignment = NGUIText.Alignment.Center;
            
            // RESET button (Bottom Left)
            CreateButton(root, "BtnReset", "DEFAULTS", new Vector3(leftX + 60, bottomY, 0), 16, Color.white, uiFont, ttfFont, 140, 40, () => OnResetClicked());

            // 4. Save & Close (Bottom Right)
            CreateButton(root, "BtnSaveAndClose", "SAVE & CLOSE", new Vector3(rightX - 100, bottomY, 0), 18, Color.white, uiFont, ttfFont, 200, 40, () => OnClose());

            MMLog.WriteDebug("UI Initial Construction Complete. Building Menu Content...");
            BuildMenu(uiFont, ttfFont);
            MMLog.WriteDebug($"UI Built for {_currentMod.Id}. Total settings: {_pages.Sum(p => p.Count)}");
        }

        private void Update()
        {
            if (!_inputLockedExternally)
                HandleManualSearchInput();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_inputLockedExternally)
                    return;

                // Esc is consumed by keybind capture while listening.
                if (KeybindCaptureListener.ShouldBlockEscapeClose())
                    return;
                OnClose();
            }
        }

        private void OnResetClicked()
        {
            if (_currentMod == null || _currentMod.SettingsProvider == null) return;
            _currentMod.SettingsProvider.ResetToDefaults();
            
            // Update snapshot so Defaults become the new Custom base
            var settings = _currentMod.SettingsProvider.GetSettingsObject();
            if (settings != null) _customSnapshotJson = JsonUtility.ToJson(settings);
            _presetOverride = null;

            // Auto-save after reset
            if (_currentMod.SettingsProvider is ISettingsProvider2 sp3) sp3.Save();

            BuildMenu(_modNameLabel.bitmapFont, _modNameLabel.trueTypeFont);
        }

        private void OnClose()
        {
            if (_isClosing) return;
            _isClosing = true;

            _lastClosedViewMode = _currentViewMode;
            FlushPendingSettingInputs();
            SaveCurrentSettings();

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

        private void CreateSearchBar(Transform parent, UIFont uiFont, Font ttfFont)
        {
            var searchLabel = CreateLabel(parent, "SearchLabel", "SEARCH:", new Vector3(-160, 0, 0), 14, COLOR_SUBTEXT, uiFont, ttfFont, 100);
            searchLabel.pivot = UIWidget.Pivot.Right; // Right align label to input box
            searchLabel.transform.localPosition = new Vector3(-110, 0, 0);

            var inputGO = new GameObject("SearchInput");
            inputGO.transform.SetParent(parent, false);
            inputGO.transform.localPosition = new Vector3(60, 0, 0); // Center on BG
            inputGO.layer = parent.gameObject.layer;

            var inputBg = inputGO.AddComponent<UITexture>();
            inputBg.mainTexture = _whiteTexture;
            inputBg.width = 320;
            inputBg.height = 35;
            inputBg.depth = 100;
            inputBg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(inputGO.transform, false);
            lblGO.layer = inputGO.layer;
            lblGO.transform.localPosition = new Vector3(-150, 0, 0);

            var lbl = lblGO.AddComponent<UILabel>();
            lbl.pivot = UIWidget.Pivot.Left;
            lbl.fontSize = 16;
            lbl.width = 300;
            lbl.depth = 105;
            lbl.overflowMethod = UILabel.Overflow.ClampContent;
            if (uiFont != null) lbl.bitmapFont = uiFont;
            else if (ttfFont != null) lbl.trueTypeFont = ttfFont;
            else lbl.trueTypeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            lbl.alignment = NGUIText.Alignment.Left;
            lbl.text = string.Empty;

            _searchInputRoot = inputGO;
            _searchDisplayLabel = lbl;
            _searchInput = null;
            _manualSearchEnabled = false;
            _searchHasFocus = false;

            bool hasUsableFont = lbl.bitmapFont != null || lbl.trueTypeFont != null;
            if (!hasUsableFont)
            {
                searchLabel.gameObject.SetActive(false);
                lbl.pivot = UIWidget.Pivot.Center;
                lbl.alignment = NGUIText.Alignment.Center;
                lbl.transform.localPosition = Vector3.zero;
                lbl.text = "Search unavailable";
                lbl.color = COLOR_SUBTEXT;
                return;
            }

            // Manual search mode is used in all runtimes to avoid Unity 5.3/5.6 UIInput edge cases.
            EnableManualSearchInput(inputGO, lbl);
            MMLog.WriteInfo("[ModSettingsPanel] Search using manual input mode.");
        }

        private void EnableManualSearchInput(GameObject inputGO, UILabel label)
        {
            if (inputGO == null || label == null) return;

            _manualSearchEnabled = true;
            _searchHasFocus = false;

            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = new Vector3(-150, 0, 0);
            label.color = Color.white;

            var col = inputGO.GetComponent<BoxCollider>();
            if (col == null) col = inputGO.AddComponent<BoxCollider>();
            col.size = new Vector3(320, 35, 1);
            col.center = Vector3.zero;

            UIEventListener.Get(inputGO).onClick = _ =>
            {
                _searchHasFocus = true;
                UICamera.selectedObject = null;
                UpdateManualSearchDisplay();
            };

            UpdateManualSearchDisplay();
        }

        private void HandleManualSearchInput()
        {
            if (!_manualSearchEnabled || _searchInputRoot == null || _searchDisplayLabel == null) return;

            if (Input.GetMouseButtonDown(0))
            {
                _searchHasFocus = IsHoveredWithin(_searchInputRoot);
                UpdateManualSearchDisplay();
            }

            if (!_searchHasFocus) return;

            string typed = Input.inputString;
            if (string.IsNullOrEmpty(typed)) return;

            bool changed = false;
            for (int i = 0; i < typed.Length; i++)
            {
                char c = typed[i];
                if (c == '\b')
                {
                    if (!string.IsNullOrEmpty(_searchFilter))
                    {
                        _searchFilter = _searchFilter.Substring(0, _searchFilter.Length - 1);
                        changed = true;
                    }
                    continue;
                }

                if (c == '\n' || c == '\r')
                {
                    _searchHasFocus = false;
                    continue;
                }

                if (char.IsControl(c)) continue;
                if (_searchFilter.Length >= MaxSearchLength) continue;

                _searchFilter += c;
                changed = true;
            }

            if (changed)
                BuildMenu(_activeBitmapFont, _activeTtfFont, true);

            UpdateManualSearchDisplay();
        }

        private void UpdateManualSearchDisplay()
        {
            if (_searchDisplayLabel == null) return;

            if (string.IsNullOrEmpty(_searchFilter))
            {
                _searchDisplayLabel.text = _searchHasFocus ? "|" : "Search...";
                _searchDisplayLabel.color = _searchHasFocus ? Color.white : COLOR_SUBTEXT;
                return;
            }

            _searchDisplayLabel.text = _searchHasFocus ? (_searchFilter + "|") : _searchFilter;
            _searchDisplayLabel.color = Color.white;
        }

        private static bool IsHoveredWithin(GameObject root)
        {
            if (root == null) return false;
            var hovered = UICamera.hoveredObject;
            if (hovered == null) return false;
            if (hovered == root) return true;
            return hovered.transform != null && hovered.transform.IsChildOf(root.transform);
        }
        
        private void CaptureTemplates(UIFont uiFont, Font ttfFont)
        {
            try {
                var allSliders = Resources.FindObjectsOfTypeAll<UISlider>();
                _sliderTemplate = allSliders.FirstOrDefault(s => s.gameObject.activeInHierarchy)?.gameObject ?? allSliders.FirstOrDefault()?.gameObject;
                SpineWidgetFactory.SliderTemplate = _sliderTemplate;
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
            _buttonTemplate = tpl;
        }

        private void SetViewMode(SettingMode mode)
        {
            _currentViewMode = mode;
            BuildMenu(_modNameLabel.bitmapFont, _modNameLabel.trueTypeFont);
        }

        private void ChangePage(int delta)
        {
             _currentPageIndex = Mathf.Clamp(_currentPageIndex + delta, 0, _pages.Count - 1);
            UpdatePageVisibility();
        }

        private void BuildMenu(UIFont uiFont, Font ttfFont, bool keepPage = false)
        {
            if (_isRebuilding) return;
            _isRebuilding = true;

            try
            {
                foreach (var page in _pages) foreach (var go in page) Destroy(go);
                _pages.Clear();
            
            foreach(Transform child in _presetBarRoot.transform) Destroy(child.gameObject);

            _modNameLabel.text = _currentMod.Name.ToUpper();
            if (_modVersionLabel != null) _modVersionLabel.text = $"v{_currentMod.Version}";
            
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
            _availablePresets = allDefs.Where(d => d.Presets != null)
                                      .SelectMany(d => d.Presets.Keys)
                                      .Distinct()
                                      .OrderBy(k => GetPresetPriority(k))
                                      .ThenBy(k => k)
                                      .ToList();

            UpdateCurrentPresetState(settings, allDefs);
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
            if (string.IsNullOrEmpty(_searchFilter))
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
                visible = visible.Where(d => (d.Label ?? d.Id).IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            if (provider is ICustomSettingsUI custom)
            {
                var customRoot = new GameObject("CustomUI");
                customRoot.transform.SetParent(_contentRoot.transform, false);
                customRoot.transform.localPosition = Vector3.zero;
                custom.DrawSettings(customRoot, WINDOW_WIDTH - 50, WINDOW_HEIGHT - 250);
                _pages.Add(new List<GameObject> { customRoot });
            }
            else
            {
                bool useWideKeybindLayout = ShouldUseWideKeybindLayout(visible, allDefs);
                if (useWideKeybindLayout)
                {
                    // Keybind rows are wide; use a single-column layout and fewer rows per page
                    // so page controls at the bottom remain unobstructed.
                    CreatePaginatedGrid(visible, allDefs, settings, itemsPerPage: 10, columns: 1, rowHeight: 50, startY: WINDOW_HEIGHT / 2 - 195f, useWideKeybindLayout: true);
                }
                else
                {
                    CreatePaginatedGrid(visible, allDefs, settings, itemsPerPage: 18, columns: 2, rowHeight: 55, startY: WINDOW_HEIGHT / 2 - 200f, useWideKeybindLayout: false);
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
            return HasPairedKeybindDefinitions(visibleItems) || HasPairedKeybindDefinitions(allDefs);
        }

        private static bool HasPairedKeybindDefinitions(List<SettingDefinition> items)
        {
            if (items == null) return false;

            for (int i = 0; i < items.Count; i++)
            {
                var def = items[i];
                if (def == null || def.Type != SettingType.Keybind || string.IsNullOrEmpty(def.Id))
                    continue;

                if (GetKeybindActionBaseId(def.Id) != null)
                    return true;
            }

            return false;
        }

        private void BuildPresetCycleWidget(UIFont uiFont, Font ttfFont, object settings, List<SettingDefinition> allDefs)
        {
            // Clear existing preset bar
            foreach(Transform child in _presetBarRoot.transform) Destroy(child.gameObject);

            // Hide the preset strip entirely for mods that do not define presets
            // (custom-only settings without Easy/Normal/Hard, etc.).
            if (_availablePresets == null || _availablePresets.Count == 0)
            {
                _presetBarRoot.SetActive(false);
                return;
            }

            _presetBarRoot.SetActive(true);
            
            float boxW = 200;
            float boxH = 40;
            float arrowSize = 40;

            // 1. Left Arrow
            CreateButton(_presetBarRoot.transform, "PresetPrev", "<", 
                new Vector3(-(boxW/2 + arrowSize/2 + 10), 0, 0), 20, Color.white, uiFont, ttfFont, (int)arrowSize, (int)boxH, () => {
                    CyclePreset(-1, allDefs, settings, uiFont, ttfFont);
                });

            // 2. Preset Name Box
            var bg = CreateTexturedBox(_presetBarRoot.transform, "PresetDisplay", Vector3.zero, (int)boxW, (int)boxH, COLOR_BTN_INACTIVE, 100, false);
            var lbl = CreateLabel(_presetBarRoot.transform, "PresetLabel", _currentPresetName.ToUpper(), Vector3.zero, 18, COLOR_TEXT, uiFont, ttfFont, 101);
            lbl.alignment = NGUIText.Alignment.Center;
            
            if (_currentPresetName == "Custom") bg.GetComponent<UITexture>().color = COLOR_CUSTOM;
            else bg.GetComponent<UITexture>().color = COLOR_PRESET_MATCH;

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
            // If no presets exist at all, do nothing
            if (_availablePresets.Count == 0) return;

            // Combine "Custom" + Presets into a single navigable list
            List<string> cycleList = new List<string> { "Custom" };
            cycleList.AddRange(_availablePresets);

            int currentIndex = cycleList.IndexOf(_currentPresetName);
            if (currentIndex == -1) currentIndex = 0; // Default to Custom if unknown

            int nextIndex = currentIndex + delta;
            
            // Wrap around
            if (nextIndex >= cycleList.Count) nextIndex = 0;
            if (nextIndex < 0) nextIndex = cycleList.Count - 1;

            string targetPreset = cycleList[nextIndex];
            MMLog.WriteDebug($"Cycling Preset to: {targetPreset}");

            if (targetPreset == "Custom")
            {
                // RESTORE Custom State: Put back the values the user edited manually
                if (!string.IsNullOrEmpty(_customSnapshotJson))
                {
                    var settingsObj = _currentMod.SettingsProvider.GetSettingsObject();
                    if (settingsObj != null)
                    {
                        MMLog.WriteDebug("Restoring Custom Snapshot...");
                        JsonUtility.FromJsonOverwrite(_customSnapshotJson, settingsObj);
                        
                        // Notify mod of change
                        _currentMod.SettingsProvider.OnSettingsLoaded();
                    }
                }
                _presetOverride = "Custom";
            }
            else
            {
                // It's a real preset
                ApplyPreset(targetPreset, allDefs, settings);
                _presetOverride = targetPreset;
            }

            BuildMenu(uiFont, ttfFont, true);
        }

        private void UpdateCurrentPresetState(object settings, List<SettingDefinition> allDefs)
        {
            if (_presetOverride != null)
            {
                _currentPresetName = _presetOverride;
                // We don't null it here, because BuildMenu might be called multiple times during UI assembly.
                // We'll trust the Cycle/Change calls to manage the override lifecycle.
                return;
            }

            _currentPresetName = "Custom";
            foreach(var preset in _availablePresets)
            {
                bool match = true;
                bool hasCheck = false; // Ensure at least one setting uses this preset name
                foreach(var def in allDefs)
                {
                    if (def.Presets != null && def.Presets.ContainsKey(preset))
                    {
                        hasCheck = true;
                        object pVal = def.Presets[preset];
                        object cVal = ReflectionHelper.GetValue(def, settings);
                        
                        // Compare values - handle loose typing
                        string sP = Convert.ToString(pVal, CultureInfo.InvariantCulture);
                        string sC = Convert.ToString(cVal, CultureInfo.InvariantCulture);
                        
                        if (!string.Equals(sP, sC)) 
                        { 
                            match = false; 
                            break; 
                        }
                    }
                }
                if (match && hasCheck) { _currentPresetName = preset; break; }
            }
        }

        private void ApplyPreset(string presetName, List<SettingDefinition> allDefs, object settings)
        {
            MMLog.WriteDebug($"Applying Preset: {presetName}");
            int appliedCount = 0;
            foreach(var def in allDefs)
            {
                if (def.Presets != null && def.Presets.TryGetValue(presetName, out var pVal))
                {
                     ReflectionHelper.SetValue(def, settings, pVal);
                     appliedCount++;
                }
            }
            
            // Notify mod that settings have changed so it can update its internal logic
            if (_currentMod != null && _currentMod.SettingsProvider != null)
            {
                _currentMod.SettingsProvider.OnSettingsLoaded();
            }

            if (_currentMod.SettingsProvider is ISettingsProvider2 sp2) sp2.Save();
            MMLog.WriteDebug($"Preset '{presetName}' applied ({appliedCount} fields updated)");
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

            for (int i = 0; i < displayEntries.Count; i += itemsPerPage)
            {
                var pageItems = new List<GameObject>();
                var segment = displayEntries.Skip(i).Take(itemsPerPage).ToList();
                int renderedRows = 0;

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
                        x = -260f;
                    }
                    else if (columns == 2)
                    {
                        x = (col == 0) ? -420f : 80f;
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
                        widget = CreateSectionHeaderWidget(entry.Primary);
                    }
                    else if (entry.Secondary != null)
                    {
                        widget = CreateDualKeybindWidget(entry.Primary, entry.Secondary, data);
                    }
                    else
                    {
                        widget = SpineWidgetFactory.CreateWidget(entry.Primary, _contentRoot.transform, data, this);
                    }

                    if (widget != null)
                    {
                        widget.transform.localPosition = new Vector3(x, y, 0);
                        if (useWideKeybindLayout)
                            NormalizeWideKeybindWidgetAlignment(widget, entry);
                        pageItems.Add(widget);
                        foreach (var w in widget.GetComponentsInChildren<UIWidget>(true)) w.depth += 100;

                        // Use hierarchy to check if any ancestor disables this widget.
                        UpdateWidgetEnabled(widget, !hierarchy.IsDisabledByAncestor(entry.Primary, data));
                        renderedRows++;
                    }
                }
                _pages.Add(pageItems);
            }

            if (_pages.Count == 0 || (_pages.Count == 1 && _pages[0].Count == 0 && !string.IsNullOrEmpty(_searchFilter)))
            {
                // Handle no search results.
                if (_pages.Count == 0) _pages.Add(new List<GameObject>());
            }
        }

        private List<KeybindDisplayEntry> BuildDisplayEntries(List<SettingDefinition> visibleItems, List<SettingDefinition> allDefs, bool pairKeybinds)
        {
            var entries = new List<KeybindDisplayEntry>();

            if (!pairKeybinds)
            {
                for (int i = 0; i < visibleItems.Count; i++)
                {
                    var def = visibleItems[i];
                    if (def == null) continue;
                    entries.Add(new KeybindDisplayEntry(def, null));
                }
                return entries;
            }

            var visibleById = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);
            var allById = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < visibleItems.Count; i++)
            {
                var def = visibleItems[i];
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    visibleById[def.Id] = def;
            }

            for (int i = 0; i < allDefs.Count; i++)
            {
                var def = allDefs[i];
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    allById[def.Id] = def;
            }

            for (int i = 0; i < visibleItems.Count; i++)
            {
                var def = visibleItems[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (consumed.Contains(def.Id)) continue;

                if (def.Type != SettingType.Keybind)
                {
                    consumed.Add(def.Id);
                    entries.Add(new KeybindDisplayEntry(def, null));
                    continue;
                }

                string baseId = GetKeybindActionBaseId(def.Id);
                if (string.IsNullOrEmpty(baseId))
                {
                    consumed.Add(def.Id);
                    entries.Add(new KeybindDisplayEntry(def, null));
                    continue;
                }

                string primaryId = baseId + ".primary";
                string secondaryId = baseId + ".secondary";

                SettingDefinition primary;
                SettingDefinition secondary;
                visibleById.TryGetValue(primaryId, out primary);
                visibleById.TryGetValue(secondaryId, out secondary);

                if (primary == null) allById.TryGetValue(primaryId, out primary);
                if (secondary == null) allById.TryGetValue(secondaryId, out secondary);

                if (primary == null && secondary == null)
                {
                    consumed.Add(def.Id);
                    entries.Add(new KeybindDisplayEntry(def, null));
                    continue;
                }

                if (primary == null) primary = def;
                consumed.Add(primary.Id);
                if (secondary != null) consumed.Add(secondary.Id);

                entries.Add(new KeybindDisplayEntry(primary, secondary));
            }

            return entries;
        }

        private GameObject CreateDualKeybindWidget(SettingDefinition primaryDef, SettingDefinition secondaryDef, object data)
        {
            var container = NGUITools.AddChild(_contentRoot);
            container.name = "DualKeybind_" + (primaryDef != null ? primaryDef.Id : "Unknown");
            NGUITools.SetLayer(container, _contentRoot.layer);
            const int keySlotWidth = 158;
            const int keySlotHeight = 38;
            const int clearWidth = 96;
            const int clearHeight = 38;

            string actionLabel = GetActionLabel(primaryDef, secondaryDef);
            var label = CreateLabel(container.transform, "ActionLabel", actionLabel, new Vector3(0, 0, 0), 16, COLOR_TEXT, _activeBitmapFont, _activeTtfFont, 102);
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = Vector3.zero;
            label.width = 250;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;
            SetTooltip(label.gameObject, primaryDef != null ? primaryDef.Tooltip : (secondaryDef != null ? secondaryDef.Tooltip : null));

            KeybindCaptureListener primaryCapture = null;
            KeybindCaptureListener secondaryCapture = null;

            Func<string> primaryDisplay = () => FormatKeyCode(ReadKeyCode(primaryDef, data));
            Func<string> secondaryDisplay = () => FormatKeyCode(ReadKeyCode(secondaryDef, data));

            Action refreshCapture = () =>
            {
                if (primaryCapture != null && primaryCapture.DisplayTextProvider != null && primaryCapture.ValueLabel != null)
                    primaryCapture.ValueLabel.text = primaryCapture.DisplayTextProvider();
                if (secondaryCapture != null && secondaryCapture.DisplayTextProvider != null && secondaryCapture.ValueLabel != null)
                    secondaryCapture.ValueLabel.text = secondaryCapture.DisplayTextProvider();
            };

            primaryCapture = CreateClickableKeySlot(
                container.transform,
                "Primary",
                new Vector3(290, 0, 0),
                primaryDisplay,
                null,
                key =>
                {
                    if (ApplySettingValue(primaryDef, data, key))
                    {
                        OnSettingChanged();
                        refreshCapture();
                    }
                },
                keySlotWidth,
                keySlotHeight);

            secondaryCapture = CreateClickableKeySlot(
                container.transform,
                "Secondary",
                new Vector3(465, 0, 0),
                secondaryDisplay,
                null,
                key =>
                {
                    if (ApplySettingValue(secondaryDef, data, key))
                    {
                        OnSettingChanged();
                        refreshCapture();
                    }
                },
                keySlotWidth,
                keySlotHeight);

            CreateButton(
                container.transform,
                "Clear",
                "CLEAR",
                new Vector3(630, 0, 0),
                13,
                Color.white,
                _activeBitmapFont,
                _activeTtfFont,
                clearWidth,
                clearHeight,
                () =>
                {
                    bool changed = false;
                    if (ApplySettingValue(primaryDef, data, KeyCode.None)) changed = true;
                    if (ApplySettingValue(secondaryDef, data, KeyCode.None)) changed = true;

                    if (changed)
                    {
                        OnSettingChanged();
                        refreshCapture();
                    }
                });

            return container;
        }

        private GameObject CreateSectionHeaderWidget(SettingDefinition def)
        {
            var container = NGUITools.AddChild(_contentRoot);
            container.name = "SectionHeader_" + (def != null ? def.Id : "Unknown");
            NGUITools.SetLayer(container, _contentRoot.layer);

            string title = def != null && !string.IsNullOrEmpty(def.Label)
                ? def.Label.ToUpperInvariant()
                : "SECTION";

            var label = CreateLabel(
                container.transform,
                "SectionLabel",
                title,
                new Vector3(0, 0, 0),
                20,
                def != null && def.HeaderColor.HasValue ? def.HeaderColor.Value : new Color(0.35f, 0.70f, 0.90f, 1f),
                _activeBitmapFont,
                _activeTtfFont,
                102);
            label.pivot = UIWidget.Pivot.Left;
            label.alignment = NGUIText.Alignment.Left;
            label.transform.localPosition = new Vector3(SectionHeaderLocalX, 0, 0);
            label.width = 300;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;

            return container;
        }

        private static bool IsSectionHeaderEntry(KeybindDisplayEntry entry)
        {
            return entry != null
                && entry.Secondary == null
                && entry.Primary != null
                && (entry.Primary.Type == SettingType.Header
                    || (!string.IsNullOrEmpty(entry.Primary.Id)
                        && entry.Primary.Id.StartsWith("CatHeader_", StringComparison.OrdinalIgnoreCase)));
        }

        private void NormalizeWideKeybindWidgetAlignment(GameObject widget, KeybindDisplayEntry entry)
        {
            if (widget == null || entry == null) return;

            var labels = widget.GetComponentsInChildren<UILabel>(true);
            if (labels == null || labels.Length == 0) return;

            bool isHeader = IsSectionHeaderEntry(entry);
            string target = isHeader
                ? ((entry.Primary != null && !string.IsNullOrEmpty(entry.Primary.Label))
                    ? entry.Primary.Label.ToUpperInvariant()
                    : "SECTION")
                : GetActionLabel(entry.Primary, entry.Secondary);

            UILabel best = null;
            for (int i = 0; i < labels.Length; i++)
            {
                UILabel candidate = labels[i];
                if (candidate == null) continue;

                string text = candidate.text ?? string.Empty;
                if (string.Equals(text.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    best = candidate;
                    break;
                }
            }

            if (best == null)
                best = labels[0];
            if (best == null) return;

            best.pivot = UIWidget.Pivot.Left;
            best.alignment = NGUIText.Alignment.Left;
            best.multiLine = false;
            best.overflowMethod = UILabel.Overflow.ClampContent;
            best.width = isHeader ? 320 : 250;

            var pos = best.transform.localPosition;
            best.transform.localPosition = new Vector3(isHeader ? SectionHeaderLocalX : 0f, pos.y, pos.z);
        }

        private KeybindCaptureListener CreateClickableKeySlot(
            Transform parent,
            string name,
            Vector3 localPosition,
            Func<string> displayTextProvider,
            Action onSelected,
            Action<KeyCode> onCaptured,
            int slotWidth,
            int slotHeight)
        {
            var slot = new GameObject(name);
            slot.transform.SetParent(parent, false);
            slot.transform.localPosition = localPosition;
            slot.layer = parent.gameObject.layer;

            var bg = slot.AddComponent<UITexture>();
            bg.mainTexture = _whiteTexture;
            bg.width = slotWidth;
            bg.height = slotHeight;
            bg.depth = 100;
            bg.color = new Color(0.19f, 0.15f, 0.12f, 0.95f);

            var valueLabel = CreateLabel(
                slot.transform,
                "Value",
                displayTextProvider != null ? displayTextProvider() : string.Empty,
                Vector3.zero,
                14,
                Color.white,
                _activeBitmapFont,
                _activeTtfFont,
                101);
            valueLabel.alignment = NGUIText.Alignment.Center;
            valueLabel.width = Mathf.Max(40, slotWidth - 8);
            valueLabel.height = Mathf.Max(20, slotHeight - 4);
            valueLabel.overflowMethod = UILabel.Overflow.ClampContent;
            valueLabel.multiLine = false;

            var col = slot.AddComponent<BoxCollider>();
            col.size = new Vector3(slotWidth, slotHeight, 1);
            col.center = Vector3.zero;

            var capture = slot.AddComponent<KeybindCaptureListener>();
            capture.ValueLabel = valueLabel;
            capture.DisplayTextProvider = displayTextProvider;
            capture.OnCanceled = () =>
            {
                if (displayTextProvider != null)
                    valueLabel.text = displayTextProvider();
            };
            capture.OnCaptured = key =>
            {
                if (onCaptured != null) onCaptured(key);
                if (displayTextProvider != null)
                    valueLabel.text = displayTextProvider();
            };

            UIEventListener.Get(slot).onClick = _ =>
            {
                if (onSelected != null) onSelected();
                capture.StartCapture();
            };

            return capture;
        }

        private static bool ApplySettingValue(SettingDefinition def, object settingsObject, object newValue)
        {
            if (def == null) return false;

            try
            {
                if (def.Validate != null && !def.Validate(newValue, settingsObject))
                    return false;

                if (def.Setter != null)
                    def.Setter(settingsObject, newValue);

                if (def.OnChanged != null)
                    def.OnChanged(settingsObject);

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ModSettingsPanel] Failed to apply keybind value for " + def.Id + ": " + ex.Message);
                return false;
            }
        }

        private static KeyCode ReadKeyCode(SettingDefinition def, object settingsObject)
        {
            if (def == null) return KeyCode.None;

            object value = null;
            try
            {
                if (def.Getter != null)
                    value = def.Getter(settingsObject);
            }
            catch { }

            if (value is KeyCode) return (KeyCode)value;
            if (value is int) return (KeyCode)(int)value;

            if (value != null)
            {
                try
                {
                    return (KeyCode)Enum.Parse(typeof(KeyCode), value.ToString(), true);
                }
                catch { }
            }

            if (def.DefaultValue is KeyCode) return (KeyCode)def.DefaultValue;
            return KeyCode.None;
        }

        private static string GetKeybindActionBaseId(string settingId)
        {
            if (string.IsNullOrEmpty(settingId)) return null;

            if (settingId.EndsWith(".primary", StringComparison.OrdinalIgnoreCase))
                return settingId.Substring(0, settingId.Length - ".primary".Length);
            if (settingId.EndsWith(".secondary", StringComparison.OrdinalIgnoreCase))
                return settingId.Substring(0, settingId.Length - ".secondary".Length);

            return null;
        }

        private static string GetActionLabel(SettingDefinition primaryDef, SettingDefinition secondaryDef)
        {
            if (primaryDef != null && !string.IsNullOrEmpty(primaryDef.Label))
                return primaryDef.Label.Replace(" (Alt)", string.Empty);

            if (secondaryDef != null && !string.IsNullOrEmpty(secondaryDef.Label))
                return secondaryDef.Label.Replace(" (Alt)", string.Empty);

            return "UNNAMED ACTION";
        }

        private static string FormatKeyCode(KeyCode key)
        {
            if (key == KeyCode.None) return "UNBOUND";

            string raw = key.ToString();
            if (raw.StartsWith("Alpha", StringComparison.Ordinal) && raw.Length == 6) return raw.Substring(5);
            if (raw.StartsWith("Keypad", StringComparison.Ordinal)) return "KP " + HumanizeKeyName(raw.Substring(6)).ToUpperInvariant();
            if (raw.EndsWith("Arrow", StringComparison.Ordinal)) return raw.Replace("Arrow", string.Empty).ToUpperInvariant();
            if (raw == "Mouse0") return "MOUSE LEFT";
            if (raw == "Mouse1") return "MOUSE RIGHT";
            if (raw == "Mouse2") return "MOUSE MIDDLE";
            return HumanizeKeyName(raw).ToUpperInvariant();
        }

        private static string HumanizeKeyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            char prev = '\0';
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '_' || c == '-')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        sb.Append(' ');
                    prev = c;
                    continue;
                }

                bool addSpace =
                    i > 0 &&
                    (
                        (char.IsUpper(c) && (char.IsLower(prev) || char.IsDigit(prev))) ||
                        (char.IsDigit(c) && char.IsLetter(prev)) ||
                        (char.IsLetter(c) && char.IsDigit(prev))
                    );

                if (addSpace && sb.Length > 0 && sb[sb.Length - 1] != ' ')
                    sb.Append(' ');

                sb.Append(c);
                prev = c;
            }

            return sb.ToString().Trim();
        }

        private sealed class KeybindDisplayEntry
        {
            public readonly SettingDefinition Primary;
            public readonly SettingDefinition Secondary;

            public KeybindDisplayEntry(SettingDefinition primary, SettingDefinition secondary)
            {
                Primary = primary;
                Secondary = secondary;
            }
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
            _presetOverride = "Custom"; 
            
            if (_currentMod == null) return;
            
            // Update the snapshot so this manual change becomes the stored "Custom" state
            var settings = _currentMod.SettingsProvider.GetSettingsObject();
            if (_currentMod.SettingsProvider is ISettingsProvider2 sp2) _customSnapshotJson = sp2.SerializeToJson();

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

            bool showPaging = _pages.Count > 1;
            
            if (_pagingLabel != null) {
                _pagingLabel.gameObject.SetActive(showPaging);
                _pagingLabel.text = $"{_currentPageIndex + 1}/{_pages.Count}";
            }
            if (_prevBtn != null) _prevBtn.SetActive(showPaging);
            if (_nextBtn != null) _nextBtn.SetActive(showPaging);

            if (showPaging)
            {
                _prevBtn.GetComponent<UIButton>().isEnabled = _currentPageIndex > 0;
                _nextBtn.GetComponent<UIButton>().isEnabled = _currentPageIndex < _pages.Count - 1;
                UpdateButtonState(_prevBtn, _currentPageIndex > 0, true);
                UpdateButtonState(_nextBtn, _currentPageIndex < _pages.Count - 1, true);
            }
        }



        // --- Helpers ---
        private void UpdateButtonState(GameObject btnGO, bool allowed, bool active)
        {
            if(!btnGO) return;
            var btn = btnGO.GetComponent<UIButton>();
            var bg = btnGO.GetComponent<UITexture>();
            var lbl = btnGO.GetComponentInChildren<UILabel>();
            btn.isEnabled = allowed;
            if (!allowed) { if (bg) bg.color = Color.Lerp(COLOR_BTN_INACTIVE, Color.black, 0.5f); if (lbl) lbl.color = Color.gray; }
            else { if (bg) bg.color = active ? COLOR_BTN_ACTIVE : COLOR_BTN_INACTIVE; if (lbl) lbl.color = active ? Color.white : COLOR_SUBTEXT; }
        }

        private GameObject CreateTexturedBox(Transform parent, string name, Vector3 pos, int w, int h, Color color, int depth, bool addCollider)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer; go.transform.localPosition = pos;
            var tex = go.AddComponent<UITexture>(); tex.mainTexture = _whiteTexture; tex.width = w; tex.height = h; tex.depth = depth; tex.color = color;
            if (addCollider) { var col = go.AddComponent<BoxCollider>(); col.size = new Vector3(w, h, 1); }
            return go;
        }

        private UILabel CreateLabel(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int depth)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer; go.transform.localPosition = pos;
            var label = go.AddComponent<UILabel>(); label.text = text; label.fontSize = fontSize; label.color = color; label.depth = depth;
            label.overflowMethod = UILabel.Overflow.ResizeFreely; label.bitmapFont = uiFont; label.trueTypeFont = ttfFont;
            return label;
        }

        private static void SetTooltip(GameObject go, string text)
        {
            if (go == null || string.IsNullOrEmpty(text)) return;
            
            // NGUI hover events require a collider.
            var box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                NGUITools.AddWidgetCollider(go);
                box = go.GetComponent<BoxCollider>();
            }

            if (box != null)
            {
                var widget = go.GetComponent<UIWidget>();
                if (widget != null && (box.size.x < 1f || box.size.y < 1f))
                {
                    box.size = new Vector3(Mathf.Max(widget.width, 200), Mathf.Max(widget.height, 24), 1);
                    box.center = new Vector3(box.size.x / 2, 0, 0);
                }
            }

            var panel = NGUITools.FindInParents<UIPanel>(go);
            var root = panel != null ? panel.transform : (go.transform != null ? go.transform.root : null);
            if (root == null) return;

            var label = go.GetComponent<UILabel>();
            UIHelper.AddTooltip(go, root, text, label != null ? label.bitmapFont : null, label != null ? label.trueTypeFont : null);
        }

        private int GetPresetPriority(string name)
        {
            if (string.IsNullOrEmpty(name)) return 999;
            string n = name.ToLowerInvariant();
            if (n == "easy") return 1;
            if (n == "medium" || n == "normal") return 2;
            if (n == "hard") return 3;
            if (n == "insane" || n == "extreme" || n == "hardcore") return 4;
            return 100;
        }

        private GameObject CreateButton(Transform parent, string name, string text, Vector3 pos, int fontSize, Color color, UIFont uiFont, Font ttfFont, int w, int h, Action onClick)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer; go.transform.localPosition = pos;
            var bg = go.AddComponent<UITexture>(); bg.mainTexture = _whiteTexture; bg.width = w; bg.height = h; bg.depth = 100; bg.color = COLOR_BTN_ACTIVE;
            var labelGo = new GameObject("Label"); labelGo.transform.SetParent(go.transform, false); labelGo.layer = go.layer;
            var label = labelGo.AddComponent<UILabel>(); label.text = text; label.fontSize = fontSize; label.color = color; label.depth = 101;
            label.alignment = NGUIText.Alignment.Center; label.bitmapFont = uiFont; label.trueTypeFont = ttfFont;
            label.width = Mathf.Max(20, w - 8);
            label.height = h;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;
            var col = go.AddComponent<BoxCollider>(); col.size = new Vector3(w, h, 1);
            var btn = go.AddComponent<UIButton>(); btn.tweenTarget = go;
            if (onClick != null) EventDelegate.Set(btn.onClick, () => onClick());
            return go;
        }

        public static class ReflectionHelper
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

