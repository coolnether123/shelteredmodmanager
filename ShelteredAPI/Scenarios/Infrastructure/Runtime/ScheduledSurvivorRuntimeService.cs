using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
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
            if (condition == null)
            {
                reason = "Survivor condition was missing.";
                return false;
            }

            List<FamilyMember> members = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            if (members == null)
            {
                reason = "FamilyManager is not ready.";
                return false;
            }

            FamilyMember target = FindPresentSurvivor(members, condition.TargetId);
            if (target == null)
            {
                reason = "Survivor not present: " + (condition != null ? condition.TargetId : string.Empty);
                return false;
            }

            if (condition.Kind == ScenarioConditionKind.SurvivorPresent)
                return true;

            if (condition.Kind == ScenarioConditionKind.SurvivorStatCheck)
                return IsStatSatisfied(target, condition, out reason);

            if (condition.Kind == ScenarioConditionKind.SurvivorTraitCheck)
                return IsTraitSatisfied(target, condition, out reason);

            reason = "Unsupported survivor condition: " + condition.Kind;
            return false;
        }

        private static FamilyMember FindPresentSurvivor(List<FamilyMember> members, string targetId)
        {
            for (int i = 0; i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member != null && string.Equals(member.firstName, targetId, StringComparison.OrdinalIgnoreCase))
                    return member;
            }

            return null;
        }

        private static bool IsStatSatisfied(FamilyMember member, ScenarioConditionRef condition, out string reason)
        {
            reason = null;
            BaseStats.StatType statType;
            if (!TryParseStatType(condition.StatId, out statType))
            {
                reason = "Unknown survivor stat: " + (condition.StatId ?? string.Empty);
                return false;
            }

            BaseStat stat = member.BaseStats != null ? member.BaseStats.GetStatByEnum(statType) : null;
            if (stat == null)
            {
                reason = "Survivor stat is unavailable: " + statType;
                return false;
            }

            int actual = stat.Level;
            int expected = condition.StatValue;
            if (CompareInt(actual, expected, condition.Comparison))
                return true;

            reason = "Survivor stat check failed: " + member.firstName + " " + statType + " " + actual + " "
                + NormalizeComparison(condition.Comparison) + " " + expected;
            return false;
        }

        private static bool IsTraitSatisfied(FamilyMember member, ScenarioConditionRef condition, out string reason)
        {
            reason = null;
            if (member.traits == null)
            {
                reason = "Survivor traits are unavailable: " + member.firstName;
                return false;
            }

            string traitId = TrimToNull(condition.TraitId);
            if (traitId == null)
            {
                reason = "Survivor trait id was missing.";
                return false;
            }

            bool hasTrait;
            if (!TryHasTrait(member.traits, traitId, out hasTrait))
            {
                reason = "Unknown survivor trait: " + traitId;
                return false;
            }

            bool expectedPresent = !IsNegativeTraitComparison(condition.Comparison);
            if (hasTrait == expectedPresent)
                return true;

            reason = "Survivor trait check failed: " + member.firstName + " " + traitId
                + (expectedPresent ? " missing." : " present.");
            return false;
        }

        private static bool TryHasTrait(Traits traits, string traitId, out bool hasTrait)
        {
            hasTrait = false;

            Traits.Strength strength;
            bool explicitStrength = HasTraitPrefix(traitId, "Strength:");
            bool explicitWeakness = HasTraitPrefix(traitId, "Weakness:");
            if (!explicitWeakness && TryParseStrengthTrait(traitId, out strength))
            {
                hasTrait = traits.HasStrength(strength);
                return true;
            }

            Traits.Weakness weakness;
            if (!explicitStrength && TryParseWeaknessTrait(traitId, out weakness))
            {
                hasTrait = traits.HasWeakness(weakness);
                return true;
            }

            return false;
        }

        private static bool CompareInt(int actual, int expected, string comparison)
        {
            string normalized = NormalizeComparison(comparison);
            if (normalized == ">" || normalized == "gt" || normalized == "greater" || normalized == "greaterthan")
                return actual > expected;
            if (normalized == ">=" || normalized == "gte" || normalized == "ge" || normalized == "atleast" || normalized == "minimum")
                return actual >= expected;
            if (normalized == "<" || normalized == "lt" || normalized == "less" || normalized == "lessthan")
                return actual < expected;
            if (normalized == "<=" || normalized == "lte" || normalized == "le" || normalized == "atmost" || normalized == "maximum")
                return actual <= expected;
            if (normalized == "!=" || normalized == "<>" || normalized == "ne" || normalized == "notequals" || normalized == "not")
                return actual != expected;
            return actual == expected;
        }

        private static string NormalizeComparison(string comparison)
        {
            string trimmed = TrimToNull(comparison);
            return trimmed == null ? "==" : trimmed.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        private static bool IsNegativeTraitComparison(string comparison)
        {
            string normalized = NormalizeComparison(comparison);
            return normalized == "!=" || normalized == "<>" || normalized == "ne" || normalized == "notequals"
                || normalized == "not" || normalized == "absent" || normalized == "false";
        }

        private static bool TryParseStatType(string value, out BaseStats.StatType statType)
        {
            statType = BaseStats.StatType.Max;
            string trimmed = TrimToNull(value);
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(BaseStats.StatType), trimmed, true);
                statType = (BaseStats.StatType)parsed;
                return statType != BaseStats.StatType.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseStrengthTrait(string value, out Traits.Strength strength)
        {
            strength = Traits.Strength.Max;
            string trimmed = TrimTraitPrefix(value, "Strength:");
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(Traits.Strength), trimmed, true);
                strength = (Traits.Strength)parsed;
                return strength != Traits.Strength.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseWeaknessTrait(string value, out Traits.Weakness weakness)
        {
            weakness = Traits.Weakness.Max;
            string trimmed = TrimTraitPrefix(value, "Weakness:");
            if (trimmed == null)
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(Traits.Weakness), trimmed, true);
                weakness = (Traits.Weakness)parsed;
                return weakness != Traits.Weakness.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasTraitPrefix(string value, string prefix)
        {
            string trimmed = TrimToNull(value);
            return trimmed != null && trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string TrimTraitPrefix(string value, string prefix)
        {
            string trimmed = TrimToNull(value);
            if (trimmed == null)
                return null;

            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return TrimToNull(trimmed.Substring(prefix.Length));

            return trimmed.IndexOf(':') >= 0 ? null : trimmed;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
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
