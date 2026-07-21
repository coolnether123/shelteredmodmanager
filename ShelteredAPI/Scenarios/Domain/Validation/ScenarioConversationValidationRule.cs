using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.Validation
{
    internal sealed class ScenarioConversationValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            ScenarioConversationAuthoringDefinition authoring = definition != null ? definition.Conversations : null;
            if (authoring == null || authoring.Conversations == null)
                return;

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> storyCharacters = BuildStoryCharacterIds(definition);
            for (int i = 0; i < authoring.Conversations.Count; i++)
                ValidateConversation(definition, summary, ids, storyCharacters, authoring.Conversations[i], i);

            if (authoring.Settings != null
                && authoring.Settings.SuppressedVanillaTopicKeys != null
                && authoring.Settings.SuppressedVanillaTopicKeys.Count > 0)
            {
                summary.AddWarning(
                    "conversation.suppression.topic_keys",
                    "[Conversations] Specific vanilla topic-key suppression is saved for authoring, but runtime key-level blocking is not active without a transpiler.");
            }
        }

        private static void ValidateConversation(
            ScenarioDefinition definition,
            ValidationSummary summary,
            HashSet<string> ids,
            HashSet<string> storyCharacters,
            ScenarioConversationDefinition conversation,
            int index)
        {
            string label = "[Conversations] Conversation #" + (index + 1).ToString(CultureInfo.InvariantCulture);
            if (conversation == null)
            {
                summary.AddError("conversation.empty", label + " is empty.");
                return;
            }

            if (string.IsNullOrEmpty(conversation.Id))
                summary.AddError("conversation.id.missing", label + " is missing an id.");
            else if (ids.Contains(conversation.Id))
                summary.AddError("conversation.id.duplicate", "[Conversations] Duplicate conversation id '" + conversation.Id + "'.");
            else
                ids.Add(conversation.Id);

            ValidateTrigger(definition, summary, conversation, label);
            HashSet<string> slots = ValidateParticipants(definition, summary, storyCharacters, conversation, label);
            ValidateLines(summary, conversation, label, slots);
        }

        private static void ValidateTrigger(ScenarioDefinition definition, ValidationSummary summary, ScenarioConversationDefinition conversation, string label)
        {
            ScenarioConversationTriggerDefinition trigger = conversation.Trigger;
            if (trigger == null)
            {
                summary.AddError("conversation.trigger.missing", label + " is missing a trigger.");
                return;
            }

            if (trigger.Source == ScenarioConversationTriggerSource.Random && trigger.Weight <= 0f)
                summary.AddError("conversation.trigger.weight", label + " random trigger weight must be greater than zero.");

            if (trigger.Source == ScenarioConversationTriggerSource.Random
                && trigger.Time != null
                && (trigger.Time.Day > 1 || trigger.Time.Hour > 0 || trigger.Time.Minute > 0))
                summary.AddError("conversation.trigger.random_scheduled", label + " has a timeline date but uses Random. Random conversations only run from idle chatter; choose Timeline to run on its authored date.");

            if (trigger.Source == ScenarioConversationTriggerSource.Event)
            {
                if (string.IsNullOrEmpty(trigger.TriggerId))
                    summary.AddError("conversation.trigger.event_missing", label + " event trigger source is missing TriggerId.");
                else if (!ScenarioDefinitionLookup.HasTrigger(definition, trigger.TriggerId))
                    summary.AddError("conversation.trigger.event_dangling", label + " references missing trigger '" + trigger.TriggerId + "'.");
            }
        }

        private static HashSet<string> ValidateParticipants(
            ScenarioDefinition definition,
            ValidationSummary summary,
            HashSet<string> storyCharacters,
            ScenarioConversationDefinition conversation,
            string label)
        {
            HashSet<string> slots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (conversation.Participants == null || conversation.Participants.Count == 0)
            {
                summary.AddError("conversation.participants.empty", label + " has no participants.");
                return slots;
            }

            for (int i = 0; i < conversation.Participants.Count; i++)
            {
                ScenarioConversationParticipantDefinition participant = conversation.Participants[i];
                string participantLabel = label + " participant #" + (i + 1).ToString(CultureInfo.InvariantCulture);
                if (participant == null)
                {
                    summary.AddError("conversation.participant.empty", participantLabel + " is empty.");
                    continue;
                }

                if (string.IsNullOrEmpty(participant.Slot))
                    summary.AddError("conversation.participant.slot_missing", participantLabel + " is missing a slot.");
                else if (slots.Contains(participant.Slot))
                    summary.AddError("conversation.participant.slot_duplicate", label + " has duplicate participant slot '" + participant.Slot + "'.");
                else
                    slots.Add(participant.Slot);

                if (!string.IsNullOrEmpty(participant.StoryCharacterId) && !storyCharacters.Contains(participant.StoryCharacterId))
                    summary.AddError("conversation.participant.story_missing", participantLabel + " references missing story character '" + participant.StoryCharacterId + "'.");

                if (conversation.Trigger != null
                    && conversation.Trigger.Source == ScenarioConversationTriggerSource.Timeline
                    && participant.Required
                    && !HasActorBackedBinding(definition, participant))
                    summary.AddError("conversation.participant.timeline_unbound", participantLabel + " is required by a Timeline conversation but has no actor-backed cast binding. Select a starting cast member; Initiator and Partner are available only to Random conversations.");
            }

            return slots;
        }

        private static bool HasActorBackedBinding(ScenarioDefinition definition, ScenarioConversationParticipantDefinition participant)
        {
            if (participant != null && participant.ActorRef != null)
                return true;

            for (int i = 0; participant != null && definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character != null
                    && string.Equals(character.CharacterId, participant.StoryCharacterId, StringComparison.OrdinalIgnoreCase)
                    && character.ActorRef != null)
                    return true;
            }
            return false;
        }

        private static void ValidateLines(ValidationSummary summary, ScenarioConversationDefinition conversation, string label, HashSet<string> slots)
        {
            if (conversation.Lines == null || conversation.Lines.Count == 0)
            {
                summary.AddError("conversation.lines.empty", label + " has no lines.");
                return;
            }

            for (int i = 0; i < conversation.Lines.Count; i++)
            {
                ScenarioConversationLineDefinition line = conversation.Lines[i];
                string lineLabel = label + " line #" + (i + 1).ToString(CultureInfo.InvariantCulture);
                if (line == null)
                {
                    summary.AddError("conversation.line.empty", lineLabel + " is empty.");
                    continue;
                }

                if (string.IsNullOrEmpty(line.SpeakerSlot) || !slots.Contains(line.SpeakerSlot))
                    summary.AddError("conversation.line.speaker_dangling", lineLabel + " references missing speaker slot '" + (line.SpeakerSlot ?? string.Empty) + "'.");
                if (string.IsNullOrEmpty(line.RawText) && string.IsNullOrEmpty(line.TextKey))
                    summary.AddError("conversation.line.text_empty", lineLabel + " has no raw text or text key.");
            }
        }

        private static HashSet<string> BuildStoryCharacterIds(ScenarioDefinition definition)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character != null && !string.IsNullOrEmpty(character.CharacterId))
                    ids.Add(character.CharacterId);
            }
            return ids;
        }
    }
}
