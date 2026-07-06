using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal sealed class ScenarioStoryFocusedEditorCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioStoryAuthoringService _storyService;
        private readonly IScenarioEditorService _editorService;

        public ScenarioStoryFocusedEditorCommandHandler(
            ScenarioStoryAuthoringService storyService,
            IScenarioEditorService editorService)
        {
            _storyService = storyService;
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || !ScenarioStoryFocusedEditorActions.CanHandle(actionId))
                return false;

            handled = true;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
            {
                message = "No active scenario draft is available.";
                return true;
            }

            if (string.Equals(actionId, ScenarioStoryFocusedEditorActions.ActionStageOpenNew, StringComparison.Ordinal))
                return OpenNewStage(state, session, definition, out message);
            if (string.Equals(actionId, ScenarioStoryFocusedEditorActions.ActionSave, StringComparison.Ordinal))
                return Close(state, false, session, out message);
            if (string.Equals(actionId, ScenarioStoryFocusedEditorActions.ActionCancel, StringComparison.Ordinal))
                return Close(state, true, session, out message);

            int stageIndex;
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioStoryFocusedEditorActions.ActionStageOpenPrefix, CountStages(definition), out stageIndex))
                return OpenExistingStage(state, stageIndex, out message);
            if (ScenarioAuthoringActionParser.TryIndex(actionId, ScenarioStoryFocusedEditorActions.ActionUnansweredNewStagePrefix, CountStages(definition), out stageIndex))
                return AddStageAndRoute(session, definition, stageIndex, -1, true, out message);

            string token;
            if (ScenarioAuthoringActionParser.TryIndexToken(actionId, ScenarioStoryFocusedEditorActions.ActionStageTitlePrefix, CountStages(definition), out stageIndex, out token))
                return SetStageTitle(session, definition, stageIndex, ScenarioStoryAuthoringActions.DecodeToken(token), out message);

            int intercomIndex;
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioStoryFocusedEditorActions.ActionStageChangeNewStagePrefix, CountStages(definition), out stageIndex, out intercomIndex))
                return AddStageAndRoute(session, definition, stageIndex, intercomIndex, false, out message);

            if (TryHandleEndOptionItemAction(session, definition, actionId, out message))
                return true;

            handled = false;
            return false;
        }

        private bool OpenNewStage(ScenarioAuthoringState state, ScenarioEditorSession session, ScenarioDefinition definition, out string message)
        {
            message = null;
            if (_storyService == null)
            {
                message = "Story editing is unavailable.";
                return true;
            }

            string addMessage;
            _storyService.TryHandleAction(session, ScenarioAuthoringActionIds.ActionStoryStageAdd, out addMessage);
            int index = CountStages(definition) - 1;
            OpenFocusedStage(state, index, true);
            message = "New story stage opened.";
            return true;
        }

        private static bool OpenExistingStage(ScenarioAuthoringState state, int stageIndex, out string message)
        {
            OpenFocusedStage(state, stageIndex, false);
            message = "Story stage opened.";
            return true;
        }

        private static bool Close(ScenarioAuthoringState state, bool cancel, ScenarioEditorSession session, out string message)
        {
            message = null;
            if (!IsStoryFocused(state))
            {
                message = "No story stage editor is open.";
                return true;
            }

            bool discard = cancel && state.FocusedEditorIsNew;
            int index = state.FocusedEditorIndex;
            if (discard && session != null && session.WorkingDefinition != null)
            {
                ScenarioFlowDefinition flow = session.WorkingDefinition.ScenarioFlow;
                if (flow != null && flow.Stages != null && index >= 0 && index < flow.Stages.Count)
                {
                    string label = flow.Stages[index] != null ? flow.Stages[index].Id : null;
                    flow.Stages.RemoveAt(index);
                    ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
                    message = "New story stage discarded" + (!string.IsNullOrEmpty(label) ? ": " + label : string.Empty) + ".";
                }
            }

            state.TimelineSelectedEntryId = discard ? null : ScenarioStoryFocusedEditorActions.FocusedEntryId(index);
            state.FocusedEditorKind = null;
            state.FocusedEditorIndex = -1;
            state.FocusedEditorIsNew = false;
            if (string.IsNullOrEmpty(message))
                message = cancel ? "Story stage editor closed." : "Story stage saved.";
            return true;
        }

        private static bool SetStageTitle(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, string title, out string message)
        {
            ScenarioFlowStageDefinition stage = GetStage(definition, stageIndex);
            if (stage == null)
            {
                message = "Story stage is missing.";
                return true;
            }

            ScenarioIntercomStageDefinition intercom = EnsureFirstIntercom(stage);
            intercom.StageDescriptionKey = string.IsNullOrEmpty(title) ? null : title;
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
            message = "Updated story stage title.";
            return true;
        }

        private bool AddStageAndRoute(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, int intercomIndex, bool unanswered, out string message)
        {
            if (_storyService == null)
            {
                message = "Story editing is unavailable.";
                return true;
            }

            ScenarioFlowStageDefinition source = GetStage(definition, stageIndex);
            if (source == null)
            {
                message = "Story stage is missing.";
                return true;
            }

            string addMessage;
            _storyService.TryHandleAction(session, ScenarioAuthoringActionIds.ActionStoryStageAdd, out addMessage);
            ScenarioFlowStageDefinition target = GetStage(definition, CountStages(definition) - 1);
            string targetId = target != null ? target.Id : null;
            if (string.IsNullOrEmpty(targetId))
            {
                message = "New story stage could not be created.";
                return true;
            }

            if (unanswered)
            {
                source.UnansweredNextStage = targetId;
                message = "Created and selected ignored-call stage '" + targetId + "'.";
            }
            else
            {
                ScenarioIntercomStageDefinition intercom = GetIntercom(definition, stageIndex, intercomIndex);
                if (intercom == null)
                {
                    message = "Encounter step is missing.";
                    return true;
                }
                if (intercom.StageChange == null)
                    intercom.StageChange = new ScenarioStageChangeDefinition();
                intercom.StageChange.Id = targetId;
                message = "Created and selected next stage '" + targetId + "'.";
            }

            MarkDirty(session);
            return true;
        }

        private static bool TryHandleEndOptionItemAction(ScenarioEditorSession session, ScenarioDefinition definition, string actionId, out string message)
        {
            message = null;
            int stageIndex;
            int intercomIndex;
            int itemIndex;
            string token;

            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioStoryFocusedEditorActions.ActionEndRewardAddPrefix, CountStages(definition), out stageIndex, out intercomIndex))
                return AddEndItem(session, definition, stageIndex, intercomIndex, true, out message);
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioStoryFocusedEditorActions.ActionTradeOverridePrefix, CountStages(definition), out stageIndex, out intercomIndex))
                return ToggleTradeOverride(session, definition, stageIndex, intercomIndex, out message);
            if (ScenarioAuthoringActionParser.TryPairIndex(actionId, ScenarioStoryFocusedEditorActions.ActionTradeAddPrefix, CountStages(definition), out stageIndex, out intercomIndex))
                return AddEndItem(session, definition, stageIndex, intercomIndex, false, out message);
            if (ScenarioStoryFocusedEditorActions.TryTriple(actionId, ScenarioStoryFocusedEditorActions.ActionEndRewardDeletePrefix, out stageIndex, out intercomIndex, out itemIndex))
                return DeleteEndItem(session, definition, stageIndex, intercomIndex, itemIndex, true, out message);
            if (ScenarioStoryFocusedEditorActions.TryTriple(actionId, ScenarioStoryFocusedEditorActions.ActionTradeDeletePrefix, out stageIndex, out intercomIndex, out itemIndex))
                return DeleteEndItem(session, definition, stageIndex, intercomIndex, itemIndex, false, out message);
            if (ScenarioStoryFocusedEditorActions.TryTripleToken(actionId, ScenarioStoryFocusedEditorActions.ActionEndRewardItemPrefix, out stageIndex, out intercomIndex, out itemIndex, out token))
                return SetEndItem(session, definition, stageIndex, intercomIndex, itemIndex, true, ScenarioStoryAuthoringActions.DecodeToken(token), out message);
            if (ScenarioStoryFocusedEditorActions.TryTripleToken(actionId, ScenarioStoryFocusedEditorActions.ActionTradeItemPrefix, out stageIndex, out intercomIndex, out itemIndex, out token))
                return SetEndItem(session, definition, stageIndex, intercomIndex, itemIndex, false, ScenarioStoryAuthoringActions.DecodeToken(token), out message);
            if (ScenarioStoryFocusedEditorActions.TryTripleToken(actionId, ScenarioStoryFocusedEditorActions.ActionEndRewardQuantityPrefix, out stageIndex, out intercomIndex, out itemIndex, out token))
                return StepEndItem(session, definition, stageIndex, intercomIndex, itemIndex, true, token, out message);
            if (ScenarioStoryFocusedEditorActions.TryTripleToken(actionId, ScenarioStoryFocusedEditorActions.ActionTradeQuantityPrefix, out stageIndex, out intercomIndex, out itemIndex, out token))
                return StepEndItem(session, definition, stageIndex, intercomIndex, itemIndex, false, token, out message);

            return false;
        }

        private static bool AddEndItem(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, int intercomIndex, bool reward, out string message)
        {
            ScenarioEncounterEndOptionsDefinition end = GetEndOptions(definition, stageIndex, intercomIndex);
            if (end == null)
            {
                message = "Encounter outcome is missing.";
                return true;
            }

            List<ItemEntry> items = reward ? end.RewardItems : end.TradeItems;
            items.Add(new ItemEntry { ItemId = ScenarioInventoryItemCatalog.DefaultItemId(), Quantity = 1 });
            MarkDirty(session);
            message = reward ? "Added outcome reward item." : "Added trade item.";
            return true;
        }

        private static bool ToggleTradeOverride(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, int intercomIndex, out string message)
        {
            ScenarioEncounterEndOptionsDefinition end = GetEndOptions(definition, stageIndex, intercomIndex);
            if (end == null)
            {
                message = "Encounter outcome is missing.";
                return true;
            }

            end.OverrideTradeItems = !end.OverrideTradeItems;
            MarkDirty(session);
            message = end.OverrideTradeItems ? "Trade override enabled." : "Trade override disabled.";
            return true;
        }

        private static bool DeleteEndItem(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, int intercomIndex, int itemIndex, bool reward, out string message)
        {
            List<ItemEntry> items = GetEndItems(definition, stageIndex, intercomIndex, reward);
            if (items == null || itemIndex < 0 || itemIndex >= items.Count)
            {
                message = "Outcome item is missing.";
                return true;
            }

            items.RemoveAt(itemIndex);
            MarkDirty(session);
            message = reward ? "Removed outcome reward item." : "Removed trade item.";
            return true;
        }

        private static bool SetEndItem(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, int intercomIndex, int itemIndex, bool reward, string itemId, out string message)
        {
            List<ItemEntry> items = GetEndItems(definition, stageIndex, intercomIndex, reward);
            if (items == null || itemIndex < 0 || itemIndex >= items.Count)
            {
                message = "Outcome item is missing.";
                return true;
            }

            items[itemIndex].ItemId = itemId;
            MarkDirty(session);
            message = reward ? "Updated outcome reward item." : "Updated trade item.";
            return true;
        }

        private static bool StepEndItem(ScenarioEditorSession session, ScenarioDefinition definition, int stageIndex, int intercomIndex, int itemIndex, bool reward, string token, out string message)
        {
            int delta;
            if (!int.TryParse(token, out delta))
            {
                message = "Invalid item quantity step.";
                return true;
            }

            List<ItemEntry> items = GetEndItems(definition, stageIndex, intercomIndex, reward);
            if (items == null || itemIndex < 0 || itemIndex >= items.Count)
            {
                message = "Outcome item is missing.";
                return true;
            }

            items[itemIndex].Quantity = Math.Max(1, items[itemIndex].Quantity + delta);
            MarkDirty(session);
            message = "Updated quantity to " + items[itemIndex].Quantity.ToString(CultureInfo.InvariantCulture) + ".";
            return true;
        }

        private static void OpenFocusedStage(ScenarioAuthoringState state, int index, bool isNew)
        {
            state.FocusedEditorKind = ScenarioStoryFocusedEditorActions.FocusedEditorKind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
            state.TimelineSelectedEntryId = ScenarioStoryFocusedEditorActions.FocusedEntryId(index);
        }

        private static bool IsStoryFocused(ScenarioAuthoringState state)
        {
            return state != null
                && string.Equals(state.FocusedEditorKind, ScenarioStoryFocusedEditorActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase);
        }

        private static int CountStages(ScenarioDefinition definition)
        {
            return definition != null && definition.ScenarioFlow != null && definition.ScenarioFlow.Stages != null
                ? definition.ScenarioFlow.Stages.Count
                : 0;
        }

        private static ScenarioFlowStageDefinition GetStage(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.ScenarioFlow != null
                && definition.ScenarioFlow.Stages != null
                && index >= 0
                && index < definition.ScenarioFlow.Stages.Count
                    ? definition.ScenarioFlow.Stages[index]
                    : null;
        }

        private static ScenarioIntercomStageDefinition EnsureFirstIntercom(ScenarioFlowStageDefinition stage)
        {
            if (stage.IntercomStages.Count == 0)
                stage.IntercomStages.Add(new ScenarioIntercomStageDefinition { Id = "step_1" });
            return stage.IntercomStages[0];
        }

        private static ScenarioIntercomStageDefinition GetIntercom(ScenarioDefinition definition, int stageIndex, int intercomIndex)
        {
            ScenarioFlowStageDefinition stage = GetStage(definition, stageIndex);
            return stage != null
                && stage.IntercomStages != null
                && intercomIndex >= 0
                && intercomIndex < stage.IntercomStages.Count
                    ? stage.IntercomStages[intercomIndex]
                    : null;
        }

        private static ScenarioEncounterEndOptionsDefinition GetEndOptions(ScenarioDefinition definition, int stageIndex, int intercomIndex)
        {
            ScenarioIntercomStageDefinition intercom = GetIntercom(definition, stageIndex, intercomIndex);
            if (intercom == null)
                return null;
            if (intercom.EndOptions == null)
                intercom.EndOptions = new ScenarioEncounterEndOptionsDefinition();
            return intercom.EndOptions;
        }

        private static List<ItemEntry> GetEndItems(ScenarioDefinition definition, int stageIndex, int intercomIndex, bool reward)
        {
            ScenarioEncounterEndOptionsDefinition end = GetEndOptions(definition, stageIndex, intercomIndex);
            if (end == null)
                return null;
            return reward ? end.RewardItems : end.TradeItems;
        }

        private static void MarkDirty(ScenarioEditorSession session)
        {
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Triggers, ScenarioEditCategory.Triggers);
        }
    }
}
