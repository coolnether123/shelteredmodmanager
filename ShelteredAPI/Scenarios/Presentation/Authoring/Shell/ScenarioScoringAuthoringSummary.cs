using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal static class ScenarioScoringAuthoringSummary
    {
        internal sealed class Summary
        {
            public bool IsEnabled;
            public string Status;
            public string ScoreLabel;
            public int CategoryCount;
            public int RuleCount;
        }

        public static Summary Build(ScenarioDefinition definition)
        {
            ScenarioScoringDefinition scoring = definition != null ? definition.Scoring : null;
            Summary summary = new Summary();
            summary.IsEnabled = scoring != null && scoring.Enabled;
            summary.ScoreLabel = scoring != null && !string.IsNullOrEmpty(scoring.ScoreLabel) ? scoring.ScoreLabel : "Score";
            summary.Status = summary.IsEnabled
                ? (string.IsNullOrEmpty(summary.ScoreLabel) ? "Enabled" : summary.ScoreLabel)
                : "Disabled";
            summary.CategoryCount = scoring != null && scoring.Categories != null ? scoring.Categories.Count : 0;
            summary.RuleCount = scoring != null && scoring.Rules != null ? scoring.Rules.Count : 0;
            return summary;
        }
    }
}
