using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using ShelteredAPI.Content;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Definitions;
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

        public static string[] StatIds
        {
            get { return CoreStats; }
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
                message = "Spawned survivor '" + (config.Name ?? "Survivor") + "'.";
                return true;
            }

            message = "FamilySpawner did not report a new survivor after spawn.";
            return false;
        }

        public static bool ScheduleRecruit(FamilyMemberConfig config, float arrivalDelay, out string message)
        {
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

            attributes.Add(CreateAttributes(config));
            pendingSpawns.Add(spawnInfo);
            message = "Scheduled recruit '" + (config.Name ?? "Survivor") + "' to ask to join.";
            return true;
        }

        public static int ClampStat(int value)
        {
            if (value < 0)
                return 0;
            return value > 20 ? 20 : value;
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
            strength = Traits.Strength.Max;
            string trimmed = TrimTraitPrefix(value, "Strength:");
            if (trimmed == null)
                return false;

            try
            {
                strength = (Traits.Strength)Enum.Parse(typeof(Traits.Strength), trimmed, true);
                return strength != Traits.Strength.Max;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParseWeaknessTrait(string value, out Traits.Weakness weakness)
        {
            weakness = Traits.Weakness.Max;
            string trimmed = TrimTraitPrefix(value, "Weakness:");
            if (trimmed == null)
                return false;

            try
            {
                weakness = (Traits.Weakness)Enum.Parse(typeof(Traits.Weakness), trimmed, true);
                return weakness != Traits.Weakness.Max;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetPairedWeakness(Traits.Strength strength, out Traits.Weakness weakness)
        {
            weakness = Traits.Weakness.Max;
            if (strength == Traits.Strength.Max)
                return false;

            int index = (int)strength;
            if (index < 0 || index >= (int)Traits.Weakness.Max)
                return false;

            weakness = (Traits.Weakness)index;
            return true;
        }

        public static bool TryGetPairedStrength(Traits.Weakness weakness, out Traits.Strength strength)
        {
            strength = Traits.Strength.Max;
            if (weakness == Traits.Weakness.Max)
                return false;

            int index = (int)weakness;
            if (index < 0 || index >= (int)Traits.Strength.Max)
                return false;

            strength = (Traits.Strength)index;
            return true;
        }

        public static bool HasConflictingTraitPair(FamilyMemberConfig config, out Traits.Strength strength, out Traits.Weakness weakness)
        {
            strength = Traits.Strength.Max;
            weakness = Traits.Weakness.Max;
            if (config == null || config.Traits == null)
                return false;

            for (int i = 0; i < config.Traits.Count; i++)
            {
                Traits.Strength candidateStrength;
                if (!TryParseStrengthTrait(config.Traits[i], out candidateStrength))
                    continue;

                Traits.Weakness pairedWeakness;
                if (!TryGetPairedWeakness(candidateStrength, out pairedWeakness))
                    continue;

                for (int j = 0; j < config.Traits.Count; j++)
                {
                    Traits.Weakness candidateWeakness;
                    if (TryParseWeaknessTrait(config.Traits[j], out candidateWeakness) && candidateWeakness == pairedWeakness)
                    {
                        strength = candidateStrength;
                        weakness = candidateWeakness;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string TrimTraitPrefix(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(prefix.Length).Trim()
                : trimmed;
        }
    }
}
