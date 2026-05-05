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
        float PaddingXs { get; }
        float PaddingSm { get; }
        float PaddingMd { get; }
        float PaddingLg { get; }
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
