using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScheduledSurvivorRuntimeService : IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.SpawnFutureSurvivor;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            bool askToJoin = ReadBool(effect, "askToJoin", false);
            FutureSurvivorDefinition survivor = FindFutureSurvivor(definition, effect.SurvivorId ?? effect.TargetId);
            if (survivor == null)
            {
                message = "Future survivor definition was not found.";
                return false;
            }

            if (askToJoin || survivor.AskToJoin)
                return ScenarioFamilyMemberFactory.ScheduleRecruit(survivor.Survivor, 0f, out message);

            FamilyMember spawned;
            return ScenarioFamilyMemberFactory.Spawn(survivor.Survivor, out spawned, out message);
        }

        public bool CanEvaluate(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.SurvivorPresent
                || kind == ScenarioConditionKind.SurvivorStatCheck
                || kind == ScenarioConditionKind.SurvivorTraitCheck;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            List<FamilyMember> members = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            if (members == null)
            {
                reason = "FamilyManager is not ready.";
                return false;
            }

            for (int i = 0; i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member != null && string.Equals(member.firstName, condition.TargetId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            reason = "Survivor not present: " + (condition != null ? condition.TargetId : string.Empty);
            return false;
        }

        private static FutureSurvivorDefinition FindFutureSurvivor(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                if (survivor != null && string.Equals(survivor.Id, id, StringComparison.OrdinalIgnoreCase))
                    return survivor;
            }
            return null;
        }

        private static bool ReadBool(ScenarioEffectDefinition effect, string key, bool fallback)
        {
            for (int i = 0; effect != null && effect.Properties != null && i < effect.Properties.Count; i++)
            {
                ScenarioProperty property = effect.Properties[i];
                bool parsed;
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase) && bool.TryParse(property.Value, out parsed))
                    return parsed;
            }
            return fallback;
        }

    }
}
