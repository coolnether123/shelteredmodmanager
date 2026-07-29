using System;

namespace ParalivesAPI.Core
{
    public static class ParalivesInteractionBuilders
    {
        public static ParalivesInteractionPackBuilder Pack(string id)
        {
            return new ParalivesInteractionPackBuilder(id);
        }

        public static ParalivesActionDefinitionBuilder Action(ulong guid, string displayName)
        {
            return new ParalivesActionDefinitionBuilder(guid, displayName);
        }

        public static ParalivesInteractionDefinitionBuilder Interaction(
            ulong guid,
            string displayName,
            ulong actionGuid)
        {
            return new ParalivesInteractionDefinitionBuilder(guid, displayName, actionGuid);
        }

        public static ParalivesInteractionGroupDefinitionBuilder Group(ulong guid, string displayName)
        {
            return new ParalivesInteractionGroupDefinitionBuilder(guid, displayName);
        }
    }

    public sealed class ParalivesInteractionPackBuilder
    {
        private readonly ParalivesInteractionPack _pack;

        public ParalivesInteractionPackBuilder(string id)
        {
            _pack = new ParalivesInteractionPack(id);
        }

        public ParalivesInteractionPackBuilder WithDisplayName(string displayName)
        {
            _pack.DisplayName = displayName;
            return this;
        }

        public ParalivesInteractionPackBuilder AddAction(ParalivesActionDefinition action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            _pack.Actions.Add(action);
            return this;
        }

        public ParalivesInteractionPackBuilder AddAction(ParalivesActionDefinitionBuilder action)
        {
            if (action == null)
                throw new ArgumentNullException("action");

            return AddAction(action.Build());
        }

        public ParalivesInteractionPackBuilder AddInstantAction(ulong guid, string displayName)
        {
            return AddAction(ParalivesInteractionBuilders.Action(guid, displayName).Instant().Build());
        }

        public ParalivesInteractionPackBuilder AddTimedAction(
            ulong guid,
            string displayName,
            float durationMinutes)
        {
            return AddAction(ParalivesInteractionBuilders.Action(guid, displayName).Timed(durationMinutes).Build());
        }

        public ParalivesInteractionPackBuilder AddConversationAction(
            ulong guid,
            string displayName,
            float durationMinutes,
            ulong animationGuid,
            ulong socialClusterRulesGuid)
        {
            return AddAction(ParalivesInteractionBuilders
                .Action(guid, displayName)
                .Timed(durationMinutes)
                .WithConversation(animationGuid, socialClusterRulesGuid)
                .Build());
        }

        public ParalivesInteractionPackBuilder AddGroup(ParalivesInteractionGroupDefinition group)
        {
            if (group == null)
                throw new ArgumentNullException("group");

            _pack.Groups.Add(group);
            return this;
        }

        public ParalivesInteractionPackBuilder AddGroup(ParalivesInteractionGroupDefinitionBuilder group)
        {
            if (group == null)
                throw new ArgumentNullException("group");

            return AddGroup(group.Build());
        }

        public ParalivesInteractionPackBuilder AddGroup(ulong guid, string displayName)
        {
            return AddGroup(ParalivesInteractionBuilders.Group(guid, displayName).Build());
        }

        public ParalivesInteractionPackBuilder AddInteraction(ParalivesInteractionDefinition interaction)
        {
            if (interaction == null)
                throw new ArgumentNullException("interaction");

            _pack.Interactions.Add(interaction);
            return this;
        }

        public ParalivesInteractionPackBuilder AddInteraction(ParalivesInteractionDefinitionBuilder interaction)
        {
            if (interaction == null)
                throw new ArgumentNullException("interaction");

            return AddInteraction(interaction.Build());
        }

        public ParalivesInteractionPackBuilder AddInteraction(
            ulong guid,
            string displayName,
            ulong actionGuid)
        {
            return AddInteraction(ParalivesInteractionBuilders.Interaction(guid, displayName, actionGuid).Build());
        }

        public ParalivesInteractionPackBuilder AddGroupChild(ParalivesInteractionGroupChildDefinition child)
        {
            if (child == null)
                throw new ArgumentNullException("child");

            _pack.GroupChildren.Add(child);
            return this;
        }

        public ParalivesInteractionPackBuilder AddInteractionToGroup(
            ulong parentGroupGuid,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            return AddGroupChild(ParalivesInteractionGroupChildDefinition.ForInteraction(
                parentGroupGuid,
                interactionGuid,
                nestedInteractionDisplayName,
                childItemGuid));
        }

        public ParalivesInteractionPackBuilder AddInteractionToRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            return AddGroupChild(ParalivesInteractionGroupChildDefinition.ForInteractionInRootGroup(
                parentRootGroup,
                interactionGuid,
                nestedInteractionDisplayName,
                childItemGuid));
        }

