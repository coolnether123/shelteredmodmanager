using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Events;

namespace ShelteredAPI.Networking.World
{
    internal sealed class ShelteredWorldTimeSideEffectService
    {
        private readonly IShelteredWorldTimeSideEffectSink _sink;
        private ShelteredWorldTimeProjectionSnapshot _previousProjection;

        public ShelteredWorldTimeSideEffectService()
            : this(new VanillaShelteredWorldTimeSideEffectSink(new DeferredShelteredWorldTimeAutosaveAdapter()))
        {
        }

        internal ShelteredWorldTimeSideEffectService(IShelteredWorldTimeSideEffectSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException("sink");

            _sink = sink;
        }

        public ShelteredWorldTimeSideEffectReport UpdateFromSession(ShelteredMultiplayerSessionContext context)
        {
            if (context == null
                || !context.IsMultiplayerActive
                || context.GameTimeMode == ShelteredMultiplayerGameTimeMode.Vanilla)
            {
                Reset();
                return ShelteredWorldTimeSideEffectReport.Inactive();
            }

            ShelteredWorldTimeProjectionSnapshot current = ShelteredWorldTimeProjection.Instance.Project(
                context.WorldTick,
                context.TickRate,
                ShelteredMultiplayerTimeSettings.MultiplayerDaySeconds);

            ShelteredWorldTimeSideEffectReport report = Apply(_previousProjection, current, true);
            _previousProjection = current;
            return report;
        }

        public ShelteredWorldTimeSideEffectReport Apply(
            ShelteredWorldTimeProjectionSnapshot previous,
            ShelteredWorldTimeProjectionSnapshot current,
            bool multiplayerActive)
        {
            if (!multiplayerActive || current == null)
                return ShelteredWorldTimeSideEffectReport.Inactive();

            _sink.ApplyProjectedTime(current);

            bool calendarRefreshRequested = previous == null || !current.HasSameCalendarValues(previous);
            if (calendarRefreshRequested)
                _sink.RefreshCalendar(previous, current);

            if (previous == null || current.WorldTick <= previous.WorldTick)
            {
                return BuildReport(
                    true,
                    true,
                    calendarRefreshRequested,
                    false,
                    false,
                    false,
                    false,
                    false,
                    0,
                    0,
                    previous,
                    current);
            }

            int daysCrossed = Math.Max(0, current.Day - previous.Day);
            int weeksCrossed = Math.Max(0, current.Week - previous.Week);
            bool newWeekFired = false;
            bool newDayFired = false;
            bool autosaveRequested = false;
            bool achievementNotified = false;
            bool currentDayStatNotified = false;

            if (weeksCrossed > 0)
            {
                _sink.FireNewWeek(current, weeksCrossed);
                newWeekFired = true;
            }

            if (daysCrossed > 0)
            {
                _sink.FireNewDay(current, daysCrossed);
                _sink.RequestAutosave(current, daysCrossed);
                _sink.NotifyAchievementNewDay(current);
                _sink.NotifyCurrentDayStat(current);
                newDayFired = true;
                autosaveRequested = true;
                achievementNotified = true;
                currentDayStatNotified = true;
            }

            return BuildReport(
                true,
                true,
                calendarRefreshRequested,
                newDayFired,
                newWeekFired,
                autosaveRequested,
                achievementNotified,
                currentDayStatNotified,
                daysCrossed,
                weeksCrossed,
                previous,
                current);
        }

        public void Reset()
        {
            _previousProjection = null;
        }

        private static ShelteredWorldTimeSideEffectReport BuildReport(
            bool multiplayerActive,
            bool projectionApplied,
            bool calendarRefreshRequested,
            bool newDayFired,
            bool newWeekFired,
            bool autosaveRequested,
            bool achievementNewDayNotified,
            bool currentDayStatNotified,
            int daysCrossed,
            int weeksCrossed,
            ShelteredWorldTimeProjectionSnapshot previous,
            ShelteredWorldTimeProjectionSnapshot current)
        {
            return new ShelteredWorldTimeSideEffectReport(
                multiplayerActive,
                projectionApplied,
                calendarRefreshRequested,
                newDayFired,
                newWeekFired,
                autosaveRequested,
                achievementNewDayNotified,
                currentDayStatNotified,
                daysCrossed > 1 || weeksCrossed > 1,
                daysCrossed,
                weeksCrossed,
                previous != null ? previous.WorldTick : 0,
                current != null ? current.WorldTick : 0);
        }
    }

