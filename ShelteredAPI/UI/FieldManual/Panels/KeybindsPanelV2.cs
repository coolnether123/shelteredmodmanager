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
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Tooltips;
using ShelteredAPI.UI.FieldManual.Widgets;

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

        private static GameObject _instance;

        private ModEntry _mod;
        private ISettingsProvider _settingsProvider;
        private object _settingsObject;
        private IThemePalette _palette;
        private IThemeMetrics _metrics;
        private ProceduralTextureLibrary _textures;
        private UIPrimitiveFactory _ui;
        private PaperPagedList _pagedList;
        private GameObject _pageFlipRoot;
        private BookPageNavigatorWidget _pageNavigator;
        private IFieldManualTransition _pageTransition;
        private FieldManualPageTurnController _pageTurnController;
        private VanillaPageTurnAssets _pageTurnAssets;
        private readonly PanelPageState _pageState = new PanelPageState();
        private List<List<ModSettingsKeybindDisplayEntry>> _pages = new List<List<ModSettingsKeybindDisplayEntry>>();
        private int _pageItemHeightBudget;
        private TooltipBus _tooltipBus;
        private TooltipDisplayWidget _tooltipDisplay;
        private bool _isClosing;
        private bool _closedRaised;
        private bool _controllerAxisButtonDown;

        public static void Show(ModEntry mod)
        {
            if (mod == null || mod.SettingsProvider == null)
            {
                MMLog.WriteWarning("[KeybindsPanelV2] Show called with null mod or provider; aborting.");
                return;
            }

            if (_instance != null) Destroy(_instance);
            UIFontCache.RefreshIfMissing();

            UIPanel overlay = UIUtil.EnsureOverlayPanel(OverlayName, OverlayDepth);
            GameObject root = new GameObject("KeybindBook_Root");
            root.transform.SetParent(overlay.transform, false);
            root.layer = overlay.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            _instance = root;

            KeybindsPanelV2 panel = root.AddComponent<KeybindsPanelV2>();
            panel._mod = mod;
            panel.Initialise(root);
        }

        private void Initialise(GameObject root)
        {
            _palette = new FieldManualPalette();
            _metrics = new FieldManualMetrics();
            _textures = new ProceduralTextureLibrary(_palette);
            _pageTransition = new FieldManualFadeTransition(FieldManualTransitionProfile.VanillaPageInfoFade);
            _pageTurnAssets = new VanillaPageTurnAssets();

            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            _ui = new UIPrimitiveFactory(fonts.Bitmap, fonts.TTF, OverlayDepth);
            ConfigurePageTurnController(root);

            string title = _mod.Name ?? "Controls";
            string subtitle = string.IsNullOrEmpty(_mod.Version) ? "Input Bindings" : ("Input Bindings - v" + _mod.Version);

            _tooltipBus = new TooltipBus();
            _tooltipBus.DefaultMessage = TooltipMessage.Hint("Select an action. Click a binding to change it.");

            IPanelFrame frame = new ShelteredBookFrame(_palette, _metrics, _textures, _ui);
            PanelFrameRegions regions = frame.Build(root, title, subtitle);
            _pageFlipRoot = _ui.CreateChild(root, "BookPageFlipRoot", Vector3.zero);

            BuildPagedList(regions);
            BuildTooltipStrip(regions);
            BuildFooter(regions);
            BuildContent();
        }

        private void ConfigurePageTurnController(GameObject root)
        {
            _pageTurnController = root.AddComponent<FieldManualPageTurnController>();
            _pageTurnController.Configure(
                FieldManualPageTurnProfile.VanillaClipboard,
                new FieldManualFadeTransition(FieldManualTransitionProfile.FadeOut(0.06f, 0f, UITweener.Method.EaseOut)),
                _pageTransition,
                new FieldManualFadeTransition(FieldManualTransitionProfile.Between(0.35f, 1f, 0.12f, 0f, UITweener.Method.EaseOut)),
                new FieldManualPageTurnAudio(_pageTurnAssets),
                new FieldManualPageFlipOverlay(_pageTurnAssets, _textures, _ui, _metrics.PanelWidth - 40f, _metrics.PanelHeight - 140f));
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
                regions.ContentRectLocal.height);

            _pagedList = new PaperPagedList(viewport, _ui.NextDepth());
            _pagedList.Build(regions.ContentRoot);
            _pageItemHeightBudget = (int)Mathf.Max(1f, viewport.height - HeaderRowHeight - _metrics.RowSpacing);
        }

        private void BuildFooter(PanelFrameRegions regions)
        {
            var buttonFactory = new BookButtonWidget(_palette, _textures, _ui);

            buttonFactory.Build(regions.FooterRoot, "BackButton", "Back",
                new Vector3(-460f, -400f, 0f), 200, 58, 24, Close);

            buttonFactory.Build(regions.FooterRoot, "DefaultsButton", "Defaults",
                new Vector3(320f, -400f, 0f), 240, 58, 22, ResetAllDefaults);

            _pageNavigator = new BookPageNavigatorWidget(_palette, _textures, _ui, _pageTurnAssets);
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
            List<SettingDefinition> visible = displayDefs.Where(IsVisible).ToList();

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
            if (animate && _pageTransition != null)
                _pageTransition.Play(_pagedList.ContentRoot);

            if (_pageNavigator != null)
                _pageNavigator.UpdateState(_pageState.CurrentPageIndex, _pageState.PageCount);

            if (animate && _pageTransition != null && _pageNavigator != null)
                _pageTransition.Play(_pageNavigator.PageLabelRoot);
        }

        private void BuildCurrentPageRows(List<ModSettingsKeybindDisplayEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            var headerFactory = new KeybindColumnHeaderWidget(_palette, _metrics, _textures, _ui);
            _pagedList.AddRow(headerFactory.Build(_pagedList.ContentRoot), HeaderRowHeight);

            var rowFactory = new KeybindRowWidget(_palette, _metrics, _textures, _ui, _tooltipBus, _settingsProvider, _settingsObject, ApplyValue, OnValueChanged);
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
            BuildContent();
        }

        private void Update()
        {
            HandlePageInput();

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (KeybindCaptureListener.ShouldBlockEscapeClose()) return;
                Close();
            }
        }

        private void HandlePageInput()
        {
            if (_pageState.PageCount <= 1 || KeybindCaptureListener.HasActiveCapture())
                return;
            if (_pageTurnController != null && _pageTurnController.IsLocked)
                return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.PageUp))
            {
                ChangePage(-1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.PageDown))
            {
                ChangePage(1);
                return;
            }

            float horizontal = PlatformInput.GetAxis(PlatformInput.MenuInputAxis.UIhorizontal);
            if (!_controllerAxisButtonDown)
            {
                if (horizontal > 0.5f)
                {
                    ChangePage(1);
                    _controllerAxisButtonDown = true;
                }
                else if (horizontal < -0.5f)
                {
                    ChangePage(-1);
                    _controllerAxisButtonDown = true;
                }
            }
            else if (horizontal < 0.5f && horizontal > -0.5f)
            {
                _controllerAxisButtonDown = false;
            }
        }

        private void ChangePage(int delta)
        {
            if (KeybindCaptureListener.HasActiveCapture())
                return;

            if (_pageTurnController != null)
            {
                _pageTurnController.TryTurn(
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
            if (_textures != null) _textures.Dispose();
            RaiseClosedOnce();
            if (_instance != null) Destroy(_instance);
            _instance = null;
        }

        private void OnDestroy()
        {
            if (_tooltipDisplay != null) _tooltipDisplay.Detach();
            if (_textures != null) _textures.Dispose();
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
