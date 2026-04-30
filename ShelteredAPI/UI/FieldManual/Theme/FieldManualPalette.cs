using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Theme
{
    /// <summary>
    /// Default Sheltered-style field manual palette: dark wood frame, aged paper,
    /// ochre accents, and charcoal ink.
    /// </summary>
    internal sealed class FieldManualPalette : IThemePalette
    {
        public Color Gunmetal           { get { return new Color(0.24f, 0.13f, 0.09f, 1f); } }
        public Color GunmetalShadow     { get { return new Color(0.12f, 0.06f, 0.05f, 1f); } }
        public Color GunmetalHighlight  { get { return new Color(0.48f, 0.30f, 0.20f, 1f); } }
        public Color OliveBand          { get { return new Color(0.47f, 0.30f, 0.22f, 1f); } }
        public Color Brass              { get { return new Color(0.64f, 0.50f, 0.18f, 1f); } }

        public Color Paper              { get { return new Color(0.88f, 0.81f, 0.63f, 1f); } }
        public Color PaperShadow        { get { return new Color(0.08f, 0.03f, 0.02f, 0.48f); } }
        public Color PaperGrain         { get { return new Color(0.61f, 0.53f, 0.38f, 1f); } }

        public Color Ink                { get { return new Color(0.17f, 0.16f, 0.16f, 1f); } }
        public Color InkFaded           { get { return new Color(0.39f, 0.34f, 0.28f, 1f); } }

        public Color StampRed           { get { return new Color(0.48f, 0.14f, 0.12f, 1f); } }
        public Color GraphitePencil     { get { return new Color(0.26f, 0.25f, 0.25f, 1f); } }

        public Color KeycapFace         { get { return new Color(0.63f, 0.43f, 0.32f, 1f); } }
        public Color KeycapBevelLight   { get { return new Color(0.82f, 0.67f, 0.50f, 1f); } }
        public Color KeycapBevelDark    { get { return new Color(0.29f, 0.16f, 0.11f, 1f); } }
        public Color KeycapInk          { get { return new Color(0.93f, 0.88f, 0.80f, 1f); } }
        public Color KeycapPulse        { get { return new Color(0.70f, 0.25f, 0.18f, 1f); } }

        public Color MaskingTape        { get { return new Color(0.81f, 0.76f, 0.58f, 0.72f); } }
        public Color Vignette           { get { return new Color(0.05f, 0.02f, 0.04f, 0.58f); } }
    }
}
