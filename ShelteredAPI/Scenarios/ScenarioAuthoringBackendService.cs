using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.InputActions;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioAuthoringBackendService : IScenarioAuthoringBackend
    {
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringSelectionService _selectionService;
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioSpriteSwapAuthoringService _spriteSwapAuthoringService;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacementAuthoringService;
        private readonly IScenarioEditorService _editorService;
        private ScenarioAuthoringState _state = new ScenarioAuthoringState();
        private ScenarioAuthoringSession _activeSession;

        public static ScenarioAuthoringBackendService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringBackendService>(); }
        }

        public event Action<ScenarioAuthoringState> StateChanged;

        public ScenarioAuthoringState CurrentState
        {
            get
            {
                lock (_sync)
                {
                    return _state.Copy();
                }
            }
        }

        internal ScenarioAuthoringBackendService(
            ScenarioAuthoringSelectionService selectionService,
            ScenarioAuthoringCaptureService captureService,
            ScenarioSpriteSwapAuthoringService spriteSwapAuthoringService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementAuthoringService,
            IScenarioEditorService editorService)
        {
            _selectionService = selectionService ?? new ScenarioAuthoringSelectionService();
            _captureService = captureService ?? ScenarioAuthoringCaptureService.Instance;
            _spriteSwapAuthoringService = spriteSwapAuthoringService ?? ScenarioSpriteSwapAuthoringService.Instance;
            _sceneSpritePlacementAuthoringService = sceneSpritePlacementAuthoringService ?? ScenarioSceneSpritePlacementAuthoringService.Instance;
            _editorService = editorService;
        }

        internal void SetActiveSession(ScenarioAuthoringSession session)
        {
            if (session == null)
                return;

            lock (_sync)
            {
                _activeSession = session;
                _state = new ScenarioAuthoringState
                {
                    IsActive = true,
                    ShellVisible = true,
                    SelectionModeActive = false,
                    ActiveTool = ScenarioAuthoringTool.Family,
                    AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting,
                    ActiveDraftId = session.DraftId,
                    ActiveScenarioFilePath = session.ScenarioFilePath,
                    StatusMessage = "Scenario authoring shell is active. Use playtest to make live shelter changes, then capture them back into the draft."
                };
            }

            ScenarioAuthoringSelectionMenuService.Instance.Reset();
            _spriteSwapAuthoringService.Invalidate();
            _sceneSpritePlacementAuthoringService.Invalidate();
            ScenarioAuthoringHistoryService.Instance.BindSession(session.DraftId);
            ScenarioSpriteSwapClipboard.Clear();
            ScenarioHoverVisualService.Instance.ClearSecondary();
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Active session set. DraftId=" + session.DraftId
                + ", ScenarioFile=" + session.ScenarioFilePath + ".");
            RaiseStateChanged();
        }

        internal void ClearActiveSession(string reason)
        {
            lock (_sync)
            {
                _activeSession = null;
                _state = new ScenarioAuthoringState
                {
                    IsActive = false,
                    StatusMessage = reason ?? string.Empty
                };
            }

            ScenarioHoverVisualService.Instance.Clear();
            ScenarioAuthoringSelectionMenuService.Instance.Reset();
            _spriteSwapAuthoringService.Invalidate();
            _sceneSpritePlacementAuthoringService.Invalidate();
            ScenarioAuthoringHistoryService.Instance.Reset();
            ScenarioSpriteSwapClipboard.Clear();
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Active session cleared. Reason=" + (reason ?? "unspecified") + ".");
            RaiseStateChanged();
        }

        internal void Update()
        {
            ScenarioAuthoringState snapshot;
            lock (_sync)
            {
                snapshot = _state.Copy();
            }

            if (snapshot == null || !snapshot.IsActive)
                return;

            bool changed = false;

            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.ToggleShell))
            {
                snapshot.ShellVisible = !snapshot.ShellVisible;
                snapshot.StatusMessage = snapshot.ShellVisible
                    ? "Authoring shell opened."
                    : "Authoring shell hidden.";
                changed = true;
            }

            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.SaveDraft))
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionSave);
            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.TogglePlaytest))
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionPlaytest);

            if (ScenarioAuthoringInputActions.IsUndoDown())
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionHistoryUndo);
            if (ScenarioAuthoringInputActions.IsRedoDown())
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionHistoryRedo);
            if (ScenarioAuthoringInputActions.IsCopyDown())
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapCopy);
            if (ScenarioAuthoringInputActions.IsPasteDown())
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapPaste);
            if (ScenarioAuthoringInputActions.IsRevertDown())
                changed |= ExecuteActionInternal(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapRevert);

            changed |= _selectionService.Update(snapshot);

            lock (_sync)
            {
                _state = snapshot;
            }

            if (changed)
                RaiseStateChanged();
        }

        public void Refresh()
        {
            RaiseStateChanged();
        }

        public bool ExecuteAction(string actionId)
        {
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionCloseEditor, StringComparison.Ordinal))
            {
                ScenarioAuthoringBootstrapService.Instance.RequestCloseActiveSession("Closed from authoring shell.", true);
                return true;
            }

            ScenarioAuthoringState snapshot;
            lock (_sync)
            {
                snapshot = _state.Copy();
            }

            if (snapshot == null || !snapshot.IsActive)
                return false;

            bool changed = ExecuteActionInternal(snapshot, actionId);
            lock (_sync)
            {
                _state = snapshot;
            }

            if (changed)
                RaiseStateChanged();
            return changed;
        }

        public ScenarioAuthoringInspectorDocument GetShellDocument()
        {
            ScenarioAuthoringState state = CurrentState;
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            ScenarioAuthoringSession session = GetActiveSession();
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            string selectedObjectStatus;
            bool canCaptureSelectedObject = _captureService.CanCaptureTarget(state.SelectedTarget, out selectedObjectStatus);
            bool hasCapturedSelectedObject = _captureService.HasCapturedPlacementForTarget(editorSession, state.SelectedTarget);

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "session",
                Title = "Session",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = new[]
                {
                    Property("Draft", Safe(state.ActiveDraftId)),
                    Property("Base Mode", session != null ? session.BaseMode.ToString() : "Unknown"),
                    Property("Draft Save", FormatDraftStorage(state.ActiveScenarioFilePath)),
                    Property("Scenario File", FormatScenarioFileName(state.ActiveScenarioFilePath)),
                    Property("Tool", state.ActiveTool.ToString()),
                    Property("Asset Mode", state.AssetMode.ToString()),
                    Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"),
                    Property("Simulation", ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Running (playtest)" : "Frozen (authoring pause)"),
                    Property("Applied To World", editorSession != null && editorSession.HasAppliedToCurrentWorld ? "Yes" : "No"),
                    Property("Dirty Sections", CountDirtyFlags(editorSession).ToString()),
                    Property("Sprite Swaps", CountSpriteSwaps(editorSession).ToString()),
                    Property("Placed Sprites", CountSceneSpritePlacements(editorSession).ToString())
                }
            });

            sections.Add(BuildWorkflowSection(editorSession));
            sections.Add(BuildHistorySection());
            sections.Add(BuildToolPickerSection(state.ActiveTool));
            sections.Add(BuildToolSection(
                state.ActiveTool,
                definition,
                state.SelectedTarget,
                canCaptureSelectedObject,
                hasCapturedSelectedObject,
                selectedObjectStatus));
            sections.Add(BuildSelectionSection(state));

            if (!string.IsNullOrEmpty(state.StatusMessage))
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "status",
                    Title = "Status",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[] { Text(state.StatusMessage) }
                });
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "controls",
                Title = "Controls",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    Text("Hold Ctrl to enter selection mode. Cyan outline follows the hovered target, yellow marks the selected target."),
                    Text("Left Click confirms the hovered target. Right Click clears the current selection."),
                    Text("F5 saves the draft, F6 toggles the shell, and F7 toggles playtest mode."),
                    Text("Ctrl+Z undoes, Ctrl+Y redoes, Ctrl+C copies, Ctrl+V pastes, Ctrl+R reverts the selected target's sprite."),
                    Text("Authoring pause freezes simulation without opening Sheltered's vanilla pause menu."),
                    Text("Use the Assets workflow to replace existing visuals or place new snapped scene sprites on the shelter map."),
                    Text("Use playtest to make real Sheltered changes in the shelter, then stop playtest and capture the live family, inventory, or spawned objects back into the draft.")
                }
            });

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Scenario Authoring",
                Subtitle = editorSession != null && editorSession.WorkingDefinition != null
                    ? editorSession.WorkingDefinition.DisplayName
                    : "No active definition",
                HeaderActions = BuildHeaderActions(editorSession, state.SelectedTarget != null),
                Sections = sections.ToArray()
            };
        }

        public ScenarioAuthoringInspectorDocument GetInspectorDocument()
        {
            ScenarioAuthoringState state = CurrentState;
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            ScenarioAuthoringTarget target = state.SelectedTarget ?? state.HoveredTarget;
            if (target == null)
            {
                return new ScenarioAuthoringInspectorDocument
                {
                    Title = "Selection Inspector",
                    Subtitle = "No target selected",
                    HeaderActions = new ScenarioAuthoringInspectorAction[0],
                    Sections = new[]
                    {
                        new ScenarioAuthoringInspectorSection
                        {
                            Id = "empty",
                            Title = "Inspector",
                            Expanded = true,
                            Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                            Items = new[]
                            {
                                Text("Hold the selection modifier and hover a world object to inspect it.")
                            }
                        }
                    }
                };
            }

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Text(
                Safe(target.DisplayName),
                Safe(target.GameObjectName),
                target.Kind.ToString(),
                "TG",
                ResolvePreviewSprite(target),
                true));
            items.Add(Property("Kind", target.Kind.ToString()));
            items.Add(Property("Name", Safe(target.DisplayName)));
            items.Add(Property("Object", Safe(target.GameObjectName)));
            items.Add(Property("Path", Safe(target.TransformPath)));
            items.Add(Property("Adapter", Safe(target.AdapterId)));
            items.Add(Property("World Position", FormatVector(target.WorldPosition)));
            if (!string.IsNullOrEmpty(target.Description))
                items.Add(Text(target.Description));

            List<ScenarioAuthoringInspectorItem> actionItems = new List<ScenarioAuthoringInspectorItem>();
            string captureReason;
            bool canCaptureTarget = _captureService.CanCaptureTarget(target, out captureReason);
            bool hasCapturedPlacement = _captureService.HasCapturedPlacementForTarget(_editorService.CurrentSession, target);
            if (canCaptureTarget)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject,
                    "Capture Selected Object",
                    "Store this live spawned shelter object as a scenario object placement.",
                    true,
                    true,
                    "CP",
                    "Persist this spawned object into the draft.")));
            }
            else if (!string.IsNullOrEmpty(captureReason))
            {
                actionItems.Add(Text(captureReason));
            }

            if (hasCapturedPlacement)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement,
                    "Remove Captured Placement",
                    "Remove this object's captured placement from the scenario draft.",
                    true,
                    false,
                    "RM",
                    "Delete this stored object capture.")));
            }

            if (target.SupportsReplace)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionToolObjects,
                    "Switch To Objects Tool",
                    "Open the shelter object capture workflow.",
                    true,
                    false,
                    "OB",
                    "Open shelter object capture tools.")));

                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionToolAssets,
                    "Switch To Assets Tool",
                    "Open the asset replacement and placement workflow for this target.",
                    true,
                    false,
                    "AS",
                    "Open the visual replacement workflow.")));
            }

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "target",
                Title = "Target",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            });
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "actions",
                Title = "Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = actionItems.Count > 0 ? actionItems.ToArray() : new[] { Text("This target does not have a scenario capture action yet.") }
            });

            List<ScenarioAuthoringInspectorSection> assetSections = BuildAssetSections(state, editorSession, target);
            for (int i = 0; i < assetSections.Count; i++)
                sections.Add(assetSections[i]);

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Target Inspector",
                Subtitle = target.DisplayName,
                HeaderActions = new[]
                {
                    Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current scenario target selection.", true, false)
                },
                Sections = sections.ToArray()
            };
        }

        public ScenarioAuthoringInspectorDocument GetHoverDocument()
        {
            ScenarioAuthoringState state = CurrentState;
            if (state == null || !state.SelectionModeActive || state.HoveredTarget == null)
                return null;

            ScenarioAuthoringTarget target = state.HoveredTarget;
            return new ScenarioAuthoringInspectorDocument
            {
                Title = target.DisplayName,
                Subtitle = target.Kind.ToString(),
                HeaderActions = new ScenarioAuthoringInspectorAction[0],
                Sections = new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "hover",
                        Title = "Hover",
                        Expanded = true,
                        Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                        Items = new[]
                        {
                            Property("Path", Safe(target.TransformPath)),
                            Property("Adapter", Safe(target.AdapterId)),
                            Text(string.IsNullOrEmpty(target.Description)
                                ? "Click to select this scenario target."
                                : target.Description),
                            Text(target.SupportsReplace
                                ? "Supports inspect and shelter capture workflows."
                                : "Supports inspect workflows.")
                        }
                    }
                }
            };
        }

        private bool ExecuteActionInternal(ScenarioAuthoringState state, string actionId)
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
                    return SetTool(state, ScenarioAuthoringTool.Shelter, "Shelter object capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolAssets:
                    return SetTool(state, ScenarioAuthoringTool.Assets, "Asset workflow active.");
                case ScenarioAuthoringActionIds.ActionToolObjects:
                    return SetTool(state, ScenarioAuthoringTool.Objects, "Shelter object capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolWiring:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, "Shelter object capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolPeople:
                    return SetTool(state, ScenarioAuthoringTool.Family, "Family capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolVehicle:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, "Shelter object capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolWinLoss:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, "Inventory capture tool active.");
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

        private ScenarioAuthoringInspectorAction[] BuildHeaderActions(ScenarioEditorSession editorSession, bool hasSelection)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSave, "Save", "Persist the current scenario draft XML.", true, true, "SV", "Write the current draft to scenario.xml."));
            actions.Add(Action(
                ScenarioAuthoringActionIds.ActionPlaytest,
                editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Playtest",
                "Toggle scenario playtest mode.",
                true,
                true,
                "PL",
                editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting
                    ? "End playtest and restore frozen authoring."
                    : "Start a live playtest from the current draft."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and release scene ownership.", true, false, "EX", "Leave the scenario editor."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current selected target.", hasSelection, false, "CL", "Drop the current target selection."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionConvertToNormal, "Convert Save", "Convert the current scenario-bound save into a normal save.", true, false, "CV", "Detach this save from the scenario editor."));
            return actions.ToArray();
        }

        private void RaiseStateChanged()
        {
            Action<ScenarioAuthoringState> handler = StateChanged;
            if (handler == null)
                return;

            try
            {
                handler(CurrentState);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioAuthoringBackend.StateChanged", ex.Message);
            }
        }

        private static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string hint,
            bool enabled,
            bool emphasized,
            string iconText = null,
            string detail = null,
            string badge = null,
            Sprite previewSprite = null)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Hint = hint,
                Detail = detail,
                Badge = badge,
                IconText = iconText,
                PreviewSprite = previewSprite,
                Enabled = enabled,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem Text(
            string value,
            string detail = null,
            string badge = null,
            string iconText = null,
            Sprite previewSprite = null,
            bool emphasized = false)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Text,
                Value = value,
                Detail = detail,
                Badge = badge,
                IconText = iconText,
                PreviewSprite = previewSprite,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem Property(
            string label,
            string value,
            string detail = null,
            string badge = null,
            string iconText = null,
            Sprite previewSprite = null,
            bool emphasized = false)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value,
                Detail = detail,
                Badge = badge,
                IconText = iconText,
                PreviewSprite = previewSprite,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem ActionItem(ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Action,
                Action = action
            };
        }

        private ScenarioAuthoringSession GetActiveSession()
        {
            lock (_sync)
            {
                return _activeSession;
            }
        }

        private static string FormatDraftStorage(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return "<none>";

            try
            {
                string directory = Path.GetDirectoryName(scenarioFilePath);
                if (string.IsNullOrEmpty(directory))
                    return "<none>";

                string leaf = Path.GetFileName(directory);
                return string.IsNullOrEmpty(leaf) ? directory : leaf;
            }
            catch
            {
                return scenarioFilePath;
            }
        }

        private static string FormatScenarioFileName(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return "<none>";

            try
            {
                string fileName = Path.GetFileName(scenarioFilePath);
                return string.IsNullOrEmpty(fileName) ? scenarioFilePath : fileName;
            }
            catch
            {
                return scenarioFilePath;
            }
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

        private static string FormatTarget(ScenarioAuthoringTarget target)
        {
            return target == null ? "<none>" : (target.DisplayName + " [" + target.Kind + "]");
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##") + ")";
        }

        private static Sprite ResolvePreviewSprite(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            ScenarioSpriteRuntimeResolver resolver = new ScenarioSpriteRuntimeResolver();
            ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget;
            return resolver.TryResolve(target, out resolvedTarget) && resolvedTarget != null
                ? resolvedTarget.CurrentSprite
                : null;
        }

        private static string CleanCandidateLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
                return "<sprite>";

            return label.EndsWith(" *", StringComparison.Ordinal)
                ? label.Substring(0, label.Length - 2)
                : label;
        }

        private static string BuildCandidateBadge(ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            if (candidate == null)
                return null;

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.ScenarioCustom)
                return "MOD";

            return "LIVE";
        }

        private static int CountSpriteSwaps(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.WorkingDefinition != null
                ? CountSpriteSwaps(editorSession.WorkingDefinition)
                : 0;
        }

        private static int CountSpriteSwaps(ScenarioDefinition definition)
        {
            if (definition == null || definition.AssetReferences == null)
                return 0;

            return definition.AssetReferences.SpriteSwaps != null
                ? definition.AssetReferences.SpriteSwaps.Count
                : 0;
        }

        private static int CountSceneSpritePlacements(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.WorkingDefinition != null
                ? CountSceneSpritePlacements(editorSession.WorkingDefinition)
                : 0;
        }

        private static int CountSceneSpritePlacements(ScenarioDefinition definition)
        {
            return definition != null && definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null
                ? definition.AssetReferences.SceneSpritePlacements.Count
                : 0;
        }

        private static int CountDirtyFlags(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.DirtyFlags != null
                ? editorSession.DirtyFlags.Count
                : 0;
        }

        private static int CountFamilyMembers(ScenarioDefinition definition)
        {
            return definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null
                ? definition.FamilySetup.Members.Count
                : 0;
        }

        private static int CountInventoryStacks(ScenarioDefinition definition)
        {
            return definition != null && definition.StartingInventory != null && definition.StartingInventory.Items != null
                ? definition.StartingInventory.Items.Count
                : 0;
        }

        private static int CountInventoryTotal(ScenarioDefinition definition)
        {
            if (definition == null || definition.StartingInventory == null || definition.StartingInventory.Items == null)
                return 0;

            int total = 0;
            for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry != null && entry.Quantity > 0)
                    total += entry.Quantity;
            }

            return total;
        }

        private static int CountObjectPlacements(ScenarioDefinition definition)
        {
            return definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null
                ? definition.BunkerEdits.ObjectPlacements.Count
                : 0;
        }

        private static string SummarizeFamily(ScenarioDefinition definition)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null || definition.FamilySetup.Members.Count == 0)
                return "No family snapshot captured yet.";

            List<string> names = new List<string>();
            for (int i = 0; i < definition.FamilySetup.Members.Count && names.Count < 4; i++)
            {
                FamilyMemberConfig member = definition.FamilySetup.Members[i];
                if (member != null && !string.IsNullOrEmpty(member.Name))
                    names.Add(member.Name);
            }

            string preview = names.Count > 0 ? string.Join(", ", names.ToArray()) : "Unnamed members";
            if (definition.FamilySetup.Members.Count > names.Count)
                preview += " +" + (definition.FamilySetup.Members.Count - names.Count);
            return preview;
        }

        private static string SummarizeInventory(ScenarioDefinition definition)
        {
            if (definition == null || definition.StartingInventory == null || definition.StartingInventory.Items == null || definition.StartingInventory.Items.Count == 0)
                return "No inventory snapshot captured yet.";

            List<string> parts = new List<string>();
            for (int i = 0; i < definition.StartingInventory.Items.Count && parts.Count < 4; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry != null && !string.IsNullOrEmpty(entry.ItemId) && entry.Quantity > 0)
                    parts.Add(entry.ItemId + " x" + entry.Quantity);
            }

            string preview = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Inventory captured";
            if (definition.StartingInventory.Items.Count > parts.Count)
                preview += " +" + (definition.StartingInventory.Items.Count - parts.Count);
            return preview;
        }

        private static string SummarizeObjectPlacements(ScenarioDefinition definition)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.ObjectPlacements == null || definition.BunkerEdits.ObjectPlacements.Count == 0)
                return "No spawned shelter objects captured yet.";

            List<string> parts = new List<string>();
            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count && parts.Count < 4; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement != null && !string.IsNullOrEmpty(placement.DefinitionReference))
                    parts.Add(placement.DefinitionReference);
            }

            string preview = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Object placements captured";
            if (definition.BunkerEdits.ObjectPlacements.Count > parts.Count)
                preview += " +" + (definition.BunkerEdits.ObjectPlacements.Count - parts.Count);
            return preview;
        }

        private List<ScenarioAuthoringInspectorSection> BuildAssetSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildAssetModeSection(state));

            if (state != null && state.AssetMode == ScenarioAssetAuthoringMode.PlaceNew)
            {
                List<ScenarioAuthoringInspectorSection> placementSections = BuildSceneSpritePlacementSections(state, editorSession, target);
                for (int i = 0; i < placementSections.Count; i++)
                    sections.Add(placementSections[i]);
            }
            else
            {
                List<ScenarioAuthoringInspectorSection> spriteSections = BuildSpriteSwapSections(state, editorSession, target);
                for (int i = 0; i < spriteSections.Count; i++)
                    sections.Add(spriteSections[i]);
            }

            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildAssetModeSection(ScenarioAuthoringState state)
        {
            ScenarioAssetAuthoringMode mode = state != null ? state.AssetMode : ScenarioAssetAuthoringMode.ReplaceExisting;
            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_mode",
                Title = "Asset Workflow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Property("Mode", mode.ToString()),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModeReplace, "Replace Existing", "Preview and persist a sprite swap on the selected visual target.", true, mode == ScenarioAssetAuthoringMode.ReplaceExisting, "RE", "Like-for-like runtime replacement.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModePlace, "Place New Snapped", "Create or update a snapped authored scene sprite placement.", true, mode == ScenarioAssetAuthoringMode.PlaceNew, "PL", "Snapped decorative scene placement."))
                }
            };
        }

        private List<ScenarioAuthoringInspectorSection> BuildSpriteSwapSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker = _spriteSwapAuthoringService.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (picker == null || picker.Target == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> summaryItems = new List<ScenarioAuthoringInspectorItem>();
            summaryItems.Add(Text(
                Safe(picker.Target.SpriteName),
                Safe(picker.Target.TextureName),
                picker.Target.Kind.ToString(),
                "SP",
                picker.Target.CurrentSprite,
                true));
            summaryItems.Add(Property("Component", picker.Target.Kind.ToString()));
            summaryItems.Add(Property("Current Sprite", Safe(picker.Target.SpriteName)));
            summaryItems.Add(Property("Current Map", Safe(picker.Target.TextureName)));
            summaryItems.Add(Property("Active Swap", Safe(picker.ActiveRuleSummary)));
            summaryItems.Add(Property("Compatibility", Safe(picker.CompatibilitySummary)));
            summaryItems.Add(Property("Stored As", Safe(picker.XmlPathHint)));
            summaryItems.Add(Property("Compatible Vanilla", CountCandidates(picker.VanillaCandidates).ToString()));
            summaryItems.Add(Property("Compatible Modded", CountCandidates(picker.ModdedCandidates).ToString()));
            bool clipboardHasRule = ScenarioSpriteSwapClipboard.HasRule;
            summaryItems.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapRevert,
                "Revert Sprite",
                "Remove the active sprite swap for this target and restore the baseline sprite immediately.",
                picker.HasActiveRule,
                false)));
            summaryItems.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapCopy,
                "Copy Swap",
                "Copy the active sprite swap on this target to the clipboard.",
                picker.HasActiveRule,
                false)));
            summaryItems.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPaste,
                "Paste Swap",
                clipboardHasRule ? "Paste the clipboard sprite swap onto this target." : "Clipboard is empty.",
                clipboardHasRule,
                clipboardHasRule)));
            summaryItems.Add(Text(Safe(picker.GuidanceMessage)));
            summaryItems.Add(Text("This follows the same serializer shape other scenario packs use: AssetReferences > SpriteSwaps > Swap."));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_swap",
                Title = "Sprite Swap",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = summaryItems.ToArray()
            });
            sections.Add(BuildSpriteCandidateSection("sprite_swap_vanilla", "Vanilla Sprites", picker.VanillaCandidates, "No verified vanilla/runtime sprites are currently available for this target family."));
            sections.Add(BuildSpriteCandidateSection("sprite_swap_modded", "Modded Sprites", picker.ModdedCandidates, "Custom sprite overrides are hidden in strict replacement mode."));
            return sections;
        }

        private List<ScenarioAuthoringInspectorSection> BuildSceneSpritePlacementSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSceneSpritePlacementAuthoringService.PlacementPickerModel picker = _sceneSpritePlacementAuthoringService.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (picker == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> summaryItems = new List<ScenarioAuthoringInspectorItem>();
            summaryItems.Add(Text(
                FormatTarget(target),
                target != null ? target.Kind.ToString() : "<none>",
                null,
                "AN",
                ResolvePreviewSprite(target),
                true));
            summaryItems.Add(Property("Anchor", FormatTarget(target)));
            summaryItems.Add(Property("Grid", target != null && target.GridX.HasValue && target.GridY.HasValue ? (target.GridX.Value + "," + target.GridY.Value) : "<none>"));
            summaryItems.Add(Property("Active Placement", picker.ActivePlacement != null ? Safe(picker.ActivePlacement.Id) : "<none>"));
            summaryItems.Add(Property("Compatibility", Safe(picker.CompatibilitySummary)));
            summaryItems.Add(Property("Stored As", Safe(picker.XmlPathHint)));
            summaryItems.Add(Property("Vanilla Options", CountCandidates(picker.VanillaCandidates).ToString()));
            summaryItems.Add(Property("Modded Options", CountCandidates(picker.ModdedCandidates).ToString()));
            summaryItems.Add(Text(Safe(picker.PlacementSummary)));
            summaryItems.Add(Text(Safe(picker.GuidanceMessage)));
            summaryItems.Add(Text("This follows the same serializer shape other scenario packs use: AssetReferences > SceneSpritePlacements > Placement."));
            summaryItems.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove,
                "Remove Placement",
                "Remove the selected authored scene sprite placement from the draft.",
                picker.ActivePlacement != null,
                false)));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "scene_sprite",
                Title = "Scene Sprite Placement",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = summaryItems.ToArray()
            });
            sections.Add(BuildPlacementCandidateSection("scene_sprite_vanilla", "Vanilla Sprites", picker.VanillaCandidates, "No loaded vanilla/runtime sprites are currently available for placement."));
            sections.Add(BuildPlacementCandidateSection("scene_sprite_modded", "Modded Sprites", picker.ModdedCandidates, "Custom sprite placements are hidden in strict placement mode."));
            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildSpriteCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Count", CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(Text(emptyMessage));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    bool active = candidate.Label != null && candidate.Label.EndsWith(" *", StringComparison.Ordinal);
                    items.Add(ActionItem(Action(
                        ScenarioSpriteSwapAuthoringService.BuildApplyActionId(candidate.Token),
                        CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        active,
                        "RT",
                        candidate.SourceName,
                        active ? "Active" : BuildCandidateBadge(candidate),
                        candidate.Sprite)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Count", CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(Text(emptyMessage));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    items.Add(ActionItem(Action(
                        ScenarioSceneSpritePlacementAuthoringService.BuildApplyActionId(candidate.Token),
                        CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        false,
                        "RT",
                        candidate.SourceName,
                        BuildCandidateBadge(candidate),
                        candidate.Sprite)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            };
        }

        private static int CountCandidates(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates)
        {
            return candidates != null ? candidates.Count : 0;
        }

        private static ScenarioAuthoringInspectorSection BuildSelectionSection(ScenarioAuthoringState state)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "selection",
                Title = "Selection",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = new[]
                {
                    Property("Selection Mode", state.SelectionModeActive ? "Active" : "Inactive"),
                    Property("Hovered", FormatTarget(state.HoveredTarget)),
                    Property("Selected", FormatTarget(state.SelectedTarget))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildHistorySection()
        {
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            bool canUndo = history.CanUndo;
            bool canRedo = history.CanRedo;
            bool clipboardHasRule = ScenarioSpriteSwapClipboard.HasRule;

            return new ScenarioAuthoringInspectorSection
            {
                Id = "history",
                Title = "History & Clipboard",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Property("Undo Depth", history.UndoDepth.ToString()),
                    Property("Redo Depth", history.RedoDepth.ToString()),
                    Property("Clipboard", ScenarioSpriteSwapClipboard.Describe()),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryUndo, "Undo (Ctrl+Z)", "Undo the last sprite swap change.", canUndo, false, "UN", "Rewind the last authored sprite change.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryRedo, "Redo (Ctrl+Y)", "Redo the last undone change.", canRedo, false, "RE", "Re-apply the last undone change.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Copy Swap (Ctrl+C)", "Copy the selected target's active sprite swap to the clipboard.", true, false, "CP", "Copy the selected sprite rule.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSpriteSwapPaste, "Paste Swap (Ctrl+V)", "Paste the clipboard sprite swap onto the selected target.", clipboardHasRule, clipboardHasRule, "PA", clipboardHasRule ? "Apply the copied rule to the current target." : "Clipboard is empty.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSpriteSwapRevert, "Revert Sprite (Ctrl+R)", "Remove the selected target's sprite swap and restore its original sprite.", true, false, "RV", "Clear the authored swap."))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildWorkflowSection(ScenarioEditorSession editorSession)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "workflow",
                Title = "Workflow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario XML.", true, false, "SV", "Write scenario.xml to the active draft.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionPlaytest,
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Start Playtest",
                        "Toggle simulation while keeping the live shelter editor session intact.",
                        true,
                        true,
                        "PL",
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting
                            ? "Return to frozen authoring mode."
                            : "Run the live shelter with the current draft.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and return the save to normal live play.", true, false, "EX", "Release the current authoring session."))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildToolPickerSection(ScenarioAuthoringTool activeTool)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "tools",
                Title = "Tools",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.TabStrip,
                Items = new[]
                {
                    Text("Tools are split by concern: capture family/inventory/object state separately from visual asset authoring."),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolFamily, "Family", "Capture the current live family roster, stats, and traits.", true, activeTool == ScenarioAuthoringTool.Family, "FM", "Family roster and stats.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolInventory, "Inventory", "Capture the current live shelter inventory.", true, activeTool == ScenarioAuthoringTool.Inventory, "IV", "Shelter inventory snapshot.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolObjects, "Shelter Objects", "Capture current live spawned shelter objects.", true, activeTool == ScenarioAuthoringTool.Shelter || activeTool == ScenarioAuthoringTool.Objects || activeTool == ScenarioAuthoringTool.Select, "OB", "Runtime shelter placements.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolAssets, "Assets", "Swap existing visuals or place new snapped scene sprites.", true, activeTool == ScenarioAuthoringTool.Assets, "AS", "Sprite replacements and scene art.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolSelect, "Select", "Stay in world selection mode while using the current workflow.", true, activeTool == ScenarioAuthoringTool.Select, "SL", "Selection-only mode."))
                }
            };
        }

        private ScenarioAuthoringInspectorSection BuildToolSection(
            ScenarioAuthoringTool activeTool,
            ScenarioDefinition definition,
            ScenarioAuthoringTarget selectedTarget,
            bool canCaptureSelectedObject,
            bool hasCapturedSelectedObject,
            string selectedObjectStatus)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            string title;
            switch (activeTool)
            {
                case ScenarioAuthoringTool.Family:
                    title = "Family";
                    items.Add(Property("Captured Members", CountFamilyMembers(definition).ToString()));
                    items.Add(Text(SummarizeFamily(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureFamily, "Capture Current Family", "Snapshot the live family roster, stats, and traits into the scenario.", true, true, "FM", "Capture live family state.")));
                    break;

                case ScenarioAuthoringTool.Inventory:
                    title = "Inventory";
                    items.Add(Property("Captured Stacks", CountInventoryStacks(definition).ToString()));
                    items.Add(Property("Total Items", CountInventoryTotal(definition).ToString()));
                    items.Add(Text(SummarizeInventory(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureInventory, "Capture Current Inventory", "Snapshot the live shelter inventory into the scenario.", true, true, "IV", "Capture live inventory.")));
                    break;

                case ScenarioAuthoringTool.Assets:
                    title = "Assets";
                    items.Add(Property("Sprite Swaps", CountSpriteSwaps(definition).ToString()));
                    items.Add(Property("Placed Sprites", CountSceneSpritePlacements(definition).ToString()));
                    items.Add(Property("Selected Target", FormatTarget(selectedTarget)));
                    items.Add(Property("Pack Layout", "Scenarios/<ScenarioName>/scenario.xml"));
                    items.Add(Property("Custom Sprite XML", "AssetReferences > CustomSprites > Sprite"));
                    items.Add(Property("Swap XML", "AssetReferences > SpriteSwaps > Swap"));
                    items.Add(Property("Placement XML", "AssetReferences > SceneSpritePlacements > Placement"));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModeReplace, "Replace Existing", "Selecting a sprite updates the currently selected visual target.", true, CurrentState.AssetMode == ScenarioAssetAuthoringMode.ReplaceExisting, "RE", "Strict family-based replacement.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModePlace, "Place New Snapped", "Selecting a sprite creates or updates a snapped authored scene sprite.", true, CurrentState.AssetMode == ScenarioAssetAuthoringMode.PlaceNew, "PL", "Snapped decorative placement.")));
                    items.Add(Text("Asset authoring now fails closed: verified in-game runtime art only, no silent size-based fallback."));
                    items.Add(Text("Use Replace Existing for like-for-like family swaps. Use Place New Snapped for visual-only scene dressing that stores Placement entries in the scenario XML."));
                    items.Add(Text("These labels mirror the serializer and Custom Scenarios guide so authored changes match how other scenario packs are structured."));
                    break;

                default:
                    title = "Shelter Objects";
                    items.Add(Property("Captured Placements", CountObjectPlacements(definition).ToString()));
                    items.Add(Text(SummarizeObjectPlacements(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureShelterObjects, "Capture All Spawned Objects", "Replace the scenario placement list with the current live spawned shelter objects.", true, true, "OB", "Capture every current shelter placement.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Selected Object", "Store the selected live shelter object as a scenario placement.", canCaptureSelectedObject, canCaptureSelectedObject, "CP", "Capture only the selected object.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Selected Capture", "Remove the selected object's captured placement from the scenario.", hasCapturedSelectedObject, false, "RM", "Delete the stored selected capture.")));
                    items.Add(Property("Selected Object", FormatTarget(selectedTarget)));
                    if (!string.IsNullOrEmpty(selectedObjectStatus))
                        items.Add(Text(selectedObjectStatus));
                    break;
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "tool",
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
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
