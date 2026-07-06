using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioWorkflowAuthoringContentBuilder
    {
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioSelectionScopeService _selectionScopeService;

        public ScenarioWorkflowAuthoringContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioSelectionScopeService selectionScopeService)
        {
            _sectionHub = sectionHub;
            _selectionScopeService = selectionScopeService;
        }

        public ScenarioAuthoringInspectorSection BuildWorkflowSection(ScenarioEditorSession editorSession)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "workflow",
                Title = "Workflow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario XML.", true, false, "SV", "Write scenario.xml to the active draft.")),
                    Item.ActionItem(Item.Action(
                        ScenarioAuthoringActionIds.ActionPlaytest,
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Start Playtest",
                        "Toggle simulation while keeping the live shelter editor session intact.",
                        true,
                        true,
                        "PL",
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting
                            ? "Return to frozen authoring mode."
                            : "Run the live shelter with the current draft.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and return the save to normal live play.", true, false, "EX", "Release the current authoring session."))
                }
            };
        }

        public ScenarioAuthoringInspectorSection BuildHistorySection()
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
                    Item.Property("Undo Depth", history.UndoDepth.ToString()),
                    Item.Property("Redo Depth", history.RedoDepth.ToString()),
                    Item.Property("Clipboard", ScenarioSpriteSwapClipboard.Describe()),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionHistoryUndo, "Undo Visual Edit (Ctrl+Z)", "Undo the last sprite swap visual edit.", canUndo, false, "UN", "Rewind the last authored visual change.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionHistoryRedo, "Redo Visual Edit (Ctrl+Y)", "Redo the last undone visual edit.", canRedo, false, "RE", "Re-apply the last undone visual change.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Copy Visual Swap (Ctrl+C)", "Copy the selected target's active sprite swap to the clipboard.", true, false, "CP", "Copy the selected visual rule.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSpriteSwapPaste, "Paste Visual Swap (Ctrl+V)", "Paste the clipboard sprite swap onto the selected target.", clipboardHasRule, clipboardHasRule, "PA", clipboardHasRule ? "Apply the copied visual rule to the current target." : "Clipboard is empty.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSpriteSwapRevert, "Revert Visual Swap (Ctrl+R)", "Remove the selected target's sprite swap and restore its original sprite.", true, false, "RV", "Clear the authored visual swap."))
                }
            };
        }

        public ScenarioAuthoringInspectorSection BuildToolPickerSection(ScenarioAuthoringTool activeTool)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "tools",
                Title = "Tools",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.TabStrip,
                Items = new[]
                {
                    Item.Text("Domains filter palettes and click priority while selection stays available."),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolFamily, "Family", "Capture the current live family roster, stats, and traits.", true, activeTool == ScenarioAuthoringTool.Family, "FM", "Family roster and stats.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolInventory, "Inventory", "Capture the current live shelter inventory.", true, activeTool == ScenarioAuthoringTool.Inventory, "IV", "Shelter inventory snapshot.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolShelter, "Structure", "Place new shelter rooms, ladders, and lights with vanilla build ghosts.", true, activeTool == ScenarioAuthoringTool.Shelter, "ST", "Shelter layout editing.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolObjects, "Objects", "Place workbenches, shelter systems, and furniture or capture live spawned objects.", true, activeTool == ScenarioAuthoringTool.Objects, "OB", "Interactive shelter objects.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolWiring, "Walls & Wiring", "Apply room wall and wiring sprites to the selected shelter tile.", true, activeTool == ScenarioAuthoringTool.Wiring, "WW", "Room finish editing.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolAssets, "Assets", "Swap existing visuals or place new snapped scene sprites.", true, activeTool == ScenarioAuthoringTool.Assets, "AS", "Sprite replacements and scene art.")),
                    Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionToolWinLoss, "Win/Loss", "Author scenario outcome conditions.", true, activeTool == ScenarioAuthoringTool.WinLoss, "WL", "Scenario outcome rules."))
                }
            };
        }

        public ScenarioAuthoringInspectorSection BuildSelectionSection(ScenarioAuthoringState state)
        {
            ScenarioTargetScope activeScope = _selectionScopeService.ResolveActiveScope(state);
            int stackCount = state != null && state.SelectionStack != null ? state.SelectionStack.Count : 0;
            string activeStackTarget = stackCount > 0
                ? "Target " + (UnityEngine.Mathf.Clamp(state.ActiveSelectionStackIndex, 0, stackCount - 1) + 1).ToString() + " of " + stackCount.ToString()
                : "No target under cursor";
            return new ScenarioAuthoringInspectorSection
            {
                Id = "selection",
                Title = "Selection",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = new[]
                {
                    Item.Property("Scope", ScenarioTargetClassifier.FormatScopeLabel(activeScope)),
                    Item.Property("Selection Mode", state.SelectionModeActive ? "Active" : "Inactive"),
                    Item.Property("Stack Target", activeStackTarget),
                    Item.Property("Hovered", Item.FormatTarget(state.HoveredTarget)),
                    Item.Property("Selected", Item.FormatTarget(state.SelectedTarget))
                }
            };
        }

        public ScenarioAuthoringInspectorSection BuildToolSection(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTool activeTool,
            ScenarioDefinition definition,
            ScenarioAuthoringTarget selectedTarget,
            bool canCaptureSelectedObject,
            bool hasCapturedSelectedObject,
            string selectedObjectStatus)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioBuildPlacementAuthoringService.StatusModel buildStatus =
                activeTool == ScenarioAuthoringTool.Shelter
                || activeTool == ScenarioAuthoringTool.Objects
                || activeTool == ScenarioAuthoringTool.Wiring
                || activeTool == ScenarioAuthoringTool.Select
                    ? _sectionHub.BuildPlacement.GetStatusModel(state, editorSession)
                    : null;
            string title;
            switch (activeTool)
            {
                case ScenarioAuthoringTool.Family:
                    title = "Family";
                    items.Add(Item.Property("Captured Members", Item.CountFamilyMembers(definition).ToString()));
                    items.Add(Item.Text(Item.SummarizeFamily(definition)));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCaptureFamily, "Capture Current Family", "Snapshot the live family roster, stats, and traits into the scenario.", true, true, "FM", "Capture live family state.")));
                    break;

                case ScenarioAuthoringTool.Inventory:
                    title = "Inventory";
                    items.Add(Item.Property("Captured Stacks", Item.CountInventoryStacks(definition).ToString()));
                    items.Add(Item.Property("Total Items", Item.CountInventoryTotal(definition).ToString()));
                    items.Add(Item.Text(Item.SummarizeInventory(definition)));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCaptureInventory, "Capture Current Inventory", "Snapshot the live shelter inventory into the scenario.", true, true, "IV", "Capture live inventory.")));
                    break;

                case ScenarioAuthoringTool.Assets:
                    title = "Assets";
                    bool showAdvancedAssetDetails = ShowAdvancedDetails(state);
                    items.Add(Item.Property("Sprite Swaps", Item.CountSpriteSwaps(definition).ToString()));
                    items.Add(Item.Property("Placed Sprites", Item.CountSceneSpritePlacements(definition).ToString()));
                    items.Add(Item.Property("Selected Target", Item.FormatTarget(selectedTarget)));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen, "Edit Selected Asset", "Open the selected target in the dedicated asset editor.", selectedTarget != null && selectedTarget.SupportsReplace, false, "ED", "Change the selected asset in its own editor window.")));
                    items.Add(Item.Text("Asset authoring uses verified in-game runtime art only."));
                    items.Add(Item.Text("Use Inspector for selected assets, or place snapped scene dressing from the Art placement browser."));
                    if (showAdvancedAssetDetails)
                    {
                        items.Add(Item.Property("Pack Layout", "Scenarios/<ScenarioName>/scenario.xml"));
                        items.Add(Item.Property("Custom Sprite XML", "AssetReferences > CustomSprites > Sprite"));
                        items.Add(Item.Property("Swap XML", "AssetReferences > SpriteSwaps > Swap"));
                        items.Add(Item.Property("Placement XML", "AssetReferences > SceneSpritePlacements > Placement"));
                    }
                    break;

                case ScenarioAuthoringTool.Shelter:
                    title = "Structure";
                    items.Add(Item.Property("Captured Objects", Item.CountObjectPlacements(definition).ToString()));
                    items.Add(Item.Property("Selected Room", Item.FormatTarget(selectedTarget)));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildStructureRoom, "Place Room Tile", "Start vanilla-style room placement for the scenario draft.", true, false, "RM", "Extend the shelter layout.")));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildStructureLadder, "Place Ladder", "Start vanilla-style ladder placement for the scenario draft.", true, false, "LD", "Connect shelter levels.")));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildStructureLight, "Place Room Light", "Start vanilla-style room-light placement for the scenario draft.", true, false, "LG", "Light a room tile.")));
                    AddBuildDeletionActions(items, selectedTarget, true, false, false);
                    AddCancelPlacement(items, buildStatus, "Stop the active structure preview without committing it.");
                    AddBuildStatus(items, buildStatus);
                    break;

                case ScenarioAuthoringTool.Objects:
                    title = "Objects";
                    items.Add(Item.Property("Captured Objects", Item.CountObjectPlacements(definition).ToString()));
                    items.Add(Item.Property("Current Pick", Item.FormatTarget(selectedTarget)));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCaptureShelterObjects, "Capture Objects", "Update the draft with the shelter objects currently in the world.", true, true, "OB", "Capture current shelter objects.")));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Pick", "Add the selected live shelter object to the draft.", canCaptureSelectedObject, canCaptureSelectedObject, "CP", "Capture only the selected object.")));
                    AddDeleteObjectAction(items, selectedTarget);
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Draft Capture (keeps object)", "Remove the selected object's captured placement from the scenario without deleting the live object.", hasCapturedSelectedObject, false, "DC", "Remove only the stored selected capture.")));
                    AddCancelPlacement(items, buildStatus, "Stop the active object preview without committing it.");
                    AddBuildStatus(items, buildStatus);
                    if (!string.IsNullOrEmpty(selectedObjectStatus))
                        items.Add(Item.Text(selectedObjectStatus));
                    break;

                case ScenarioAuthoringTool.Wiring:
                    title = "Walls & Wiring";
                    items.Add(Item.Property("Selected Room", Item.FormatTarget(selectedTarget)));
                    items.Add(Item.Property("Recorded Room Edits", definition != null && definition.BunkerEdits != null ? definition.BunkerEdits.RoomChanges.Count.ToString() : "0"));
                    AddBuildDeletionActions(items, selectedTarget, false, true, true);
                    AddBuildStatus(items, buildStatus);
                    items.Add(Item.Text("Pick a room tile, then choose wall or wiring variants from the palette."));
                    break;

                case ScenarioAuthoringTool.WinLoss:
                    title = "Victory";
                    ScenarioScoringAuthoringSummary.Summary scoring = ScenarioScoringAuthoringSummary.Build(definition);
                    AddVictoryAuthoringItems(items, definition);
                    items.Add(Item.Property("Scoring", scoring.IsEnabled ? "Enabled" : "Disabled"));
                    items.Add(Item.Property("Score Label", scoring.ScoreLabel));
                    items.Add(Item.Property("Score Categories", scoring.CategoryCount.ToString()));
                    items.Add(Item.Property("Score Rules", scoring.RuleCount.ToString()));
                    break;

                case ScenarioAuthoringTool.Select:
                    title = "Selection";
                    items.Add(Item.Property("Selected Target", Item.FormatTarget(selectedTarget)));
                    items.Add(Item.Text("Selection mode is active for inspecting world objects, rooms, and authored sprites."));
                    items.Add(Item.Text("Structure, Objects, and Walls & Wiring expose palettes in the Build Palette window."));
                    break;

                default:
                    title = "Shelter";
                    items.Add(Item.Property("Captured Placements", Item.CountObjectPlacements(definition).ToString()));
                    items.Add(Item.Text(Item.SummarizeObjectPlacements(definition)));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCaptureShelterObjects, "Capture Objects", "Update the draft with the shelter objects currently in the world.", true, true, "OB", "Capture current shelter objects.")));
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Pick", "Add the selected live shelter object to the draft.", canCaptureSelectedObject, canCaptureSelectedObject, "CP", "Capture only the selected object.")));
                    AddDeleteObjectAction(items, selectedTarget);
                    items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Draft Capture (keeps object)", "Remove the selected object's captured placement from the scenario without deleting the live object.", hasCapturedSelectedObject, false, "DC", "Remove only the stored selected capture.")));
                    items.Add(Item.Property("Current Pick", Item.FormatTarget(selectedTarget)));
                    if (!string.IsNullOrEmpty(selectedObjectStatus))
                        items.Add(Item.Text(selectedObjectStatus));
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

        private static void AddVictoryAuthoringItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition)
        {
            WinLossConditionsDefinition winLoss = definition != null ? definition.WinLossConditions : null;
            int winCount = winLoss != null && winLoss.WinConditions != null ? winLoss.WinConditions.Count : 0;
            int lossCount = winLoss != null && winLoss.LossConditions != null ? winLoss.LossConditions.Count : 0;
            items.Add(Item.Property("Win Conditions", winCount.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Loss Conditions", lossCount.ToString(CultureInfo.InvariantCulture)));
            if (winCount + lossCount == 0)
                items.Add(Item.Text("No victory condition - scenario runs forever."));
            items.Add(Item.ActionItem(Item.Action(ScenarioWinLossAuthoringActionIds.AddWin, "Add Victory Condition", "Create a runtime-backed win condition.", definition != null, false, "W+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioWinLossAuthoringActionIds.AddLoss, "Add Failure Condition", "Create a runtime-backed loss condition.", definition != null, false, "L+")));
            AddConditionList(items, winLoss != null ? winLoss.WinConditions : null, true);
            AddConditionList(items, winLoss != null ? winLoss.LossConditions : null, false);
            AddSupportedConditionSummary(items);
        }

        private static void AddConditionList(List<ScenarioAuthoringInspectorItem> items, List<ConditionDef> conditions, bool win)
        {
            string label = win ? "Victory" : "Failure";
            string typePrefix = win ? ScenarioWinLossAuthoringActionIds.TypeWinPrefix : ScenarioWinLossAuthoringActionIds.TypeLossPrefix;
            string deletePrefix = win ? ScenarioWinLossAuthoringActionIds.DeleteWinPrefix : ScenarioWinLossAuthoringActionIds.DeleteLossPrefix;
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ConditionDef condition = conditions[i];
                ScenarioWinLossConditionDescriptor descriptor = null;
                if (condition != null)
                    ScenarioWinLossConditionSupport.TryGetDescriptor(condition.Type, out descriptor);
                string id = condition != null ? condition.Id : null;
                items.Add(Item.Property(label + " " + i.ToString(CultureInfo.InvariantCulture), Item.Safe(id) + " / " + (descriptor != null ? descriptor.Label : "Unsupported: " + (condition != null ? condition.Type : string.Empty))));
                if (descriptor != null)
                    items.Add(Item.Text(descriptor.Summary));
                items.Add(Item.ActionItem(Item.Action(typePrefix + i.ToString(CultureInfo.InvariantCulture), "Condition Type", "Cycle to the next runtime-supported condition type.", condition != null, false, "TY", descriptor != null ? descriptor.Label : "Unsupported type")));
                AddConditionFieldItems(items, condition, descriptor, i, win);
                items.Add(Item.ActionItem(Item.Action(deletePrefix + i.ToString(CultureInfo.InvariantCulture), "Delete " + label, "Remove this outcome condition.", condition != null, false, "DEL")));
            }
        }

        private static void AddConditionFieldItems(
            List<ScenarioAuthoringInspectorItem> items,
            ConditionDef condition,
            ScenarioWinLossConditionDescriptor descriptor,
            int index,
            bool win)
        {
            if (condition == null || descriptor == null)
                return;

            if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Time)
            {
                string dayPrefix = win ? ScenarioWinLossAuthoringActionIds.DayWinPrefix : ScenarioWinLossAuthoringActionIds.DayLossPrefix;
                string hourPrefix = win ? ScenarioWinLossAuthoringActionIds.HourWinPrefix : ScenarioWinLossAuthoringActionIds.HourLossPrefix;
                string minutePrefix = win ? ScenarioWinLossAuthoringActionIds.MinuteWinPrefix : ScenarioWinLossAuthoringActionIds.MinuteLossPrefix;
                items.Add(Item.Property("Day", ScenarioPropertyBag.GetInt(condition.Properties, "day", ScenarioPropertyBag.GetInt(condition.Properties, "days", 1)).ToString(CultureInfo.InvariantCulture)));
                AddStepper(items, dayPrefix, index, "Day", 1, 7);
                items.Add(Item.Property("Hour", ScenarioPropertyBag.GetInt(condition.Properties, "hour", 0).ToString(CultureInfo.InvariantCulture)));
                AddStepper(items, hourPrefix, index, "Hour", 1, 6);
                items.Add(Item.Property("Minute", ScenarioPropertyBag.GetInt(condition.Properties, "minute", 0).ToString(CultureInfo.InvariantCulture)));
                AddStepper(items, minutePrefix, index, "Minute", 5, 15);
                return;
            }

            if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Quantity)
            {
                string quantityPrefix = win ? ScenarioWinLossAuthoringActionIds.QuantityWinPrefix : ScenarioWinLossAuthoringActionIds.QuantityLossPrefix;
                items.Add(Item.Property("Item Id", Item.Safe(ScenarioPropertyBag.FirstString(condition.Properties, "itemId", "targetId"))));
                items.Add(Item.Property("Quantity", ScenarioPropertyBag.GetInt(condition.Properties, "quantity", 1).ToString(CultureInfo.InvariantCulture)));
                AddStepper(items, quantityPrefix, index, "Quantity", 1, 10);
                return;
            }

            if (descriptor.FieldKind == ScenarioWinLossConditionFieldKind.Flag)
            {
                items.Add(Item.Property("Flag Id", Item.Safe(ScenarioPropertyBag.FirstString(condition.Properties, "flagId", "targetId"))));
                items.Add(Item.Property("Flag Value", Item.Safe(ScenarioPropertyBag.FirstString(condition.Properties, "flagValue", "value"))));
                items.Add(Item.Text("Flag id/value are loaded from XML today; the runtime honors them but this surface does not yet include text entry."));
                return;
            }

            items.Add(Item.Property("Target Id", Item.Safe(ScenarioPropertyBag.FirstString(condition.Properties, "questId", "survivorId", "name", "bunkerExpansionId", "triggerId", "targetId"))));
            items.Add(Item.Text("Target ids are loaded from XML today; the runtime honors this type but this surface does not yet include text entry."));
        }

        private static void AddStepper(List<ScenarioAuthoringInspectorItem> items, string prefix, int index, string label, int small, int large)
        {
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            items.Add(Item.ActionItem(Item.Action(prefix + indexText + "." + (-large).ToString(CultureInfo.InvariantCulture), label + " -" + large.ToString(CultureInfo.InvariantCulture), "Decrease " + label.ToLowerInvariant() + ".", true, false, "-" + large.ToString(CultureInfo.InvariantCulture))));
            items.Add(Item.ActionItem(Item.Action(prefix + indexText + "." + (-small).ToString(CultureInfo.InvariantCulture), label + " -" + small.ToString(CultureInfo.InvariantCulture), "Decrease " + label.ToLowerInvariant() + ".", true, false, "-" + small.ToString(CultureInfo.InvariantCulture))));
            items.Add(Item.ActionItem(Item.Action(prefix + indexText + "." + small.ToString(CultureInfo.InvariantCulture), label + " +" + small.ToString(CultureInfo.InvariantCulture), "Increase " + label.ToLowerInvariant() + ".", true, false, "+" + small.ToString(CultureInfo.InvariantCulture))));
            items.Add(Item.ActionItem(Item.Action(prefix + indexText + "." + large.ToString(CultureInfo.InvariantCulture), label + " +" + large.ToString(CultureInfo.InvariantCulture), "Increase " + label.ToLowerInvariant() + ".", true, false, "+" + large.ToString(CultureInfo.InvariantCulture))));
        }

        private static void AddSupportedConditionSummary(List<ScenarioAuthoringInspectorItem> items)
        {
            ScenarioWinLossConditionDescriptor[] descriptors = ScenarioWinLossConditionSupport.GetDescriptors();
            string labels = string.Empty;
            for (int i = 0; descriptors != null && i < descriptors.Length; i++)
            {
                if (descriptors[i] == null)
                    continue;
                labels = labels.Length == 0 ? descriptors[i].Label : labels + ", " + descriptors[i].Label;
            }

            items.Add(Item.Text("Supported runtime condition types: " + labels + "."));
        }

        private static void AddBuildStatus(List<ScenarioAuthoringInspectorItem> items, ScenarioBuildPlacementAuthoringService.StatusModel buildStatus)
        {
            if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Guidance))
                items.Add(Item.Text(buildStatus.Guidance));
            if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Detail))
                items.Add(Item.Text(buildStatus.Detail));
            if (buildStatus != null && buildStatus.PlacementActive)
            {
                if (!string.IsNullOrEmpty(buildStatus.TargetCell))
                    items.Add(Item.Property("Target Cell", buildStatus.TargetCell));
                if (buildStatus.CanPlace.HasValue)
                    items.Add(Item.Property("Placement", buildStatus.CanPlace.Value ? "Valid" : "Invalid"));
                if (!string.IsNullOrEmpty(buildStatus.ValidationReason))
                    items.Add(Item.Text(buildStatus.ValidationReason));
            }
        }

        private void AddDeleteObjectAction(List<ScenarioAuthoringInspectorItem> items, ScenarioAuthoringTarget selectedTarget)
        {
            string reason;
            bool canDelete = _sectionHub.BuildPlacement.CanDeleteObject(selectedTarget, out reason);
            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionBuildDeleteObject,
                "Delete Live Object + Draft",
                "Remove the selected live object through ObjectManager and remove its draft ObjectPlacement.",
                canDelete,
                false,
                "DL",
                canDelete ? "Delete the object and its scenario placement." : reason)));
        }

        private void AddBuildDeletionActions(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioAuthoringTarget selectedTarget,
            bool structureDeletes,
            bool resetWall,
            bool resetWire)
        {
            string reason;
            if (structureDeletes)
            {
                bool canDeleteRoom = _sectionHub.BuildPlacement.CanDeleteRoom(selectedTarget, out reason);
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildDeleteRoom, "Delete Room + Draft", "Remove the selected room tile and dependent authored room placements.", canDeleteRoom, false, "DR", canDeleteRoom ? "Delete the room tile and matching draft records." : reason)));

                bool canDeleteLadder = _sectionHub.BuildPlacement.CanDeleteLadder(selectedTarget, out reason);
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildDeleteLadder, "Delete Ladder + Draft", "Remove the ladder from the selected top cell and remove its draft placement.", canDeleteLadder, false, "DL", canDeleteLadder ? "Delete the ladder and matching draft record." : reason)));

                bool canDeleteLight = _sectionHub.BuildPlacement.CanDeleteLight(selectedTarget, out reason);
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildDeleteLight, "Delete Light + Draft", "Remove the room light from the selected cell and remove its draft placement.", canDeleteLight, false, "DG", canDeleteLight ? "Delete the light and matching draft record." : reason)));
            }

            if (resetWall)
            {
                bool canResetWall = _sectionHub.BuildPlacement.CanResetWall(selectedTarget, out reason);
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildResetWall, "Reset Wall (persist clear)", "Reset the selected room wall to the default wall and store an authored clear.", canResetWall, false, "RW", canResetWall ? "Persist a wall clear for this room." : reason)));
            }

            if (resetWire)
            {
                bool canResetWire = _sectionHub.BuildPlacement.CanResetWire(selectedTarget, out reason);
                items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionBuildResetWire, "Reset Wire (persist clear)", "Clear the selected room wiring and store an authored no-wire state.", canResetWire, false, "RX", canResetWire ? "Persist a no-wire clear for this room." : reason)));
            }
        }

        private static void AddCancelPlacement(List<ScenarioAuthoringInspectorItem> items, ScenarioBuildPlacementAuthoringService.StatusModel buildStatus, string hint)
        {
            if (buildStatus == null || !buildStatus.CanCancel)
                return;

            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionBuildPlacementCancel,
                "Cancel Placement",
                hint,
                true,
                false,
                "CX",
                "Clear the active ghost preview.")));
        }

        private static bool ShowAdvancedDetails(ScenarioAuthoringState state)
        {
            return state != null
                && state.Settings != null
                && state.Settings.GetBool("debug.show_advanced_details", false);
        }

    }
}
