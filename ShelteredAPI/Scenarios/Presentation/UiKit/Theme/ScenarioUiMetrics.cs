using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.UiKit;
using ShelteredAPI.Scenarios.Presentation.UiKit.Textures;

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
        // Phase 9 spacing scale: 0, 4, 8, 12, 16, 24, 32.
        public float Space0 { get { return 0f; } }
        public float Space1 { get { return 4f; } }
        public float Space2 { get { return 8f; } }
        public float Space3 { get { return 12f; } }
        public float Space4 { get { return 16f; } }
        public float Space5 { get { return 24f; } }
        public float Space6 { get { return 32f; } }
        public float PaddingXs { get { return 4f; } }
        public float PaddingSm { get { return 8f; } }
        public float PaddingMd { get { return 8f; } }
        public float PaddingLg { get { return 12f; } }
        public float PaddingXl { get { return 16f; } }
        public float PaddingXxl { get { return 24f; } }
        public float PaddingXxxl { get { return 32f; } }
        public float Gutter    { get { return ScenarioAuthoringShellLayout.Gutter; } }

        public float HeaderHeight     { get { return 36f; } }
        public float HeaderPaddingX   { get { return 12f; } }
        public float FooterHeight     { get { return 32f; } }
        public float FooterPaddingX   { get { return 12f; } }
        public float DividerThickness { get { return 1f; } }
        public float CornerInset      { get { return ScenarioUiAtlasSkin.CornerInsetPixels; } }

        public float CardPadding      { get { return 12f; } }
        public float CardTitleHeight  { get { return 32f; } }
        public float RowHeight        { get { return 40f; } }
        public float CompactRowHeight { get { return 28f; } }
        public float PillHeight       { get { return 20f; } }
        public float PillPaddingX     { get { return 6f; } }
        public float PanePadding { get { return 12f; } }
        public float InsetPadding { get { return 8f; } }
        public float NavigatorRowGap { get { return 4f; } }
        public float NavigatorGroupGap { get { return 12f; } }
        public float CardGap { get { return 12f; } }
        public float HeadingGap { get { return 8f; } }
        public float FieldRowGap { get { return 8f; } }
        public float FormGroupGap { get { return 16f; } }
        public float ButtonGap { get { return 8f; } }
        public float SubtabGap { get { return 4f; } }
        public float ButtonHeight { get { return 32f; } }
        public float SubtabHeight { get { return 36f; } }
        public float NavigatorRowHeight { get { return 40f; } }
        public float NavigatorTwoLineHeight { get { return 52f; } }
        public float SectionHeadingHeight { get { return 32f; } }
        public float MinimumCardHeight { get { return 56f; } }
        public float DocumentMaxWidth { get { return 760f; } }
        public float TextFieldMaxWidth { get { return 520f; } }
        public float SelectorMaxWidth { get { return 320f; } }
        public float NumericFieldMaxWidth { get { return 160f; } }
        public float NarrativeFieldMaxWidth { get { return 680f; } }
        public float ButtonMaxWidth { get { return 240f; } }
        public float CompactChoiceMaxWidth { get { return 720f; } }
        public float SpecializedSurfaceMaxWidth { get { return 1080f; } }

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
