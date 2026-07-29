using System;
using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public static class ParalivesInteractionFactory
    {
        public static ParalivesInteractionContent CreateContent(ParalivesInteractionPack pack)
        {
            if (pack == null)
                throw new ArgumentNullException("pack");

            ParalivesInteractionContent content = new ParalivesInteractionContent();

            for (int i = 0; i < pack.Actions.Count; i++)
                content.Actions.Add(CreateAction(pack.Actions[i]));
            for (int i = 0; i < pack.Groups.Count; i++)
                content.Groups.Add(CreateGroup(pack.Groups[i]));
            for (int i = 0; i < pack.Interactions.Count; i++)
                content.Interactions.Add(CreateInteraction(pack.Interactions[i]));
            for (int i = 0; i < pack.GroupChildren.Count; i++)
                content.GroupChildren.Add(CreateGroupChildRegistration(pack.GroupChildren[i]));

            return content;
        }

        public static ActionUnit CreateAction(ParalivesActionDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            if (definition.Guid == 0UL)
                throw new ArgumentException("Actions must have a non-zero GUID.", "definition");

            return new ActionUnit
            {
                GUID = definition.Guid,
                DisplayName = definition.DisplayName,
                Type = ActionUnitType.Single,
                EndCondition = ToActionEndCondition(definition.CompletionMode),
                EndConditionFixedDurationInMinutes = definition.DurationMinutes,
                IsCancellable = definition.IsCancellable,
                Animation = definition.AnimationGuid,
                HasConversation = definition.HasConversation,
                SocialClusterRules = definition.SocialClusterRulesGuid,
                SocialClusterInPositionCondition = SocialClusterInPositionCondition.IdleUntilAnyoneElseInPosition,
                CancelSocialClusterIfAnyoneLeave = definition.CancelSocialClusterIfAnyoneLeave,
                WaitForEveryoneToBeDone = definition.WaitForEveryoneToBeDone,
                SocialTargetCharacterMovesAndTurn = definition.SocialTargetCharacterMovesAndTurn,
                MinimumDistanceBetweenCharacters = definition.MinimumDistanceBetweenCharacters
            };
        }

        public static InteractionUnit CreateInteraction(ParalivesInteractionDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            if (definition.Guid == 0UL)
                throw new ArgumentException("Interactions must have a non-zero GUID.", "definition");

            return new InteractionUnit
            {
                GUID = definition.Guid,
                DisplayName = definition.DisplayName,
                ActionGUID = definition.ActionGuid,
                CharacterRequirement = definition.CharacterRequirementGuid,
                OtherCharacterRequirement = definition.OtherCharacterRequirementGuid,
                InteractionMenuIcon = definition.InteractionMenuIconGuid,
                InteractionQueueIcon = definition.InteractionQueueIconGuid,
                InteractionUsabilityRules = CreateRules(definition.InteractionUsabilityRules),
                CanPerformIfNeedsAreCritical = definition.CanPerformIfNeedsAreCritical,
                ForceInteractionDoneSolo = definition.ForceInteractionDoneSolo,
                IsInstant = definition.IsInstant,
                InstantIsDoneByOnlyOneCharacter = definition.InstantIsDoneByOnlyOneCharacter,
                IsPlayerCancellable = definition.IsPlayerCancellable,
                StartingRequirementChecks = ToRequirementChecks(definition.StartingRequirementChecks),
                RunningRequirementChecks = ToRequirementChecks(definition.RunningRequirementChecks)
            };
        }

        public static InteractionGroup CreateGroup(ParalivesInteractionGroupDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            if (definition.Guid == 0UL)
                throw new ArgumentException("Interaction groups must have a non-zero GUID.", "definition");

            return new InteractionGroup
            {
                GUID = definition.Guid,
                DisplayName = definition.DisplayName,
                ItemMouseOverHightlight = definition.ItemMouseOverHighlight,
                ChildrenInteractionAndGroups = CreateGroupItems(definition.Guid, definition.Children)
            };
        }

        public static ParalivesInteractionGroupChildRegistration CreateGroupChildRegistration(
            ParalivesInteractionGroupChildDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            if (definition.Kind == ParalivesInteractionChildKind.Group)
            {
                return definition.UsesParentRootGroup
                    ? ParalivesInteractionGroupChildRegistration.ForGroupInRootGroup(
                        definition.ParentRootGroup,
                        definition.GroupGuid,
                        definition.ChildItemGuid)
                    : ParalivesInteractionGroupChildRegistration.ForGroup(
                        definition.ParentGroupGuid,
                        definition.GroupGuid,
                        definition.ChildItemGuid);
            }

            return definition.UsesParentRootGroup
                ? ParalivesInteractionGroupChildRegistration.ForInteractionInRootGroup(
                    definition.ParentRootGroup,
                    definition.InteractionGuid,
                    definition.NestedInteractionDisplayName,
                    definition.ChildItemGuid)
                : ParalivesInteractionGroupChildRegistration.ForInteraction(
                    definition.ParentGroupGuid,
                    definition.InteractionGuid,
                    definition.NestedInteractionDisplayName,
                    definition.ChildItemGuid);
        }

        public static InteractionUsabilityRule CreateRule(
            ParalivesInteractionUsabilityRuleDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            if (definition.Requirement == null)
                throw new ArgumentException("Interaction usability rules must include a requirement.", "definition");

            return new InteractionUsabilityRule
            {
                GUID = definition.Guid,
                Requirement = CreateRequirement(definition.Requirement),
                DisplayRule = ToDisplayRule(definition.DisplayWhenNotMet),
                ShowCooldownFromRequirements = definition.ShowCooldownFromRequirements,
                ShowCostFromRequirements = definition.ShowCostFromRequirements
            };
        }

        public static ContextRequirement CreateRequirement(
            ParalivesContextRequirementDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            ContextRequirement requirement = new ContextRequirement
            {
                GUID = definition.Guid,
                Type = ToContextRequirementType(definition.Kind),
                CharacterRequirement = definition.CharacterRequirementGuid,
                MustBeTrue = definition.MustBeTrue
            };

            if (definition.Kind == ParalivesContextRequirementKind.SwitchActorAndTarget)
                requirement.Requirement = CreateRequirement(definition.Requirement);

            return requirement;
        }

        public static ActionUnit CreateInstantAction(ulong guid, string displayName)
        {
            return new ActionUnit
            {
                GUID = guid,
                DisplayName = displayName,
                Type = ActionUnitType.Single,
                EndCondition = ActionEndCondition.InstantNoQueue,
                EndConditionFixedDurationInMinutes = 0.1f,
                IsCancellable = false
            };
        }

        public static ActionUnit CreateTimedAction(ulong guid, string displayName, float durationMinutes)
        {
            return new ActionUnit
            {
                GUID = guid,
                DisplayName = displayName,
                Type = ActionUnitType.Single,
                EndCondition = ActionEndCondition.FixedDurationInMinutes,
                EndConditionFixedDurationInMinutes = durationMinutes,
                IsCancellable = true
            };
        }

        public static ActionUnit CreateConversationAction(
            ulong guid,
            string displayName,
            float durationMinutes,
            ulong animationGuid,
            ulong socialClusterRulesGuid)
        {
            ActionUnit action = CreateTimedAction(guid, displayName, durationMinutes);
            action.Animation = animationGuid;
            action.HasConversation = true;
            action.SocialClusterRules = socialClusterRulesGuid;
            action.SocialClusterInPositionCondition = SocialClusterInPositionCondition.IdleUntilAnyoneElseInPosition;
            action.CancelSocialClusterIfAnyoneLeave = true;
            action.WaitForEveryoneToBeDone = true;
            action.SocialTargetCharacterMovesAndTurn = true;
            action.MinimumDistanceBetweenCharacters = 0.5f;
            return action;
        }

        public static InteractionUnit CreateInteraction(
            ulong guid,
            string displayName,
            ulong actionGuid)
        {
            return CreateInteraction(guid, displayName, actionGuid, 0UL, null, false, false);
        }

        public static InteractionUnit CreateInteraction(
            ulong guid,
            string displayName,
            ulong actionGuid,
            ulong characterRequirement,
            InteractionUsabilityRule[] usabilityRules,
            bool forceSolo,
            bool isInstant)
        {
            return new InteractionUnit
            {
                GUID = guid,
                DisplayName = displayName,
                ActionGUID = actionGuid,
                CharacterRequirement = characterRequirement,
                InteractionUsabilityRules = usabilityRules ?? new InteractionUsabilityRule[0],
                CanPerformIfNeedsAreCritical = true,
                ForceInteractionDoneSolo = forceSolo,
                IsInstant = isInstant,
                InstantIsDoneByOnlyOneCharacter = true,
                IsPlayerCancellable = !isInstant,
                StartingRequirementChecks = RequirementChecks.OnAllCharactersInvolved,
                RunningRequirementChecks = RequirementChecks.OnAllCharactersInvolved
            };
        }

        public static InteractionGroup CreateGroup(ulong guid, string displayName, params InteractionGroupItem[] children)
        {
            return new InteractionGroup
            {
                GUID = guid,
                DisplayName = displayName,
                ItemMouseOverHightlight = true,
                ChildrenInteractionAndGroups = children ?? new InteractionGroupItem[0]
            };
        }

        public static InteractionGroupItem CreateGroupItem(ulong itemGuid, ulong groupGuid)
        {
            return new InteractionGroupItem
            {
                GUID = itemGuid,
                Type = InteractionItemType.Group,
                Group = groupGuid
            };
        }

        public static InteractionGroupItem CreateInteractionItem(ulong itemGuid, ulong interactionGuid)
        {
            return CreateInteractionItem(itemGuid, interactionGuid, null);
        }

        public static InteractionGroupItem CreateInteractionItem(
            ulong itemGuid,
            ulong interactionGuid,
            string nestedDisplayName)
        {
            return new InteractionGroupItem
            {
                GUID = itemGuid,
                Type = InteractionItemType.Interaction,
                Interaction = interactionGuid,
                IsNestedNameDifferentThanInteractionName = !string.IsNullOrEmpty(nestedDisplayName),
                DisplayNameOfNestedInteraction = nestedDisplayName
            };
        }

        public static InteractionUsabilityRule CreateRule(
            ulong ruleGuid,
            ContextRequirement requirement)
        {
            return CreateRule(ruleGuid, requirement, HowToDisplayIfNotMet.Hide);
        }

        public static InteractionUsabilityRule CreateRule(
            ulong ruleGuid,
            ContextRequirement requirement,
            HowToDisplayIfNotMet displayRule)
        {
            return new InteractionUsabilityRule
            {
                GUID = ruleGuid,
                Requirement = requirement,
                DisplayRule = displayRule
            };
        }

        public static ContextRequirement CreateSameHouseholdRequirement(ulong requirementGuid)
        {
            return new ContextRequirement
            {
                GUID = requirementGuid,
                Type = ContextRequirementType.IsInSameHousehold,
                MustBeTrue = true
            };
        }

        public static ContextRequirement CreateCharacterRequirement(
            ulong requirementGuid,
            ulong characterRequirementGuid)
        {
            return new ContextRequirement
            {
                GUID = requirementGuid,
                Type = ContextRequirementType.CharacterHasCharacterRequirements,
                CharacterRequirement = characterRequirementGuid,
                MustBeTrue = true
            };
        }

        public static ContextRequirement CreateOtherCharacterRequirement(
            ulong requirementGuid,
            ulong characterRequirementGuid)
        {
            return new ContextRequirement
            {
                GUID = requirementGuid,
                Type = ContextRequirementType.OtherCharacterHasCharacterRequirements,
                CharacterRequirement = characterRequirementGuid,
                MustBeTrue = true
            };
        }

        public static ContextRequirement CreateMandatorySchoolLifestageRequirement(ulong requirementGuid)
        {
            return new ContextRequirement
            {
                GUID = requirementGuid,
                Type = ContextRequirementType.IsInMandatorySchoolLifestage,
                MustBeTrue = true
            };
        }

        public static ContextRequirement CreateSwitchedCharacterRequirement(
            ulong requirementGuid,
            ContextRequirement requirement)
        {
            return new ContextRequirement
            {
                GUID = requirementGuid,
                Type = ContextRequirementType.SwitchCharacterAndOtherCharacterForRequirement,
                Requirement = requirement,
                MustBeTrue = true
            };
        }

        private static ActionEndCondition ToActionEndCondition(ParalivesActionCompletionMode mode)
        {
            switch (mode)
            {
                case ParalivesActionCompletionMode.InstantNoQueue:
                    return ActionEndCondition.InstantNoQueue;
                case ParalivesActionCompletionMode.FixedDurationInMinutes:
                default:
                    return ActionEndCondition.FixedDurationInMinutes;
            }
        }

        private static RequirementChecks ToRequirementChecks(ParalivesInteractionRequirementCheckScope checks)
        {
            switch (checks)
            {
                case ParalivesInteractionRequirementCheckScope.None:
                    return RequirementChecks.None;
                case ParalivesInteractionRequirementCheckScope.OnlyOnInitiator:
                    return RequirementChecks.OnlyOnInitiator;
                case ParalivesInteractionRequirementCheckScope.OnAllCharactersInvolved:
                default:
                    return RequirementChecks.OnAllCharactersInvolved;
            }
        }

        private static HowToDisplayIfNotMet ToDisplayRule(ParalivesInteractionUnavailableDisplay display)
        {
            switch (display)
            {
                case ParalivesInteractionUnavailableDisplay.GreyOut:
                    return HowToDisplayIfNotMet.GreyOut;
                case ParalivesInteractionUnavailableDisplay.Hide:
                default:
                    return HowToDisplayIfNotMet.Hide;
            }
        }

        private static ContextRequirementType ToContextRequirementType(ParalivesContextRequirementKind kind)
        {
            switch (kind)
            {
                case ParalivesContextRequirementKind.ActorHasCharacterRequirement:
                    return ContextRequirementType.CharacterHasCharacterRequirements;
                case ParalivesContextRequirementKind.TargetHasCharacterRequirement:
                    return ContextRequirementType.OtherCharacterHasCharacterRequirements;
                case ParalivesContextRequirementKind.MandatorySchoolLifeStage:
                    return ContextRequirementType.IsInMandatorySchoolLifestage;
                case ParalivesContextRequirementKind.SwitchActorAndTarget:
                    return ContextRequirementType.SwitchCharacterAndOtherCharacterForRequirement;
                case ParalivesContextRequirementKind.SameHousehold:
                default:
                    return ContextRequirementType.IsInSameHousehold;
            }
        }

        private static InteractionUsabilityRule[] CreateRules(
            IList<ParalivesInteractionUsabilityRuleDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return new InteractionUsabilityRule[0];

            List<InteractionUsabilityRule> rules = new List<InteractionUsabilityRule>();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null)
                    rules.Add(CreateRule(definitions[i]));
            }

            return rules.ToArray();
        }

        private static InteractionGroupItem[] CreateGroupItems(
            ulong parentGroupGuid,
            IList<ParalivesInteractionGroupChildDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                return new InteractionGroupItem[0];

            List<InteractionGroupItem> children = new List<InteractionGroupItem>();
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null)
                    children.Add(CreateGroupItem(parentGroupGuid, definitions[i]));
            }

            return children.ToArray();
        }

        private static InteractionGroupItem CreateGroupItem(
            ulong parentGroupGuid,
            ParalivesInteractionGroupChildDefinition definition)
        {
            if (definition.Kind == ParalivesInteractionChildKind.Group)
                return CreateGroupItem(GetChildItemGuid(parentGroupGuid, definition), definition.GroupGuid);

            return CreateInteractionItem(
                GetChildItemGuid(parentGroupGuid, definition),
                definition.InteractionGuid,
                definition.NestedInteractionDisplayName);
        }

        private static ulong GetChildItemGuid(
            ulong parentGroupGuid,
            ParalivesInteractionGroupChildDefinition definition)
        {
            if (definition.ChildItemGuid != 0UL)
                return definition.ChildItemGuid;

            string childType = definition.Kind == ParalivesInteractionChildKind.Group ? "group" : "interaction";
            ulong childGuid = definition.Kind == ParalivesInteractionChildKind.Group
                ? definition.GroupGuid
                : definition.InteractionGuid;

            return ParalivesGuid.FromStableName(
                "ParalivesAPI.InteractionGroupChild",
                parentGroupGuid + ":" + childType + ":" + childGuid);
        }
    }
}
