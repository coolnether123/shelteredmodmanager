using System.Collections.Generic;
using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Textures
{
    internal enum MaterialSurfaceTier
    {
        Flat = 0,
        Page = 1,
        RaisedCard = 2,
        RecessedInset = 3
    }

    /// <summary>
    /// Canonical process-lifetime material generator for editor IMGUI surfaces.
    /// Material fills are forced opaque; only the chamfered corner pixels are transparent.
    /// </summary>
    internal static class ProceduralTextureLibrary
    {
        private static readonly Dictionary<string, Texture2D> MaterialSurfaces =
            new Dictionary<string, Texture2D>();

        internal static Texture2D MaterialSurface(
            MaterialSurfaceTier tier,
            int width,
            int height,
            int cornerCut,
            Color fill)
        {
            Color32 opaque = new Color(fill.r, fill.g, fill.b, 1f);
            string key = (int)tier + ":" + width + "x" + height + ":" + cornerCut + ":"
                + opaque.r + ":" + opaque.g + ":" + opaque.b + ":255";
            Texture2D texture;
            if (MaterialSurfaces.TryGetValue(key, out texture) && texture != null)
                return texture;

            texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool cut = cornerCut > 0 && (
                        x < cornerCut && y < cornerCut && x + y < cornerCut
                        || width - x - 1 < cornerCut && y < cornerCut && width - x - 1 + y < cornerCut
                        || x < cornerCut && height - y - 1 < cornerCut && x + height - y - 1 < cornerCut
                        || width - x - 1 < cornerCut && height - y - 1 < cornerCut
                            && width - x - 1 + height - y - 1 < cornerCut);
                    pixels[(y * width) + x] = cut ? transparent : opaque;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            MaterialSurfaces[key] = texture;
            return texture;
        }
    }
}
