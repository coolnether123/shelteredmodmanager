using System;
using System.Collections;
using System.Collections.Generic;

using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    internal sealed class ScenarioConversationRuntimeService : IScenarioEffectHandler
    {
        private const string RuntimeObjectName = "ShelteredAPI.ScenarioConversationRuntime";
        private readonly ScenarioActorResolver _actorResolver;
        private readonly ScenarioRuntimeStateService _stateService;
        private readonly ScenarioConditionEvaluatorRegistry _conditions;
        private ScenarioDefinition _activeDefinition;

        public ScenarioConversationRuntimeService(
            ScenarioActorResolver actorResolver,
            ScenarioRuntimeStateService stateService,
            ScenarioConditionEvaluatorRegistry conditions)
        {
            _actorResolver = actorResolver;
            _stateService = stateService;
            _conditions = conditions;
        }

        public void Activate(ScenarioDefinition definition)
        {
            _activeDefinition = definition;
        }

        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.StartConversation;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            string id = effect != null ? (effect.ConversationId ?? effect.TargetId) : null;
            ScenarioConversationDefinition conversation = FindConversation(definition, id);
            if (conversation == null)
            {
                message = "Conversation not found: " + (id ?? string.Empty) + ".";
                return false;
            }

            return TryStartConversation(definition, conversation, null, null, state, "effect", out message);
        }

        public bool TryHandleRandomComment(FamilyMember initiator, out string message)
        {
            message = null;
            ScenarioDefinition definition = _activeDefinition;
            ScenarioConversationAuthoringDefinition authoring = definition != null ? definition.Conversations : null;
            ScenarioConversationSuppressionDefinition settings = authoring != null ? authoring.Settings : null;
            if (settings != null && settings.SuppressVanillaRandomChatter)
            {
                message = "Vanilla random chatter suppressed by scenario conversation settings.";
                return true;
            }

            FamilyMember partner = FindNearestIdlePartner(initiator);
            if (partner == null)
                return false;

            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            ScenarioConversationDefinition conversation = SelectRandomConversation(definition, initiator, partner, state);
            if (conversation == null)
                return false;

            if (TryStartConversation(definition, conversation, initiator, partner, state, "random", out message))
                return true;

            return false;
        }

        public bool ShouldSuppressGenericBantz()
        {
            return HasSuppressedCategory("GenericBantz");
        }

        public bool ShouldSuppressIllness(FamilyMember target)
        {
            if (target == null || target.illness == null)
                return false;

            try
            {
                if (target.illness.bleeding != null && target.illness.bleeding.isActive && HasSuppressedCategory("Illness.Bleeding"))
                    return true;
                if (target.illness.foodPoisoning != null && target.illness.foodPoisoning.isActive && HasSuppressedCategory("Illness.FoodPoisoning"))
                    return true;
                if (target.illness.infection != null && target.illness.infection.isActive && HasSuppressedCategory("Illness.Infection"))
                    return true;
                if (target.illness.malnourishment != null && target.illness.malnourishment.isActive && HasSuppressedCategory("Illness.Malnourishment"))
                    return true;
                if (target.illness.radiation != null && target.illness.radiation.isActive && (HasSuppressedCategory("Illness.Radiation") || HasSuppressedCategory("Illness.RadiationPoisoning")))
                    return true;
                if (target.illness.suffocation != null && target.illness.suffocation.isActive && HasSuppressedCategory("Illness.Suffocation"))
                    return true;
            }
            catch
            {
            }

            return HasSuppressedCategory("Illness");
        }

        public string GetSuppressionNote()
        {
            ScenarioConversationSuppressionDefinition settings = _activeDefinition != null && _activeDefinition.Conversations != null ? _activeDefinition.Conversations.Settings : null;
            if (settings == null || settings.SuppressedVanillaTopicKeys == null || settings.SuppressedVanillaTopicKeys.Count == 0)
                return null;
            return "Specific vanilla idle-speech key suppression is stored in XML but not enforced without a transpiler.";
        }

        private bool TryStartConversation(
            ScenarioDefinition definition,
            ScenarioConversationDefinition conversation,
            FamilyMember initiator,
            FamilyMember partner,
            ScenarioRuntimeState state,
            string source,
            out string message)
        {
            message = null;
            if (conversation == null)
            {
                message = "Conversation is missing.";
                return false;
            }

            string reason;
            if (_conditions != null && !_conditions.AreConditionsSatisfied(definition, conversation.Conditions, state, out reason))
            {
                message = reason;
                return false;
            }

            Dictionary<string, FamilyMember> participants;
            if (!TryResolveParticipants(definition, conversation, initiator, partner, out participants, out message))
                return false;
            if (!CanRun(conversation, state, out message))
                return false;

            ScenarioConversationRunner runner = EnsureRunner();
            runner.Play(conversation, participants);
            RecordPlayed(conversation, state);
            message = "Started conversation '" + (conversation.Id ?? string.Empty) + "' from " + (source ?? "runtime") + ".";
            return true;
        }

        private bool TryResolveParticipants(
            ScenarioDefinition definition,
            ScenarioConversationDefinition conversation,
            FamilyMember initiator,
            FamilyMember partner,
            out Dictionary<string, FamilyMember> participants,
            out string message)
        {
            participants = new Dictionary<string, FamilyMember>(StringComparer.OrdinalIgnoreCase);
            message = null;

            for (int i = 0; conversation.Participants != null && i < conversation.Participants.Count; i++)
            {
                ScenarioConversationParticipantDefinition participant = conversation.Participants[i];
                if (participant == null || string.IsNullOrEmpty(participant.Slot))
                    continue;

                FamilyMember member = ResolveParticipant(definition, participant, initiator, partner, participants);
                if (member == null && participant.Required)
                {
                    message = "Conversation '" + (conversation.Id ?? string.Empty) + "' could not resolve participant slot '" + participant.Slot + "'.";
                    return false;
                }
                if (member != null)
                    participants[participant.Slot] = member;
            }

            return participants.Count > 0;
        }

        private FamilyMember ResolveParticipant(
            ScenarioDefinition definition,
            ScenarioConversationParticipantDefinition participant,
            FamilyMember initiator,
            FamilyMember partner,
            Dictionary<string, FamilyMember> alreadySelected)
        {
            FamilyMember member;
            if (participant.ActorRef != null && _actorResolver != null && _actorResolver.TryResolveFamilyMember(definition, participant.ActorRef, out member))
                return member;

            ScenarioActorRef storyRef = ResolveStoryCharacterRef(definition, participant.StoryCharacterId);
            if (storyRef != null && _actorResolver != null && _actorResolver.TryResolveFamilyMember(definition, storyRef, out member))
                return member;

            if (participant.Fallback == ScenarioConversationParticipantFallback.Initiator)
                return initiator;
            if (participant.Fallback == ScenarioConversationParticipantFallback.Partner)
                return partner;
            if (participant.Fallback == ScenarioConversationParticipantFallback.NearestIdleFamily)
                return FindNearestIdlePartner(initiator);
            if (participant.Fallback == ScenarioConversationParticipantFallback.AnyFamily)
                return FindAnyIdleFamily(alreadySelected);

            return null;
        }

        private static ScenarioActorRef ResolveStoryCharacterRef(ScenarioDefinition definition, string storyCharacterId)
        {
            for (int i = 0; definition != null && definition.ScenarioCharacters != null && i < definition.ScenarioCharacters.Count; i++)
            {
                ScenarioNpcDefinition character = definition.ScenarioCharacters[i];
                if (character != null && string.Equals(character.CharacterId, storyCharacterId, StringComparison.OrdinalIgnoreCase))
                    return character.ActorRef;
            }
            return null;
        }

        private ScenarioConversationDefinition SelectRandomConversation(ScenarioDefinition definition, FamilyMember initiator, FamilyMember partner, ScenarioRuntimeState state)
        {
            ScenarioConversationAuthoringDefinition authoring = definition != null ? definition.Conversations : null;
            if (authoring == null || authoring.Conversations == null)
                return null;

            List<ScenarioConversationDefinition> candidates = new List<ScenarioConversationDefinition>();
            float total = 0f;
            for (int i = 0; i < authoring.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = authoring.Conversations[i];
                ScenarioConversationTriggerDefinition trigger = conversation != null ? conversation.Trigger : null;
                if (trigger == null || trigger.Source != ScenarioConversationTriggerSource.Random || trigger.Weight <= 0f)
                    continue;

                string reason;
                Dictionary<string, FamilyMember> ignored;
                if (!CanRun(conversation, state, out reason)
                    || (_conditions != null && !_conditions.AreConditionsSatisfied(definition, conversation.Conditions, state, out reason))
                    || !TryResolveParticipants(definition, conversation, initiator, partner, out ignored, out reason))
                {
                    continue;
                }

                candidates.Add(conversation);
                total += trigger.Weight;
            }

            if (candidates.Count == 0 || total <= 0f)
                return null;

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= candidates[i].Trigger.Weight;
                if (roll <= 0f)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private bool CanRun(ScenarioConversationDefinition conversation, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            ScenarioConversationTriggerDefinition trigger = conversation != null ? conversation.Trigger : null;
            if (trigger == null || state == null)
                return true;

            ScenarioConversationRuntimeRecord record = FindRecord(state, conversation.Id);
            if (record == null)
                return true;
            if (trigger.Once && record.PlayCount > 0)
            {
                reason = "Conversation has already played once.";
                return false;
            }
            if (trigger.CooldownDays <= 0f)
                return true;

            float elapsedDays = (float)(GameTime.Day - record.LastPlayedDay);
            elapsedDays += ((float)(GameTime.Hour - record.LastPlayedHour)) / 24f;
            elapsedDays += ((float)(GameTime.Minute - record.LastPlayedMinute)) / 1440f;
            if (elapsedDays >= trigger.CooldownDays)
                return true;

            reason = "Conversation cooldown is still active.";
            return false;
        }

        private static void RecordPlayed(ScenarioConversationDefinition conversation, ScenarioRuntimeState state)
        {
            if (conversation == null || state == null || string.IsNullOrEmpty(conversation.Id))
                return;

            ScenarioConversationRuntimeRecord record = FindRecord(state, conversation.Id);
            if (record == null)
            {
                record = new ScenarioConversationRuntimeRecord();
                record.ConversationId = conversation.Id;
                state.Conversations.Add(record);
            }

            record.LastPlayedDay = GameTime.Day;
            record.LastPlayedHour = GameTime.Hour;
            record.LastPlayedMinute = GameTime.Minute;
            record.PlayCount = Math.Max(0, record.PlayCount) + 1;
        }

        private static ScenarioConversationRuntimeRecord FindRecord(ScenarioRuntimeState state, string id)
        {
            for (int i = 0; state != null && state.Conversations != null && i < state.Conversations.Count; i++)
            {
                ScenarioConversationRuntimeRecord record = state.Conversations[i];
                if (record != null && string.Equals(record.ConversationId, id, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
            return null;
        }

        private bool HasSuppressedCategory(string category)
        {
            ScenarioConversationSuppressionDefinition settings = _activeDefinition != null && _activeDefinition.Conversations != null ? _activeDefinition.Conversations.Settings : null;
            for (int i = 0; settings != null && settings.SuppressedVanillaCategories != null && i < settings.SuppressedVanillaCategories.Count; i++)
                if (string.Equals(settings.SuppressedVanillaCategories[i], category, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static ScenarioConversationDefinition FindConversation(ScenarioDefinition definition, string id)
        {
            ScenarioConversationAuthoringDefinition authoring = definition != null ? definition.Conversations : null;
            for (int i = 0; authoring != null && authoring.Conversations != null && i < authoring.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = authoring.Conversations[i];
                if (conversation != null && string.Equals(conversation.Id, id, StringComparison.OrdinalIgnoreCase))
                    return conversation;
            }
            return null;
        }

        private static FamilyMember FindNearestIdlePartner(FamilyMember initiator)
        {
            if (initiator == null || FamilyManager.Instance == null || ShelterRoomGrid.Instance == null)
                return null;

            List<FamilyMember> members = FamilyManager.Instance.GetAllFamilyMembers();
            FamilyMember best = null;
            float bestDistance = 1000f;
            ShelterRoomGrid.GridCell cell = ShelterRoomGrid.Instance.GetCell(initiator.transform.position);
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember candidate = members[i];
                if (candidate == null || candidate == initiator || candidate.isSpeaking || !candidate.IsIdle())
                    continue;
                if (cell != ShelterRoomGrid.Instance.GetCell(candidate.transform.position))
                    continue;

                float distance = Vector3.Distance(initiator.transform.position, candidate.transform.position);
                if (distance < 3f && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        private static FamilyMember FindAnyIdleFamily(Dictionary<string, FamilyMember> alreadySelected)
        {
            List<FamilyMember> members = FamilyManager.Instance != null ? FamilyManager.Instance.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member == null || member.isSpeaking || !member.IsIdle() || ContainsMember(alreadySelected, member))
                    continue;
                return member;
            }
            return null;
        }

        private static bool ContainsMember(Dictionary<string, FamilyMember> members, FamilyMember member)
        {
            if (members == null || member == null)
                return false;
            foreach (FamilyMember selected in members.Values)
                if (selected == member)
                    return true;
            return false;
        }

        private static ScenarioConversationRunner EnsureRunner()
        {
            GameObject runtimeObject = GameObject.Find(RuntimeObjectName);
            if (runtimeObject == null)
            {
                runtimeObject = new GameObject(RuntimeObjectName);
                UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
            }

            ScenarioConversationRunner runner = runtimeObject.GetComponent<ScenarioConversationRunner>();
            if (runner == null)
                runner = runtimeObject.AddComponent<ScenarioConversationRunner>();
            return runner;
        }

        private sealed class ScenarioConversationRunner : MonoBehaviour
        {
            public void Play(ScenarioConversationDefinition conversation, Dictionary<string, FamilyMember> participants)
            {
                StartCoroutine(PlayRoutine(conversation, participants));
            }

            private IEnumerator PlayRoutine(ScenarioConversationDefinition conversation, Dictionary<string, FamilyMember> participants)
            {
                SetConversationFlags(participants, true);
                for (int i = 0; conversation != null && conversation.Lines != null && i < conversation.Lines.Count; i++)
                {
                    ScenarioConversationLineDefinition line = conversation.Lines[i];
                    if (line == null)
                        continue;

                    if (line.DelaySeconds > 0f)
                        yield return new WaitForSeconds(line.DelaySeconds);

                    FamilyMember speaker;
                    if (participants != null && participants.TryGetValue(line.SpeakerSlot ?? string.Empty, out speaker) && speaker != null)
                        speaker.Say(ResolveText(line));
                }

                yield return new WaitForSeconds(5f);
                SetConversationFlags(participants, false);
            }

            private static string ResolveText(ScenarioConversationLineDefinition line)
            {
                if (line == null)
                    return string.Empty;
                if (!string.IsNullOrEmpty(line.RawText))
                    return line.RawText;
                if (!string.IsNullOrEmpty(line.TextKey))
                {
                    try { return Localization.Get(line.TextKey); }
                    catch { return line.TextKey; }
                }
                return string.Empty;
            }

            private static void SetConversationFlags(Dictionary<string, FamilyMember> participants, bool value)
            {
                if (participants == null)
                    return;
                foreach (FamilyMember member in participants.Values)
                {
                    if (member == null)
                        continue;
                    try { Traverse.Create(member).Field("isInCoversation").SetValue(value); }
                    catch (Exception ex) { MMLog.WriteWarning("[ScenarioConversationRuntime] Failed to set conversation flag: " + ex.Message); }
                }
            }
        }
    }
}
