using System;
using System.Linq;
using UnityEngine;
using ModAPI.Core;
namespace ShelteredAPI.UI.Compatibility
{
    /// <summary>
    /// Caches fonts to prevent expensive repeated lookups during UI construction.
    /// </summary>
    internal static class UIFontCache
    {
        private static UIFont _cachedBitmapFont;
        private static Font _cachedTTFFont;
        private static bool _initialized = false;
        private static UIFont _preferredBitmapFont;

        internal struct FontResult
        {
            public UIFont Bitmap;
            public Font TTF;
        }

        public static FontResult GetFonts()
        {
            if (!_initialized)
            {
                Initialize();
            }
            return new FontResult { Bitmap = _cachedBitmapFont, TTF = _cachedTTFFont };
        }

        public static void RefreshIfMissing()
        {
            // Bitmap fonts are optional for IMGUI. Treating a missing bitmap
            // font as an invalid cache caused every style lookup to repeat a
            // global Unity resource scan from OnGUI.
            if (!_initialized || _cachedTTFFont == null)
                Initialize();
        }

        public static void SeedFromGameObject(GameObject root, string reason)
        {
            if (root == null) return;
            try
            {
                UILabel[] labels = root.GetComponentsInChildren<UILabel>(true);
                var bitmapLabel = labels
                    .FirstOrDefault(l => l != null && l.bitmapFont != null);
                var ttfLabel = labels
                    .FirstOrDefault(l => l != null && l.trueTypeFont != null);

                if (bitmapLabel != null)
                {
                    _preferredBitmapFont = bitmapLabel.bitmapFont;
                    _cachedBitmapFont = bitmapLabel.bitmapFont;
                }
                if (ttfLabel != null)
                    _cachedTTFFont = ttfLabel.trueTypeFont;

                // Seed while the scene hierarchy is stable, outside OnGUI.
                // Initialize supplies the built-in TTF fallback when the
                // source hierarchy contains bitmap labels only.
                _initialized = false;
                Initialize();
                MMLog.WriteInfo("[UIFontCache] Seeded fonts from " + reason
                    + ". Bitmap: " + (_cachedBitmapFont ? _cachedBitmapFont.name : "null")
                    + ", TTF: " + (_cachedTTFFont ? _cachedTTFFont.name : "null"));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[UIFontCache] Failed to seed from " + reason + ": " + ex.Message);
            }
        }

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                if (_preferredBitmapFont != null)
                    _cachedBitmapFont = _preferredBitmapFont;

                // Never enumerate Unity's global object registry here. This
                // method can be reached while IMGUI is rendering, and Unity
                // 5.3/Mono can crash natively while wrapping objects returned
                // by FindObjectsOfTypeAll during that phase.
                if (_cachedTTFFont == null)
                    _cachedTTFFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

                _initialized = true;
                MMLog.Write($"[UIFontCache] Initialized. Bitmap: {(_cachedBitmapFont ? _cachedBitmapFont.name : "null")}, TTF: {(_cachedTTFFont ? _cachedTTFFont.name : "null")}");
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[UIFontCache] Initialization failed: {ex.Message}");
            }
        }
    }
}
