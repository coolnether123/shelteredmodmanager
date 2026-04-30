using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Theme
{
    /// <summary>
    /// Default "Operator's Field Manual" palette: gunmetal frame + cream paper + ink + stamp red.
    /// </summary>
    internal sealed class FieldManualPalette : IThemePalette
    {
        public Color Gunmetal           { get { return new Color(0.22f, 0.23f, 0.25f, 1f); } }
        public Color GunmetalShadow     { get { return new Color(0.13f, 0.14f, 0.16f, 1f); } }
        public Color GunmetalHighlight  { get { return new Color(0.34f, 0.35f, 0.37f, 1f); } }
        public Color OliveBand          { get { return new Color(0.35f, 0.38f, 0.28f, 1f); } }
        public Color Brass              { get { return new Color(0.72f, 0.55f, 0.20f, 1f); } }

        public Color Paper              { get { return new Color(0.92f, 0.87f, 0.76f, 1f); } }
        public Color PaperShadow        { get { return new Color(0.00f, 0.00f, 0.00f, 0.35f); } }
        public Color PaperGrain         { get { return new Color(0.78f, 0.71f, 0.58f, 1f); } }

        public Color Ink                { get { return new Color(0.15f, 0.12f, 0.10f, 1f); } }
        public Color InkFaded           { get { return new Color(0.40f, 0.35f, 0.28f, 1f); } }

        public Color StampRed           { get { return new Color(0.55f, 0.18f, 0.15f, 1f); } }
        public Color GraphitePencil     { get { return new Color(0.30f, 0.30f, 0.32f, 1f); } }

        public Color KeycapFace         { get { return new Color(0.97f, 0.94f, 0.86f, 1f); } }
        public Color KeycapBevelLight   { get { return new Color(1.00f, 0.99f, 0.93f, 1f); } }
        public Color KeycapBevelDark    { get { return new Color(0.62f, 0.55f, 0.42f, 1f); } }
        public Color KeycapInk          { get { return new Color(0.12f, 0.10f, 0.08f, 1f); } }
        public Color KeycapPulse        { get { return new Color(0.78f, 0.20f, 0.16f, 1f); } }

        public Color MaskingTape        { get { return new Color(0.94f, 0.91f, 0.78f, 0.78f); } }
        public Color Vignette           { get { return new Color(0.00f, 0.00f, 0.00f, 0.55f); } }
    }
}
