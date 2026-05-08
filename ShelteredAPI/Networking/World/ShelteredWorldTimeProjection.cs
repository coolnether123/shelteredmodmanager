using System;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldTimeProjection
    {
        private static readonly ShelteredWorldTimeProjection _instance = new ShelteredWorldTimeProjection();

        public static ShelteredWorldTimeProjection Instance
        {
            get { return _instance; }
        }

        public ShelteredWorldTimeProjectionSnapshot Project(long worldTick, int tickRate, float dayLengthSeconds)
        {
            if (worldTick < 0)
                worldTick = 0;
            if (tickRate <= 0)
                tickRate = ShelteredMultiplayerWorldClock.DefaultTickRate;
            if (dayLengthSeconds <= 0f)
                dayLengthSeconds = ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds;

            double ticksPerDay = tickRate * (double)dayLengthSeconds;
            if (ticksPerDay <= 0d)
                ticksPerDay = ShelteredMultiplayerWorldClock.DefaultTickRate
                    * (double)ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds;

            double elapsedGameSeconds =
                worldTick / ticksPerDay * ShelteredMultiplayerTimeSettings.GameSecondsPerDay;
            long completedDays = (long)Math.Floor(elapsedGameSeconds / ShelteredMultiplayerTimeSettings.GameSecondsPerDay);
            double ticksIntoDay = worldTick - completedDays * ticksPerDay;
            if (ticksIntoDay < 0d)
                ticksIntoDay = 0d;

            double gameSeconds = ShelteredMultiplayerTimeSettings.VanillaDayStartGameSeconds
                + ticksIntoDay / ticksPerDay * ShelteredMultiplayerTimeSettings.GameSecondsPerDay;
            while (gameSeconds >= ShelteredMultiplayerTimeSettings.GameSecondsPerDay)
                gameSeconds -= ShelteredMultiplayerTimeSettings.GameSecondsPerDay;

            int wholeGameSeconds = ((int)Math.Floor(gameSeconds))
                % (int)ShelteredMultiplayerTimeSettings.GameSecondsPerDay;
            int hour = wholeGameSeconds / 3600;
            int minute = (wholeGameSeconds - hour * 3600) / 60;
            int day = ToCalendarDay(completedDays);
            int week = ToCalendarWeek(day);
            long previousTick = worldTick > 0 ? worldTick - 1 : 0;
            long previousCompletedDays = (long)Math.Floor(previousTick / ticksPerDay);
            int previousDay = ToCalendarDay(previousCompletedDays);
            int previousWeek = ToCalendarWeek(previousDay);
            bool dayRollover = worldTick > 0 && day > previousDay;

            return new ShelteredWorldTimeProjectionSnapshot(
                worldTick,
                tickRate,
                dayLengthSeconds,
                elapsedGameSeconds,
                gameSeconds,
                minute,
                hour,
                day,
                week,
                previousDay,
                previousWeek,
                dayRollover,
                previousWeek != week,
                gameSeconds / ShelteredMultiplayerTimeSettings.GameSecondsPerDay);
        }

        private static int ToCalendarDay(long completedDays)
        {
            if (completedDays >= int.MaxValue - 1L)
                return int.MaxValue;

            return (int)completedDays + 1;
        }

        private static int ToCalendarWeek(int day)
        {
            if (day <= 1)
                return 1;

            return ((day - 1) / 7) + 1;
        }
    }

    internal sealed class ShelteredWorldTimeProjectionSnapshot
    {
        public ShelteredWorldTimeProjectionSnapshot(
            long worldTick,
            int tickRate,
            float dayLengthSeconds,
            double elapsedGameSeconds,
            double gameSeconds,
            int minute,
            int hour,
            int day,
            int week,
            int previousDay,
            int previousWeek,
            bool dayRollover,
            bool weekRollover,
            double dayProgress)
        {
            WorldTick = worldTick;
            TickRate = tickRate;
            DayLengthSeconds = dayLengthSeconds;
            ElapsedGameSeconds = elapsedGameSeconds;
            GameSeconds = gameSeconds;
            Minute = minute;
            Hour = hour;
            Day = day;
            Week = week;
            PreviousDay = previousDay;
            PreviousWeek = previousWeek;
            DayRollover = dayRollover;
            WeekRollover = weekRollover;
            DayProgress = dayProgress;
        }

        public long WorldTick { get; private set; }
        public int TickRate { get; private set; }
        public float DayLengthSeconds { get; private set; }
        public double ElapsedGameSeconds { get; private set; }
        public double GameSeconds { get; private set; }
        public int Minute { get; private set; }
        public int Hour { get; private set; }
        public int Day { get; private set; }
        public int Week { get; private set; }
        public int PreviousDay { get; private set; }
        public int PreviousWeek { get; private set; }
        public bool DayRollover { get; private set; }
        public bool WeekRollover { get; private set; }
        public double DayProgress { get; private set; }

        public bool HasSameCalendarValues(ShelteredWorldTimeProjectionSnapshot other)
        {
            return other != null
                && Day == other.Day
                && Week == other.Week
                && Hour == other.Hour
                && Minute == other.Minute;
        }
    }
}
