using Setting;

namespace ParalivesAPI.Core
{
    public static class ParalivesInteractionFactory
    {
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
    }
}
