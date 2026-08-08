using System;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Domain.People
{
    internal static class ScenarioFamilyConfigurationPolicy
    {
        private static readonly string[] CoreStats = { "Strength", "Dexterity", "Intelligence", "Charisma", "Perception" };
        private static readonly string[] CoreConditions = { "Hunger", "Thirst", "Fatigue", "Dirtiness", "Toilet", "Stress" };

        public const int StatMin = 1;
        public const int StatMax = 20;
        public const int ConditionMin = 0;
        public const int ConditionMax = 100;
        public static string[] StatIds { get { return CoreStats; } }
        public static string[] ConditionIds { get { return CoreConditions; } }

        public static FamilyMemberConfig CreateDefaultConfig(string name, ScenarioGender gender)
        {
            FamilyMemberConfig config = new FamilyMemberConfig { Name = string.IsNullOrEmpty(name) ? "Survivor" : name, Gender = gender, ExactAge = 25 };
            EnsureCoreStats(config);
            return config;
        }

        public static void EnsureCoreStats(FamilyMemberConfig config)
        {
            if (config == null) return;
            for (int i = 0; i < CoreStats.Length; i++) EnsureStat(config, CoreStats[i], 5);
        }

        public static StatOverride EnsureStat(FamilyMemberConfig config, string statId, int fallback)
        {
            if (config == null || string.IsNullOrEmpty(statId)) return null;
            for (int i = 0; config.Stats != null && i < config.Stats.Count; i++)
            {
                StatOverride stat = config.Stats[i];
                if (stat != null && string.Equals(stat.StatId, statId, StringComparison.OrdinalIgnoreCase)) return stat;
            }
            StatOverride created = new StatOverride { StatId = statId, Value = ClampStat(fallback) };
            config.Stats.Add(created);
            return created;
        }

        public static bool TryGetConditionValue(FamilyMemberConfig config, string conditionId, out int value)
        {
            value = 0;
            int? stored = config == null ? null : GetConditionValue(config.Conditions, conditionId);
            if (!stored.HasValue) return false;
            value = ClampCondition(stored.Value);
            return true;
        }

        public static void SetConditionValue(FamilyMemberConfig config, string conditionId, int value)
        {
            if (config == null || string.IsNullOrEmpty(conditionId)) return;
            if (config.Conditions == null) config.Conditions = new FamilyMemberConditionConfig();
            int clamped = ClampCondition(value);
            if (string.Equals(conditionId, "Hunger", StringComparison.OrdinalIgnoreCase)) config.Conditions.Hunger = clamped;
            else if (string.Equals(conditionId, "Thirst", StringComparison.OrdinalIgnoreCase)) config.Conditions.Thirst = clamped;
            else if (string.Equals(conditionId, "Fatigue", StringComparison.OrdinalIgnoreCase)) config.Conditions.Fatigue = clamped;
            else if (string.Equals(conditionId, "Dirtiness", StringComparison.OrdinalIgnoreCase)) config.Conditions.Dirtiness = clamped;
            else if (string.Equals(conditionId, "Toilet", StringComparison.OrdinalIgnoreCase)) config.Conditions.Toilet = clamped;
            else if (string.Equals(conditionId, "Stress", StringComparison.OrdinalIgnoreCase)) config.Conditions.Stress = clamped;
        }

        public static int ClampStat(int value) { return value < StatMin ? StatMin : (value > StatMax ? StatMax : value); }
        public static int ClampCondition(int value) { return value < ConditionMin ? ConditionMin : (value > ConditionMax ? ConditionMax : value); }

        private static int? GetConditionValue(FamilyMemberConditionConfig conditions, string conditionId)
        {
            if (conditions == null || string.IsNullOrEmpty(conditionId)) return null;
            if (string.Equals(conditionId, "Hunger", StringComparison.OrdinalIgnoreCase)) return conditions.Hunger;
            if (string.Equals(conditionId, "Thirst", StringComparison.OrdinalIgnoreCase)) return conditions.Thirst;
            if (string.Equals(conditionId, "Fatigue", StringComparison.OrdinalIgnoreCase)) return conditions.Fatigue;
            if (string.Equals(conditionId, "Dirtiness", StringComparison.OrdinalIgnoreCase)) return conditions.Dirtiness;
            if (string.Equals(conditionId, "Toilet", StringComparison.OrdinalIgnoreCase)) return conditions.Toilet;
            if (string.Equals(conditionId, "Stress", StringComparison.OrdinalIgnoreCase)) return conditions.Stress;
            return null;
        }
    }
}
