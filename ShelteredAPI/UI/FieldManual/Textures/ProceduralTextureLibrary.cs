using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;
using ShelteredAPI.UI.FieldManual.Textures.Generators;

namespace ShelteredAPI.UI.FieldManual.Textures
{
    internal enum BookChromeTexture
    {
        Vignette = 0,
        White = 1,
        Gunmetal = 2,
        Paper = 3,
        Rivet = 4
    }

    /// <summary>
    /// ITextureLibrary implementation that delegates to the per-kind generators and
    /// caches results for the process lifetime keyed by generator inputs and palette.
    /// Instances are lightweight views over the shared cache and do not own textures.
    /// </summary>
    internal sealed class ProceduralTextureLibrary : ITextureLibrary
    {
        internal const int BookChromeTextureCount = 5;

        private readonly IThemePalette _palette;
        private readonly string _paletteFingerprint;
        private static readonly Dictionary<string, Texture2D> SharedCache = new Dictionary<string, Texture2D>();

        public ProceduralTextureLibrary(IThemePalette palette)
        {
            _palette = palette;
            _paletteFingerprint = BuildPaletteFingerprint(palette);
        }

        public Texture2D White
        {
            get
            {
                return GetOrCreate("white:2x2", delegate
                {
                    Texture2D white = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    Color[] px = new Color[4];
                    for (int i = 0; i < 4; i++) px[i] = Color.white;
                    white.SetPixels(px);
                    white.filterMode = FilterMode.Point;
                    white.wrapMode = TextureWrapMode.Clamp;
                    white.Apply(false, false);
                    return white;
                });
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

        internal Texture2D GetBookChromeTexture(BookChromeTexture texture)
        {
            switch (texture)
            {
                case BookChromeTexture.Vignette:
                    return Vignette(512, 512);
                case BookChromeTexture.White:
                    return White;
                case BookChromeTexture.Gunmetal:
                    return Gunmetal(1240, 720);
                case BookChromeTexture.Paper:
                    return Paper(560, 660);
                case BookChromeTexture.Rivet:
                    return Rivet(18);
                default:
                    throw new ArgumentOutOfRangeException("texture");
            }
        }

        /// <summary>
        /// Warms a bounded slice of the exact unique texture keys used by the book
        /// frame. Callers can pass one as maxTextures to keep generation to one
        /// main-thread texture per frame.
        /// </summary>
        internal static int PrewarmAll(IThemePalette palette, int nextTextureIndex, int maxTextures)
        {
            if (palette == null) throw new ArgumentNullException("palette");
            if (nextTextureIndex < 0) throw new ArgumentOutOfRangeException("nextTextureIndex");
            if (maxTextures < 0) throw new ArgumentOutOfRangeException("maxTextures");

            ProceduralTextureLibrary textures = new ProceduralTextureLibrary(palette);
            int warmed = 0;
            while (nextTextureIndex < BookChromeTextureCount && warmed < maxTextures)
            {
                textures.GetBookChromeTexture((BookChromeTexture)nextTextureIndex);
                nextTextureIndex++;
                warmed++;
            }

            return nextTextureIndex;
        }

        public void Dispose()
        {
            // Shared textures intentionally live for the process lifetime. Unity may
            // still destroy one externally; GetOrCreate detects fake-null and repairs it.
        }

        private delegate Texture2D Factory();

        private Texture2D GetOrCreate(string key, Factory factory)
        {
            string sharedKey = _paletteFingerprint + ":" + key;
            Texture2D tex;
            if (SharedCache.TryGetValue(sharedKey, out tex) && tex != null) return tex;
            tex = factory();
            tex.hideFlags = HideFlags.HideAndDontSave;
            SharedCache[sharedKey] = tex;
            return tex;
        }

        private static string BuildPaletteFingerprint(IThemePalette palette)
        {
            StringBuilder fingerprint = new StringBuilder(640);
            AppendColor(fingerprint, palette.Gunmetal);
            AppendColor(fingerprint, palette.GunmetalShadow);
            AppendColor(fingerprint, palette.GunmetalHighlight);
            AppendColor(fingerprint, palette.OliveBand);
            AppendColor(fingerprint, palette.Brass);
            AppendColor(fingerprint, palette.Paper);
            AppendColor(fingerprint, palette.PaperShadow);
            AppendColor(fingerprint, palette.PaperGrain);
            AppendColor(fingerprint, palette.Ink);
            AppendColor(fingerprint, palette.InkFaded);
            AppendColor(fingerprint, palette.StampRed);
            AppendColor(fingerprint, palette.GraphitePencil);
            AppendColor(fingerprint, palette.KeycapFace);
            AppendColor(fingerprint, palette.KeycapBevelLight);
            AppendColor(fingerprint, palette.KeycapBevelDark);
            AppendColor(fingerprint, palette.KeycapInk);
            AppendColor(fingerprint, palette.KeycapPulse);
            AppendColor(fingerprint, palette.MaskingTape);
            AppendColor(fingerprint, palette.Vignette);
            return fingerprint.ToString();
        }

        private static void AppendColor(StringBuilder fingerprint, Color color)
        {
            AppendFloat(fingerprint, color.r);
            AppendFloat(fingerprint, color.g);
            AppendFloat(fingerprint, color.b);
            AppendFloat(fingerprint, color.a);
        }

        private static void AppendFloat(StringBuilder fingerprint, float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < bytes.Length; i++)
                fingerprint.Append(bytes[i].ToString("X2"));
        }
    }
}
