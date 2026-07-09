using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ShelteredAPI.Content;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.UI.Internal.Settings;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class ScenarioFamilyMemberFactory
    {
        private static readonly FieldInfo NpcVisitPendingSpawnsField = typeof(NpcVisitManager).GetField("m_pendingSpawns", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly Type NpcVisitSpawnInfoType = typeof(NpcVisitManager).GetNestedType("SpawnInfo", BindingFlags.NonPublic);
        private static readonly FieldInfo SpawnInfoTypeField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("type", BindingFlags.Public | BindingFlags.Instance) : null;
        private static readonly FieldInfo SpawnInfoAttributesField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("npcAttributes", BindingFlags.Public | BindingFlags.Instance) : null;
        private static readonly FieldInfo SpawnInfoTimerField = NpcVisitSpawnInfoType != null ? NpcVisitSpawnInfoType.GetField("spawnTimer", BindingFlags.Public | BindingFlags.Instance) : null;

        private static readonly string[] CoreStats = new[]
        {
            "Strength",
            "Dexterity",
            "Intelligence",
            "Charisma",
            "Perception"
        };

        private static readonly string[] CoreConditions = new[]
        {
            "Hunger",
            "Thirst",
            "Fatigue",
            "Dirtiness",
            "Toilet",
            "Stress"
        };

        public const int StatMin = 1;
        public const int StatMax = 20;
        public const int ConditionMin = 0;
        public const int ConditionMax = 100;

        public static string[] StatIds
        {
            get { return CoreStats; }
        }

        public static string[] ConditionIds
        {
            get { return CoreConditions; }
        }

        public static FamilyMemberConfig CreateDefaultConfig(string name, ScenarioGender gender)
        {
            FamilyMemberConfig config = new FamilyMemberConfig();
            config.Name = string.IsNullOrEmpty(name) ? "Survivor" : name;
            config.Gender = gender;
            config.ExactAge = 25;
            EnsureCoreStats(config);
            return config;
        }

        public static void EnsureCoreStats(FamilyMemberConfig config)
        {
            if (config == null)
                return;

            for (int i = 0; i < CoreStats.Length; i++)
                EnsureStat(config, CoreStats[i], 5);
        }

        public static StatOverride EnsureStat(FamilyMemberConfig config, string statId, int fallback)
        {
            if (config == null || string.IsNullOrEmpty(statId))
                return null;

            for (int i = 0; config.Stats != null && i < config.Stats.Count; i++)
            {
                StatOverride stat = config.Stats[i];
                if (stat != null && string.Equals(stat.StatId, statId, StringComparison.OrdinalIgnoreCase))
                    return stat;
            }

            StatOverride created = new StatOverride
            {
                StatId = statId,
                Value = ClampStat(fallback)
            };
            config.Stats.Add(created);
            return created;
        }

        public static bool TryGetConditionValue(FamilyMemberConfig config, string conditionId, out int value)
        {
            value = 0;
            if (config == null || config.Conditions == null || string.IsNullOrEmpty(conditionId))
                return false;

            int? stored = GetConditionValue(config.Conditions, conditionId);
            if (!stored.HasValue)
                return false;

            value = ClampCondition(stored.Value);
            return true;
        }

        public static void SetConditionValue(FamilyMemberConfig config, string conditionId, int value)
        {
            if (config == null || string.IsNullOrEmpty(conditionId))
                return;

            if (config.Conditions == null)
                config.Conditions = new FamilyMemberConditionConfig();

            int clamped = ClampCondition(value);
            if (string.Equals(conditionId, "Hunger", StringComparison.OrdinalIgnoreCase))
                config.Conditions.Hunger = clamped;
            else if (string.Equals(conditionId, "Thirst", StringComparison.OrdinalIgnoreCase))
                config.Conditions.Thirst = clamped;
            else if (string.Equals(conditionId, "Fatigue", StringComparison.OrdinalIgnoreCase))
                config.Conditions.Fatigue = clamped;
            else if (string.Equals(conditionId, "Dirtiness", StringComparison.OrdinalIgnoreCase))
                config.Conditions.Dirtiness = clamped;
            else if (string.Equals(conditionId, "Toilet", StringComparison.OrdinalIgnoreCase))
                config.Conditions.Toilet = clamped;
            else if (string.Equals(conditionId, "Stress", StringComparison.OrdinalIgnoreCase))
                config.Conditions.Stress = clamped;
        }

        public static FamilySpawner.CharacterAttributes CreateAttributes(FamilyMemberConfig config)
        {
            FamilySpawner.CharacterAttributes attributes = new FamilySpawner.CharacterAttributes();
            if (config == null)
                return attributes;

            attributes.m_firstName = string.IsNullOrEmpty(config.Name) ? "Survivor" : config.Name;
            attributes.m_lastName = string.Empty;
            attributes.m_meshId = ResolveMeshId(config);

            if (config.Appearance != null)
            {
                if (!string.IsNullOrEmpty(config.Appearance.HeadTextureId))
                    attributes.m_headTexture = config.Appearance.HeadTextureId;
                if (!string.IsNullOrEmpty(config.Appearance.TorsoTextureId))
                    attributes.m_torsoTexture = config.Appearance.TorsoTextureId;
                if (!string.IsNullOrEmpty(config.Appearance.LegTextureId))
                    attributes.m_legTexture = config.Appearance.LegTextureId;
                ApplyColor(config.Appearance.HairColorHex, delegate(Color color) { attributes.m_hairColor = color; });
                ApplyColor(config.Appearance.SkinColorHex, delegate(Color color) { attributes.m_skinColor = color; });
                ApplyColor(config.Appearance.ShirtColorHex, delegate(Color color) { attributes.m_shirtColor = color; });
                ApplyColor(config.Appearance.PantsColorHex, delegate(Color color) { attributes.m_pantsColor = color; });
            }

            for (int i = 0; config.Stats != null && i < config.Stats.Count; i++)
                ApplyStat(attributes, config.Stats[i]);

            for (int i = 0; config.Traits != null && i < config.Traits.Count; i++)
                ApplyTrait(attributes, config.Traits[i]);

            SanitizeTraitPairs(attributes, config);
            return attributes;
        }

        public static bool Spawn(FamilyMemberConfig config, out FamilyMember spawned, out string message)
        {
            spawned = null;
            message = null;
            if (config == null)
            {
                message = "Survivor configuration was missing.";
                return false;
            }

            if ((UnityEngine.Object)FamilySpawner.instance == (UnityEngine.Object)null)
            {
                message = "FamilySpawner is not ready; survivor spawn skipped.";
                return false;
            }

            List<FamilyMember> before = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            int previousCount = before != null ? before.Count : 0;

            FamilySpawner.SetPendingFamilySpawn(CreateAttributes(config));
            FamilySpawner.ForceSpawnPending();

            List<FamilyMember> after = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            if (after != null && after.Count > previousCount)
            {
                spawned = after[after.Count - 1];
                ApplyConditions(spawned, config);
                message = "Spawned survivor '" + (config.Name ?? "Survivor") + "'.";
                return true;
            }

            message = "FamilySpawner did not report a new survivor after spawn.";
            return false;
        }

        public static bool ScheduleRecruit(FamilyMemberConfig config, float arrivalDelay, out string message)
        {
            FamilySpawner.CharacterAttributes queuedAttributes;
            return ScheduleRecruit(config, arrivalDelay, out queuedAttributes, out message);
        }

        public static bool ScheduleRecruit(
            FamilyMemberConfig config,
            float arrivalDelay,
            out FamilySpawner.CharacterAttributes queuedAttributes,
            out string message)
        {
            queuedAttributes = null;
            message = null;
            if (config == null)
            {
                message = "Recruit configuration was missing.";
                return false;
            }

            if ((UnityEngine.Object)NpcVisitManager.Instance == (UnityEngine.Object)null)
            {
                message = "NpcVisitManager is not ready; recruit arrival skipped.";
                return false;
            }

            if (NpcVisitPendingSpawnsField == null
                || NpcVisitSpawnInfoType == null
                || SpawnInfoTypeField == null
                || SpawnInfoAttributesField == null
                || SpawnInfoTimerField == null)
            {
                message = "Sheltered recruit spawn internals are unavailable.";
                return false;
            }

            IList pendingSpawns = NpcVisitPendingSpawnsField.GetValue(NpcVisitManager.Instance) as IList;
            if (pendingSpawns == null)
            {
                message = "Sheltered recruit pending-spawn list is unavailable.";
                return false;
            }

            object spawnInfo = Activator.CreateInstance(NpcVisitSpawnInfoType);
            SpawnInfoTypeField.SetValue(spawnInfo, NpcVisitor.NpcType.Joiner);
            SpawnInfoTimerField.SetValue(spawnInfo, Mathf.Max(0f, arrivalDelay));

            IList attributes = SpawnInfoAttributesField.GetValue(spawnInfo) as IList;
            if (attributes == null)
            {
                message = "Sheltered recruit attributes list is unavailable.";
                return false;
            }

            queuedAttributes = CreateAttributes(config);
            attributes.Add(queuedAttributes);
            pendingSpawns.Add(spawnInfo);
            message = "Scheduled recruit '" + (config.Name ?? "Survivor") + "' to ask to join.";
            return true;
        }

        public static int ClampStat(int value)
        {
            if (value < StatMin)
                return StatMin;
            return value > StatMax ? StatMax : value;
        }

        public static int ClampCondition(int value)
        {
            if (value < ConditionMin)
                return ConditionMin;
            return value > ConditionMax ? ConditionMax : value;
        }

        public static bool ApplyConditions(FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null || config.Conditions == null || member.stats == null)
                return false;

            bool changed = false;
            changed |= ApplyCondition(member.stats.hunger, config.Conditions.Hunger);
            changed |= ApplyCondition(member.stats.thirst, config.Conditions.Thirst);
            changed |= ApplyCondition(member.stats.fatigue, config.Conditions.Fatigue);
            changed |= ApplyCondition(member.stats.dirtiness, config.Conditions.Dirtiness);
            changed |= ApplyCondition(member.stats.toilet, config.Conditions.Toilet);
            changed |= ApplyCondition(member.stats.stress, config.Conditions.Stress);
            return changed;
        }

        private static bool ApplyCondition(BehaviourStat target, int? value)
        {
            if (target == null || !value.HasValue)
                return false;

            target.Set(ClampCondition(value.Value));
            return true;
        }

        private static int? GetConditionValue(FamilyMemberConditionConfig conditions, string conditionId)
        {
            if (conditions == null || string.IsNullOrEmpty(conditionId))
                return null;

            if (string.Equals(conditionId, "Hunger", StringComparison.OrdinalIgnoreCase))
                return conditions.Hunger;
            if (string.Equals(conditionId, "Thirst", StringComparison.OrdinalIgnoreCase))
                return conditions.Thirst;
            if (string.Equals(conditionId, "Fatigue", StringComparison.OrdinalIgnoreCase))
                return conditions.Fatigue;
            if (string.Equals(conditionId, "Dirtiness", StringComparison.OrdinalIgnoreCase))
                return conditions.Dirtiness;
            if (string.Equals(conditionId, "Toilet", StringComparison.OrdinalIgnoreCase))
                return conditions.Toilet;
            if (string.Equals(conditionId, "Stress", StringComparison.OrdinalIgnoreCase))
                return conditions.Stress;

            return null;
        }

        private static string ResolveMeshId(FamilyMemberConfig config)
        {
            if (config != null && config.Appearance != null && !string.IsNullOrEmpty(config.Appearance.MeshId))
                return config.Appearance.MeshId;

            ScenarioGender gender = config != null ? config.Gender : ScenarioGender.Any;
            bool adult = true;
            if (config != null && config.Appearance != null && config.Appearance.IsAdult.HasValue)
                adult = config.Appearance.IsAdult.Value;
            else if (config != null && config.ExactAge.HasValue)
                adult = config.ExactAge.Value >= 18;

            if (gender == ScenarioGender.Female)
                return adult ? "woman" : "girl";
            return adult ? "man" : "boy";
        }

        private static void ApplyColor(string colorHex, Action<Color> apply)
        {
            if (apply == null)
                return;

            Color color;
            if (ScenarioCharacterAppearanceService.TryParseColorHex(colorHex, out color))
                apply(color);
        }

        private static void ApplyStat(FamilySpawner.CharacterAttributes attributes, StatOverride stat)
        {
            if (attributes == null || stat == null || string.IsNullOrEmpty(stat.StatId))
                return;

            int level = ClampStat(stat.Value);
            if (string.Equals(stat.StatId, "Strength", StringComparison.OrdinalIgnoreCase))
                attributes.m_strengthLevel = level;
            else if (string.Equals(stat.StatId, "Dexterity", StringComparison.OrdinalIgnoreCase))
                attributes.m_dexterityLevel = level;
            else if (string.Equals(stat.StatId, "Charisma", StringComparison.OrdinalIgnoreCase))
                attributes.m_charismaLevel = level;
            else if (string.Equals(stat.StatId, "Perception", StringComparison.OrdinalIgnoreCase))
                attributes.m_perceptionLevel = level;
            else if (string.Equals(stat.StatId, "Intelligence", StringComparison.OrdinalIgnoreCase))
                attributes.m_intelligenceLevel = level;
        }

        private static void ApplyTrait(FamilySpawner.CharacterAttributes attributes, string traitId)
        {
            if (attributes == null || string.IsNullOrEmpty(traitId))
                return;

            Traits.Strength strength;
            if (TryParseStrengthTrait(traitId, out strength) && !attributes.m_strengthTraits.Contains(strength))
            {
                attributes.m_strengthTraits.Add(strength);
                return;
            }

            Traits.Weakness weakness;
            if (TryParseWeaknessTrait(traitId, out weakness) && !attributes.m_weaknessTraits.Contains(weakness))
                attributes.m_weaknessTraits.Add(weakness);
        }

        private static void SanitizeTraitPairs(FamilySpawner.CharacterAttributes attributes, FamilyMemberConfig config)
        {
            if (attributes == null || attributes.m_strengthTraits == null || attributes.m_weaknessTraits == null)
                return;

            for (int i = attributes.m_weaknessTraits.Count - 1; i >= 0; i--)
            {
                Traits.Weakness weakness = attributes.m_weaknessTraits[i];
                Traits.Strength pairedStrength;
                if (!TryGetPairedStrength(weakness, out pairedStrength) || !attributes.m_strengthTraits.Contains(pairedStrength))
                    continue;

                attributes.m_weaknessTraits.RemoveAt(i);
                MMLog.WriteWarning("[ScenarioFamilyMemberFactory] Dropped conflicting weakness '" + weakness
                    + "' from survivor '" + (config != null && !string.IsNullOrEmpty(config.Name) ? config.Name : "Survivor")
                    + "' because paired strength '" + pairedStrength + "' is already active.");
            }
        }

        public static bool TryParseStrengthTrait(string value, out Traits.Strength strength)
        {
            return ScenarioSurvivorTraitConflictRules.TryParseStrength(value, out strength);
        }

        public static bool TryParseWeaknessTrait(string value, out Traits.Weakness weakness)
        {
            return ScenarioSurvivorTraitConflictRules.TryParseWeakness(value, out weakness);
        }

        public static bool TryGetPairedWeakness(Traits.Strength strength, out Traits.Weakness weakness)
        {
            return ScenarioSurvivorTraitConflictRules.TryGetPairedWeakness(strength, out weakness);
        }

        public static bool TryGetPairedStrength(Traits.Weakness weakness, out Traits.Strength strength)
        {
            return ScenarioSurvivorTraitConflictRules.TryGetPairedStrength(weakness, out strength);
        }

        public static bool HasConflictingTraitPair(FamilyMemberConfig config, out Traits.Strength strength, out Traits.Weakness weakness)
        {
            return ScenarioSurvivorTraitConflictRules.HasConflict(config, out strength, out weakness);
        }
    }
}
