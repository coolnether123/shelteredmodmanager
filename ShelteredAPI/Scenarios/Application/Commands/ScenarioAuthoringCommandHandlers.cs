using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class SpriteCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSpriteSwapAuthoringService _service;

        public SpriteCommandHandler(ScenarioSpriteSwapAuthoringService service)
        {
            _service = service;
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

            return _service.TryHandleAction(state, actionId, out handled, out message);
        }
    }

    internal sealed class SceneSpriteCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioSceneSpritePlacementAuthoringService _service;

        public SceneSpriteCommandHandler(ScenarioSceneSpritePlacementAuthoringService service)
        {
            _service = service;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_service == null || string.IsNullOrEmpty(actionId) || !actionId.StartsWith("scene_sprite.", StringComparison.Ordinal))
                return false;

            return _service.TryHandleAction(state, actionId, out handled, out message);
        }
    }

    internal sealed class BuildCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioBuildPlacementAuthoringService _service;

        public BuildCommandHandler(ScenarioBuildPlacementAuthoringService service)
        {
            _service = service;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (_service == null || string.IsNullOrEmpty(actionId) || !actionId.StartsWith("build.", StringComparison.Ordinal))
                return false;

            return _service.TryHandleAction(state, actionId, out handled, out message);
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
                case ScenarioAuthoringActionIds.ActionShellOpenSettings:
                    if (!_layoutService.SetSettingsWindowOpen(state, true))
                        return false;
                    message = "Editor settings opened.";
                    return true;
                case ScenarioAuthoringActionIds.ActionShellCloseSettings:
                    if (!_layoutService.SetSettingsWindowOpen(state, false))
                        return false;
                    message = "Editor settings closed.";
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
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionInspectorTabPrefix, StringComparison.Ordinal)
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
                    state.StatusMessage = "Toggled panel '" + windowId + "'.";
                return toggled;
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionStageSelectPrefix, StringComparison.Ordinal))
            {
                string token = actionId.Substring(ScenarioAuthoringActionIds.ActionStageSelectPrefix.Length);
                ScenarioStageKind stageKind;
                if (!TryParseStageKind(token, out stageKind))
                    return false;

                return _layoutService.SelectStage(state, stageKind);
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
                return SetInspectorTab(state, actionId.Substring(ScenarioAuthoringActionIds.ActionInspectorTabPrefix.Length));

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingTogglePrefix, StringComparison.Ordinal))
                return ToggleSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingTogglePrefix.Length));

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix, StringComparison.Ordinal))
                return StepSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingIncreasePrefix.Length), +1f);

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix, StringComparison.Ordinal))
                return StepSetting(state, actionId.Substring(ScenarioAuthoringActionIds.ActionSettingDecreasePrefix.Length), -1f);

            return false;
        }

        private bool SetStage(ScenarioAuthoringState state, ScenarioStageKind stageKind, out string message, string statusMessage)
        {
            message = null;
            bool changed = _layoutService.SelectStage(state, stageKind);
            if (changed)
                message = statusMessage;
            return changed;
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
    }

    internal sealed class CaptureCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly IScenarioEditorService _editorService;

        public CaptureCommandHandler(ScenarioAuthoringCaptureService captureService, IScenarioEditorService editorService)
        {
            _captureService = captureService;
            _editorService = editorService;
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
                    return Capture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentFamily(session, out text); }, out message);
                case ScenarioAuthoringActionIds.ActionCaptureInventory:
                    return Capture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentInventory(session, out text); }, out message);
                case ScenarioAuthoringActionIds.ActionCaptureShelterObjects:
                    return Capture(state, delegate(ScenarioEditorSession session, out string text) { return _captureService.CaptureCurrentShelterObjects(session, out text); }, out message);
                case ScenarioAuthoringActionIds.ActionCaptureSelectedObject:
                    {
                        bool captured = _captureService.CaptureSelectedObject(_editorService.CurrentSession, state.SelectedTarget, out message);
                        return captured || !string.IsNullOrEmpty(message);
                    }
                case ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement:
                    {
                        bool removed = _captureService.RemoveSelectedObjectPlacement(_editorService.CurrentSession, state.SelectedTarget, out message);
                        return removed || !string.IsNullOrEmpty(message);
                    }
                default:
                    handled = false;
                    return false;
            }
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

    internal sealed class EditorLifecycleCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioEditorService _editorService;

        public EditorLifecycleCommandHandler(IScenarioEditorService editorService)
        {
            _editorService = editorService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null && actionId.StartsWith("editor.", StringComparison.Ordinal);
            message = null;
            if (!handled)
                return false;

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionSave:
                    return SaveDraft(state, out message);
                case ScenarioAuthoringActionIds.ActionPlaytest:
                    return TogglePlaytest(state, out message);
                case ScenarioAuthoringActionIds.ActionConvertToNormal:
                    _editorService.ConvertToNormalSave();
                    message = "Scenario binding converted to a normal save.";
                    return true;
                default:
                    handled = false;
                    return false;
            }
        }

        private bool SaveDraft(ScenarioAuthoringState state, out string message)
        {
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(null);
                if (validation != null && validation.IsValid)
                {
                    message = "Scenario draft saved.";
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

                ScenarioApplyResult result = _editorService.BeginPlaytest();
                message = BuildPlaytestStatus(result);
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
            handled = string.Equals(actionId, ScenarioAuthoringActionIds.ActionSelectionClear, StringComparison.Ordinal);
            message = null;
            if (!handled || state.SelectedTarget == null)
                return false;

            state.SelectedTarget = null;
            state.MultiSelection.Clear();
            message = "Selection cleared.";
            return true;
        }
    }

    internal sealed class AssetModeCommandHandler : IScenarioCommandHandler
    {
        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = actionId != null && actionId.StartsWith("asset.mode.", StringComparison.Ordinal);
            message = null;
            if (!handled)
                return false;

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionAssetModeReplace:
                    if (state.AssetMode == ScenarioAssetAuthoringMode.ReplaceExisting)
                        return false;
                    state.AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting;
                    message = "Asset workflow set to replace existing visuals.";
                    return true;
                case ScenarioAuthoringActionIds.ActionAssetModePlace:
                    if (state.AssetMode == ScenarioAssetAuthoringMode.PlaceNew)
                        return false;
                    state.AssetMode = ScenarioAssetAuthoringMode.PlaceNew;
                    message = "Asset workflow set to place new snapped sprites.";
                    return true;
                default:
                    handled = false;
                    return false;
            }
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
                    return SetTool(state, ScenarioAuthoringTool.Select, out message, "Selection tool active.");
                case ScenarioAuthoringActionIds.ActionToolFamily:
                    return SetTool(state, ScenarioAuthoringTool.Family, out message, "Family capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolInventory:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, out message, "Inventory capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolShelter:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, out message, "Structure placement tool active.");
                case ScenarioAuthoringActionIds.ActionToolAssets:
                    return SetTool(state, ScenarioAuthoringTool.Assets, out message, "Asset workflow active.");
                case ScenarioAuthoringActionIds.ActionToolObjects:
                    return SetTool(state, ScenarioAuthoringTool.Objects, out message, "Shelter object placement tool active.");
                case ScenarioAuthoringActionIds.ActionToolWiring:
                    return SetTool(state, ScenarioAuthoringTool.Wiring, out message, "Wall and wiring tool active.");
                case ScenarioAuthoringActionIds.ActionToolPeople:
                    return SetTool(state, ScenarioAuthoringTool.Family, out message, "Family capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolVehicle:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, out message, "Shelter object capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolWinLoss:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, out message, "Inventory capture tool active.");
                default:
                    handled = false;
                    return false;
            }
        }

        private bool SetTool(ScenarioAuthoringState state, ScenarioAuthoringTool tool, out string message, string statusMessage)
        {
            message = null;
            if (state.ActiveTool == tool)
                return false;

            state.ActiveTool = tool;
            if (tool == ScenarioAuthoringTool.Wiring)
                _layoutService.SelectStage(state, ScenarioStageKind.BunkerBackground);
            else if (tool == ScenarioAuthoringTool.Objects || tool == ScenarioAuthoringTool.Assets)
                _layoutService.SelectStage(state, ScenarioStageKind.BunkerInside);
            else if (tool == ScenarioAuthoringTool.Shelter || tool == ScenarioAuthoringTool.Select)
                _layoutService.SelectStage(state, ScenarioStageKind.BunkerSurface);
            else if (tool == ScenarioAuthoringTool.Family)
                _layoutService.SelectStage(state, ScenarioStageKind.People);
            else if (tool == ScenarioAuthoringTool.Inventory)
                _layoutService.SelectStage(state, ScenarioStageKind.InventoryStorage);

            message = statusMessage;
            return true;
        }
    }
}
