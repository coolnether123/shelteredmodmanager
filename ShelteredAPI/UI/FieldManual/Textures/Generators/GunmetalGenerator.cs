using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders a brushed-metal panel: vertical brush noise on a gunmetal base
    /// with a 1px highlight on the top edge and a 1px shadow on the bottom edge.
    /// </summary>
    internal static class GunmetalGenerator
    {
        private const int BrushStreakSpacing = 3;
        private const float BrushStreakAlpha = 0.06f;

        public static Texture2D Generate(int width, int height, IThemePalette palette)
        {
            var canvas = new TextureCanvas(width, height);
            canvas.Fill(palette.Gunmetal);

            // Vertical brush streaks: alternating subtle dark/light columns.
            var rng = new System.Random(0xBADA55);
            for (int x = 0; x < width; x += BrushStreakSpacing)
            {
                bool dark = (rng.Next() & 1) == 0;
                Color streak = dark
                    ? new Color(palette.GunmetalShadow.r, palette.GunmetalShadow.g, palette.GunmetalShadow.b, BrushStreakAlpha)
                    : new Color(palette.GunmetalHighlight.r, palette.GunmetalHighlight.g, palette.GunmetalHighlight.b, BrushStreakAlpha);
                for (int y = 0; y < height; y++)
                    canvas.BlendPixel(x, y, streak);
            }

            // Edge bevels.
            for (int x = 0; x < width; x++)
            {
                canvas.BlendPixel(x, height - 1, new Color(palette.GunmetalHighlight.r, palette.GunmetalHighlight.g, palette.GunmetalHighlight.b, 0.6f));
                canvas.BlendPixel(x, height - 2, new Color(palette.GunmetalHighlight.r, palette.GunmetalHighlight.g, palette.GunmetalHighlight.b, 0.25f));
                canvas.BlendPixel(x, 0, new Color(palette.GunmetalShadow.r, palette.GunmetalShadow.g, palette.GunmetalShadow.b, 0.7f));
                canvas.BlendPixel(x, 1, new Color(palette.GunmetalShadow.r, palette.GunmetalShadow.g, palette.GunmetalShadow.b, 0.3f));
            }
            for (int y = 0; y < height; y++)
            {
                canvas.BlendPixel(0, y, new Color(palette.GunmetalShadow.r, palette.GunmetalShadow.g, palette.GunmetalShadow.b, 0.5f));
                canvas.BlendPixel(width - 1, y, new Color(palette.GunmetalShadow.r, palette.GunmetalShadow.g, palette.GunmetalShadow.b, 0.5f));
            }

            return canvas.ToTexture(FilterMode.Bilinear);
        }
    }
}
