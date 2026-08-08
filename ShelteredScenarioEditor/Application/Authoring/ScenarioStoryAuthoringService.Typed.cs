using System;
using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Domain.Validation;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed partial class ScenarioStoryAuthoringService
    {
        public bool TryHandleCommand(ScenarioEditorSession session, StoryAuthoringCommand command, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return false;
            }
            string reason = null;
            if (command == null || !command.ValidateStructure(out reason))
            {
                message = reason ?? "Story command is invalid.";
                return false;
            }

            ScenarioDefinition definition = session.WorkingDefinition;
            StoryAuthoringCommandFamily family = StoryAuthoringCommandCatalog.Describe(command.Kind).Family;
            if (command.Kind == StoryAuthoringCommandKind.AddStage) return AddStage(session, EnsureFlow(definition), out message);
            if (command.Kind == StoryAuthoringCommandKind.AddStoryCharacter) return AddCharacter(session, definition, out message);
            switch (family)
            {
                case StoryAuthoringCommandFamily.Conversation:
                    return HandleConversationCommand(session, definition, command, out message);
                case StoryAuthoringCommandFamily.Character:
                    return HandleCharacterCommand(session, definition, command, out message);
                case StoryAuthoringCommandFamily.Flow:
                    return HandleFlowCommand(session, definition, command, out message);
                default:
                    throw new ArgumentOutOfRangeException("family", family, "Story command family is not supported.");
            }
        }

        private bool HandleCharacterCommand(ScenarioEditorSession session, ScenarioDefinition definition, StoryAuthoringCommand command, out string message)
        {
            List<ScenarioNpcDefinition> characters = EnsureCharacters(definition);
            if (!ValidIndex(command.PrimaryIndex, characters.Count, "Story character", out message)) return false;
            ScenarioNpcDefinition character = characters[command.PrimaryIndex];
            if (character == null) { message = "Story character row is empty."; return false; }
            switch (command.Kind)
            {
                case StoryAuthoringCommandKind.DeleteStoryCharacter:
                    return DeleteCharacter(session, definition, command.PrimaryIndex, out message);
                case StoryAuthoringCommandKind.SetStoryCharacterActor:
                {
                    ScenarioCastMemberReferenceCandidate candidate;
                    if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, true, true, command.Value, out candidate)) { message = "Actor reference is missing."; return false; }
                    character.ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef);
                    MarkDirty(session); message = "Linked story character to " + candidate.DisplayName + "."; return true;
                }
                case StoryAuthoringCommandKind.ClearStoryCharacterActor:
                    character.ActorRef = null; MarkDirty(session); message = "Cleared story character actor link."; return true;
                case StoryAuthoringCommandKind.EditStoryCharacter:
                    return EditCharacter(session, character, command.Field, command.Value, out message);
                default: return false;
            }
        }

        private static bool EditCharacter(ScenarioEditorSession session, ScenarioNpcDefinition character, string field, string value, out string message)
        {
            string normalized = value != null ? value.Trim() : string.Empty;
            if (string.Equals(field, "displayName", StringComparison.OrdinalIgnoreCase))
            {
                if (normalized.Length == 0) { message = "Story character display name cannot be empty."; return false; }
                character.DisplayName = normalized;
            }
            else if (string.Equals(field, "presetId", StringComparison.OrdinalIgnoreCase)) character.PresetId = normalized;
            else if (string.Equals(field, "personality", StringComparison.OrdinalIgnoreCase)) character.Personality = normalized;
            else if (string.Equals(field, "species", StringComparison.OrdinalIgnoreCase)) character.Species = normalized;
            else { message = "Unknown story character field '" + field + "'."; return false; }
            MarkDirty(session); message = "Updated story character '" + DisplayCharacterName(character) + "'."; return true;
        }

        private bool HandleFlowCommand(ScenarioEditorSession session, ScenarioDefinition definition, StoryAuthoringCommand command, out string message)
        {
            ScenarioFlowDefinition flow = EnsureFlow(definition);
            if (!ValidIndex(command.PrimaryIndex, flow.Stages.Count, "Story stage", out message)) return false;
            ScenarioFlowStageDefinition stage = flow.Stages[command.PrimaryIndex];
            switch (command.Kind)
            {
                case StoryAuthoringCommandKind.DeleteStage:
                {
                    string reason;
                    if (!CanRemoveStage(definition, stage != null ? stage.Id : null, out reason)) { message = reason; return false; }
                    RecordUndo(session, "Remove story stage"); flow.Stages.RemoveAt(command.PrimaryIndex); MarkDirty(session); message = "Removed story stage."; return true;
                }
                case StoryAuthoringCommandKind.DuplicateStage:
                {
                    ScenarioFlowStageDefinition copy = CloneStage(stage, NextStageId(flow)); flow.Stages.Insert(command.PrimaryIndex + 1, copy); MarkDirty(session); message = "Duplicated story stage '" + copy.Id + "'."; return true;
                }
                case StoryAuthoringCommandKind.MoveStage: return Move(flow.Stages, command.PrimaryIndex, command.Delta, session, "story stage", out message);
                case StoryAuthoringCommandKind.SetStageId: return RenameStage(session, definition, flow, command.PrimaryIndex, command.Value, out message);
                case StoryAuthoringCommandKind.SetStageTitle:
                {
                    ScenarioIntercomStageDefinition first = stage.IntercomStages.Count > 0 ? stage.IntercomStages[0] : CreateIntercom(stage);
                    if (stage.IntercomStages.Count == 0) stage.IntercomStages.Add(first);
                    first.StageDescriptionKey = string.IsNullOrEmpty(command.Value) ? null : command.Value;
                    MarkDirty(session); message = "Updated story stage title."; return true;
                }
                case StoryAuthoringCommandKind.ToggleStageCharacter: Toggle(stage.CharacterIds, command.Value); MarkDirty(session); message = "Updated stage character list."; return true;
                case StoryAuthoringCommandKind.SetUnansweredStage: stage.UnansweredNextStage = NullIfNone(command.Value); MarkDirty(session); message = "Updated unanswered routing."; return true;
                case StoryAuthoringCommandKind.StepUnansweredDelay: stage.UnansweredNextDays = Math.Max(0, stage.UnansweredNextDays + command.Delta); MarkDirty(session); message = "Updated unanswered delay."; return true;
                case StoryAuthoringCommandKind.ToggleUnansweredPunishment: stage.PunishOnUnanswered = !stage.PunishOnUnanswered; MarkDirty(session); message = "Updated unanswered punishment."; return true;
                case StoryAuthoringCommandKind.AddScene: RecordUndo(session, "Add story scene"); stage.IntercomStages.Add(CreateIntercom(stage)); MarkDirty(session); message = "Added intercom step."; return true;
                case StoryAuthoringCommandKind.AddUnansweredStage:
                    return AddRoutedStage(session, flow, stage, null, true, out message);
                case StoryAuthoringCommandKind.AddRoutedStage:
                    if (!ValidIndex(command.SecondaryIndex, stage.IntercomStages.Count, "Story scene", out message)) return false;
                    return AddRoutedStage(session, flow, stage, stage.IntercomStages[command.SecondaryIndex], false, out message);
            }
            if (!ValidIndex(command.SecondaryIndex, stage.IntercomStages.Count, "Story scene", out message)) return false;
            return HandleSceneCommand(session, definition, stage, stage.IntercomStages[command.SecondaryIndex], command, out message);
        }

        private bool HandleSceneCommand(ScenarioEditorSession session, ScenarioDefinition definition, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition scene, StoryAuthoringCommand command, out string message)
        {
            int index = command.SecondaryIndex;
            switch (command.Kind)
            {
                case StoryAuthoringCommandKind.DeleteScene: stage.IntercomStages.RemoveAt(index); MarkDirty(session); message = "Removed intercom step."; return true;
                case StoryAuthoringCommandKind.DuplicateScene:
                {
                    ScenarioIntercomStageDefinition copy = CloneIntercom(scene, NextIntercomId(stage)); stage.IntercomStages.Insert(index + 1, copy); MarkDirty(session); message = "Duplicated intercom step '" + copy.Id + "'."; return true;
                }
                case StoryAuthoringCommandKind.MoveScene: return Move(stage.IntercomStages, index, command.Delta, session, "intercom step", out message);
                case StoryAuthoringCommandKind.SetSceneId:
                {
                    string reason;
                    if (!ValidateIntercomRename(stage, scene.Id, command.Value, out reason)) { message = reason; return false; }
                    string oldId = scene.Id; RecordUndo(session, "Rename intercom step"); scene.Id = command.Value;
                    ScenarioReferenceIndex.RedirectReferences(definition, ScenarioReferenceTargetKind.IntercomStep, oldId, command.Value, command.PrimaryIndex);
                    MarkDirty(session); message = "Renamed intercom step to '" + scene.Id + "'."; return true;
                }
                case StoryAuthoringCommandKind.SetSceneType: scene.Type = command.Value; MarkDirty(session); message = "Updated intercom type."; return true;
                case StoryAuthoringCommandKind.SetSceneNext: return SetIntercomTarget(session, scene, "next", command.Value, out message);
                case StoryAuthoringCommandKind.SetSceneAlternate: return SetIntercomTarget(session, scene, "alternate", command.Value, out message);
                case StoryAuthoringCommandKind.SetStageChangeTarget: EnsureStageChange(scene).Id = NullIfNone(command.Value); MarkDirty(session); message = "Updated stage change target."; return true;
                case StoryAuthoringCommandKind.StepStageChangeDelay: EnsureStageChange(scene).DelayDays = Math.Max(0, EnsureStageChange(scene).DelayDays + command.Delta); MarkDirty(session); message = "Updated stage change delay."; return true;
                case StoryAuthoringCommandKind.ToggleRecruitCharacter: Toggle(scene.CharacterIdsToRecruit, command.Value); MarkDirty(session); message = "Updated recruitment list."; return true;
                case StoryAuthoringCommandKind.ToggleRecruitAsFamily: scene.RecruitAsFamily = !scene.RecruitAsFamily; MarkDirty(session); message = "Updated recruitment mode."; return true;
                case StoryAuthoringCommandKind.SetEndType: EnsureEnd(scene).Type = command.Value; MarkDirty(session); message = "Updated encounter end type."; return true;
                case StoryAuthoringCommandKind.ToggleCompleteQuest: EnsureEnd(scene).CompleteQuest = !EnsureEnd(scene).CompleteQuest; MarkDirty(session); message = "Updated quest completion outcome."; return true;
                case StoryAuthoringCommandKind.ToggleCompleteScenario:
                    if (EnsureEnd(scene).CompleteParentScenario) { EnsureEnd(scene).CompleteParentScenario = false; MarkDirty(session); message = "Removed unsupported parent-scenario completion."; return true; }
                    message = "Parent-scenario completion is disabled here; use Victory conditions."; return false;
                case StoryAuthoringCommandKind.AddOutcomeReward: return AddItem(session, EnsureEnd(scene).RewardItems, "outcome reward", out message);
                case StoryAuthoringCommandKind.AddTradeItem: return AddItem(session, EnsureEnd(scene).TradeItems, "trade", out message);
                case StoryAuthoringCommandKind.ToggleTradeOverride: EnsureEnd(scene).OverrideTradeItems = !EnsureEnd(scene).OverrideTradeItems; MarkDirty(session); message = "Updated trade override."; return true;
                case StoryAuthoringCommandKind.AddDialogue: scene.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = FirstOrNone(stage.CharacterIds), TextKey = "dialogue_" + (scene.Dialogue.Count + 1).ToString() }); MarkDirty(session); message = "Added dialogue line."; return true;
                case StoryAuthoringCommandKind.AddOption: scene.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "option_" + (scene.Options.Count + 1).ToString(), NextId = FirstOtherIntercomId(stage, scene.Id) }); MarkDirty(session); message = "Added response option."; return true;
                case StoryAuthoringCommandKind.AddRandomRoute: scene.RandomizedNextIds.Add(FirstOtherIntercomId(stage, scene.Id)); MarkDirty(session); message = "Added randomized route."; return true;
                case StoryAuthoringCommandKind.AddReward: return AddItem(session, scene.Items, "reward", out message);
                case StoryAuthoringCommandKind.AddRemoval: return AddItem(session, scene.ItemsToRemove, "removal", out message);
                case StoryAuthoringCommandKind.AddMilestone: scene.SetMilestones.Add(new ScenarioMilestoneDefinition { Name = "milestone_" + (scene.SetMilestones.Count + 1).ToString(), Scope = "Scenario", Action = "Set" }); MarkDirty(session); message = "Added milestone."; return true;
            }
            return HandleSceneChildCommand(session, scene, command, out message);
        }

        private bool AddRoutedStage(ScenarioEditorSession session, ScenarioFlowDefinition flow, ScenarioFlowStageDefinition source, ScenarioIntercomStageDefinition sourceScene, bool unanswered, out string message)
        {
            string addMessage;
            if (!AddStage(session, flow, out addMessage) || flow.Stages.Count == 0) { message = addMessage; return false; }
            ScenarioFlowStageDefinition target = flow.Stages[flow.Stages.Count - 1];
            if (unanswered) source.UnansweredNextStage = target.Id;
            else EnsureStageChange(sourceScene).Id = target.Id;
            MarkDirty(session);
            message = unanswered ? "Created the ignored-call stage." : "Created the next routed stage.";
            return true;
        }

        private static bool HandleSceneChildCommand(ScenarioEditorSession session, ScenarioIntercomStageDefinition scene, StoryAuthoringCommand command, out string message)
        {
            message = null;
            int child = command.ChildIndex;
            StoryAuthoringCommandGroup group = StoryAuthoringCommandCatalog.Describe(command.Kind).Group;
            if (group == StoryAuthoringCommandGroup.Dialogue)
            {
                if (!ValidIndex(child, scene.Dialogue.Count, "Dialogue line", out message)) return false;
                ScenarioDialogueLineDefinition line = scene.Dialogue[child];
                if (command.Kind == StoryAuthoringCommandKind.DeleteDialogue) scene.Dialogue.RemoveAt(child);
                else if (command.Kind == StoryAuthoringCommandKind.SetDialogueSpeaker) line.Character = NullIfNone(command.Value);
                else if (command.Kind == StoryAuthoringCommandKind.SetDialogueKey) line.TextKey = command.Value;
                else return false;
            }
            else if (group == StoryAuthoringCommandGroup.Option)
            {
                if (!ValidIndex(child, scene.Options.Count, "Response option", out message)) return false;
                ScenarioDialogueOptionDefinition option = scene.Options[child];
                if (command.Kind == StoryAuthoringCommandKind.DeleteOption) scene.Options.RemoveAt(child);
                else if (command.Kind == StoryAuthoringCommandKind.SetOptionKey) option.TextKey = command.Value;
                else if (command.Kind == StoryAuthoringCommandKind.SetOptionNext) option.NextId = NullIfNone(command.Value);
                else return false;
            }
            else if (group == StoryAuthoringCommandGroup.RandomRoute)
            {
                if (!ValidIndex(child, scene.RandomizedNextIds.Count, "Random route", out message)) return false;
                if (command.Kind == StoryAuthoringCommandKind.DeleteRandomRoute) scene.RandomizedNextIds.RemoveAt(child); else scene.RandomizedNextIds[child] = NullIfNone(command.Value);
            }
            else if (group == StoryAuthoringCommandGroup.Item)
            {
                List<ItemEntry> items = command.Kind == StoryAuthoringCommandKind.DeleteRemoval || command.Kind == StoryAuthoringCommandKind.SetRemovalItem || command.Kind == StoryAuthoringCommandKind.StepRemovalQuantity ? scene.ItemsToRemove : scene.Items;
                if (!ValidIndex(child, items.Count, "Story item", out message)) return false;
                if (command.Kind == StoryAuthoringCommandKind.DeleteReward || command.Kind == StoryAuthoringCommandKind.DeleteRemoval) items.RemoveAt(child);
                else if (command.Kind == StoryAuthoringCommandKind.SetRewardItem || command.Kind == StoryAuthoringCommandKind.SetRemovalItem) return SetItem(session, items[child], command.Value, out message);
                else return StepQuantity(session, items[child], command.Delta, out message);
            }
            else if (group == StoryAuthoringCommandGroup.Milestone)
            {
                if (!ValidIndex(child, scene.SetMilestones.Count, "Milestone", out message)) return false;
                if (command.Kind == StoryAuthoringCommandKind.DeleteMilestone) scene.SetMilestones.RemoveAt(child); else scene.SetMilestones[child].Name = command.Value;
            }
            else if (group == StoryAuthoringCommandGroup.OutcomeItem)
            {
                ScenarioEncounterEndOptionsDefinition end = EnsureEnd(scene);
                List<ItemEntry> items = command.Kind == StoryAuthoringCommandKind.DeleteOutcomeReward
                    || command.Kind == StoryAuthoringCommandKind.SetOutcomeRewardItem
                    || command.Kind == StoryAuthoringCommandKind.StepOutcomeRewardQuantity ? end.RewardItems : end.TradeItems;
                if (!ValidIndex(child, items.Count, "Outcome item", out message)) return false;
                if (command.Kind == StoryAuthoringCommandKind.DeleteOutcomeReward || command.Kind == StoryAuthoringCommandKind.DeleteTradeItem) items.RemoveAt(child);
                else if (command.Kind == StoryAuthoringCommandKind.SetOutcomeRewardItem || command.Kind == StoryAuthoringCommandKind.SetTradeItem) return SetItem(session, items[child], command.Value, out message);
                else return StepQuantity(session, items[child], command.Delta, out message);
            }
            else return false;
            MarkDirty(session); message = "Updated story scene content."; return true;
        }

        private bool HandleConversationCommand(ScenarioEditorSession session, ScenarioDefinition definition, StoryAuthoringCommand command, out string message)
        {
            ScenarioConversationAuthoringDefinition authored = EnsureConversations(definition);
            if (command.Kind == StoryAuthoringCommandKind.AddConversation) { RecordUndo(session, "Add NPC conversation"); authored.Conversations.Add(CreateConversation(definition)); MarkDirty(session); message = "Added NPC conversation."; return true; }
            if (command.Kind == StoryAuthoringCommandKind.ToggleConversationSuppression) { authored.Settings.SuppressVanillaRandomChatter = !authored.Settings.SuppressVanillaRandomChatter; MarkDirty(session); message = "Updated vanilla random chatter suppression."; return true; }
            if (command.Kind == StoryAuthoringCommandKind.SetConversationSuppressionCategory) { Toggle(authored.Settings.SuppressedVanillaCategories, command.Value); MarkDirty(session); message = "Updated vanilla chatter category suppression."; return true; }
            if (command.Kind == StoryAuthoringCommandKind.SetConversationSuppressionTopic) { ReplaceCsv(authored.Settings.SuppressedVanillaTopicKeys, command.Value); MarkDirty(session); message = "Updated stored vanilla topic-key suppression policy."; return true; }
            if (!ValidIndex(command.PrimaryIndex, authored.Conversations.Count, "Conversation", out message)) return false;
            ScenarioConversationDefinition conversation = authored.Conversations[command.PrimaryIndex];
            switch (command.Kind)
            {
                case StoryAuthoringCommandKind.PreviewConversation: return PreviewConversation(definition, conversation, out message);
                case StoryAuthoringCommandKind.DeleteConversation: authored.Conversations.RemoveAt(command.PrimaryIndex); MarkDirty(session); message = "Removed NPC conversation."; return true;
                case StoryAuthoringCommandKind.DuplicateConversation:
                { ScenarioConversationDefinition copy = CloneConversation(conversation, NextConversationId(definition)); authored.Conversations.Insert(command.PrimaryIndex + 1, copy); MarkDirty(session); message = "Duplicated NPC conversation '" + copy.Id + "'."; return true; }
                case StoryAuthoringCommandKind.MoveConversation: return Move(authored.Conversations, command.PrimaryIndex, command.Delta, session, "NPC conversation", out message);
                case StoryAuthoringCommandKind.SetConversationId: conversation.Id = command.Value; MarkDirty(session); message = "Renamed NPC conversation."; return true;
                case StoryAuthoringCommandKind.SetConversationTriggerSource: EnsureTrigger(conversation).Source = ParseTriggerSource(command.Value); break;
                case StoryAuthoringCommandKind.SetConversationTriggerId: EnsureTrigger(conversation).TriggerId = command.Value; break;
                case StoryAuthoringCommandKind.SetConversationTriggerWeight: EnsureTrigger(conversation).Weight = Math.Max(0.1f, EnsureTrigger(conversation).Weight + command.Number); break;
                case StoryAuthoringCommandKind.StepConversationTriggerCooldown: EnsureTrigger(conversation).CooldownDays = Math.Max(0, EnsureTrigger(conversation).CooldownDays + command.Delta); break;
                case StoryAuthoringCommandKind.ToggleConversationTriggerOnce: EnsureTrigger(conversation).Once = !EnsureTrigger(conversation).Once; break;
                case StoryAuthoringCommandKind.StepConversationTriggerDay: EnsureTrigger(conversation).Time.Day = Math.Max(1, EnsureTrigger(conversation).Time.Day + command.Delta); break;
                case StoryAuthoringCommandKind.StepConversationTriggerHour: EnsureTrigger(conversation).Time.Hour = ScenarioAuthoringSchedule.Clamp(EnsureTrigger(conversation).Time.Hour + command.Delta, 0, 23); break;
                case StoryAuthoringCommandKind.StepConversationTriggerMinute: EnsureTrigger(conversation).Time.Minute = ScenarioAuthoringSchedule.Clamp(EnsureTrigger(conversation).Time.Minute + command.Delta, 0, 59); break;
                case StoryAuthoringCommandKind.AddConversationParticipant: conversation.Participants.Add(new ScenarioConversationParticipantDefinition { Slot = NextParticipantSlot(conversation), Fallback = conversation.Participants.Count == 0 ? ScenarioConversationParticipantFallback.Initiator : ScenarioConversationParticipantFallback.Partner, Required = true }); break;
                case StoryAuthoringCommandKind.AddConversationLine: conversation.Lines.Add(new ScenarioConversationLineDefinition { SpeakerSlot = conversation.Participants.Count > 0 ? conversation.Participants[0].Slot : "A", RawText = "New line", DelaySeconds = conversation.Lines.Count == 0 ? 0f : 6f }); break;
                default: return HandleConversationChildCommand(session, definition, conversation, command, out message);
            }
            MarkDirty(session); message = "Updated NPC conversation."; return true;
        }

        private static bool HandleConversationChildCommand(ScenarioEditorSession session, ScenarioDefinition definition, ScenarioConversationDefinition conversation, StoryAuthoringCommand command, out string message)
        {
            int index = command.SecondaryIndex;
            StoryAuthoringCommandGroup group = StoryAuthoringCommandCatalog.Describe(command.Kind).Group;
            if (group == StoryAuthoringCommandGroup.ConversationParticipant)
            {
                if (!ValidIndex(index, conversation.Participants.Count, "Conversation participant", out message)) return false;
                ScenarioConversationParticipantDefinition participant = conversation.Participants[index];
                switch (command.Kind)
                {
                    case StoryAuthoringCommandKind.DeleteConversationParticipant: conversation.Participants.RemoveAt(index); break;
                    case StoryAuthoringCommandKind.SetConversationParticipantSlot: participant.Slot = command.Value; break;
                    case StoryAuthoringCommandKind.SetConversationParticipantStoryCharacter: participant.StoryCharacterId = NullIfNone(command.Value); break;
                    case StoryAuthoringCommandKind.SetConversationParticipantActor:
                    { ScenarioCastMemberReferenceCandidate candidate; if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, true, true, command.Value, out candidate)) { message = "Actor reference is missing."; return false; } participant.ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef); break; }
                    case StoryAuthoringCommandKind.SetConversationParticipantFallback: participant.Fallback = ParseFallback(command.Value); break;
                    case StoryAuthoringCommandKind.ToggleConversationParticipantRequired: participant.Required = !participant.Required; break;
                    default: return false;
                }
            }
            else if (group == StoryAuthoringCommandGroup.ConversationLine)
            {
                if (!ValidIndex(index, conversation.Lines.Count, "Conversation line", out message)) return false;
                ScenarioConversationLineDefinition line = conversation.Lines[index];
                switch (command.Kind)
                {
                    case StoryAuthoringCommandKind.DeleteConversationLine: conversation.Lines.RemoveAt(index); break;
                    case StoryAuthoringCommandKind.SetConversationLineSpeaker: line.SpeakerSlot = command.Value; break;
                    case StoryAuthoringCommandKind.SetConversationLineText: line.RawText = command.Value; break;
                    case StoryAuthoringCommandKind.SetConversationLineDelay: line.DelaySeconds = Math.Max(0f, line.DelaySeconds + command.Number); break;
                    default: return false;
                }
            }
            else
            {
                message = "Story conversation command is not classified: " + command.Kind + ".";
                return false;
            }
            MarkDirty(session); message = "Updated NPC conversation."; return true;
        }

        private static bool ValidIndex(int index, int count, string label, out string message) { if (index >= 0 && index < count) { message = null; return true; } message = label + " no longer exists."; return false; }
    }
}
