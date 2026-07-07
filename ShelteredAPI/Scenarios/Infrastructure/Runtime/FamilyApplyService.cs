using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class FamilyApplyService
    {
        private static readonly FieldInfo BaseCharacterFirstNameField = typeof(BaseCharacter).GetField("m_firstName", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo BaseCharacterMaleField = typeof(BaseCharacter).GetField("m_male", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo FamilyManagerGameOverField = typeof(FamilyManager).GetField("game_over", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly ScenarioCharacterAppearanceService _characterAppearanceService;
        private readonly ScenarioActorResolver _actorResolver;

        public FamilyApplyService(
            ScenarioCharacterAppearanceService characterAppearanceService,
            ScenarioActorResolver actorResolver)
        {
            _characterAppearanceService = characterAppearanceService;
            _actorResolver = actorResolver;
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
            if (members == null)
                members = new List<FamilyMember>();

            if (members.Count == 0 && definition.FamilySetup.OverrideVanillaFamily)
            {
                int spawnedCount = SpawnMissingMembers(definition, scenarioFilePath, result, 0);
                if (spawnedCount > 0)
                    ClearGameOverAfterFamilySpawn();
                else
                    result.AddMessage("No spawned family members found; authored family spawn failed.");
                return;
            }

            if (members.Count == 0)
            {
                result.AddMessage("No spawned family members found; family changes skipped.");
                return;
            }

            for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberConfig config = definition.FamilySetup.Members[i];
                if (config == null)
                    continue;

                FamilyMember member = ResolveAuthoredMember(definition, config, i, members);
                if (member == null && definition.FamilySetup.OverrideVanillaFamily)
                {
                    SpawnConfiguredMember(definition, scenarioFilePath, result, config);
                    continue;
                }

                if (member != null)
                    ApplyConfiguredMember(definition, scenarioFilePath, result, member, config);
            }
        }

        private int SpawnMissingMembers(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result, int startIndex)
        {
            int spawnedCount = 0;
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null)
                return spawnedCount;

            for (int i = Math.Max(0, startIndex); i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberConfig config = definition.FamilySetup.Members[i];
                if (SpawnConfiguredMember(definition, scenarioFilePath, result, config))
                    spawnedCount++;
            }

            return spawnedCount;
        }

        private FamilyMember ResolveAuthoredMember(ScenarioDefinition definition, FamilyMemberConfig config, int index, List<FamilyMember> members)
        {
            FamilyMember resolved;
            if (_actorResolver != null
                && config != null
                && config.ActorRef != null
                && _actorResolver.TryResolveFamilyMember(definition, config.ActorRef, out resolved))
            {
                return resolved;
            }

            return members != null && index >= 0 && index < members.Count ? members[index] : null;
        }

        private bool SpawnConfiguredMember(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result, FamilyMemberConfig config)
        {
            FamilyMember spawned;
            string spawnMessage;
            if (ScenarioFamilyMemberFactory.Spawn(config, out spawned, out spawnMessage))
            {
                result.FamilyChanges++;
                ApplyAppearance(definition, scenarioFilePath, spawned, config, result);
                BindMaterializedMember(definition, config != null ? config.ActorRef : null, spawned, result);
                return true;
            }

            if (!string.IsNullOrEmpty(spawnMessage))
                result.AddMessage(spawnMessage);
            return false;
        }

        private void ApplyConfiguredMember(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result, FamilyMember member, FamilyMemberConfig config)
        {
            if (member == null || config == null)
                return;

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

            ApplyMeshAlignment(member, config, result);

            ApplyStats(member, config, result);
            ApplyTraits(member, config, result);
            ApplyConditions(member, config, result);
            ApplySkills(member, config, result);
            ApplyAppearance(definition, scenarioFilePath, member, config, result);
            BindMaterializedMember(definition, config.ActorRef, member, result);
        }

        private void BindMaterializedMember(ScenarioDefinition definition, ScenarioActorRef actorRef, FamilyMember member, ScenarioApplyResult result)
        {
            if (_actorResolver == null || actorRef == null || member == null)
                return;

            string bindMessage;
            if (!_actorResolver.BindMaterializedFamilyMember(definition, actorRef, member, out bindMessage)
                && !string.IsNullOrEmpty(bindMessage))
            {
                result.AddMessage(bindMessage);
            }
        }

        private static void ClearGameOverAfterFamilySpawn()
        {
            if (FamilyManager.Instance == null || FamilyManagerGameOverField == null)
                return;

            try
            {
                FamilyManagerGameOverField.SetValue(FamilyManager.Instance, false);
            }
            catch
            {
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

        private void ApplyMeshAlignment(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (_characterAppearanceService == null || member == null || config == null)
                return;

            string message;
            if (_characterAppearanceService.AlignLiveMesh(config, member, out message))
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

                int level = ScenarioFamilyMemberFactory.ClampStat(stat.Value);
                target.SetInitialLevel(level, ScenarioFamilyMemberFactory.StatMax);
                result.FamilyChanges++;
            }
        }

        private static void ApplyConditions(FamilyMember member, FamilyMemberConfig config, ScenarioApplyResult result)
        {
            if (member == null || config == null)
                return;

            if (ScenarioFamilyMemberFactory.ApplyConditions(member, config) && result != null)
                result.FamilyChanges++;
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
