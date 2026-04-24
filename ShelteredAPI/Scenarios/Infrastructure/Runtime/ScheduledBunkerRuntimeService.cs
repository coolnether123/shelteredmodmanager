using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScheduledBunkerRuntimeService : IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.UnlockBunkerExpansion;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            string id = effect != null ? (effect.BunkerExpansionId ?? effect.TargetId) : null;
            if (string.IsNullOrEmpty(id) || state == null)
            {
                message = "Bunker expansion id is missing.";
                return false;
            }

            if (!IsUnlocked(state, id))
            {
                state.UnlockedBunker.Add(new ScenarioUnlockedBunkerRecord
                {
                    ExpansionId = id,
                    Day = GameTime.Day,
                    Hour = GameTime.Hour,
                    Minute = GameTime.Minute
                });
            }

            return true;
        }

        public bool CanEvaluate(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.BunkerExpansionUnlocked || kind == ScenarioConditionKind.TechnologyUnlocked;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            if (condition == null)
                return true;
            if (condition.Kind == ScenarioConditionKind.TechnologyUnlocked)
            {
                reason = "Technology unlock checks are declared but not connected to a stable vanilla tech adapter yet.";
                return false;
            }
            bool ok = state != null && IsUnlocked(state, condition.TargetId);
            if (!ok)
                reason = "Bunker expansion is locked: " + (condition.TargetId ?? string.Empty);
            return ok;
        }

        private static bool IsUnlocked(ScenarioRuntimeState state, string id)
        {
            for (int i = 0; state != null && state.UnlockedBunker != null && i < state.UnlockedBunker.Count; i++)
            {
                ScenarioUnlockedBunkerRecord record = state.UnlockedBunker[i];
                if (record != null && string.Equals(record.ExpansionId, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
