using UnityEngine;
using ShelteredScenarioEditor.Application.Authoring;
namespace ShelteredScenarioEditor.Presentation.UiKit.Theme{
    /// <summary>
    /// An immutable bundle of the live palette, metrics, and the user-driven
    /// modal-scrim opacity setting. Themes are cheap value objects: build a new one
    /// when settings change instead of mutating an existing instance.
    ///
    /// Material surfaces are always opaque. The existing setting controls only
    /// SurfaceScrim and can never fade editor panels or pages.
    /// </summary>
    internal sealed class ScenarioUiTheme
    {
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

        public Color ScrimColor
        {
            get { return ScaleAlpha(_palette.SurfaceScrim, _panelOpacity); }
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
