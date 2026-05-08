namespace ShelteredAPI.Networking.World
{
    internal interface IShelteredWorldTimeSideEffectSink
    {
        void ApplyProjectedTime(ShelteredWorldTimeProjectionSnapshot projection);
        void RefreshCalendar(ShelteredWorldTimeProjectionSnapshot previous, ShelteredWorldTimeProjectionSnapshot current);
        void FireNewWeek(ShelteredWorldTimeProjectionSnapshot projection, int weeksCrossed);
        void FireNewDay(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed);
        void RequestAutosave(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed);
        void NotifyAchievementNewDay(ShelteredWorldTimeProjectionSnapshot projection);
        void NotifyCurrentDayStat(ShelteredWorldTimeProjectionSnapshot projection);
    }

    internal interface IShelteredWorldTimeAutosaveAdapter
    {
        void RequestAutosave(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed);
    }

    internal sealed class ShelteredWorldTimeSideEffectReport
    {
        public ShelteredWorldTimeSideEffectReport(
            bool multiplayerActive,
            bool projectionApplied,
            bool calendarRefreshRequested,
            bool newDayFired,
            bool newWeekFired,
            bool autosaveRequested,
            bool achievementNewDayNotified,
            bool currentDayStatNotified,
            bool coalescedBoundaryEvents,
            int daysCrossed,
            int weeksCrossed,
            long previousWorldTick,
            long currentWorldTick)
        {
            MultiplayerActive = multiplayerActive;
            ProjectionApplied = projectionApplied;
            CalendarRefreshRequested = calendarRefreshRequested;
            NewDayFired = newDayFired;
            NewWeekFired = newWeekFired;
            AutosaveRequested = autosaveRequested;
            AchievementNewDayNotified = achievementNewDayNotified;
            CurrentDayStatNotified = currentDayStatNotified;
            CoalescedBoundaryEvents = coalescedBoundaryEvents;
            DaysCrossed = daysCrossed;
            WeeksCrossed = weeksCrossed;
            PreviousWorldTick = previousWorldTick;
            CurrentWorldTick = currentWorldTick;
        }

        public bool MultiplayerActive { get; private set; }
        public bool ProjectionApplied { get; private set; }
        public bool CalendarRefreshRequested { get; private set; }
        public bool NewDayFired { get; private set; }
        public bool NewWeekFired { get; private set; }
        public bool AutosaveRequested { get; private set; }
        public bool AchievementNewDayNotified { get; private set; }
        public bool CurrentDayStatNotified { get; private set; }
        public bool CoalescedBoundaryEvents { get; private set; }
        public int DaysCrossed { get; private set; }
        public int WeeksCrossed { get; private set; }
        public long PreviousWorldTick { get; private set; }
        public long CurrentWorldTick { get; private set; }

        public static ShelteredWorldTimeSideEffectReport Inactive()
        {
            return new ShelteredWorldTimeSideEffectReport(
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                0);
        }
    }
}
