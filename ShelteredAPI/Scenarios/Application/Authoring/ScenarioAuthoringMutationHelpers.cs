using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal static class ScenarioAuthoringActionParser
    {
        public static bool TryIndex(string actionId, string prefix, int count, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            if (!int.TryParse(actionId.Substring(prefix.Length), out index))
                return false;
            return index >= 0 && index < count;
        }

        public static bool TrySignedIndex(string actionId, string prefix, int count, out int index, out int delta)
        {
            index = -1;
            delta = 0;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = actionId.Substring(prefix.Length).Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[0], out index) || !int.TryParse(parts[1], out delta))
                return false;
            return index >= 0 && index < count && delta != 0;
        }

        public static bool TryPairIndex(string actionId, string prefix, int count, out int first, out int second)
        {
            first = -1;
            second = -1;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string[] parts = actionId.Substring(prefix.Length).Split('.');
            if (parts.Length != 2 || !int.TryParse(parts[0], out first) || !int.TryParse(parts[1], out second))
                return false;
            return first >= 0 && first < count && second >= 0;
        }
    }

    internal static class ScenarioAuthoringSchedule
    {
        public static ScenarioScheduleTime NextTime()
        {
            ScenarioScheduleTime time = new ScenarioScheduleTime();
            try
            {
                time.Day = Math.Max(1, GameTime.Day + 1);
                time.Hour = Clamp(GameTime.Hour, 0, 23);
                time.Minute = Clamp(GameTime.Minute, 0, 59);
            }
            catch
            {
                time.Day = 2;
                time.Hour = 8;
                time.Minute = 0;
            }
            return time;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            return value > max ? max : value;
        }

        public static string Format(ScenarioScheduleTime time)
        {
            if (time == null)
                return "unscheduled";
            return "day " + time.Day + " at " + time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2");
        }
    }

    internal static class ScenarioAuthoringMutation
    {
        public static void MarkDirty(ScenarioEditorSession session, ScenarioDirtySection section, ScenarioEditCategory category)
        {
            if (!session.DirtyFlags.Contains(section))
                session.DirtyFlags.Add(section);
            session.CurrentEditCategory = category;
            session.HasAppliedToCurrentWorld = true;
        }
    }
}
