using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Composition;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredScenarioEditor.Domain.Validation;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Infrastructure.Unity;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed partial class ScenarioStoryAuthoringService
    {
        private readonly ScenarioPreviewSessionHost _previewSession;
        private readonly ScenarioAuthoringHistoryService _historyService;

        internal ScenarioStoryAuthoringService(ScenarioPreviewSessionHost previewSession, ScenarioAuthoringHistoryService historyService)
        {
            _previewSession = previewSession;
            _historyService = historyService;
        }

        private bool AddCharacter(ScenarioEditorSession session, ScenarioDefinition definition, out string message)
        {
            List<ScenarioNpcDefinition> characters = EnsureCharacters(definition);
            int index = characters.Count + 1;
            string characterId;
            do
            {
                characterId = "StoryCharacter" + index.ToString();
                index++;
            }
            while (HasCharacterId(definition, characterId));

            RecordUndo(session, "Add story character");
            ScenarioNpcDefinition character = new ScenarioNpcDefinition();
            character.CharacterId = characterId;
            character.DisplayName = "Story Character " + (characters.Count + 1).ToString();
            character.PresetId = "Default";
            characters.Add(character);
            MarkDirty(session);
            message = "Added story character '" + character.DisplayName + "'.";
            return true;
        }

        private bool DeleteCharacter(ScenarioEditorSession session, ScenarioDefinition definition, int characterIndex, out string message)
        {
            message = null;
            List<ScenarioNpcDefinition> characters = EnsureCharacters(definition);
            if (characterIndex < 0 || characterIndex >= characters.Count)
            {
                message = "Story character no longer exists.";
                return true;
            }

            ScenarioNpcDefinition character = characters[characterIndex];
            string characterId = character != null ? character.CharacterId : null;
            string reason;
            if (!CanRemoveCharacter(definition, characterId, out reason))
            {
                message = reason;
                return true;
            }

            RecordUndo(session, "Remove story character");
            characters.RemoveAt(characterIndex);
            MarkDirty(session);
            message = "Removed story character '" + DisplayCharacterName(character) + "'.";
            return true;
        }

        private bool AddStage(ScenarioEditorSession session, ScenarioFlowDefinition flow, out string message)
        {
            RecordUndo(session, "Add story stage");
            ScenarioFlowStageDefinition stage = new ScenarioFlowStageDefinition();
            stage.Id = NextStageId(flow);
            stage.IntercomStages.Add(CreateIntercom(stage));
            flow.Stages.Add(stage);
            MarkDirty(session);
            message = "Added story stage '" + stage.Id + "'.";
            return true;
        }

        private static ScenarioIntercomStageDefinition CreateIntercom(ScenarioFlowStageDefinition stage)
        {
            ScenarioIntercomStageDefinition intercom = new ScenarioIntercomStageDefinition();
            intercom.Id = NextIntercomId(stage);
            intercom.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = FirstOrNone(stage != null ? stage.CharacterIds : null), TextKey = "dialogue_" + intercom.Id });
            intercom.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "option_continue" });
            return intercom;
        }

        private static bool Move<T>(List<T> list, int index, int delta, ScenarioEditorSession session, string label, out string message)
        {
            int target = index + delta;
            if (list == null || target < 0 || target >= list.Count)
            {
                message = label + " is already at the edge.";
                return true;
            }
            T item = list[index];
            list.RemoveAt(index);
            list.Insert(target, item);
            MarkDirty(session);
            message = "Moved " + label + ".";
            return true;
        }

        private static bool SetIntercomTarget(ScenarioEditorSession session, ScenarioIntercomStageDefinition intercom, string slot, string target, out string message)
        {
            if (slot == "alternate")
                intercom.AlternateNextId = NullIfNone(target);
            else
                intercom.NextId = NullIfNone(target);
            MarkDirty(session);
            message = "Updated " + slot + " intercom route.";
            return true;
        }

        private static bool AddItem(ScenarioEditorSession session, List<ItemEntry> list, string label, out string message)
        {
            list.Add(new ItemEntry { ItemId = ScenarioInventoryItemCatalog.DefaultItemId(), Quantity = 1 });
            MarkDirty(session);
            message = "Added " + label + " item.";
            return true;
        }

        private bool PreviewConversation(ScenarioDefinition definition, ScenarioConversationDefinition conversation, out string message)
        {
            if (_previewSession == null || !_previewSession.IsActive)
            {
                message = "Conversation preview is unavailable because the scenario runtime is not initialized.";
                return false;
            }
            return _previewSession.TryFireRuntimeElement(conversation != null ? conversation.Id : null, out message);
        }

        private static bool SetItem(ScenarioEditorSession session, ItemEntry item, string itemId, out string message)
        {
            item.ItemId = itemId;
            MarkDirty(session);
            message = "Updated item.";
            return true;
        }

        private static bool StepQuantity(ScenarioEditorSession session, ItemEntry item, int delta, out string message)
        {
            item.Quantity = Math.Max(1, item.Quantity + delta);
            MarkDirty(session);
            message = "Updated quantity to " + item.Quantity + ".";
            return true;
        }

        private static ScenarioFlowDefinition EnsureFlow(ScenarioDefinition definition)
        {
            if (definition.ScenarioFlow == null)
                definition.ScenarioFlow = new ScenarioFlowDefinition();
            return definition.ScenarioFlow;
        }

        private static ScenarioStageChangeDefinition EnsureStageChange(ScenarioIntercomStageDefinition intercom)
        {
            if (intercom.StageChange == null)
                intercom.StageChange = new ScenarioStageChangeDefinition();
            return intercom.StageChange;
        }

        private static ScenarioEncounterEndOptionsDefinition EnsureEnd(ScenarioIntercomStageDefinition intercom)
        {
            if (intercom.EndOptions == null)
                intercom.EndOptions = new ScenarioEncounterEndOptionsDefinition();
            return intercom.EndOptions;
        }

        private static ScenarioFlowStageDefinition CloneStage(ScenarioFlowStageDefinition source, string id)
        {
            ScenarioFlowStageDefinition copy = new ScenarioFlowStageDefinition();
            copy.Id = id;
            copy.UnansweredNextStage = source != null ? source.UnansweredNextStage : null;
            copy.UnansweredNextDays = source != null ? source.UnansweredNextDays : 1;
            copy.PunishOnUnanswered = source != null && source.PunishOnUnanswered;
            for (int i = 0; source != null && source.CharacterIds != null && i < source.CharacterIds.Count; i++)
                copy.CharacterIds.Add(source.CharacterIds[i]);
            for (int i = 0; source != null && source.IntercomStages != null && i < source.IntercomStages.Count; i++)
                copy.IntercomStages.Add(CloneIntercom(source.IntercomStages[i], source.IntercomStages[i] != null ? source.IntercomStages[i].Id : NextIntercomId(copy)));
            return copy;
        }

        private static ScenarioIntercomStageDefinition CloneIntercom(ScenarioIntercomStageDefinition source, string id)
        {
            ScenarioIntercomStageDefinition copy = new ScenarioIntercomStageDefinition();
            copy.Id = id;
            copy.Type = source != null ? source.Type : "Standard";
            copy.NextId = source != null ? source.NextId : null;
            copy.AlternateNextId = source != null ? source.AlternateNextId : null;
            copy.StageDescriptionKey = source != null ? source.StageDescriptionKey : null;
            copy.RecruitAsFamily = source != null && source.RecruitAsFamily;
            for (int i = 0; source != null && source.Dialogue != null && i < source.Dialogue.Count; i++)
                copy.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = source.Dialogue[i].Character, TextKey = source.Dialogue[i].TextKey });
            for (int i = 0; source != null && source.Options != null && i < source.Options.Count; i++)
                copy.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = source.Options[i].TextKey, NextId = source.Options[i].NextId });
            for (int i = 0; source != null && source.RandomizedNextIds != null && i < source.RandomizedNextIds.Count; i++)
                copy.RandomizedNextIds.Add(source.RandomizedNextIds[i]);
            CopyItems(source != null ? source.Items : null, copy.Items);
            CopyItems(source != null ? source.ItemsToRemove : null, copy.ItemsToRemove);
            for (int i = 0; source != null && source.SetMilestones != null && i < source.SetMilestones.Count; i++)
                copy.SetMilestones.Add(new ScenarioMilestoneDefinition { Name = source.SetMilestones[i].Name, Scope = source.SetMilestones[i].Scope, Action = source.SetMilestones[i].Action });
            if (source != null && source.StageChange != null)
                copy.StageChange = new ScenarioStageChangeDefinition { Id = source.StageChange.Id, DelayDays = source.StageChange.DelayDays };
            if (source != null && source.EndOptions != null)
                copy.EndOptions = new ScenarioEncounterEndOptionsDefinition { Type = source.EndOptions.Type, CompleteQuest = source.EndOptions.CompleteQuest, CompleteParentScenario = source.EndOptions.CompleteParentScenario };
            for (int i = 0; source != null && source.CharacterIdsToRecruit != null && i < source.CharacterIdsToRecruit.Count; i++)
                copy.CharacterIdsToRecruit.Add(source.CharacterIdsToRecruit[i]);
            return copy;
        }

        private static void CopyItems(List<ItemEntry> source, List<ItemEntry> target)
        {
            for (int i = 0; source != null && i < source.Count; i++)
                target.Add(new ItemEntry { ItemId = source[i].ItemId, Quantity = source[i].Quantity });
        }

        private bool RenameStage(ScenarioEditorSession session, ScenarioDefinition definition, ScenarioFlowDefinition flow, int stageIndex, string newId, out string message)
        {
            ScenarioFlowStageDefinition stage = flow.Stages[stageIndex];
            string oldId = stage != null ? stage.Id : null;
            string reason;
            if (!ValidateStageRename(flow, stageIndex, newId, out reason))
            {
                message = reason;
                return true;
            }

            RecordUndo(session, "Rename story stage");
            stage.Id = newId;
            int updated = ScenarioReferenceIndex.RedirectReferences(definition, ScenarioReferenceTargetKind.Stage, oldId, newId, -1);
            MarkDirty(session);
            message = updated > 0
                ? "Renamed story stage to '" + newId + "' and updated " + updated.ToString(CultureInfo.InvariantCulture) + " reference(s)."
                : "Renamed story stage to '" + newId + "'.";
            return true;
        }

        private static bool ValidateStageRename(ScenarioFlowDefinition flow, int stageIndex, string newId, out string reason)
        {
            reason = null;
            string trimmed = newId != null ? newId.Trim() : string.Empty;
            if (trimmed.Length == 0)
            {
                reason = "Story stage id cannot be empty.";
                return false;
            }
            if (!IsValidId(trimmed))
            {
                reason = "Story stage id '" + trimmed + "' contains unsupported characters. Use letters, numbers, '_' or '-'.";
                return false;
            }
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                if (i == stageIndex || flow.Stages[i] == null)
                    continue;
                if (string.Equals(flow.Stages[i].Id, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Story stage id '" + trimmed + "' is already used by another stage.";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateIntercomRename(ScenarioFlowStageDefinition stage, string oldId, string newId, out string reason)
        {
            reason = null;
            string trimmed = newId != null ? newId.Trim() : string.Empty;
            if (trimmed.Length == 0)
            {
                reason = "Encounter step id cannot be empty.";
                return false;
            }
            if (!IsValidId(trimmed))
            {
                reason = "Encounter step id '" + trimmed + "' contains unsupported characters. Use letters, numbers, '_' or '-'.";
                return false;
            }
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step == null || string.Equals(step.Id, oldId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(step.Id, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "Encounter step id '" + trimmed + "' is already used in this stage.";
                    return false;
                }
            }
            return true;
        }

        private static bool IsValidId(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    return false;
            }
            return true;
        }

        private static bool CanRemoveStage(ScenarioDefinition definition, string stageId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(stageId))
                return true;

            List<ScenarioReferenceUsage> usages = ScenarioReferenceIndex.FindUsages(definition, ScenarioReferenceTargetKind.Stage, stageId);
            if (usages.Count == 0)
                return true;

            reason = "Cannot remove story stage '" + stageId + "' because it is referenced by: "
                + DescribeUsages(usages) + ". Clear those references first.";
            return false;
        }

        private static bool CanRemoveCharacter(ScenarioDefinition definition, string characterId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(characterId))
                return true;

            List<ScenarioReferenceUsage> usages = ScenarioReferenceIndex.FindUsages(definition, ScenarioReferenceTargetKind.StoryCharacter, characterId);
            if (usages.Count == 0)
                return true;

            reason = "Cannot remove story character '" + characterId + "' because it is referenced by: "
                + DescribeUsages(usages)
                + ". Open those rows, clear the stage cast, dialogue speaker, recruit toggle, or conversation participant, then remove the character.";
            return false;
        }

        private static string DescribeUsages(List<ScenarioReferenceUsage> usages)
        {
            List<string> parts = new List<string>();
            for (int i = 0; usages != null && i < usages.Count; i++)
            {
                ScenarioReferenceUsage usage = usages[i];
                parts.Add(usage.OwnerLabel + " " + usage.DisplayLabel);
            }
            return string.Join(", ", parts.ToArray());
        }

        private void RecordUndo(ScenarioEditorSession session, string description)
        {
            if (session == null || session.WorkingDefinition == null)
                return;
            if (_historyService != null)
                _historyService.RecordAuthoringChange(session.WorkingDefinition, description, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
        }

        private static string NextStageId(ScenarioFlowDefinition flow)
        {
            int index = flow != null && flow.Stages != null ? flow.Stages.Count + 1 : 1;
            string id;
            do
            {
                id = "stage_" + index.ToString();
                index++;
            }
            while (HasStage(flow, id));
            return id;
        }

        private static string NextIntercomId(ScenarioFlowStageDefinition stage)
        {
            int index = stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count + 1 : 1;
            string id;
            do
            {
                id = "step_" + index.ToString();
                index++;
            }
            while (HasIntercom(stage, id));
            return id;
        }

        private static bool HasStage(ScenarioFlowDefinition flow, string id)
        {
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
                if (flow.Stages[i] != null && string.Equals(flow.Stages[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool HasIntercom(ScenarioFlowStageDefinition stage, string id)
        {
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (stage.IntercomStages[i] != null && string.Equals(stage.IntercomStages[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool HasCharacterId(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
                if (definition.ScenarioCharacters[i] != null && string.Equals(definition.ScenarioCharacters[i].CharacterId, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static List<ScenarioNpcDefinition> EnsureCharacters(ScenarioDefinition definition)
        {
            return definition.ScenarioCharacters;
        }

        private static string DisplayCharacterName(ScenarioNpcDefinition character)
        {
            if (character == null)
                return "<missing>";
            if (!string.IsNullOrEmpty(character.DisplayName))
                return character.DisplayName;
            if (!string.IsNullOrEmpty(character.CharacterId))
                return character.CharacterId;
            return "<unnamed>";
        }

        private static string FirstOtherIntercomId(ScenarioFlowStageDefinition stage, string current)
        {
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (stage.IntercomStages[i] != null && !string.Equals(stage.IntercomStages[i].Id, current, StringComparison.OrdinalIgnoreCase))
                    return stage.IntercomStages[i].Id;
            return null;
        }

        private static string FirstOrNone(List<string> values)
        {
            return values != null && values.Count > 0 ? values[0] : null;
        }

        private static void Toggle(List<string> values, string value)
        {
            if (values == null || string.IsNullOrEmpty(value))
                return;
            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    values.RemoveAt(i);
                    return;
                }
            }
            values.Add(value);
        }

        private static ScenarioConversationAuthoringDefinition EnsureConversations(ScenarioDefinition definition)
        {
            if (definition.Conversations == null)
                definition.Conversations = new ScenarioConversationAuthoringDefinition();
            if (definition.Conversations.Settings == null)
                definition.Conversations.Settings = new ScenarioConversationSuppressionDefinition();
            return definition.Conversations;
        }

        private static ScenarioConversationDefinition CreateConversation(ScenarioDefinition definition)
        {
            ScenarioConversationDefinition conversation = new ScenarioConversationDefinition();
            conversation.Id = NextConversationId(definition);
            conversation.Trigger = new ScenarioConversationTriggerDefinition
            {
                Source = ScenarioConversationTriggerSource.Random,
                Weight = 1f,
                CooldownDays = 1,
                Once = false,
                Time = ScenarioAuthoringSchedule.NextTime()
            };
            conversation.Participants.Add(new ScenarioConversationParticipantDefinition
            {
                Slot = "A",
                Fallback = ScenarioConversationParticipantFallback.Initiator,
                Required = true
            });
            conversation.Participants.Add(new ScenarioConversationParticipantDefinition
            {
                Slot = "B",
                Fallback = ScenarioConversationParticipantFallback.Partner,
                Required = true
            });
            conversation.Lines.Add(new ScenarioConversationLineDefinition { SpeakerSlot = "A", RawText = "Did you hear that?", DelaySeconds = 0f });
            conversation.Lines.Add(new ScenarioConversationLineDefinition { SpeakerSlot = "B", RawText = "Keep your voice down.", DelaySeconds = 6f });
            return conversation;
        }

        private static ScenarioConversationDefinition CloneConversation(ScenarioConversationDefinition source, string id)
        {
            ScenarioConversationDefinition copy = new ScenarioConversationDefinition();
            copy.Id = id;
            copy.Trigger = CloneTrigger(source != null ? source.Trigger : null);
            for (int i = 0; source != null && source.Participants != null && i < source.Participants.Count; i++)
                copy.Participants.Add(CloneParticipant(source.Participants[i]));
            for (int i = 0; source != null && source.Conditions != null && i < source.Conditions.Count; i++)
            {
                ScenarioConditionRef condition = source.Conditions[i];
                if (condition != null)
                    copy.Conditions.Add(new ScenarioConditionRef { Id = condition.Id, Kind = condition.Kind, TargetId = condition.TargetId, Quantity = condition.Quantity, FlagValue = condition.FlagValue });
            }
            for (int i = 0; source != null && source.Lines != null && i < source.Lines.Count; i++)
                copy.Lines.Add(CloneLine(source.Lines[i]));
            for (int i = 0; source != null && source.Tags != null && i < source.Tags.Count; i++)
                copy.Tags.Add(source.Tags[i]);
            return copy;
        }

        private static ScenarioConversationTriggerDefinition CloneTrigger(ScenarioConversationTriggerDefinition source)
        {
            ScenarioConversationTriggerDefinition copy = new ScenarioConversationTriggerDefinition();
            if (source == null)
                return copy;
            copy.Source = source.Source;
            copy.TriggerId = source.TriggerId;
            copy.Weight = source.Weight;
            copy.CooldownDays = source.CooldownDays;
            copy.Once = source.Once;
            copy.Time = source.Time != null
                ? new ScenarioScheduleTime { Day = source.Time.Day, Hour = source.Time.Hour, Minute = source.Time.Minute }
                : new ScenarioScheduleTime();
            return copy;
        }

        private static ScenarioConversationParticipantDefinition CloneParticipant(ScenarioConversationParticipantDefinition source)
        {
            ScenarioConversationParticipantDefinition copy = new ScenarioConversationParticipantDefinition();
            if (source == null)
                return copy;
            copy.Slot = source.Slot;
            copy.StoryCharacterId = source.StoryCharacterId;
            copy.ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(source.ActorRef);
            copy.Fallback = source.Fallback;
            copy.Required = source.Required;
            return copy;
        }

        private static ScenarioConversationLineDefinition CloneLine(ScenarioConversationLineDefinition source)
        {
            ScenarioConversationLineDefinition copy = new ScenarioConversationLineDefinition();
            if (source == null)
                return copy;
            copy.SpeakerSlot = source.SpeakerSlot;
            copy.TextKey = source.TextKey;
            copy.RawText = source.RawText;
            copy.DelaySeconds = source.DelaySeconds;
            return copy;
        }

        private static ScenarioConversationTriggerDefinition EnsureTrigger(ScenarioConversationDefinition conversation)
        {
            if (conversation.Trigger == null)
                conversation.Trigger = new ScenarioConversationTriggerDefinition();
            if (conversation.Trigger.Time == null)
                conversation.Trigger.Time = new ScenarioScheduleTime();
            return conversation.Trigger;
        }

        private static string NextConversationId(ScenarioDefinition definition)
        {
            int index = definition != null && definition.Conversations != null && definition.Conversations.Conversations != null
                ? definition.Conversations.Conversations.Count + 1
                : 1;
            string id;
            do
            {
                id = "conversation_" + index.ToString(CultureInfo.InvariantCulture);
                index++;
            }
            while (HasConversation(definition, id));
            return id;
        }

        private static bool HasConversation(ScenarioDefinition definition, string id)
        {
            List<ScenarioConversationDefinition> conversations = definition != null && definition.Conversations != null ? definition.Conversations.Conversations : null;
            for (int i = 0; conversations != null && i < conversations.Count; i++)
                if (conversations[i] != null && string.Equals(conversations[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string NextParticipantSlot(ScenarioConversationDefinition conversation)
        {
            string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            for (int i = 0; i < alphabet.Length; i++)
            {
                string slot = alphabet.Substring(i, 1);
                bool used = false;
                for (int p = 0; conversation != null && conversation.Participants != null && p < conversation.Participants.Count; p++)
                    if (conversation.Participants[p] != null && string.Equals(conversation.Participants[p].Slot, slot, StringComparison.OrdinalIgnoreCase))
                        used = true;
                if (!used)
                    return slot;
            }
            return "P" + (conversation != null && conversation.Participants != null ? (conversation.Participants.Count + 1).ToString(CultureInfo.InvariantCulture) : "1");
        }

        private static ScenarioConversationTriggerSource ParseTriggerSource(string value)
        {
            try
            {
                return (ScenarioConversationTriggerSource)Enum.Parse(typeof(ScenarioConversationTriggerSource), value, true);
            }
            catch
            {
                return ScenarioConversationTriggerSource.Random;
            }
        }

        private static ScenarioConversationParticipantFallback ParseFallback(string value)
        {
            try
            {
                return (ScenarioConversationParticipantFallback)Enum.Parse(typeof(ScenarioConversationParticipantFallback), value, true);
            }
            catch
            {
                return ScenarioConversationParticipantFallback.None;
            }
        }

        private static void ReplaceCsv(List<string> values, string csv)
        {
            if (values == null)
                return;
            values.Clear();
            string[] parts = (csv ?? string.Empty).Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string value = parts[i] != null ? parts[i].Trim() : null;
                if (!string.IsNullOrEmpty(value))
                    values.Add(value);
            }
        }

        private static string NullIfNone(string value)
        {
            return string.IsNullOrEmpty(value) || string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? null : value;
        }

        private static void MarkDirty(ScenarioEditorSession session)
        {
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
        }
    }
}
