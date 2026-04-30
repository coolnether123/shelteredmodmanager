using System.Collections.Generic;
using System.Linq;
using ModAPI.Core;
using ModAPI.Internal.UI;
using ModAPI.Spine;
using ModAPI.Spine.UI;
using ModAPI.UI;
using UnityEngine;
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
    /// Orchestrator for the redesigned Sheltered keybinds panel. Composes a frame,
    /// a scrollable paper list, and keybind row widgets. Reuses the existing
    /// <see cref="ModSettingsKeybindLayout"/> / <see cref="ModSettingsKeybindRuntime"/>
    /// services so the data model is unchanged.
    /// </summary>
    internal sealed class KeybindsPanelV2 : MonoBehaviour
    {
        private const int OverlayDepth = 50100;
        private const string OverlayName = "ShelteredAPI_FieldManualPanel";

        private static GameObject _instance;

        private ModEntry _mod;
        private IThemePalette _palette;
        private IThemeMetrics _metrics;
        private ProceduralTextureLibrary _textures;
        private UIPrimitiveFactory _ui;
        private PaperScrollList _scrollList;
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
            UIFontCache.RefreshIfMissing();

            UIPanel overlay = UIUtil.EnsureOverlayPanel(OverlayName, OverlayDepth);
            GameObject root = new GameObject("FieldManual_Root");
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

            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            _ui = new UIPrimitiveFactory(fonts.Bitmap, fonts.TTF, OverlayDepth);

            string title = _mod.Name ?? "Controls";
            string subtitle = string.IsNullOrEmpty(_mod.Version) ? "Input Bindings" : ("Input Bindings — v" + _mod.Version);

            _tooltipBus = new TooltipBus();
            _tooltipBus.DefaultMessage = TooltipMessage.Hint("Hover an action or key to see what it does. Click a key to rebind.");

            IPanelFrame frame = new FieldManualFrame(_palette, _metrics, _textures, _ui);
            PanelFrameRegions regions = frame.Build(root, title.ToUpperInvariant(), subtitle);

            BuildScrollList(regions);
            BuildTooltipStrip(regions);
            BuildFooter(regions);
            BuildContent();
        }

        private void BuildTooltipStrip(PanelFrameRegions regions)
        {
            // Sit just above the footer, on the paper.
            float y = -_metrics.PanelHeight * 0.5f + _metrics.FooterHeight + _metrics.ContentBottomPadding + 20;
            int width = (int)(regions.ContentRectLocal.width - 80);

            _tooltipDisplay = new TooltipDisplayWidget(_palette, _ui);
            _tooltipDisplay.Build(regions.Root, new Vector3(0, y, 0), width, _tooltipBus);
        }

        private void BuildScrollList(PanelFrameRegions regions)
        {
            // The scroll list lives in a centered coord space inside ContentRoot, which itself
            // is positioned at the paper center. So the viewport rect is centered around (0,0)
            // with width/height taken from the frame's content rect.
            Rect viewport = new Rect(
                -regions.ContentRectLocal.width * 0.5f,
                -regions.ContentRectLocal.height * 0.5f,
                regions.ContentRectLocal.width,
                regions.ContentRectLocal.height);

            _scrollList = new PaperScrollList(viewport, _ui.NextDepth());
            _scrollList.Build(regions.ContentRoot);
        }

        private void BuildFooter(PanelFrameRegions regions)
        {
            int btnW = 200;
            int btnH = 44;
            float rightX = _metrics.PanelWidth * 0.5f - _metrics.ContentSidePadding - btnW * 0.5f;
            float leftX = -(_metrics.PanelWidth * 0.5f - _metrics.ContentSidePadding - 140 * 0.5f);

            // Save & Close (filled olive button)
            int btnDepth = _ui.NextDepth();
            UITexture saveBg = _ui.CreateQuad(regions.FooterRoot, "SaveBg", _textures.OliveBand(btnW, btnH),
                new Vector3(rightX, 0, 0), btnW, btnH, Color.white, btnDepth);
            UILabel saveLabel = _ui.CreateLabel(regions.FooterRoot, "SaveLabel", "FILE & CLOSE",
                new Vector3(rightX, 0, 0), 16, _palette.Paper,
                btnW, btnH, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            _ui.AddClickCollider(saveBg.gameObject, btnW, btnH, Close);

            // Defaults (text link)
            int defDepth = _ui.NextDepth();
            UILabel defaults = _ui.CreateLabel(regions.FooterRoot, "Defaults", "↺  Restore Defaults",
                new Vector3(leftX, 0, 0), 14, _palette.InkFaded,
                240, 32, NGUIText.Alignment.Left, UIWidget.Pivot.Left, defDepth);
            _ui.AddClickCollider(defaults.gameObject, 220, 28, ResetAllDefaults);
        }

        private void BuildContent()
        {
            ISettingsProvider provider = _mod.SettingsProvider;
            object settings = provider.GetSettingsObject();
            List<SettingDefinition> allDefs = provider.GetSettings().ToList();
            List<SettingDefinition> displayDefs = allDefs.Where(IsKeybindPanelItem).ToList();
            List<SettingDefinition> visible = displayDefs.Where(IsVisible).ToList();

            bool pairKeybinds = ModSettingsKeybindLayout.ShouldUseWideKeybindLayout(visible, displayDefs);
            List<ModSettingsKeybindDisplayEntry> entries = ModSettingsKeybindLayout.BuildDisplayEntries(visible, displayDefs, pairKeybinds);

            var rowFactory = new KeybindRowWidget(_palette, _metrics, _textures, _ui, _tooltipBus, provider, settings, ApplyValue, OnValueChanged);
            var stampFactory = new SectionStampWidget(_palette, _metrics, _ui);

            for (int i = 0; i < entries.Count; i++)
            {
                ModSettingsKeybindDisplayEntry entry = entries[i];
                if (entry == null || entry.Primary == null) continue;

                if (ModSettingsKeybindLayout.IsSectionHeaderEntry(entry))
                {
                    GameObject stamp = stampFactory.Build(_scrollList.ContentRoot, entry.Primary.Label);
                    _scrollList.AddRow(stamp, _metrics.SectionStampHeight);
                }
                else
                {
                    GameObject row = rowFactory.Build(_scrollList.ContentRoot, entry);
                    _scrollList.AddRow(row, _metrics.RowHeight);
                }
            }

            _scrollList.Layout(_metrics.RowSpacing);
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
            // Persist eagerly — keeps parity with ModSettingsPanel which saves on close;
            // doing it on each change makes the panel resilient to scene changes.
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
            _scrollList.Clear();
            BuildContent();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (KeybindCaptureListener.ShouldBlockEscapeClose()) return;
                Close();
            }
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
