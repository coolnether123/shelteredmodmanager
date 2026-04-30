using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders a radial darkening overlay: transparent at center, opaque at corners.
    /// Used in place of a flat black backdrop so the panel sits in a warm spotlight.
    /// </summary>
    internal static class VignetteGenerator
    {
        public static Texture2D Generate(int width, int height, IThemePalette palette)
        {
            var canvas = new TextureCanvas(width, height);
            float cx = width * 0.5f;
            float cy = height * 0.5f;
            float maxR = Mathf.Sqrt(cx * cx + cy * cy);
            Color base_ = palette.Vignette;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float t = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
                    // Smoothstep so the center stays clear and only the edges darken.
                    float k = Mathf.SmoothStep(0.35f, 1.0f, t);
                    canvas.SetPixel(x, y, new Color(base_.r, base_.g, base_.b, base_.a * k));
                }
            }

            return canvas.ToTexture(FilterMode.Bilinear);
        }
    }
}
