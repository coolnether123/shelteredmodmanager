using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.UiKit;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Theme{
    /// <summary>
    /// Default metrics for the scenario authoring shell. Window-chrome values
    /// reuse <see cref="ShelteredAPI.Scenarios.Presentation.Authoring.Shell.ScenarioAuthoringShellLayout"/> so
    /// there is one source of truth for the shared shell spacing. Padding tokens
    /// are tuned to match the IMGUI shell's existing inner-padding choices so
    /// migrating renderer surfaces does not visibly shift their content.
    /// </summary>
    internal sealed class ScenarioUiMetrics : IScenarioUiMetrics
    {
        // Padding scale. Specific surfaces (PanelBase, Card) use values between
        // these tokens; see ScenarioUiStyleSheet for the per-role mapping.
        public float PaddingXs { get { return 4f; } }
        public float PaddingSm { get { return 8f; } }
        public float PaddingMd { get { return 8f; } }
        public float PaddingLg { get { return 12f; } }
        public float Gutter    { get { return ScenarioAuthoringShellLayout.Gutter; } }

        public float HeaderHeight     { get { return 36f; } }
        public float HeaderPaddingX   { get { return 12f; } }
        public float FooterHeight     { get { return 32f; } }
        public float FooterPaddingX   { get { return 12f; } }
        public float DividerThickness { get { return 1f; } }
        public float CornerInset      { get { return 6f; } }

        public float CardPadding      { get { return 10f; } }
        public float CardTitleHeight  { get { return 22f; } }
        public float RowHeight        { get { return 26f; } }
        public float CompactRowHeight { get { return 20f; } }
        public float PillHeight       { get { return 18f; } }
        public float PillPaddingX     { get { return 6f; } }

        // Typography. Sizes mirror the IMGUI shell renderer's local styles so a
        // 1:1 swap between renderer fields and style-sheet roles preserves the
        // shell's existing visual hierarchy.
        public int FontSizeBrand    { get { return 27; } }
        public int FontSizeTitle    { get { return 18; } }
        public int FontSizeSubtitle { get { return 14; } }
        public int FontSizeSection  { get { return 15; } }
        public int FontSizeBody     { get { return 15; } }
        public int FontSizeMuted    { get { return 13; } }
        public int FontSizePill     { get { return 11; } }
    }
}
