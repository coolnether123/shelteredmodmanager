using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredScenarioEditor.Application.Authoring{
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
            if (session != null)
                session.MarkDraftChanged(section, category);
        }
    }
}
