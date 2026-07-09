using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Validation;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioStoryAuthoringService
    {
        public bool CanHandle(string actionId)
        {
            return ScenarioStoryAuthoringActions.CanHandle(actionId);
        }

        public bool TryHandleAction(ScenarioEditorSession session, string actionId, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            ScenarioDefinition definition = session.WorkingDefinition;
            ScenarioFlowDefinition flow = EnsureFlow(definition);
            if (ScenarioStoryAuthoringActions.IsAddStage(actionId))
                return AddStage(session, flow, out message);
            if (ScenarioStoryAuthoringActions.IsAddCharacter(actionId))
                return AddCharacter(session, definition, out message);
            if (TryHandleConversationAction(session, definition, actionId, out message))
                return true;

            int stageIndex;
            int delta;
            string token;
            int storyCharacterCount = EnsureCharacters(definition).Count;
            if (TryHandleCharacterEdit(session, definition, actionId, storyCharacterCount, out message))
                return true;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryCharacterDeletePrefix, storyCharacterCount, out stageIndex))
                return DeleteCharacter(session, definition, stageIndex, out message);

            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryCharacterActorPrefix, storyCharacterCount, out stageIndex, out token))
            {
                ScenarioCastMemberReferenceCandidate candidate;
                if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, true, true, Uri.UnescapeDataString(token), out candidate))
                    return false;
                if (definition.ScenarioCharacters[stageIndex] == null)
                    return false;
                definition.ScenarioCharacters[stageIndex].ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef);
                MarkDirty(session);
                string characterId = definition.ScenarioCharacters[stageIndex].CharacterId;
                message = "Linked story character '" + (characterId ?? ("#" + stageIndex.ToString())) + "' to " + candidate.DisplayName + ".";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryCharacterActorClearPrefix, storyCharacterCount, out stageIndex))
            {
                if (definition.ScenarioCharacters[stageIndex] == null)
                    return false;
                definition.ScenarioCharacters[stageIndex].ActorRef = null;
                MarkDirty(session);
                message = "Cleared story character actor link.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryStageDeletePrefix, flow.Stages.Count, out stageIndex))
            {
                string id = flow.Stages[stageIndex] != null ? flow.Stages[stageIndex].Id : null;
                string reason;
                if (!CanRemoveStage(definition, id, out reason))
                {
                    message = reason;
                    return true;
                }
                RecordUndo(session, "Remove story stage");
                flow.Stages.RemoveAt(stageIndex);
                MarkDirty(session);
                message = "Removed story stage '" + (id ?? ("#" + stageIndex.ToString())) + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryStageDuplicatePrefix, flow.Stages.Count, out stageIndex))
            {
                ScenarioFlowStageDefinition copy = CloneStage(flow.Stages[stageIndex], NextStageId(flow));
                flow.Stages.Insert(stageIndex + 1, copy);
                MarkDirty(session);
                message = "Duplicated story stage '" + copy.Id + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryStageMovePrefix, flow.Stages.Count, out stageIndex, out delta))
                return Move(flow.Stages, stageIndex, delta, session, "story stage", out message);
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageIdPrefix, flow.Stages.Count, out stageIndex, out token))
                return RenameStage(session, definition, flow, stageIndex, Decode(token), out message);
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageCharacterTogglePrefix, flow.Stages.Count, out stageIndex, out token))
            {
                Toggle(flow.Stages[stageIndex].CharacterIds, Decode(token));
                MarkDirty(session);
                message = "Updated stage character list.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageUnansweredPrefix, flow.Stages.Count, out stageIndex, out token))
            {
                flow.Stages[stageIndex].UnansweredNextStage = NullIfNone(Decode(token));
                MarkDirty(session);
                message = "Updated unanswered routing.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryStageUnansweredDelayPrefix, flow.Stages.Count, out stageIndex, out delta))
            {
                flow.Stages[stageIndex].UnansweredNextDays = Math.Max(0, flow.Stages[stageIndex].UnansweredNextDays + delta);
                MarkDirty(session);
                message = "Updated unanswered delay to " + flow.Stages[stageIndex].UnansweredNextDays + " day(s).";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryStagePunishPrefix, flow.Stages.Count, out stageIndex))
            {
                flow.Stages[stageIndex].PunishOnUnanswered = !flow.Stages[stageIndex].PunishOnUnanswered;
                MarkDirty(session);
                message = "Updated unanswered punishment.";
                return true;
            }

            return TryHandleIntercom(session, definition, flow, actionId, out message);
        }

        private static bool TryHandleIntercom(ScenarioEditorSession session, ScenarioDefinition definition, ScenarioFlowDefinition flow, string actionId, out string message)
        {
            message = null;
            int stageIndex;
            int intercomIndex;
            int delta;
            string token;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomAddPrefix, flow.Stages.Count, out stageIndex))
            {
                ScenarioFlowStageDefinition stage = flow.Stages[stageIndex];
                stage.IntercomStages.Add(CreateIntercom(stage));
                MarkDirty(session);
                message = "Added intercom step.";
                return true;
            }

            if (TryHandleIntercomChildAction(session, flow, actionId, out message))
                return true;

            if (!ScenarioStoryAuthoringActions.TryResolveIntercom(actionId, flow, out stageIndex, out intercomIndex, out ScenarioIntercomStageDefinition intercom))
                return false;

            int resolvedStageIndex = stageIndex;
            int resolvedIntercomIndex = intercomIndex;
            ScenarioFlowStageDefinition resolvedStage = flow.Stages[resolvedStageIndex];

            if (string.Equals(actionId, ScenarioStoryAuthoringActions.IntercomDelete(resolvedStageIndex, resolvedIntercomIndex), StringComparison.Ordinal))
            {
                resolvedStage.IntercomStages.RemoveAt(resolvedIntercomIndex);
                MarkDirty(session);
                message = "Removed intercom step.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.IntercomDuplicate(resolvedStageIndex, resolvedIntercomIndex), StringComparison.Ordinal))
            {
                ScenarioIntercomStageDefinition copy = CloneIntercom(intercom, NextIntercomId(resolvedStage));
                resolvedStage.IntercomStages.Insert(resolvedIntercomIndex + 1, copy);
                MarkDirty(session);
                message = "Duplicated intercom step '" + copy.Id + "'.";
                return true;
            }
            int parsedStageIndex;
            int parsedIntercomIndex;
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomMovePrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex
                && int.TryParse(token, out delta))
                return Move(resolvedStage.IntercomStages, resolvedIntercomIndex, delta, session, "intercom step", out message);
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomIdPrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
            {
                string oldId = intercom.Id;
                string newIntercomId = Decode(token);
                string intercomReason;
                if (!ValidateIntercomRename(resolvedStage, oldId, newIntercomId, out intercomReason))
                {
                    message = intercomReason;
                    return true;
                }
                RecordUndo(session, "Rename intercom step");
                intercom.Id = newIntercomId;
                ScenarioReferenceIndex.RedirectReferences(definition, ScenarioReferenceTargetKind.IntercomStep, oldId, newIntercomId, resolvedStageIndex);
                MarkDirty(session);
                message = "Renamed intercom step to '" + intercom.Id + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomTypePrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
            {
                intercom.Type = Decode(token);
                MarkDirty(session);
                message = "Updated intercom type.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomNextPrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
                return SetIntercomTarget(session, intercom, "next", Decode(token), out message);
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomAlternatePrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
                return SetIntercomTarget(session, intercom, "alternate", Decode(token), out message);
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageChangeTargetPrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
            {
                EnsureStageChange(intercom).Id = NullIfNone(Decode(token));
                MarkDirty(session);
                message = "Updated stage change target.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex
                && int.TryParse(token, out delta))
            {
                EnsureStageChange(intercom).DelayDays = Math.Max(0, EnsureStageChange(intercom).DelayDays + delta);
                MarkDirty(session);
                message = "Updated stage change delay.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryRecruitTogglePrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
            {
                Toggle(intercom.CharacterIdsToRecruit, Decode(token));
                MarkDirty(session);
                message = "Updated recruitment list.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.RecruitFamily(resolvedStageIndex, resolvedIntercomIndex), StringComparison.Ordinal))
            {
                intercom.RecruitAsFamily = !intercom.RecruitAsFamily;
                MarkDirty(session);
                message = "Updated recruitment mode.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryEndTypePrefix, flow.Stages.Count, out parsedStageIndex, out parsedIntercomIndex, out token)
                && parsedStageIndex == resolvedStageIndex
                && parsedIntercomIndex == resolvedIntercomIndex)
            {
                EnsureEnd(intercom).Type = Decode(token);
                MarkDirty(session);
                message = "Updated encounter end type.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.EndCompleteQuest(resolvedStageIndex, resolvedIntercomIndex), StringComparison.Ordinal))
            {
                EnsureEnd(intercom).CompleteQuest = !EnsureEnd(intercom).CompleteQuest;
                MarkDirty(session);
                message = "Updated quest completion outcome.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.EndCompleteScenario(resolvedStageIndex, resolvedIntercomIndex), StringComparison.Ordinal))
            {
                ScenarioEncounterEndOptionsDefinition end = EnsureEnd(intercom);
                if (end.CompleteParentScenario)
                {
                    end.CompleteParentScenario = false;
                    MarkDirty(session);
                    message = "Removed unsupported parent-scenario completion. Use Victory conditions for scenario completion.";
                }
                else
                {
                    message = "Parent-scenario completion is disabled here; use Victory conditions for scenario completion.";
                }
                return true;
            }

            return TryHandleIntercomChildren(session, resolvedStage, intercom, actionId, resolvedStageIndex, resolvedIntercomIndex, out message);
        }

        private static bool TryHandleIntercomChildAction(ScenarioEditorSession session, ScenarioFlowDefinition flow, string actionId, out string message)
        {
            message = null;
            if (flow == null || flow.Stages == null)
                return false;

            for (int stageIndex = 0; stageIndex < flow.Stages.Count; stageIndex++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[stageIndex];
                if (stage == null || stage.IntercomStages == null)
                    continue;

                for (int intercomIndex = 0; intercomIndex < stage.IntercomStages.Count; intercomIndex++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[intercomIndex];
                    if (intercom == null)
                        continue;

                    if (string.Equals(actionId, ScenarioStoryAuthoringActions.DialogueAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
                    {
                        intercom.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = FirstOrNone(stage.CharacterIds), TextKey = "dialogue_" + (intercom.Dialogue.Count + 1).ToString() });
                        MarkDirty(session);
                        message = "Added dialogue line.";
                        return true;
                    }

                    if (string.Equals(actionId, ScenarioStoryAuthoringActions.OptionAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
                    {
                        intercom.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "option_" + (intercom.Options.Count + 1).ToString(), NextId = FirstOtherIntercomId(stage, intercom.Id) });
                        MarkDirty(session);
                        message = "Added response option.";
                        return true;
                    }

                    int child;
                    string token;
                    if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryDialogueKeyPrefix, stageIndex, intercomIndex, intercom.Dialogue.Count, out child, out token))
                    {
                        intercom.Dialogue[child].TextKey = Decode(token);
                        MarkDirty(session);
                        message = "Updated dialogue key.";
                        return true;
                    }

                    if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix, stageIndex, intercomIndex, intercom.Options.Count, out child, out token))
                    {
                        intercom.Options[child].NextId = NullIfNone(Decode(token));
                        MarkDirty(session);
                        message = "Updated option route.";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryHandleIntercomChildren(ScenarioEditorSession session, ScenarioFlowStageDefinition stage, ScenarioIntercomStageDefinition intercom, string actionId, int stageIndex, int intercomIndex, out string message)
        {
            message = null;
            int child;
            string token;
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.DialogueAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                intercom.Dialogue.Add(new ScenarioDialogueLineDefinition { Character = FirstOrNone(stage.CharacterIds), TextKey = "dialogue_" + (intercom.Dialogue.Count + 1).ToString() });
                MarkDirty(session);
                message = "Added dialogue line.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryDialogueSpeakerPrefix, stageIndex, intercomIndex, intercom.Dialogue.Count, out child, out token))
            {
                intercom.Dialogue[child].Character = NullIfNone(Decode(token));
                MarkDirty(session);
                message = "Updated dialogue speaker.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryDialogueKeyPrefix, stageIndex, intercomIndex, intercom.Dialogue.Count, out child, out token))
            {
                intercom.Dialogue[child].TextKey = Decode(token);
                MarkDirty(session);
                message = "Updated dialogue key.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryDialogueDeletePrefix, stageIndex, intercomIndex, intercom.Dialogue.Count, out child))
            {
                intercom.Dialogue.RemoveAt(child);
                MarkDirty(session);
                message = "Removed dialogue line.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.OptionAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                intercom.Options.Add(new ScenarioDialogueOptionDefinition { TextKey = "option_" + (intercom.Options.Count + 1).ToString(), NextId = FirstOtherIntercomId(stage, intercom.Id) });
                MarkDirty(session);
                message = "Added response option.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryOptionKeyPrefix, stageIndex, intercomIndex, intercom.Options.Count, out child, out token))
            {
                intercom.Options[child].TextKey = Decode(token);
                MarkDirty(session);
                message = "Updated option key.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix, stageIndex, intercomIndex, intercom.Options.Count, out child, out token))
            {
                intercom.Options[child].NextId = NullIfNone(Decode(token));
                MarkDirty(session);
                message = "Updated option route.";
                return true;
            }

            if (StartsWithAny(actionId, ScenarioAuthoringActionIds.ActionStoryOptionKeyPrefix, ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix, ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix))
            {
                message = "The selected response option no longer exists.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix, stageIndex, intercomIndex, intercom.Options.Count, out child))
            {
                intercom.Options.RemoveAt(child);
                MarkDirty(session);
                message = "Removed response option.";
                return true;
            }

            if (StartsWithAny(actionId, ScenarioAuthoringActionIds.ActionStoryOptionKeyPrefix, ScenarioAuthoringActionIds.ActionStoryOptionNextPrefix, ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix))
            {
                message = "The selected response option no longer exists.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.RandomRouteAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                intercom.RandomizedNextIds.Add(FirstOtherIntercomId(stage, intercom.Id));
                MarkDirty(session);
                message = "Added randomized route.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomRandomTargetPrefix, stageIndex, intercomIndex, intercom.RandomizedNextIds.Count, out child, out token))
            {
                intercom.RandomizedNextIds[child] = NullIfNone(Decode(token));
                MarkDirty(session);
                message = "Updated randomized route.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomRandomDeletePrefix, stageIndex, intercomIndex, intercom.RandomizedNextIds.Count, out child))
            {
                intercom.RandomizedNextIds.RemoveAt(child);
                MarkDirty(session);
                message = "Removed randomized route.";
                return true;
            }

            return TryHandleItemsAndMilestones(session, intercom, actionId, stageIndex, intercomIndex, out message);
        }

        private static bool TryHandleItemsAndMilestones(ScenarioEditorSession session, ScenarioIntercomStageDefinition intercom, string actionId, int stageIndex, int intercomIndex, out string message)
        {
            message = null;
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.RewardAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
                return AddItem(session, intercom.Items, "reward", out message);
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.RemovalAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
                return AddItem(session, intercom.ItemsToRemove, "removal", out message);
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.MilestoneAdd(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                intercom.SetMilestones.Add(new ScenarioMilestoneDefinition { Name = "milestone_" + (intercom.SetMilestones.Count + 1).ToString(), Scope = "Scenario", Action = "Set" });
                MarkDirty(session);
                message = "Added milestone.";
                return true;
            }

            int child;
            int delta;
            string token;
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryRewardItemPrefix, stageIndex, intercomIndex, intercom.Items.Count, out child, out token))
                return SetItem(session, intercom.Items[child], Decode(token), out message);
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryRemovalItemPrefix, stageIndex, intercomIndex, intercom.ItemsToRemove.Count, out child, out token))
                return SetItem(session, intercom.ItemsToRemove[child], Decode(token), out message);
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryRewardQuantityPrefix, stageIndex, intercomIndex, intercom.Items.Count, out child, out token) && int.TryParse(token, out delta))
                return StepQuantity(session, intercom.Items[child], delta, out message);
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryRemovalQuantityPrefix, stageIndex, intercomIndex, intercom.ItemsToRemove.Count, out child, out token) && int.TryParse(token, out delta))
                return StepQuantity(session, intercom.ItemsToRemove[child], delta, out message);
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryRewardDeletePrefix, stageIndex, intercomIndex, intercom.Items.Count, out child))
            {
                intercom.Items.RemoveAt(child);
                MarkDirty(session);
                message = "Removed reward item.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryRemovalDeletePrefix, stageIndex, intercomIndex, intercom.ItemsToRemove.Count, out child))
            {
                intercom.ItemsToRemove.RemoveAt(child);
                MarkDirty(session);
                message = "Removed removal item.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChildToken(actionId, ScenarioAuthoringActionIds.ActionStoryMilestoneNamePrefix, stageIndex, intercomIndex, intercom.SetMilestones.Count, out child, out token))
            {
                intercom.SetMilestones[child].Name = Decode(token);
                MarkDirty(session);
                message = "Updated milestone name.";
                return true;
            }
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryMilestoneDeletePrefix, stageIndex, intercomIndex, intercom.SetMilestones.Count, out child))
            {
                intercom.SetMilestones.RemoveAt(child);
                MarkDirty(session);
                message = "Removed milestone.";
                return true;
            }
            return false;
        }

        private static bool AddCharacter(ScenarioEditorSession session, ScenarioDefinition definition, out string message)
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

            ScenarioNpcDefinition character = new ScenarioNpcDefinition();
            character.CharacterId = characterId;
            character.DisplayName = "Story Character " + (characters.Count + 1).ToString();
            character.PresetId = "Default";
            characters.Add(character);
            MarkDirty(session);
            message = "Added story character '" + character.DisplayName + "' (" + character.CharacterId + ").";
            return true;
        }

        private static bool TryHandleCharacterEdit(ScenarioEditorSession session, ScenarioDefinition definition, string actionId, int characterCount, out string message)
        {
            message = null;
            string field;
            int characterIndex;
            string value;
            if (!TryParseCharacterEditAction(actionId, out field, out characterIndex, out value))
                return false;
            if (characterIndex < 0 || characterIndex >= characterCount)
            {
                message = "Story character no longer exists.";
                return true;
            }

            ScenarioNpcDefinition character = definition.ScenarioCharacters[characterIndex];
            if (character == null)
            {
                message = "Story character row is empty.";
                return true;
            }

            string normalized = value != null ? value.Trim() : string.Empty;
            if (string.Equals(field, "displayName", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(normalized))
                {
                    message = "Story character display name cannot be empty.";
                    return true;
                }
                character.DisplayName = normalized;
            }
            else if (string.Equals(field, "presetId", StringComparison.OrdinalIgnoreCase))
                character.PresetId = normalized;
            else if (string.Equals(field, "personality", StringComparison.OrdinalIgnoreCase))
                character.Personality = normalized;
            else if (string.Equals(field, "species", StringComparison.OrdinalIgnoreCase))
                character.Species = normalized;
            else
            {
                message = "Unknown story character field '" + field + "'.";
                return true;
            }

            MarkDirty(session);
            message = "Updated story character '" + DisplayCharacterName(character) + "'. CharacterId remains '" + character.CharacterId + "'.";
            return true;
        }

        private static bool DeleteCharacter(ScenarioEditorSession session, ScenarioDefinition definition, int characterIndex, out string message)
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

        private static bool AddStage(ScenarioEditorSession session, ScenarioFlowDefinition flow, out string message)
        {
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
            list.Add(new ItemEntry { ItemId = ShelteredAPI.Scenarios.Infrastructure.Unity.ScenarioInventoryItemCatalog.DefaultItemId(), Quantity = 1 });
            MarkDirty(session);
            message = "Added " + label + " item.";
            return true;
        }

        private static bool TryHandleConversationAction(ScenarioEditorSession session, ScenarioDefinition definition, string actionId, out string message)
        {
            message = null;
            ScenarioConversationAuthoringDefinition conversations = EnsureConversations(definition);
            int count = conversations.Conversations.Count;
            int index;
            int delta;
            string token;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionStoryConversationAdd, StringComparison.Ordinal))
            {
                conversations.Conversations.Add(CreateConversation(definition));
                MarkDirty(session);
                message = "Added NPC conversation.";
                return true;
            }
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionStoryConversationSuppressionToggle, StringComparison.Ordinal))
            {
                conversations.Settings.SuppressVanillaRandomChatter = !conversations.Settings.SuppressVanillaRandomChatter;
                MarkDirty(session);
                message = conversations.Settings.SuppressVanillaRandomChatter ? "Vanilla random chatter suppressed." : "Vanilla random chatter restored.";
                return true;
            }
            if (TryTokenOnly(actionId, ScenarioAuthoringActionIds.ActionStoryConversationSuppressionCategoryPrefix, out token))
            {
                Toggle(conversations.Settings.SuppressedVanillaCategories, Decode(token));
                MarkDirty(session);
                message = "Updated vanilla chatter category suppression.";
                return true;
            }
            if (TryTokenOnly(actionId, ScenarioAuthoringActionIds.ActionStoryConversationSuppressionTopicPrefix, out token))
            {
                ReplaceCsv(conversations.Settings.SuppressedVanillaTopicKeys, Decode(token));
                MarkDirty(session);
                message = "Updated stored vanilla topic-key suppression policy.";
                return true;
            }

            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationPreviewPrefix, count, out index))
                return PreviewConversation(definition, conversations.Conversations[index], out message);
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationDeletePrefix, count, out index))
            {
                string id = conversations.Conversations[index] != null ? conversations.Conversations[index].Id : null;
                conversations.Conversations.RemoveAt(index);
                MarkDirty(session);
                message = "Removed NPC conversation '" + (id ?? ("#" + index.ToString(CultureInfo.InvariantCulture))) + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationDuplicatePrefix, count, out index))
            {
                ScenarioConversationDefinition copy = CloneConversation(conversations.Conversations[index], NextConversationId(definition));
                conversations.Conversations.Insert(index + 1, copy);
                MarkDirty(session);
                message = "Duplicated NPC conversation '" + copy.Id + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationMovePrefix, count, out index, out delta))
                return Move(conversations.Conversations, index, delta, session, "NPC conversation", out message);
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationIdPrefix, count, out index, out token))
            {
                conversations.Conversations[index].Id = Decode(token);
                MarkDirty(session);
                message = "Renamed NPC conversation.";
                return true;
            }

            if (TryHandleConversationTrigger(session, conversations, actionId, out message))
                return true;
            if (TryHandleConversationParticipant(session, definition, conversations, actionId, out message))
                return true;
            if (TryHandleConversationLine(session, conversations, actionId, out message))
                return true;

            return false;
        }

        private static bool TryHandleConversationTrigger(ScenarioEditorSession session, ScenarioConversationAuthoringDefinition conversations, string actionId, out string message)
        {
            message = null;
            int index;
            int delta;
            string token;
            int count = conversations.Conversations.Count;
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerSourcePrefix, count, out index, out token))
            {
                EnsureTrigger(conversations.Conversations[index]).Source = ParseTriggerSource(Decode(token));
                MarkDirty(session);
                message = "Updated conversation trigger source.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerIdPrefix, count, out index, out token))
            {
                EnsureTrigger(conversations.Conversations[index]).TriggerId = Decode(token);
                MarkDirty(session);
                message = "Updated conversation event trigger id.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerWeightPrefix, count, out index, out token))
            {
                float value;
                if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    return false;
                ScenarioConversationTriggerDefinition trigger = EnsureTrigger(conversations.Conversations[index]);
                trigger.Weight = Math.Max(0.1f, trigger.Weight + value);
                MarkDirty(session);
                message = "Updated random conversation weight.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerCooldownPrefix, count, out index, out delta))
            {
                ScenarioConversationTriggerDefinition trigger = EnsureTrigger(conversations.Conversations[index]);
                trigger.CooldownDays = Math.Max(0, trigger.CooldownDays + delta);
                MarkDirty(session);
                message = "Updated conversation cooldown.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerOncePrefix, count, out index))
            {
                ScenarioConversationTriggerDefinition trigger = EnsureTrigger(conversations.Conversations[index]);
                trigger.Once = !trigger.Once;
                MarkDirty(session);
                message = "Updated conversation once policy.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerDayPrefix, count, out index, out delta))
            {
                EnsureTrigger(conversations.Conversations[index]).Time.Day = Math.Max(1, EnsureTrigger(conversations.Conversations[index]).Time.Day + delta);
                MarkDirty(session);
                message = "Updated conversation timeline day.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerHourPrefix, count, out index, out delta))
            {
                EnsureTrigger(conversations.Conversations[index]).Time.Hour = ScenarioAuthoringSchedule.Clamp(EnsureTrigger(conversations.Conversations[index]).Time.Hour + delta, 0, 23);
                MarkDirty(session);
                message = "Updated conversation timeline hour.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TrySignedIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationTriggerMinutePrefix, count, out index, out delta))
            {
                EnsureTrigger(conversations.Conversations[index]).Time.Minute = ScenarioAuthoringSchedule.Clamp(EnsureTrigger(conversations.Conversations[index]).Time.Minute + delta, 0, 59);
                MarkDirty(session);
                message = "Updated conversation timeline minute.";
                return true;
            }

            return false;
        }

        private static bool TryHandleConversationParticipant(ScenarioEditorSession session, ScenarioDefinition definition, ScenarioConversationAuthoringDefinition conversations, string actionId, out string message)
        {
            message = null;
            int conversationIndex;
            int participantIndex;
            string token;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantAddPrefix, conversations.Conversations.Count, out conversationIndex))
            {
                ScenarioConversationDefinition conversation = conversations.Conversations[conversationIndex];
                conversation.Participants.Add(new ScenarioConversationParticipantDefinition
                {
                    Slot = NextParticipantSlot(conversation),
                    Fallback = conversation.Participants.Count == 0 ? ScenarioConversationParticipantFallback.Initiator : ScenarioConversationParticipantFallback.Partner,
                    Required = true
                });
                MarkDirty(session);
                message = "Added conversation participant.";
                return true;
            }
            if (!TryConversationPair(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantDeletePrefix, conversations, out conversationIndex, out participantIndex))
            {
                if (TryConversationPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantSlotPrefix, conversations, out conversationIndex, out participantIndex, out token))
                {
                    conversations.Conversations[conversationIndex].Participants[participantIndex].Slot = Decode(token);
                    MarkDirty(session);
                    message = "Updated participant slot.";
                    return true;
                }
                if (TryConversationPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantStoryPrefix, conversations, out conversationIndex, out participantIndex, out token))
                {
                    conversations.Conversations[conversationIndex].Participants[participantIndex].StoryCharacterId = NullIfNone(Decode(token));
                    MarkDirty(session);
                    message = "Updated participant story character.";
                    return true;
                }
                if (TryConversationPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantActorPrefix, conversations, out conversationIndex, out participantIndex, out token))
                {
                    ScenarioCastMemberReferenceCandidate candidate;
                    if (!ScenarioCastMemberReferenceCatalog.TryFindByToken(definition, true, true, Uri.UnescapeDataString(token), out candidate))
                        return false;
                    conversations.Conversations[conversationIndex].Participants[participantIndex].ActorRef = ScenarioCastMemberReferenceCatalog.CopyActorRef(candidate.ActorRef);
                    MarkDirty(session);
                    message = "Updated participant actor reference.";
                    return true;
                }
                if (TryConversationPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantFallbackPrefix, conversations, out conversationIndex, out participantIndex, out token))
                {
                    conversations.Conversations[conversationIndex].Participants[participantIndex].Fallback = ParseFallback(Decode(token));
                    MarkDirty(session);
                    message = "Updated participant fallback.";
                    return true;
                }
                if (TryConversationPair(actionId, ScenarioAuthoringActionIds.ActionStoryConversationParticipantRequiredPrefix, conversations, out conversationIndex, out participantIndex))
                {
                    ScenarioConversationParticipantDefinition participant = conversations.Conversations[conversationIndex].Participants[participantIndex];
                    participant.Required = !participant.Required;
                    MarkDirty(session);
                    message = "Updated participant required policy.";
                    return true;
                }
                return false;
            }

            conversations.Conversations[conversationIndex].Participants.RemoveAt(participantIndex);
            MarkDirty(session);
            message = "Removed conversation participant.";
            return true;
        }

        private static bool TryHandleConversationLine(ScenarioEditorSession session, ScenarioConversationAuthoringDefinition conversations, string actionId, out string message)
        {
            message = null;
            int conversationIndex;
            int lineIndex;
            string token;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryConversationLineAddPrefix, conversations.Conversations.Count, out conversationIndex))
            {
                ScenarioConversationDefinition conversation = conversations.Conversations[conversationIndex];
                conversation.Lines.Add(new ScenarioConversationLineDefinition
                {
                    SpeakerSlot = conversation.Participants.Count > 0 ? conversation.Participants[0].Slot : "A",
                    RawText = "New line",
                    DelaySeconds = conversation.Lines.Count == 0 ? 0f : 6f
                });
                MarkDirty(session);
                message = "Added conversation line.";
                return true;
            }
            if (TryConversationLinePair(actionId, ScenarioAuthoringActionIds.ActionStoryConversationLineDeletePrefix, conversations, out conversationIndex, out lineIndex))
            {
                conversations.Conversations[conversationIndex].Lines.RemoveAt(lineIndex);
                MarkDirty(session);
                message = "Removed conversation line.";
                return true;
            }
            if (TryConversationLinePairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationLineSpeakerPrefix, conversations, out conversationIndex, out lineIndex, out token))
            {
                conversations.Conversations[conversationIndex].Lines[lineIndex].SpeakerSlot = Decode(token);
                MarkDirty(session);
                message = "Updated line speaker.";
                return true;
            }
            if (TryConversationLinePairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationLineTextPrefix, conversations, out conversationIndex, out lineIndex, out token))
            {
                conversations.Conversations[conversationIndex].Lines[lineIndex].RawText = Decode(token);
                MarkDirty(session);
                message = "Updated line text.";
                return true;
            }
            if (TryConversationLinePairToken(actionId, ScenarioAuthoringActionIds.ActionStoryConversationLineDelayPrefix, conversations, out conversationIndex, out lineIndex, out token))
            {
                float delta;
                if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out delta))
                    return false;
                ScenarioConversationLineDefinition line = conversations.Conversations[conversationIndex].Lines[lineIndex];
                line.DelaySeconds = Math.Max(0f, line.DelaySeconds + delta);
                MarkDirty(session);
                message = "Updated line delay.";
                return true;
            }

            return false;
        }

        private static bool PreviewConversation(ScenarioDefinition definition, ScenarioConversationDefinition conversation, out string message)
        {
            message = null;
            try
            {
                ScenarioConversationRuntimeService service = ScenarioCompositionRoot.ResolveRuntime<ScenarioConversationRuntimeService>();
                ScenarioRuntimeStateService stateService = ScenarioCompositionRoot.ResolveRuntime<ScenarioRuntimeStateService>();
                if (service == null || stateService == null)
                {
                    message = "Conversation preview is unavailable because the scenario runtime is not initialized.";
                    return true;
                }

                service.Activate(definition);
                return service.Handle(definition, new ScenarioEffectDefinition
                {
                    Kind = ScenarioEffectKind.StartConversation,
                    ConversationId = conversation != null ? conversation.Id : null,
                    TargetId = conversation != null ? conversation.Id : null
                }, stateService.State, out message);
            }
            catch (Exception ex)
            {
                message = "Conversation preview failed: " + ex.Message;
                return true;
            }
        }

        private static bool SetItem(ScenarioEditorSession session, ItemEntry item, string itemId, out string message)
        {
            item.ItemId = itemId;
            MarkDirty(session);
            message = "Updated item.";
            return true;
        }

        private static bool StartsWithAny(string value, params string[] prefixes)
        {
            if (string.IsNullOrEmpty(value) || prefixes == null)
                return false;

            for (int i = 0; i < prefixes.Length; i++)
            {
                if (!string.IsNullOrEmpty(prefixes[i]) && value.StartsWith(prefixes[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
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

        // Safe stage rename: validate, record undo, move the declaration and every reference
        // atomically. Reference re-pointing is delegated to the shared reference index so the
        // set of "stage reference" shapes lives in exactly one place.
        private static bool RenameStage(ScenarioEditorSession session, ScenarioDefinition definition, ScenarioFlowDefinition flow, int stageIndex, string newId, out string message)
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

        // Reference-aware delete guard: block removal while references exist and list them, using
        // the shared reference index so the guard and Find Usages agree on what counts.
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

        private static void RecordUndo(ScenarioEditorSession session, string description)
        {
            if (session == null || session.WorkingDefinition == null)
                return;
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            if (history != null)
                history.RecordAuthoringChange(session.WorkingDefinition, description, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
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

        private static bool TryTokenOnly(string actionId, string prefix, out string token)
        {
            token = null;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            token = actionId.Substring(prefix.Length);
            return true;
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

        private static bool TryConversationPair(string actionId, string prefix, ScenarioConversationAuthoringDefinition conversations, out int conversationIndex, out int childIndex)
        {
            conversationIndex = -1;
            childIndex = -1;
            int first;
            int second;
            if (!ScenarioAuthoringActionParser.TryPairIndex(actionId, prefix, conversations.Conversations.Count, out first, out second))
                return false;
            if (!HasParticipant(conversations, first, second))
                return false;
            conversationIndex = first;
            childIndex = second;
            return true;
        }

        private static bool TryConversationPairToken(string actionId, string prefix, ScenarioConversationAuthoringDefinition conversations, out int conversationIndex, out int childIndex, out string token)
        {
            conversationIndex = -1;
            childIndex = -1;
            token = null;
            int first;
            int second;
            if (!ScenarioAuthoringActionParser.TryPairToken(actionId, prefix, conversations.Conversations.Count, out first, out second, out token))
                return false;
            if (!HasParticipant(conversations, first, second))
                return false;
            conversationIndex = first;
            childIndex = second;
            return true;
        }

        private static bool TryConversationLinePair(string actionId, string prefix, ScenarioConversationAuthoringDefinition conversations, out int conversationIndex, out int lineIndex)
        {
            conversationIndex = -1;
            lineIndex = -1;
            int first;
            int second;
            if (!ScenarioAuthoringActionParser.TryPairIndex(actionId, prefix, conversations.Conversations.Count, out first, out second))
                return false;
            if (!HasLine(conversations, first, second))
                return false;
            conversationIndex = first;
            lineIndex = second;
            return true;
        }

        private static bool TryConversationLinePairToken(string actionId, string prefix, ScenarioConversationAuthoringDefinition conversations, out int conversationIndex, out int lineIndex, out string token)
        {
            conversationIndex = -1;
            lineIndex = -1;
            token = null;
            int first;
            int second;
            if (!ScenarioAuthoringActionParser.TryPairToken(actionId, prefix, conversations.Conversations.Count, out first, out second, out token))
                return false;
            if (!HasLine(conversations, first, second))
                return false;
            conversationIndex = first;
            lineIndex = second;
            return true;
        }

        private static bool HasParticipant(ScenarioConversationAuthoringDefinition conversations, int conversationIndex, int participantIndex)
        {
            return conversations != null
                && conversations.Conversations != null
                && conversationIndex >= 0
                && conversationIndex < conversations.Conversations.Count
                && conversations.Conversations[conversationIndex] != null
                && conversations.Conversations[conversationIndex].Participants != null
                && participantIndex >= 0
                && participantIndex < conversations.Conversations[conversationIndex].Participants.Count;
        }

        private static bool HasLine(ScenarioConversationAuthoringDefinition conversations, int conversationIndex, int lineIndex)
        {
            return conversations != null
                && conversations.Conversations != null
                && conversationIndex >= 0
                && conversationIndex < conversations.Conversations.Count
                && conversations.Conversations[conversationIndex] != null
                && conversations.Conversations[conversationIndex].Lines != null
                && lineIndex >= 0
                && lineIndex < conversations.Conversations[conversationIndex].Lines.Count;
        }

        private static bool TryParseCharacterEditAction(string actionId, out string field, out int characterIndex, out string value)
        {
            field = null;
            characterIndex = -1;
            value = null;
            string prefix = ScenarioAuthoringActionIds.ActionStoryCharacterEditPrefix;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string body = actionId.Substring(prefix.Length);
            int firstDot = body.IndexOf('.');
            if (firstDot <= 0)
                return false;

            int secondDot = body.IndexOf('.', firstDot + 1);
            if (secondDot <= firstDot)
                return false;

            field = body.Substring(0, firstDot);
            if (!int.TryParse(body.Substring(firstDot + 1, secondDot - firstDot - 1), out characterIndex))
                return false;

            value = Decode(body.Substring(secondDot + 1));
            return true;
        }

        private static string Decode(string token)
        {
            return ScenarioStoryAuthoringActions.DecodeToken(token);
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
