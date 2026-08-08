using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Application.Runtime
{
    internal sealed class ScenarioTestConsoleService
    {
        internal const int MaximumJumpHours = 72;
        private readonly ScenarioTestTimeAdvanceService _time;
        private readonly ScenarioPreviewSessionHost _previewSession;
        private string _activeStoryStageId;

        public ScenarioTestConsoleService(
            ScenarioTestTimeAdvanceService time,
            ScenarioPreviewSessionHost previewSession)
        {
            _time = time;
            _previewSession = previewSession;
        }

        public string ActiveStoryStageId { get { return _activeStoryStageId; } }

        public void SetConsoleVisible(bool visible)
        {
            _previewSession.SetExecutionLogging(visible);
        }

        public bool TryFireNow(ScenarioDefinition definition, string actionId, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(actionId))
            {
                message = "No authored runtime element was selected.";
                return false;
            }

            return _previewSession.TryFireRuntimeElement(actionId, out message);
        }

        public bool TryAdvanceOneHour(out string message)
        {
            return AdvanceMinutes(60, out message);
        }

        public bool TryAdvanceOneDay(out string message)
        {
            return AdvanceMinutes(24 * 60, out message);
        }

        public bool TryRunUntilNextAuthoredEvent(out string message)
        {
            int minutes;
            if (!_previewSession.TryGetMinutesUntilNextAuthoredEvent(MaximumJumpHours * 60, out minutes))
            {
                message = "No authored event falls within the next " + MaximumJumpHours + " hours.";
                return false;
            }
            return AdvanceMinutes(minutes, out message);
        }

        public bool TryJumpToStoryStage(ScenarioDefinition definition, string stageId, out string message)
        {
            // ScenarioDef exposes stage progression only through the live encounter/quest flow.  Mutating it
            // in place is not safe, so this records the requested test focus instead of corrupting a save.
            if (!HasStoryStage(definition, stageId))
            {
                message = "Story stage was not found.";
                return false;
            }
            _activeStoryStageId = stageId;
            message = "Story stage '" + stageId + "' selected for observation. Direct vanilla encounter jumping is disabled until its runtime seam is live-verified.";
            return true;
        }

        private bool AdvanceMinutes(int minutes, out string message)
        {
            if (_time == null || minutes <= 0 || minutes > MaximumJumpHours * 60)
            {
                message = "Time control request is outside the safe " + MaximumJumpHours + " hour limit.";
                return false;
            }
            bool advanced = _time.TryAdvanceMinutes(minutes, out message);
            if (advanced)
            {
                _previewSession.NotifyGameTimeChanged();
            }
            return advanced;
        }

        private static bool HasStoryStage(ScenarioDefinition definition, string stageId)
        {
            for (int i = 0; definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null && i < definition.ScenarioFlow.Stages.Count; i++)
                if (definition.ScenarioFlow.Stages[i] != null && string.Equals(definition.ScenarioFlow.Stages[i].Id, stageId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

    }

    /// <summary>Uses vanilla GameTime's persisted clock fields and its new-day delegate, never Time.timeScale.</summary>
    internal sealed class ScenarioTestTimeAdvanceService
    {
        internal const int MaximumMinutesPerRequest = ScenarioTestConsoleService.MaximumJumpHours * 60;
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly FieldInfo GameTimeField = typeof(GameTime).GetField("game_time", StaticPrivate);
        private static readonly FieldInfo MinuteField = typeof(GameTime).GetField("current_minute", StaticPrivate);
        private static readonly FieldInfo HourField = typeof(GameTime).GetField("current_hour", StaticPrivate);
        private static readonly FieldInfo DayField = typeof(GameTime).GetField("current_day", StaticPrivate);
        private static readonly FieldInfo WeekField = typeof(GameTime).GetField("current_week", StaticPrivate);
        private static readonly FieldInfo NewDayField = typeof(GameTime).GetField("newDay", StaticPrivate);

        public bool TryAdvanceMinutes(int minutes, out string message)
        {
            if (minutes <= 0 || minutes > MaximumMinutesPerRequest || GameTimeField == null || MinuteField == null || HourField == null || DayField == null)
            {
                message = "Vanilla GameTime advance seam is unavailable or request exceeds " + MaximumMinutesPerRequest + " minutes.";
                return false;
            }

            try
            {
                int remaining = minutes;
                while (remaining > 0)
                {
                    int increment = Math.Min(60, remaining); // bounded hourly increments; no fast-forward time scale.
                    AdvanceOneIncrement(increment);
                    remaining -= increment;
                }
                message = "Advanced " + minutes + " game minute(s) through bounded vanilla clock increments.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Vanilla GameTime advance failed: " + ex.Message;
                return false;
            }
        }

        private static void AdvanceOneIncrement(int minutes)
        {
            float previous = (float)GameTimeField.GetValue(null);
            float next = previous + (minutes * 60f);
            bool crossedDay = previous < 21600f && next >= 21600f;
            if (next >= 86400f)
                next -= 86400f;

            GameTimeField.SetValue(null, next);
            int seconds = (int)next % 86400;
            HourField.SetValue(null, seconds / 3600);
            MinuteField.SetValue(null, (seconds % 3600) / 60);
            if (!crossedDay)
                return;

            int day = GameTime.Day + 1;
            DayField.SetValue(null, day);
            if (day % 7 == 1 && WeekField != null)
                WeekField.SetValue(null, GameTime.Week + 1);
            Delegate handler = NewDayField != null ? NewDayField.GetValue(null) as Delegate : null;
            if (handler != null)
                handler.DynamicInvoke(new object[0]);
        }
    }
}
