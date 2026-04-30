using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders the title strip/button surface: a worn brown band with subtle horizontal weave noise
    /// and 1px dark borders top/bottom.
    /// </summary>
    internal static class OliveBandGenerator
    {
        private const float WeaveAlpha = 0.05f;

        public static Texture2D Generate(int width, int height, IThemePalette palette)
        {
            var canvas = new TextureCanvas(width, height);
            canvas.Fill(palette.OliveBand);

            var rng = new System.Random(0x0FF1CE);
            for (int y = 0; y < height; y++)
            {
                bool dark = (y & 1) == 0;
                Color streak = dark
                    ? new Color(0f, 0f, 0f, WeaveAlpha)
                    : new Color(1f, 1f, 1f, WeaveAlpha * 0.6f);
                for (int x = 0; x < width; x++)
                {
                    if (rng.NextDouble() < 0.35)
                        canvas.BlendPixel(x, y, streak);
                }
            }

            for (int x = 0; x < width; x++)
            {
                canvas.BlendPixel(x, 0, new Color(0f, 0f, 0f, 0.45f));
                canvas.BlendPixel(x, height - 1, new Color(0f, 0f, 0f, 0.30f));
            }

            return canvas.ToTexture(FilterMode.Bilinear);
        }
    }
}
