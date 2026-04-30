namespace ShelteredAPI.UI.FieldManual.Theme
{
    /// <summary>
    /// Default metrics for a 1200x900 field-manual panel.
    /// </summary>
    internal sealed class FieldManualMetrics : IThemeMetrics
    {
        public int PanelWidth            { get { return 1200; } }
        public int PanelHeight           { get { return 900; } }
        public int FrameInset            { get { return 28; } }
        public int RivetSize             { get { return 14; } }
        public int RivetMargin           { get { return 18; } }

        public int TitleStripHeight      { get { return 64; } }
        public int TitleStripInset       { get { return 12; } }

        public int TapeWidth             { get { return 140; } }
        public int TapeHeight            { get { return 28; } }

        public int RowHeight             { get { return 56; } }
        public int RowSpacing            { get { return 8; } }
        public int ContentTopPadding     { get { return 24; } }
        public int ContentBottomPadding  { get { return 24; } }
        public int ContentSidePadding    { get { return 36; } }

        public int KeycapWidth           { get { return 96; } }
        public int KeycapHeight          { get { return 42; } }
        public int KeycapSpacing         { get { return 12; } }
        public int ActionLabelWidth      { get { return 360; } }

        public int SectionStampHeight                { get { return 38; } }
        public float SectionStampRotationDegrees     { get { return -2.5f; } }

        public int FooterHeight          { get { return 64; } }
    }
}
