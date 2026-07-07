using UnityEngine;
namespace ShelteredAPI.Scenarios.Presentation.UiKit.Theme{
    /// <summary>
    /// Default palette for the scenario authoring shell. Values lean on
    /// Sheltered's parchment panels, red/amber prompts, and green status
    /// accents rather than one uniform brown stack. Alpha is held at 1.0 here;
    /// the
    /// <see cref="ScenarioUiTheme"/> opacity helpers are responsible for the
    /// per-surface alpha layering convention.
    /// </summary>
    internal sealed class ScenarioUiPalette : IScenarioUiPalette
    {
        public Color PanelBase     { get { return new Color(0.13f, 0.09f, 0.06f, 1f); } }
        public Color PanelRaised   { get { return new Color(0.33f, 0.25f, 0.17f, 1f); } }
        public Color PanelInset    { get { return new Color(0.20f, 0.16f, 0.12f, 1f); } }
        public Color Viewport      { get { return new Color(0.09f, 0.06f, 0.05f, 0.18f); } }

        public Color BorderSubtle  { get { return new Color(0.50f, 0.39f, 0.23f, 0.85f); } }
        public Color BorderStrong  { get { return new Color(0.75f, 0.60f, 0.32f, 0.96f); } }

        public Color AccentActive  { get { return new Color(0.55f, 0.41f, 0.16f, 1f); } }
        public Color AccentHover   { get { return new Color(0.51f, 0.41f, 0.26f, 1f); } }
        public Color AccentDanger  { get { return new Color(0.54f, 0.18f, 0.15f, 1f); } }
        public Color AccentMuted   { get { return new Color(0.48f, 0.40f, 0.28f, 1f); } }
        public Color AccentSuccess { get { return new Color(0.31f, 0.48f, 0.24f, 1f); } }
        public Color AccentWarning { get { return new Color(0.65f, 0.43f, 0.12f, 1f); } }
        public Color AccentNeutral { get { return new Color(0.81f, 0.71f, 0.53f, 1f); } }
        public Color DisabledSurface { get { return new Color(0.22f, 0.20f, 0.18f, 1f); } }

        public Color TextTitle     { get { return new Color(0.96f, 0.79f, 0.44f, 1f); } }
        public Color TextSubtitle  { get { return new Color(0.88f, 0.74f, 0.49f, 1f); } }
        public Color TextBody      { get { return new Color(0.92f, 0.89f, 0.82f, 1f); } }
        public Color TextMuted     { get { return new Color(0.77f, 0.72f, 0.63f, 1f); } }
        public Color TextDisabled  { get { return new Color(0.48f, 0.45f, 0.39f, 1f); } }
        public Color TextOnAccent  { get { return new Color(0.98f, 0.92f, 0.74f, 1f); } }
        public Color TextOnLight   { get { return new Color(0.16f, 0.11f, 0.07f, 1f); } }
    }
}
