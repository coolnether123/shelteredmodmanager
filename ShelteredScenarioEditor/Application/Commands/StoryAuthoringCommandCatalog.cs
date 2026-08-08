using System;

namespace ShelteredScenarioEditor.Application.Commands
{
    [Flags]
    internal enum StoryAuthoringCommandRequirements
    {
        None = 0,
        PrimaryIndex = 1,
        SecondaryIndex = 2,
        ChildIndex = 4,
        Delta = 8,
        Value = 16
    }

    internal enum StoryAuthoringCommandFamily
    {
        Flow,
        Character,
        Conversation
    }

    internal enum StoryAuthoringCommandGroup
    {
        General,
        Dialogue,
        Option,
        RandomRoute,
        Item,
        Milestone,
        OutcomeItem,
        ConversationParticipant,
        ConversationLine
    }

    internal struct StoryAuthoringCommandDescriptor
    {
        internal StoryAuthoringCommandDescriptor(
            StoryAuthoringCommandFamily family,
            StoryAuthoringCommandRequirements requirements,
            StoryAuthoringCommandGroup group)
        {
            Family = family;
            Requirements = requirements;
            Group = group;
        }

        internal StoryAuthoringCommandFamily Family { get; private set; }
        internal StoryAuthoringCommandRequirements Requirements { get; private set; }
        internal StoryAuthoringCommandGroup Group { get; private set; }

        internal bool Requires(StoryAuthoringCommandRequirements requirement)
        {
            return (Requirements & requirement) != 0;
        }
    }

