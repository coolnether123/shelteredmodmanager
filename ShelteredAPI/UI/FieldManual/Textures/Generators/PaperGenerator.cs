using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders a cream paper sheet with low-frequency speckle grain and softened corners.
    /// </summary>
    internal static class PaperGenerator
    {
        private const float GrainProbability = 0.04f;
        private const float GrainAlpha = 0.08f;
        private const int CornerRadius = 4;

        public static Texture2D Generate(int width, int height, IThemePalette palette)
        {
            var canvas = new TextureCanvas(width, height);
            canvas.Fill(palette.Paper);

            var rng = new System.Random(unchecked((int)0xCAFEBABE));
            Color grain = new Color(palette.PaperGrain.r, palette.PaperGrain.g, palette.PaperGrain.b, GrainAlpha);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (rng.NextDouble() < GrainProbability)
                        canvas.BlendPixel(x, y, grain);
                }
            }

            // Round corners by clearing pixels outside corner radius arcs.
            ClearOutsideCorner(canvas, CornerRadius, CornerRadius, 0, 0);
            ClearOutsideCorner(canvas, width - 1 - CornerRadius, CornerRadius, width - 1, 0);
            ClearOutsideCorner(canvas, CornerRadius, height - 1 - CornerRadius, 0, height - 1);
            ClearOutsideCorner(canvas, width - 1 - CornerRadius, height - 1 - CornerRadius, width - 1, height - 1);

            return canvas.ToTexture(FilterMode.Bilinear);
        }

        private static void ClearOutsideCorner(TextureCanvas canvas, int cx, int cy, int outerX, int outerY)
        {
            int xStart = Mathf.Min(cx, outerX);
            int xEnd = Mathf.Max(cx, outerX);
            int yStart = Mathf.Min(cy, outerY);
            int yEnd = Mathf.Max(cy, outerY);
            float r = CornerRadius;
            float r2 = r * r;
            for (int y = yStart; y <= yEnd; y++)
            {
                for (int x = xStart; x <= xEnd; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy > r2)
                        canvas.SetPixel(x, y, Color.clear);
                }
            }
        }
    }
}
