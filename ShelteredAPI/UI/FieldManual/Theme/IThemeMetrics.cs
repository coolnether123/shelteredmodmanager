using ShelteredAPI.UI.FieldManual.Tooltips;

namespace ShelteredAPI.UI.FieldManual.Theme
{
    /// <summary>
    /// Layout metrics for a field-manual themed panel. All values are in NGUI virtual pixels
    /// and reference a centered coordinate system (origin at panel center, +Y up).
    /// </summary>
    internal interface IThemeMetrics
    {
        // Outer panel
        int PanelWidth { get; }
        int PanelHeight { get; }
        int FrameInset { get; }      // distance from gunmetal edge to paper edge
        int RivetSize { get; }
        int RivetMargin { get; }     // distance from gunmetal corner to rivet center

        // Title strip
        int TitleStripHeight { get; }
        int TitleStripInset { get; } // distance from frame edge to strip edge

        // Tape
        int TapeWidth { get; }
        int TapeHeight { get; }

        // Content rows
        int RowHeight { get; }
        int RowSpacing { get; }
        int ContentTopPadding { get; }
        int ContentBottomPadding { get; }
        int ContentSidePadding { get; }

        // Keycaps
        int KeycapWidth { get; }
        int KeycapHeight { get; }
        int KeycapSpacing { get; }
        int ActionLabelWidth { get; }

        // Section stamp
        int SectionStampHeight { get; }
        float SectionStampRotationDegrees { get; }

        // Footer (Save & Close strip)
        int FooterHeight { get; }
    }
}
