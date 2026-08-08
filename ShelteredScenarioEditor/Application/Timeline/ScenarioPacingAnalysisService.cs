using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Domain.Timeline;

namespace ShelteredScenarioEditor.Application.Timeline
{
    /// <summary>Read-only creator guidance derived from authored scenario times.</summary>
    internal sealed class ScenarioPacingAnalysisService
    {
        internal const int QuietDayGuidanceThreshold = 4;
        internal const int VisibleDayLimit = 30;

        private readonly ScenarioTimelineBuilder _timelineBuilder;

        public ScenarioPacingAnalysisService(ScenarioTimelineBuilder timelineBuilder)
        {
            _timelineBuilder = timelineBuilder;
        }

        public ScenarioPacingAnalysis Analyze(ScenarioDefinition definition)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            HashSet<string> countedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<ScenarioTimelineEntry> entries = _timelineBuilder != null
                ? _timelineBuilder.BuildEntries(definition, null)
                : new List<ScenarioTimelineEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (!IsAuthoredTimedEntry(definition, entry))
                    continue;

                int day;
                if (!ScenarioTimelineCollisionAnalyzer.TryGetScenarioDay(entry.When, out day))
                    continue;

                AddDay(counts, day);
                if (!string.IsNullOrEmpty(entry.Id))
                    countedIds.Add(entry.Id);
            }

