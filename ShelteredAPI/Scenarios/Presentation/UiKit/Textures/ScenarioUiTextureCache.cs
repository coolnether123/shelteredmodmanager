using System.Collections.Generic;
using UnityEngine;
namespace ShelteredAPI.Scenarios.Presentation.UiKit.Textures{
    /// <summary>
    /// Single source of 1x1 flat-colour <see cref="Texture2D"/>s for IMGUI
    /// backgrounds. Replaces the per-renderer <c>MakeTexture</c> helpers that
    /// otherwise allocate duplicate textures across render modules. Textures
    /// are flagged <see cref="HideFlags.HideAndDontSave"/> so they survive
    /// scene loads and never appear in saved data.
    /// </summary>
    internal sealed class ScenarioUiTextureCache
    {
        private readonly Dictionary<int, Texture2D> _textures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, Texture2D> _cornerCutTextures = new Dictionary<int, Texture2D>();

        /// <summary>Returns a 1x1 texture filled with <paramref name="color"/>.</summary>
        public Texture2D Get(Color color)
        {
            int key = ColorKey(color);
            Texture2D texture;
            if (_textures.TryGetValue(key, out texture) && texture != null)
                return texture;

            texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            _textures[key] = texture;
            return texture;
        }

        /// <summary>Returns a flat-colour texture with transparent chamfered corner cuts.</summary>
        public Texture2D GetCornerCut(Color color)
        {
            int key = ColorKey(color);
            Texture2D texture;
            if (_cornerCutTextures.TryGetValue(key, out texture) && texture != null)
                return texture;

            int size = ScenarioUiAtlasSkin.CornerTextureSize;
            int cut = ScenarioUiAtlasSkin.CornerRadiusPixels;
            texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool cornerCut =
                        IsChamferCornerPixel(x, y, cut)
                        || IsChamferCornerPixel(size - x - 1, y, cut)
                        || IsChamferCornerPixel(x, size - y - 1, cut)
                        || IsChamferCornerPixel(size - x - 1, size - y - 1, cut);
                    texture.SetPixel(x, y, cornerCut ? new Color(0f, 0f, 0f, 0f) : color);
                }
            }
            texture.Apply();
            _cornerCutTextures[key] = texture;
            return texture;
        }

        /// <summary>
        /// Releases every cached texture. Call from the owner's teardown so
        /// the cache does not hold references through scene transitions.
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<int, Texture2D> entry in _textures)
            {
                if (entry.Value != null)
                    Object.Destroy(entry.Value);
            }
            _textures.Clear();
            foreach (KeyValuePair<int, Texture2D> entry in _cornerCutTextures)
            {
                if (entry.Value != null)
                    Object.Destroy(entry.Value);
            }
            _cornerCutTextures.Clear();
        }

        private static int ColorKey(Color color)
        {
            // Quantise to 0-255 channels so near-duplicate floats collapse to
            // the same texture entry.
            int r = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            int a = Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        private static bool IsChamferCornerPixel(int x, int y, int cut)
        {
            return x < cut && y < cut && x + y < cut;
        }
    }
}
