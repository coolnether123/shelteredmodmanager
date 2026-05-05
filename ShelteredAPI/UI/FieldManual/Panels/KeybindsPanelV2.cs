using System.Collections.Generic;
using System.Linq;
using ModAPI.Core;
using ModAPI.Spine;
using ShelteredAPI.UI.Spine;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.Internal;
using UnityEngine;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Frame;
using ShelteredAPI.UI.FieldManual.Layout;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Tooltips;
using ShelteredAPI.UI.FieldManual.Widgets;


using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI;
using ShelteredAPI.UI.Internal.Settings;
namespace ShelteredAPI.UI.FieldManual.Panels
{
    /// <summary>
    /// Orchestrator for the Sheltered keybinds panel. Composes a scenario-book frame,
    /// paged list, and keybind row widgets while reusing the existing settings
    /// layout/runtime services.
    /// </summary>
    internal sealed class KeybindsPanelV2 : MonoBehaviour
    {
        private const int OverlayDepth = 50100;
        private const string OverlayName = "ShelteredAPI_KeybindBookPanel";
        private const int HeaderRowHeight = 32;
        private const float SearchBarY = 222f;
        private const float SearchReservedHeight = 54f;

        private static GameObject _instance;

        private ModEntry _mod;
        private ISettingsProvider _settingsProvider;
        private object _settingsObject;
        private IThemePalette _palette;
        private IThemeMetrics _metrics;
        private UIPrimitiveFactory _ui;
        private FieldManualWindowChrome _chrome;
        private PaperPagedList _pagedList;
        private GameObject _pageFlipRoot;
        private BookPageNavigatorWidget _pageNavigator;
        private BookSearchBarWidget _searchBar;
        private FieldManualBookPageTurn _pageTurn;
        private readonly PanelPageState _pageState = new PanelPageState();
        private List<List<ModSettingsKeybindDisplayEntry>> _pages = new List<List<ModSettingsKeybindDisplayEntry>>();
        private int _pageItemHeightBudget;
        private TooltipBus _tooltipBus;
        private TooltipDisplayWidget _tooltipDisplay;
        private bool _isClosing;
        private bool _closedRaised;

        public static void Show(ModEntry mod)
        {
            if (mod == null || mod.SettingsProvider == null)
            {
                MMLog.WriteWarning("[KeybindsPanelV2] Show called with null mod or provider; aborting.");
                return;
            }

            if (_instance != null) Destroy(_instance);

            GameObject root = FieldManualWindowChrome.CreateOverlayRoot(OverlayName, OverlayDepth, "KeybindBook_Root");
            _instance = root;

            KeybindsPanelV2 panel = root.AddComponent<KeybindsPanelV2>();
            panel._mod = mod;
            panel.Initialise(root);
        }

        private void Initialise(GameObject root)
        {
            string title = _mod.Name ?? "Controls";
            string subtitle = string.IsNullOrEmpty(_mod.Version) ? "Input Bindings" : ("Input Bindings - v" + _mod.Version);

            _chrome = FieldManualWindowChrome.BuildBook(root, OverlayDepth, title, subtitle);
            _palette = _chrome.Palette;
            _metrics = _chrome.Metrics;
            _ui = _chrome.Ui;
            _pageTurn = FieldManualBookPageTurn.Attach(root, _chrome);

            _tooltipBus = new TooltipBus();
            _tooltipBus.DefaultMessage = TooltipMessage.Hint("Select an action. Click a binding to change it.");

            PanelFrameRegions regions = _chrome.Regions;
            _pageFlipRoot = _ui.CreateChild(root, "BookPageFlipRoot", Vector3.zero);

            BuildSearchBar(regions);
            BuildPagedList(regions);
            BuildTooltipStrip(regions);
            BuildFooter(regions);
            BuildContent();
        }

        private void BuildSearchBar(PanelFrameRegions regions)
        {
            _searchBar = new BookSearchBarWidget(_palette, _chrome.Textures, _ui);
            _searchBar.Build(regions.ContentRoot, "KeybindSearchBar", new Vector3(-300f, SearchBarY, 0f), "Search controls...");
        }

        private void BuildTooltipStrip(PanelFrameRegions regions)
        {
            float y = -_metrics.PanelHeight * 0.5f + _metrics.FooterHeight + _metrics.ContentBottomPadding + 20;
            _tooltipDisplay = new TooltipDisplayWidget(_palette, _ui);
            _tooltipDisplay.Build(regions.Root, new Vector3(300f, y, 0), 480, _tooltipBus);
        }

