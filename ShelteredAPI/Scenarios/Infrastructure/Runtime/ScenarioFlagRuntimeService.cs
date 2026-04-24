using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioFlagRuntimeService : IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.SetScenarioFlag;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            if (effect == null || state == null)
                return false;
            string id = effect.FlagId ?? effect.TargetId;
            if (string.IsNullOrEmpty(id))
            {
                message = "Scenario flag id is missing.";
                return false;
            }

            ScenarioRuntimeFlag flag = FindOrCreate(state, id);
            flag.Value = effect.FlagValue ?? "true";
            return true;
        }

        public bool CanEvaluate(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.ScenarioFlagSet || kind == ScenarioConditionKind.TimeReached;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (condition == null)
                return true;
            if (condition.Kind == ScenarioConditionKind.TimeReached)
                return IsDue(condition.Time);

            string id = condition.FlagId ?? condition.TargetId;
            ScenarioRuntimeFlag flag = Find(state, id);
            bool ok = flag != null && (string.IsNullOrEmpty(condition.FlagValue) || string.Equals(flag.Value, condition.FlagValue, StringComparison.OrdinalIgnoreCase));
            if (!ok)
                reason = "Scenario flag is not set: " + (id ?? string.Empty);
            return ok;
        }

        private static ScenarioRuntimeFlag Find(ScenarioRuntimeState state, string id)
        {
            for (int i = 0; state != null && state.Flags != null && i < state.Flags.Count; i++)
            {
                ScenarioRuntimeFlag flag = state.Flags[i];
                if (flag != null && string.Equals(flag.FlagId, id, StringComparison.OrdinalIgnoreCase))
                    return flag;
            }
            return null;
        }

        private static ScenarioRuntimeFlag FindOrCreate(ScenarioRuntimeState state, string id)
        {
            ScenarioRuntimeFlag flag = Find(state, id);
            if (flag != null)
                return flag;
            flag = new ScenarioRuntimeFlag();
            flag.FlagId = id;
            state.Flags.Add(flag);
            return flag;
        }

        private static bool IsDue(ScenarioScheduleTime time)
        {
            if (time == null)
                return true;
            if (GameTime.Day > time.Day)
                return true;
            if (GameTime.Day < time.Day)
                return false;
            if (GameTime.Hour > time.Hour)
                return true;
            if (GameTime.Hour < time.Hour)
                return false;
            return GameTime.Minute >= time.Minute;
        }
    }
}
