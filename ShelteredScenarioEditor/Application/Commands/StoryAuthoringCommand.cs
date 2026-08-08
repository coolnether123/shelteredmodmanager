using System;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum StoryAuthoringCommandKind
    {
        AddStage,
        DeleteStage,
        DuplicateStage,
        MoveStage,
        SetStageId,
        SetStageTitle,
        AddRoutedStage,
        AddUnansweredStage,
        ToggleStageCharacter,
        SetUnansweredStage,
        StepUnansweredDelay,
        ToggleUnansweredPunishment,
        AddStoryCharacter,
        DeleteStoryCharacter,
        EditStoryCharacter,
        SetStoryCharacterActor,
        ClearStoryCharacterActor,
        AddScene,
        DeleteScene,
        DuplicateScene,
        MoveScene,
        SetSceneId,
        SetSceneType,
        SetSceneNext,
        SetSceneAlternate,
        SetStageChangeTarget,
        StepStageChangeDelay,
        ToggleRecruitCharacter,
        ToggleRecruitAsFamily,
        SetEndType,
        ToggleCompleteQuest,
        ToggleCompleteScenario,
        AddOutcomeReward,
        AddTradeItem,
        ToggleTradeOverride,
        DeleteOutcomeReward,
        DeleteTradeItem,
        SetOutcomeRewardItem,
        SetTradeItem,
        StepOutcomeRewardQuantity,
        StepTradeQuantity,
        AddDialogue,
        DeleteDialogue,
        SetDialogueSpeaker,
        SetDialogueKey,
        AddOption,
        DeleteOption,
        SetOptionKey,
        SetOptionNext,
        AddRandomRoute,
        DeleteRandomRoute,
        SetRandomRouteTarget,
        AddReward,
        DeleteReward,
        SetRewardItem,
        StepRewardQuantity,
        AddRemoval,
        DeleteRemoval,
        SetRemovalItem,
        StepRemovalQuantity,
        AddMilestone,
        DeleteMilestone,
        SetMilestoneName,
        AddConversation,
        PreviewConversation,
        DeleteConversation,
        DuplicateConversation,
        MoveConversation,
        SetConversationId,
        ToggleConversationSuppression,
        SetConversationSuppressionCategory,
        SetConversationSuppressionTopic,
        SetConversationTriggerSource,
        SetConversationTriggerId,
        SetConversationTriggerWeight,
        StepConversationTriggerCooldown,
        ToggleConversationTriggerOnce,
        StepConversationTriggerDay,
        StepConversationTriggerHour,
        StepConversationTriggerMinute,
        AddConversationParticipant,
        DeleteConversationParticipant,
        SetConversationParticipantSlot,
        SetConversationParticipantStoryCharacter,
        SetConversationParticipantActor,
        SetConversationParticipantFallback,
        ToggleConversationParticipantRequired,
        AddConversationLine,
        DeleteConversationLine,
        SetConversationLineSpeaker,
        SetConversationLineText,
        SetConversationLineDelay
    }

    /// <summary>Validated payload for Story stages, scenes, characters, and conversations.</summary>
    internal sealed class StoryAuthoringCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private StoryAuthoringCommand(
            StoryAuthoringCommandKind kind,
            string automationId,
            int primaryIndex,
            int secondaryIndex,
            int childIndex,
            int delta,
            float number,
            string field,
            string value)
            : base(automationId,
                kind == StoryAuthoringCommandKind.PreviewConversation
                    ? ScenarioAuthoringCommandPolicy.World
                    : (IsDestructive(kind) ? ScenarioAuthoringCommandPolicy.SafetySnapshot : ScenarioAuthoringCommandPolicy.Default))
        {
            Kind = kind;
            PrimaryIndex = primaryIndex;
            SecondaryIndex = secondaryIndex;
            ChildIndex = childIndex;
            Delta = delta;
            Number = number;
            Field = field;
            Value = value;
        }

        internal StoryAuthoringCommandKind Kind { get; private set; }
        internal int PrimaryIndex { get; private set; }
        internal int SecondaryIndex { get; private set; }
        internal int ChildIndex { get; private set; }
        internal int Delta { get; private set; }
        internal float Number { get; private set; }
        internal string Field { get; private set; }
        internal string Value { get; private set; }

        internal bool ValidateStructure(out string reason)
        {
            StoryAuthoringCommandDescriptor descriptor = StoryAuthoringCommandCatalog.Describe(Kind);
            reason = null;
            if (descriptor.Requires(StoryAuthoringCommandRequirements.PrimaryIndex) && PrimaryIndex < 0)
                reason = "Story command primary index is invalid.";
            else if (descriptor.Requires(StoryAuthoringCommandRequirements.SecondaryIndex) && SecondaryIndex < 0)
                reason = "Story command secondary index is invalid.";
            else if (descriptor.Requires(StoryAuthoringCommandRequirements.ChildIndex) && ChildIndex < 0)
                reason = "Story command child index is invalid.";
            else if (descriptor.Requires(StoryAuthoringCommandRequirements.Delta) && Delta == 0)
                reason = "Story command step is invalid.";
            else if (descriptor.Requires(StoryAuthoringCommandRequirements.Value) && Value == null)
                reason = "Story command value is invalid.";
            else if (Kind == StoryAuthoringCommandKind.EditStoryCharacter && string.IsNullOrEmpty(Field))
                reason = "Story character field is invalid.";
            return reason == null;
        }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            return New(Kind, PrimaryIndex, SecondaryIndex, ChildIndex, Delta, Number, Field, value);
        }

        internal static StoryAuthoringCommand New(
            StoryAuthoringCommandKind kind,
            int primaryIndex = -1,
            int secondaryIndex = -1,
            int childIndex = -1,
            int delta = 0,
            float number = 0f,
            string field = null,
            string value = null)
        {
            return new StoryAuthoringCommand(kind, BuildAutomationId(kind, primaryIndex, secondaryIndex, childIndex, delta, number, field, value), primaryIndex, secondaryIndex, childIndex, delta, number, field, value);
        }

        private static string BuildAutomationId(StoryAuthoringCommandKind kind, int primary, int secondary, int child, int delta, float number, string field, string value)
        {
            string id = "scenario.story.command." + kind.ToString().ToLowerInvariant();
            if (primary >= 0) id += "." + primary.ToString(CultureInfo.InvariantCulture);
            if (secondary >= 0) id += "." + secondary.ToString(CultureInfo.InvariantCulture);
            if (child >= 0) id += "." + child.ToString(CultureInfo.InvariantCulture);
            if (delta != 0) id += ".step." + delta.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(field)) id += ".field." + ScenarioAutomationIdCodec.EncodeToken(field);
            if (value != null) id += ".value." + ScenarioAutomationIdCodec.EncodeToken(value);
            if (number != 0f) id += ".number." + number.ToString(CultureInfo.InvariantCulture);
            return id;
        }

        private static bool IsDestructive(StoryAuthoringCommandKind kind)
        {
            switch (kind)
            {
                case StoryAuthoringCommandKind.DeleteStage:
                case StoryAuthoringCommandKind.DeleteStoryCharacter:
                case StoryAuthoringCommandKind.DeleteScene:
                case StoryAuthoringCommandKind.DeleteDialogue:
                case StoryAuthoringCommandKind.DeleteOption:
                case StoryAuthoringCommandKind.DeleteRandomRoute:
                case StoryAuthoringCommandKind.DeleteReward:
                case StoryAuthoringCommandKind.DeleteRemoval:
                case StoryAuthoringCommandKind.DeleteMilestone:
                case StoryAuthoringCommandKind.DeleteConversation:
                case StoryAuthoringCommandKind.DeleteConversationParticipant:
                case StoryAuthoringCommandKind.DeleteConversationLine:
                case StoryAuthoringCommandKind.DeleteOutcomeReward:
                case StoryAuthoringCommandKind.DeleteTradeItem:
                    return true;
                default:
                    return false;
            }
        }

    }

    internal static class StoryAuthoringCommands
    {
        internal static StoryAuthoringCommand AddStage() { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.AddStage); }
        internal static StoryAuthoringCommand DeleteStage(int stage) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.DeleteStage, stage); }
        internal static StoryAuthoringCommand DuplicateStage(int stage) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.DuplicateStage, stage); }
        internal static StoryAuthoringCommand MoveStage(int stage, int delta) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.MoveStage, stage, delta: delta); }
        internal static StoryAuthoringCommand SetStageId(int stage, string value) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.SetStageId, stage, value: value); }
        internal static StoryAuthoringCommand ToggleStageCharacter(int stage, string value) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.ToggleStageCharacter, stage, value: value); }
        internal static StoryAuthoringCommand SetUnansweredStage(int stage, string value) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.SetUnansweredStage, stage, value: value); }
        internal static StoryAuthoringCommand StepUnansweredDelay(int stage, int delta) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.StepUnansweredDelay, stage, delta: delta); }
        internal static StoryAuthoringCommand ToggleUnansweredPunishment(int stage) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.ToggleUnansweredPunishment, stage); }
        internal static StoryAuthoringCommand AddStoryCharacter() { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.AddStoryCharacter); }
        internal static StoryAuthoringCommand DeleteStoryCharacter(int index) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.DeleteStoryCharacter, index); }
        internal static StoryAuthoringCommand EditStoryCharacter(int index, string field, string value = null) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.EditStoryCharacter, index, field: field, value: value); }
        internal static StoryAuthoringCommand SetStoryCharacterActor(int index, string value) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.SetStoryCharacterActor, index, value: value); }
        internal static StoryAuthoringCommand ClearStoryCharacterActor(int index) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.ClearStoryCharacterActor, index); }
        internal static StoryAuthoringCommand Scene(StoryAuthoringCommandKind kind, int stage, int scene = -1, int child = -1, int delta = 0, string value = null) { return StoryAuthoringCommand.New(kind, stage, scene, child, delta, value: value); }
        internal static StoryAuthoringCommand Conversation(StoryAuthoringCommandKind kind, int conversation = -1, int child = -1, int delta = 0, float number = 0f, string value = null) { return StoryAuthoringCommand.New(kind, conversation, child, delta: delta, number: number, value: value); }
        internal static StoryAuthoringCommand CharacterDelete(int index) { return DeleteStoryCharacter(index); }
        internal static StoryAuthoringCommand CharacterEdit(int index, string field) { return EditStoryCharacter(index, field); }
        internal static StoryAuthoringCommand StageDelete(int index) { return DeleteStage(index); }
        internal static StoryAuthoringCommand StageDuplicate(int index) { return DuplicateStage(index); }
        internal static StoryAuthoringCommand StageMove(int index, int delta) { return MoveStage(index, delta); }
        internal static StoryAuthoringCommand StageId(int index, string value) { return SetStageId(index, value); }
        internal static StoryAuthoringCommand SetStageTitle(int stage, string value) { return StoryAuthoringCommand.New(StoryAuthoringCommandKind.SetStageTitle, stage, value: value); }
        internal static StoryAuthoringCommand AddRoutedStage(int stage, int scene, bool unanswered) { return StoryAuthoringCommand.New(unanswered ? StoryAuthoringCommandKind.AddUnansweredStage : StoryAuthoringCommandKind.AddRoutedStage, stage, scene); }
        internal static StoryAuthoringCommand StageCharacterToggle(int index, string value) { return ToggleStageCharacter(index, value); }
        internal static StoryAuthoringCommand StageUnanswered(int index, string value) { return SetUnansweredStage(index, value); }
        internal static StoryAuthoringCommand StageUnansweredDelay(int index, int delta) { return StepUnansweredDelay(index, delta); }
        internal static StoryAuthoringCommand StagePunish(int index) { return ToggleUnansweredPunishment(index); }
        internal static StoryAuthoringCommand IntercomAdd(int stage) { return Scene(StoryAuthoringCommandKind.AddScene, stage); }
        internal static StoryAuthoringCommand IntercomDelete(int stage, int scene) { return Scene(StoryAuthoringCommandKind.DeleteScene, stage, scene); }
        internal static StoryAuthoringCommand IntercomDuplicate(int stage, int scene) { return Scene(StoryAuthoringCommandKind.DuplicateScene, stage, scene); }
        internal static StoryAuthoringCommand IntercomMove(int stage, int scene, int delta) { return Scene(StoryAuthoringCommandKind.MoveScene, stage, scene, delta: delta); }
        internal static StoryAuthoringCommand IntercomId(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.SetSceneId, stage, scene, value: value); }
        internal static StoryAuthoringCommand IntercomType(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.SetSceneType, stage, scene, value: value); }
        internal static StoryAuthoringCommand IntercomNext(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.SetSceneNext, stage, scene, value: value); }
        internal static StoryAuthoringCommand IntercomAlternate(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.SetSceneAlternate, stage, scene, value: value); }
        internal static StoryAuthoringCommand StageChangeTarget(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.SetStageChangeTarget, stage, scene, value: value); }
        internal static StoryAuthoringCommand StageChangeDelay(int stage, int scene, int delta) { return Scene(StoryAuthoringCommandKind.StepStageChangeDelay, stage, scene, delta: delta); }
        internal static StoryAuthoringCommand RecruitToggle(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.ToggleRecruitCharacter, stage, scene, value: value); }
        internal static StoryAuthoringCommand RecruitFamily(int stage, int scene) { return Scene(StoryAuthoringCommandKind.ToggleRecruitAsFamily, stage, scene); }
        internal static StoryAuthoringCommand EndType(int stage, int scene, string value) { return Scene(StoryAuthoringCommandKind.SetEndType, stage, scene, value: value); }
        internal static StoryAuthoringCommand EndCompleteQuest(int stage, int scene) { return Scene(StoryAuthoringCommandKind.ToggleCompleteQuest, stage, scene); }
        internal static StoryAuthoringCommand EndCompleteScenario(int stage, int scene) { return Scene(StoryAuthoringCommandKind.ToggleCompleteScenario, stage, scene); }
        internal static StoryAuthoringCommand AddOutcomeItem(int stage, int scene, bool reward) { return Scene(reward ? StoryAuthoringCommandKind.AddOutcomeReward : StoryAuthoringCommandKind.AddTradeItem, stage, scene); }
        internal static StoryAuthoringCommand ToggleTradeOverride(int stage, int scene) { return Scene(StoryAuthoringCommandKind.ToggleTradeOverride, stage, scene); }
        internal static StoryAuthoringCommand DeleteOutcomeItem(int stage, int scene, int item, bool reward) { return Scene(reward ? StoryAuthoringCommandKind.DeleteOutcomeReward : StoryAuthoringCommandKind.DeleteTradeItem, stage, scene, item); }
        internal static StoryAuthoringCommand SetOutcomeItem(int stage, int scene, int item, bool reward, string value) { return Scene(reward ? StoryAuthoringCommandKind.SetOutcomeRewardItem : StoryAuthoringCommandKind.SetTradeItem, stage, scene, item, value: value); }
        internal static StoryAuthoringCommand StepOutcomeQuantity(int stage, int scene, int item, bool reward, int delta) { return Scene(reward ? StoryAuthoringCommandKind.StepOutcomeRewardQuantity : StoryAuthoringCommandKind.StepTradeQuantity, stage, scene, item, delta); }
        internal static StoryAuthoringCommand DialogueAdd(int stage, int scene) { return Scene(StoryAuthoringCommandKind.AddDialogue, stage, scene); }
        internal static StoryAuthoringCommand DialogueDelete(int stage, int scene, int child) { return Scene(StoryAuthoringCommandKind.DeleteDialogue, stage, scene, child); }
        internal static StoryAuthoringCommand DialogueSpeaker(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetDialogueSpeaker, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand DialogueKey(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetDialogueKey, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand OptionAdd(int stage, int scene) { return Scene(StoryAuthoringCommandKind.AddOption, stage, scene); }
        internal static StoryAuthoringCommand OptionDelete(int stage, int scene, int child) { return Scene(StoryAuthoringCommandKind.DeleteOption, stage, scene, child); }
        internal static StoryAuthoringCommand OptionKey(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetOptionKey, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand OptionNext(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetOptionNext, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand RandomRouteAdd(int stage, int scene) { return Scene(StoryAuthoringCommandKind.AddRandomRoute, stage, scene); }
        internal static StoryAuthoringCommand RandomRouteDelete(int stage, int scene, int child) { return Scene(StoryAuthoringCommandKind.DeleteRandomRoute, stage, scene, child); }
        internal static StoryAuthoringCommand RandomRouteTarget(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetRandomRouteTarget, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand RewardAdd(int stage, int scene) { return Scene(StoryAuthoringCommandKind.AddReward, stage, scene); }
        internal static StoryAuthoringCommand RewardDelete(int stage, int scene, int child) { return Scene(StoryAuthoringCommandKind.DeleteReward, stage, scene, child); }
        internal static StoryAuthoringCommand RewardItem(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetRewardItem, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand RewardQuantity(int stage, int scene, int child, int delta) { return Scene(StoryAuthoringCommandKind.StepRewardQuantity, stage, scene, child, delta); }
        internal static StoryAuthoringCommand RemovalAdd(int stage, int scene) { return Scene(StoryAuthoringCommandKind.AddRemoval, stage, scene); }
        internal static StoryAuthoringCommand RemovalDelete(int stage, int scene, int child) { return Scene(StoryAuthoringCommandKind.DeleteRemoval, stage, scene, child); }
        internal static StoryAuthoringCommand RemovalItem(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetRemovalItem, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand RemovalQuantity(int stage, int scene, int child, int delta) { return Scene(StoryAuthoringCommandKind.StepRemovalQuantity, stage, scene, child, delta); }
        internal static StoryAuthoringCommand MilestoneAdd(int stage, int scene) { return Scene(StoryAuthoringCommandKind.AddMilestone, stage, scene); }
        internal static StoryAuthoringCommand MilestoneDelete(int stage, int scene, int child) { return Scene(StoryAuthoringCommandKind.DeleteMilestone, stage, scene, child); }
        internal static StoryAuthoringCommand MilestoneName(int stage, int scene, int child, string value) { return Scene(StoryAuthoringCommandKind.SetMilestoneName, stage, scene, child, value: value); }
        internal static StoryAuthoringCommand AddConversation() { return Conversation(StoryAuthoringCommandKind.AddConversation); }
        internal static StoryAuthoringCommand PreviewConversation(int conversation) { return Conversation(StoryAuthoringCommandKind.PreviewConversation, conversation); }
        internal static StoryAuthoringCommand DeleteConversation(int conversation) { return Conversation(StoryAuthoringCommandKind.DeleteConversation, conversation); }
        internal static StoryAuthoringCommand DuplicateConversation(int conversation) { return Conversation(StoryAuthoringCommandKind.DuplicateConversation, conversation); }
        internal static StoryAuthoringCommand MoveConversation(int conversation, int delta) { return Conversation(StoryAuthoringCommandKind.MoveConversation, conversation, delta: delta); }
        internal static StoryAuthoringCommand SetConversationId(int conversation, string value = null) { return Conversation(StoryAuthoringCommandKind.SetConversationId, conversation, value: value); }
        internal static StoryAuthoringCommand ToggleConversationSuppression() { return Conversation(StoryAuthoringCommandKind.ToggleConversationSuppression); }
        internal static StoryAuthoringCommand SetConversationSuppressionCategory(string value) { return Conversation(StoryAuthoringCommandKind.SetConversationSuppressionCategory, value: value); }
        internal static StoryAuthoringCommand SetConversationSuppressionTopic(string value = null) { return Conversation(StoryAuthoringCommandKind.SetConversationSuppressionTopic, value: value); }
        internal static StoryAuthoringCommand SetConversationTriggerSource(int conversation, string value) { return Conversation(StoryAuthoringCommandKind.SetConversationTriggerSource, conversation, value: value); }
        internal static StoryAuthoringCommand SetConversationTriggerId(int conversation, string value = null) { return Conversation(StoryAuthoringCommandKind.SetConversationTriggerId, conversation, value: value); }
        internal static StoryAuthoringCommand SetConversationTriggerWeight(int conversation, float delta) { return Conversation(StoryAuthoringCommandKind.SetConversationTriggerWeight, conversation, number: delta); }
        internal static StoryAuthoringCommand StepConversationTriggerCooldown(int conversation, int delta) { return Conversation(StoryAuthoringCommandKind.StepConversationTriggerCooldown, conversation, delta: delta); }
        internal static StoryAuthoringCommand ToggleConversationTriggerOnce(int conversation) { return Conversation(StoryAuthoringCommandKind.ToggleConversationTriggerOnce, conversation); }
        internal static StoryAuthoringCommand StepConversationTriggerDay(int conversation, int delta) { return Conversation(StoryAuthoringCommandKind.StepConversationTriggerDay, conversation, delta: delta); }
        internal static StoryAuthoringCommand StepConversationTriggerHour(int conversation, int delta) { return Conversation(StoryAuthoringCommandKind.StepConversationTriggerHour, conversation, delta: delta); }
        internal static StoryAuthoringCommand StepConversationTriggerMinute(int conversation, int delta) { return Conversation(StoryAuthoringCommandKind.StepConversationTriggerMinute, conversation, delta: delta); }
        internal static StoryAuthoringCommand AddConversationParticipant(int conversation) { return Conversation(StoryAuthoringCommandKind.AddConversationParticipant, conversation); }
        internal static StoryAuthoringCommand DeleteConversationParticipant(int conversation, int participant) { return Conversation(StoryAuthoringCommandKind.DeleteConversationParticipant, conversation, participant); }
        internal static StoryAuthoringCommand SetConversationParticipantSlot(int conversation, int participant, string value = null) { return Conversation(StoryAuthoringCommandKind.SetConversationParticipantSlot, conversation, participant, value: value); }
        internal static StoryAuthoringCommand SetConversationParticipantStoryCharacter(int conversation, int participant, string value) { return Conversation(StoryAuthoringCommandKind.SetConversationParticipantStoryCharacter, conversation, participant, value: value); }
        internal static StoryAuthoringCommand SetConversationParticipantActor(int conversation, int participant, string value) { return Conversation(StoryAuthoringCommandKind.SetConversationParticipantActor, conversation, participant, value: value); }
        internal static StoryAuthoringCommand SetConversationParticipantFallback(int conversation, int participant, string value) { return Conversation(StoryAuthoringCommandKind.SetConversationParticipantFallback, conversation, participant, value: value); }
        internal static StoryAuthoringCommand ToggleConversationParticipantRequired(int conversation, int participant) { return Conversation(StoryAuthoringCommandKind.ToggleConversationParticipantRequired, conversation, participant); }
        internal static StoryAuthoringCommand AddConversationLine(int conversation) { return Conversation(StoryAuthoringCommandKind.AddConversationLine, conversation); }
        internal static StoryAuthoringCommand DeleteConversationLine(int conversation, int line) { return Conversation(StoryAuthoringCommandKind.DeleteConversationLine, conversation, line); }
        internal static StoryAuthoringCommand SetConversationLineSpeaker(int conversation, int line, string value) { return Conversation(StoryAuthoringCommandKind.SetConversationLineSpeaker, conversation, line, value: value); }
        internal static StoryAuthoringCommand SetConversationLineText(int conversation, int line, string value = null) { return Conversation(StoryAuthoringCommandKind.SetConversationLineText, conversation, line, value: value); }
        internal static StoryAuthoringCommand SetConversationLineDelay(int conversation, int line, float delta) { return Conversation(StoryAuthoringCommandKind.SetConversationLineDelay, conversation, line, number: delta); }
    }
}