        private void BuildPagedList(PanelFrameRegions regions)
        {
            Rect viewport = new Rect(
                -regions.ContentRectLocal.width * 0.5f,
                -regions.ContentRectLocal.height * 0.5f,
                regions.ContentRectLocal.width,
                regions.ContentRectLocal.height - SearchReservedHeight);

            _pagedList = new PaperPagedList(viewport, _ui.NextDepth());
            _pagedList.Build(regions.ContentRoot);
            _pageItemHeightBudget = (int)Mathf.Max(1f, viewport.height - HeaderRowHeight - _metrics.RowSpacing);
        }

        private void BuildFooter(PanelFrameRegions regions)
        {
            var buttonFactory = _chrome.Buttons;

            buttonFactory.Build(regions.FooterRoot, "BackButton", "Back",
                new Vector3(-460f, -400f, 0f), 200, 58, 24, Close);

            buttonFactory.Build(regions.FooterRoot, "DefaultsButton", "Defaults",
                new Vector3(320f, -400f, 0f), 240, 58, 22, ResetAllDefaults);

            _pageNavigator = new BookPageNavigatorWidget(_palette, _chrome.Textures, _ui, _pageTurn != null ? _pageTurn.Assets : null);
            _pageNavigator.Build(regions.FooterRoot, new Vector3(0f, -400f, 0f),
                delegate { ChangePage(-1); },
                delegate { ChangePage(1); });
        }

        private void BuildContent()
        {
            _settingsProvider = _mod.SettingsProvider;
            _settingsObject = _settingsProvider.GetSettingsObject();
            List<SettingDefinition> allDefs = _settingsProvider.GetSettings().ToList();
            List<SettingDefinition> displayDefs = allDefs.Where(IsKeybindPanelItem).ToList();
            List<SettingDefinition> visible = displayDefs.Where(IsVisible).Where(MatchesSearch).ToList();

            bool pairKeybinds = ModSettingsKeybindLayout.ShouldUseWideKeybindLayout(visible, displayDefs);
            List<ModSettingsKeybindDisplayEntry> entries = ModSettingsKeybindLayout.BuildDisplayEntries(visible, displayDefs, pairKeybinds);

            _pages = BuildPages(entries);
            _pageState.SetPageCount(_pages.Count);
            RenderCurrentPage(false);
        }

        private List<List<ModSettingsKeybindDisplayEntry>> BuildPages(List<ModSettingsKeybindDisplayEntry> entries)
        {
            var pages = new List<List<ModSettingsKeybindDisplayEntry>>();
            if (entries == null || entries.Count == 0)
                return pages;

            var rows = new List<PaperPageRow<ModSettingsKeybindDisplayEntry>>();
            for (int i = 0; i < entries.Count; i++)
            {
                ModSettingsKeybindDisplayEntry entry = entries[i];
                rows.Add(new PaperPageRow<ModSettingsKeybindDisplayEntry>(
                    entry,
                    GetEntryHeight(entry),
                    ModSettingsKeybindLayout.IsSectionHeaderEntry(entry)));
            }

            var paginator = new PaperPagePaginator<ModSettingsKeybindDisplayEntry>(_pageItemHeightBudget, _metrics.RowSpacing);
            pages = paginator.BuildPages(rows);
            return pages;
        }

        private int GetEntryHeight(ModSettingsKeybindDisplayEntry entry)
        {
            return ModSettingsKeybindLayout.IsSectionHeaderEntry(entry)
                ? _metrics.SectionStampHeight
                : _metrics.RowHeight;
        }

        private void RenderCurrentPage(bool animate)
        {
            if (_pagedList == null)
                return;

            _pagedList.Clear();
            if (_tooltipBus != null)
                _tooltipBus.Clear();

            bool hasRows = _pages != null && _pages.Count > 0;
            if (hasRows)
                BuildCurrentPageRows(_pages[_pageState.CurrentPageIndex]);

            _pagedList.Layout(_metrics.RowSpacing);
            if (animate && _pageTurn != null && _pageTurn.PageTransition != null)
                _pageTurn.PageTransition.Play(_pagedList.ContentRoot);

            if (_pageNavigator != null)
                _pageNavigator.UpdateState(_pageState.CurrentPageIndex, _pageState.PageCount);

            if (animate && _pageTurn != null && _pageTurn.PageTransition != null && _pageNavigator != null)
                _pageTurn.PageTransition.Play(_pageNavigator.PageLabelRoot);
        }

