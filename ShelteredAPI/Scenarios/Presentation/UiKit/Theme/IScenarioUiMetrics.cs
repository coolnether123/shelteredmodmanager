using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Theme{
    /// <summary>
    /// Sizing, spacing, and typography metrics for a scenario authoring window
    /// theme. Values are in IMGUI virtual pixels (the same coordinate space the
    /// shell layout already uses). Defaults align with
    /// <see cref="ShelteredAPI.Scenarios.Presentation.Authoring.Shell.ScenarioAuthoringShellLayout"/> so the kit
    /// does not redeclare shell-level constants.
    /// </summary>
    internal interface IScenarioUiMetrics
    {
        // Spacing scale
        float Space0 { get; }
        float Space1 { get; }
        float Space2 { get; }
        float Space3 { get; }
        float Space4 { get; }
        float Space5 { get; }
        float Space6 { get; }
        float PaddingXs { get; }
        float PaddingSm { get; }
        float PaddingMd { get; }
        float PaddingLg { get; }
        float PaddingXl { get; }
        float PaddingXxl { get; }
        float PaddingXxxl { get; }
        float Gutter { get; }

        // Window chrome
        float HeaderHeight { get; }
        float HeaderPaddingX { get; }
        float FooterHeight { get; }
        float FooterPaddingX { get; }
        float DividerThickness { get; }
        float CornerInset { get; }

        // Widgets
        float CardPadding { get; }
        float CardTitleHeight { get; }
        float RowHeight { get; }
        float CompactRowHeight { get; }
        float PillHeight { get; }
        float PillPaddingX { get; }
        float PanePadding { get; }
        float InsetPadding { get; }
        float NavigatorRowGap { get; }
        float NavigatorGroupGap { get; }
        float CardGap { get; }
        float HeadingGap { get; }
        float FieldRowGap { get; }
        float FormGroupGap { get; }
        float ButtonGap { get; }
        float SubtabGap { get; }
        float ButtonHeight { get; }
        float SubtabHeight { get; }
        float NavigatorRowHeight { get; }
        float NavigatorTwoLineHeight { get; }
        float SectionHeadingHeight { get; }
        float MinimumCardHeight { get; }
        float DocumentMaxWidth { get; }
        float TextFieldMaxWidth { get; }
        float SelectorMaxWidth { get; }
        float NumericFieldMaxWidth { get; }
        float NarrativeFieldMaxWidth { get; }
        float ButtonMaxWidth { get; }
        float CompactChoiceMaxWidth { get; }
        float SpecializedSurfaceMaxWidth { get; }

        // Typography (point sizes)
        int FontSizeBrand { get; }
        int FontSizeTitle { get; }
        int FontSizeSubtitle { get; }
        int FontSizeSection { get; }
        int FontSizeBody { get; }
        int FontSizeMuted { get; }
        int FontSizePill { get; }
    }
}