    internal sealed class VanillaShelteredWorldTimeSideEffectSink : IShelteredWorldTimeSideEffectSink
    {
        private static readonly FieldInfo GameTimeSecondsField = AccessTools.Field(typeof(GameTime), "game_time");
        private static readonly FieldInfo CurrentMinuteField = AccessTools.Field(typeof(GameTime), "current_minute");
        private static readonly FieldInfo CurrentHourField = AccessTools.Field(typeof(GameTime), "current_hour");
        private static readonly FieldInfo CurrentDayField = AccessTools.Field(typeof(GameTime), "current_day");
        private static readonly FieldInfo CurrentWeekField = AccessTools.Field(typeof(GameTime), "current_week");
        private static readonly FieldInfo GameTimeInstanceField = AccessTools.Field(typeof(GameTime), "m_instance");
        private static readonly FieldInfo NewDayEventField = AccessTools.Field(typeof(GameTime), "newDay");
        private static readonly FieldInfo NewWeekEventField = AccessTools.Field(typeof(GameTime), "newWeek");
        private static readonly MethodInfo SendCurrentDayStatMethod = AccessTools.Method(typeof(GameTime), "SendCurrentDayStat");

        private readonly IShelteredWorldTimeAutosaveAdapter _autosaveAdapter;

        public VanillaShelteredWorldTimeSideEffectSink(IShelteredWorldTimeAutosaveAdapter autosaveAdapter)
        {
            _autosaveAdapter = autosaveAdapter ?? new DeferredShelteredWorldTimeAutosaveAdapter();
        }

        public void ApplyProjectedTime(ShelteredWorldTimeProjectionSnapshot projection)
        {
            if (projection == null)
                return;

            SetStaticField(GameTimeSecondsField, (float)projection.GameSeconds);
            SetStaticField(CurrentMinuteField, projection.Minute);
            SetStaticField(CurrentHourField, projection.Hour);
            SetStaticField(CurrentDayField, projection.Day);
            SetStaticField(CurrentWeekField, projection.Week);
        }

        public void RefreshCalendar(ShelteredWorldTimeProjectionSnapshot previous, ShelteredWorldTimeProjectionSnapshot current)
        {
            if (current == null)
                return;

            GameEvents.TryRaiseCalendarTimeProjected(current.Day, current.Week, current.Hour, current.Minute);
        }

        public void FireNewWeek(ShelteredWorldTimeProjectionSnapshot projection, int weeksCrossed)
        {
            InvokeGameTimeEvent(NewWeekEventField, "newWeek");
        }

        public void FireNewDay(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed)
        {
            InvokeGameTimeEvent(NewDayEventField, "newDay");
        }

        public void RequestAutosave(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed)
        {
            _autosaveAdapter.RequestAutosave(projection, daysCrossed);
        }

        public void NotifyAchievementNewDay(ShelteredWorldTimeProjectionSnapshot projection)
        {
            if (projection == null || AchievementManager.instance == null)
                return;

            AchievementManager.instance.OnNewDay(projection.Day);
        }

        public void NotifyCurrentDayStat(ShelteredWorldTimeProjectionSnapshot projection)
        {
            object instance = GameTimeInstanceField != null ? GameTimeInstanceField.GetValue(null) : null;
            if (instance == null || SendCurrentDayStatMethod == null)
                return;

            SendCurrentDayStatMethod.Invoke(instance, null);
        }

        private static void SetStaticField(FieldInfo field, object value)
        {
            if (field != null)
                field.SetValue(null, value);
        }

        private static void InvokeGameTimeEvent(FieldInfo eventField, string eventName)
        {
            if (eventField == null)
                return;

            GameTime.GameTimeEvent handler = eventField.GetValue(null) as GameTime.GameTimeEvent;
            if (handler == null)
                return;

            try
            {
                handler();
            }
            catch (Exception ex)
            {
                TryWarnOnce("ShelteredWorldTimeSideEffects." + eventName,
                    "Projected GameTime." + eventName + " handler failed: " + ex.Message);
            }
        }

        private static void TryWarnOnce(string key, string message)
        {
            try
            {
                MMLog.WarnOnce(key, message);
            }
            catch
            {
                // GuardrailAllow: SilentCatch - side-effect diagnostics must not affect deterministic time projection.
            }
        }
    }

    internal sealed class DeferredShelteredWorldTimeAutosaveAdapter : IShelteredWorldTimeAutosaveAdapter
    {
        public void RequestAutosave(ShelteredWorldTimeProjectionSnapshot projection, int daysCrossed)
        {
            // TODO: Replace this with a multiplayer save-owner adapter once host/client save snapshot timing is signed off.
            try
            {
                MMLog.WarnOnce(
                    "ShelteredWorldTimeProjectedAutosave.Deferred",
                    "Projected multiplayer time reached a vanilla autosave boundary, but autosave is deferred until multiplayer save ownership is signed off.");
            }
            catch
            {
                // GuardrailAllow: SilentCatch - deferred autosave diagnostics must not affect deterministic time projection.
            }
        }
    }
}
