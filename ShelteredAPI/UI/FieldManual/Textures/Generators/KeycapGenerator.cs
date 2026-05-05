using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;


using ShelteredAPI.UI.FieldManual.Textures;
namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders a Sheltered-style book button slot: mostly square, worn brown fill,
    /// dark rim, and light top edge. State controls the face and rim colors.
    /// </summary>
    internal static class KeycapGenerator
    {
        private const int CornerRadius = 2;
        private const int BevelLightThickness = 2;
        private const int BevelDarkThickness = 2;

        public static Texture2D Generate(int width, int height, IThemePalette palette, KeycapState state)
        {
            var canvas = new TextureCanvas(width, height);

            Color face;
            Color rim;
            switch (state)
            {
                case KeycapState.Hover:
                    face = Lighten(palette.KeycapFace, 0.04f);
                    rim = palette.Brass;
                    break;
                case KeycapState.Pulse:
                    face = palette.KeycapPulse;
                    rim = palette.StampRed;
                    break;
                case KeycapState.Empty:
                    face = new Color(palette.KeycapFace.r, palette.KeycapFace.g, palette.KeycapFace.b, 0.55f);
                    rim = palette.InkFaded;
                    break;
                default:
                    face = palette.KeycapFace;
                    rim = palette.KeycapBevelDark;
                    break;
            }

            FillRoundedRect(canvas, 0, 0, width, height, CornerRadius, face);
            AddWornNoise(canvas, width, height, palette);

            // Top highlight bevel
            for (int t = 0; t < BevelLightThickness; t++)
                StrokeRoundedRectTop(canvas, t, height - 1 - t, width - 2 * t,
                    new Color(palette.KeycapBevelLight.r, palette.KeycapBevelLight.g, palette.KeycapBevelLight.b, 0.85f - t * 0.25f),
                    CornerRadius - t);

            // Bottom shadow bevel
            for (int t = 0; t < BevelDarkThickness; t++)
                StrokeRoundedRectBottom(canvas, t, t, width - 2 * t,
                    new Color(rim.r, rim.g, rim.b, 0.85f - t * 0.25f),
                    CornerRadius - t);

            // 1px rim around the whole shape
            StrokeRoundedRect(canvas, 0, 0, width, height, CornerRadius, new Color(rim.r, rim.g, rim.b, 0.9f));

            return canvas.ToTexture(FilterMode.Bilinear);
        }

        private static void AddWornNoise(TextureCanvas canvas, int width, int height, IThemePalette palette)
        {
            var rng = new System.Random(unchecked(width * 397 ^ height * 541));
            Color light = new Color(palette.KeycapBevelLight.r, palette.KeycapBevelLight.g, palette.KeycapBevelLight.b, 0.06f);
            Color dark = new Color(palette.KeycapBevelDark.r, palette.KeycapBevelDark.g, palette.KeycapBevelDark.b, 0.08f);
            for (int y = 2; y < height - 2; y++)
            {
                for (int x = 2; x < width - 2; x++)
                {
                    double roll = rng.NextDouble();
                    if (roll < 0.035)
                        canvas.BlendPixel(x, y, light);
                    else if (roll > 0.965)
                        canvas.BlendPixel(x, y, dark);
                }
            }
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        private static void FillRoundedRect(TextureCanvas canvas, int x, int y, int w, int h, int r, Color color)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) / 2);
            for (int py = y; py < y + h; py++)
            {
                for (int px = x; px < x + w; px++)
                {
                    int dx = 0;
                    int dy = 0;
                    if (px < x + r) dx = (x + r) - px;
                    else if (px >= x + w - r) dx = px - (x + w - 1 - r);
                    if (py < y + r) dy = (y + r) - py;
                    else if (py >= y + h - r) dy = py - (y + h - 1 - r);

                    if (dx == 0 && dy == 0)
                    {
                        canvas.SetPixel(px, py, color);
                        continue;
                    }
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= r)
                    {
                        float edge = Mathf.Clamp01(r - dist + 0.5f);
                        canvas.BlendPixel(px, py, new Color(color.r, color.g, color.b, color.a * edge));
                    }
                }
            }
        }

        private static void StrokeRoundedRect(TextureCanvas canvas, int x, int y, int w, int h, int r, Color color)
        {
            r = Mathf.Min(r, Mathf.Min(w, h) / 2);
            for (int py = y; py < y + h; py++)
            {
                for (int px = x; px < x + w; px++)
                {
                    int dx = 0;
                    int dy = 0;
                    if (px < x + r) dx = (x + r) - px;
                    else if (px >= x + w - r) dx = px - (x + w - 1 - r);
                    if (py < y + r) dy = (y + r) - py;
                    else if (py >= y + h - r) dy = py - (y + h - 1 - r);

                    bool onEdge =
                        px == x || px == x + w - 1 || py == y || py == y + h - 1
                        || (dx > 0 && dy > 0 && Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r) < 1.0f);

                    if (onEdge)
                        canvas.BlendPixel(px, py, color);
                }
            }
        }

        private static void StrokeRoundedRectTop(TextureCanvas canvas, int x, int yTop, int w, Color color, int r)
        {
            if (r < 0) r = 0;
            for (int px = x + r; px < x + w - r; px++)
                canvas.BlendPixel(px, yTop, color);
        }

        private static void StrokeRoundedRectBottom(TextureCanvas canvas, int x, int yBot, int w, Color color, int r)
        {
            if (r < 0) r = 0;
            for (int px = x + r; px < x + w - r; px++)
                canvas.BlendPixel(px, yBot, color);
        }
    }
}