        public ParalivesInteractionPackBuilder AddInteractionToOtherCharacterInteractions(
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            return AddInteractionToRootGroup(
                ParalivesInteractionRootGroup.OtherCharacter,
                interactionGuid,
                nestedInteractionDisplayName,
                childItemGuid);
        }

        public ParalivesInteractionPackBuilder AddGroupToGroup(
            ulong parentGroupGuid,
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            return AddGroupChild(ParalivesInteractionGroupChildDefinition.ForGroup(
                parentGroupGuid,
                groupGuid,
                childItemGuid));
        }

        public ParalivesInteractionPackBuilder AddGroupToRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            return AddGroupChild(ParalivesInteractionGroupChildDefinition.ForGroupInRootGroup(
                parentRootGroup,
                groupGuid,
                childItemGuid));
        }

        public ParalivesInteractionPackBuilder AddGroupToOtherCharacterInteractions(
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            return AddGroupToRootGroup(
                ParalivesInteractionRootGroup.OtherCharacter,
                groupGuid,
                childItemGuid);
        }

        public ParalivesInteractionPack Build()
        {
            return _pack;
        }
    }

    public sealed class ParalivesActionDefinitionBuilder
    {
        private readonly ParalivesActionDefinition _definition;

        public ParalivesActionDefinitionBuilder(ulong guid, string displayName)
        {
            _definition = new ParalivesActionDefinition
            {
                Guid = guid,
                DisplayName = displayName
            };
        }

        public ParalivesActionDefinitionBuilder Timed(float durationMinutes)
        {
            _definition.CompletionMode = ParalivesActionCompletionMode.FixedDurationInMinutes;
            _definition.DurationMinutes = durationMinutes;
            _definition.IsCancellable = true;
            return this;
        }

        public ParalivesActionDefinitionBuilder Instant()
        {
            _definition.CompletionMode = ParalivesActionCompletionMode.InstantNoQueue;
            _definition.DurationMinutes = 0.1f;
            _definition.IsCancellable = false;
            return this;
        }

        public ParalivesActionDefinitionBuilder Cancellable(bool isCancellable)
        {
            _definition.IsCancellable = isCancellable;
            return this;
        }

        public ParalivesActionDefinitionBuilder WithAnimation(ulong animationGuid)
        {
            _definition.AnimationGuid = animationGuid;
            return this;
        }

        public ParalivesActionDefinitionBuilder WithConversation(
            ulong animationGuid,
            ulong socialClusterRulesGuid)
        {
            _definition.AnimationGuid = animationGuid;
            _definition.HasConversation = true;
            _definition.SocialClusterRulesGuid = socialClusterRulesGuid;
            _definition.CancelSocialClusterIfAnyoneLeave = true;
            _definition.WaitForEveryoneToBeDone = true;
            _definition.SocialTargetCharacterMovesAndTurn = true;
            _definition.MinimumDistanceBetweenCharacters = 0.5f;
            return this;
        }

        public ParalivesActionDefinitionBuilder WithSocialCluster(
            ulong socialClusterRulesGuid,
            bool waitForEveryoneToBeDone,
            bool cancelIfAnyoneLeaves)
        {
            _definition.SocialClusterRulesGuid = socialClusterRulesGuid;
            _definition.WaitForEveryoneToBeDone = waitForEveryoneToBeDone;
            _definition.CancelSocialClusterIfAnyoneLeave = cancelIfAnyoneLeaves;
            return this;
        }

        public ParalivesActionDefinition Build()
        {
            return _definition;
        }
    }

    public sealed class ParalivesInteractionDefinitionBuilder
    {
        private readonly ParalivesInteractionDefinition _definition;

        public ParalivesInteractionDefinitionBuilder(ulong guid, string displayName, ulong actionGuid)
        {
            _definition = new ParalivesInteractionDefinition
            {
                Guid = guid,
                DisplayName = displayName,
                ActionGuid = actionGuid
            };
        }

        public ParalivesInteractionDefinitionBuilder RequiresActor(ulong characterRequirementGuid)
        {
            _definition.CharacterRequirementGuid = characterRequirementGuid;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder RequiresTarget(ulong characterRequirementGuid)
        {
            _definition.OtherCharacterRequirementGuid = characterRequirementGuid;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder WithMenuIcon(ulong iconGuid)
        {
            _definition.InteractionMenuIconGuid = iconGuid;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder WithQueueIcon(ulong iconGuid)
        {
            _definition.InteractionQueueIconGuid = iconGuid;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder ForceSolo(bool forceSolo = true)
        {
            _definition.ForceInteractionDoneSolo = forceSolo;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder Instant(bool onlyOneCharacter = true)
        {
            _definition.IsInstant = true;
            _definition.InstantIsDoneByOnlyOneCharacter = onlyOneCharacter;
            _definition.IsPlayerCancellable = false;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder PlayerCancellable(bool isCancellable)
        {
            _definition.IsPlayerCancellable = isCancellable;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder NeedsCanBeCritical(bool canPerform)
        {
            _definition.CanPerformIfNeedsAreCritical = canPerform;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder WithRequirementChecks(
            ParalivesInteractionRequirementCheckScope startingChecks,
            ParalivesInteractionRequirementCheckScope runningChecks)
        {
            _definition.StartingRequirementChecks = startingChecks;
            _definition.RunningRequirementChecks = runningChecks;
            return this;
        }

        public ParalivesInteractionDefinitionBuilder AddUsabilityRule(
            ParalivesInteractionUsabilityRuleDefinition rule)
        {
            if (rule == null)
                throw new ArgumentNullException("rule");

            _definition.InteractionUsabilityRules.Add(rule);
            return this;
        }

        public ParalivesInteractionDefinitionBuilder HideUnlessSameHousehold(
            ulong ruleGuid,
            ulong requirementGuid)
        {
            return AddUsabilityRule(CreateRule(
                ruleGuid,
                ParalivesContextRequirementDefinition.SameHousehold(requirementGuid)));
        }

        public ParalivesInteractionDefinitionBuilder HideUnlessActorHasRequirement(
            ulong ruleGuid,
            ulong requirementGuid,
            ulong characterRequirementGuid)
        {
            return AddUsabilityRule(CreateRule(
                ruleGuid,
                ParalivesContextRequirementDefinition.ActorHasCharacterRequirement(
                    requirementGuid,
                    characterRequirementGuid)));
        }

        public ParalivesInteractionDefinitionBuilder HideUnlessTargetHasRequirement(
            ulong ruleGuid,
            ulong requirementGuid,
            ulong characterRequirementGuid)
        {
            return AddUsabilityRule(CreateRule(
                ruleGuid,
                ParalivesContextRequirementDefinition.TargetHasCharacterRequirement(
                    requirementGuid,
                    characterRequirementGuid)));
        }

        public ParalivesInteractionDefinitionBuilder HideUnlessMandatorySchoolLifeStage(
            ulong ruleGuid,
            ulong requirementGuid)
        {
            return AddUsabilityRule(CreateRule(
                ruleGuid,
                ParalivesContextRequirementDefinition.MandatorySchoolLifeStage(requirementGuid)));
        }

        public ParalivesInteractionDefinitionBuilder HideUnlessTargetMandatorySchoolLifeStage(
            ulong ruleGuid,
            ulong switchRequirementGuid,
            ulong targetRequirementGuid)
        {
            return AddUsabilityRule(CreateRule(
                ruleGuid,
                ParalivesContextRequirementDefinition.SwitchActorAndTarget(
                    switchRequirementGuid,
                    ParalivesContextRequirementDefinition.MandatorySchoolLifeStage(targetRequirementGuid))));
        }

        public ParalivesInteractionDefinition Build()
        {
            return _definition;
        }

        private static ParalivesInteractionUsabilityRuleDefinition CreateRule(
            ulong ruleGuid,
            ParalivesContextRequirementDefinition requirement)
        {
            return new ParalivesInteractionUsabilityRuleDefinition
            {
                Guid = ruleGuid,
                Requirement = requirement,
                DisplayWhenNotMet = ParalivesInteractionUnavailableDisplay.Hide
            };
        }
    }

    public sealed class ParalivesInteractionGroupDefinitionBuilder
    {
        private readonly ParalivesInteractionGroupDefinition _definition;

        public ParalivesInteractionGroupDefinitionBuilder(ulong guid, string displayName)
        {
            _definition = new ParalivesInteractionGroupDefinition
            {
                Guid = guid,
                DisplayName = displayName
            };
        }

        public ParalivesInteractionGroupDefinitionBuilder MouseOverHighlight(bool enabled)
        {
            _definition.ItemMouseOverHighlight = enabled;
            return this;
        }

        public ParalivesInteractionGroupDefinitionBuilder AddInteraction(
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            _definition.Children.Add(ParalivesInteractionGroupChildDefinition.ForInteraction(
                _definition.Guid,
                interactionGuid,
                nestedInteractionDisplayName,
                childItemGuid));
            return this;
        }

        public ParalivesInteractionGroupDefinitionBuilder AddGroup(
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            _definition.Children.Add(ParalivesInteractionGroupChildDefinition.ForGroup(
                _definition.Guid,
                groupGuid,
                childItemGuid));
            return this;
        }

        public ParalivesInteractionGroupDefinition Build()
        {
            return _definition;
        }
    }
}
