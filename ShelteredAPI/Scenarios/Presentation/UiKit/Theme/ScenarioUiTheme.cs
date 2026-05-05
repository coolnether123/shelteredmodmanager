using UnityEngine;
using ShelteredAPI.Scenarios.Application.Authoring;
namespace ShelteredAPI.Scenarios.Presentation.UiKit.Theme{
    /// <summary>
    /// An immutable bundle of the live palette, metrics, and the user-driven
    /// panel opacity setting. Themes are cheap value objects: build a new one
    /// when settings change instead of mutating an existing instance.
    ///
    /// Surface layering convention: base panels apply <see cref="WithPanelOpacity"/>,
    /// raised surfaces (cards, headers) apply <see cref="WithRaisedOpacity"/>,
    /// and active fills (selected buttons/tabs) apply <see cref="WithActiveOpacity"/>.
    /// Each step nudges alpha up so layers visually separate even at low opacity.
    /// </summary>
    internal sealed class ScenarioUiTheme
    {
        private const float RaisedOpacityBoost = 0.06f;
        private const float ActiveOpacityBoost = 0.10f;

        private readonly IScenarioUiPalette _palette;
        private readonly IScenarioUiMetrics _metrics;
        private readonly float _panelOpacity;

        public ScenarioUiTheme(IScenarioUiPalette palette, IScenarioUiMetrics metrics, float panelOpacity)
        {
            _palette = palette ?? new ScenarioUiPalette();
            _metrics = metrics ?? new ScenarioUiMetrics();
            _panelOpacity = Mathf.Clamp01(panelOpacity);
        }

        public IScenarioUiPalette Palette { get { return _palette; } }
        public IScenarioUiMetrics Metrics { get { return _metrics; } }
        public float PanelOpacity { get { return _panelOpacity; } }

        /// <summary>
        /// Returns the palette colour with the theme's panel opacity applied.
        /// Use for base panels (the bottom-most surface in a stack).
        /// </summary>
        public Color WithPanelOpacity(Color baseColor)
        {
            return ScaleAlpha(baseColor, _panelOpacity);
        }

        /// <summary>
        /// Returns the palette colour with the raised-surface opacity applied
        /// (panel opacity + a small boost). Use for cards, headers, and other
        /// surfaces that sit on top of <see cref="WithPanelOpacity"/> base.
        /// </summary>
        public Color WithRaisedOpacity(Color baseColor)
        {
            return ScaleAlpha(baseColor, Mathf.Min(1f, _panelOpacity + RaisedOpacityBoost));
        }

        /// <summary>
        /// Returns the palette colour with the active-fill opacity applied
        /// (panel opacity + a larger boost). Use for currently selected
        /// buttons, tabs, and pills so they read clearly even when the user
        /// has dialled panel opacity down.
        /// </summary>
        public Color WithActiveOpacity(Color baseColor)
        {
            return ScaleAlpha(baseColor, Mathf.Min(1f, _panelOpacity + ActiveOpacityBoost));
        }

        /// <summary>
        /// Builds a theme from an authoring settings snapshot using the default
        /// palette and metrics. Reads <c>shell.panel_opacity</c> if present and
        /// clamps to the same range the shell renderer uses.
        /// </summary>
        public static ScenarioUiTheme FromSettings(ScenarioAuthoringSettingsSnapshot settings)
        {
            return FromSettings(settings, null, null);
        }

        /// <summary>
        /// Variant of <see cref="FromSettings(ScenarioAuthoringSettingsSnapshot)"/>
        /// that lets callers swap the palette or metrics without re-implementing
        /// the opacity-reading logic.
        /// </summary>
        public static ScenarioUiTheme FromSettings(
            ScenarioAuthoringSettingsSnapshot settings,
            IScenarioUiPalette palette,
            IScenarioUiMetrics metrics)
        {
            float opacity = ResolvePanelOpacity(settings);
            return new ScenarioUiTheme(
                palette ?? new ScenarioUiPalette(),
                metrics ?? new ScenarioUiMetrics(),
                opacity);
        }

        public static float ResolvePanelOpacity(ScenarioAuthoringSettingsSnapshot settings)
        {
            return settings != null
                ? Mathf.Clamp(settings.GetFloat("shell.panel_opacity", 0.82f), 0.55f, 1f)
                : 0.82f;
        }

        /// <summary>
        /// Returns a theme using the default palette and metrics but ignoring
        /// any user setting; useful for diagnostics or unit-style harnesses.
        /// </summary>
        public static ScenarioUiTheme Default()
        {
            return new ScenarioUiTheme(new ScenarioUiPalette(), new ScenarioUiMetrics(), 1f);
        }

        private static Color ScaleAlpha(Color baseColor, float alphaMultiplier)
        {
            return new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alphaMultiplier);
        }
    }
}
