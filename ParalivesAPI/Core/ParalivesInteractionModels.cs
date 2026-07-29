using System.Collections.Generic;

namespace ParalivesAPI.Core
{
    public enum ParalivesActionCompletionMode
    {
        FixedDurationInMinutes,
        InstantNoQueue
    }

    public enum ParalivesInteractionChildKind
    {
        Interaction,
        Group
    }

    public enum ParalivesInteractionUnavailableDisplay
    {
        Hide,
        GreyOut
    }

    public enum ParalivesInteractionRequirementCheckScope
    {
        None,
        OnAllCharactersInvolved,
        OnlyOnInitiator
    }

    public enum ParalivesContextRequirementKind
    {
        SameHousehold,
        ActorHasCharacterRequirement,
        TargetHasCharacterRequirement,
        MandatorySchoolLifeStage,
        SwitchActorAndTarget
    }

    public sealed class ParalivesInteractionPack
    {
        public ParalivesInteractionPack()
            : this(null)
        {
        }

        public ParalivesInteractionPack(string id)
        {
            Id = id;
            Actions = new List<ParalivesActionDefinition>();
            Groups = new List<ParalivesInteractionGroupDefinition>();
            Interactions = new List<ParalivesInteractionDefinition>();
            GroupChildren = new List<ParalivesInteractionGroupChildDefinition>();
        }

        public string Id { get; set; }

        public string DisplayName { get; set; }

        public IList<ParalivesActionDefinition> Actions { get; private set; }

        public IList<ParalivesInteractionGroupDefinition> Groups { get; private set; }

        public IList<ParalivesInteractionDefinition> Interactions { get; private set; }

        public IList<ParalivesInteractionGroupChildDefinition> GroupChildren { get; private set; }
    }

    public sealed class ParalivesActionDefinition
    {
        public ParalivesActionDefinition()
        {
            CompletionMode = ParalivesActionCompletionMode.FixedDurationInMinutes;
            DurationMinutes = 5f;
            IsCancellable = true;
            SocialTargetCharacterMovesAndTurn = true;
            MinimumDistanceBetweenCharacters = 0.5f;
        }

        public ulong Guid { get; set; }

        public string DisplayName { get; set; }

        public ParalivesActionCompletionMode CompletionMode { get; set; }

        public float DurationMinutes { get; set; }

        public bool IsCancellable { get; set; }

        public ulong AnimationGuid { get; set; }

        public bool HasConversation { get; set; }

        public ulong SocialClusterRulesGuid { get; set; }

        public bool CancelSocialClusterIfAnyoneLeave { get; set; }

        public bool WaitForEveryoneToBeDone { get; set; }

        public bool SocialTargetCharacterMovesAndTurn { get; set; }

        public float MinimumDistanceBetweenCharacters { get; set; }
    }

    public sealed class ParalivesInteractionDefinition
    {
        public ParalivesInteractionDefinition()
        {
            InteractionUsabilityRules = new List<ParalivesInteractionUsabilityRuleDefinition>();
            CanPerformIfNeedsAreCritical = true;
            InstantIsDoneByOnlyOneCharacter = true;
            IsPlayerCancellable = true;
            StartingRequirementChecks = ParalivesInteractionRequirementCheckScope.OnAllCharactersInvolved;
            RunningRequirementChecks = ParalivesInteractionRequirementCheckScope.OnAllCharactersInvolved;
        }

        public ulong Guid { get; set; }

        public string DisplayName { get; set; }

        public ulong ActionGuid { get; set; }

        public ulong CharacterRequirementGuid { get; set; }

        public ulong OtherCharacterRequirementGuid { get; set; }

        public ulong InteractionMenuIconGuid { get; set; }

        public ulong InteractionQueueIconGuid { get; set; }

        public bool ForceInteractionDoneSolo { get; set; }

        public bool IsInstant { get; set; }

        public bool InstantIsDoneByOnlyOneCharacter { get; set; }

        public bool CanPerformIfNeedsAreCritical { get; set; }

        public bool IsPlayerCancellable { get; set; }

        public ParalivesInteractionRequirementCheckScope StartingRequirementChecks { get; set; }

        public ParalivesInteractionRequirementCheckScope RunningRequirementChecks { get; set; }

        public IList<ParalivesInteractionUsabilityRuleDefinition> InteractionUsabilityRules { get; private set; }
    }

    public sealed class ParalivesInteractionGroupDefinition
    {
        public ParalivesInteractionGroupDefinition()
        {
            ItemMouseOverHighlight = true;
            Children = new List<ParalivesInteractionGroupChildDefinition>();
        }

        public ulong Guid { get; set; }

        public string DisplayName { get; set; }

        public bool ItemMouseOverHighlight { get; set; }

        public IList<ParalivesInteractionGroupChildDefinition> Children { get; private set; }
    }

    public sealed class ParalivesInteractionGroupChildDefinition
    {
        private ParalivesInteractionGroupChildDefinition()
        {
        }

        public ulong ParentGroupGuid { get; private set; }

        public bool UsesParentRootGroup { get; private set; }

        public ParalivesInteractionRootGroup ParentRootGroup { get; private set; }

        public ulong ChildItemGuid { get; private set; }

        public ParalivesInteractionChildKind Kind { get; private set; }

        public ulong InteractionGuid { get; private set; }

        public ulong GroupGuid { get; private set; }

