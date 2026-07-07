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
        private readonly Dictionary<int, Texture2D> _grainTextures = new Dictionary<int, Texture2D>();
        private Texture2D _verticalSheen;

        /// <summary>Tile size (px) of the paper-grain textures produced by <see cref="GetGrain"/>.</summary>
        public const int GrainTileSize = 64;

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
        /// Returns a shared vertical-sheen overlay: a warm-white highlight that
        /// fades out toward the middle and a soft shadow that deepens toward the
        /// bottom, over a transparent core. Drawn stretched over a surface it
        /// gives the flat parchment a subtle top-lit, bottom-shaded value
        /// gradient without smearing (the overlay is a 4x64 straight-alpha strip
        /// resampled bilinearly, so it maps cleanly to any surface height).
        /// </summary>
        public Texture2D GetVerticalSheen()
        {
            if (_verticalSheen != null)
                return _verticalSheen;

            const int width = 4;
            const int height = 64;
            const float topHighlight = 0.06f;
            const float bottomShade = 0.09f;
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                // Texture row 0 is the bottom of the drawn image in Unity's GUI,
                // so t=1 is the top of the surface and t=0 the bottom.
                float t = height > 1 ? (float)y / (height - 1) : 1f;
                float highlight = Mathf.Clamp01((t - 0.58f) / 0.42f) * topHighlight;
                float shade = Mathf.Clamp01((0.42f - t) / 0.42f) * bottomShade;
                Color color = highlight >= shade
                    ? new Color(1f, 1f, 1f, highlight)
                    : new Color(0f, 0f, 0f, shade);
                for (int x = 0; x < width; x++)
                    pixels[(y * width) + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            _verticalSheen = texture;
            return texture;
        }

        /// <summary>
        /// Returns a deterministic tiled paper-grain overlay for the given
        /// <paramref name="seed"/>. Each pixel is a light or dark fleck whose
        /// alpha is the absolute noise value (0..1); callers scale the overall
        /// strength via <c>GUI.color.a</c>. Two octaves of seamless value noise
        /// give a soft paper tooth rather than harsh static. The texture wraps,
        /// so it tiles at native pixel density via <c>DrawTextureWithTexCoords</c>.
        /// </summary>
        public Texture2D GetGrain(int seed)
        {
            Texture2D texture;
            if (_grainTextures.TryGetValue(seed, out texture) && texture != null)
                return texture;

            const int size = GrainTileSize;
            texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Repeat;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = (ValueNoise(x, y, size, 8, seed) * 0.65f)
                        + (ValueNoise(x, y, size, 16, (seed * 31) + 7) * 0.35f);
                    noise = Mathf.Clamp(noise, -1f, 1f);
                    float alpha = Mathf.Abs(noise);
                    pixels[(y * size) + x] = noise >= 0f
                        ? new Color(1f, 1f, 1f, alpha)
                        : new Color(0f, 0f, 0f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            _grainTextures[seed] = texture;
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
            foreach (KeyValuePair<int, Texture2D> entry in _grainTextures)
            {
                if (entry.Value != null)
                    Object.Destroy(entry.Value);
            }
            _grainTextures.Clear();
            if (_verticalSheen != null)
            {
                Object.Destroy(_verticalSheen);
                _verticalSheen = null;
            }
        }

        // Seamless 2D value noise in [-1, 1]. The lattice wraps at frequency
        // <paramref name="freq"/> so the tile edges match and the grain repeats
        // without a visible seam.
        private static float ValueNoise(int x, int y, int size, int freq, int seed)
        {
            float fx = (float)x / size * freq;
            float fy = (float)y / size * freq;
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;
            tx = tx * tx * (3f - (2f * tx));
            ty = ty * ty * (3f - (2f * ty));

            float v00 = LatticeValue(x0, y0, freq, seed);
            float v10 = LatticeValue(x0 + 1, y0, freq, seed);
            float v01 = LatticeValue(x0, y0 + 1, freq, seed);
            float v11 = LatticeValue(x0 + 1, y0 + 1, freq, seed);
            float top = Mathf.Lerp(v00, v10, tx);
            float bottom = Mathf.Lerp(v01, v11, tx);
            return Mathf.Lerp(top, bottom, ty);
        }

        private static float LatticeValue(int x, int y, int freq, int seed)
        {
            int xi = (((x % freq) + freq) % freq);
            int yi = (((y % freq) + freq) % freq);
            unchecked
            {
                int h = seed;
                h = (h * 73856093) ^ (xi * 19349663) ^ (yi * 83492791);
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return ((h & 0xFFFF) / 32767.5f) - 1f;
            }
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
