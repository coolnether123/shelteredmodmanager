using System;
using System.Collections.Generic;

using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.People;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Application.Authoring
{
    /// <summary>
    /// Bulk survivor mutations live here so their declared scope is reviewable and
    /// contract-testable independently from IMGUI and action routing.
    /// </summary>
    internal sealed class ScenarioSurvivorAuthoringOperations
    {
        public const string RandomizeDisclosure = "Randomizes: name, gender, age, appearance, stats, traits. Keeps: story links, arrival settings, starting condition, skills, actor identity.";
        public const string DuplicateDisclosure = "Copies: gender, age, appearance, stats, traits, starting condition, skills, arrival settings, mod fields. Regenerates: name (adds Copy), survivor identity, actor reference. Keeps story links on the original.";

        private readonly ScenarioCharacterAppearanceService _appearanceService;

        public ScenarioSurvivorAuthoringOperations(ScenarioCharacterAppearanceService appearanceService)
        {
            _appearanceService = appearanceService;
        }

        public void RandomizeDeclaredFields(FamilyMemberConfig member)
        {
            if (member == null)
                return;

            member.Gender = UnityEngine.Random.Range(0, 2) == 0 ? ScenarioGender.Male : ScenarioGender.Female;
            bool adult = UnityEngine.Random.Range(0, 2) == 0;
            member.ExactAge = adult ? UnityEngine.Random.Range(18, 71) : UnityEngine.Random.Range(6, 18);
            member.MinAge = null;
            member.MaxAge = null;
            member.Name = NameGenerator.GetFirstName(member.Gender == ScenarioGender.Female
                ? NameGenerator.Gender.Female
                : NameGenerator.Gender.Male);

            ScenarioFamilyMemberFactory.EnsureCoreStats(member);
            for (int i = 0; member.Stats != null && i < member.Stats.Count; i++)
            {
                StatOverride stat = member.Stats[i];
                if (stat != null)
                    stat.Value = UnityEngine.Random.Range(ScenarioFamilyMemberFactory.StatMin, ScenarioFamilyMemberFactory.StatMax + 1);
            }

            member.Traits.Clear();
            AddRandomTrait(member, true);
            AddRandomTrait(member, false);
            RandomizeAppearance(member);
        }

        public void RandomizeAppearance(FamilyMemberConfig member)
        {
            if (member == null)
                return;

            if (member.Appearance == null)
                member.Appearance = new FamilyMemberAppearanceConfig();

            bool adult = member.ExactAge.HasValue
                ? member.ExactAge.Value >= 18
                : !member.Appearance.IsAdult.HasValue || member.Appearance.IsAdult.Value;
            member.Appearance.IsAdult = adult;
            member.Appearance.MeshId = ResolveVanillaMeshId(member.Gender, adult);

            if (_appearanceService != null)
            {
                member.Appearance.HeadTextureId = _appearanceService.RandomTextureId(member.Appearance.MeshId, ScenarioCharacterTexturePart.Head);
                member.Appearance.TorsoTextureId = _appearanceService.RandomTextureId(member.Appearance.MeshId, ScenarioCharacterTexturePart.Torso);
                member.Appearance.LegTextureId = _appearanceService.RandomTextureId(member.Appearance.MeshId, ScenarioCharacterTexturePart.Legs);
                member.Appearance.HairColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Hair);
                member.Appearance.SkinColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Skin);
                member.Appearance.ShirtColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Shirt);
                member.Appearance.PantsColorHex = _appearanceService.RandomColorHex(ScenarioCharacterColorPart.Pants);
            }

            member.Appearance.HeadTexturePath = null;
            member.Appearance.TorsoTexturePath = null;
            member.Appearance.LegTexturePath = null;
        }

        public static FamilyMemberConfig DuplicateMember(FamilyMemberConfig source)
        {
            FamilyMemberConfig copy = new FamilyMemberConfig();
            if (source == null)
            {
                copy.Name = "Survivor Copy";
                return copy;
            }

            copy.Name = BuildCopyName(source.Name);
            copy.ActorRef = null;
            copy.Gender = source.Gender;
            copy.ExactAge = source.ExactAge;
            copy.MinAge = source.MinAge;
            copy.MaxAge = source.MaxAge;
            copy.Appearance = CopyAppearance(source.Appearance);
            copy.Conditions = CopyConditions(source.Conditions);
            CopyStats(source.Stats, copy.Stats);
            CopyStrings(source.Traits, copy.Traits);
            CopySkills(source.Skills, copy.Skills);
            CopyActorComponents(source.ActorComponents, copy.ActorComponents);
            return copy;
        }

        public static FutureSurvivorDefinition DuplicateFutureSurvivor(FutureSurvivorDefinition source, IList<FutureSurvivorDefinition> siblings)
        {
            FutureSurvivorDefinition copy = new FutureSurvivorDefinition();
            copy.Id = BuildUniqueFutureId(source != null ? source.Id : null, siblings);
            copy.ActorRef = null;
            copy.Arrival = CopySchedule(source != null ? source.Arrival : null);
            copy.AskToJoin = source == null || source.AskToJoin;
            copy.Survivor = DuplicateMember(source != null ? source.Survivor : null);
            copy.Survivor.ActorRef = null;
            if (source != null)
                CopyActorComponents(source.ActorComponents, copy.ActorComponents);
            return copy;
        }

        private static void AddRandomTrait(FamilyMemberConfig member, bool strength)
        {
            Array values = Enum.GetValues(strength ? typeof(Traits.Strength) : typeof(Traits.Weakness));
            if (values == null || values.Length == 0)
                return;

            int start = UnityEngine.Random.Range(0, values.Length);
            for (int offset = 0; offset < values.Length; offset++)
            {
                object candidate = values.GetValue((start + offset) % values.Length);
                if (candidate == null || string.Equals(candidate.ToString(), "Max", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (ScenarioSurvivorTraitConflictRules.ConflictsWithSelection(member, strength, candidate))
                    continue;

                member.Traits.Add((strength ? "Strength:" : "Weakness:") + candidate);
                return;
            }
        }

        private static string ResolveVanillaMeshId(ScenarioGender gender, bool adult)
        {
            if (gender == ScenarioGender.Female)
                return adult ? "woman" : "girl";
            return adult ? "man" : "boy";
        }

        private static string BuildCopyName(string sourceName)
        {
            string baseName = string.IsNullOrEmpty(sourceName) ? "Survivor" : sourceName.Trim();
            return baseName + " Copy";
        }

        private static string BuildUniqueFutureId(string sourceId, IList<FutureSurvivorDefinition> siblings)
        {
            string baseId = string.IsNullOrEmpty(sourceId) ? "future_survivor" : sourceId.Trim();
            string candidate = baseId + "_copy";
            int suffix = 2;
            while (ContainsFutureId(siblings, candidate))
            {
                candidate = baseId + "_copy_" + suffix.ToString();
                suffix++;
            }
            return candidate;
        }

        private static bool ContainsFutureId(IList<FutureSurvivorDefinition> survivors, string id)
        {
            for (int i = 0; survivors != null && i < survivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = survivors[i];
                if (survivor != null && string.Equals(survivor.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static FamilyMemberAppearanceConfig CopyAppearance(FamilyMemberAppearanceConfig source)
        {
            if (source == null)
                return new FamilyMemberAppearanceConfig();
            return new FamilyMemberAppearanceConfig
            {
                MeshId = source.MeshId,
                IsAdult = source.IsAdult,
                HeadTextureId = source.HeadTextureId,
                HeadTexturePath = source.HeadTexturePath,
                TorsoTextureId = source.TorsoTextureId,
                TorsoTexturePath = source.TorsoTexturePath,
                LegTextureId = source.LegTextureId,
                LegTexturePath = source.LegTexturePath,
                HairColorHex = source.HairColorHex,
                SkinColorHex = source.SkinColorHex,
                ShirtColorHex = source.ShirtColorHex,
                PantsColorHex = source.PantsColorHex
            };
        }

        private static FamilyMemberConditionConfig CopyConditions(FamilyMemberConditionConfig source)
        {
            if (source == null)
                return new FamilyMemberConditionConfig();
            return new FamilyMemberConditionConfig
            {
                Hunger = source.Hunger,
                Thirst = source.Thirst,
                Fatigue = source.Fatigue,
                Dirtiness = source.Dirtiness,
                Toilet = source.Toilet,
                Stress = source.Stress
            };
        }

        private static ScenarioScheduleTime CopySchedule(ScenarioScheduleTime source)
        {
            return new ScenarioScheduleTime
            {
                Day = source != null ? source.Day : 1,
                Hour = source != null ? source.Hour : 0,
                Minute = source != null ? source.Minute : 0
            };
        }

        private static void CopyStats(IList<StatOverride> source, IList<StatOverride> target)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                StatOverride stat = source[i];
                if (stat != null)
                    target.Add(new StatOverride { StatId = stat.StatId, Value = stat.Value });
            }
        }

        private static void CopySkills(IList<SkillOverride> source, IList<SkillOverride> target)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                SkillOverride skill = source[i];
                if (skill != null)
                    target.Add(new SkillOverride { SkillId = skill.SkillId, Level = skill.Level });
            }
        }

        private static void CopyStrings(IList<string> source, IList<string> target)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                target.Add(source[i]);
        }

        private static void CopyActorComponents(IList<ScenarioActorComponentDefinition> source, IList<ScenarioActorComponentDefinition> target)
        {
            for (int i = 0; source != null && i < source.Count; i++)
            {
                ScenarioActorComponentDefinition component = source[i];
                if (component == null)
                    continue;
                target.Add(new ScenarioActorComponentDefinition
                {
                    ComponentId = component.ComponentId,
                    OwnerModId = component.OwnerModId,
                    Version = component.Version,
                    PayloadJson = component.PayloadJson
                });
            }
        }
    }
}
