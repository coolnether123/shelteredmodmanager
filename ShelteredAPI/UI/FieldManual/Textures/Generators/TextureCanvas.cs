using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Mutable 8-bit pixel buffer with helpers for primitive drawing. Keeps generator code
    /// declarative — generators describe pixels, not Texture2D plumbing.
    /// </summary>
    internal sealed class TextureCanvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;

        public TextureCanvas(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            Pixels = new Color32[Width * Height];
        }

        public void Fill(Color color)
        {
            for (int i = 0; i < Pixels.Length; i++) Pixels[i] = color;
        }

        public void SetPixel(int x, int y, Color color)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            Pixels[y * Width + x] = color;
        }

        public Color GetPixel(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return Color.clear;
            return Pixels[y * Width + x];
        }

        public void BlendPixel(int x, int y, Color src)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            int idx = y * Width + x;
            Color dst = Pixels[idx];
            float a = src.a;
            float ia = 1f - a;
            Pixels[idx] = new Color(
                src.r * a + dst.r * ia,
                src.g * a + dst.g * ia,
                src.b * a + dst.b * ia,
                Mathf.Clamp01(dst.a + a * (1f - dst.a)));
        }

        public void FillRect(int x0, int y0, int w, int h, Color color)
        {
            int x1 = Mathf.Min(Width, x0 + w);
            int y1 = Mathf.Min(Height, y0 + h);
            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    Pixels[y * Width + x] = color;
        }

        public void BlendRect(int x0, int y0, int w, int h, Color color)
        {
            int x1 = Mathf.Min(Width, x0 + w);
            int y1 = Mathf.Min(Height, y0 + h);
            x0 = Mathf.Max(0, x0);
            y0 = Mathf.Max(0, y0);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    BlendPixel(x, y, color);
        }

        public void FillCircle(float cx, float cy, float radius, Color color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(cx + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(cy + radius));
            float r2 = radius * radius;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 <= r2)
                    {
                        // Antialias the outer ~1px ring for a softer edge.
                        float edge = Mathf.Clamp01(radius - Mathf.Sqrt(d2));
                        if (edge >= 1f)
                            SetPixel(x, y, color);
                        else
                            BlendPixel(x, y, new Color(color.r, color.g, color.b, color.a * edge));
                    }
                }
            }
        }

        public Texture2D ToTexture(FilterMode filter)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.ARGB32, false);
            tex.filterMode = filter;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(Pixels);
            // Procedural chrome is immutable after creation. Discarding Unity's
            // readable CPU copy keeps the shared process-lifetime cache GPU-only.
            tex.Apply(false, true);
            return tex;
        }
    }
}
