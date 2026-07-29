using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesContentFacade
    {
        private readonly ParalivesSettingsFacade _settings;

        internal ParalivesContentFacade(ParalivesSettingsFacade settings)
        {
            _settings = settings;
        }

        public bool TryReadAction(ulong actionGuid, out ParalivesActionContentSnapshot snapshot)
        {
            snapshot = CreateMissingAction(actionGuid);

            try
            {
                ActionUnit action;
                if (!_settings.TryGetAction(actionGuid, out action))
                    return false;

                snapshot = CreateActionSnapshot(action);
                return snapshot.Exists;
            }
            catch
            {
                snapshot = CreateMissingAction(actionGuid);
                return false;
            }
        }

        public ParalivesActionContentSnapshot ReadAction(ulong actionGuid)
        {
            ParalivesActionContentSnapshot snapshot;
            return TryReadAction(actionGuid, out snapshot) ? snapshot : CreateMissingAction(actionGuid);
        }

        public ParalivesActionContentSnapshot[] ReadActions()
        {
            try
            {
                Actions actions;
                if (!_settings.TryGet<Actions>(out actions) || actions.AllActions == null)
                    return new ParalivesActionContentSnapshot[0];

                List<ParalivesActionContentSnapshot> snapshots = new List<ParalivesActionContentSnapshot>();
                for (int i = 0; i < actions.AllActions.Length; i++)
                {
                    if (actions.AllActions[i] != null)
                        snapshots.Add(CreateActionSnapshot(actions.AllActions[i]));
                }

                return snapshots.ToArray();
            }
            catch
            {
                return new ParalivesActionContentSnapshot[0];
            }
        }

        public bool TryReadInteraction(ulong interactionGuid, out ParalivesInteractionContentSnapshot snapshot)
        {
            snapshot = CreateMissingInteraction(interactionGuid);

            try
            {
                InteractionUnit interaction;
                if (!_settings.TryGetInteraction(interactionGuid, out interaction))
                    return false;

                snapshot = CreateInteractionSnapshot(interaction);
                return snapshot.Exists;
            }
            catch
            {
                snapshot = CreateMissingInteraction(interactionGuid);
                return false;
            }
        }

        public ParalivesInteractionContentSnapshot ReadInteraction(ulong interactionGuid)
        {
            ParalivesInteractionContentSnapshot snapshot;
            return TryReadInteraction(interactionGuid, out snapshot)
                ? snapshot
                : CreateMissingInteraction(interactionGuid);
        }

        public ParalivesInteractionContentSnapshot[] ReadInteractions()
        {
            try
            {
                Interactions interactions;
                if (!_settings.TryGet<Interactions>(out interactions) || interactions.AllInteractions == null)
                    return new ParalivesInteractionContentSnapshot[0];

                List<ParalivesInteractionContentSnapshot> snapshots = new List<ParalivesInteractionContentSnapshot>();
                for (int i = 0; i < interactions.AllInteractions.Length; i++)
                {
                    if (interactions.AllInteractions[i] != null)
                        snapshots.Add(CreateInteractionSnapshot(interactions.AllInteractions[i]));
                }

                return snapshots.ToArray();
            }
            catch
            {
                return new ParalivesInteractionContentSnapshot[0];
            }
        }

        public bool TryReadInteractionGroup(ulong groupGuid, out ParalivesInteractionGroupContentSnapshot snapshot)
        {
            snapshot = CreateMissingInteractionGroup(groupGuid);

            try
            {
                InteractionGroup group;
                if (!_settings.TryGetInteractionGroup(groupGuid, out group))
                    return false;

                snapshot = CreateInteractionGroupSnapshot(group);
                return snapshot.Exists;
            }
            catch
            {
                snapshot = CreateMissingInteractionGroup(groupGuid);
                return false;
            }
        }

        public ParalivesInteractionGroupContentSnapshot ReadInteractionGroup(ulong groupGuid)
        {
            ParalivesInteractionGroupContentSnapshot snapshot;
            return TryReadInteractionGroup(groupGuid, out snapshot)
                ? snapshot
                : CreateMissingInteractionGroup(groupGuid);
        }

        public ParalivesInteractionGroupContentSnapshot[] ReadInteractionGroups()
        {
            try
            {
                Interactions interactions;
                if (!_settings.TryGet<Interactions>(out interactions) || interactions.InteractionGroups == null)
                    return new ParalivesInteractionGroupContentSnapshot[0];

                List<ParalivesInteractionGroupContentSnapshot> snapshots = new List<ParalivesInteractionGroupContentSnapshot>();
                for (int i = 0; i < interactions.InteractionGroups.Length; i++)
                {
                    if (interactions.InteractionGroups[i] != null)
                        snapshots.Add(CreateInteractionGroupSnapshot(interactions.InteractionGroups[i]));
                }

                return snapshots.ToArray();
            }
            catch
            {
                return new ParalivesInteractionGroupContentSnapshot[0];
            }
        }

        public bool TryReadSkill(ulong skillGuid, out ParalivesSkillContentSnapshot snapshot)
        {
            snapshot = CreateMissingSkill(skillGuid);

            try
            {
                Skill skill;
                if (!_settings.TryGetSkill(skillGuid, out skill))
                    return false;

                snapshot = CreateSkillSnapshot(skill);
                return snapshot.Exists;
            }
            catch
            {
                snapshot = CreateMissingSkill(skillGuid);
                return false;
            }
        }

        public ParalivesSkillContentSnapshot ReadSkill(ulong skillGuid)
        {
            ParalivesSkillContentSnapshot snapshot;
            return TryReadSkill(skillGuid, out snapshot) ? snapshot : CreateMissingSkill(skillGuid);
        }

        public ParalivesSkillContentSnapshot[] ReadSkills()
        {
            try
            {
                Skills skills;
                if (!_settings.TryGet<Skills>(out skills) || skills.AllSkills == null)
                    return new ParalivesSkillContentSnapshot[0];

                List<ParalivesSkillContentSnapshot> snapshots = new List<ParalivesSkillContentSnapshot>();
                for (int i = 0; i < skills.AllSkills.Length; i++)
                {
                    if (skills.AllSkills[i] != null)
                        snapshots.Add(CreateSkillSnapshot(skills.AllSkills[i]));
                }

                return snapshots.ToArray();
            }
            catch
            {
                return new ParalivesSkillContentSnapshot[0];
            }
        }

        public bool TryReadOccupation(ulong occupationGuid, out ParalivesOccupationContentSnapshot snapshot)
        {
            snapshot = CreateMissingOccupation(occupationGuid);

            try
            {
                Occupation occupation;
                if (!_settings.TryGetOccupation(occupationGuid, out occupation))
                    return false;

                snapshot = CreateOccupationSnapshot(occupation);
                return snapshot.Exists;
            }
            catch
            {
                snapshot = CreateMissingOccupation(occupationGuid);
                return false;
            }
        }

        public ParalivesOccupationContentSnapshot ReadOccupation(ulong occupationGuid)
        {
            ParalivesOccupationContentSnapshot snapshot;
            return TryReadOccupation(occupationGuid, out snapshot)
                ? snapshot
                : CreateMissingOccupation(occupationGuid);
        }

        public ParalivesOccupationContentSnapshot[] ReadOccupations()
        {
            try
            {
                Occupations occupations;
                if (!_settings.TryGet<Occupations>(out occupations) || occupations.AllOccupations == null)
                    return new ParalivesOccupationContentSnapshot[0];

                List<ParalivesOccupationContentSnapshot> snapshots = new List<ParalivesOccupationContentSnapshot>();
                for (int i = 0; i < occupations.AllOccupations.Length; i++)
                {
                    if (occupations.AllOccupations[i] != null)
                        snapshots.Add(CreateOccupationSnapshot(occupations.AllOccupations[i]));
                }

                return snapshots.ToArray();
            }
            catch
            {
                return new ParalivesOccupationContentSnapshot[0];
            }
        }

        private static ParalivesActionContentSnapshot CreateActionSnapshot(ActionUnit action)
        {
            if (action == null)
                return CreateMissingAction(0UL);

            try
            {
                return new ParalivesActionContentSnapshot
                {
                    Exists = true,
                    ActionGuid = action.GUID,
                    DisplayName = action.DisplayName ?? string.Empty,
                    Type = (int)action.Type,
                    EndCondition = (int)action.EndCondition,
                    IsCancellable = action.IsCancellable,
                    HasLocomotion = action.HasLocomotion,
                    AnimationGuid = action.Animation,
                    ItemFinderRuleGuid = action.ItemFinderRule,
                    SittingItemFinderRuleGuid = action.SittingItemFinderRule,
                    RequirementCount = Count(action.Requirements),
                    OutcomeCount = Count(action.Outcomes)
                };
            }
            catch
            {
                return CreateMissingAction(0UL);
            }
        }

        private static ParalivesInteractionContentSnapshot CreateInteractionSnapshot(InteractionUnit interaction)
        {
            if (interaction == null)
                return CreateMissingInteraction(0UL);

            try
            {
                return new ParalivesInteractionContentSnapshot
                {
                    Exists = true,
                    InteractionGuid = interaction.GUID,
                    DisplayName = interaction.DisplayName ?? string.Empty,
                    TranslationKey = interaction.TranslationKey ?? string.Empty,
                    ActionGuid = interaction.ActionGUID,
                    CharacterRequirementGuid = interaction.CharacterRequirement,
                    OtherCharacterRequirementGuid = interaction.OtherCharacterRequirement,
                    SocialGroupRuleGuid = interaction.SocialGroupRule,
                    IsInstant = interaction.IsInstant,
                    IsPlayerCancellable = interaction.IsPlayerCancellable,
                    ForceInteractionDoneSolo = interaction.ForceInteractionDoneSolo,
                    InjectToAllEvenIfTargeted = interaction.InjectToAllEvenIfTargetted,
                    StartingRequirementChecks = (int)interaction.StartingRequirementChecks,
                    RunningRequirementChecks = (int)interaction.RunningRequirementChecks,
                    UsabilityRuleCount = Count(interaction.InteractionUsabilityRules)
                };
            }
            catch
            {
                return CreateMissingInteraction(0UL);
            }
        }

        private static ParalivesInteractionGroupContentSnapshot CreateInteractionGroupSnapshot(InteractionGroup group)
        {
            if (group == null)
                return CreateMissingInteractionGroup(0UL);

            try
            {
                return new ParalivesInteractionGroupContentSnapshot
                {
                    Exists = true,
                    GroupGuid = group.GUID,
                    DisplayName = group.DisplayName ?? string.Empty,
                    TranslationKey = group.TranslationKey ?? string.Empty,
                    DisplayNameIsSkinnable = group.DisplayNameIsSkinnable,
                    DisplayNameIsImpostorLotType = group.DisplayNameIsImpostorLotType,
                    ItemMouseOverHighlight = group.ItemMouseOverHightlight,
                    Children = CreateInteractionGroupChildren(group.ChildrenInteractionAndGroups)
                };
            }
            catch
            {
                return CreateMissingInteractionGroup(0UL);
            }
        }

        private static ParalivesSkillContentSnapshot CreateSkillSnapshot(Skill skill)
        {
            if (skill == null)
                return CreateMissingSkill(0UL);

            try
            {
                return new ParalivesSkillContentSnapshot
                {
                    Exists = true,
                    SkillGuid = skill.GUID,
                    DisplayName = skill.DisplayName ?? string.Empty,
                    TranslationKey = skill.TranslationKey ?? string.Empty,
                    Enabled = skill.Enabled,
                    MaxLevel = skill.MaxLevel,
                    ParentKnowledgeSkillGuid = skill.ParentKnowledgeSkill,
                    RestrictedToRequirementGuid = skill.RestrictedToRequirements
                };
            }
            catch
            {
                return CreateMissingSkill(0UL);
            }
        }

        private static ParalivesOccupationContentSnapshot CreateOccupationSnapshot(Occupation occupation)
        {
            if (occupation == null)
                return CreateMissingOccupation(0UL);

            try
            {
                return new ParalivesOccupationContentSnapshot
                {
                    Exists = true,
                    OccupationGuid = occupation.GUID,
                    DisplayName = occupation.DisplayName ?? string.Empty,
                    TranslationKey = "OccupationName_" + (occupation.DisplayName ?? string.Empty),
                    Type = (int)occupation.Type,
                    CompanyGuid = occupation.Company,
                    ProgressionLevelGuid = occupation.ProgressionLevel,
                    ScheduleGuid = occupation.Schedule,
                    DomainGuids = ToUlongArray(occupation.Domains),
                    AppropriateLifeStageGuids = ToUlongArray(occupation.AppropriateLifestages),
                    IsRabbitHole = occupation.IsRabbithole,
                    OverridesCompanyRabbitHole = occupation.OverridesCompanyRabbithole,
                    TravelDuration = occupation.TravelDuration,
                    MaxNumberOfExtraSlots = occupation.MaxNumberOfExtraSlots
                };
            }
            catch
            {
                return CreateMissingOccupation(0UL);
            }
        }

        private static ParalivesInteractionGroupChildSnapshot[] CreateInteractionGroupChildren(InteractionGroupItem[] children)
        {
            if (children == null || children.Length == 0)
                return new ParalivesInteractionGroupChildSnapshot[0];

            List<ParalivesInteractionGroupChildSnapshot> snapshots = new List<ParalivesInteractionGroupChildSnapshot>();
            for (int i = 0; i < children.Length; i++)
            {
                InteractionGroupItem child = children[i];
                if (child == null)
                    continue;

                try
                {
                    snapshots.Add(new ParalivesInteractionGroupChildSnapshot
                    {
                        ItemGuid = child.GUID,
                        Type = (int)child.Type,
                        InteractionGuid = child.Interaction,
                        GroupGuid = child.Group,
                        IsNestedNameDifferentThanInteractionName = child.IsNestedNameDifferentThanInteractionName,
                        DisplayNameOfNestedInteraction = child.DisplayNameOfNestedInteraction ?? string.Empty,
                        NestedNameTranslationKey = child.NestedNameTranslationKey ?? string.Empty
                    });
                }
                catch
                {
                }
            }

            return snapshots.ToArray();
        }

        private static ParalivesActionContentSnapshot CreateMissingAction(ulong actionGuid)
        {
            return new ParalivesActionContentSnapshot
            {
                ActionGuid = actionGuid,
                DisplayName = string.Empty
            };
        }

        private static ParalivesInteractionContentSnapshot CreateMissingInteraction(ulong interactionGuid)
        {
            return new ParalivesInteractionContentSnapshot
            {
                InteractionGuid = interactionGuid,
                DisplayName = string.Empty,
                TranslationKey = string.Empty
            };
        }

        private static ParalivesInteractionGroupContentSnapshot CreateMissingInteractionGroup(ulong groupGuid)
        {
            return new ParalivesInteractionGroupContentSnapshot
            {
                GroupGuid = groupGuid,
                DisplayName = string.Empty,
                TranslationKey = string.Empty,
                Children = new ParalivesInteractionGroupChildSnapshot[0]
            };
        }

        private static ParalivesSkillContentSnapshot CreateMissingSkill(ulong skillGuid)
        {
            return new ParalivesSkillContentSnapshot
            {
                SkillGuid = skillGuid,
                DisplayName = string.Empty,
                TranslationKey = string.Empty
            };
        }

        private static ParalivesOccupationContentSnapshot CreateMissingOccupation(ulong occupationGuid)
        {
            return new ParalivesOccupationContentSnapshot
            {
                OccupationGuid = occupationGuid,
                DisplayName = string.Empty,
                TranslationKey = string.Empty,
                DomainGuids = new ulong[0],
                AppropriateLifeStageGuids = new ulong[0]
            };
        }

        private static int Count<T>(T[] values)
        {
            return values == null ? 0 : values.Length;
        }

        private static ulong[] ToUlongArray(global::UlongAndGuid[] values)
        {
            if (values == null || values.Length == 0)
                return new ulong[0];

            List<ulong> result = new List<ulong>();
            for (int i = 0; i < values.Length; i++)
            {
                try
                {
                    if (values[i] != null && values[i].Value != 0UL)
                        result.Add(values[i].Value);
                }
                catch
                {
                }
            }

            return result.ToArray();
        }
    }
}
