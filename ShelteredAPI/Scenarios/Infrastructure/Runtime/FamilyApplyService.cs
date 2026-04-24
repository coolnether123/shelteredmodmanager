using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class FamilyApplyService
    {
        private static readonly FieldInfo BaseCharacterFirstNameField = typeof(BaseCharacter).GetField("m_firstName", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMaleField = typeof(BaseCharacter).GetField("m_male", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly ScenarioCharacterAppearanceService _characterAppearanceService;

        public FamilyApplyService(ScenarioCharacterAppearanceService characterAppearanceService)
        {
            _characterAppearanceService = characterAppearanceService;
        }

        public void Apply(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members.Count == 0)
                return;

            if (FamilyManager.Instance == null)
            {
                result.AddMessage("FamilyManager is not ready; family changes skipped.");
                return;
            }

            List<FamilyMember> members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null || members.Count == 0)
            {
                result.AddMessage("No spawned family members found; family changes skipped.");
                return;
            }

            int limit = Math.Min(members.Count, definition.FamilySetup.Members.Count);
            for (int i = 0; i < limit; i++)
            {
                FamilyMember member = members[i];
                FamilyMemberConfig config = definition.FamilySetup.Members[i];
                if (member == null || config == null)
                    continue;

                if (!string.IsNullOrEmpty(config.Name) && BaseCharacterFirstNameField != null)
                {
                    BaseCharacterFirstNameField.SetValue(member, config.Name);
                    member.name = config.Name;
                    result.FamilyChanges++;
                }

                if (config.Gender != ScenarioGender.Any && BaseCharacterMaleField != null)
                {
                    BaseCharacterMaleField.SetValue(member, config.Gender == ScenarioGender.Male);
                    result.FamilyChanges++;
                }

                ApplyStats(member, config, result);
                ApplyTraits(member, config, result);
                ApplySkills(member, config, result);
                ApplyAppearance(definition, scenarioFilePath, member, config, result);
            }

            if (definition.FamilySetup.OverrideVanillaFamily && definition.FamilySetup.Members.Count > members.Count)
            {
                result.AddMessage("OverrideVanillaFamily requested more members than currently spawned. Creating/removing family members is deferred until a safe spawn adapter is added.");
            }
        }

        private void ApplyAppearance(
            ScenarioDefinition definition,
            string scenarioFilePath,
            FamilyMember member,
            FamilyMemberConfig config,
            ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Appearance == null)
                return;

            string message;
            if (_characterAppearanceService.ApplyConfiguredAppearance(definition, scenarioFilePath, config, member, out message))
                result.FamilyChanges++;
            else if (!string.IsNullOrEmpty(message))
                result.AddMessage(message);
        }

        private static void ApplyStats(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Stats.Count == 0 || member.BaseStats == null)
                return;

            for (int i = 0; i < config.Stats.Count; i++)
            {
                StatOverride stat = config.Stats[i];
                if (stat == null)
                    continue;

                BaseStats.StatType statType;
                if (!TryParseStatType(stat.StatId, out statType))
                {
                    result.AddMessage("Unknown stat id skipped for '" + (config.Name ?? member.firstName) + "': " + (stat.StatId ?? string.Empty));
                    continue;
                }

                BaseStat target = member.BaseStats.GetStatByEnum(statType);
                if (target == null)
                {
                    result.AddMessage("Stat target was unavailable for '" + (config.Name ?? member.firstName) + "': " + statType + ".");
                    continue;
                }

                int level = Mathf.Clamp(stat.Value, 0, 20);
                target.SetInitialLevel(level, 20);
                result.FamilyChanges++;
            }
        }

        private static void ApplyTraits(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Traits.Count == 0 || member.traits == null)
                return;

            for (int i = 0; i < config.Traits.Count; i++)
            {
                string traitId = config.Traits[i];
                Traits.Strength strength;
                if (TryParseStrengthTrait(traitId, out strength))
                {
                    if (member.traits.AddStrength(strength))
                        result.FamilyChanges++;
                    else
                        result.AddMessage("Strength trait was already active or blocked by its paired weakness: " + traitId);
                    continue;
                }

                Traits.Weakness weakness;
                if (TryParseWeaknessTrait(traitId, out weakness))
                {
                    if (member.traits.AddWeakness(weakness, true))
                        result.FamilyChanges++;
                    else
                        result.AddMessage("Weakness trait was already active or blocked by its paired strength: " + traitId);
                    continue;
                }

                result.AddMessage("Unknown trait id skipped for '" + (config.Name ?? member.firstName) + "': " + (traitId ?? string.Empty));
            }
        }

        private static void ApplySkills(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null || config.Skills.Count == 0)
                return;

            for (int i = 0; i < config.Skills.Count; i++)
            {
                SkillOverride skill = config.Skills[i];
                if (skill == null)
                    continue;

                result.AddMessage("Skill '" + (skill.SkillId ?? string.Empty) + "' level " + skill.Level
                    + " for '" + (config.Name ?? member.firstName)
                    + "' is deferred because Sheltered exposes no stable runtime skill/save API comparable to BaseStats or Traits.");
            }
        }

        private static bool TryParseStatType(string value, out BaseStats.StatType statType)
        {
            statType = BaseStats.StatType.Max;
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                statType = (BaseStats.StatType)Enum.Parse(typeof(BaseStats.StatType), value, true);
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
                strength = (Traits.Strength)Enum.Parse(typeof(Traits.Strength), trimmed, true);
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
                weakness = (Traits.Weakness)Enum.Parse(typeof(Traits.Weakness), trimmed, true);
                return weakness != Traits.Weakness.Max;
            }
            catch
            {
                return false;
            }
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
