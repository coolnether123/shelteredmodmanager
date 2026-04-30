using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders a strip of masking tape: semi-transparent off-white with torn edges
    /// (small noise on the left/right vertical borders).
    /// </summary>
    internal static class MaskingTapeGenerator
    {
        private const int TornEdgeJitter = 3;

        public static Texture2D Generate(int width, int height, IThemePalette palette)
        {
            var canvas = new TextureCanvas(width, height);
            var rng = new System.Random(unchecked((int)0xFEEDFACE));

            // Build per-row left/right offsets so the edges look hand-torn.
            int[] leftOffsets = new int[height];
            int[] rightOffsets = new int[height];
            for (int y = 0; y < height; y++)
            {
                leftOffsets[y] = rng.Next(0, TornEdgeJitter + 1);
                rightOffsets[y] = rng.Next(0, TornEdgeJitter + 1);
            }

            Color tape = palette.MaskingTape;
            Color tapeShade = new Color(tape.r * 0.92f, tape.g * 0.92f, tape.b * 0.92f, tape.a);

            for (int y = 0; y < height; y++)
            {
                int x0 = leftOffsets[y];
                int x1 = width - rightOffsets[y];
                bool shadeRow = (y % 4) == 0;
                for (int x = x0; x < x1; x++)
                    canvas.SetPixel(x, y, shadeRow ? tapeShade : tape);
            }

            // Subtle drop shadow underneath the tape (one row of soft dark below).
            for (int x = 0; x < width; x++)
                canvas.BlendPixel(x, 0, new Color(0f, 0f, 0f, 0.10f));

            return canvas.ToTexture(FilterMode.Bilinear);
        }
    }
}
