using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal sealed class TypedGameplayScheduleCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioGameplayScheduleAuthoringService _service;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;

        internal TypedGameplayScheduleCommandHandler(ScenarioGameplayScheduleAuthoringService service, IScenarioEditorService editorService, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            _service = service;
            _editorService = editorService;
            _rendererInteraction = rendererInteraction;
        }

        public bool CanHandle(ScenarioAuthoringCommand command) { return command is GameplayScheduleCommand; }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            GameplayScheduleCommand gameplay = command as GameplayScheduleCommand;
            string reason = null;
            if (gameplay == null || !gameplay.ValidateStructure(out reason)) return Result(false, reason ?? "Gameplay schedule command is invalid.");
            if (_service == null || _editorService == null) return Result(false, "Gameplay schedule editing is unavailable.");

            if (gameplay.Kind == GameplayScheduleCommandKind.AddStartingItemAndPick)
                return AddInventoryAndPick(state, GameplayScheduleCommands.AddStartingItem(), ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker, true);
            if (gameplay.Kind == GameplayScheduleCommandKind.AddTimedItemAndPick)
                return AddInventoryAndPick(state, GameplayScheduleCommands.AddTimedItem(gameplay.Remove), ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, false);
            if (gameplay.Kind == GameplayScheduleCommandKind.OpenStartingPicker)
                return OpenPicker(state, ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker, gameplay.Index, "Opened searchable starting item picker.");
            if (gameplay.Kind == GameplayScheduleCommandKind.OpenTimedPicker)
                return OpenPicker(state, ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, gameplay.Index, "Opened searchable timed item picker.");
            if (gameplay.Kind == GameplayScheduleCommandKind.PreviewSuppliesPreset)
            {
                ScenarioSuppliesWorkspaceActions.SelectPresetDocument(gameplay.Index, _rendererInteraction);
                ClearPresetModal(state);
                return Result(true, "Opened the starter loadout in Supplies.");
            }
            if (gameplay.Kind == GameplayScheduleCommandKind.OpenFutureSurvivor)
            {
                ScenarioEditorSession current = _editorService.CurrentSession;
                ScenarioCastWorkspaceActions.SelectFutureDocument(current != null ? current.WorkingDefinition : null, gameplay.Index, _rendererInteraction);
                return Result(true, "Opened future survivor editor.");
            }
            if (gameplay.Kind == GameplayScheduleCommandKind.OpenWeatherEditor)
            {
                SetFocusedEditor(state, "weather", gameplay.Index, false);
                return Result(true, "Opened weather editor.");
            }
            if (gameplay.Kind == GameplayScheduleCommandKind.OpenQuestDocument)
            {
                ScenarioEditorSession current = _editorService.CurrentSession;
                ScenarioStoryFocusedEditorActions.SelectQuestDocument(current != null ? current.WorkingDefinition : null, gameplay.Index, _rendererInteraction);
                return Result(true, "Opened quest editor.");
            }

            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            QuestDefinition selectedQuest = ResolveSelectedQuest(definition);
            FutureSurvivorDefinition selectedFuture = ResolveSelectedFutureSurvivor(definition);
            string message;
            bool changed = _service.TryHandleCommand(session, gameplay, out message);
            if (changed)
            {
                FocusCreatedEditor(state, definition, gameplay);
                CloseItemPicker(state, gameplay.Kind);
                ReconcileQuestSelection(definition, gameplay, selectedQuest);
                ReconcileFutureSelection(definition, gameplay.Kind, selectedFuture);
            }
            return Result(changed, message);
        }

        private ScenarioCommandDispatchResult AddInventoryAndPick(ScenarioAuthoringState state, GameplayScheduleCommand add, string pickerKind, bool starting)
        {
            string message;
            bool changed = _service.TryHandleCommand(_editorService.CurrentSession, add, out message);
            if (!changed) return Result(false, message);
            ScenarioEditorSession session = _editorService.CurrentSession;
            StartingInventoryDefinition inventory = session != null && session.WorkingDefinition != null ? session.WorkingDefinition.StartingInventory : null;
            int index = inventory == null ? -1 : (starting ? inventory.Items.Count - 1 : inventory.ScheduledChanges.Count - 1);
            SetFocusedEditor(state, pickerKind, index, true);
            return Result(true, message);
        }

        private static ScenarioCommandDispatchResult OpenPicker(ScenarioAuthoringState state, string kind, int index, string message)
        {
            SetFocusedEditor(state, kind, index, false);
            return Result(true, message);
        }

        private static void ClearPresetModal(ScenarioAuthoringState state)
        {
            if (state != null && string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindSuppliesPreset, StringComparison.OrdinalIgnoreCase))
            {
                state.FocusedEditorKind = null;
                state.FocusedEditorIndex = -1;
                state.FocusedEditorIsNew = false;
            }
        }

        private static void CloseItemPicker(ScenarioAuthoringState state, GameplayScheduleCommandKind kind)
        {
            if (state == null || (kind != GameplayScheduleCommandKind.SetStartingItem && kind != GameplayScheduleCommandKind.SetTimedItem)) return;
            state.FocusedEditorKind = null;
            state.FocusedEditorIndex = -1;
            state.FocusedEditorIsNew = false;
        }

        private static void FocusCreatedEditor(ScenarioAuthoringState state, ScenarioDefinition definition, GameplayScheduleCommand command)
        {
            if (state == null || definition == null) return;
            if (command.Kind == GameplayScheduleCommandKind.AddWeather && definition.TriggersAndEvents != null)
                SetFocusedEditor(state, "weather", definition.TriggersAndEvents.WeatherEvents.Count - 1, true);
        }

        private FutureSurvivorDefinition ResolveSelectedFutureSurvivor(ScenarioDefinition definition)
        {
            string selected = _rendererInteraction.GetWorkspaceSelection(ScenarioCastWorkspaceActions.WorkspaceId, ScenarioCastWorkspaceActions.SubtabId);
            int index;
            return ScenarioCastWorkspaceActions.TryResolveFutureEntity(definition, selected, out index)
                && definition != null && definition.FamilySetup != null && index >= 0 && index < definition.FamilySetup.FutureSurvivors.Count
                    ? definition.FamilySetup.FutureSurvivors[index] : null;
        }

        private QuestDefinition ResolveSelectedQuest(ScenarioDefinition definition)
        {
            string selected = _rendererInteraction.GetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId);
            int index;
            return ScenarioStoryFocusedEditorActions.TryResolveQuestEntity(definition, selected, out index)
                && definition != null && definition.Quests != null ? definition.Quests.Quests[index] : null;
        }

        private void ReconcileFutureSelection(ScenarioDefinition definition, GameplayScheduleCommandKind kind, FutureSurvivorDefinition selected)
        {
            if (definition == null || definition.FamilySetup == null) return;
            if (kind == GameplayScheduleCommandKind.AddFutureSurvivor)
            {
                ScenarioCastWorkspaceActions.SelectFutureDocument(definition, definition.FamilySetup.FutureSurvivors.Count - 1, _rendererInteraction);
                return;
            }
            if (kind != GameplayScheduleCommandKind.RemoveFutureSurvivor || selected == null) return;
            for (int i = 0; i < definition.FamilySetup.FutureSurvivors.Count; i++)
                if (object.ReferenceEquals(definition.FamilySetup.FutureSurvivors[i], selected)) { ScenarioCastWorkspaceActions.SelectFutureDocument(definition, i, _rendererInteraction); return; }
            ScenarioCastWorkspaceActions.SelectOverview(_rendererInteraction);
        }

        private void ReconcileQuestSelection(ScenarioDefinition definition, GameplayScheduleCommand command, QuestDefinition selected)
        {
            if (definition == null || definition.Quests == null) return;
            List<QuestDefinition> quests = definition.Quests.Quests;
            if (command.Kind == GameplayScheduleCommandKind.AddQuest || command.Kind == GameplayScheduleCommandKind.AddCatalogQuest)
            {
                if (quests.Count > 0) ScenarioStoryFocusedEditorActions.SelectQuestDocument(definition, quests.Count - 1, _rendererInteraction);
                return;
            }
            if (command.Kind == GameplayScheduleCommandKind.DuplicateQuest && command.Index + 1 < quests.Count)
            {
                ScenarioStoryFocusedEditorActions.SelectQuestDocument(definition, command.Index + 1, _rendererInteraction);
                return;
            }
            if (command.Kind != GameplayScheduleCommandKind.DeleteQuest || selected == null) return;
            for (int i = 0; i < quests.Count; i++)
                if (object.ReferenceEquals(quests[i], selected)) { ScenarioStoryFocusedEditorActions.SelectQuestDocument(definition, i, _rendererInteraction); return; }
            _rendererInteraction.SetWorkspaceSubtab(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId);
            _rendererInteraction.SetWorkspaceSelection(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, null);
            _rendererInteraction.SetWorkspaceNarrowPane(ScenarioStoryFocusedEditorActions.WorkspaceId, ScenarioStoryFocusedEditorActions.QuestPopupsSubtabId, true);
        }

        private static void SetFocusedEditor(ScenarioAuthoringState state, string kind, int index, bool isNew)
        {
            if (state == null || index < 0) return;
            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
            state.TimelineSelectedEntryId = kind + ":" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = true, Changed = changed, Message = message };
        }
    }
}
