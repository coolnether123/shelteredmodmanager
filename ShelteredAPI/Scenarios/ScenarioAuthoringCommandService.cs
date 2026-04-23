using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringCommandService
    {
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioSpriteSwapAuthoringService _spriteSwapAuthoringService;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacementAuthoringService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public ScenarioAuthoringCommandService(
            ScenarioAuthoringCaptureService captureService,
            ScenarioSpriteSwapAuthoringService spriteSwapAuthoringService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementAuthoringService,
            IScenarioEditorService editorService,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService)
        {
            _captureService = captureService ?? ScenarioAuthoringCaptureService.Instance;
            _spriteSwapAuthoringService = spriteSwapAuthoringService ?? ScenarioSpriteSwapAuthoringService.Instance;
            _sceneSpritePlacementAuthoringService = sceneSpritePlacementAuthoringService ?? ScenarioSceneSpritePlacementAuthoringService.Instance;
            _editorService = editorService;
            _settingsService = settingsService ?? new ScenarioAuthoringSettingsService();
            _layoutService = layoutService;
        }

        public bool Execute(ScenarioAuthoringState state, string actionId)
        {
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            bool handled;
            string spriteSwapMessage;
            bool spriteSwapChanged = _spriteSwapAuthoringService.TryHandleAction(state, actionId, out handled, out spriteSwapMessage);
            if (handled)
            {
                if (!string.IsNullOrEmpty(spriteSwapMessage))
                    state.StatusMessage = spriteSwapMessage;
                return spriteSwapChanged || !string.IsNullOrEmpty(spriteSwapMessage);
            }

            string scenePlacementMessage;
            bool scenePlacementChanged = _sceneSpritePlacementAuthoringService.TryHandleAction(state, actionId, out handled, out scenePlacementMessage);
            if (handled)
            {
                if (!string.IsNullOrEmpty(scenePlacementMessage))
                    state.StatusMessage = scenePlacementMessage;
                return scenePlacementChanged || !string.IsNullOrEmpty(scenePlacementMessage);
            }

            string buildPlacementMessage;
            bool buildPlacementChanged = ScenarioBuildPlacementAuthoringService.Instance.TryHandleAction(state, actionId, out handled, out buildPlacementMessage);
            if (handled)
            {
                if (!string.IsNullOrEmpty(buildPlacementMessage))
                    state.StatusMessage = buildPlacementMessage;
                return buildPlacementChanged || !string.IsNullOrEmpty(buildPlacementMessage);
            }

            if (TryHandlePrefixedAction(state, actionId))
                return true;

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionShellToggle:
                    state.ShellVisible = !state.ShellVisible;
                    state.StatusMessage = state.ShellVisible ? "Authoring shell opened." : "Authoring shell hidden.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellShow:
                    if (!state.ShellVisible)
                    {
                        state.ShellVisible = true;
                        state.StatusMessage = "Authoring shell opened.";
                    }
                    else
                    {
                        state.StatusMessage = "Authoring inspector already open.";
                    }

                    return true;

                case ScenarioAuthoringActionIds.ActionShellHideAll:
                case ScenarioAuthoringActionIds.ActionShellMinimalMode:
                    _layoutService.HideAll(state);
                    state.StatusMessage = "Major authoring panels hidden.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellResetLayout:
                    _layoutService.ResetLayout(state);
                    state.StatusMessage = "Authoring shell layout reset.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellFocusSelection:
                    _layoutService.FocusSelection(state);
                    state.StatusMessage = "Focused the shell on the current selection.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellOpenSettings:
                    if (!_layoutService.SetSettingsWindowOpen(state, true))
                        return false;
                    state.StatusMessage = "Editor settings opened.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellCloseSettings:
                    if (!_layoutService.SetSettingsWindowOpen(state, false))
                        return false;
                    state.StatusMessage = "Editor settings closed.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellSettingsReset:
                    state.Settings = _settingsService.ResetToDefaults();
                    _layoutService.ResetLayout(state);
                    state.StatusMessage = "Editor settings reset to defaults.";
                    return true;

                case ScenarioAuthoringActionIds.ActionShellTabShelter:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Shelter, ScenarioAuthoringTool.Select, "Shelter overview active.");
                case ScenarioAuthoringActionIds.ActionShellTabBuild:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Build, ScenarioAuthoringTool.Shelter, "Build workflow active.");
                case ScenarioAuthoringActionIds.ActionShellTabSurvivors:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Survivors, ScenarioAuthoringTool.Family, "Survivor authoring active.");
                case ScenarioAuthoringActionIds.ActionShellTabStockpile:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Stockpile, ScenarioAuthoringTool.Inventory, "Stockpile authoring active.");
                case ScenarioAuthoringActionIds.ActionShellTabTriggers:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Triggers, ScenarioAuthoringTool.Select, "Trigger workflow active.");
                case ScenarioAuthoringActionIds.ActionShellTabQuests:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Quests, ScenarioAuthoringTool.Select, "Quest workflow active.");
                case ScenarioAuthoringActionIds.ActionShellTabArt:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Art, ScenarioAuthoringTool.Assets, "Art workflow active.");
                case ScenarioAuthoringActionIds.ActionShellTabTest:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Test, ScenarioAuthoringTool.Select, "Test workflow active.");
                case ScenarioAuthoringActionIds.ActionShellTabShell:
                    return SetShellTab(state, ScenarioAuthoringShellTab.Shell, ScenarioAuthoringTool.Select, "Shell tools active.");

                case ScenarioAuthoringActionIds.ActionSave:
                    return SaveDraft(state);

                case ScenarioAuthoringActionIds.ActionPlaytest:
                    return TogglePlaytest(state);

                case ScenarioAuthoringActionIds.ActionCaptureFamily:
                    return CaptureCurrentFamily(state);

                case ScenarioAuthoringActionIds.ActionCaptureInventory:
                    return CaptureCurrentInventory(state);

                case ScenarioAuthoringActionIds.ActionCaptureShelterObjects:
                    return CaptureShelterObjects(state);

                case ScenarioAuthoringActionIds.ActionCaptureSelectedObject:
                    return CaptureSelectedObject(state);

                case ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement:
                    return RemoveSelectedObjectPlacement(state);

                case ScenarioAuthoringActionIds.ActionConvertToNormal:
                    _editorService.ConvertToNormalSave();
                    state.StatusMessage = "Scenario binding converted to a normal save.";
                    return true;

                case ScenarioAuthoringActionIds.ActionSelectionClear:
                    if (state.SelectedTarget == null)
                        return false;
                    state.SelectedTarget = null;
                    state.MultiSelection.Clear();
                    state.StatusMessage = "Selection cleared.";
                    return true;

                case ScenarioAuthoringActionIds.ActionAssetModeReplace:
                    if (state.AssetMode == ScenarioAssetAuthoringMode.ReplaceExisting)
                        return false;
                    state.AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting;
                    state.StatusMessage = "Asset workflow set to replace existing visuals.";
                    return true;

                case ScenarioAuthoringActionIds.ActionAssetModePlace:
                    if (state.AssetMode == ScenarioAssetAuthoringMode.PlaceNew)
                        return false;
                    state.AssetMode = ScenarioAssetAuthoringMode.PlaceNew;
                    state.StatusMessage = "Asset workflow set to place new snapped sprites.";
                    return true;

                case ScenarioAuthoringActionIds.ActionToolSelect:
                    return SetTool(state, ScenarioAuthoringTool.Select, "Selection tool active.");
                case ScenarioAuthoringActionIds.ActionToolFamily:
                    return SetTool(state, ScenarioAuthoringTool.Family, "Family capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolInventory:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, "Inventory capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolShelter:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, "Structure placement tool active.");
                case ScenarioAuthoringActionIds.ActionToolAssets:
                    return SetTool(state, ScenarioAuthoringTool.Assets, "Asset workflow active.");
                case ScenarioAuthoringActionIds.ActionToolObjects:
                    return SetTool(state, ScenarioAuthoringTool.Objects, "Shelter object placement tool active.");
                case ScenarioAuthoringActionIds.ActionToolWiring:
                    return SetTool(state, ScenarioAuthoringTool.Wiring, "Wall and wiring tool active.");
                case ScenarioAuthoringActionIds.ActionToolPeople:
                    return SetTool(state, ScenarioAuthoringTool.Family, "Family capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolVehicle:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, "Shelter object capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolWinLoss:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, "Inventory capture tool active.");
            }

            return false;
        }

        private bool TryHandlePrefixedAction(ScenarioAuthoringState state, string actionId)
        {
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowTogglePrefix, StringComparison.Ordinal))
            {
                string windowId = actionId.Substring(ScenarioAuthoringActionIds.ActionWindowTogglePrefix.Length);
                bool toggled = _layoutService.ToggleWindowVisibility(state, windowId);
                if (toggled)
                    state.StatusMessage = "Toggled panel '" + windowId + "'.";
                return toggled;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix, StringComparison.Ordinal))
            {
                string windowId = actionId.Substring(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix.Length);
                bool toggled = _layoutService.ToggleWindowCollapsed(state, windowId);
                if (toggled)
                    state.StatusMessage = "Updated panel '" + windowId + "'.";
                return toggled;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionInspectorTabPrefix, StringComparison.Ordinal))
            {
                string token = actionId.Substring(ScenarioAuthoringActionIds.ActionInspectorTabPrefix.Length);
                return SetInspectorTab(state, token);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingTogglePrefix, StringComparison.Ordinal))
            {
                string settingId = actionId.Substring(ScenarioAuthoringActionIds.ActionSettingTogglePrefix.Length);
                return ToggleSetting(state, settingId);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix, StringComparison.Ordinal))
            {
                string settingId = actionId.Substring(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix.Length);
                return StepSetting(state, settingId, +1f);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix, StringComparison.Ordinal))
            {
                string settingId = actionId.Substring(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix.Length);
                return StepSetting(state, settingId, -1f);
            }

            return false;
        }

        private bool SaveDraft(ScenarioAuthoringState state)
        {
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(null);
                if (validation != null && validation.IsValid)
                {
                    state.StatusMessage = "Scenario draft saved.";
                    return true;
                }

                state.StatusMessage = "Scenario draft save failed validation: " + FormatValidationSummary(validation);
                return true;
            }
            catch (Exception ex)
            {
                state.StatusMessage = "Scenario draft save failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Save failed: " + ex.Message);
                return true;
            }
        }

        private bool TogglePlaytest(ScenarioAuthoringState state)
        {
            try
            {
                ScenarioEditorSession editorSession = _editorService.CurrentSession;
                if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                {
                    _editorService.EndPlaytest();
                    state.StatusMessage = "Playtest ended. Authoring pause restored.";
                    return true;
                }

                ScenarioApplyResult result = _editorService.BeginPlaytest();
                state.StatusMessage = BuildPlaytestStatus(result);
                return true;
            }
            catch (Exception ex)
            {
                state.StatusMessage = "Playtest toggle failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Playtest toggle failed: " + ex.Message);
                return true;
            }
        }

        private bool CaptureCurrentFamily(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureCurrentFamily(_editorService.CurrentSession, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool CaptureCurrentInventory(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureCurrentInventory(_editorService.CurrentSession, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool CaptureShelterObjects(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureCurrentShelterObjects(_editorService.CurrentSession, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool CaptureSelectedObject(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureSelectedObject(_editorService.CurrentSession, state.SelectedTarget, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool RemoveSelectedObjectPlacement(ScenarioAuthoringState state)
        {
            string message;
            bool removed = _captureService.RemoveSelectedObjectPlacement(_editorService.CurrentSession, state.SelectedTarget, out message);
            state.StatusMessage = message;
            return removed || !string.IsNullOrEmpty(message);
        }

        private static string BuildPlaytestStatus(ScenarioApplyResult result)
        {
            if (result == null || result.Messages == null || result.Messages.Length == 0)
                return "Playtest started.";

            return string.Join(" ", result.Messages);
        }

        private static bool SetTool(ScenarioAuthoringState state, ScenarioAuthoringTool tool, string message)
        {
            if (state.ActiveTool == tool)
                return false;

            state.ActiveTool = tool;
            state.StatusMessage = message;
            return true;
        }

        private static bool SetShellTab(ScenarioAuthoringState state, ScenarioAuthoringShellTab tab, ScenarioAuthoringTool tool, string message)
        {
            bool changed = false;
            if (state.ActiveShellTab != tab)
            {
                state.ActiveShellTab = tab;
                changed = true;
            }

            if (state.ActiveTool != tool)
            {
                state.ActiveTool = tool;
                changed = true;
            }

            if (changed)
                state.StatusMessage = message;

            return changed;
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

            return messages.Count > 0
                ? string.Join(" | ", messages.ToArray())
                : "Unknown validation error.";
        }
    }
}
