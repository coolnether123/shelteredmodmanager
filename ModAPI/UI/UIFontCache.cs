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
        private static UIFont _preferredBitmapFont;

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

        public static void RefreshIfMissing()
        {
            if (!_initialized)
            {
                Initialize();
                return;
            }

            if (_cachedBitmapFont != null && _cachedTTFFont != null) return;

            _initialized = false;
            Initialize();
        }

        public static void SeedFromGameObject(GameObject root, string reason)
        {
            if (root == null) return;
            try
            {
                var label = root.GetComponentsInChildren<UILabel>(true)
                    .FirstOrDefault(l => l != null && l.bitmapFont != null);
                if (label == null || label.bitmapFont == null) return;

                _preferredBitmapFont = label.bitmapFont;
                _cachedBitmapFont = label.bitmapFont;
                MMLog.WriteInfo("[UIFontCache] Seeded bitmap font from " + reason + ": " + label.bitmapFont.name);
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
                UILabel sampleLabel = null;

                if (_preferredBitmapFont != null)
                {
                    _cachedBitmapFont = _preferredBitmapFont;
                }

                // Prefer a live in-scene label with bitmap font.
                if (_cachedBitmapFont == null)
                {
                    sampleLabel = Resources.FindObjectsOfTypeAll<UILabel>()
                        .FirstOrDefault(l => l != null && l.gameObject != null && l.gameObject.activeInHierarchy && l.bitmapFont != null);
                    if (sampleLabel != null)
                    {
                        _cachedBitmapFont = sampleLabel.bitmapFont;
                    }
                }

                // Fallback to any loaded label (active or inactive).
                if (_cachedBitmapFont == null)
                {
                    sampleLabel = Resources.FindObjectsOfTypeAll<UILabel>()
                        .FirstOrDefault(l => l != null && l.bitmapFont != null);
                    if (sampleLabel != null)
                    {
                        _cachedBitmapFont = sampleLabel.bitmapFont;
                    }
                }

                // Final fallback: loaded UIFont assets.
                if (_cachedBitmapFont == null)
                {
                    _cachedBitmapFont = Resources.FindObjectsOfTypeAll<UIFont>()
                        .FirstOrDefault(f => f != null);
                }

                // Try to find a TTF font from active labels first.
                var ttfSample = Resources.FindObjectsOfTypeAll<UILabel>()
                    .FirstOrDefault(l => l != null && l.gameObject != null && l.gameObject.activeInHierarchy && l.trueTypeFont != null);

                if (ttfSample == null)
                {
                    ttfSample = Resources.FindObjectsOfTypeAll<UILabel>()
                        .FirstOrDefault(l => l != null && l.trueTypeFont != null);
                }

                if (ttfSample != null && ttfSample.trueTypeFont != null)
                {
                    _cachedTTFFont = ttfSample.trueTypeFont;
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
