using System;
using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Timeline;

namespace ShelteredAPI.Scenarios.Application.Timeline
{
    /// <summary>Validation-style analysis for schedule collisions; it does not mutate the scenario definition.</summary>
    internal static class ScenarioTimelineCollisionAnalyzer
    {
        public static void ApplyWarnings(ScenarioDefinition definition, List<ScenarioTimelineEntry> entries)
        {
            Dictionary<string, List<ScenarioTimelineEntry>> atTime = new Dictionary<string, List<ScenarioTimelineEntry>>(StringComparer.Ordinal);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                int day;
                int hour;
                int minute;
                if (entry == null || !TryNormalizeTime(entry.When, out day, out hour, out minute))
                    continue;
                string key = day + ":" + hour + ":" + minute;
                List<ScenarioTimelineEntry> group;
                if (!atTime.TryGetValue(key, out group))
                {
                    group = new List<ScenarioTimelineEntry>();
                    atTime.Add(key, group);
                }
                group.Add(entry);
            }

            foreach (KeyValuePair<string, List<ScenarioTimelineEntry>> pair in atTime)
            {
                List<ScenarioTimelineEntry> group = pair.Value;
                if (group.Count < 2)
                    continue;
                string warning = FindInventoryOrderingWarning(definition, group);
                if (string.IsNullOrEmpty(warning))
                    warning = group.Count.ToString() + " entries share this time; they run in their listed order.";
                for (int i = 0; i < group.Count; i++)
                    AppendWarning(group[i], warning);
            }
        }

        /// <summary>Returns the same normalized scenario day used to group collision candidates.</summary>
        internal static bool TryGetScenarioDay(ScenarioScheduleTime time, out int day)
        {
            int hour;
            int minute;
            return TryNormalizeTime(time, out day, out hour, out minute);
        }

        private static bool TryNormalizeTime(ScenarioScheduleTime time, out int day, out int hour, out int minute)
        {
            day = 0;
            hour = 0;
            minute = 0;
            if (time == null)
                return false;

            long totalMinutes = ScenarioSchedulePolicyEvaluator.ToGameMinutes(time.Day, time.Hour, time.Minute);
            if (totalMinutes < (24L * 60L))
                return false;

            day = (int)(totalMinutes / (24L * 60L));
            long minutesInDay = totalMinutes % (24L * 60L);
            hour = (int)(minutesInDay / 60L);
            minute = (int)(minutesInDay % 60L);
            return true;
        }

        private static string FindInventoryOrderingWarning(ScenarioDefinition definition, List<ScenarioTimelineEntry> group)
        {
            Dictionary<string, int> operations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; group != null && i < group.Count; i++)
            {
                ScenarioTimelineEntry entry = group[i];
                if (entry == null || !string.Equals(entry.SourceKind, "scheduled_action", StringComparison.OrdinalIgnoreCase)
                    || definition == null || definition.ScheduledActions == null || entry.SourceIndex < 0 || entry.SourceIndex >= definition.ScheduledActions.Count)
                    continue;
                for (int e = 0; definition.ScheduledActions[entry.SourceIndex] != null && e < definition.ScheduledActions[entry.SourceIndex].Effects.Count; e++)
                {
                    ScenarioEffectDefinition effect = definition.ScheduledActions[entry.SourceIndex].Effects[e];
                    if (effect == null || (effect.Kind != ScenarioEffectKind.AddInventory && effect.Kind != ScenarioEffectKind.RemoveInventory) || string.IsNullOrEmpty(effect.ItemId))
                        continue;
                    int value;
                    operations.TryGetValue(effect.ItemId, out value);
                    value |= effect.Kind == ScenarioEffectKind.AddInventory ? 1 : 2;
                    operations[effect.ItemId] = value;
                }
            }

            foreach (KeyValuePair<string, int> pair in operations)
                if (pair.Value == 3)
                    return "Adds and removes " + pair.Key + " at this time; listed order can change the final stockpile.";
            return null;
        }

        private static void AppendWarning(ScenarioTimelineEntry entry, string warning)
        {
            if (entry == null || string.IsNullOrEmpty(warning) || (entry.Warning != null && entry.Warning.IndexOf(warning, StringComparison.Ordinal) >= 0))
                return;
            entry.Warning = string.IsNullOrEmpty(entry.Warning) ? warning : entry.Warning + " " + warning;
        }
    }
}
