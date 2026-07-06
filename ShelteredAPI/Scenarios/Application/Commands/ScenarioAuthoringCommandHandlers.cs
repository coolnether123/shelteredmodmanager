using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Application.Commands{
    internal sealed class SpriteCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSpriteSwapAuthoringService _service;
        private readonly ScenarioSelectionScopeService _scopeService;
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public SpriteCommandHandler(
            ScenarioSpriteSwapAuthoringService service,
            ScenarioSelectionScopeService scopeService,
            ScenarioAuthoringLayoutService layoutService)
        {
            _service = service;
            _scopeService = scopeService;
            _layoutService = layoutService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_service == null || string.IsNullOrEmpty(actionId))
                return false;

            if (!actionId.StartsWith("sprite_swap.", StringComparison.Ordinal)
                && !string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryUndo, StringComparison.Ordinal)
                && !string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryRedo, StringComparison.Ordinal))
            {
                return false;
            }

            if (RequiresScopedTarget(actionId) && !_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
            {
                handled = true;
                return true;
            }

            bool changed = _service.TryHandleAction(state, actionId, out handled, out message);
            if (handled && IsPixelEditorTerminalAction(actionId) && _service.GetCustomEditorModel(state) == null && _layoutService != null)
                changed |= _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.PixelEditor, false);
            return changed;
        }

        private static bool RequiresScopedTarget(string actionId)
        {
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryUndo, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionHistoryRedo, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, StringComparison.Ordinal))
                return false;

            return actionId.StartsWith("sprite_swap.", StringComparison.Ordinal);
        }

        private static bool IsPixelEditorTerminalAction(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapClear, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, StringComparison.Ordinal);
        }
    }

    internal sealed class SceneSpriteCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSceneSpritePlacementAuthoringService _service;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly ScenarioSelectionScopeService _scopeService;

        public SceneSpriteCommandHandler(
            ScenarioSceneSpritePlacementAuthoringService service,
            ScenarioBuildPlacementAuthoringService buildPlacement,
            ScenarioSelectionScopeService scopeService)
        {
            _service = service;
            _buildPlacement = buildPlacement;
            _scopeService = scopeService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_service == null || string.IsNullOrEmpty(actionId) || !actionId.StartsWith("scene_sprite.", StringComparison.Ordinal))
                return false;

            if (state != null
                && state.SelectedTarget != null
                && !_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
            {
                handled = true;
                return true;
            }

            bool changed = _service.TryHandleAction(state, actionId, out handled, out message);
            if (changed && handled && IsSceneSpritePlacementStartAction(actionId) && _service.HasActivePlacement && _buildPlacement != null)
                _buildPlacement.Reset();

            return changed;
        }

        private static bool IsSceneSpritePlacementStartAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId)
                && actionId.StartsWith(ScenarioAuthoringActionIds.ActionSceneSpritePlacementApplyPrefix, StringComparison.Ordinal);
        }
    }

    internal sealed class BuildCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioBuildPlacementAuthoringService _service;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacement;

        public BuildCommandHandler(
            ScenarioBuildPlacementAuthoringService service,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement)
        {
            _service = service;
            _sceneSpritePlacement = sceneSpritePlacement;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_service == null || string.IsNullOrEmpty(actionId) || !actionId.StartsWith("build.", StringComparison.Ordinal))
                return false;

            bool changed = _service.TryHandleAction(state, actionId, out handled, out message);
            if (changed && handled && IsBuildPlacementStartAction(actionId) && _service.HasActivePlacement && _sceneSpritePlacement != null)
                _sceneSpritePlacement.Reset();

            return changed;
        }

        private static bool IsBuildPlacementStartAction(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureRoom, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureLadder, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureLight, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix, StringComparison.Ordinal));
        }
    }

    internal sealed class ShellCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioAuthoringSettingsService _settingsService;

        public ShellCommandHandler(ScenarioAuthoringLayoutService layoutService, ScenarioAuthoringSettingsService settingsService)
        {
            _layoutService = layoutService;
            _settingsService = settingsService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (!IsShellAction(actionId))
                return false;

            handled = true;
            if (TryHandlePrefixedAction(state, actionId))
            {
                message = state.StatusMessage;
                return true;
            }

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionShellTabShelter:
                case ScenarioAuthoringActionIds.ActionShellTabBuild:
                    return SetStage(state, ScenarioStageKind.BunkerInside, out message, "Bunker workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabSurvivors:
                    return SetStage(state, ScenarioStageKind.People, out message, "People workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabStockpile:
                    return SetStage(state, ScenarioStageKind.InventoryStorage, out message, "Inventory workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabTriggers:
                    return SetStage(state, ScenarioStageKind.Events, out message, "Events workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabQuests:
                    return SetStage(state, ScenarioStageKind.Quests, out message, "Quests workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabArt:
                    return SetStage(state, ScenarioStageKind.BunkerInside, out message, "Asset authoring active.");
                case ScenarioAuthoringActionIds.ActionShellTabMap:
                    return SetStage(state, ScenarioStageKind.Map, out message, "Map workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabTest:
                    return SetStage(state, ScenarioStageKind.Test, out message, "Test workspace active.");
                case ScenarioAuthoringActionIds.ActionShellTabPublish:
                    return SetStage(state, ScenarioStageKind.Publish, out message, "Publish workspace active.");
                case ScenarioAuthoringActionIds.ActionShellToggle:
                    state.ShellVisible = !state.ShellVisible;
                    message = state.ShellVisible ? "Authoring shell opened." : "Authoring shell hidden.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellShow:
                    if (!state.ShellVisible)
                    {
                        state.ShellVisible = true;
                        message = "Authoring shell opened.";
                    }
                    else
                    {
                        message = "Authoring inspector already open.";
                    }
                    return true;
                case ScenarioAuthoringActionIds.ActionShellHideAll:
                case ScenarioAuthoringActionIds.ActionShellMinimalMode:
                    _layoutService.HideAll(state);
                    message = "Major authoring panels hidden.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellResetLayout:
                    _layoutService.ResetLayout(state);
                    message = "Authoring shell layout reset.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellFocusSelection:
                    _layoutService.FocusSelection(state);
                    message = "Focused the shell on the current selection.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellToggleWindowMenu:
                    state.WindowMenuOpen = !state.WindowMenuOpen;
                    message = state.WindowMenuOpen ? "Windows menu opened." : "Windows menu closed.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellOpenSettings:
                    if (!_layoutService.SetSettingsWindowOpen(state, true))
                        return false;
                    message = "Editor settings opened.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellOpenHelp:
                    if (state.HelpWindowOpen)
                        return false;
                    state.HelpWindowOpen = true;
                    message = "Workshop help opened.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellOpenTimeline:
                case ScenarioAuthoringActionIds.ActionShellOpenCalendar:
                    if (!_layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Triggers, true))
                        return false;
                    message = "Timeline opened.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellCloseSettings:
                    if (!_layoutService.SetSettingsWindowOpen(state, false))
                        return false;
                    message = "Editor settings closed.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellCloseHelp:
                    if (!state.HelpWindowOpen)
                        return false;
                    state.HelpWindowOpen = false;
                    message = "Workshop help closed.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellSettingsReset:
                    state.Settings = _settingsService.ResetToDefaults();
                    _layoutService.ResetLayout(state);
                    message = "Editor settings reset to defaults.";
                    return true;
            }

            handled = false;
            return false;
        }

        private static bool IsShellAction(string actionId)
        {
            return actionId.StartsWith(ScenarioAuthoringActionIds.ActionStageSelectPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowRestorePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionInspectorTabPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionInspectorPinTogglePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingTogglePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix, StringComparison.Ordinal)
                || actionId.StartsWith("shell.", StringComparison.Ordinal);
        }

        private bool TryHandlePrefixedAction(ScenarioAuthoringState state, string actionId)
        {
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal))
            {
                string windowId = actionId.Substring(ScenarioAuthoringActionIds.ActionWindowTogglePrefix.Length);
                bool toggled = _layoutService.ToggleWindowVisibility(state, windowId);
                if (toggled)
                    state.StatusMessage = BuildWindowStatus(state, windowId);
                return toggled;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionStageSelectPrefix, StringComparison.Ordinal))
            {
                string token = actionId.Substring(ScenarioAuthoringActionIds.ActionStageSelectPrefix.Length);
                ScenarioStageKind stageKind;
                if (!TryParseStageKind(token, out stageKind))
                    return false;

                ScenarioStageKind previousStage = state.ActiveStage;
                ScenarioAuthoringTool previousTool = state.ActiveTool;
                bool changed = _layoutService.SelectStage(state, stageKind);
                if (changed)
                    state.StatusMessage = BuildStageStatus(state, previousStage, previousTool);
                return changed;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal))
            {
                string windowId = actionId.Substring(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix.Length);
                bool toggled = _layoutService.ToggleWindowCollapsed(state, windowId);
                if (toggled)
                    state.StatusMessage = "Panel " + FormatWindowLabel(windowId) + " collapsed.";
                return toggled;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowRestorePrefix, StringComparison.Ordinal))
            {
                string windowId = actionId.Substring(ScenarioAuthoringActionIds.ActionWindowRestorePrefix.Length);
                bool restored = _layoutService.RestoreWindow(state, windowId);
                if (restored)
                    state.StatusMessage = "Panel " + FormatWindowLabel(windowId) + " restored.";
                return restored;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionInspectorTabPrefix, StringComparison.Ordinal))
                return SetInspectorTab(state, actionId.Substring(ScenarioAuthoringActionIds.ActionInspectorTabPrefix.Length));

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionInspectorPinTogglePrefix, StringComparison.Ordinal))
                return ToggleInspectorPin(state, actionId.Substring(ScenarioAuthoringActionIds.ActionInspectorPinTogglePrefix.Length));

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingTogglePrefix, StringComparison.Ordinal))
                return ToggleSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingTogglePrefix.Length));

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix, StringComparison.Ordinal))
                return StepSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix.Length), +1f);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix, StringComparison.Ordinal))
                return StepSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix.Length), -1f);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingSelectPrefix, StringComparison.Ordinal))
                return SelectSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingSelectPrefix.Length));

            return false;
        }

        private bool ToggleInspectorPin(ScenarioAuthoringState state, string token)
        {
            if (state == null || state.Settings == null || string.IsNullOrEmpty(token))
                return false;

            string settingId = "inspector.pin." + token;
            bool current = state.Settings.GetBool(settingId, true);
            state.Settings.Set(settingId, current ? "false" : "true");
            _settingsService.Save(state.Settings);
            state.StatusMessage = "Inspector fact " + (current ? "unpinned." : "pinned.");
            return true;
        }

        private bool SetStage(ScenarioAuthoringState state, ScenarioStageKind stageKind, out string message, string statusMessage)
        {
            message = null;
            bool changed = _layoutService.SelectStage(state, stageKind);
            if (changed)
                message = statusMessage;
            return changed;
        }

        private static string BuildStageStatus(
            ScenarioAuthoringState state,
            ScenarioStageKind previousStage,
            ScenarioAuthoringTool previousTool)
        {
            string stageLabel = ScenarioAuthoringWorkflowLabels.GetStageLabel(state.ActiveStage, false);
            string toolLabel = ScenarioAuthoringWorkflowLabels.GetToolLabel(state.ActiveTool);
            if (state.ActiveTool != previousTool)
                return stageLabel + " workspace active. Tool changed to " + toolLabel + ".";
            if (state.ActiveStage == previousStage)
                return stageLabel + " workspace already active.";
            return stageLabel + " workspace active.";
        }

        private static string BuildWindowStatus(ScenarioAuthoringState state, string windowId)
        {
            string label = FormatWindowLabel(windowId);
            bool open = false;
            for (int i = 0; state != null && state.WindowStates != null && i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (window != null && string.Equals(window.Id, windowId, StringComparison.OrdinalIgnoreCase))
                {
                    open = window.Visible && !window.Collapsed;
                    break;
                }
            }

            return "Panel " + label + (open ? " opened." : " hidden.");
        }

        private static string FormatWindowLabel(string windowId)
        {
            if (string.IsNullOrEmpty(windowId))
                return "window";

            return "'" + windowId.Replace('_', ' ') + "'";
        }

        private static bool TryParseStageKind(string token, out ScenarioStageKind stageKind)
        {
            stageKind = ScenarioStageKind.None;
            if (string.IsNullOrEmpty(token))
                return false;

            try
            {
                object parsed = Enum.Parse(typeof(ScenarioStageKind), token, true);
                if (parsed == null || !Enum.IsDefined(typeof(ScenarioStageKind), parsed))
                    return false;

                stageKind = (ScenarioStageKind)parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool SetInspectorTab(ScenarioAuthoringState state, string token)
        {
            ScenarioAuthoringInspectorTab tab;
            switch ((token ?? string.Empty).ToLowerInvariant())
            {
                case "interactions":
                    tab = ScenarioAuthoringInspectorTab.Interactions;
                    break;
                case "visuals":
                    tab = ScenarioAuthoringInspectorTab.Visuals;
                    break;
                case "runtime":
                    tab = ScenarioAuthoringInspectorTab.Runtime;
                    break;
                case "notes":
                    tab = ScenarioAuthoringInspectorTab.Notes;
                    break;
                default:
                    tab = ScenarioAuthoringInspectorTab.Properties;
                    break;
            }

            if (state.InspectorTab == tab)
                return false;

            state.InspectorTab = tab;
            state.StatusMessage = "Inspector switched to " + tab + ".";
            return true;
        }

        private bool ToggleSetting(ScenarioAuthoringState state, string settingId)
        {
            ScenarioAuthoringSettingDefinition definition = _settingsService.FindDefinition(settingId);
            if (definition == null || definition.Kind != ScenarioAuthoringSettingKind.Toggle || state.Settings == null)
                return false;

            bool current = state.Settings.GetBool(settingId, string.Equals(definition.DefaultValue, "true", StringComparison.OrdinalIgnoreCase));
            state.Settings.Set(settingId, current ? "false" : "true");
            _settingsService.Save(state.Settings);
            _layoutService.PersistIfEnabled(state);
            state.StatusMessage = definition.Label + " set to " + (!current ? "On" : "Off") + ".";
            return true;
        }

        private bool StepSetting(ScenarioAuthoringState state, string settingId, float direction)
        {
            ScenarioAuthoringSettingDefinition definition = _settingsService.FindDefinition(settingId);
            if (definition == null || state.Settings == null)
                return false;

            if (definition.Kind == ScenarioAuthoringSettingKind.Integer)
            {
                int current = state.Settings.GetInt(settingId, (int)definition.MinValue);
                int next = current + (int)Mathf.Sign(direction) * (int)Mathf.Max(1f, definition.Step);
                next = Mathf.Clamp(next, (int)definition.MinValue, (int)definition.MaxValue);
                if (next == current)
                    return false;
                state.Settings.Set(settingId, next.ToString(CultureInfo.InvariantCulture));
            }
            else if (definition.Kind == ScenarioAuthoringSettingKind.Float)
            {
                float current = state.Settings.GetFloat(settingId, definition.MinValue);
                float next = current + (Mathf.Sign(direction) * definition.Step);
                next = Mathf.Clamp(next, definition.MinValue, definition.MaxValue);
                if (Math.Abs(next - current) <= 0.0001f)
                    return false;
                state.Settings.Set(settingId, next.ToString("0.00", CultureInfo.InvariantCulture));
            }
            else
            {
                return false;
            }

            _settingsService.Save(state.Settings);
            _layoutService.PersistIfEnabled(state);
            state.StatusMessage = definition.Label + " updated.";
            return true;
        }

        private bool SelectSetting(ScenarioAuthoringState state, string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;

            int separator = token.LastIndexOf('.');
            if (separator <= 0 || separator >= token.Length - 1)
                return false;

            string settingId = token.Substring(0, separator);
            string selectedValue = token.Substring(separator + 1);
            ScenarioAuthoringSettingDefinition definition = _settingsService.FindDefinition(settingId);
            if (definition == null || definition.Kind != ScenarioAuthoringSettingKind.Choice || state.Settings == null)
                return false;

            bool allowed = false;
            for (int i = 0; definition.ChoiceValues != null && i < definition.ChoiceValues.Length; i++)
            {
                if (string.Equals(definition.ChoiceValues[i], selectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    selectedValue = definition.ChoiceValues[i];
                    break;
                }
            }

            if (!allowed)
                return false;
            if (string.Equals(state.Settings.Get(settingId, definition.DefaultValue), selectedValue, StringComparison.OrdinalIgnoreCase))
                return false;

            state.Settings.Set(settingId, selectedValue);
            _settingsService.Save(state.Settings);
            _layoutService.PersistIfEnabled(state);
            state.StatusMessage = definition.Label + " set to " + selectedValue + ".";
            return true;
        }
    }

    internal sealed class TutorialCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringTutorialService _tutorialService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public TutorialCommandHandler(
            ScenarioAuthoringTutorialService tutorialService,
            IScenarioEditorService editorService,
            ScenarioAuthoringLayoutService layoutService)
        {
            _tutorialService = tutorialService;
            _editorService = editorService;
            _layoutService = layoutService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_tutorialService == null || state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (!actionId.StartsWith("tutorial.", StringComparison.Ordinal))
                return false;

            handled = true;
            if (state.HelpWindowOpen
                && (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialOpenTarget, StringComparison.Ordinal)
                    || string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialNext, StringComparison.Ordinal)
                    || string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialSkip, StringComparison.Ordinal)))
            {
                message = "Close help before continuing the tutorial.";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTutorialOpenTarget, StringComparison.Ordinal))
                return OpenTargetOrAdvance(state, out message);

            return _tutorialService.HandleAction(state, actionId, out message);
        }

        private bool OpenTargetOrAdvance(ScenarioAuthoringState state, out string message)
        {
            message = null;
            TutorialStep step = _tutorialService.GetActiveStep(state);
            if (step == null)
                return false;

            ScenarioEditorSession editorSession = _editorService != null ? _editorService.CurrentSession : null;
            if (_tutorialService.IsStepSatisfied(state, editorSession, step))
                return _tutorialService.HandleAction(state, ScenarioAuthoringActionIds.ActionTutorialNext, out message);

            if (string.Equals(step.TargetActionId, "playtest", StringComparison.Ordinal))
            {
                message = "Use Playtest to continue.";
                return true;
            }

            return _tutorialService.OpenStepTarget(state, step, _layoutService, out message);
        }
    }

    internal sealed class TimelineCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private readonly ScenarioTimelineNavigationService _navigationService;

        public TimelineCommandHandler(
            IScenarioEditorService editorService,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineNavigationService navigationService)
        {
            _editorService = editorService;
            _timelineBuilder = timelineBuilder;
            _navigationService = navigationService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionTimelineDayPrefix, StringComparison.Ordinal))
            {
                handled = true;
                state.TimelineSelectedDayId = actionId.Substring(ScenarioAuthoringActionIds.ActionTimelineDayPrefix.Length);
                state.TimelineSelectionId = state.TimelineSelectedDayId;
                message = "Timeline day " + state.TimelineSelectedDayId + " selected.";
                return true;
            }

            if (!actionId.StartsWith(ScenarioAuthoringActionIds.ActionTimelineEntryPrefix, StringComparison.Ordinal))
                return false;

            handled = true;
            string entryId = actionId.Substring(ScenarioAuthoringActionIds.ActionTimelineEntryPrefix.Length);
            ScenarioTimelineEntry entry = FindEntry(entryId);
            if (entry == null)
            {
                message = "Timeline entry target is missing: " + entryId + ".";
                return true;
            }

            return _navigationService.Navigate(state, entry, out message);
        }

        private ScenarioTimelineEntry FindEntry(string entryId)
        {
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            ScenarioRuntimeState runtimeState = null;
            try
            {
                ScenarioRuntimeStateService runtimeStateService = ScenarioCompositionRoot.Resolve<ScenarioRuntimeStateService>();
                runtimeState = runtimeStateService != null ? runtimeStateService.State : null;
            }
            catch
            {
            }

            List<ScenarioTimelineEntry> entries = _timelineBuilder.BuildEntries(definition, runtimeState);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (entries[i] != null && string.Equals(entries[i].Id, entryId, StringComparison.OrdinalIgnoreCase))
                    return entries[i];
            }
            return null;
        }
    }

    internal sealed class CaptureCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioSelectionScopeService _scopeService;

        public CaptureCommandHandler(
            ScenarioAuthoringCaptureService captureService,
            IScenarioEditorService editorService,
            ScenarioSelectionScopeService scopeService)
        {
            _captureService = captureService;
            _editorService = editorService;
            _scopeService = scopeService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null && actionId.StartsWith("capture.", StringComparison.Ordinal);
            message = null;
            if (!handled)
                return false;

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionCaptureFamily:
                    return OpenCapturePreview(state, ScenarioAuthoringLocalActionIds.FocusedKindCaptureFamily, out message);
                case ScenarioAuthoringLocalActionIds.ActionCaptureFamilyConfirm:
                    return ConfirmCapture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentFamily(session, out text); }, out message);
                case ScenarioAuthoringActionIds.ActionCaptureInventory:
                    return OpenCapturePreview(state, ScenarioAuthoringLocalActionIds.FocusedKindCaptureInventory, out message);
                case ScenarioAuthoringLocalActionIds.ActionCaptureInventoryConfirm:
                    return ConfirmCapture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentInventory(session, out text); }, out message);
                case ScenarioAuthoringActionIds.ActionCaptureShelterObjects:
                    if (_scopeService.ResolveActiveScope(state) != ScenarioTargetScope.BunkerInside)
                    {
                        message = "Shelter object capture is available only in the Inside selection scope.";
                        return true;
                    }
                    return Capture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentShelterObjects(session, out text); }, out message);
                case ScenarioAuthoringActionIds.ActionCaptureSelectedObject:
                    {
                        if (!_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
                            return true;
                        bool captured = _captureService.CaptureSelectedObject(_editorService.CurrentSession, state.SelectedTarget, out message);
                        return captured || !string.IsNullOrEmpty(message);
                    }
                case ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement:
                    {
                        if (!_scopeService.CanSelectTargetForCurrentStage(state, state.SelectedTarget, out message))
                        {
                            string status = message;
                            return FailWithoutChange(state, status, out message);
                        }
                        bool removed = _captureService.RemoveSelectedObjectPlacement(_editorService.CurrentSession, state.SelectedTarget, out message);
                        if (!removed)
                        {
                            string status = message;
                            return FailWithoutChange(state, status, out message);
                        }
                        return true;
                    }
                default:
                    handled = false;
                    return false;
            }
        }

        private static bool FailWithoutChange(ScenarioAuthoringState state, string statusMessage, out string message)
        {
            if (state != null && !string.IsNullOrEmpty(statusMessage))
                state.StatusMessage = statusMessage;
            message = null;
            return false;
        }

        private bool OpenCapturePreview(ScenarioAuthoringState state, string kind, out string message)
        {
            message = "Review the world capture preview, then confirm or cancel.";
            if (state != null)
            {
                state.FocusedEditorKind = kind;
                state.FocusedEditorIndex = 0;
                state.FocusedEditorIsNew = false;
                state.StatusMessage = message;
            }
            return true;
        }

        private bool ConfirmCapture(ScenarioAuthoringState state, CaptureAction action, out string message)
        {
            bool captured = Capture(state, action, out message);
            if (state != null)
            {
                state.FocusedEditorKind = null;
                state.FocusedEditorIndex = -1;
                state.FocusedEditorIsNew = false;
            }
            return captured;
        }

        private bool Capture(ScenarioAuthoringState state, CaptureAction action, out string message)
        {
            bool captured = action(_editorService.CurrentSession, out message);
            if (state != null)
                state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private delegate bool CaptureAction(ScenarioEditorSession session, out string message);
    }

    internal sealed class StoryAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioStoryAuthoringService _service;
        private readonly IScenarioEditorService _editorService;

        public StoryAuthoringCommandHandler(
            ScenarioStoryAuthoringService service,
            IScenarioEditorService editorService)
        {
            _service = service;
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_service == null || string.IsNullOrEmpty(actionId) || !_service.CanHandle(actionId))
                return false;

            handled = true;
            ScenarioEditorSession session = _editorService != null ? _editorService.CurrentSession : null;
            return _service.TryHandleAction(session, actionId, out message);
        }
    }

    internal sealed class GameplayScheduleCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioGameplayScheduleAuthoringService _service;
        private readonly IScenarioEditorService _editorService;

        public GameplayScheduleCommandHandler(
            ScenarioGameplayScheduleAuthoringService service,
            IScenarioEditorService editorService)
        {
            _service = service;
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null && actionId.StartsWith("scenario.", StringComparison.Ordinal);
            message = null;
            if (!handled || _service == null)
                return false;

            int pickerIndex;
            if (TryOpenIndexedFocusedEditor(
                state,
                actionId,
                ScenarioAuthoringLocalActionIds.ActionInventoryStartingPickerOpenPrefix,
                ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker,
                out pickerIndex))
            {
                message = "Opened searchable starting item picker.";
                return true;
            }

            if (TryOpenIndexedFocusedEditor(
                state,
                actionId,
                ScenarioAuthoringLocalActionIds.ActionInventorySchedulePickerOpenPrefix,
                ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker,
                out pickerIndex))
            {
                message = "Opened searchable timed item picker.";
                return true;
            }

            bool changed = _service.TryHandleAction(_editorService.CurrentSession, actionId, out message);
            if (changed)
            {
                FocusGameplayEditor(state, _editorService.CurrentSession, actionId);
                CloseInventoryPickerAfterSelection(state, actionId);
            }
            return changed;
        }

        private static bool TryOpenIndexedFocusedEditor(ScenarioAuthoringState state, string actionId, string prefix, string kind, out int index)
        {
            index = -1;
            if (state == null || string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            if (!int.TryParse(actionId.Substring(prefix.Length), out index) || index < 0)
                return false;

            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = false;
            return true;
        }

        private static void CloseInventoryPickerAfterSelection(ScenarioAuthoringState state, string actionId)
        {
            if (state == null || string.IsNullOrEmpty(state.FocusedEditorKind) || string.IsNullOrEmpty(actionId))
                return;

            bool pickerOpen = string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, StringComparison.OrdinalIgnoreCase);
            if (!pickerOpen)
                return;

            if (!actionId.StartsWith(ScenarioAuthoringActionIds.ActionInventoryStartingItemSelectPrefix, StringComparison.Ordinal)
                && !actionId.StartsWith(ScenarioAuthoringActionIds.ActionInventoryScheduleItemSelectPrefix, StringComparison.Ordinal))
                return;

            state.FocusedEditorKind = null;
            state.FocusedEditorIndex = -1;
            state.FocusedEditorIsNew = false;
        }

        private static void FocusGameplayEditor(ScenarioAuthoringState state, ScenarioEditorSession session, string actionId)
        {
            if (state == null || session == null || session.WorkingDefinition == null)
                return;

            ScenarioDefinition definition = session.WorkingDefinition;
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionWeatherScheduleAdd, StringComparison.Ordinal)
                && definition.TriggersAndEvents != null)
                SetFocusedEditor(state, "weather", definition.TriggersAndEvents.WeatherEvents.Count - 1, true);
            else if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionFutureSurvivorAdd, StringComparison.Ordinal)
                && definition.FamilySetup != null)
                SetFocusedEditor(state, ScenarioAuthoringLocalActionIds.FocusedKindFutureSurvivor, definition.FamilySetup.FutureSurvivors.Count - 1, true);
        }

        private static void SetFocusedEditor(ScenarioAuthoringState state, string kind, int index, bool isNew)
        {
            if (index < 0)
                return;
            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
            state.TimelineSelectedEntryId = kind + ":" + index.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class EventAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioEventAuthoringService _service;
        private readonly IScenarioEditorService _editorService;

        public EventAuthoringCommandHandler(
            ScenarioEventAuthoringService service,
            IScenarioEditorService editorService)
        {
            _service = service;
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = _service != null && _service.CanHandle(actionId);
            message = null;
            if (!handled)
                return false;

            bool changed = _service.TryHandleAction(_editorService.CurrentSession, actionId, out message);
            if (changed)
                FocusEventEditor(state, _editorService.CurrentSession, actionId);
            return changed;
        }

        private static void FocusEventEditor(ScenarioAuthoringState state, ScenarioEditorSession session, string actionId)
        {
            if (state == null || session == null || session.WorkingDefinition == null)
                return;

            ScenarioDefinition definition = session.WorkingDefinition;
            if (definition.TriggersAndEvents != null
                && (string.Equals(actionId, ScenarioAuthoringActionIds.ActionTriggerAddManual, StringComparison.Ordinal)
                    || string.Equals(actionId, ScenarioAuthoringActionIds.ActionTriggerAddScheduled, StringComparison.Ordinal)))
                SetFocusedEditor(state, "trigger", definition.TriggersAndEvents.Triggers.Count - 1, true);
            else if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionGateAdd, StringComparison.Ordinal))
                SetFocusedEditor(state, "gate", definition.Gates != null ? definition.Gates.Count - 1 : -1, true);
            else if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionScheduledActionAdd, StringComparison.Ordinal))
                SetFocusedEditor(state, "scheduled_action", definition.ScheduledActions != null ? definition.ScheduledActions.Count - 1 : -1, true);
        }

        private static void SetFocusedEditor(ScenarioAuthoringState state, string kind, int index, bool isNew)
        {
            if (index < 0)
                return;
            state.FocusedEditorKind = kind;
            state.FocusedEditorIndex = index;
            state.FocusedEditorIsNew = isNew;
            state.TimelineSelectedEntryId = kind + ":" + index.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class CharacterEditorCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioCharacterEditorAuthoringService _service;
        private readonly IScenarioEditorService _editorService;

        public CharacterEditorCommandHandler(
            ScenarioCharacterEditorAuthoringService service,
            IScenarioEditorService editorService)
        {
            _service = service;
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null
                && (string.Equals(actionId, ScenarioAuthoringActionIds.ActionStartingSurvivorAdd, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthoringLocalActionIds.ActionStartingSurvivorEditorOpenPrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthoringActionIds.ActionFutureSurvivorEditPrefix, StringComparison.Ordinal));
            message = null;
            if (!handled || _service == null)
                return false;

            return _service.TryHandleAction(_editorService.CurrentSession, state, actionId, out message);
        }
    }

    internal sealed class EditorLifecycleCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacementService;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacementService;
        private readonly ScenarioAuthoringBaseModeReloadService _baseModeReloadService;

        public EditorLifecycleCommandHandler(
            IScenarioEditorService editorService,
            ScenarioBuildPlacementAuthoringService buildPlacementService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementService,
            ScenarioAuthoringBaseModeReloadService baseModeReloadService)
        {
            _editorService = editorService;
            _buildPlacementService = buildPlacementService;
            _sceneSpritePlacementService = sceneSpritePlacementService;
            _baseModeReloadService = baseModeReloadService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null
                && (actionId.StartsWith("editor.", StringComparison.Ordinal)
                    || actionId.StartsWith("scenario.mode.", StringComparison.Ordinal)
                    || actionId.StartsWith("scenario.focused_editor.", StringComparison.Ordinal));
            message = null;
            if (!handled)
                return false;

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionSave:
                    return SaveDraft(state, out message);
                case ScenarioAuthoringActionIds.ActionDraftCopyPath:
                    return CopyDraftPath(state, out message);
                case ScenarioAuthoringActionIds.ActionPlaytest:
                    return TogglePlaytest(state, out message);
                case ScenarioAuthoringActionIds.ActionOpenPauseMenu:
                    return OpenPauseMenu(out message);
                case ScenarioAuthoringActionIds.ActionConvertToNormal:
                    _editorService.ConvertToNormalSave();
                    message = "Scenario binding converted to a normal save.";
                    return true;
                case ScenarioAuthoringActionIds.ActionScenarioModePrevious:
                    return OpenBaseModeDialog(state, -1, out message);
                case ScenarioAuthoringActionIds.ActionScenarioModeNext:
                    return OpenBaseModeDialog(state, 1, out message);
                case ScenarioAuthoringActionIds.ActionFocusedEditorSave:
                    return CloseFocusedEditor(state, false, out message);
                case ScenarioAuthoringActionIds.ActionFocusedEditorCancel:
                    return CloseFocusedEditor(state, true, out message);
                default:
                    ScenarioBaseGameMode reloadBaseMode;
                    if (ScenarioBaseModeAuthoringActions.TryParseBaseMode(
                        actionId,
                        ScenarioBaseModeAuthoringActions.ActionSwitchReloadPrefix,
                        out reloadBaseMode))
                    {
                        return SaveAndReloadBaseMode(state, reloadBaseMode, out message);
                    }

                    ScenarioBaseGameMode switchOnlyBaseMode;
                    if (ScenarioBaseModeAuthoringActions.TryParseBaseMode(
                        actionId,
                        ScenarioBaseModeAuthoringActions.ActionSwitchOnlyPrefix,
                        out switchOnlyBaseMode))
                    {
                        return SwitchBaseModeOnly(state, switchOnlyBaseMode, out message);
                    }

                    if (string.Equals(actionId, ScenarioBaseModeAuthoringActions.ActionSwitchCancel, StringComparison.Ordinal))
                        return CloseBaseModeDialog(state, "Base switch canceled.", out message);

                    handled = false;
                    return false;
            }
        }

        private static bool CopyDraftPath(ScenarioAuthoringState state, out string message)
        {
            string path = state != null ? state.ActiveScenarioFilePath : null;
            if (string.IsNullOrEmpty(path))
            {
                message = "No draft path is active.";
                return true;
            }

            GUIUtility.systemCopyBuffer = path;
            message = "Draft path copied.";
            return true;
        }

        private bool CloseFocusedEditor(ScenarioAuthoringState state, bool cancel, out string message)
        {
            message = null;
            if (state == null || string.IsNullOrEmpty(state.FocusedEditorKind))
            {
                message = "No focused editor is open.";
                return true;
            }

            string kind = state.FocusedEditorKind;
            int index = state.FocusedEditorIndex;
            bool discard = cancel && state.FocusedEditorIsNew;
            if (discard)
                DiscardFocusedEntry(kind, index);

            state.TimelineSelectedEntryId = discard ? null : kind + ":" + index.ToString(CultureInfo.InvariantCulture);
            state.FocusedEditorKind = null;
            state.FocusedEditorIndex = -1;
            state.FocusedEditorIsNew = false;
            message = discard ? "New timeline entry discarded." : "Timeline entry saved.";
            return true;
        }

        private bool OpenBaseModeDialog(ScenarioAuthoringState state, int direction, out string message)
        {
            message = null;
            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (state == null || definition == null)
            {
                message = "No active scenario definition is available.";
                return true;
            }

            ScenarioBaseGameMode nextMode = ResolveAdjacentBaseMode(definition.BaseGameMode, direction);
            if (nextMode == definition.BaseGameMode)
            {
                message = "Base mode is already " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(nextMode) + ".";
                return true;
            }

            state.FocusedEditorKind = ScenarioBaseModeAuthoringActions.FocusedEditorKind;
            state.FocusedEditorIndex = (int)nextMode;
            state.FocusedEditorIsNew = false;
            state.TimelineSelectedEntryId = null;
            message = "Choose how to switch base to " + ScenarioAuthoringBaseModeReloadService.FormatBaseMode(nextMode) + ".";
            return true;
        }

        private bool SaveAndReloadBaseMode(ScenarioAuthoringState state, ScenarioBaseGameMode baseMode, out string message)
        {
            CloseBaseModeDialogState(state);
            if (_baseModeReloadService == null)
            {
                message = "Base reload service is unavailable.";
                return true;
            }

            return _baseModeReloadService.SaveAndReload(_editorService.CurrentSession, baseMode, out message);
        }

        private bool SwitchBaseModeOnly(ScenarioAuthoringState state, ScenarioBaseGameMode baseMode, out string message)
        {
            CloseBaseModeDialogState(state);
            if (_baseModeReloadService == null)
            {
                message = "Base reload service is unavailable.";
                return true;
            }

            return _baseModeReloadService.SaveBaseModeOnly(_editorService.CurrentSession, baseMode, out message);
        }

        private static bool CloseBaseModeDialog(ScenarioAuthoringState state, string closeMessage, out string message)
        {
            CloseBaseModeDialogState(state);
            message = closeMessage;
            return true;
        }

        private static void CloseBaseModeDialogState(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            if (string.Equals(state.FocusedEditorKind, ScenarioBaseModeAuthoringActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
            {
                state.FocusedEditorKind = null;
                state.FocusedEditorIndex = -1;
                state.FocusedEditorIsNew = false;
                state.TimelineSelectedEntryId = null;
            }
        }

        private void DiscardFocusedEntry(string kind, int index)
        {
            ScenarioEditorSession session = _editorService.CurrentSession;
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null || index < 0)
                return;

            if (string.Equals(kind, "weather", StringComparison.OrdinalIgnoreCase)
                && definition.TriggersAndEvents != null
                && index < definition.TriggersAndEvents.WeatherEvents.Count)
            {
                definition.TriggersAndEvents.WeatherEvents.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if (string.Equals(kind, "trigger", StringComparison.OrdinalIgnoreCase)
                && definition.TriggersAndEvents != null
                && index < definition.TriggersAndEvents.Triggers.Count)
            {
                definition.TriggersAndEvents.Triggers.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if (string.Equals(kind, "gate", StringComparison.OrdinalIgnoreCase)
                && definition.Gates != null
                && index < definition.Gates.Count)
            {
                string id = definition.Gates[index] != null ? definition.Gates[index].Id : null;
                definition.Gates.RemoveAt(index);
                ClearGateReferences(definition, id);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
            else if (string.Equals(kind, "scheduled_action", StringComparison.OrdinalIgnoreCase)
                && definition.ScheduledActions != null
                && index < definition.ScheduledActions.Count)
            {
                definition.ScheduledActions.RemoveAt(index);
                MarkDirty(session, ScenarioDirtySection.Triggers);
            }
        }

        private static void ClearGateReferences(ScenarioDefinition definition, string gateId)
        {
            if (definition == null || string.IsNullOrEmpty(gateId) || definition.ScheduledActions == null)
                return;

            for (int i = 0; i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action != null && string.Equals(action.GateId, gateId, StringComparison.OrdinalIgnoreCase))
                    action.GateId = null;
            }
        }

        private static bool OpenPauseMenu(out string message)
        {
            bool opened = ScenarioAuthoringPauseService.Instance.OpenPauseMenu("Scenario authoring pause menu button.");
            message = opened ? "Pause menu opened." : "Pause menu could not be opened.";
            return true;
        }

        private static ScenarioBaseGameMode ResolveAdjacentBaseMode(ScenarioBaseGameMode mode, int direction)
        {
            int count = Enum.GetValues(typeof(ScenarioBaseGameMode)).Length;
            int next = ((int)mode + direction) % count;
            if (next < 0)
                next += count;

            return (ScenarioBaseGameMode)next;
        }

        private static void MarkDirty(ScenarioEditorSession session, ScenarioDirtySection section)
        {
            if (session == null)
                return;

            session.MarkDraftChanged(section);
        }

        private bool SaveDraft(ScenarioAuthoringState state, out string message)
        {
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(null);
                if (validation != null && validation.IsValid)
                {
                    ScenarioEditorSession session = _editorService.CurrentSession;
                    message = session != null && session.HasUnappliedDraftChanges
                        ? "Scenario draft saved. Running playtest predates recent edits; stop and restart playtest to verify the saved draft."
                        : "Scenario draft saved.";
                    return true;
                }

                message = "Scenario draft save failed validation: " + FormatValidationSummary(validation);
                return true;
            }
            catch (Exception ex)
            {
                message = "Scenario draft save failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Save failed: " + ex.Message);
                return true;
            }
        }

        private bool TogglePlaytest(ScenarioAuthoringState state, out string message)
        {
            try
            {
                ScenarioEditorSession editorSession = _editorService.CurrentSession;
                if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                {
                    _editorService.EndPlaytest();
                    message = "Playtest ended. Authoring pause restored.";
                    return true;
                }

                string placementMessage = null;
                if (_buildPlacementService != null && _buildPlacementService.HasActivePlacement)
                    _buildPlacementService.CancelForPlaytest(out placementMessage);

                if (_sceneSpritePlacementService != null && _sceneSpritePlacementService.HasActivePlacement)
                {
                    _sceneSpritePlacementService.Reset();
                    if (string.IsNullOrEmpty(placementMessage))
                        placementMessage = "Placement cancelled before playtest started.";
                }

                ScenarioApplyResult result = _editorService.BeginPlaytest();
                string playtestMessage = BuildPlaytestStatus(result);
                message = !string.IsNullOrEmpty(placementMessage)
                    ? placementMessage + " " + playtestMessage
                    : playtestMessage;
                return true;
            }
            catch (Exception ex)
            {
                message = "Playtest toggle failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Playtest toggle failed: " + ex.Message);
                return true;
            }
        }

        private static string BuildPlaytestStatus(ScenarioApplyResult result)
        {
            if (result == null || result.Messages == null || result.Messages.Length == 0)
                return "Playtest started.";

            return string.Join(" ", result.Messages);
        }

        private static string FormatValidationSummary(ScenarioValidationResult validation)
        {
            if (validation == null)
                return "Unknown validation error.";

            ScenarioValidationIssue[] issues = validation.Issues;
            if (issues == null || issues.Length == 0)
                return "Unknown validation error.";

            List<string> messages = new List<string>();
            for (int i = 0; i < issues.Length && messages.Count < 2; i++)
            {
                ScenarioValidationIssue issue = issues[i];
                if (issue != null && !string.IsNullOrEmpty(issue.Message))
                    messages.Add(issue.Message);
            }

            return messages.Count > 0 ? string.Join(" | ", messages.ToArray()) : "Unknown validation error.";
        }
    }

    internal sealed class SelectionCommandHandler : IScenarioCommandHandler
    {
        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            message = null;
            handled = false;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSelectionClear, StringComparison.Ordinal))
            {
                handled = true;
                if (state.SelectedTarget == null && (state.MultiSelection == null || state.MultiSelection.Count == 0))
                {
                    message = "Selection is already clear.";
                    return true;
                }

                state.SelectedTarget = null;
                state.MultiSelection.Clear();
                message = "Selection cleared.";
                return true;
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionSelectionStackCycle, StringComparison.Ordinal))
            {
                handled = true;
                return CycleSelectionStack(state, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix, StringComparison.Ordinal))
            {
                handled = true;
                string token = actionId.Substring(ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix.Length);
                int index;
                if (!int.TryParse(token, out index))
                {
                    message = "Selection stack row is invalid.";
                    return true;
                }

                return SelectStackIndex(state, index, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionHierarchySelectPrefix, StringComparison.Ordinal))
            {
                handled = true;
                string targetId = actionId.Substring(ScenarioAuthoringActionIds.ActionHierarchySelectPrefix.Length);
                return SelectHierarchyTarget(state, targetId, out message);
            }

            return false;
        }

        private static bool CycleSelectionStack(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (state == null || state.SelectionStack == null || state.SelectionStack.Count == 0)
            {
                message = "No selection stack candidates are available.";
                return true;
            }

            int count = state.SelectionStack.Count;
            int next = (state.ActiveSelectionStackIndex + 1) % count;
            return SelectStackIndex(state, next, out message);
        }

        private static bool SelectStackIndex(ScenarioAuthoringState state, int index, out string message)
        {
            message = null;
            if (state == null || state.SelectionStack == null || state.SelectionStack.Count == 0)
            {
                message = "No selection stack candidates are available.";
                return true;
            }

            if (index < 0 || index >= state.SelectionStack.Count)
            {
                message = "Selection stack row is out of range.";
                return true;
            }

            state.ActiveSelectionStackIndex = index;
            ScenarioAuthoringTarget target = state.SelectionStack[index];
            if (target == null)
            {
                message = "Selection stack target is missing.";
                return true;
            }

            state.SelectedTarget = target.Copy();
            state.HoveredTarget = target.Copy();
            state.MultiSelection.Clear();
            state.MultiSelection.Add(target.Copy());
            message = "Selected " + target.DisplayName + " from the stack.";
            return true;
        }

        private static bool SelectHierarchyTarget(ScenarioAuthoringState state, string targetId, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(targetId))
            {
                message = "Hierarchy target is missing.";
                return true;
            }

            ScenarioAuthoringTarget target = FindStackTarget(state, targetId) ?? BuildTargetFromScene(targetId);
            if (target == null)
            {
                message = "Hierarchy target is not live in the current scene: " + targetId + ".";
                return true;
            }

            state.SelectedTarget = target.Copy();
            state.HoveredTarget = target.Copy();
            state.MultiSelection.Clear();
            state.MultiSelection.Add(target.Copy());
            message = "Selected " + target.DisplayName + " from hierarchy.";
            return true;
        }

        private static ScenarioAuthoringTarget FindStackTarget(ScenarioAuthoringState state, string targetId)
        {
            for (int i = 0; state != null && state.SelectionStack != null && i < state.SelectionStack.Count; i++)
            {
                ScenarioAuthoringTarget target = state.SelectionStack[i];
                if (target != null && string.Equals(target.Id, targetId, StringComparison.OrdinalIgnoreCase))
                    return target;
            }

            return null;
        }

        private static ScenarioAuthoringTarget BuildTargetFromScene(string targetId)
        {
            int separator = targetId != null ? targetId.LastIndexOf(':') : -1;
            if (separator <= 0 || separator >= targetId.Length - 1)
                return null;

            int instanceId;
            if (!int.TryParse(targetId.Substring(separator + 1), out instanceId))
                return null;

            ScenarioAuthoringTargetKind kind = ScenarioAuthoringTargetKind.Unknown;
            try
            {
                object parsed = Enum.Parse(typeof(ScenarioAuthoringTargetKind), targetId.Substring(0, separator), true);
                if (parsed != null && Enum.IsDefined(typeof(ScenarioAuthoringTargetKind), parsed))
                    kind = (ScenarioAuthoringTargetKind)parsed;
            }
            catch
            {
            }

            GameObject[] objects = UnityEngine.Object.FindObjectsOfType<GameObject>();
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                GameObject gameObject = objects[i];
                if (gameObject == null || gameObject.transform == null || gameObject.transform.GetInstanceID() != instanceId)
                    continue;

                ScenarioSceneSpritePlacementMarker marker = gameObject.GetComponentInParent<ScenarioSceneSpritePlacementMarker>();
                return new ScenarioAuthoringTarget
                {
                    Id = targetId,
                    Kind = kind,
                    DisplayName = string.IsNullOrEmpty(gameObject.name) ? kind.ToString() : gameObject.name,
                    Description = kind + " at " + BuildTransformPath(gameObject.transform),
                    AdapterId = "ShelteredAPI.Hierarchy",
                    GameObjectName = gameObject.name,
                    TransformPath = BuildTransformPath(gameObject.transform),
                    ScenarioReferenceId = marker != null ? marker.PlacementId : null,
                    RuntimeObject = gameObject,
                    HighlightObject = gameObject,
                    WorldPosition = gameObject.transform.position,
                    SupportsInspect = true,
                    SupportsReplace = true
                };
            }

            return null;
        }

        private static string BuildTransformPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }

    internal sealed class ToolCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public ToolCommandHandler(ScenarioAuthoringLayoutService layoutService)
        {
            _layoutService = layoutService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null && actionId.StartsWith("tool.", StringComparison.Ordinal);
            message = null;
            if (!handled)
                return false;

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionToolSelect:
                    return FocusSelection(state, out message);
                case ScenarioAuthoringActionIds.ActionToolFamily:
                    return SetTool(state, ScenarioAuthoringTool.Family, out message);
                case ScenarioAuthoringActionIds.ActionToolInventory:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, out message);
                case ScenarioAuthoringActionIds.ActionToolShelter:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, out message);
                case ScenarioAuthoringActionIds.ActionToolAssets:
                    bool modeChanged = state.AssetMode != ScenarioAssetAuthoringMode.PlaceNew;
                    state.AssetMode = ScenarioAssetAuthoringMode.PlaceNew;
                    bool toolChanged = SetTool(state, ScenarioAuthoringTool.Assets, out message);
                    if (!toolChanged && modeChanged)
                        message = BuildToolStatus(state, ScenarioAuthoringTool.Assets, true);
                    return toolChanged || modeChanged;
                case ScenarioAuthoringActionIds.ActionToolObjects:
                    return SetTool(state, ScenarioAuthoringTool.Objects, out message);
                case ScenarioAuthoringActionIds.ActionToolWiring:
                    return SetTool(state, ScenarioAuthoringTool.Wiring, out message);
                case ScenarioAuthoringActionIds.ActionToolPeople:
                    return SetTool(state, ScenarioAuthoringTool.Family, out message);
                case ScenarioAuthoringActionIds.ActionToolVehicle:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, out message);
                case ScenarioAuthoringActionIds.ActionToolWinLoss:
                    return SetTool(state, ScenarioAuthoringTool.WinLoss, out message);
                default:
                    handled = false;
                    return false;
            }
        }

        private bool SetTool(ScenarioAuthoringState state, ScenarioAuthoringTool tool, out string message)
        {
            message = null;
            if (state == null)
                return false;

            ScenarioAuthoringWorkflowTransition transition = _layoutService.SelectTool(state, tool);
            if (!transition.Changed)
                return false;

            message = BuildToolStatus(state, tool, transition.StageChanged);
            string placementMessage;
            ScenarioBuildPlacementAuthoringService buildPlacement = ScenarioCompositionRoot.Resolve<ScenarioBuildPlacementAuthoringService>();
            if (buildPlacement != null && buildPlacement.HasActivePlacement && buildPlacement.CancelForToolSwitch(out placementMessage))
            {
                message = !string.IsNullOrEmpty(placementMessage)
                    ? message + " " + placementMessage
                    : message;
            }
            return true;
        }

        private bool FocusSelection(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (state == null)
                return false;

            bool changed = false;
            string placementMessage;
            ScenarioBuildPlacementAuthoringService buildPlacement = ScenarioCompositionRoot.Resolve<ScenarioBuildPlacementAuthoringService>();
            if (buildPlacement != null && buildPlacement.HasActivePlacement && buildPlacement.CancelForToolSwitch(out placementMessage))
            {
                message = placementMessage;
                changed = true;
            }

            if (state.ActiveTool == ScenarioAuthoringTool.Select)
                return changed;

            ScenarioAuthoringWorkflowTransition transition = _layoutService.SelectTool(state, ScenarioAuthoringTool.Select);
            if (transition.Changed)
            {
                message = string.IsNullOrEmpty(message)
                    ? "Selection focused."
                    : message + " Selection focused.";
                changed = true;
            }

            return changed;
        }

        private static string BuildToolStatus(ScenarioAuthoringState state, ScenarioAuthoringTool requestedTool, bool stageChanged)
        {
            string toolLabel = ScenarioAuthoringWorkflowLabels.GetToolLabel(state != null ? state.ActiveTool : requestedTool);
            string stageLabel = ScenarioAuthoringWorkflowLabels.GetStageLabel(state != null ? state.ActiveStage : ScenarioStageKind.None, false);
            string workspace = ScenarioAuthoringWorkflowRules.ShouldShowToolWorkspace(state)
                ? " Tool workspace opened."
                : string.Empty;

            if (stageChanged)
                return toolLabel + " tool active in " + stageLabel + "." + workspace;

            return toolLabel + " tool active." + workspace;
        }
    }
}
