using System;
using ShelteredAPI.UI.Compatibility;
using ShelteredAPI.UI.FieldManual.Frame;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Textures;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Widgets;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Panels
{
    /// <summary>
    /// Shared book-window composition used by settings-oriented Field Manual panels.
    /// Owns visual chrome and theme resources; panel classes own behavior and content.
    /// </summary>
    internal sealed class FieldManualWindowChrome : IDisposable
    {
        public readonly GameObject Root;
        public readonly IThemePalette Palette;
        public readonly IThemeMetrics Metrics;
        public readonly ProceduralTextureLibrary Textures;
        public readonly UIPrimitiveFactory Ui;
        public readonly PanelFrameRegions Regions;
        public readonly BookButtonWidget Buttons;

        private bool _disposed;

        private FieldManualWindowChrome(
            GameObject root,
            IThemePalette palette,
            IThemeMetrics metrics,
            ProceduralTextureLibrary textures,
            UIPrimitiveFactory ui,
            PanelFrameRegions regions)
        {
            Root = root;
            Palette = palette;
            Metrics = metrics;
            Textures = textures;
            Ui = ui;
            Regions = regions;
            Buttons = new BookButtonWidget(palette, textures, ui);
        }

        public static GameObject CreateOverlayRoot(string overlayName, int overlayDepth, string rootName)
        {
            UIFontCache.RefreshIfMissing();

            UIPanel overlay = UIUtil.EnsureOverlayPanel(overlayName, overlayDepth);
            GameObject root = new GameObject(rootName);
            root.transform.SetParent(overlay.transform, false);
            root.layer = overlay.gameObject.layer;
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one;
            return root;
        }

        public static FieldManualWindowChrome BuildBook(GameObject root, int overlayDepth, string title, string subtitle)
        {
            IThemePalette palette = new FieldManualPalette();
            IThemeMetrics metrics = new FieldManualMetrics();
            ProceduralTextureLibrary textures = new ProceduralTextureLibrary(palette);

            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            UIPrimitiveFactory ui = new UIPrimitiveFactory(fonts.Bitmap, fonts.TTF, overlayDepth);

            IPanelFrame frame = new ShelteredBookFrame(palette, metrics, textures, ui);
            PanelFrameRegions regions = frame.Build(root, title, subtitle);

            return new FieldManualWindowChrome(root, palette, metrics, textures, ui, regions);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (Textures != null)
                Textures.Dispose();
        }
    }
}
