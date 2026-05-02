namespace ShelteredAPI.UI.FieldManual.Theme
{
    /// <summary>
    /// Default metrics for the Sheltered book-style keybind panel.
    /// </summary>
    internal sealed class FieldManualMetrics : IThemeMetrics
    {
        public int PanelWidth            { get { return 1280; } }
        public int PanelHeight           { get { return 820; } }
        public int FrameInset            { get { return 28; } }
        public int RivetSize             { get { return 14; } }
        public int RivetMargin           { get { return 18; } }

        public int TitleStripHeight      { get { return 64; } }
        public int TitleStripInset       { get { return 12; } }

        public int TapeWidth             { get { return 140; } }
        public int TapeHeight            { get { return 28; } }

        public int RowHeight             { get { return 50; } }
        public int RowSpacing            { get { return 6; } }
        public int ContentTopPadding     { get { return 20; } }
        public int ContentBottomPadding  { get { return 24; } }
        public int ContentSidePadding    { get { return 36; } }

        public int KeycapWidth           { get { return 112; } }
        public int KeycapHeight          { get { return 38; } }
        public int KeycapSpacing         { get { return 8; } }
        public int ActionLabelWidth      { get { return 430; } }

        public int SectionStampHeight                { get { return 38; } }
        public float SectionStampRotationDegrees     { get { return 0f; } }

        public int FooterHeight          { get { return 92; } }
    }
}