        public string NestedInteractionDisplayName { get; private set; }

        public static ParalivesInteractionGroupChildDefinition ForInteraction(
            ulong parentGroupGuid,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            return new ParalivesInteractionGroupChildDefinition
            {
                ParentGroupGuid = parentGroupGuid,
                UsesParentRootGroup = false,
                ChildItemGuid = childItemGuid,
                Kind = ParalivesInteractionChildKind.Interaction,
                InteractionGuid = interactionGuid,
                GroupGuid = 0UL,
                NestedInteractionDisplayName = nestedInteractionDisplayName
            };
        }

        public static ParalivesInteractionGroupChildDefinition ForGroup(
            ulong parentGroupGuid,
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            return new ParalivesInteractionGroupChildDefinition
            {
                ParentGroupGuid = parentGroupGuid,
                UsesParentRootGroup = false,
                ChildItemGuid = childItemGuid,
                Kind = ParalivesInteractionChildKind.Group,
                InteractionGuid = 0UL,
                GroupGuid = groupGuid,
                NestedInteractionDisplayName = null
            };
        }

        public static ParalivesInteractionGroupChildDefinition ForInteractionInRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong interactionGuid,
            string nestedInteractionDisplayName = null,
            ulong childItemGuid = 0UL)
        {
            return new ParalivesInteractionGroupChildDefinition
            {
                ParentGroupGuid = 0UL,
                UsesParentRootGroup = true,
                ParentRootGroup = parentRootGroup,
                ChildItemGuid = childItemGuid,
                Kind = ParalivesInteractionChildKind.Interaction,
                InteractionGuid = interactionGuid,
                GroupGuid = 0UL,
                NestedInteractionDisplayName = nestedInteractionDisplayName
            };
        }

        public static ParalivesInteractionGroupChildDefinition ForGroupInRootGroup(
            ParalivesInteractionRootGroup parentRootGroup,
            ulong groupGuid,
            ulong childItemGuid = 0UL)
        {
            return new ParalivesInteractionGroupChildDefinition
            {
                ParentGroupGuid = 0UL,
                UsesParentRootGroup = true,
                ParentRootGroup = parentRootGroup,
                ChildItemGuid = childItemGuid,
                Kind = ParalivesInteractionChildKind.Group,
                InteractionGuid = 0UL,
                GroupGuid = groupGuid,
                NestedInteractionDisplayName = null
            };
        }
    }

    public sealed class ParalivesInteractionUsabilityRuleDefinition
    {
        public ParalivesInteractionUsabilityRuleDefinition()
        {
            DisplayWhenNotMet = ParalivesInteractionUnavailableDisplay.Hide;
        }

        public ulong Guid { get; set; }

        public ParalivesContextRequirementDefinition Requirement { get; set; }

        public ParalivesInteractionUnavailableDisplay DisplayWhenNotMet { get; set; }

        public bool ShowCooldownFromRequirements { get; set; }

        public bool ShowCostFromRequirements { get; set; }
    }

    public sealed class ParalivesContextRequirementDefinition
    {
        public ParalivesContextRequirementDefinition()
        {
            MustBeTrue = true;
        }

        public ulong Guid { get; set; }

        public ParalivesContextRequirementKind Kind { get; set; }

        public bool MustBeTrue { get; set; }

        public ulong CharacterRequirementGuid { get; set; }

        public ParalivesContextRequirementDefinition Requirement { get; set; }

        public static ParalivesContextRequirementDefinition SameHousehold(ulong requirementGuid)
        {
            return new ParalivesContextRequirementDefinition
            {
                Guid = requirementGuid,
                Kind = ParalivesContextRequirementKind.SameHousehold,
                MustBeTrue = true
            };
        }

        public static ParalivesContextRequirementDefinition ActorHasCharacterRequirement(
            ulong requirementGuid,
            ulong characterRequirementGuid)
        {
            return new ParalivesContextRequirementDefinition
            {
                Guid = requirementGuid,
                Kind = ParalivesContextRequirementKind.ActorHasCharacterRequirement,
                CharacterRequirementGuid = characterRequirementGuid,
                MustBeTrue = true
            };
        }

        public static ParalivesContextRequirementDefinition TargetHasCharacterRequirement(
            ulong requirementGuid,
            ulong characterRequirementGuid)
        {
            return new ParalivesContextRequirementDefinition
            {
                Guid = requirementGuid,
                Kind = ParalivesContextRequirementKind.TargetHasCharacterRequirement,
                CharacterRequirementGuid = characterRequirementGuid,
                MustBeTrue = true
            };
        }

        public static ParalivesContextRequirementDefinition MandatorySchoolLifeStage(ulong requirementGuid)
        {
            return new ParalivesContextRequirementDefinition
            {
                Guid = requirementGuid,
                Kind = ParalivesContextRequirementKind.MandatorySchoolLifeStage,
                MustBeTrue = true
            };
        }

        public static ParalivesContextRequirementDefinition SwitchActorAndTarget(
            ulong requirementGuid,
            ParalivesContextRequirementDefinition requirement)
        {
            return new ParalivesContextRequirementDefinition
            {
                Guid = requirementGuid,
                Kind = ParalivesContextRequirementKind.SwitchActorAndTarget,
                Requirement = requirement,
                MustBeTrue = true
            };
        }
    }
}
