using System;
using System.Linq;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    /// <summary>
    /// Caches fonts to prevent expensive repeated lookups during UI construction.
    /// </summary>
    public static class UIFontCache
    {
        private static UIFont _cachedBitmapFont;
        private static Font _cachedTTFFont;
        private static bool _initialized = false;

        public struct FontResult
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

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Try to find a NGUI bitmap font from an active label
                var sampleLabel = Resources.FindObjectsOfTypeAll<UILabel>()
                    .FirstOrDefault(l => l.gameObject.activeInHierarchy && l.bitmapFont != null);
                
                if (sampleLabel != null)
                {
                    _cachedBitmapFont = sampleLabel.bitmapFont;
                }

                // Try to find a TTF font
                // First checks if the sample label had one
                if (sampleLabel != null && sampleLabel.trueTypeFont != null)
                {
                    _cachedTTFFont = sampleLabel.trueTypeFont;
                }
                
                // Fallback to built-in Arial if no TTF found yet
                if (_cachedTTFFont == null)
                {
                    _cachedTTFFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                
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
