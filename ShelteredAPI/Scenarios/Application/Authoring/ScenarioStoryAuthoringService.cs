using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;

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

            ScenarioFlowDefinition flow = EnsureFlow(session.WorkingDefinition);
            if (ScenarioStoryAuthoringActions.IsAddStage(actionId))
                return AddStage(session, flow, out message);

            int stageIndex;
            int delta;
            string token;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioAuthoringActionIds.ActionStoryStageDeletePrefix, flow.Stages.Count, out stageIndex))
            {
                string id = flow.Stages[stageIndex] != null ? flow.Stages[stageIndex].Id : null;
                string reason;
                if (!CanRemoveStage(flow, id, out reason))
                {
                    message = reason;
                    return true;
                }
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
            {
                string oldId = flow.Stages[stageIndex].Id;
                string newId = Decode(token);
                flow.Stages[stageIndex].Id = newId;
                ReplaceStageReferences(flow, oldId, newId);
                MarkDirty(session);
                message = "Renamed story stage to '" + newId + "'.";
                return true;
            }
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

            return TryHandleIntercom(session, flow, actionId, out message);
        }

        private static bool TryHandleIntercom(ScenarioEditorSession session, ScenarioFlowDefinition flow, string actionId, out string message)
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
            if (!ScenarioStoryAuthoringActions.TryResolveIntercom(actionId, flow, out stageIndex, out intercomIndex, out ScenarioIntercomStageDefinition intercom))
                return false;

            if (string.Equals(actionId, ScenarioStoryAuthoringActions.IntercomDelete(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                flow.Stages[stageIndex].IntercomStages.RemoveAt(intercomIndex);
                MarkDirty(session);
                message = "Removed intercom step.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.IntercomDuplicate(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                ScenarioIntercomStageDefinition copy = CloneIntercom(intercom, NextIntercomId(flow.Stages[stageIndex]));
                flow.Stages[stageIndex].IntercomStages.Insert(intercomIndex + 1, copy);
                MarkDirty(session);
                message = "Duplicated intercom step '" + copy.Id + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomMovePrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token)
                && int.TryParse(token, out delta))
                return Move(flow.Stages[stageIndex].IntercomStages, intercomIndex, delta, session, "intercom step", out message);
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomIdPrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
            {
                string oldId = intercom.Id;
                intercom.Id = Decode(token);
                ReplaceIntercomReferences(flow.Stages[stageIndex], oldId, intercom.Id);
                MarkDirty(session);
                message = "Renamed intercom step to '" + intercom.Id + "'.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomTypePrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
            {
                intercom.Type = Decode(token);
                MarkDirty(session);
                message = "Updated intercom type.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomNextPrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
                return SetIntercomTarget(session, intercom, "next", Decode(token), out message);
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryIntercomAlternatePrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
                return SetIntercomTarget(session, intercom, "alternate", Decode(token), out message);
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageChangeTargetPrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
            {
                EnsureStageChange(intercom).Id = NullIfNone(Decode(token));
                MarkDirty(session);
                message = "Updated stage change target.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryStageChangeDelayPrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token)
                && int.TryParse(token, out delta))
            {
                EnsureStageChange(intercom).DelayDays = Math.Max(0, EnsureStageChange(intercom).DelayDays + delta);
                MarkDirty(session);
                message = "Updated stage change delay.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryRecruitTogglePrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
            {
                Toggle(intercom.CharacterIdsToRecruit, Decode(token));
                MarkDirty(session);
                message = "Updated recruitment list.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.RecruitFamily(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                intercom.RecruitAsFamily = !intercom.RecruitAsFamily;
                MarkDirty(session);
                message = "Updated recruitment mode.";
                return true;
            }
            if (ScenarioAuthoringActionParser.TryPairToken(actionId, ScenarioAuthoringActionIds.ActionStoryEndTypePrefix, flow.Stages.Count, out stageIndex, out intercomIndex, out token))
            {
                EnsureEnd(intercom).Type = Decode(token);
                MarkDirty(session);
                message = "Updated encounter end type.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.EndCompleteQuest(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                EnsureEnd(intercom).CompleteQuest = !EnsureEnd(intercom).CompleteQuest;
                MarkDirty(session);
                message = "Updated quest completion outcome.";
                return true;
            }
            if (string.Equals(actionId, ScenarioStoryAuthoringActions.EndCompleteScenario(stageIndex, intercomIndex), StringComparison.Ordinal))
            {
                EnsureEnd(intercom).CompleteParentScenario = !EnsureEnd(intercom).CompleteParentScenario;
                MarkDirty(session);
                message = "Updated scenario completion outcome.";
                return true;
            }

            return TryHandleIntercomChildren(session, flow.Stages[stageIndex], intercom, actionId, stageIndex, intercomIndex, out message);
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
            if (ScenarioStoryAuthoringActions.TryChild(actionId, ScenarioAuthoringActionIds.ActionStoryOptionDeletePrefix, stageIndex, intercomIndex, intercom.Options.Count, out child))
            {
                intercom.Options.RemoveAt(child);
                MarkDirty(session);
                message = "Removed response option.";
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

        private static void ReplaceStageReferences(ScenarioFlowDefinition flow, string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId))
                return;
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (stage == null)
                    continue;
                if (string.Equals(stage.UnansweredNextStage, oldId, StringComparison.OrdinalIgnoreCase))
                    stage.UnansweredNextStage = newId;
                for (int s = 0; stage.IntercomStages != null && s < stage.IntercomStages.Count; s++)
                    if (stage.IntercomStages[s] != null && stage.IntercomStages[s].StageChange != null && string.Equals(stage.IntercomStages[s].StageChange.Id, oldId, StringComparison.OrdinalIgnoreCase))
                        stage.IntercomStages[s].StageChange.Id = newId;
            }
        }

        private static bool CanRemoveStage(ScenarioFlowDefinition flow, string stageId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(stageId))
                return true;

            List<string> references = new List<string>();
            for (int i = 0; flow != null && flow.Stages != null && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                string label = stage != null && !string.IsNullOrEmpty(stage.Id) ? stage.Id : "#" + i.ToString();
                if (stage != null && string.Equals(stage.UnansweredNextStage, stageId, StringComparison.OrdinalIgnoreCase))
                    references.Add(label + " unanswered route");
                for (int s = 0; stage != null && stage.IntercomStages != null && s < stage.IntercomStages.Count; s++)
                {
                    ScenarioIntercomStageDefinition intercom = stage.IntercomStages[s];
                    string intercomLabel = label + "/" + (intercom != null && !string.IsNullOrEmpty(intercom.Id) ? intercom.Id : "#" + s.ToString());
                    if (intercom != null && intercom.StageChange != null && string.Equals(intercom.StageChange.Id, stageId, StringComparison.OrdinalIgnoreCase))
                        references.Add(intercomLabel + " stage change");
                }
            }

            if (references.Count == 0)
                return true;

            reason = "Cannot remove story stage '" + stageId + "' because it is referenced by: " + string.Join(", ", references.ToArray()) + ". Clear those references first.";
            return false;
        }

        private static void ReplaceIntercomReferences(ScenarioFlowStageDefinition stage, string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId))
                return;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step == null)
                    continue;
                if (string.Equals(step.NextId, oldId, StringComparison.OrdinalIgnoreCase))
                    step.NextId = newId;
                if (string.Equals(step.AlternateNextId, oldId, StringComparison.OrdinalIgnoreCase))
                    step.AlternateNextId = newId;
                for (int r = 0; step.RandomizedNextIds != null && r < step.RandomizedNextIds.Count; r++)
                    if (string.Equals(step.RandomizedNextIds[r], oldId, StringComparison.OrdinalIgnoreCase))
                        step.RandomizedNextIds[r] = newId;
                for (int o = 0; step.Options != null && o < step.Options.Count; o++)
                    if (step.Options[o] != null && string.Equals(step.Options[o].NextId, oldId, StringComparison.OrdinalIgnoreCase))
                        step.Options[o].NextId = newId;
            }
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