    /// <summary>
    /// Exhaustive command metadata. Adding a command kind requires assigning its family and payload
    /// shape here, so enum naming and ordering cannot change validation or mutation routing.
    /// </summary>
    internal static class StoryAuthoringCommandCatalog
    {
        private static readonly StoryAuthoringCommandDescriptor FlowNone = Flow(StoryAuthoringCommandRequirements.None);
        private static readonly StoryAuthoringCommandDescriptor FlowPrimary = Flow(StoryAuthoringCommandRequirements.PrimaryIndex);
        private static readonly StoryAuthoringCommandDescriptor FlowPrimaryDelta = Flow(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.Delta);
        private static readonly StoryAuthoringCommandDescriptor FlowPrimaryValue = Flow(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.Value);
        private static readonly StoryAuthoringCommandDescriptor FlowPair = Flow(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.SecondaryIndex);
        private static readonly StoryAuthoringCommandDescriptor FlowPairDelta = Flow(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.SecondaryIndex | StoryAuthoringCommandRequirements.Delta);
        private static readonly StoryAuthoringCommandDescriptor FlowPairValue = Flow(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.SecondaryIndex | StoryAuthoringCommandRequirements.Value);
        private static readonly StoryAuthoringCommandRequirements FlowChildRequirements = StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.SecondaryIndex | StoryAuthoringCommandRequirements.ChildIndex;
        private static readonly StoryAuthoringCommandDescriptor FlowDialogue = Flow(FlowChildRequirements, StoryAuthoringCommandGroup.Dialogue);
        private static readonly StoryAuthoringCommandDescriptor FlowDialogueValue = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.Dialogue);
        private static readonly StoryAuthoringCommandDescriptor FlowOption = Flow(FlowChildRequirements, StoryAuthoringCommandGroup.Option);
        private static readonly StoryAuthoringCommandDescriptor FlowOptionValue = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.Option);
        private static readonly StoryAuthoringCommandDescriptor FlowRandomRoute = Flow(FlowChildRequirements, StoryAuthoringCommandGroup.RandomRoute);
        private static readonly StoryAuthoringCommandDescriptor FlowItem = Flow(FlowChildRequirements, StoryAuthoringCommandGroup.Item);
        private static readonly StoryAuthoringCommandDescriptor FlowItemDelta = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Delta, StoryAuthoringCommandGroup.Item);
        private static readonly StoryAuthoringCommandDescriptor FlowItemValue = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.Item);
        private static readonly StoryAuthoringCommandDescriptor FlowMilestone = Flow(FlowChildRequirements, StoryAuthoringCommandGroup.Milestone);
        private static readonly StoryAuthoringCommandDescriptor FlowMilestoneValue = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.Milestone);
        private static readonly StoryAuthoringCommandDescriptor FlowOutcomeItem = Flow(FlowChildRequirements, StoryAuthoringCommandGroup.OutcomeItem);
        private static readonly StoryAuthoringCommandDescriptor FlowOutcomeItemDelta = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Delta, StoryAuthoringCommandGroup.OutcomeItem);
        private static readonly StoryAuthoringCommandDescriptor FlowOutcomeItemValue = Flow(FlowChildRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.OutcomeItem);
        private static readonly StoryAuthoringCommandDescriptor CharacterNone = Character(StoryAuthoringCommandRequirements.None);
        private static readonly StoryAuthoringCommandDescriptor CharacterPrimary = Character(StoryAuthoringCommandRequirements.PrimaryIndex);
        private static readonly StoryAuthoringCommandDescriptor CharacterPrimaryValue = Character(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.Value);
        private static readonly StoryAuthoringCommandDescriptor ConversationNone = Conversation(StoryAuthoringCommandRequirements.None);
        private static readonly StoryAuthoringCommandDescriptor ConversationValue = Conversation(StoryAuthoringCommandRequirements.Value);
        private static readonly StoryAuthoringCommandDescriptor ConversationPrimary = Conversation(StoryAuthoringCommandRequirements.PrimaryIndex);
        private static readonly StoryAuthoringCommandDescriptor ConversationPrimaryDelta = Conversation(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.Delta);
        private static readonly StoryAuthoringCommandDescriptor ConversationPrimaryValue = Conversation(StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.Value);
        private static readonly StoryAuthoringCommandRequirements ConversationPairRequirements = StoryAuthoringCommandRequirements.PrimaryIndex | StoryAuthoringCommandRequirements.SecondaryIndex;
        private static readonly StoryAuthoringCommandDescriptor ConversationParticipant = Conversation(ConversationPairRequirements, StoryAuthoringCommandGroup.ConversationParticipant);
        private static readonly StoryAuthoringCommandDescriptor ConversationParticipantValue = Conversation(ConversationPairRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.ConversationParticipant);
        private static readonly StoryAuthoringCommandDescriptor ConversationLine = Conversation(ConversationPairRequirements, StoryAuthoringCommandGroup.ConversationLine);
        private static readonly StoryAuthoringCommandDescriptor ConversationLineValue = Conversation(ConversationPairRequirements | StoryAuthoringCommandRequirements.Value, StoryAuthoringCommandGroup.ConversationLine);

        internal static StoryAuthoringCommandDescriptor Describe(StoryAuthoringCommandKind kind)
        {
            switch (kind)
            {
                case StoryAuthoringCommandKind.AddStage:
                    return FlowNone;

                case StoryAuthoringCommandKind.DeleteStage:
                case StoryAuthoringCommandKind.DuplicateStage:
                case StoryAuthoringCommandKind.AddUnansweredStage:
                case StoryAuthoringCommandKind.ToggleUnansweredPunishment:
                case StoryAuthoringCommandKind.SetUnansweredStage:
                case StoryAuthoringCommandKind.AddScene:
                    return FlowPrimary;

                case StoryAuthoringCommandKind.MoveStage:
                case StoryAuthoringCommandKind.StepUnansweredDelay:
                    return FlowPrimaryDelta;

                case StoryAuthoringCommandKind.SetStageId:
                case StoryAuthoringCommandKind.SetStageTitle:
                case StoryAuthoringCommandKind.ToggleStageCharacter:
                    return FlowPrimaryValue;

                case StoryAuthoringCommandKind.AddRoutedStage:
                case StoryAuthoringCommandKind.DeleteScene:
                case StoryAuthoringCommandKind.DuplicateScene:
                case StoryAuthoringCommandKind.ToggleRecruitAsFamily:
                case StoryAuthoringCommandKind.ToggleCompleteQuest:
                case StoryAuthoringCommandKind.ToggleCompleteScenario:
                case StoryAuthoringCommandKind.AddOutcomeReward:
                case StoryAuthoringCommandKind.AddTradeItem:
                case StoryAuthoringCommandKind.ToggleTradeOverride:
                case StoryAuthoringCommandKind.SetSceneNext:
                case StoryAuthoringCommandKind.SetSceneAlternate:
                case StoryAuthoringCommandKind.SetStageChangeTarget:
                case StoryAuthoringCommandKind.AddDialogue:
                case StoryAuthoringCommandKind.AddOption:
                case StoryAuthoringCommandKind.AddRandomRoute:
                case StoryAuthoringCommandKind.AddReward:
                case StoryAuthoringCommandKind.AddRemoval:
                case StoryAuthoringCommandKind.AddMilestone:
                    return FlowPair;

                case StoryAuthoringCommandKind.MoveScene:
                case StoryAuthoringCommandKind.StepStageChangeDelay:
                    return FlowPairDelta;

                case StoryAuthoringCommandKind.SetSceneId:
                case StoryAuthoringCommandKind.SetSceneType:
                case StoryAuthoringCommandKind.ToggleRecruitCharacter:
                case StoryAuthoringCommandKind.SetEndType:
                    return FlowPairValue;

                case StoryAuthoringCommandKind.DeleteOutcomeReward:
                case StoryAuthoringCommandKind.DeleteTradeItem:
                    return FlowOutcomeItem;

                case StoryAuthoringCommandKind.DeleteDialogue:
                    return FlowDialogue;

                case StoryAuthoringCommandKind.DeleteOption:
                case StoryAuthoringCommandKind.SetOptionNext:
                    return FlowOption;

                case StoryAuthoringCommandKind.DeleteRandomRoute:
                case StoryAuthoringCommandKind.SetRandomRouteTarget:
                    return FlowRandomRoute;

                case StoryAuthoringCommandKind.DeleteReward:
                case StoryAuthoringCommandKind.DeleteRemoval:
                    return FlowItem;

                case StoryAuthoringCommandKind.DeleteMilestone:
                    return FlowMilestone;

                case StoryAuthoringCommandKind.StepOutcomeRewardQuantity:
                case StoryAuthoringCommandKind.StepTradeQuantity:
                    return FlowOutcomeItemDelta;

                case StoryAuthoringCommandKind.StepRewardQuantity:
                case StoryAuthoringCommandKind.StepRemovalQuantity:
                    return FlowItemDelta;

                case StoryAuthoringCommandKind.SetOutcomeRewardItem:
                case StoryAuthoringCommandKind.SetTradeItem:
                    return FlowOutcomeItemValue;

                case StoryAuthoringCommandKind.SetDialogueSpeaker:
                case StoryAuthoringCommandKind.SetDialogueKey:
                    return FlowDialogueValue;

                case StoryAuthoringCommandKind.SetOptionKey:
                    return FlowOptionValue;

                case StoryAuthoringCommandKind.SetRewardItem:
                case StoryAuthoringCommandKind.SetRemovalItem:
                    return FlowItemValue;

                case StoryAuthoringCommandKind.SetMilestoneName:
                    return FlowMilestoneValue;

                case StoryAuthoringCommandKind.AddStoryCharacter:
                    return CharacterNone;

                case StoryAuthoringCommandKind.DeleteStoryCharacter:
                case StoryAuthoringCommandKind.ClearStoryCharacterActor:
                    return CharacterPrimary;

                case StoryAuthoringCommandKind.EditStoryCharacter:
                case StoryAuthoringCommandKind.SetStoryCharacterActor:
                    return CharacterPrimaryValue;

                case StoryAuthoringCommandKind.AddConversation:
                case StoryAuthoringCommandKind.ToggleConversationSuppression:
                    return ConversationNone;

                case StoryAuthoringCommandKind.SetConversationSuppressionCategory:
                case StoryAuthoringCommandKind.SetConversationSuppressionTopic:
                    return ConversationValue;

                case StoryAuthoringCommandKind.PreviewConversation:
                case StoryAuthoringCommandKind.DeleteConversation:
                case StoryAuthoringCommandKind.DuplicateConversation:
                case StoryAuthoringCommandKind.SetConversationTriggerWeight:
                case StoryAuthoringCommandKind.ToggleConversationTriggerOnce:
                case StoryAuthoringCommandKind.AddConversationParticipant:
                case StoryAuthoringCommandKind.AddConversationLine:
                    return ConversationPrimary;

                case StoryAuthoringCommandKind.MoveConversation:
                case StoryAuthoringCommandKind.StepConversationTriggerCooldown:
                case StoryAuthoringCommandKind.StepConversationTriggerDay:
                case StoryAuthoringCommandKind.StepConversationTriggerHour:
                case StoryAuthoringCommandKind.StepConversationTriggerMinute:
                    return ConversationPrimaryDelta;

                case StoryAuthoringCommandKind.SetConversationId:
                case StoryAuthoringCommandKind.SetConversationTriggerSource:
                case StoryAuthoringCommandKind.SetConversationTriggerId:
                    return ConversationPrimaryValue;

                case StoryAuthoringCommandKind.DeleteConversationParticipant:
                case StoryAuthoringCommandKind.ToggleConversationParticipantRequired:
                    return ConversationParticipant;

                case StoryAuthoringCommandKind.DeleteConversationLine:
                case StoryAuthoringCommandKind.SetConversationLineDelay:
                    return ConversationLine;

                case StoryAuthoringCommandKind.SetConversationParticipantSlot:
                case StoryAuthoringCommandKind.SetConversationParticipantStoryCharacter:
                case StoryAuthoringCommandKind.SetConversationParticipantActor:
                case StoryAuthoringCommandKind.SetConversationParticipantFallback:
                    return ConversationParticipantValue;

                case StoryAuthoringCommandKind.SetConversationLineSpeaker:
                case StoryAuthoringCommandKind.SetConversationLineText:
                    return ConversationLineValue;

                default:
                    throw new ArgumentOutOfRangeException("kind", kind, "Story command kind has no descriptor.");
            }
        }

        private static StoryAuthoringCommandDescriptor Flow(
            StoryAuthoringCommandRequirements requirements,
            StoryAuthoringCommandGroup group = StoryAuthoringCommandGroup.General)
        {
            return new StoryAuthoringCommandDescriptor(StoryAuthoringCommandFamily.Flow, requirements, group);
        }

        private static StoryAuthoringCommandDescriptor Character(StoryAuthoringCommandRequirements requirements)
        {
            return new StoryAuthoringCommandDescriptor(StoryAuthoringCommandFamily.Character, requirements, StoryAuthoringCommandGroup.General);
        }

        private static StoryAuthoringCommandDescriptor Conversation(
            StoryAuthoringCommandRequirements requirements,
            StoryAuthoringCommandGroup group = StoryAuthoringCommandGroup.General)
        {
            return new StoryAuthoringCommandDescriptor(StoryAuthoringCommandFamily.Conversation, requirements, group);
        }
    }
}
