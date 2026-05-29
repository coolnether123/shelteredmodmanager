namespace ParalivesAPI.Core
{
    public sealed class ParalivesTimeState
    {
        public float TotalMinutes { get; set; }

        public float LastTotalMinutes { get; set; }

        public float DeltaMinute { get; set; }

        public int Day { get; set; }

        public int DayOfWeek { get; set; }

        public int Week { get; set; }

        public float MinutesOfDay { get; set; }

        public int TimeSpeedIndex { get; set; }

        public bool IsPaused { get; set; }

        public bool IsPausedByPlayer { get; set; }

        public bool IsPausedByUi { get; set; }

        public float PauseStartTime { get; set; }

        public float PauseTimeDuration { get; set; }

        public int DateTimeNowTicks { get; set; }
    }

    public sealed class ParalivesTimeFacade
    {
        internal ParalivesTimeFacade()
        {
        }

        public float TotalMinutes
        {
            get { return global::ParaTime.TotalMinutes; }
        }

        public int Day
        {
            get { return global::ParaTime.Day; }
        }

        public int DayOfWeek
        {
            get { return global::ParaTime.DayOfWeek; }
        }

        public int Week
        {
            get { return global::ParaTime.GetTotalWeekIndex(global::ParaTime.TotalMinutes); }
        }

        public float MinutesOfDay
        {
            get { return global::ParaTime.MinutesOfDay; }
        }

        public bool IsPaused
        {
            get { return global::ParaTime.IsPaused; }
        }

        public ParalivesTimeState ReadState()
        {
            return new ParalivesTimeState
            {
                TotalMinutes = global::ParaTime.TotalMinutes,
                LastTotalMinutes = global::ParaTime.LastTotalMinutes,
                DeltaMinute = global::ParaTime.DeltaMinute,
                Day = global::ParaTime.Day,
                DayOfWeek = global::ParaTime.DayOfWeek,
                Week = global::ParaTime.GetTotalWeekIndex(global::ParaTime.TotalMinutes),
                MinutesOfDay = global::ParaTime.MinutesOfDay,
                TimeSpeedIndex = global::ParaTime.TimeSpeedIndex,
                IsPaused = global::ParaTime.IsPaused,
                IsPausedByPlayer = global::ParaTime.IsPausedByPlayer,
                IsPausedByUi = global::ParaTime.IsPausedByUI,
                PauseStartTime = global::ParaTime.PauseStartTime,
                PauseTimeDuration = global::ParaTime.PauseTimeDuration,
                DateTimeNowTicks = global::ParaTime.DateTimeNowTicks
            };
        }

        public void ApplyState(ParalivesTimeState state)
        {
            ApplyState(state, true);
        }

        public void ApplyState(ParalivesTimeState state, bool updateDeltaMinute)
        {
            if (state == null)
                return;

            float previousTotal = global::ParaTime.TotalMinutes;
            global::ParaTime.LastTotalMinutes = state.LastTotalMinutes;
            global::ParaTime.TotalMinutes = state.TotalMinutes;
            global::ParaTime.TimeSpeedIndex = state.TimeSpeedIndex;
            global::ParaTime.IsPausedByPlayer = state.IsPausedByPlayer;
            global::ParaTime.IsPausedByUI = state.IsPausedByUi;
            global::ParaTime.PauseStartTime = state.PauseStartTime;
            global::ParaTime.PauseTimeDuration = state.PauseTimeDuration;
            global::ParaTime.DateTimeNowTicks = state.DateTimeNowTicks;
            global::ParaTime.DeltaMinute = updateDeltaMinute
                ? state.TotalMinutes - previousTotal
                : state.DeltaMinute;
        }

        public void SetPausedByPlayer(bool paused)
        {
            global::ParaTime.IsPausedByPlayer = paused;
        }

        public void SetPausedByUi(bool paused)
        {
            global::ParaTime.IsPausedByUI = paused;
        }

        public void SetTimeSpeed(int timeSpeedIndex)
        {
            global::ParaTime.SetTimeSpeed(timeSpeedIndex);
        }
    }
}
