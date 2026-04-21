using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Inspector;
using ModAPI.InputActions;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    public sealed class ScenarioAuthoringBackendService : IScenarioAuthoringBackend
    {
        private static readonly ScenarioAuthoringBackendService _instance = new ScenarioAuthoringBackendService();
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringSelectionService _selectionService = new ScenarioAuthoringSelectionService();
        private readonly ScenarioAuthoringCaptureService _captureService = ScenarioAuthoringCaptureService.Instance;
        private ScenarioAuthoringState _state = new ScenarioAuthoringState();
        private ScenarioAuthoringSession _activeSession;

        public static ScenarioAuthoringBackendService Instance
        {
            get { return _instance; }
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

        private ScenarioAuthoringBackendService()
        {
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
                    ActiveDraftId = session.DraftId,
                    ActiveScenarioFilePath = session.ScenarioFilePath,
                    StatusMessage = "Scenario authoring shell is active. Use playtest to make live shelter changes, then capture them back into the draft."
                };
            }

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

            BoundsHighlighter.Target = null;
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
            ScenarioEditorSession editorSession = ScenarioEditorController.Instance.CurrentSession;
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
                Items = new[]
                {
                    Property("Draft", Safe(state.ActiveDraftId)),
                    Property("Base Mode", session != null ? session.BaseMode.ToString() : "Unknown"),
                    Property("Draft Save", FormatDraftStorage(state.ActiveScenarioFilePath)),
                    Property("Scenario File", FormatScenarioFileName(state.ActiveScenarioFilePath)),
                    Property("Tool", state.ActiveTool.ToString()),
                    Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"),
                    Property("Simulation", ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Running (playtest)" : "Frozen (authoring pause)"),
                    Property("Applied To World", editorSession != null && editorSession.HasAppliedToCurrentWorld ? "Yes" : "No"),
                    Property("Dirty Sections", CountDirtyFlags(editorSession).ToString()),
                    Property("Sprite Swaps", CountSpriteSwaps(editorSession).ToString())
                }
            });

            sections.Add(BuildWorkflowSection(editorSession));
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
                    Items = new[] { Text(state.StatusMessage) }
                });
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "controls",
                Title = "Controls",
                Expanded = true,
                Items = new[]
                {
                    Text("Hold Ctrl to enter selection mode."),
                    Text("Left Click confirms the hovered target. Right Click clears the current selection."),
                    Text("F5 saves the draft, F6 toggles the shell, and F7 toggles playtest mode."),
                    Text("Authoring pause freezes simulation without opening Sheltered's vanilla pause menu."),
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
                            Items = new[]
                            {
                                Text("Hold the selection modifier and hover a world object to inspect it.")
                            }
                        }
                    }
                };
            }

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
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
            bool hasCapturedPlacement = _captureService.HasCapturedPlacementForTarget(ScenarioEditorController.Instance.CurrentSession, target);
            if (canCaptureTarget)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject,
                    "Capture Selected Object",
                    "Store this live spawned shelter object as a scenario object placement.",
                    true,
                    true)));
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
                    false)));
            }

            if (target.SupportsReplace)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionToolObjects,
                    "Switch To Objects Tool",
                    "Open the shelter object capture workflow.",
                    true,
                    false)));
            }

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Target Inspector",
                Subtitle = target.DisplayName,
                HeaderActions = new[]
                {
                    Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current scenario target selection.", true, false)
                },
                Sections = new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "target",
                        Title = "Target",
                        Expanded = true,
                        Items = items.ToArray()
                    },
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "actions",
                        Title = "Actions",
                        Expanded = true,
                        Items = actionItems.Count > 0 ? actionItems.ToArray() : new[] { Text("This target does not have a scenario capture action yet.") }
                    }
                }
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

            switch (actionId)
            {
                case ScenarioAuthoringActionIds.ActionShellToggle:
                    state.ShellVisible = !state.ShellVisible;
                    state.StatusMessage = state.ShellVisible ? "Authoring shell opened." : "Authoring shell hidden.";
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
                    ScenarioEditorController.Instance.ConvertToNormalSave();
                    state.StatusMessage = "Scenario binding converted to a normal save.";
                    return true;

                case ScenarioAuthoringActionIds.ActionSelectionClear:
                    if (state.SelectedTarget == null)
                        return false;
                    state.SelectedTarget = null;
                    state.StatusMessage = "Selection cleared.";
                    return true;

                case ScenarioAuthoringActionIds.ActionToolSelect:
                    return SetTool(state, ScenarioAuthoringTool.Select, "Selection tool active.");
                case ScenarioAuthoringActionIds.ActionToolFamily:
                    return SetTool(state, ScenarioAuthoringTool.Family, "Family capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolInventory:
                    return SetTool(state, ScenarioAuthoringTool.Inventory, "Inventory capture tool active.");
                case ScenarioAuthoringActionIds.ActionToolShelter:
                    return SetTool(state, ScenarioAuthoringTool.Shelter, "Shelter object capture tool active.");
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

        private static bool SaveDraft(ScenarioAuthoringState state)
        {
            try
            {
                ScenarioValidationResult validation = ScenarioEditorController.Instance.CommitChanges(null);
                if (validation != null && validation.IsValid)
                {
                    state.StatusMessage = "Scenario draft saved.";
                    return true;
                }

                state.StatusMessage = "Scenario draft save failed validation.";
                return true;
            }
            catch (Exception ex)
            {
                state.StatusMessage = "Scenario draft save failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioAuthoringBackend] Save failed: " + ex.Message);
                return true;
            }
        }

        private static bool TogglePlaytest(ScenarioAuthoringState state)
        {
            try
            {
                ScenarioEditorSession editorSession = ScenarioEditorController.Instance.CurrentSession;
                if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                {
                    ScenarioEditorController.Instance.EndPlaytest();
                    state.StatusMessage = "Playtest ended. Authoring pause restored.";
                    return true;
                }

                ScenarioApplyResult result = ScenarioEditorController.Instance.BeginPlaytest();
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
            bool captured = _captureService.CaptureCurrentFamily(ScenarioEditorController.Instance.CurrentSession, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool CaptureCurrentInventory(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureCurrentInventory(ScenarioEditorController.Instance.CurrentSession, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool CaptureShelterObjects(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureCurrentShelterObjects(ScenarioEditorController.Instance.CurrentSession, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool CaptureSelectedObject(ScenarioAuthoringState state)
        {
            string message;
            bool captured = _captureService.CaptureSelectedObject(ScenarioEditorController.Instance.CurrentSession, state.SelectedTarget, out message);
            state.StatusMessage = message;
            return captured || !string.IsNullOrEmpty(message);
        }

        private bool RemoveSelectedObjectPlacement(ScenarioAuthoringState state)
        {
            string message;
            bool removed = _captureService.RemoveSelectedObjectPlacement(ScenarioEditorController.Instance.CurrentSession, state.SelectedTarget, out message);
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
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSave, "Save", "Persist the current scenario draft XML.", true, true));
            actions.Add(Action(
                ScenarioAuthoringActionIds.ActionPlaytest,
                editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Playtest",
                "Toggle scenario playtest mode.",
                true,
                true));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and release scene ownership.", true, false));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current selected target.", hasSelection, false));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionConvertToNormal, "Convert Save", "Convert the current scenario-bound save into a normal save.", true, false));
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

        private static ScenarioAuthoringInspectorAction Action(string id, string label, string hint, bool enabled, bool emphasized)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Hint = hint,
                Enabled = enabled,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem Text(string value)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Text,
                Value = value
            };
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value
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

        private static int CountSpriteSwaps(ScenarioEditorSession editorSession)
        {
            if (editorSession == null || editorSession.WorkingDefinition == null || editorSession.WorkingDefinition.AssetReferences == null)
                return 0;

            return editorSession.WorkingDefinition.AssetReferences.SpriteSwaps != null
                ? editorSession.WorkingDefinition.AssetReferences.SpriteSwaps.Count
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

        private static ScenarioAuthoringInspectorSection BuildSelectionSection(ScenarioAuthoringState state)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "selection",
                Title = "Selection",
                Expanded = true,
                Items = new[]
                {
                    Property("Selection Mode", state.SelectionModeActive ? "Active" : "Inactive"),
                    Property("Hovered", FormatTarget(state.HoveredTarget)),
                    Property("Selected", FormatTarget(state.SelectedTarget))
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
                Items = new[]
                {
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario XML.", true, false)),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionPlaytest,
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Start Playtest",
                        "Toggle simulation while keeping the live shelter editor session intact.",
                        true,
                        true)),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and return the save to normal live play.", true, false))
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
                Items = new[]
                {
                    Text("This first pass is focused on live family, inventory, and spawned shelter object capture."),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolFamily, "Family", "Capture the current live family roster, stats, and traits.", true, activeTool == ScenarioAuthoringTool.Family)),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolInventory, "Inventory", "Capture the current live shelter inventory.", true, activeTool == ScenarioAuthoringTool.Inventory)),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolObjects, "Shelter Objects", "Capture current live spawned shelter objects.", true, activeTool == ScenarioAuthoringTool.Shelter || activeTool == ScenarioAuthoringTool.Objects || activeTool == ScenarioAuthoringTool.Select)),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolSelect, "Select", "Stay in world selection mode while using the current workflow.", true, activeTool == ScenarioAuthoringTool.Select))
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
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureFamily, "Capture Current Family", "Snapshot the live family roster, stats, and traits into the scenario.", true, true)));
                    break;

                case ScenarioAuthoringTool.Inventory:
                    title = "Inventory";
                    items.Add(Property("Captured Stacks", CountInventoryStacks(definition).ToString()));
                    items.Add(Property("Total Items", CountInventoryTotal(definition).ToString()));
                    items.Add(Text(SummarizeInventory(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureInventory, "Capture Current Inventory", "Snapshot the live shelter inventory into the scenario.", true, true)));
                    break;

                default:
                    title = "Shelter Objects";
                    items.Add(Property("Captured Placements", CountObjectPlacements(definition).ToString()));
                    items.Add(Text(SummarizeObjectPlacements(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureShelterObjects, "Capture All Spawned Objects", "Replace the scenario placement list with the current live spawned shelter objects.", true, true)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Selected Object", "Store the selected live shelter object as a scenario placement.", canCaptureSelectedObject, canCaptureSelectedObject)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Selected Capture", "Remove the selected object's captured placement from the scenario.", hasCapturedSelectedObject, false)));
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
                Items = items.ToArray()
            };
        }
    }
}