            AddTimedStoryConversations(definition, counts, countedIds);
            return BuildAnalysis(definition, counts);
        }

        internal static bool HasAuthoredEndCondition(ScenarioDefinition definition)
        {
            WinLossConditionsDefinition winLoss = definition != null ? definition.WinLossConditions : null;
            int wins = winLoss != null && winLoss.WinConditions != null ? winLoss.WinConditions.Count : 0;
            int losses = winLoss != null && winLoss.LossConditions != null ? winLoss.LossConditions.Count : 0;
            return wins + losses > 0;
        }

        private static bool IsAuthoredTimedEntry(ScenarioDefinition definition, ScenarioTimelineEntry entry)
        {
            if (entry == null || entry.When == null)
                return false;

            // A restore marker is derived from one authored weather event. Object activation
            // references have no authored clock and the Timeline builder gives them a display fallback.
            if (string.Equals(entry.SourceKind, "weather_restore", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.SourceKind, "object_activation", StringComparison.OrdinalIgnoreCase))
                return false;

            int index = entry.SourceIndex;
            if (string.Equals(entry.SourceKind, "scheduled_action", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.ScheduledActions != null && index >= 0 && index < definition.ScheduledActions.Count
                    && definition.ScheduledActions[index] != null && definition.ScheduledActions[index].DueTime != null;
            if (string.Equals(entry.SourceKind, "future_survivor", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && index >= 0 && index < definition.FamilySetup.FutureSurvivors.Count
                    && definition.FamilySetup.FutureSurvivors[index] != null && definition.FamilySetup.FutureSurvivors[index].Arrival != null;
            if (string.Equals(entry.SourceKind, "inventory_change", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && index >= 0 && index < definition.StartingInventory.ScheduledChanges.Count
                    && definition.StartingInventory.ScheduledChanges[index] != null && definition.StartingInventory.ScheduledChanges[index].When != null;
            if (string.Equals(entry.SourceKind, "weather_event", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null && index >= 0 && index < definition.TriggersAndEvents.WeatherEvents.Count
                    && definition.TriggersAndEvents.WeatherEvents[index] != null && definition.TriggersAndEvents.WeatherEvents[index].When != null;
            if (string.Equals(entry.SourceKind, "quest_popup", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.Quests != null && definition.Quests.Quests != null && index >= 0 && index < definition.Quests.Quests.Count
                    && definition.Quests.Quests[index] != null && definition.Quests.Quests[index].ScheduledStart != null;
            if (string.Equals(entry.SourceKind, "journal_entry", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.Journal != null && definition.Journal.Entries != null && index >= 0 && index < definition.Journal.Entries.Count
                    && definition.Journal.Entries[index] != null && definition.Journal.Entries[index].DueTime != null;
            if (string.Equals(entry.SourceKind, "bunker_expansion", StringComparison.OrdinalIgnoreCase))
                return definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && index >= 0 && index < definition.BunkerGrid.Expansions.Count
                    && definition.BunkerGrid.Expansions[index] != null && definition.BunkerGrid.Expansions[index].RequiredTime != null;
            return true;
        }

        private static void AddTimedStoryConversations(
            ScenarioDefinition definition,
            Dictionary<int, int> counts,
            HashSet<string> countedIds)
        {
            ScenarioConversationAuthoringDefinition authoring = definition != null ? definition.Conversations : null;
            for (int i = 0; authoring != null && authoring.Conversations != null && i < authoring.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = authoring.Conversations[i];
                ScenarioConversationTriggerDefinition trigger = conversation != null ? conversation.Trigger : null;
                if (trigger == null || trigger.Source != ScenarioConversationTriggerSource.Timeline)
                    continue;

                string id = "conversation." + (conversation.Id ?? i.ToString(CultureInfo.InvariantCulture));
                if (countedIds.Contains(id))
                    continue;

                int day;
                if (!ScenarioTimelineCollisionAnalyzer.TryGetScenarioDay(trigger.Time, out day))
                    continue;
                AddDay(counts, day);
                countedIds.Add(id);
            }
        }

        private static void AddDay(Dictionary<int, int> counts, int day)
        {
            int count;
            counts.TryGetValue(day, out count);
            counts[day] = count + 1;
        }

        private static ScenarioPacingAnalysis BuildAnalysis(ScenarioDefinition definition, Dictionary<int, int> counts)
        {
            ScenarioPacingAnalysis analysis = new ScenarioPacingAnalysis();
            analysis.HasAuthoredEndCondition = HasAuthoredEndCondition(definition);
            if (counts.Count == 0)
            {
                analysis.BusiestDays = new int[0];
                analysis.Reading = "No authored happenings yet - add a few beats to see the scenario rhythm.";
                return analysis;
            }

            int firstDay = int.MaxValue;
            int lastDay = 0;
            int total = 0;
            int busiestCount = 0;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                firstDay = Math.Min(firstDay, pair.Key);
                lastDay = Math.Max(lastDay, pair.Key);
                total += pair.Value;
                busiestCount = Math.Max(busiestCount, pair.Value);
            }

            analysis.FirstAuthoredDay = firstDay;
            analysis.LastAuthoredDay = lastDay;
            analysis.TotalAuthoredHappenings = total;
            analysis.BusiestCount = busiestCount;
            analysis.SetDayCounts(counts);
            List<int> busiestDays = new List<int>();
            List<int> authoredDays = new List<int>(counts.Keys);
            authoredDays.Sort();
            for (int i = 0; i < authoredDays.Count; i++)
            {
                int day = authoredDays[i];
                if (counts[day] == busiestCount)
                    busiestDays.Add(day);
            }
            analysis.BusiestDays = busiestDays.ToArray();

            ResolveLongestQuietStretch(analysis, authoredDays);
            analysis.Reading = BuildReading(analysis);
            if (analysis.LongestQuietDayCount >= QuietDayGuidanceThreshold)
            {
                analysis.QuietCallout = analysis.LongestQuietDayCount.ToString(CultureInfo.InvariantCulture)
                    + " quiet days in a row - players may lose momentum.";
            }
            if (!analysis.HasAuthoredEndCondition)
            {
                analysis.EndingCallout = "Nothing authored after day "
                    + analysis.LastAuthoredDay.ToString(CultureInfo.InvariantCulture)
                    + " - does the scenario have an ending?";
            }
            return analysis;
        }

        private static void ResolveLongestQuietStretch(ScenarioPacingAnalysis analysis, List<int> authoredDays)
        {
            int previousAuthoredDay = 0;
            for (int i = 0; authoredDays != null && i < authoredDays.Count; i++)
            {
                int day = authoredDays[i];
                int quietCount = day - previousAuthoredDay - 1;
                if (quietCount > analysis.LongestQuietDayCount)
                {
                    analysis.LongestQuietDayCount = quietCount;
                    analysis.LongestQuietStartDay = previousAuthoredDay + 1;
                    analysis.LongestQuietEndDay = day - 1;
                }
                previousAuthoredDay = day;
            }
        }

        private static string BuildReading(ScenarioPacingAnalysis analysis)
        {
            List<string> clauses = new List<string>();
            int openingCount = analysis.GetCount(1) + analysis.GetCount(2);
            if (openingCount >= 4)
            {
                clauses.Add("Busy start (" + openingCount.ToString(CultureInfo.InvariantCulture) + " events days 1-2)");
            }
            else if (analysis.FirstAuthoredDay > 1)
            {
                clauses.Add("First authored beat on day " + analysis.FirstAuthoredDay.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                clauses.Add("Measured start (" + openingCount.ToString(CultureInfo.InvariantCulture) + " event" + (openingCount == 1 ? string.Empty : "s") + " days 1-2)");
            }

            if (analysis.LongestQuietDayCount > 0)
            {
                clauses.Add("quiet " + FormatDayRange(analysis.LongestQuietStartDay, analysis.LongestQuietEndDay));
            }
            clauses.Add("nothing after day " + analysis.LastAuthoredDay.ToString(CultureInfo.InvariantCulture));
            return string.Join(", ", clauses.ToArray());
        }

        private static string FormatDayRange(int start, int end)
        {
            return start == end
                ? "day " + start.ToString(CultureInfo.InvariantCulture)
                : "days " + start.ToString(CultureInfo.InvariantCulture) + "-" + end.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ScenarioPacingAnalysis
    {
        private Dictionary<int, int> _dayCounts;

        public ScenarioPacingAnalysis()
        {
            _dayCounts = new Dictionary<int, int>();
            BusiestDays = new int[0];
        }

        public int TotalAuthoredHappenings { get; internal set; }
        public int FirstAuthoredDay { get; internal set; }
        public int LastAuthoredDay { get; internal set; }
        public int LongestQuietDayCount { get; internal set; }
        public int LongestQuietStartDay { get; internal set; }
        public int LongestQuietEndDay { get; internal set; }
        public int BusiestCount { get; internal set; }
        public int[] BusiestDays { get; internal set; }
        public bool HasAuthoredEndCondition { get; internal set; }
        public string Reading { get; internal set; }
        public string QuietCallout { get; internal set; }
        public string EndingCallout { get; internal set; }

        public int GetCount(int day)
        {
            int count;
            return day > 0 && _dayCounts.TryGetValue(day, out count) ? count : 0;
        }

        internal void SetDayCounts(Dictionary<int, int> dayCounts)
        {
            _dayCounts = dayCounts != null
                ? new Dictionary<int, int>(dayCounts)
                : new Dictionary<int, int>();
        }
    }
}
