using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>Builds pacing guidance for Timeline and the informational playtest pre-flight link.</summary>
    internal static class ScenarioPacingAuthoringSectionBuilder
    {
        internal const string SectionId = "timeline_pacing";
        internal const string DensityLabel = "Density data";

        public static ScenarioAuthoringInspectorSection BuildTimelineSection(
            ScenarioDefinition definition,
            ScenarioTimelineBuilder timelineBuilder)
        {
            ScenarioPacingAnalysis analysis = Analyze(definition, timelineBuilder);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Reading", analysis.Reading, "Pacing guidance, not a rule. Shape the rhythm that fits your scenario."));

            int shownDays = ResolveShownDayCount(analysis);
            ScenarioAuthoringInspectorItem density = Item.Property(DensityLabel, BuildDensityMetadata(analysis, shownDays));
            density.Detail = analysis.LastAuthoredDay > ScenarioPacingAnalysisService.VisibleDayLimit
                ? "Showing the first 30 days; authored happenings continue through day " + analysis.LastAuthoredDay.ToString(CultureInfo.InvariantCulture) + "."
                : "Showing day 1 through day " + shownDays.ToString(CultureInfo.InvariantCulture) + ".";
            items.Add(density);

            AddCallout(items, analysis.QuietCallout);
            AddCallout(items, analysis.EndingCallout);
            if (string.IsNullOrEmpty(analysis.QuietCallout) && string.IsNullOrEmpty(analysis.EndingCallout))
                items.Add(Item.Text("Guidance only: this rhythm has no obvious long gap or missing end condition."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = SectionId,
                Title = "Pacing",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }

        public static void AddPreflightGuidance(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioDefinition definition,
            ScenarioTimelineBuilder timelineBuilder)
        {
            if (items == null)
                return;

            ScenarioPacingAnalysis analysis = Analyze(definition, timelineBuilder);
            string callout = !string.IsNullOrEmpty(analysis.EndingCallout)
                ? analysis.EndingCallout
                : analysis.QuietCallout;
            if (string.IsNullOrEmpty(callout))
                return;

            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionShellOpenTimeline,
                "Pacing guidance: " + callout,
                "Informational only - open Timeline to review the per-day rhythm.",
                true,
                false,
                "PC",
                "Guidance, not a playtest blocker.")));
        }

        private static ScenarioPacingAnalysis Analyze(ScenarioDefinition definition, ScenarioTimelineBuilder timelineBuilder)
        {
            return new ScenarioPacingAnalysisService(timelineBuilder).Analyze(definition);
        }

        private static int ResolveShownDayCount(ScenarioPacingAnalysis analysis)
        {
            int authoredDays = analysis != null ? analysis.LastAuthoredDay : 0;
            return Math.Min(ScenarioPacingAnalysisService.VisibleDayLimit, Math.Max(8, authoredDays));
        }

        private static string BuildDensityMetadata(ScenarioPacingAnalysis analysis, int shownDays)
        {
            List<string> values = new List<string>();
            for (int day = 1; day <= shownDays; day++)
                values.Add(analysis.GetCount(day).ToString(CultureInfo.InvariantCulture));
            return string.Join(",", values.ToArray());
        }

        private static void AddCallout(List<ScenarioAuthoringInspectorItem> items, string callout)
        {
            if (string.IsNullOrEmpty(callout))
                return;
            ScenarioAuthoringInspectorItem item = Item.Text("Guidance: " + callout);
            item.Emphasized = true;
            items.Add(item);
        }
    }
}