        private void BuildCurrentPageRows(List<ModSettingsKeybindDisplayEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            var headerFactory = new KeybindColumnHeaderWidget(_palette, _metrics, _chrome.Textures, _ui);
            _pagedList.AddRow(headerFactory.Build(_pagedList.ContentRoot), HeaderRowHeight);

            var rowFactory = new KeybindRowWidget(_palette, _metrics, _chrome.Textures, _ui, _tooltipBus, _settingsProvider, _settingsObject, ApplyValue, OnValueChanged);
            var stampFactory = new SectionStampWidget(_palette, _metrics, _ui);

            for (int i = 0; i < entries.Count; i++)
            {
                ModSettingsKeybindDisplayEntry entry = entries[i];
                if (entry == null || entry.Primary == null) continue;

                if (ModSettingsKeybindLayout.IsSectionHeaderEntry(entry))
                {
                    GameObject stamp = stampFactory.Build(_pagedList.ContentRoot, entry.Primary.Label);
                    _pagedList.AddRow(stamp, _metrics.SectionStampHeight);
                }
                else
                {
                    GameObject row = rowFactory.Build(_pagedList.ContentRoot, entry);
                    _pagedList.AddRow(row, _metrics.RowHeight);
                }
            }
        }

        private static bool IsVisible(SettingDefinition def)
        {
            return def != null && def.ShowInAdvancedView;
        }

        private static bool IsKeybindPanelItem(SettingDefinition def)
        {
            return def != null && (def.Type == SettingType.Keybind || def.Type == SettingType.Header);
        }

        private static bool ApplyValue(SettingDefinition def, object settings, object value)
        {
            return ModSettingsKeybindRuntime.ApplySettingValue(def, settings, value);
        }

        private void OnValueChanged()
        {
            ISettingsProvider2 sp2 = _mod.SettingsProvider as ISettingsProvider2;
            if (sp2 != null) sp2.Save();
        }

        private void ResetAllDefaults()
        {
            if (_mod == null || _mod.SettingsProvider == null) return;
            _mod.SettingsProvider.ResetToDefaults();
            ISettingsProvider2 sp2 = _mod.SettingsProvider as ISettingsProvider2;
            if (sp2 != null) sp2.Save();
            Rebuild();
        }

        private void Rebuild()
        {
            if (_pagedList != null)
                _pagedList.Clear();
            _pageState.Reset();
            BuildContent();
        }

        private void Update()
        {
            if (_searchBar != null)
                _searchBar.HandleInput(delegate { Rebuild(); });

            HandlePageInput();

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (KeybindCaptureListener.ShouldBlockEscapeClose()) return;
                Close();
            }
        }

        private void HandlePageInput()
        {
            if (_pageTurn != null)
                _pageTurn.HandlePageInput(_pageState.PageCount, KeybindCaptureListener.HasActiveCapture, ChangePage);
        }

        private bool MatchesSearch(SettingDefinition def)
        {
            if (def == null)
                return false;

            string filter = _searchBar != null ? _searchBar.Filter : null;
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
                && value.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ChangePage(int delta)
        {
            if (KeybindCaptureListener.HasActiveCapture())
                return;

            if (_pageTurn != null)
            {
                _pageTurn.TryTurn(
                    delta,
                    _pagedList != null ? _pagedList.ContentRoot : null,
                    _pageFlipRoot != null ? _pageFlipRoot : (_pagedList != null ? _pagedList.Viewport : null),
                    _pageNavigator != null ? _pageNavigator.PageLabelRoot : null,
                    CanChangePage,
                    CommitPageChange,
                    RenderCurrentPageWithoutAnimation);
                return;
            }

            if (!_pageState.MoveBy(delta))
                return;

            RenderCurrentPage(true);
        }

        private bool CanChangePage(int delta)
        {
            if (delta < 0)
                return _pageState.CanGoPrevious;
            if (delta > 0)
                return _pageState.CanGoNext;

            return false;
        }

        private void CommitPageChange(int delta)
        {
            _pageState.MoveBy(delta);
        }

        private void RenderCurrentPageWithoutAnimation()
        {
            RenderCurrentPage(false);
        }

        private void Close()
        {
            if (_isClosing) return;
            _isClosing = true;
            if (_tooltipDisplay != null) { _tooltipDisplay.Detach(); _tooltipDisplay = null; }
            if (_tooltipBus != null) { _tooltipBus.Clear(); _tooltipBus = null; }
            ISettingsProvider2 sp2 = _mod.SettingsProvider as ISettingsProvider2;
            if (sp2 != null) sp2.Save();
            if (_chrome != null) { _chrome.Dispose(); _chrome = null; }
            RaiseClosedOnce();
            if (_instance != null) Destroy(_instance);
            _instance = null;
        }

        private void OnDestroy()
        {
            if (_tooltipDisplay != null) _tooltipDisplay.Detach();
            if (_chrome != null) { _chrome.Dispose(); _chrome = null; }
            RaiseClosedOnce();
            if (_instance == gameObject) _instance = null;
        }

        private void RaiseClosedOnce()
        {
            if (_closedRaised) return;
            _closedRaised = true;
            ShelteredAPI.UI.ShelteredKeybindsUIV2.NotifyClosed();
        }
    }
}
