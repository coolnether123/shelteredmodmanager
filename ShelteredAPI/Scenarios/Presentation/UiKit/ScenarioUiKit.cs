using ShelteredAPI.Scenarios.UiKit.Frame;
using ShelteredAPI.Scenarios.UiKit.Textures;
using ShelteredAPI.Scenarios.UiKit.Theme;

namespace ShelteredAPI.Scenarios.UiKit
{
    /// <summary>
    /// Composition root for the scenario authoring UiKit. Hand a render module
    /// the live <see cref="ScenarioAuthoringSettingsSnapshot"/> and a context
    /// pops out: theme, texture cache, style sheet, and a default window
    /// frame wired together with consistent lifecycles. The kit is
    /// immutable; rebuild a context when settings change rather than mutating.
    ///
    /// Boundaries (deliberately not in scope):
    /// <list type="bullet">
    /// <item><description>Window roster: see <see cref="ScenarioAuthoringWindowRegistry"/>.</description></item>
    /// <item><description>Shell layout (top bar, status, inspector): see <see cref="ScenarioAuthoringShellLayout"/>.</description></item>
    /// <item><description>View-model contracts (sections, actions): see <see cref="ScenarioAuthoringInspectorSection"/>.</description></item>
    /// <item><description>Floating-window drag/resize: owned by the IMGUI render module.</description></item>
    /// </list>
    /// </summary>
    internal static class ScenarioUiKit
    {
        /// <summary>
        /// Builds a fresh context using the default palette and metrics. The
        /// returned context owns its texture cache; call <see cref="ScenarioUiContext.Dispose"/>
        /// when the consumer is torn down.
        /// </summary>
        public static ScenarioUiContext Build(ScenarioAuthoringSettingsSnapshot settings)
        {
            return Build(settings, null, null);
        }

        public static ScenarioUiContext Build(
            ScenarioAuthoringSettingsSnapshot settings,
            IScenarioUiPalette palette,
            IScenarioUiMetrics metrics)
        {
            ScenarioUiTheme theme = ScenarioUiTheme.FromSettings(settings, palette, metrics);
            ScenarioUiTextureCache textures = new ScenarioUiTextureCache();
            ScenarioUiStyleSheet styles = new ScenarioUiStyleSheet(theme, textures);
            IScenarioUiWindowFrame chrome = new ScenarioUiWindowChrome(styles);
            return new ScenarioUiContext(theme, textures, styles, chrome);
        }
    }

    /// <summary>
    /// Bag of UiKit services for a single render pass / render module owner.
    /// Holds the texture cache lifecycle so renderers can dispose cleanly when
    /// the authoring shell is torn down.
    /// </summary>
    internal sealed class ScenarioUiContext : System.IDisposable
    {
        private readonly ScenarioUiTheme _theme;
        private readonly ScenarioUiTextureCache _textures;
        private readonly ScenarioUiStyleSheet _styles;
        private readonly IScenarioUiWindowFrame _frame;

        internal ScenarioUiContext(
            ScenarioUiTheme theme,
            ScenarioUiTextureCache textures,
            ScenarioUiStyleSheet styles,
            IScenarioUiWindowFrame frame)
        {
            _theme = theme;
            _textures = textures;
            _styles = styles;
            _frame = frame;
        }

        public ScenarioUiTheme Theme { get { return _theme; } }
        public ScenarioUiTextureCache Textures { get { return _textures; } }
        public ScenarioUiStyleSheet Styles { get { return _styles; } }
        public IScenarioUiWindowFrame Frame { get { return _frame; } }

        /// <summary>Releases textures created via the cache.</summary>
        public void Dispose()
        {
            if (_textures != null)
                _textures.Clear();
        }
    }
}
