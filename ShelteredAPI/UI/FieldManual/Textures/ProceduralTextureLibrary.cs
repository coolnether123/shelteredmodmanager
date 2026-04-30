using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Textures.Generators;

namespace ShelteredAPI.UI.FieldManual.Textures
{
    /// <summary>
    /// ITextureLibrary implementation that delegates to the per-kind generators and
    /// caches results keyed by (kind, width, height, state). All textures are owned
    /// by this instance; <see cref="Dispose"/> destroys them.
    /// </summary>
    internal sealed class ProceduralTextureLibrary : ITextureLibrary
    {
        private readonly IThemePalette _palette;
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private Texture2D _white;

        public ProceduralTextureLibrary(IThemePalette palette)
        {
            _palette = palette;
        }

        public Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    Color[] px = new Color[4];
                    for (int i = 0; i < 4; i++) px[i] = Color.white;
                    _white.SetPixels(px);
                    _white.filterMode = FilterMode.Point;
                    _white.wrapMode = TextureWrapMode.Clamp;
                    _white.Apply(false, false);
                }
                return _white;
            }
        }

        public Texture2D Gunmetal(int width, int height)
        {
            return GetOrCreate("gun:" + width + "x" + height,
                delegate { return GunmetalGenerator.Generate(width, height, _palette); });
        }

        public Texture2D Paper(int width, int height)
        {
            return GetOrCreate("paper:" + width + "x" + height,
                delegate { return PaperGenerator.Generate(width, height, _palette); });
        }

        public Texture2D Rivet(int diameter)
        {
            return GetOrCreate("rivet:" + diameter,
                delegate { return RivetGenerator.Generate(diameter, _palette); });
        }

        public Texture2D Keycap(int width, int height, KeycapState state)
        {
            return GetOrCreate("keycap:" + width + "x" + height + ":" + (int)state,
                delegate { return KeycapGenerator.Generate(width, height, _palette, state); });
        }

        public Texture2D MaskingTape(int width, int height)
        {
            return GetOrCreate("tape:" + width + "x" + height,
                delegate { return MaskingTapeGenerator.Generate(width, height, _palette); });
        }

        public Texture2D OliveBand(int width, int height)
        {
            return GetOrCreate("olive:" + width + "x" + height,
                delegate { return OliveBandGenerator.Generate(width, height, _palette); });
        }

        public Texture2D Vignette(int width, int height)
        {
            return GetOrCreate("vig:" + width + "x" + height,
                delegate { return VignetteGenerator.Generate(width, height, _palette); });
        }

        public void Dispose()
        {
            foreach (var kv in _cache)
            {
                if (kv.Value != null) Object.Destroy(kv.Value);
            }
            _cache.Clear();
            if (_white != null) { Object.Destroy(_white); _white = null; }
        }

        private delegate Texture2D Factory();

        private Texture2D GetOrCreate(string key, Factory factory)
        {
            Texture2D tex;
            if (_cache.TryGetValue(key, out tex) && tex != null) return tex;
            tex = factory();
            _cache[key] = tex;
            return tex;
        }
    }
}
