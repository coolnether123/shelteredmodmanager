using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Textures
{
    /// <summary>
    /// Shared "parchment face" treatment for scenario authoring surfaces. Layers
    /// a subtle domain tint wash, a vertical value-gradient sheen, a low-alpha
    /// paper-grain overlay, and an optional 1px bevel on top of a flat surface
    /// so cards, chrome bands, and tiles read like aged book paper instead of
    /// flat generated rectangles.
    ///
    /// The overlays inset by <see cref="ChamferSafeInset"/> so they never paint
    /// into the transparent chamfered corners of the underlying corner-cut
    /// surface (the surface corner cut removes pixels where the corner distance
    /// sum is below the radius; a 2px inset keeps every overlay pixel inside the
    /// filled region for the shared 4px radius). Every texture is produced once
    /// by <see cref="ScenarioUiTextureCache"/> and cached, so a full pass is a
    /// handful of <c>GUI.DrawTexture</c> calls with zero per-frame allocation.
    /// </summary>
    internal static class ScenarioUiParchment
    {
        /// <summary>Inset applied to overlays so they clear the surface's chamfered corners.</summary>
        public const float ChamferSafeInset = 2f;

        /// <summary>
        /// Paints the parchment treatment over <paramref name="rect"/>. Call
        /// after the surface's flat background is drawn and before its text and
        /// icons, so the treatment sits under the content.
        /// </summary>
        /// <param name="rect">The full surface rect (bevel follows its chamfer).</param>
        /// <param name="cache">The live texture cache that owns the overlays.</param>
        /// <param name="tintWash">A low-alpha domain wash; pass a zero-alpha colour to skip.</param>
        /// <param name="grainSeed">Deterministic seed selecting the paper-grain tile.</param>
        /// <param name="grainStrength">Overall grain alpha (0 skips the grain).</param>
        /// <param name="sheenStrength">Overall sheen alpha multiplier (0 skips the sheen).</param>
        /// <param name="bevelLight">Top/left highlight texture, or null to skip the bevel.</param>
        /// <param name="bevelDark">Bottom/right shadow texture, or null to reuse the light bevel.</param>
        public static void PaintFace(
            Rect rect,
            ScenarioUiTextureCache cache,
            Color tintWash,
            int grainSeed,
            float grainStrength,
            float sheenStrength,
            Texture2D bevelLight,
            Texture2D bevelDark)
        {
            if (cache == null || rect.width <= (ChamferSafeInset * 2f) || rect.height <= (ChamferSafeInset * 2f))
                return;

            Rect face = Inset(rect, ChamferSafeInset);
            Color previous = GUI.color;

            if (tintWash.a > 0.001f)
            {
                GUI.color = tintWash;
                GUI.DrawTexture(face, Texture2D.whiteTexture);
            }

            if (sheenStrength > 0.001f)
            {
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(sheenStrength));
                GUI.DrawTexture(face, cache.GetVerticalSheen());
            }

            if (grainStrength > 0.001f)
            {
                Texture2D grain = cache.GetGrain(grainSeed);
                if (grain != null && grain.width > 0 && grain.height > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(grainStrength));
                    Rect uv = new Rect(0f, 0f, face.width / grain.width, face.height / grain.height);
                    GUI.DrawTextureWithTexCoords(face, grain, uv, true);
                }
            }

            GUI.color = previous;

            if (bevelLight != null || bevelDark != null)
                ScenarioUiAtlasSkin.DrawCornerCutBorder(rect, bevelLight, bevelDark ?? bevelLight);
        }

        private static Rect Inset(Rect rect, float inset)
        {
            return new Rect(
                rect.x + inset,
                rect.y + inset,
                Mathf.Max(0f, rect.width - (inset * 2f)),
                Mathf.Max(0f, rect.height - (inset * 2f)));
        }
    }
}
