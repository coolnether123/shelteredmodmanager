using UnityEngine;
using ShelteredAPI.UI.FieldManual.Theme;

namespace ShelteredAPI.UI.FieldManual.Textures.Generators
{
    /// <summary>
    /// Renders a small brass rivet: filled circle with a top highlight and bottom shadow.
    /// </summary>
    internal static class RivetGenerator
    {
        public static Texture2D Generate(int diameter, IThemePalette palette)
        {
            int size = Mathf.Max(4, diameter);
            var canvas = new TextureCanvas(size, size);
            float r = size * 0.5f - 0.5f;
            float cx = size * 0.5f;
            float cy = size * 0.5f;

            canvas.FillCircle(cx, cy, r, palette.Brass);

            // Inner shadow ring (lower half)
            canvas.FillCircle(cx, cy - 0.5f, r - 1.0f,
                new Color(palette.GunmetalShadow.r, palette.GunmetalShadow.g, palette.GunmetalShadow.b, 0.45f));

            // Highlight (upper-left)
            canvas.FillCircle(cx - r * 0.3f, cy + r * 0.3f, r * 0.35f,
                new Color(1f, 0.95f, 0.7f, 0.55f));

            return canvas.ToTexture(FilterMode.Bilinear);
        }
    }
}
