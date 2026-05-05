using UnityEngine;
namespace ShelteredAPI.Scenarios.Presentation.UiKit.Theme{
    /// <summary>
    /// Default palette for the scenario authoring shell. Values are derived from
    /// the existing IMGUI shell renderer so windows built on UiKit visually match
    /// the shell without re-tuning. Alpha is held at 1.0 here; the
    /// <see cref="ScenarioUiTheme"/> opacity helpers are responsible for the
    /// per-surface alpha layering convention.
    /// </summary>
    internal sealed class ScenarioUiPalette : IScenarioUiPalette
    {
        public Color PanelBase     { get { return new Color(0.12f, 0.07f, 0.05f, 1f); } }
        public Color PanelRaised   { get { return new Color(0.20f, 0.13f, 0.09f, 1f); } }
        public Color PanelInset    { get { return new Color(0.08f, 0.07f, 0.05f, 1f); } }
        public Color Viewport      { get { return new Color(0.07f, 0.05f, 0.04f, 0.18f); } }

        public Color BorderSubtle  { get { return new Color(0.46f, 0.34f, 0.18f, 0.66f); } }
        public Color BorderStrong  { get { return new Color(0.67f, 0.52f, 0.19f, 0.92f); } }

        // The shell uses a darker olive for "active/selected" fills (so the
        // selected tab settles back rather than glowing) and a brighter olive
        // for hover/highlight cues. Keep that convention here.
        public Color AccentActive  { get { return new Color(0.48f, 0.36f, 0.13f, 1f); } }
        public Color AccentDanger  { get { return new Color(0.50f, 0.13f, 0.09f, 1f); } }
        public Color AccentMuted   { get { return new Color(0.29f, 0.24f, 0.15f, 1f); } }

        public Color TextTitle     { get { return new Color(0.94f, 0.80f, 0.52f, 1f); } }
        public Color TextSubtitle  { get { return new Color(0.88f, 0.74f, 0.49f, 1f); } }
        public Color TextBody      { get { return new Color(0.92f, 0.89f, 0.82f, 1f); } }
        public Color TextMuted     { get { return new Color(0.77f, 0.72f, 0.63f, 1f); } }
        public Color TextOnAccent  { get { return new Color(0.98f, 0.92f, 0.74f, 1f); } }
    }
}
