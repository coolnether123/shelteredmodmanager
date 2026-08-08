using System;
using System.Globalization;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Tutorial;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Stages;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal sealed class ShellUxCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioAuthoringTutorialService _tutorialService;
        private readonly ScenarioEditorStateSessionService _editorStateSession;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioDraftSnapshotService _snapshotService;
        private readonly ScenarioAuthorTestChecklistService _checklistService;
        private readonly ScenarioVanillaInteractionRuntimeService _vanillaInteraction;
        private readonly ScenarioAuthoringHistoryService _historyService;

        public ShellUxCommandHandler(
            ScenarioAuthoringLayoutService layoutService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringTutorialService tutorialService,
            ScenarioEditorStateSessionService editorStateSession,
            IScenarioEditorService editorService,
            ScenarioDraftSnapshotService snapshotService,
            ScenarioAuthorTestChecklistService checklistService,
            ScenarioVanillaInteractionRuntimeService vanillaInteraction,
            ScenarioAuthoringHistoryService historyService)
        {
            _layoutService = layoutService;
            _settingsService = settingsService;
            _tutorialService = tutorialService;
            _editorStateSession = editorStateSession;
            _editorService = editorService;
            _snapshotService = snapshotService;
            _checklistService = checklistService;
            _vanillaInteraction = vanillaInteraction;
            _historyService = historyService;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is ShellUxCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            ShellUxCommand shell = command as ShellUxCommand;
            if (state == null || shell == null)
                return Result(false, false, "Shell command was unavailable.");

            string message;
            bool changed = Execute(state, shell, out message);
            return Result(true, changed, message);
        }

        private bool Execute(ScenarioAuthoringState state, ShellUxCommand command, out string message)
        {
            message = null;
            switch (command.Kind)
            {
                case ShellUxCommandKind.SelectStage: return SelectStage(state, command.Stage, command.Value, out message);
                case ShellUxCommandKind.ToggleWindow:
                    bool toggled = _layoutService.ToggleWindowVisibility(state, command.Key);
                    message = toggled ? BuildWindowStatus(state, command.Key) : null;
                    return toggled;
                case ShellUxCommandKind.CollapseWindow:
                    bool collapsed = _layoutService.ToggleWindowCollapsed(state, command.Key);
                    message = collapsed ? "Panel " + FormatWindowLabel(command.Key) + " collapsed." : null;
                    return collapsed;
                case ShellUxCommandKind.RestoreWindow:
                    bool restored = _layoutService.RestoreWindow(state, command.Key);
                    message = restored ? "Panel " + FormatWindowLabel(command.Key) + " restored." : null;
                    return restored;
                case ShellUxCommandKind.SelectInspectorTab: return SetInspectorTab(state, command.InspectorTab, out message);
                case ShellUxCommandKind.ToggleInspectorPin: return ToggleInspectorPin(state, command.Key, out message);
                case ShellUxCommandKind.ToggleSetting: return ToggleSetting(state, command.Key, out message);
                case ShellUxCommandKind.StepSetting: return StepSetting(state, command.Key, command.Delta, out message);
                case ShellUxCommandKind.SelectSetting: return SelectSetting(state, command.Key, command.Value, out message);
                case ShellUxCommandKind.ToggleShell:
                    state.ShellVisible = !state.ShellVisible;
                    message = state.ShellVisible ? "Authoring shell opened." : "Authoring shell hidden.";
                    return true;
                case ShellUxCommandKind.ShowShell:
                    if (!state.ShellVisible) { state.ShellVisible = true; message = "Authoring shell opened."; }
                    else message = "Authoring inspector already open.";
                    return true;
                case ShellUxCommandKind.ReturnFromVanilla:
                    if (_vanillaInteraction != null) _vanillaInteraction.CloseVanillaAndReturnToEditor(state); else state.ShellVisible = true;
                    message = state.StatusMessage;
                    return true;
                case ShellUxCommandKind.HideAll: _layoutService.HideAll(state); message = "Major authoring panels hidden."; return true;
                case ShellUxCommandKind.ResetLayout: _layoutService.ResetLayout(state); message = "Authoring shell layout reset."; return true;
                case ShellUxCommandKind.FocusSelection: _layoutService.FocusSelection(state); message = "Focused the shell on the current selection."; return true;
                case ShellUxCommandKind.ToggleWindowMenu:
                    state.WindowMenuOpen = !state.WindowMenuOpen;
                    message = state.WindowMenuOpen ? "Windows menu opened." : "Windows menu closed.";
                    return true;
                case ShellUxCommandKind.OpenSettings: return WindowResult(_layoutService.SetSettingsWindowOpen(state, true), "Editor settings opened.", out message);
                case ShellUxCommandKind.OpenHelp: return SetHelp(state, false, out message);
                case ShellUxCommandKind.OpenShortcuts: return SetHelp(state, true, out message);
                case ShellUxCommandKind.ShowHelpPages:
                    if (!state.HelpWindowOpen || !state.HelpShortcutsView) return false;
                    state.HelpShortcutsView = false; message = "Workshop help pages shown."; return true;
                case ShellUxCommandKind.OpenTimeline: return WindowResult(_layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Triggers, true), "Timeline opened.", out message);
                case ShellUxCommandKind.CloseSettings: return WindowResult(_layoutService.SetSettingsWindowOpen(state, false), "Editor settings closed.", out message);
                case ShellUxCommandKind.CloseHelp:
                    if (!state.HelpWindowOpen) return false;
                    state.HelpWindowOpen = false; state.HelpShortcutsView = false; message = "Workshop help closed."; return true;
                case ShellUxCommandKind.ResetSettings:
                    state.Settings = _settingsService.ResetToDefaults(); _layoutService.ResetLayout(state); message = "Editor settings reset to defaults."; return true;
                case ShellUxCommandKind.ToggleGlobalSearch:
                    state.GlobalSearchOpen = !state.GlobalSearchOpen;
                    message = state.GlobalSearchOpen ? "Search opened. Type to find commands and scenario elements." : "Search closed.";
                    return true;
                case ShellUxCommandKind.CloseGlobalSearch:
                    if (state.GlobalSearchOpen) { state.GlobalSearchOpen = false; message = "Search closed."; }
                    return true;
                case ShellUxCommandKind.OpenHelpTopic:
                    return _tutorialService != null && _tutorialService.OpenHelpTopic(state, command.Key, _layoutService, out message);
                case ShellUxCommandKind.StartTour:
                    return _tutorialService != null && _tutorialService.StartTour(state, command.Key, _layoutService, out message);
                case ShellUxCommandKind.TourNext: return _tutorialService != null && _tutorialService.StepTour(state, 1, _layoutService, out message);
                case ShellUxCommandKind.TourBack: return _tutorialService != null && _tutorialService.StepTour(state, -1, _layoutService, out message);
                case ShellUxCommandKind.TourExit: return _tutorialService != null && _tutorialService.ExitTour(state, out message);
                case ShellUxCommandKind.DismissSetup: return DismissSetup(out message);
                case ShellUxCommandKind.TutorialOpenTarget: return OpenTutorialTarget(state, out message);
                case ShellUxCommandKind.TutorialNext:
                case ShellUxCommandKind.TutorialBack:
                case ShellUxCommandKind.TutorialSkipPrompt:
                case ShellUxCommandKind.TutorialSkipCancel:
                case ShellUxCommandKind.TutorialSkip:
                case ShellUxCommandKind.TutorialReset:
                case ShellUxCommandKind.HelpPageNext:
                case ShellUxCommandKind.HelpPagePrevious:
                    return HandleTutorialAction(state, command, out message);
                case ShellUxCommandKind.SetLaunchMode:
                case ShellUxCommandKind.ToggleLaunchSelectable:
                case ShellUxCommandKind.StepLaunchValue:
                    return ApplyLaunchSetup(command, out message);
                case ShellUxCommandKind.ToggleChecklistItem: return ToggleChecklist(command.Key, out message);
                case ShellUxCommandKind.SetChecklistNote: return SetChecklistNote(command.Key, command.Value, out message);
                default: return false;
            }
        }

        private bool SelectStage(ScenarioAuthoringState state, ScenarioStageKind stage, string fixedMessage, out string message)
        {
            ScenarioStageKind previousStage = state.ActiveStage;
            ScenarioAuthoringTool previousTool = state.ActiveTool;
            bool closed = CloseFocusedEditorForPageSwitch(state, stage);
            bool changed = _layoutService.SelectStage(state, stage);
            message = changed || closed
                ? (string.IsNullOrEmpty(fixedMessage) ? BuildStageStatus(state, previousStage, previousTool) : fixedMessage) + (closed ? " Focused editor closed." : string.Empty)
                : null;
            return changed || closed;
        }

        private bool ToggleInspectorPin(ScenarioAuthoringState state, string token, out string message)
        {
            message = null;
            if (state.Settings == null || string.IsNullOrEmpty(token)) return false;
            string settingId = "inspector.pin." + token;
            bool current = state.Settings.GetBool(settingId, true);
            state.Settings.Set(settingId, current ? "false" : "true");
            _settingsService.Save(state.Settings);
            message = "Inspector fact " + (current ? "unpinned." : "pinned.");
            return true;
        }

        private bool ToggleSetting(ScenarioAuthoringState state, string settingId, out string message)
        {
            message = null;
            ScenarioAuthoringSettingDefinition definition = _settingsService.FindDefinition(settingId);
            if (definition == null || definition.Kind != ScenarioAuthoringSettingKind.Toggle || state.Settings == null) return false;
            bool current = state.Settings.GetBool(settingId, string.Equals(definition.DefaultValue, "true", StringComparison.OrdinalIgnoreCase));
            state.Settings.Set(settingId, current ? "false" : "true");
            _settingsService.Save(state.Settings); _layoutService.PersistIfEnabled(state);
            message = definition.Label + " set to " + (!current ? "On" : "Off") + ".";
            return true;
        }

        private bool StepSetting(ScenarioAuthoringState state, string settingId, int direction, out string message)
        {
            message = null;
            ScenarioAuthoringSettingDefinition definition = _settingsService.FindDefinition(settingId);
            if (definition == null || state.Settings == null) return false;
            if (definition.Kind == ScenarioAuthoringSettingKind.Integer)
            {
                int current = state.Settings.GetInt(settingId, (int)definition.MinValue);
                int next = Mathf.Clamp(current + Math.Sign(direction) * (int)Mathf.Max(1f, definition.Step), (int)definition.MinValue, (int)definition.MaxValue);
                if (next == current) return false;
                state.Settings.Set(settingId, next.ToString(CultureInfo.InvariantCulture));
            }
            else if (definition.Kind == ScenarioAuthoringSettingKind.Float)
            {
                float current = state.Settings.GetFloat(settingId, definition.MinValue);
                float next = Mathf.Clamp(current + Math.Sign(direction) * definition.Step, definition.MinValue, definition.MaxValue);
                if (Math.Abs(next - current) <= 0.0001f) return false;
                state.Settings.Set(settingId, next.ToString("0.00", CultureInfo.InvariantCulture));
            }
            else return false;
            _settingsService.Save(state.Settings); _layoutService.PersistIfEnabled(state); message = definition.Label + " updated."; return true;
        }

        private bool SelectSetting(ScenarioAuthoringState state, string settingId, string selectedValue, out string message)
        {
            message = null;
            ScenarioAuthoringSettingDefinition definition = _settingsService.FindDefinition(settingId);
            if (definition == null || definition.Kind != ScenarioAuthoringSettingKind.Choice || state.Settings == null) return false;
            bool allowed = false;
            for (int i = 0; definition.ChoiceValues != null && i < definition.ChoiceValues.Length; i++)
                if (string.Equals(definition.ChoiceValues[i], selectedValue, StringComparison.OrdinalIgnoreCase)) { allowed = true; selectedValue = definition.ChoiceValues[i]; break; }
            if (!allowed || string.Equals(state.Settings.Get(settingId, definition.DefaultValue), selectedValue, StringComparison.OrdinalIgnoreCase)) return false;
            state.Settings.Set(settingId, selectedValue); _settingsService.Save(state.Settings); _layoutService.PersistIfEnabled(state);
            message = definition.Label + " set to " + selectedValue + "."; return true;
        }

        private bool DismissSetup(out string message)
        {
            message = null;
            ScenarioEditorState editorState = _editorStateSession != null ? _editorStateSession.Current : null;
            if (editorState == null) return false;
            editorState.ChecklistDismissed = true; _editorStateSession.SaveCurrent(); message = "Scenario setup checklist dismissed."; return true;
        }

        private bool OpenTutorialTarget(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (_tutorialService == null) return false;
            TutorialStep step = _tutorialService.GetActiveStep(state);
            if (step == null) return false;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            if (_tutorialService.IsStepSatisfied(state, session, step)) return _tutorialService.HandleAction(state, ShellUxCommandKind.TutorialNext, out message);
            if (string.Equals(step.TargetActionId, "playtest", StringComparison.Ordinal)) { message = "Use Playtest to continue."; return true; }
            return _tutorialService.OpenStepTarget(state, step, _layoutService, out message);
        }

        private bool HandleTutorialAction(ScenarioAuthoringState state, ShellUxCommand command, out string message)
        {
            message = null;
            if (_tutorialService == null) return false;
            if (state.HelpWindowOpen && BlocksWhileHelpOpen(command.Kind)) { message = "Close help before continuing the tutorial."; return true; }
            return _tutorialService.HandleAction(state, command.Kind, out message);
        }

        private static bool BlocksWhileHelpOpen(ShellUxCommandKind kind)
        {
            return kind == ShellUxCommandKind.TutorialNext
                || kind == ShellUxCommandKind.TutorialBack
                || kind == ShellUxCommandKind.TutorialSkipPrompt
                || kind == ShellUxCommandKind.TutorialSkipCancel
                || kind == ShellUxCommandKind.TutorialSkip;
        }

        private bool ApplyLaunchSetup(ShellUxCommand command, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null) { message = "No active scenario definition."; return false; }
            if (definition.LaunchSetup == null) definition.LaunchSetup = ScenarioLaunchSetupDefinition.CreateDefault();
            ScenarioDefinition before = ScenarioEditorDefinitionCloner.Clone(definition);
            ScenarioDifficultyCategoryDefinition category;
            switch (command.Kind)
            {
                case ShellUxCommandKind.SetLaunchMode:
                    if (definition.LaunchSetup.Mode == command.LaunchMode) { message = "Play experience is already " + command.LaunchMode + "."; return false; }
                    definition.LaunchSetup.Mode = command.LaunchMode; message = "Play experience set to " + command.LaunchMode + "."; break;
                case ShellUxCommandKind.ToggleLaunchSelectable:
                    category = GetOrCreate(definition.LaunchSetup, command.Key);
                    if (category == null) { message = "Unknown difficulty category."; return false; }
                    category.PlayerSelectable = !category.PlayerSelectable;
                    message = category.PlayerSelectable ? "Player can change " + category.Id + "." : "Scenario locks " + category.Id + "."; break;
                default:
                    category = GetOrCreate(definition.LaunchSetup, command.Key);
                    if (category == null) { message = "Unknown difficulty category."; return false; }
                    int maximum = category.Id == ScenarioDifficultyCategoryIds.MapSize ? 2 : category.Id == ScenarioDifficultyCategoryIds.Fog ? 1 : 3;
                    int next = Math.Max(0, Math.Min(maximum, category.AuthoredValue + command.Delta));
                    if (next == category.AuthoredValue) { message = "Authored " + category.Id + " value is already at its limit."; return false; }
                    category.AuthoredValue = next; message = "Updated authored " + category.Id + " value."; break;
            }
            if (_historyService != null)
                _historyService.RecordAuthoringChange(before, "Change play experience", ScenarioDirtySection.LaunchSetup, ScenarioEditCategory.LaunchSetup);
            session.MarkDraftChanged(ScenarioDirtySection.LaunchSetup, ScenarioEditCategory.LaunchSetup);
            string ignored; if (_snapshotService != null) _snapshotService.TryAutosaveCurrent("play experience change", out ignored);
            return true;
        }

        private bool ToggleChecklist(string id, out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            bool changed = _checklistService != null && _checklistService.ToggleManual(session, id);
            message = changed ? "Author test checklist updated." : "Checklist item could not be updated."; return changed;
        }

        private bool SetChecklistNote(string id, string note, out string message)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            bool changed = _checklistService != null && _checklistService.SetNote(session, id, note);
            message = changed ? "Checklist note updated." : "Checklist note was unchanged."; return changed;
        }

        private static ScenarioDifficultyCategoryDefinition GetOrCreate(ScenarioLaunchSetupDefinition setup, string id)
        {
            if (!ScenarioDifficultyCategoryIds.IsKnown(id)) return null;
            for (int i = 0; setup.Categories != null && i < setup.Categories.Count; i++)
                if (setup.Categories[i] != null && string.Equals(setup.Categories[i].Id, id, StringComparison.OrdinalIgnoreCase)) return setup.Categories[i];
            ScenarioDifficultyCategoryDefinition category = new ScenarioDifficultyCategoryDefinition { Id = id, AuthoredValue = id == ScenarioDifficultyCategoryIds.MapSize || id == ScenarioDifficultyCategoryIds.Fog ? 0 : 1, PlayerSelectable = true };
            setup.Categories.Add(category); return category;
        }

        private static bool SetHelp(ScenarioAuthoringState state, bool shortcuts, out string message)
        {
            message = null;
            if (state.HelpWindowOpen && state.HelpShortcutsView == shortcuts) return false;
            state.HelpWindowOpen = true; state.HelpShortcutsView = shortcuts;
            message = shortcuts ? "Keyboard shortcuts opened." : "Workshop help opened."; return true;
        }

        private static bool WindowResult(bool changed, string successMessage, out string message) { message = changed ? successMessage : null; return changed; }
        private static bool SetInspectorTab(ScenarioAuthoringState state, ScenarioAuthoringInspectorTab tab, out string message)
        {
            message = null; if (state.InspectorTab == tab) return false; state.InspectorTab = tab; message = "Inspector switched to " + tab + "."; return true;
        }
        private static bool CloseFocusedEditorForPageSwitch(ScenarioAuthoringState state, ScenarioStageKind stage)
        {
            if (string.IsNullOrEmpty(state.FocusedEditorKind) || ScenarioAuthoringWorkflowRules.ResolveStageKind(state) == stage) return false;
            state.TimelineSelectedEntryId = state.FocusedEditorKind + ":" + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture);
            state.FocusedEditorKind = null; state.FocusedEditorIndex = -1; state.FocusedEditorIsNew = false; state.SurvivorColorPickerChannel = null; state.SurvivorColorPickerRequestId = 0; return true;
        }
        private static string BuildStageStatus(ScenarioAuthoringState state, ScenarioStageKind previousStage, ScenarioAuthoringTool previousTool)
        {
            string stage = ScenarioAuthoringWorkflowLabels.GetStageLabel(state.ActiveStage, false); string tool = ScenarioAuthoringWorkflowLabels.GetToolLabel(state.ActiveTool);
            if (state.ActiveTool != previousTool) return stage + " workspace active. Tool changed to " + tool + ".";
            return state.ActiveStage == previousStage ? stage + " workspace already active." : stage + " workspace active.";
        }
        private static string BuildWindowStatus(ScenarioAuthoringState state, string id)
        {
            bool open = false;
            for (int i = 0; state.WindowStates != null && i < state.WindowStates.Count; i++)
                if (state.WindowStates[i] != null && string.Equals(state.WindowStates[i].Id, id, StringComparison.OrdinalIgnoreCase)) { open = state.WindowStates[i].Visible && !state.WindowStates[i].Collapsed; break; }
            return "Panel " + FormatWindowLabel(id) + (open ? " opened." : " hidden.");
        }
        private static string FormatWindowLabel(string id) { return string.IsNullOrEmpty(id) ? "window" : "'" + id.Replace('_', ' ') + "'"; }
        private static ScenarioCommandDispatchResult Result(bool handled, bool changed, string message)
        {
            return new ScenarioCommandDispatchResult { Handled = handled, Changed = changed, Message = message };
        }
    }
}
