using System.Collections.Generic;

namespace ParalivesAPI.Core
{
    public enum ParalivesCharacterTab
    {
        Thoughts = 0,
        Profile = 1,
        Skills = 2,
        Social = 3,
        Relationships = Social,
        Occupations = 4,
        Inventory = 5,
        Memories = 6,
        Goals = 7
    }

    public sealed class ParalivesUiText
    {
        public string Text { get; set; }

        public string TranslationKey { get; set; }

        public string[] Parameters { get; set; }

        public bool HasValue
        {
            get { return !string.IsNullOrEmpty(Text) || !string.IsNullOrEmpty(TranslationKey); }
        }

        public static ParalivesUiText FromText(string text)
        {
            return new ParalivesUiText { Text = text ?? string.Empty };
        }

        public static ParalivesUiText FromTranslation(string translationKey, params string[] parameters)
        {
            return new ParalivesUiText
            {
                TranslationKey = translationKey ?? string.Empty,
                Parameters = parameters
            };
        }
    }

    public sealed class ParalivesOccupationPanel
    {
        private readonly List<ParalivesOccupationPanelRow> _rows =
            new List<ParalivesOccupationPanelRow>();

        public bool ReplacePerformanceRows { get; set; }

        public ParalivesUiText PerformanceLabel { get; set; }

        public string PerformanceLabelText
        {
            get { return PerformanceLabel == null ? null : PerformanceLabel.Text; }
            set { PerformanceLabel = ParalivesUiText.FromText(value); }
        }

        public IList<ParalivesOccupationPanelRow> Rows
        {
            get { return _rows; }
        }

        public IList<ParalivesOccupationPanelRow> PerformanceRows
        {
            get { return _rows; }
        }

        public ParalivesOccupationPanel AddPerformanceRow(string text, string tooltipText, bool isGood)
        {
            return AddPerformanceRow(
                ParalivesUiText.FromText(text),
                ParalivesUiText.FromText(tooltipText),
                isGood);
        }

        public ParalivesOccupationPanel AddPerformanceRow(
            ParalivesUiText label,
            ParalivesUiText tooltip,
            bool isPositive)
        {
            _rows.Add(new ParalivesOccupationPanelRow
            {
                Label = label,
                Tooltip = tooltip,
                IsPositive = isPositive
            });
            return this;
        }
    }

    public sealed class ParalivesOccupationPanelRow
    {
        public ParalivesUiText Label { get; set; }

        public ParalivesUiText Tooltip { get; set; }

        public bool IsPositive { get; set; }

        public string Text
        {
            get { return Label == null ? null : Label.Text; }
            set { Label = ParalivesUiText.FromText(value); }
        }

        public string TooltipText
        {
            get { return Tooltip == null ? null : Tooltip.Text; }
            set { Tooltip = ParalivesUiText.FromText(value); }
        }

        public bool IsGood
        {
            get { return IsPositive; }
            set { IsPositive = value; }
        }
    }
}
