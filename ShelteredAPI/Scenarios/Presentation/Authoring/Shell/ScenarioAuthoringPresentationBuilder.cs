using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Bunker;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Presentation.Timeline;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioAuthoringPresentationBuilder
    {
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioAuthoringWindowRegistry _windowRegistry;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioSpriteRuntimeResolver _runtimeResolver;
        private readonly ShellChromeViewModelBuilder _shellChromeBuilder;
        private readonly StageNavigationViewModelBuilder _stageNavigationBuilder;
        private readonly InspectorViewModelBuilder _inspectorViewModelBuilder;
        private readonly StatusBarViewModelBuilder _statusBarViewModelBuilder;
        private readonly ScenarioSelectionScopeService _selectionScopeService;
        private readonly ScenarioTargetClassifier _targetClassifier;
        private readonly ScenarioAssetAuthoringContentBuilder _assetAuthoringContentBuilder;
        private readonly ScenarioMapAuthoringContentBuilder _mapAuthoringContentBuilder;
        private readonly ScenarioQuestAuthoringContentBuilder _questAuthoringContentBuilder;
        private readonly ScenarioWorkflowAuthoringContentBuilder _workflowAuthoringContentBuilder;
        private readonly ScenarioOverviewAuthoringContentBuilder _scenarioOverviewAuthoringContentBuilder;
        private readonly ScenarioRuntimeTestAuthoringContentBuilder _runtimeTestAuthoringContentBuilder;
        private readonly ScenarioHierarchyAuthoringContentBuilder _hierarchyAuthoringContentBuilder;
        private readonly ScenarioSelectionStackAuthoringContentBuilder _selectionStackAuthoringContentBuilder;
        private readonly ScenarioPublishAuthoringContentBuilder _publishAuthoringContentBuilder;
        private readonly ScenarioTimelineAuthoringContentBuilder _timelineAuthoringContentBuilder;
        private readonly ScenarioAuthoringTutorialService _tutorialService;
        private readonly ScenarioHelpAuthoringContentBuilder _helpAuthoringContentBuilder;
        private readonly Dictionary<ScenarioAuthoringWindowContentKind, IScenarioAuthoringWindowContentBuilder> _windowSectionBuilders;

        public ScenarioAuthoringPresentationBuilder(
            ScenarioAuthoringCaptureService captureService,
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioAuthoringWindowRegistry windowRegistry,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioSpriteRuntimeResolver runtimeResolver,
            ShellChromeViewModelBuilder shellChromeBuilder,
            StageNavigationViewModelBuilder stageNavigationBuilder,
            InspectorViewModelBuilder inspectorViewModelBuilder,
            StatusBarViewModelBuilder statusBarViewModelBuilder,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineViewModelBuilder timelineViewModelBuilder,
            ScenarioModDependencyDetector modDependencyDetector,
            ScenarioModCompatibilityViewModelBuilder modCompatibilityViewModelBuilder,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioTargetClassifier targetClassifier,
            ScenarioAssetAuthoringContentBuilder assetAuthoringContentBuilder,
            ScenarioMapAuthoringContentBuilder mapAuthoringContentBuilder,
            ScenarioQuestAuthoringContentBuilder questAuthoringContentBuilder,
            ScenarioAuthoringTutorialService tutorialService,
            ScenarioHelpAuthoringContentBuilder helpAuthoringContentBuilder)
        {
            _captureService = captureService;
            _sectionHub = sectionHub;
            _windowRegistry = windowRegistry;
            _settingsService = settingsService;
            _layoutService = layoutService;
            _runtimeResolver = runtimeResolver;
            _shellChromeBuilder = shellChromeBuilder;
            _stageNavigationBuilder = stageNavigationBuilder;
            _inspectorViewModelBuilder = inspectorViewModelBuilder;
            _statusBarViewModelBuilder = statusBarViewModelBuilder;
            _selectionScopeService = selectionScopeService;
            _targetClassifier = targetClassifier;
            _assetAuthoringContentBuilder = assetAuthoringContentBuilder;
            _mapAuthoringContentBuilder = mapAuthoringContentBuilder;
            _questAuthoringContentBuilder = questAuthoringContentBuilder;
            _tutorialService = tutorialService;
            _helpAuthoringContentBuilder = helpAuthoringContentBuilder;
            _workflowAuthoringContentBuilder = new ScenarioWorkflowAuthoringContentBuilder(sectionHub, selectionScopeService);
            _scenarioOverviewAuthoringContentBuilder = new ScenarioOverviewAuthoringContentBuilder();
            _runtimeTestAuthoringContentBuilder = new ScenarioRuntimeTestAuthoringContentBuilder(timelineBuilder, modDependencyDetector, modCompatibilityViewModelBuilder);
            _hierarchyAuthoringContentBuilder = new ScenarioHierarchyAuthoringContentBuilder();
            _selectionStackAuthoringContentBuilder = new ScenarioSelectionStackAuthoringContentBuilder();
            _publishAuthoringContentBuilder = new ScenarioPublishAuthoringContentBuilder(timelineBuilder, modDependencyDetector, modCompatibilityViewModelBuilder);
            _timelineAuthoringContentBuilder = new ScenarioTimelineAuthoringContentBuilder(timelineBuilder, timelineViewModelBuilder);
            _windowSectionBuilders = CreateWindowSectionBuilders();
        }

        public ScenarioAuthoringShellViewModel BuildShellViewModel(
            ScenarioAuthoringContext context,
            ScenarioAuthoringContextMenuModel contextMenu)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            ScenarioAuthoringSession session = context != null ? context.AuthoringSession : null;
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            List<ScenarioAuthoringShellWindowViewModel> windows = new List<ScenarioAuthoringShellWindowViewModel>();
            AppendShellWindowViewModels(windows, state, editorSession, session, definition);

            ScenarioAuthoringShellViewModel viewModel = new ScenarioAuthoringShellViewModel
            {
                Tabs = _stageNavigationBuilder.BuildTabs(state),
                ToolbarActions = _stageNavigationBuilder.BuildToolbarActions(state),
                LayoutActions = _stageNavigationBuilder.BuildLayoutActions(state),
                WorldSubstageActions = _stageNavigationBuilder.BuildWorldSubstageActions(state),
                ToolButtons = _stageNavigationBuilder.BuildToolButtons(state),
                WindowMenuActions = _stageNavigationBuilder.BuildWindowMenuActions(state, _windowRegistry),
                Windows = windows.ToArray(),
                SpritePickerDocument = BuildSpritePickerDocument(state, editorSession),
                FocusedEditorDocument = BuildFocusedEditorDocument(state, editorSession, definition, _captureService),
                CustomSpriteEditor = _assetAuthoringContentBuilder.BuildCustomEditorModel(state),
                Settings = state.SettingsWindowOpen ? BuildSettingsViewModel(state) : null,
                Help = state != null && state.HelpWindowOpen && _helpAuthoringContentBuilder != null ? _helpAuthoringContentBuilder.Build(state) : null,
                Tour = BuildTourViewModel(),
                Tutorial = state != null && (state.HelpWindowOpen || (_tutorialService != null && _tutorialService.CurrentTour() != null)) ? null : BuildTutorialViewModel(state, editorSession),
                ContextMenu = contextMenu,
                StatusEntries = _statusBarViewModelBuilder.BuildEntries(state, editorSession, session, _stageNavigationBuilder.BuildStageLabel(state))
            };
            _shellChromeBuilder.ApplyShellChrome(viewModel, state, editorSession, session);
            return viewModel;
        }

        private ScenarioAuthoringTourViewModel BuildTourViewModel()
        {
            if (_tutorialService == null)
                return null;

            ScenarioAuthoringTourDefinition tour = _tutorialService.CurrentTour();
            ScenarioAuthoringTourStep step = _tutorialService.CurrentTourStep();
            if (tour == null || step == null)
                return null;

            int stepIndex = _tutorialService.CurrentTourStepIndex;
            int stepCount = tour.Steps != null ? tour.Steps.Length : 0;
            return new ScenarioAuthoringTourViewModel
            {
                Visible = true,
                TourId = tour.Id,
                StepIndex = stepIndex,
                StepCount = stepCount,
                TargetId = step.TargetId,
                Title = step.Title,
                Body = step.Body,
                BackAction = Item.Action(ScenarioAuthoringActionIds.ActionTourBack, "BACK", "Go to the previous tour step.", stepIndex > 0, false, "BK"),
                NextAction = Item.Action(ScenarioAuthoringActionIds.ActionTourNext, stepIndex + 1 >= stepCount ? "DONE" : "NEXT", "Continue the spotlight tour.", true, true, "NX"),
                ExitAction = Item.Action(ScenarioAuthoringActionIds.ActionTourExit, "EXIT", "Close the spotlight tour.", true, false, "EX")
            };
        }
        private ScenarioAuthoringTutorialViewModel BuildTutorialViewModel(ScenarioAuthoringState state, ScenarioEditorSession editorSession)
        {
            if (_tutorialService == null)
                return null;

            TutorialStep step = _tutorialService.GetActiveStep(state);
            if (step == null)
                return null;

            bool satisfied = _tutorialService.IsStepSatisfied(state, editorSession, step);
            _tutorialService.MarkStepRendered(state, editorSession, step);
            TutorialStep[] steps = TutorialContent.GetSteps();
            return new ScenarioAuthoringTutorialViewModel
            {
                Visible = true,
                StepIndex = step.Index,
                StepCount = steps != null ? steps.Length : 0,
                StepId = step.Id,
                Title = step.Title,
                Body = step.Body,
                PrimaryCallout = satisfied ? "NEXT" : step.PendingCallout,
                WaitingForAction = !satisfied && string.Equals(step.PendingCallout, "WAITING FOR ACTION", StringComparison.Ordinal),
                TargetWindowId = step.TargetWindowId,
                TargetActionId = step.TargetActionId,
                TargetStage = step.TargetStage,
                PrimaryAction = Item.Action(
                    ScenarioAuthoringActionIds.ActionTutorialOpenTarget,
                    satisfied ? "NEXT" : step.PendingCallout,
                    satisfied ? "Continue to the next tutorial step." : "Open or use the highlighted editor target.",
                    true,
                    true,
                    "GO"),
                NextAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialNext, "NEXT", "Continue to the next tutorial step.", true, false, "NX"),
                SkipAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialSkip, "SKIP", "End the guided tour.", true, false, "SK"),
                HelpAction = Item.Action(ScenarioAuthoringActionIds.ActionShellOpenHelp, "HELP", "Open the workshop help pages.", true, false, "HP")
            };
        }

        public ScenarioAuthoringInspectorDocument BuildShellDocument(ScenarioAuthoringContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            ScenarioAuthoringSession session = context != null ? context.AuthoringSession : null;
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            string selectedObjectStatus;
            bool canCaptureSelectedObject = _captureService.CanCaptureTarget(state.SelectedTarget, out selectedObjectStatus);
            bool hasCapturedSelectedObject = _captureService.HasCapturedPlacementForTarget(editorSession, state.SelectedTarget);

            sections.Add(_inspectorViewModelBuilder.BuildSessionSection(state, editorSession, session, _stageNavigationBuilder.BuildStageLabel(state)));

            sections.Add(_workflowAuthoringContentBuilder.BuildWorkflowSection(editorSession));
            sections.Add(_workflowAuthoringContentBuilder.BuildHistorySection());
            sections.Add(_workflowAuthoringContentBuilder.BuildToolPickerSection(state.ActiveTool));
            sections.Add(_workflowAuthoringContentBuilder.BuildToolSection(
                state,
                editorSession,
                state.ActiveTool,
                definition,
                state.SelectedTarget,
                canCaptureSelectedObject,
                hasCapturedSelectedObject,
                selectedObjectStatus));
            sections.Add(_workflowAuthoringContentBuilder.BuildSelectionSection(state));

            if (!string.IsNullOrEmpty(state.StatusMessage))
            {
                sections.Add(_inspectorViewModelBuilder.BuildStatusSection(state.StatusMessage));
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "controls",
                Title = "Controls",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    Text("Hold Ctrl to inspect shelter objects. Cyan follows the hovered object, yellow marks the selected object."),
                    Text("Left Click picks the hovered object. Right Click clears the current selection."),
                    Text("F5 saves the draft, F6 toggles the workshop, and F7 toggles playtest mode."),
                    Text("Ctrl+Z undoes, Ctrl+Y redoes, Ctrl+C copies, Ctrl+V pastes, Ctrl+R reverts selected art."),
                    Text("The workshop pauses the shelter without opening Sheltered's vanilla pause menu."),
                    Text("Use Art to replace existing visuals or place new snapped scene sprites on the shelter map."),
                    Text("Use Test to apply the draft, let Sheltered run, then capture live family, supplies, or spawned objects back into the draft.")
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

        public ScenarioAuthoringInspectorDocument BuildInspectorDocument(ScenarioAuthoringContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
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
                        Text("Pick a shelter object to review its scenario rules, dependencies, and authored visual changes.")
                    }
                }
            }
        };
            }

            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            ScenarioTargetClassification classification = _targetClassifier.Classify(target);
            ObjectPlacement objectPlacement = FindObjectPlacement(definition, target);
            int linkedTimelineEntries = CountLikelyTriggerReferences(definition, target);
            string scopeReason;
            bool scopeAllowed = _selectionScopeService.CanSelectTargetForCurrentStage(state, target, out scopeReason);

            string captureReason;
            bool canCaptureTarget = _captureService.CanCaptureTarget(target, out captureReason);
            bool hasCapturedPlacement = _captureService.HasCapturedPlacementForTarget(editorSession, target);
            bool replacementAllowed = scopeAllowed && target.SupportsReplace;

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildTargetStripSection(state, target, classification));
            sections.Add(BuildPinnedFactsSection(state, target, classification, objectPlacement, hasCapturedPlacement));
            sections.Add(BuildPrimaryActionsSection(scopeAllowed, scopeReason, canCaptureTarget, captureReason, hasCapturedPlacement, replacementAllowed, target));
            if (replacementAllowed)
            {
                List<ScenarioAuthoringInspectorSection> assetEditorSections = _assetAuthoringContentBuilder.BuildSelectedAssetEditorSections(state, editorSession, target);
                for (int i = 0; i < assetEditorSections.Count; i++)
                    sections.Add(assetEditorSections[i]);
            }
            sections.Add(BuildScenarioBehaviorSection(target, objectPlacement, linkedTimelineEntries));
            sections.Add(BuildWarningsSection(scopeAllowed, target, objectPlacement, definition, captureReason));

            if (state != null && state.Settings != null && state.Settings.GetBool("debug.show_advanced_details", false))
                sections.Add(BuildAdvancedDebugSection(target));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Target Inspector",
                Subtitle = target.DisplayName,
                HeaderActions = BuildInspectorHeaderActions(state),
                Sections = sections.ToArray()
            };
        }

        private ScenarioAuthoringInspectorSection BuildTargetStripSection(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            ScenarioTargetClassification classification)
        {
            string friendlyKind = FriendlyKindLabel(target.Kind);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Text(
                Safe(target.DisplayName),
                friendlyKind,
                _targetClassifier.FormatScopeLabel(classification),
                "TG",
                ResolvePreviewSprite(target),
                true));
            int stackCount = state != null && state.SelectionStack != null ? state.SelectionStack.Count : 0;
            if (stackCount > 0)
            {
                int activeIndex = Mathf.Clamp(state.ActiveSelectionStackIndex, 0, stackCount - 1);
                items.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionSelectionStackCycle,
                    "Target " + (activeIndex + 1).ToString(CultureInfo.InvariantCulture) + " of " + stackCount.ToString(CultureInfo.InvariantCulture),
                    "Cycle through targets captured at the selected click point.",
                    stackCount > 1,
                    false,
                    ">>")));

                int displayCount = state.SelectionStackExpanded ? stackCount : Mathf.Min(stackCount, 5);
                for (int i = 0; i < displayCount; i++)
                {
                    ScenarioAuthoringTarget candidate = state.SelectionStack[i];
                    if (candidate == null)
                        continue;

                    bool active = i == activeIndex;
                    bool selected = SameTarget(state.SelectedTarget, candidate);
                    bool emphasized = selected || (state.SelectedTarget == null && active);
                    items.Add(ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix + i.ToString(CultureInfo.InvariantCulture),
                        FormatSelectionStackRowLabel(i, candidate),
                        FormatSelectionStackRowHint(candidate, selected || active),
                        true,
                        emphasized,
                        selected ? "SEL" : (active ? "ON" : "ST"),
                        FormatTargetCell(candidate))));
                }

                if (stackCount > displayCount)
                {
                    items.Add(ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSelectionStackToggleExpanded,
                        "+" + (stackCount - displayCount).ToString(CultureInfo.InvariantCulture) + " more",
                        "Show the remaining targets captured at the selected click point.",
                        true,
                        false,
                        "+")));
                }
                else if (state.SelectionStackExpanded && stackCount > 5)
                {
                    items.Add(ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSelectionStackToggleExpanded,
                        "Show fewer",
                        "Collapse the target stack back to the first five rows.",
                        true,
                        false,
                        "-")));
                }
            }
            return new ScenarioAuthoringInspectorSection
            {
                Id = "target_strip",
                Title = "Target",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }

        private static string FormatSelectionStackRowLabel(int index, ScenarioAuthoringTarget target)
        {
            return (index + 1).ToString(CultureInfo.InvariantCulture)
                + ". "
                + Safe(target != null ? target.DisplayName : null)
                + " - "
                + (target != null ? FriendlyKindLabel(target.Kind) : "Target");
        }

        private static string FormatSelectionStackRowHint(ScenarioAuthoringTarget target, bool active)
        {
            string prefix = active ? "Current target. " : "Select this target. ";
            return prefix
                + (target != null ? FriendlyKindLabel(target.Kind) : "Target")
                + " at "
                + FormatTargetCell(target);
        }

        private ScenarioAuthoringInspectorSection BuildPinnedFactsSection(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            ScenarioTargetClassification classification,
            ObjectPlacement objectPlacement,
            bool hasCapturedPlacement)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            bool editPins = state != null && state.Settings != null && state.Settings.GetBool("inspector.pin_edit_mode", false);
            AddPinnedFact(items, state, target.Kind, "kind", "Kind", FriendlyKindLabel(target.Kind), editPins);
            AddPinnedFact(items, state, target.Kind, "room_cell", "Room/Cell", FormatTargetCell(target), editPins);
            AddPinnedFact(items, state, target.Kind, "draft_state", "Captured State", ResolveDraftStatus(target, objectPlacement, hasCapturedPlacement), editPins);
            AddPinnedFact(items, state, target.Kind, "sprite", "Sprite", ResolveTargetSpriteName(target), editPins);
            AddPinnedFact(items, state, target.Kind, "layer", "Layer", _targetClassifier.FormatScopeLabel(classification), editPins);
            AddPinnedFact(items, state, target.Kind, "starts", "Starts", FormatStartState(objectPlacement), editPins);
            if (items.Count == 0)
                items.Add(Text(editPins ? "All facts are unpinned for this target kind." : "No pinned facts are enabled."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "pinned_facts",
                Title = editPins ? "Pinned Facts (editing)" : "Pinned Facts",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildScenarioBehaviorSection(
            ScenarioAuthoringTarget target,
            ObjectPlacement objectPlacement,
            int linkedTimelineEntries)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Foundation", Safe(objectPlacement != null ? objectPlacement.RequiredFoundationId : null)));
            items.Add(Property("Expansion", Safe(objectPlacement != null ? objectPlacement.RequiredBunkerExpansionId : null)));
            items.Add(Property("Unlock Gate", Safe(objectPlacement != null ? objectPlacement.UnlockGateId : null)));
            items.Add(Property("Activation", Safe(objectPlacement != null ? objectPlacement.ScheduledActivationId : null)));
            items.Add(Property("Timeline Links", linkedTimelineEntries.ToString(CultureInfo.InvariantCulture)));
            items.Add(Property("Source", target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId) ? "Scenario authored" : "Vanilla or live object"));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "scenario_behavior",
                Title = "Scenario Rules",
                Expanded = false,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildPrimaryActionsSection(
            bool scopeAllowed,
            string scopeReason,
            bool canCaptureTarget,
            string captureReason,
            bool hasCapturedPlacement,
            bool replacementAllowed,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            string scopedReason = !scopeAllowed ? (!string.IsNullOrEmpty(scopeReason) ? scopeReason : "This target is outside the active workspace scope.") : null;
            string captureDisabledReason = !scopeAllowed
                ? scopedReason
                : (!canCaptureTarget ? (!string.IsNullOrEmpty(captureReason) ? captureReason : "This target cannot be captured as a scenario placement.") : null);
            string removeDisabledReason = !scopeAllowed
                ? scopedReason
                : (!hasCapturedPlacement ? "This target has no captured draft placement to remove." : null);
            string editDisabledReason = !scopeAllowed
                ? scopedReason
                : (target == null || !target.SupportsReplace ? "This target does not expose an editable sprite asset." : null);
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionCaptureSelectedObject,
                "Capture Placement",
                "Store this live spawned shelter object as a scenario object placement.",
                scopeAllowed && canCaptureTarget,
                scopeAllowed && canCaptureTarget,
                "CP",
                null,
                null,
                null,
                captureDisabledReason)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement,
                "Remove Draft Capture (keeps object)",
                "Remove Draft Capture (keeps object): remove this object's captured placement from the scenario draft.",
                scopeAllowed && hasCapturedPlacement,
                false,
                "RM",
                null,
                null,
                null,
                removeDisabledReason)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionToolAssets,
                "Place Asset",
                "Open the asset placement browser for snapped scene sprites.",
                scopeAllowed,
                false,
                "PL",
                null,
                null,
                null,
                scopedReason)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                "Edit Asset",
                "Open the asset editor for this visual target.",
                replacementAllowed,
                false,
                "ED",
                null,
                null,
                null,
                editDisabledReason)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart,
                "Edit Pixels",
                "Open the pixel editor for this visual target.",
                replacementAllowed,
                false,
                "PX",
                null,
                null,
                null,
                editDisabledReason)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSelectionClear,
                "Clear Selection",
                "Clear the current scenario target selection.",
                true,
                false,
                "CL")));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "primary_actions",
                Title = "Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildWarningsSection(
            bool scopeAllowed,
            ScenarioAuthoringTarget target,
            ObjectPlacement objectPlacement,
            ScenarioDefinition definition,
            string captureReason)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (!scopeAllowed)
                items.Add(Text("Target is filtered by the active selection scope."));
            if (objectPlacement != null && string.IsNullOrEmpty(objectPlacement.ScenarioObjectId))
                items.Add(Text("Missing scenario object id."));
            if (objectPlacement != null
                && string.IsNullOrEmpty(objectPlacement.RequiredFoundationId)
                && string.IsNullOrEmpty(objectPlacement.RequiredBunkerExpansionId))
                items.Add(Text("Missing foundation or expansion support."));
            if (objectPlacement != null
                && objectPlacement.StartState == ScenarioObjectStartState.StartsEnabled
                && !HasSupport(definition, objectPlacement))
                items.Add(Text("Object starts active but its support is not present in the draft."));
            if (objectPlacement != null
                && objectPlacement.StartState == ScenarioObjectStartState.StartsEnabled
                && !string.IsNullOrEmpty(objectPlacement.RequiredBunkerExpansionId)
                && !HasExpansion(definition, objectPlacement.RequiredBunkerExpansionId))
                items.Add(Text("Object is inside a locked or missing expansion but starts enabled."));
            if (target != null && target.SupportsReplace && string.IsNullOrEmpty(target.ScenarioReferenceId) && objectPlacement == null)
                items.Add(Text("Visual replacement may need an asset or object capture before it is portable."));
            if (!string.IsNullOrEmpty(captureReason))
                items.Add(Text(captureReason));
            if (items.Count == 0)
                items.Add(Text("No warnings for this target."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "warnings",
                Title = "Warnings",
                Expanded = items.Count > 0 && !string.Equals(items[0].Value, "No warnings for this target.", StringComparison.Ordinal),
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = items.ToArray()
            };
        }

        private static void AddPinnedFact(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioAuthoringState state,
            ScenarioAuthoringTargetKind kind,
            string factId,
            string label,
            string value,
            bool editPins)
        {
            string keyToken = BuildInspectorPinToken(kind, factId);
            bool pinned = state == null || state.Settings == null || state.Settings.GetBool("inspector.pin." + keyToken, true);
            if (!pinned && !editPins)
                return;

            if (editPins)
            {
                items.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionInspectorPinTogglePrefix + keyToken,
                    (pinned ? "Unpin " : "Pin ") + label,
                    "Toggle whether this fact appears for " + FriendlyKindLabel(kind).ToLowerInvariant() + " targets.",
                    true,
                    pinned,
                    pinned ? "PIN" : "+",
                    label + ": " + Safe(value))));
                return;
            }

            items.Add(Property(label, Safe(value)));
        }

        private static string BuildInspectorPinToken(ScenarioAuthoringTargetKind kind, string factId)
        {
            return kind.ToString().ToLowerInvariant() + "." + (factId ?? string.Empty).ToLowerInvariant();
        }

        private static string FormatTargetCell(ScenarioAuthoringTarget target)
        {
            if (target == null || !target.GridX.HasValue || !target.GridY.HasValue)
                return "No grid cell";

            return target.GridX.Value.ToString(CultureInfo.InvariantCulture) + "," + target.GridY.Value.ToString(CultureInfo.InvariantCulture);
        }

        private string ResolveTargetSpriteName(ScenarioAuthoringTarget target)
        {
            if (target == null || !target.SupportsReplace)
                return "No editable sprite";

            ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget;
            if (_runtimeResolver.TryResolve(target, out resolvedTarget) && resolvedTarget != null)
                return Safe(resolvedTarget.SpriteName);

            return "No editable sprite";
        }

        private ScenarioAuthoringInspectorDocument BuildSpritePickerDocument(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession)
        {
            ScenarioAuthoringInspectorDocument inventoryPicker = BuildInventoryItemPickerDocument(state, editorSession != null ? editorSession.WorkingDefinition : null);
            if (inventoryPicker != null)
                return inventoryPicker;

            if (state == null
                || state.SpriteSwapPicker == null
                || !state.SpriteSwapPicker.IsOpen
                || state.SpriteSwapPicker.Target == null)
            {
                return null;
            }

            ScenarioSpriteSwapAuthoringService.CustomEditorModel customEditor = _sectionHub.SpriteSwap.GetCustomEditorModel(state);
            if (customEditor != null && customEditor.IsCharacterEditor)
                return BuildCharacterSpritePickerDocument(state, customEditor);
            if (customEditor != null)
                return null;

            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker = _sectionHub.SpriteSwap.GetPickerModel(
                editorSession,
                state.SpriteSwapPicker.Target,
                state.ActiveScenarioFilePath);

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            if (picker == null || picker.Target == null)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "sprite_picker_empty",
                    Title = "Asset Editor",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text("The selected target no longer exposes compatible sprite replacements.")
                    }
                });

                return new ScenarioAuthoringInspectorDocument
                {
                    Title = "Asset Editor",
                    Subtitle = FormatTarget(state.SpriteSwapPicker.Target),
                    HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, "Close the asset editor."),
                    Sections = sections.ToArray()
                };
            }

            string savedToken = !string.IsNullOrEmpty(state.SpriteSwapPicker.SavedCandidateToken)
                ? state.SpriteSwapPicker.SavedCandidateToken
                : picker.ActiveCandidateToken;
            string previewToken = !string.IsNullOrEmpty(state.SpriteSwapPicker.PreviewCandidateToken)
                ? state.SpriteSwapPicker.PreviewCandidateToken
                : savedToken;
            ScenarioSpriteCatalogService.SpriteCandidate previewCandidate = FindCandidate(picker, previewToken);
            bool pickerDirty = (customEditor != null && customEditor.Dirty)
                || !string.Equals(previewToken, savedToken, StringComparison.Ordinal);

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_picker_summary",
                Title = "Selected Sprite",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = new[]
                {
                    Text(
                        Safe(picker.Target.SpriteName),
                        Safe(picker.Target.TextureName),
                        picker.Target.Kind.ToString(),
                        "SP",
                        previewCandidate != null ? previewCandidate.Sprite : picker.Target.CurrentSprite,
                        true)
                }
            });
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_picker_current",
                Title = "Current Look",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    Property("Target", FormatTarget(state.SpriteSwapPicker.Target)),
                    Property("Sprite", Safe(picker.Target.SpriteName)),
                    Property("Source", Safe(picker.Target.TextureName)),
                    Property("Saved Swap", Safe(picker.ActiveRuleSummary))
                }
            });
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_picker_replacement",
                Title = "Replacement",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    Property("Preview", customEditor != null ? "Custom Sprite Draft" : (previewCandidate != null ? CleanCandidateLabel(previewCandidate.Label) : "Current saved look")),
                    Property("Source", previewCandidate != null ? Safe(previewCandidate.SourceName) : "Current sprite"),
                    Property("Options", CountCandidates(picker.VanillaCandidates).ToString() + " vanilla / " + CountCandidates(picker.ModdedCandidates).ToString() + " scenario"),
                    Property("Compatibility", Safe(picker.CompatibilitySummary))
                }
            });
            if (ShowAdvancedDetails(state))
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "sprite_picker_advanced",
                    Title = "Advanced Details",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Property("Component", picker.Target.Kind.ToString()),
                        Property("Current Map", Safe(picker.Target.TextureName)),
                        Property("Draft Location", Safe(picker.XmlPathHint)),
                        Property("PNG Import Folder", Safe(ScenarioPngImportService.GetImportFolderPath(state.ActiveScenarioFilePath)))
                    }
                });
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_picker_actions",
                Title = "Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave,
                        "Save",
                        "Persist the previewed sprite swap.",
                        pickerDirty,
                        customEditor != null && customEditor.Dirty,
                        "SV",
                        "Commit the current preview.",
                        null,
                        null,
                        pickerDirty ? null : "No sprite changes to save.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel,
                        "Discard Preview",
                        "Discard the preview and restore the currently saved sprite.",
                        pickerDirty,
                        false,
                        "CL",
                        "Restore the previous sprite.",
                        null,
                        null,
                        pickerDirty ? null : "No preview changes are pending.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapImportPng,
                        "Import PNG",
                        "Import the newest same-size PNG from the scenario import folder as a user-owned full replacement.",
                        true,
                        false,
                        "IM",
                        "Copy a user-owned PNG into the scenario pack.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart,
                        customEditor != null ? "Pixel Editor Open" : "Edit Pixels",
                        "Open the dedicated pixel editor for the current preview.",
                        true,
                        customEditor != null,
                        "PX",
                        "Edit pixels in a dedicated window.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard,
                        "Revert",
                        "Discard the current custom sprite draft and restore the saved sprite preview.",
                        customEditor != null,
                        false,
                        "DS",
                        "Drop the in-progress custom sprite draft.",
                        null,
                        null,
                        customEditor != null ? null : "Open the pixel editor before reverting pixel edits."))
                }
            });

            sections.Add(BuildSpriteCandidateSection(
                "sprite_picker_vanilla",
                "Vanilla Replacement Assets",
                _selectionScopeService.FilterCandidatesForScope(picker.VanillaCandidates, state),
                "No verified vanilla/runtime sprites are currently available for this target family.",
                savedToken,
                previewToken));
            sections.Add(BuildSpriteCandidateSection(
                "sprite_picker_modded",
                "Scenario Replacement Assets",
                _selectionScopeService.FilterCandidatesForScope(picker.ModdedCandidates, state),
                "Custom sprite overrides are hidden in strict replacement mode.",
                savedToken,
                previewToken));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Asset Editor",
                Subtitle = FormatTarget(state.SpriteSwapPicker.Target),
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, "Close the asset editor."),
                Sections = sections.ToArray()
            };
        }

        private ScenarioAuthoringInspectorDocument BuildCharacterSpritePickerDocument(
            ScenarioAuthoringState state,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel customEditor)
        {
            string subtitle = state != null && state.SpriteSwapPicker != null
                ? FormatTarget(state.SpriteSwapPicker.Target)
                : "Character";
            string partLabel = customEditor != null && !string.IsNullOrEmpty(customEditor.CharacterPartLabel)
                ? customEditor.CharacterPartLabel
                : "Part";
            bool characterDirty = customEditor != null && customEditor.Dirty;

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "character_picker_summary",
                Title = "Character Texture",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = new[]
                {
                    Property("Target", subtitle),
                    Property("Editor", "Character Pixel Editor"),
                    Property("Editing", partLabel),
                    Property("Canvas", (customEditor != null ? customEditor.Width : 0) + "x" + (customEditor != null ? customEditor.Height : 0)),
                    Property("Zoom", (customEditor != null ? customEditor.Zoom : 8) + "x"),
                    Text("Family member visuals use dedicated head, torso, and legs textures instead of the regular sprite-swap catalog. The live character preview updates as you paint.")
                }
            });
            if (ShowAdvancedDetails(state))
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "character_picker_advanced",
                    Title = "Advanced Details",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Property("Draft Location", "Family appearance"),
                        Property("PNG Import Folder", Safe(ScenarioPngImportService.GetImportFolderPath(state != null ? state.ActiveScenarioFilePath : null)))
                    }
                });
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "character_picker_commit",
                Title = "Commit",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave,
                        "Save",
                        "Persist the current character texture edit into the scenario pack.",
                        characterDirty,
                        false,
                        "SV",
                        "Write the edited character texture and update FamilySetup appearance data.",
                        null,
                        null,
                        characterDirty ? null : "No character texture changes to save.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel,
                        "Discard Draft",
                        "Discard the current character texture draft and restore the previously configured appearance.",
                        characterDirty,
                        false,
                        "CL",
                        "Restore the previous character appearance.",
                        null,
                        null,
                        characterDirty ? null : "No character texture changes are pending.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapImportPng,
                        "Import PNG Replacement",
                        "Import the newest same-size PNG from the scenario import folder as a user-owned full character texture replacement.",
                        true,
                        false,
                        "IM",
                        "Copy a user-owned PNG into the scenario pack."))
                }
            });

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "character_picker_help",
                Title = "Workflow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    Text("Use the part buttons in the pixel editor to switch between head, torso, and legs. Each part saves independently."),
                    Text("Mouse wheel zooms the canvas. Paint edits individual pixels, Pick samples exact RGBA values, and Select enables rectangular copy/paste."),
                    Text("Saving writes the PNG into the active scenario pack and stores the file paths in the family appearance section of the draft.")
                }
            });

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Character Editor",
                Subtitle = subtitle,
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, "Close the character editor."),
                Sections = sections.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildAdvancedDebugSection(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Runtime Kind", target.Kind.ToString()));
            items.Add(Property("Game Object", Safe(target.GameObjectName)));
            items.Add(Property("Transform Path", Safe(target.TransformPath)));
            items.Add(Property("Adapter", Safe(target.AdapterId)));
            items.Add(Property("Scenario Ref", Safe(target.ScenarioReferenceId)));

            GameObject gameObject = ResolveGameObject(target);
            List<string> componentNames = GetComponentNames(gameObject);
            items.Add(Property("Components", componentNames.Count.ToString()));
            if (componentNames.Count > 0)
                items.Add(Text("Attached: " + string.Join(", ", componentNames.ToArray())));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "advanced",
                Title = "Advanced (debug)",
                Expanded = false,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ObjectPlacement FindObjectPlacement(ScenarioDefinition definition, ScenarioAuthoringTarget target)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.ObjectPlacements == null || target == null)
                return null;

            GameObject gameObject = ResolveGameObject(target);
            Obj_Base obj = gameObject != null ? gameObject.GetComponent<Obj_Base>() : null;
            int objIndex = ScenarioBunkerDraftService.FindPlacementIndex(definition.BunkerEdits.ObjectPlacements, obj);
            if (objIndex >= 0 && objIndex < definition.BunkerEdits.ObjectPlacements.Count)
                return definition.BunkerEdits.ObjectPlacements[objIndex];

            string reference = target.ScenarioReferenceId;
            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;

                if (!string.IsNullOrEmpty(reference)
                    && (string.Equals(placement.ScenarioObjectId, reference, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(placement.RuntimeBindingKey, reference, StringComparison.OrdinalIgnoreCase)))
                {
                    return placement;
                }
            }

            return null;
        }

        private static string ResolveScenarioObjectId(ScenarioAuthoringTarget target, ObjectPlacement placement, bool hasCapturedPlacement)
        {
            if (placement != null && !string.IsNullOrEmpty(placement.ScenarioObjectId))
                return placement.ScenarioObjectId;
            if (target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId))
                return target.ScenarioReferenceId;
            return hasCapturedPlacement ? "Captured" : "Not captured";
        }

        private static string ResolveDraftStatus(ScenarioAuthoringTarget target, ObjectPlacement placement, bool hasCapturedPlacement)
        {
            if (placement != null)
                return "Authored placement";
            if (hasCapturedPlacement)
                return "Captured";
            if (target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId))
                return "Runtime generated";
            return "Live only";
        }

        private static string FormatStartState(ObjectPlacement placement)
        {
            if (placement == null)
                return "Starts Enabled";

            switch (placement.StartState)
            {
                case ScenarioObjectStartState.StartsDisabled: return "Starts Disabled";
                case ScenarioObjectStartState.StartsHidden: return "Starts Hidden";
                case ScenarioObjectStartState.StartsLocked: return "Starts Locked";
                case ScenarioObjectStartState.AppearsLater: return "Appears Later";
                case ScenarioObjectStartState.RemovedAtStart: return "Removed At Start";
                default: return "Starts Enabled";
            }
        }

        private static bool HasSupport(ScenarioDefinition definition, ObjectPlacement placement)
        {
            if (placement == null)
                return false;
            if (string.IsNullOrEmpty(placement.RequiredFoundationId) && string.IsNullOrEmpty(placement.RequiredBunkerExpansionId))
                return false;
            return HasFoundation(definition, placement.RequiredFoundationId) || HasExpansion(definition, placement.RequiredBunkerExpansionId);
        }

        private static bool HasFoundation(ScenarioDefinition definition, string foundationId)
        {
            if (string.IsNullOrEmpty(foundationId) || definition == null || definition.BunkerGrid == null || definition.BunkerGrid.Foundations == null)
                return false;

            for (int i = 0; i < definition.BunkerGrid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = definition.BunkerGrid.Foundations[i];
                if (foundation != null && string.Equals(foundation.Id, foundationId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasExpansion(ScenarioDefinition definition, string expansionId)
        {
            if (string.IsNullOrEmpty(expansionId) || definition == null || definition.BunkerGrid == null || definition.BunkerGrid.Expansions == null)
                return false;

            for (int i = 0; i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion != null && string.Equals(expansion.Id, expansionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string FriendlyKindLabel(ScenarioAuthoringTargetKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringTargetKind.Character: return "Character";
                case ScenarioAuthoringTargetKind.PlaceableObject: return "Interactive Object";
                case ScenarioAuthoringTargetKind.Wall: return "Wall";
                case ScenarioAuthoringTargetKind.Wire: return "Wire";
                case ScenarioAuthoringTargetKind.Light: return "Light";
                case ScenarioAuthoringTargetKind.Vehicle: return "Vehicle";
                case ScenarioAuthoringTargetKind.Room: return "Room";
                case ScenarioAuthoringTargetKind.Tile: return "Shelter Tile";
                case ScenarioAuthoringTargetKind.Background: return "Background";
                case ScenarioAuthoringTargetKind.SceneSprite: return "Scene Sprite";
                case ScenarioAuthoringTargetKind.Unknown: return "Target";
                default: return "Object";
            }
        }

        private static string FriendlyKindLabel(ScenarioSpriteTargetComponentKind kind)
        {
            switch (kind)
            {
                case ScenarioSpriteTargetComponentKind.SpriteRenderer: return "Sprite Renderer";
                case ScenarioSpriteTargetComponentKind.UI2DSprite: return "UI Sprite";
                case ScenarioSpriteTargetComponentKind.ParticleSystemRenderer: return "Particle Renderer";
                case ScenarioSpriteTargetComponentKind.Auto: return "Auto";
                default: return kind.ToString();
            }
        }

        public ScenarioAuthoringInspectorDocument BuildHoverDocument(ScenarioAuthoringState state)
        {
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

        public void OpenContextMenu(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            ScenarioAuthoringContextMenuService contextMenuService)
        {
            OpenContextMenu(state, target, contextMenuService, false);
        }

        public void OpenContextMenu(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            ScenarioAuthoringContextMenuService contextMenuService,
            bool centerOnScreen)
        {
            if (contextMenuService == null)
                return;

            if (state == null)
            {
                contextMenuService.Close();
                return;
            }

            Vector3 mouse = UnityEngine.Input.mousePosition;
            float anchorX = mouse.x;
            float anchorY = Screen.height - mouse.y;
            ScenarioAuthoringInspectorAction[] actions = BuildContextMenuActions(state, target);
            contextMenuService.Open(
                target != null ? Safe(target.DisplayName) : "World",
                target != null ? FriendlyKindLabel(target.Kind) : "Empty world",
                anchorX,
                anchorY,
                centerOnScreen,
                actions);
        }

        private void AppendShellWindowViewModels(
            List<ScenarioAuthoringShellWindowViewModel> windows,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            ScenarioAuthoringWindowDefinition[] definitions = _windowRegistry.GetDefinitions();
            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringWindowDefinition definitionEntry = definitions[i];
                if (definitionEntry == null)
                    continue;

                ScenarioAuthoringWindowState windowState = _layoutService.FindWindow(state, definitionEntry.Id);
                if (windowState == null)
                    continue;

                ScenarioSpriteSwapAuthoringService.CustomEditorModel pixelEditor = _sectionHub.SpriteSwap.GetCustomEditorModel(state);
                bool forcePixelEditor = pixelEditor != null
                    && pixelEditor.Visible
                    && windowState.Visible
                    && string.Equals(definitionEntry.Id, ScenarioAuthoringWindowIds.PixelEditor, StringComparison.OrdinalIgnoreCase);
                if (!forcePixelEditor
                    && string.Equals(definitionEntry.Id, ScenarioAuthoringWindowIds.PixelEditor, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!forcePixelEditor
                    && !IsWindowInShell(windowState)
                    && !string.Equals(definitionEntry.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ScenarioAuthoringShellWindowViewModel window = new ScenarioAuthoringShellWindowViewModel
                {
                    Id = definitionEntry.Id,
                    Title = forcePixelEditor && pixelEditor.Dirty ? "Pixel Editor *" : ResolveWindowTitle(definitionEntry, state),
                    Dock = definitionEntry.Dock,
                    WorkspaceStage = ResolveWindowWorkspaceStage(definitionEntry, state),
                    RendererKind = definitionEntry.RendererKind,
                    WorkspaceTabVisible = definitionEntry.WorkspaceTabVisible,
                    Visible = forcePixelEditor || windowState.Visible,
                    Collapsed = forcePixelEditor ? false : windowState.Collapsed,
                    HasCustomBounds = windowState.HasCustomBounds,
                    X = windowState.X,
                    Y = windowState.Y,
                    Width = windowState.Width,
                    Height = windowState.Height,
                    MinWidth = definitionEntry.MinWidth,
                    MinHeight = definitionEntry.MinHeight,
                    ZIndex = windowState.ZIndex,
                    HeaderActions = BuildWindowHeaderActions(definitionEntry, windowState, state)
                };
                window.Sections = BuildWindowSections(definitionEntry, state, editorSession, session, definition);
                windows.Add(window);
            }
        }

        private static string ResolveWindowTitle(ScenarioAuthoringWindowDefinition definitionEntry, ScenarioAuthoringState state)
        {
            if (definitionEntry != null
                && string.Equals(definitionEntry.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase)
                && state != null
                && state.ActiveStage == ScenarioStageKind.Test)
            {
                return "Test";
            }

            return definitionEntry != null ? definitionEntry.Title : string.Empty;
        }

        private static ScenarioStageKind ResolveWindowWorkspaceStage(ScenarioAuthoringWindowDefinition definitionEntry, ScenarioAuthoringState state)
        {
            if (definitionEntry != null
                && string.Equals(definitionEntry.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase)
                && state != null
                && state.ActiveStage == ScenarioStageKind.Test)
            {
                return ScenarioStageKind.Test;
            }

            return definitionEntry != null ? definitionEntry.WorkspaceStage : ScenarioStageKind.None;
        }

        private ScenarioAuthoringInspectorSection[] BuildWindowSections(
            ScenarioAuthoringWindowDefinition windowDefinition,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            IScenarioAuthoringWindowContentBuilder builder;
            if (windowDefinition == null || !_windowSectionBuilders.TryGetValue(windowDefinition.ContentKind, out builder))
                return BuildEmptyWindowSections();

            return builder.Build(new ScenarioAuthoringWindowContentContext(state, editorSession, session, definition));
        }

        private Dictionary<ScenarioAuthoringWindowContentKind, IScenarioAuthoringWindowContentBuilder> CreateWindowSectionBuilders()
        {
            Dictionary<ScenarioAuthoringWindowContentKind, IScenarioAuthoringWindowContentBuilder> builders = new Dictionary<ScenarioAuthoringWindowContentKind, IScenarioAuthoringWindowContentBuilder>();
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.Scenario, delegate(ScenarioAuthoringWindowContentContext context)
            {
                return context.State != null && context.State.ActiveStage == ScenarioStageKind.Test
                    ? _runtimeTestAuthoringContentBuilder.Build(context)
                    : _scenarioOverviewAuthoringContentBuilder.Build(context);
            });
            builders[ScenarioAuthoringWindowContentKind.Hierarchy] = _hierarchyAuthoringContentBuilder;
            builders[ScenarioAuthoringWindowContentKind.SelectionStack] = _selectionStackAuthoringContentBuilder;
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.TilesPalette, delegate(ScenarioAuthoringWindowContentContext context) { return BuildPaletteWindowSections(context.State, context.EditorSession, context.Definition); });
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.Inspector, delegate(ScenarioAuthoringWindowContentContext context) { return BuildInspectorShellSections(context.State, context.EditorSession, context.Definition); });
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.BuildTools, delegate(ScenarioAuthoringWindowContentContext context) { return BuildBuildToolsWindowSections(context.State, context.EditorSession, context.Definition); });
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.PixelEditor, delegate(ScenarioAuthoringWindowContentContext context) { return BuildPixelEditorWindowSections(context.State); });
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.Triggers, delegate(ScenarioAuthoringWindowContentContext context) { return BuildTimelineWindowSections(context.State, context.EditorSession, context.Session, context.Definition); });
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.Survivors, delegate(ScenarioAuthoringWindowContentContext context) { return BuildSurvivorWindowSections(context.State, context.Definition); });
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.Stockpile, delegate(ScenarioAuthoringWindowContentContext context) { return BuildStockpileWindowSections(context.Definition); });
            builders[ScenarioAuthoringWindowContentKind.Quests] = _questAuthoringContentBuilder;
            builders[ScenarioAuthoringWindowContentKind.Map] = _mapAuthoringContentBuilder;
            builders[ScenarioAuthoringWindowContentKind.Publish] = _publishAuthoringContentBuilder;
            return builders;
        }

        private static void RegisterWindowContentBuilder(
            Dictionary<ScenarioAuthoringWindowContentKind, IScenarioAuthoringWindowContentBuilder> builders,
            ScenarioAuthoringWindowContentKind contentKind,
            Func<ScenarioAuthoringWindowContentContext, ScenarioAuthoringInspectorSection[]> build)
        {
            builders[contentKind] = new DelegateScenarioAuthoringWindowContentBuilder(contentKind, build);
        }

        private ScenarioAuthoringInspectorSection[] BuildEmptyWindowSections()
        {
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "empty",
                    Title = string.Empty,
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[] { Text("Window content is not available.") }
                }
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildPaletteWindowSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            if (state.ActiveStage == ScenarioStageKind.BunkerInside && state.ActiveTool == ScenarioAuthoringTool.Assets)
            {
                ScenarioAuthoringInspectorSection section = _workflowAuthoringContentBuilder.BuildToolSection(
                    state,
                    editorSession,
                    ScenarioAuthoringTool.Assets,
                    definition,
                    state.SelectedTarget,
                    false,
                    false,
                    null);
                return new[] { section };
            }

            if (state.ActiveStage == ScenarioStageKind.BunkerBackground
                || state.ActiveStage == ScenarioStageKind.BunkerSurface
                || state.ActiveStage == ScenarioStageKind.BunkerInside
                || state.ActiveTool == ScenarioAuthoringTool.Objects
                || state.ActiveTool == ScenarioAuthoringTool.Select
                || state.ActiveTool == ScenarioAuthoringTool.Shelter
                || state.ActiveTool == ScenarioAuthoringTool.Wiring)
            {
                List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
                ScenarioBuildPlacementAuthoringService.StatusModel status = _sectionHub.BuildPlacement.GetStatusModel(state, editorSession);
                if (status != null && status.PlacementActive)
                    sections.Add(BuildPlacementStatusSection(status));

                List<ScenarioBuildPlacementAuthoringService.PaletteSectionModel> paletteSections = _sectionHub.BuildPlacement.GetPaletteSections(
                    state,
                    editorSession);
                for (int i = 0; paletteSections != null && i < paletteSections.Count; i++)
                    sections.Add(BuildPlacementPaletteSection(paletteSections[i]));

                return sections.ToArray();
            }

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "palette",
                    Title = "Build Palette",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text("Pick a build tool to see its palette here.")
                    }
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementStatusSection(ScenarioBuildPlacementAuthoringService.StatusModel model)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (model != null && !string.IsNullOrEmpty(model.Guidance))
                items.Add(Text(model.Guidance));
            if (model != null && model.PlacementActive)
            {
                if (!string.IsNullOrEmpty(model.TargetCell))
                    items.Add(Property("Target Cell", model.TargetCell));
                if (model.CanPlace.HasValue)
                    items.Add(Property("Placement", model.CanPlace.Value ? "Valid" : "Invalid"));
                if (!string.IsNullOrEmpty(model.ValidationReason))
                    items.Add(Text(model.ValidationReason));
            }
            if (model != null && model.CanCancel)
            {
                items.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionBuildPlacementCancel,
                    "Cancel Placement",
                    "Stop the current build preview without committing it.",
                    true,
                    false,
                    "CX",
                    "Cancel the active ghost preview.")));
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "build_palette_status",
                Title = model != null && !string.IsNullOrEmpty(model.Title) ? model.Title : "Build Palette",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementPaletteSection(ScenarioBuildPlacementAuthoringService.PaletteSectionModel model)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int count = model != null && model.Entries != null ? model.Entries.Count : 0;
            items.Add(Property("Count", count.ToString()));
            if (count == 0)
            {
                items.Add(Text(model != null ? Safe(model.EmptyMessage) : "No palette entries are available."));
            }
            else
            {
                for (int i = 0; model != null && model.Entries != null && i < model.Entries.Count; i++)
                {
                    ScenarioBuildPlacementAuthoringService.PaletteEntryModel entry = model.Entries[i];
                    if (entry == null)
                        continue;

                    items.Add(ActionItem(Action(
                        entry.ActionId,
                        CleanCandidateLabel(entry.Label),
                        entry.Hint,
                        entry.Enabled,
                        entry.Active,
                        "PL",
                        entry.Source,
                        entry.Badge,
                        entry.Preview)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = model != null ? model.Id : "build_palette",
                Title = model != null ? Safe(model.Title) : "Palette",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildInspectorShellSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            ScenarioAuthoringInspectorDocument document = BuildInspectorDocument(new ScenarioAuthoringContext
            {
                State = state,
                EditorSession = editorSession
            });
            if (document == null || document.Sections == null || document.Sections.Length == 0)
            {
                return new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "empty",
                        Title = "Inspector",
                        Expanded = true,
                        Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                        Items = new[] { Text("Select a shelter tile, object, survivor, or authored sprite to inspect it.") }
                    }
                };
            }

            return document.Sections;
        }

        private ScenarioAuthoringInspectorItem[] BuildInteractionItems(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            ScenarioAuthoringInspectorAction[] actions = BuildContextMenuActions(state, target);
            if (actions == null || actions.Length == 0)
                return new[] { Text("No contextual actions are available for this target.") };

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; i < actions.Length; i++)
                items.Add(ActionItem(actions[i]));
            return items.ToArray();
        }

        private ScenarioAuthoringInspectorSection[] BuildBuildToolsWindowSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            AddWorldSubstageSection(sections, state);
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.Assets)
            {
                sections.Add(_workflowAuthoringContentBuilder.BuildToolSection(
                    state,
                    editorSession,
                    state.ActiveTool,
                    definition,
                    state.SelectedTarget,
                    false,
                    false,
                    null));

                ScenarioAuthoringTarget target = state.SelectedTarget;
                List<ScenarioAuthoringInspectorSection> assetSections = _assetAuthoringContentBuilder.BuildAssetPlacementSections(state, editorSession, target);
                for (int i = 0; i < assetSections.Count; i++)
                    sections.Add(assetSections[i]);

                return sections.ToArray();
            }

            List<ScenarioBuildPlacementAuthoringService.PaletteSectionModel> paletteSections = _sectionHub.BuildPlacement.GetPaletteSections(
                state,
                editorSession);
            for (int i = 0; paletteSections != null && i < paletteSections.Count; i++)
                sections.Add(BuildPlacementPaletteSection(paletteSections[i]));

            ScenarioBuildPlacementAuthoringService.StatusModel buildStatus = _sectionHub.BuildPlacement.GetStatusModel(state, editorSession);
            if (buildStatus != null && buildStatus.PlacementActive)
                sections.Add(BuildPlacementStatusSection(buildStatus));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "snap",
                Title = "Snap",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.snap_to_grid",
                        state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true) ? "Snap To Grid: On" : "Snap To Grid: Off",
                        "Scene sprites use this by default. Rooms and room lights stay grid-locked; ladders keep hybrid placement; objects already place freely.",
                        true,
                        state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true),
                        "SN")),
                    Property("Scene Sprites", state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true) ? "Snapped by default; Shift places freely" : "Free by default; Shift snaps"),
                    Property("Rooms / Lights", "Always grid-locked"),
                    Property("Ladders / Objects", "Hybrid ladders; objects free")
                }
            });

            if (ShowAdvancedDetails(state))
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "snap_diagnostics",
                    Title = "Snap Diagnostics",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Property("Grid", state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "On" : "Off"),
                        Property("Snap", state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true) ? "On" : "Off"),
                        Property("Scroll", state.Settings != null ? state.Settings.GetFloat("input.scroll_speed", 1f).ToString("0.00", CultureInfo.InvariantCulture) + "x" : "1.00x")
                    }
                });
                sections.Add(BuildBunkerRuntimeSection(definition, state));
            }
            return sections.ToArray();
        }

        private ScenarioAuthoringInspectorSection[] BuildTimelineWindowSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.WinLoss)
            {
                return new[]
                {
                    _workflowAuthoringContentBuilder.BuildToolSection(
                        state,
                        editorSession,
                        ScenarioAuthoringTool.WinLoss,
                        definition,
                        state.SelectedTarget,
                        false,
                        false,
                        null)
                };
            }

            return _timelineAuthoringContentBuilder.Build(new ScenarioAuthoringWindowContentContext(state, editorSession, session, definition));
        }

        private void AddWorldSubstageSection(List<ScenarioAuthoringInspectorSection> sections, ScenarioAuthoringState state)
        {
            if (!IsWorldSubstageActive(state))
                return;

            ScenarioAuthoringInspectorAction[] actions = _stageNavigationBuilder.BuildWorldSubstageActions(state);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; actions != null && i < actions.Length; i++)
                items.Add(ActionItem(CleanWorldSubstageAction(actions[i])));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "world_substages",
                Title = string.Empty,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.TabStrip,
                Items = items.ToArray()
            });
        }

        private static ScenarioAuthoringInspectorAction CleanWorldSubstageAction(ScenarioAuthoringInspectorAction action)
        {
            if (action == null)
                return null;

            string label = action.Label ?? string.Empty;
            if (label.StartsWith("- ", StringComparison.Ordinal))
                label = label.Substring(2);

            return new ScenarioAuthoringInspectorAction
            {
                Id = action.Id,
                Label = label,
                Hint = action.Hint,
                Detail = action.Detail,
                Badge = action.Badge,
                IconText = action.IconText,
                PreviewSprite = action.PreviewSprite,
                Enabled = action.Enabled,
                Emphasized = action.Emphasized,
                DisabledReason = action.DisabledReason
            };
        }

        private static ScenarioAuthoringInspectorDocument BuildInventoryItemPickerDocument(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            if (state == null || definition == null)
                return null;

            bool starting = string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker, StringComparison.OrdinalIgnoreCase);
            bool scheduled = string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, StringComparison.OrdinalIgnoreCase);
            if (!starting && !scheduled)
                return null;

            StartingInventoryDefinition inventory = definition.StartingInventory;
            int index = state.FocusedEditorIndex;
            string currentItemId = null;
            if (starting)
            {
                if (inventory == null || inventory.Items == null || index < 0 || index >= inventory.Items.Count)
                    return null;
                currentItemId = inventory.Items[index] != null ? inventory.Items[index].ItemId : null;
            }
            else
            {
                if (inventory == null || inventory.ScheduledChanges == null || index < 0 || index >= inventory.ScheduledChanges.Count)
                    return null;
                currentItemId = inventory.ScheduledChanges[index] != null ? inventory.ScheduledChanges[index].ItemId : null;
            }

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioInventoryItemCatalogEntry current = ScenarioInventoryItemCatalog.Resolve(currentItemId);
            sections.Add(FactSection("inventory_picker_summary", "Selected Item", new List<ScenarioAuthoringInspectorItem>
            {
                Fact("Current", current.DisplayName, current.Detail),
                Fact("Category", current.Category.ToString(), "Catalog category from Sheltered item definitions.")
            }));

            AddInventoryPickerCategorySections(sections, starting, index, currentItemId);
            sections.Add(ActionSection("inventory_picker_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
            {
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", "Close the item picker without changing this row.", true, false, "CL"))
            }));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = starting ? "Pick Starting Item" : "Pick Timed Item",
                Subtitle = "Search by item name, id, detail, or category.",
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Close the item picker."),
                Sections = sections.ToArray()
            };
        }

        private static void AddInventoryPickerCategorySections(List<ScenarioAuthoringInspectorSection> sections, bool starting, int index, string currentItemId)
        {
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            Dictionary<string, List<ScenarioInventoryItemCatalogEntry>> byCategory = new Dictionary<string, List<ScenarioInventoryItemCatalogEntry>>(StringComparer.OrdinalIgnoreCase);
            List<string> categoryOrder = new List<string>();
            for (int i = 0; i < catalog.Count; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = catalog[i];
                if (entry == null)
                    continue;

                string category = entry.Category.ToString();
                List<ScenarioInventoryItemCatalogEntry> entries;
                if (!byCategory.TryGetValue(category, out entries))
                {
                    entries = new List<ScenarioInventoryItemCatalogEntry>();
                    byCategory[category] = entries;
                    categoryOrder.Add(category);
                }
                entries.Add(entry);
            }

            categoryOrder.Sort(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < categoryOrder.Count; c++)
            {
                string category = categoryOrder[c];
                List<ScenarioInventoryItemCatalogEntry> entries = byCategory[category];
                List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
                items.Add(Property("Count", entries.Count.ToString(CultureInfo.InvariantCulture)));
                for (int i = 0; i < entries.Count; i++)
                {
                    ScenarioInventoryItemCatalogEntry entry = entries[i];
                    string actionPrefix = starting
                        ? ScenarioAuthoringActionIds.ActionInventoryStartingItemSelectPrefix
                        : ScenarioAuthoringActionIds.ActionInventoryScheduleItemSelectPrefix;
                    items.Add(ActionItem(Action(
                        actionPrefix + index.ToString(CultureInfo.InvariantCulture) + "." + EncodeToken(entry.ItemId),
                        entry.DisplayName,
                        "Select this stockpile item.",
                        true,
                        string.Equals(currentItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase),
                        "IT",
                        entry.Detail + " | " + category,
                        category,
                        entry.PreviewSprite)));
                }

                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "inventory_picker_" + category.ToLowerInvariant(),
                    Title = category,
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                    Items = items.ToArray()
                });
            }
        }

        private static ScenarioAuthoringInspectorDocument BuildCapturePreviewDocument(
            ScenarioAuthoringCaptureService captureService,
            ScenarioEditorSession editorSession,
            bool family)
        {
            ScenarioAuthoringCaptureService.ScenarioCapturePreview preview = null;
            string message = null;
            bool ok = family
                ? captureService != null && captureService.BuildFamilyCapturePreview(editorSession, out preview, out message)
                : captureService != null && captureService.BuildInventoryCapturePreview(editorSession, out preview, out message);

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            if (!ok || preview == null)
            {
                sections.Add(FactSection("capture_preview_unavailable", "Preview", new List<ScenarioAuthoringInspectorItem>
                {
                    Text(!string.IsNullOrEmpty(message) ? message : "World capture preview is unavailable.")
                }));
            }
            else
            {
                List<ScenarioAuthoringInspectorItem> summary = new List<ScenarioAuthoringInspectorItem>();
                summary.Add(Fact("Source Entries", preview.SourceCount.ToString(CultureInfo.InvariantCulture), "Entries currently visible in the live world."));
                if (!family)
                    summary.Add(Fact("Total Items", preview.TotalQuantity.ToString(CultureInfo.InvariantCulture), "Total live item count across captured stacks."));
                summary.Add(Fact("Adds", preview.Additions.ToString(CultureInfo.InvariantCulture), "New rows that will be added."));
                summary.Add(Fact("Changes", preview.Changes.ToString(CultureInfo.InvariantCulture), "Existing rows that will be replaced with live values."));
                summary.Add(Fact("Removals", preview.Removals.ToString(CultureInfo.InvariantCulture), "Authored rows missing from the live world that will be removed."));
                sections.Add(FactSection("capture_preview_summary", "Diff Summary", summary));

                List<ScenarioAuthoringInspectorItem> diff = new List<ScenarioAuthoringInspectorItem>();
                if (preview.Lines.Count == 0)
                    diff.Add(Text("No differences from the current authored draft."));
                else
                {
                    for (int i = 0; i < preview.Lines.Count; i++)
                        diff.Add(Text(preview.Lines[i]));
                }
                sections.Add(FactSection("capture_preview_diff", "Changes", diff));
            }

            string confirmId = family
                ? ScenarioAuthoringLocalActionIds.ActionCaptureFamilyConfirm
                : ScenarioAuthoringLocalActionIds.ActionCaptureInventoryConfirm;
            sections.Add(ActionSection("capture_preview_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
            {
                ActionItem(Action(confirmId, "Confirm", family ? "Replace the starting cast with the current live family." : "Replace the starting stockpile with the current live stockpile.", ok, true, "OK", null, null, null, ok ? null : message)),
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", "Close this preview without changing the draft.", true, false, "CL"))
            }));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = family ? "Refresh Cast from World" : "Capture Current Stockpile",
                Subtitle = "Review additions, changes, and removals before confirming.",
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Close this preview."),
                Sections = sections.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorDocument BuildSurvivorFocusedEditorDocument(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            bool starting = string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, StringComparison.OrdinalIgnoreCase);
            bool future = string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindFutureSurvivor, StringComparison.OrdinalIgnoreCase);
            if (!starting && !future)
                return null;

            FamilySetupDefinition family = definition.FamilySetup;
            int index = state.FocusedEditorIndex;
            FamilyMemberConfig member = null;
            FutureSurvivorDefinition futureSurvivor = null;
            if (starting)
            {
                if (family == null || family.Members == null || index < 0 || index >= family.Members.Count)
                    return null;
                member = family.Members[index];
            }
            else
            {
                if (family == null || family.FutureSurvivors == null || index < 0 || index >= family.FutureSurvivors.Count)
                    return null;
                futureSurvivor = family.FutureSurvivors[index];
                member = futureSurvivor != null ? futureSurvivor.Survivor : null;
            }

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "survivor_editor_preview",
                Title = "Person",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CastCardGrid,
                Items = new[]
                {
                    CastCardItem(BuildAuthoredSurvivorCard(
                        member,
                        index,
                        futureSurvivor != null ? (futureSurvivor.AskToJoin ? "Ask to join" : "Auto join") + " - " + FormatSchedule(futureSurvivor.Arrival) : null,
                        starting ? "Starting survivor" : "Future arrival",
                        null,
                        new ScenarioAuthoringInspectorAction[0]))
                }
            });
            sections.Add(FactSection("survivor_editor_summary", "Summary", new List<ScenarioAuthoringInspectorItem>
            {
                Fact("Name", Safe(member != null ? member.Name : null), "Survivor display name."),
                Fact("Age Band", FormatAgeBand(member), "Adult or child body choice."),
                Fact("Gender", member != null ? member.Gender.ToString() : ScenarioGender.Any.ToString(), "Vanilla survivor gender setting."),
                Fact("Stats", FormatStatLine(member), "Vanilla stats are clamped to 0-20."),
                Fact("Traits", FindTrait(member, "Strength:") + " / " + FindTrait(member, "Weakness:"), "Strength and weakness pairs block vanilla conflicts."),
                Fact("Appearance", FormatAppearance(member), "Mesh, textures, and colors currently stored.")
            }));

            List<ScenarioAuthoringInspectorItem> identity = new List<ScenarioAuthoringInspectorItem>();
            string actionPrefix = starting ? ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix : ScenarioAuthoringActionIds.ActionFutureSurvivorEditPrefix;
            AddFamilyMemberEditorItems(identity, state, member, index, actionPrefix, false, true);
            sections.Add(ActionSection("survivor_editor_fields", "Identity, Stats, Traits, Appearance", identity));

            if (future && futureSurvivor != null)
            {
                List<ScenarioAuthoringInspectorItem> schedule = new List<ScenarioAuthoringInspectorItem>();
                schedule.Add(Fact("Arrival", FormatSchedule(futureSurvivor.Arrival), "When this survivor arrives or asks to join."));
                AddScheduleActions(schedule, ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix, ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix, index);
                schedule.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorToggleAskPrefix + index.ToString(CultureInfo.InvariantCulture), "Toggle Join Mode", "Switch between recruit intercom flow and immediate auto-join.", true, futureSurvivor.AskToJoin, "AJ")));
                sections.Add(ActionSection("survivor_editor_schedule", "Arrival", schedule));
            }

            sections.Add(ActionSection("survivor_editor_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
            {
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorSave, "Save", "Close this focused survivor editor.", true, true, "SV")),
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", state.FocusedEditorIsNew ? "Close this new survivor editor." : "Close this survivor editor.", true, false, "CL"))
            }));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = starting ? "Starting Survivor" : "Future Survivor",
                Subtitle = Safe(member != null ? member.Name : null),
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Close this survivor editor."),
                Sections = sections.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorDocument BuildFocusedEditorDocument(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition,
            ScenarioAuthoringCaptureService captureService)
        {
            if (state == null || string.IsNullOrEmpty(state.FocusedEditorKind) || definition == null)
                return null;

            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindCaptureFamily, StringComparison.OrdinalIgnoreCase))
                return BuildCapturePreviewDocument(captureService, editorSession, true);
            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindCaptureInventory, StringComparison.OrdinalIgnoreCase))
                return BuildCapturePreviewDocument(captureService, editorSession, false);
            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindStartingSurvivor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindFutureSurvivor, StringComparison.OrdinalIgnoreCase))
                return BuildSurvivorFocusedEditorDocument(state, definition);
            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, StringComparison.OrdinalIgnoreCase))
                return null;

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            string title = "Edit Timeline Entry";
            string subtitle = "Use the compact fields below, then save or cancel.";
            ScenarioAuthoringInspectorDocument storyDocument;
            if (ScenarioStoryFocusedEditorDocumentBuilder.TryBuild(state, definition, out storyDocument))
                return storyDocument;

            if (string.Equals(state.FocusedEditorKind, ScenarioBaseModeAuthoringActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.IsDefined(typeof(ScenarioBaseGameMode), state.FocusedEditorIndex))
                    return null;

                ScenarioBaseGameMode targetMode = (ScenarioBaseGameMode)state.FocusedEditorIndex;
                string targetLabel = FormatBaseMode(targetMode);
                title = "Switch base to " + targetLabel + "?";
                subtitle = "Save this backend's shelter world, then load the " + targetLabel + " backend world.";
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Current Draft Base", FormatBaseMode(definition.BaseGameMode), "Base saved in the scenario XML."));
                facts.Add(Fact("Target Backend", targetLabel, "Loads the saved shelter world for this backend."));
                facts.Add(Fact("Current World", "Saved", "Returning to this backend restores rooms, objects, walls, wiring, ladders, lights, and scene placements as you left them."));
                facts.Add(Fact("Shared Settings", "Unchanged", "Supplies, cast, story, map, timeline, art, and victory stay shared across all backends."));
                facts.Add(Fact("Default Family", targetLabel + " family", "Optional. Cast is shared, so this replaces the shared starting cast."));
                facts.Add(Text("Switching does not auto-play the opening cutscene. Use Watch opening cutscene from Home when you want to preview it."));
                facts.Add(Text("Keep current cast is the default. If no starting cast is authored yet, the live family is captured before switching."));
                sections.Add(FactSection("base_mode_scope", "Scope", facts));

                List<ScenarioAuthoringInspectorItem> actions = new List<ScenarioAuthoringInspectorItem>();
                actions.Add(ActionItem(Action(ScenarioBaseModeAuthoringActions.SwitchReload(targetMode, ScenarioBaseFamilyChoices.KeepCurrentCast), "Switch, keep shared cast", "Save this backend world, keep the shared cast, and load the " + targetLabel + " backend.", true, true, "KEEP")));
                actions.Add(ActionItem(Action(ScenarioBaseModeAuthoringActions.SwitchReload(targetMode, ScenarioBaseFamilyChoices.UseBaseDefaultFamily), "Switch, use default family", "Save this backend world, replace the shared starting cast with the " + targetLabel + " default family, and load the backend.", true, false, "DF")));
                actions.Add(ActionItem(Action(ScenarioBaseModeAuthoringActions.ActionSwitchCancel, "Cancel", "Keep the current base mode.", true, false, "CL")));
                sections.Add(ActionSection("base_mode_choices", string.Empty, actions));

                return new ScenarioAuthoringInspectorDocument
                {
                    Title = title,
                    Subtitle = subtitle,
                    HeaderActions = BuildModalCloseHeaderActions(ScenarioBaseModeAuthoringActions.ActionSwitchCancel, "Close the base mode dialog."),
                    Sections = sections.ToArray()
                };
            }
            if (string.Equals(state.FocusedEditorKind, "weather", StringComparison.OrdinalIgnoreCase))
            {
                WeatherEventDefinition weather = definition.TriggersAndEvents != null
                    && state.FocusedEditorIndex >= 0
                    && state.FocusedEditorIndex < definition.TriggersAndEvents.WeatherEvents.Count
                        ? definition.TriggersAndEvents.WeatherEvents[state.FocusedEditorIndex]
                        : null;
                if (weather == null)
                    return null;

                title = "Weather Event";
                subtitle = "Schedule a weather change for a scenario day and hour.";
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Weather", FormatWeatherStateLabel(weather.WeatherState), "Weather state to apply."));
                facts.Add(Fact("When", FormatSchedule(weather.When), "Scenario day and time."));
                facts.Add(Fact("Duration", weather.DurationHours.ToString(CultureInfo.InvariantCulture) + " hour(s)", "Zero leaves weather unchanged until another event."));
                sections.Add(FactSection("focused_weather_facts", "Weather", facts));

                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                AddWeatherStateActions(controls, state.FocusedEditorIndex, weather.WeatherState);
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleDurationPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".1", "Duration +", "Increase weather duration by one hour.", true, false, "DU+")));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleDurationPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".-1", "Duration -", "Decrease weather duration by one hour.", true, false, "DU-")));
                AddScheduleActions(controls, ScenarioAuthoringActionIds.ActionWeatherScheduleDayPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleHourPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleMinutePrefix, state.FocusedEditorIndex);
                sections.Add(ActionSection("focused_weather_controls", "Fields", controls));
            }
            else if (string.Equals(state.FocusedEditorKind, "trigger", StringComparison.OrdinalIgnoreCase))
            {
                TriggerDef trigger = definition.TriggersAndEvents != null
                    && state.FocusedEditorIndex >= 0
                    && state.FocusedEditorIndex < definition.TriggersAndEvents.Triggers.Count
                        ? definition.TriggersAndEvents.Triggers[state.FocusedEditorIndex]
                        : null;
                if (trigger == null)
                    return null;

                title = "Trigger";
                subtitle = "Define what starts a timeline event.";
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Name", Safe(trigger.Id), "Stable trigger id saved in the scenario XML."));
                facts.Add(Fact("Condition", FormatTriggerTypeLabel(trigger.Type), "What the trigger listens for."));
                facts.Add(Fact("When", FormatTriggerSchedule(trigger), "Only used by timed triggers."));
                sections.Add(FactSection("focused_trigger_facts", "Trigger", facts));

                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerTypePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Cycle Condition", "Switch between manual, timed, flag, quest, and item trigger templates.", true, false, "TY")));
                AddTriggerTargetActions(controls, definition, trigger, state.FocusedEditorIndex);
                AddScheduleActions(controls, ScenarioAuthoringActionIds.ActionTriggerDayPrefix, ScenarioAuthoringActionIds.ActionTriggerHourPrefix, ScenarioAuthoringActionIds.ActionTriggerMinutePrefix, state.FocusedEditorIndex);
                sections.Add(ActionSection("focused_trigger_controls", "Fields", controls));
            }
            else if (string.Equals(state.FocusedEditorKind, "scheduled_action", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions != null
                    && state.FocusedEditorIndex >= 0
                    && state.FocusedEditorIndex < definition.ScheduledActions.Count
                        ? definition.ScheduledActions[state.FocusedEditorIndex]
                        : null;
                if (action == null)
                    return null;

                title = "Scheduled Change";
                subtitle = "Choose when this change happens and what it does.";
                string condition = string.IsNullOrEmpty(action.GateId) ? "No condition" : action.GateId;
                string repeat = action.Policy != null && action.Policy.Repeatable ? "Repeat" : "Once";
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Kind", FormatScheduledActionTypeLabel(action.ActionType), "Primary effect template."));
                facts.Add(Fact("When", FormatSchedule(action.DueTime), "Scenario day and time."));
                facts.Add(Fact("Condition", condition, "The event only fires while this is true."));
                facts.Add(Fact("Repeat", repeat, "Whether the scheduled change can fire more than once."));
                sections.Add(FactSection("focused_action_facts", "Scheduled Change", facts));

                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                AddScheduleActions(controls, ScenarioAuthoringActionIds.ActionScheduledActionDayPrefix, ScenarioAuthoringActionIds.ActionScheduledActionHourPrefix, state.FocusedEditorIndex);
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionTypePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Cycle Kind", "Cycle the primary effect template for this scheduled change.", true, false, "TY")));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionGatePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Cycle Condition", "Attach the next authored condition, or clear it when the list wraps.", true, !string.IsNullOrEmpty(action.GateId), "CN", condition)));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionRepeatPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Toggle Repeat", "Switch this change between once-only and repeatable execution.", true, action.Policy != null && action.Policy.Repeatable, "RP")));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionEffectAddPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Add Effect", "Add another effect to this scheduled change.", true, false, "E+")));
                for (int e = 0; action.Effects != null && e < action.Effects.Count; e++)
                    AddEffectItems(controls, definition, action.Effects[e], state.FocusedEditorIndex, e);
                sections.Add(ActionSection("focused_action_controls", "Fields", controls));
            }
            else if (string.Equals(state.FocusedEditorKind, "gate", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioGateDefinition gate = definition.Gates != null
                    && state.FocusedEditorIndex >= 0
                    && state.FocusedEditorIndex < definition.Gates.Count
                        ? definition.Gates[state.FocusedEditorIndex]
                        : null;
                if (gate == null)
                    return null;

                title = "Condition";
                subtitle = "The event only fires while this is true.";
                ScenarioConditionGroup group = gate.Conditions;
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Name", Safe(gate.Id), "Stable condition id saved in the scenario XML."));
                facts.Add(Fact("Match", group != null ? group.Mode.ToString() : "All", "Whether all checks or any check must pass."));
                facts.Add(Fact("Checks", CountConditions(group).ToString(CultureInfo.InvariantCulture), "Individual condition checks."));
                sections.Add(FactSection("focused_condition_facts", "Condition", facts));

                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateModePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Match All/Any", "Switch this condition between requiring all checks and any check.", true, group != null && group.Mode == ScenarioConditionGroupMode.Any, "ANY")));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateConditionAddPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Add Check", "Add another check to this condition.", true, false, "C+")));
                for (int c = 0; group != null && group.Conditions != null && c < group.Conditions.Count; c++)
                    AddConditionItems(controls, definition, group.Conditions[c], state.FocusedEditorIndex, c);
                sections.Add(ActionSection("focused_condition_controls", "Fields", controls));
            }

            sections.Add(ActionSection("focused_editor_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
            {
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorSave, "Save", "Close this focused editor and keep the entry.", true, true, "SV")),
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", state.FocusedEditorIsNew ? "Discard this new entry and close the editor." : "Close this editor.", true, false, "CL"))
            }));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = title,
                Subtitle = subtitle,
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Close this focused editor."),
                Sections = sections.ToArray()
            };
        }

        private static bool IsWorldSubstageActive(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            return state.ActiveStage == ScenarioStageKind.Bunker
                || state.ActiveStage == ScenarioStageKind.BunkerBackground
                || state.ActiveStage == ScenarioStageKind.BunkerSurface
                || state.ActiveStage == ScenarioStageKind.BunkerInside;
        }

        private ScenarioAuthoringInspectorSection[] BuildPixelEditorWindowSections(ScenarioAuthoringState state)
        {
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor = _sectionHub.SpriteSwap.GetCustomEditorModel(state);
            if (editor == null || !editor.Visible)
                return BuildEmptyWindowSections();

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "pixel_editor_host",
                    Title = editor.IsCharacterEditor ? "Character Pixel Editor" : "Pixel Editor",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text((editor.SourceLabel ?? "<sprite>") + (editor.Dirty ? " | Unsaved changes" : " | Saved draft"))
                    }
                }
            };
        }

        internal static ScenarioAuthoringInspectorSection[] BuildTriggerWindowSections(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> triggerItems = new List<ScenarioAuthoringInspectorItem>();
            triggerItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerAddManual, "Add Manual Trigger", "Create a trigger that can be fired by code or another scheduled effect.", true, true, "T+")));
            triggerItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerAddScheduled, "Add Timed Trigger", "Create a trigger that fires on a specific scenario day and hour.", true, true, "TS")));
            if (definition != null && definition.TriggersAndEvents != null)
            {
                for (int i = 0; i < definition.TriggersAndEvents.Triggers.Count; i++)
                {
                    TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                    AddTriggerItems(triggerItems, state, definition, trigger, i);
                }
            }

            if (triggerItems.Count == 2)
                triggerItems.Add(Text("No authored triggers are in this draft yet."));

            List<ScenarioAuthoringInspectorItem> weatherItems = new List<ScenarioAuthoringInspectorItem>();
            weatherItems.Add(Property("Current Weather", GetCurrentWeatherSummary()));
            weatherItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleAdd, "Add Weather Event", "Schedule a weather state for a specific day and hour.", true, true, "WE")));
            if (definition != null && definition.TriggersAndEvents != null)
            {
                for (int i = 0; i < definition.TriggersAndEvents.WeatherEvents.Count; i++)
                    AddWeatherEventItems(weatherItems, state, definition.TriggersAndEvents.WeatherEvents[i], i);
            }

            List<ScenarioAuthoringInspectorItem> actionItems = BuildScheduledActionItems(state, definition);
            List<ScenarioAuthoringInspectorItem> gateItems = BuildGateItems(state, definition);
            List<ScenarioAuthoringInspectorItem> graphItems = BuildEventGraphItems(definition);

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "event_graph",
                    Title = "Event Graph / Quest Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = graphItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "triggers",
                    Title = "Triggers / Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = triggerItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "weather_events",
                    Title = "Weather Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = weatherItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scheduled_actions",
                    Title = "Scheduled Changes",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = actionItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_gates",
                    Title = "Conditions / Flags",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = gateItems.ToArray()
                }
            };
        }

        private static List<ScenarioAuthoringInspectorItem> BuildEventGraphItems(ScenarioDefinition definition)
        {
            return ScenarioEventGraphInspectorBuilder.BuildItems(definition);
        }

        internal static string FormatEffectTarget(ScenarioEffectDefinition effect)
        {
            if (effect == null)
                return "missing effect";
            if (!string.IsNullOrEmpty(effect.QuestId))
                return "quest " + effect.QuestId;
            if (!string.IsNullOrEmpty(effect.ObjectId))
                return "object " + effect.ObjectId;
            if (!string.IsNullOrEmpty(effect.ItemId))
                return "item " + effect.ItemId + " x" + effect.Quantity.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(effect.WeatherState))
                return "weather " + effect.WeatherState;
            if (!string.IsNullOrEmpty(effect.FlagId))
                return "flag " + effect.FlagId + "=" + effect.FlagValue;
            if (!string.IsNullOrEmpty(effect.SurvivorId))
                return "survivor " + effect.SurvivorId;
            if (!string.IsNullOrEmpty(effect.BunkerExpansionId))
                return "bunker " + effect.BunkerExpansionId;
            if (!string.IsNullOrEmpty(effect.TargetId))
                return "target " + effect.TargetId;
            return effect.Kind.ToString();
        }

        private static ScenarioAuthoringInspectorSection[] BuildSurvivorWindowSections(
            ScenarioAuthoringState state,
            ScenarioDefinition definition)
        {
            bool showAdvancedDetails = ShowAdvancedDetails(state);
            List<ScenarioAuthoringInspectorItem> currentItems = BuildLiveSurvivorItems();
            currentItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureFamily, "Refresh from World", "Preview additions, changes, and removals before replacing the starting cast from current live survivors.", true, false, "RF")));
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            if (history != null && history.CanUndo)
                currentItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryUndo, "Undo Last Capture", "Restore the roster from before the last capture or edit snapshot.", true, false, "UN")));

            List<ScenarioAuthoringInspectorItem> startingItems = new List<ScenarioAuthoringInspectorItem>();
            startingItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionStartingSurvivorAdd, "Add Starting Survivor", "Create a new editable starting crew member.", true, true, "S+")));
            if (definition != null && definition.FamilySetup != null)
            {
                for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
                {
                    FamilyMemberConfig member = definition.FamilySetup.Members[i];
                    AddFamilyMemberCardItems(
                        startingItems,
                        member,
                        i,
                        ScenarioAuthoringLocalActionIds.ActionStartingSurvivorEditorOpenPrefix,
                        ScenarioAuthoringActionIds.ActionStartingSurvivorPrefix,
                        true,
                        showAdvancedDetails);
                }
            }

            if (startingItems.Count == 1)
                startingItems.Add(Text("No starting survivors have been authored into this draft."));

            List<ScenarioAuthoringInspectorItem> futureItems = new List<ScenarioAuthoringInspectorItem>();
            futureItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorAdd, "Add Future Survivor", "Create a survivor who arrives or asks to join at a scheduled day and hour.", true, true, "FS")));
            if (definition != null && definition.FamilySetup != null)
            {
                for (int i = 0; i < definition.FamilySetup.FutureSurvivors.Count; i++)
                    AddFutureSurvivorItems(futureItems, definition.FamilySetup.FutureSurvivors[i], i, showAdvancedDetails);
            }
            if (futureItems.Count == 1)
                futureItems.Add(Text("No future survivor arrivals have been authored yet."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "current_survivors",
                    Title = "Current Survivors - Live World Reference",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.CastCardGrid,
                    Items = currentItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "starting_survivors",
                    Title = "Starting Survivors",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.CastCardGrid,
                    Items = startingItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "future_survivors",
                    Title = "Future Survivors",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.CastCardGrid,
                    Items = futureItems.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildStockpileWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> liveItems = BuildLiveInventoryItems();
            liveItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureInventory, "Capture Current Stockpile", "Preview additions, quantity changes, and removals before replacing the starting stockpile.", true, true, "IV")));

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            StartingInventoryDefinition inventory = definition != null ? definition.StartingInventory : null;
            bool overrideRandomStart = inventory != null && inventory.OverrideRandomStart;
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingAdd, "Add Starting Item", "Add an editable item stack to the starting stockpile.", true, true, "A+")));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionInventoryStartingOverrideToggle,
                "Override Random Start",
                "Toggle whether this scenario replaces the game's random starting item roll.",
                true,
                overrideRandomStart,
                "OR",
                overrideRandomStart ? "Random starting items disabled" : "Random starting items still allowed")));

            if (inventory != null)
            {
                for (int i = 0; i < inventory.Items.Count; i++)
                    AddStartingInventoryItems(items, inventory.Items[i], i);
            }

            if (items.Count == 2)
                items.Add(Text("No starting stockpile has been captured or authored into this draft."));

            List<ScenarioAuthoringInspectorItem> scheduledItems = new List<ScenarioAuthoringInspectorItem>();
            scheduledItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleAdd, "Schedule Add", "Add an item stack at a specific day and hour.", true, true, "A+")));
            scheduledItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleRemove, "Schedule Remove", "Remove an item stack at a specific day and hour.", true, false, "R-")));
            if (inventory != null)
            {
                for (int i = 0; i < inventory.ScheduledChanges.Count; i++)
                    AddInventoryChangeItems(scheduledItems, inventory.ScheduledChanges[i], i);
            }
            if (scheduledItems.Count == 2)
                scheduledItems.Add(Text("No timed stockpile changes have been authored yet."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "current_stockpile",
                    Title = "Current Shelter Items",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = liveItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "starting_stockpile",
                    Title = "Starting Items",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = items.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scheduled_stockpile",
                    Title = "Timed Item Changes",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = scheduledItems.ToArray()
                }
            };
        }


        private static List<ScenarioAuthoringInspectorItem> BuildLiveSurvivorItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            FamilyManager manager = FamilyManager.Instance;
            List<FamilyMember> members = manager != null ? manager.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member == null)
                    continue;

                items.Add(CastCardItem(BuildLiveSurvivorCard(member, i)));
            }

            if (items.Count == 0)
                items.Add(Text("No live survivors are available from FamilyManager."));
            return items;
        }

        private static ScenarioCastCardViewModel BuildLiveSurvivorCard(FamilyMember member, int liveIndex)
        {
            string status = member != null
                ? member.isDead ? "Dead" : member.isAway ? "Away" : member.IsUnconscious ? "Unconscious" : member.isCatatonic ? "Catatonic" : "Active"
                : "Unavailable";
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ScenarioCastPortraitResolver.ResolveColors(member, out hair, out skin, out shirt, out pants);

            return new ScenarioCastCardViewModel
            {
                Name = Safe(member != null ? member.firstName : null),
                RoleLine = member != null ? FormatAgeBand(member.isChild ? false : true) + " " + (member.isMale ? "Male" : "Female") + " / Live family" : "Live family unavailable",
                Status = status,
                PortraitSprite = ScenarioCastPortraitResolver.Resolve(member),
                PortraitTexture = ScenarioCastPortraitResolver.ResolveTexture(member),
                HairColor = hair,
                SkinColor = skin,
                ShirtColor = shirt,
                PantsColor = pants,
                Stats = BuildLiveStats(member),
                Traits = BuildLiveTraits(member != null ? member.traits : null),
                PrimaryAction = Action(
                    ScenarioAuthoringActionIds.ActionLiveSurvivorAddToStartingPrefix + liveIndex.ToString(CultureInfo.InvariantCulture),
                    "Add Start",
                    "Copy this live survivor into the authored starting cast without replacing the rest of the draft.",
                    member != null,
                    true,
                    "A+",
                    "Creates an authored starting survivor from the live world reference."),
                SecondaryActions = new ScenarioAuthoringInspectorAction[0]
            };
        }

        private static string[] BuildLiveTraits(Traits traits)
        {
            if (traits == null)
                return new[] { "Traits unavailable" };

            List<string> names = traits.GetLocalizedStrengthNames(true);
            List<string> weaknesses = traits.GetLocalizedWeaknessNames(true);
            if (names == null)
                names = new List<string>();
            if (weaknesses != null)
                names.AddRange(weaknesses);

            return names.Count == 0 ? new[] { "No visible traits" } : names.ToArray();
        }

        private static ScenarioCastStatViewModel[] BuildLiveStats(FamilyMember member)
        {
            if (member == null || member.BaseStats == null)
                return new ScenarioCastStatViewModel[0];

            return new[]
            {
                Stat("Strength", "Str", member.BaseStats.Strength.Level),
                Stat("Dexterity", "Dex", member.BaseStats.Dexterity.Level),
                Stat("Intelligence", "Int", member.BaseStats.Intelligence.Level),
                Stat("Charisma", "Cha", member.BaseStats.Charisma.Level),
                Stat("Perception", "Per", member.BaseStats.Perception.Level)
            };
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLiveInventoryItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            InventoryManager manager = InventoryManager.Instance;
            List<ItemStack> stacks = manager != null ? manager.GetItems() : null;
            int total = 0;
            for (int i = 0; stacks != null && i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.m_type == ItemManager.ItemType.Undefined || stack.m_count <= 0)
                    continue;

                ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(stack.m_type);
                items.Add(Property(
                    catalogEntry.DisplayName,
                    "x" + stack.m_count.ToString(CultureInfo.InvariantCulture),
                    catalogEntry.Detail,
                    null,
                    null,
                    catalogEntry.PreviewSprite));
                total += stack.m_count;
            }

            items.Insert(0, Property("Total Items", total.ToString(CultureInfo.InvariantCulture)));
            if (items.Count == 1)
                items.Add(Text("No current shelter inventory items are available from InventoryManager."));
            return items;
        }

        private static void AddFutureSurvivorItems(List<ScenarioAuthoringInspectorItem> items, FutureSurvivorDefinition survivor, int index, bool showAdvancedDetails)
        {
            if (items == null || survivor == null)
                return;

            string indexText = index.ToString(CultureInfo.InvariantCulture);
            string arrival = (survivor.AskToJoin ? "Ask to join" : "Auto join") + " - " + FormatSchedule(survivor.Arrival);
            FamilyMemberConfig member = survivor.Survivor ?? ScenarioFamilyMemberFactory.CreateDefaultConfig(Safe(survivor.Id), ScenarioGender.Any);
            items.Add(CastCardItem(BuildAuthoredSurvivorCard(
                member,
                index,
                arrival,
                "Future arrival",
                ScenarioAuthoringLocalActionIds.ActionFutureSurvivorEditorOpenPrefix,
                new[]
                {
                    Action(ScenarioAuthoringActionIds.ActionFutureSurvivorToggleAskPrefix + indexText, "Join Mode", "Switch between recruit intercom flow and immediate auto-join.", true, survivor.AskToJoin, "AJ", survivor.AskToJoin ? "Ask" : "Auto"),
                    Action(ScenarioAuthoringActionIds.ActionFutureSurvivorRemovePrefix + indexText, "Remove", "Remove this future survivor arrival.", true, false, "RM")
                })));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix, ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix, index);
        }

        private static void AddFamilyMemberCardItems(
            List<ScenarioAuthoringInspectorItem> items,
            FamilyMemberConfig member,
            int index,
            string openEditorPrefix,
            string removeOrStartingPrefix,
            bool includeOrdering,
            bool showAdvancedDetails)
        {
            if (items == null)
                return;

            if (member == null)
                member = new FamilyMemberConfig();

            string indexText = index.ToString(CultureInfo.InvariantCulture);
            List<ScenarioAuthoringInspectorAction> secondary = new List<ScenarioAuthoringInspectorAction>();
            if (includeOrdering)
            {
                secondary.Add(Action(removeOrStartingPrefix + "move." + indexText + ".-1", "Move Up", "Move this starting survivor earlier in the crew order.", true, false, "UP"));
                secondary.Add(Action(removeOrStartingPrefix + "move." + indexText + ".1", "Move Down", "Move this starting survivor later in the crew order.", true, false, "DN"));
                secondary.Add(Action(removeOrStartingPrefix + "remove." + indexText, "Remove", "Remove this starting survivor from the start crew.", true, false, "RM"));
            }
            else
            {
                secondary.Add(Action(removeOrStartingPrefix + indexText, "Remove", "Remove this future survivor arrival.", true, false, "RM"));
            }

            items.Add(CastCardItem(BuildAuthoredSurvivorCard(
                member,
                index,
                null,
                "Starting survivor",
                openEditorPrefix,
                secondary.ToArray())));
        }

        private static ScenarioCastCardViewModel BuildAuthoredSurvivorCard(
            FamilyMemberConfig member,
            int index,
            string arrivalSummary,
            string status,
            string openEditorPrefix,
            ScenarioAuthoringInspectorAction[] secondaryActions)
        {
            Color hair;
            Color skin;
            Color shirt;
            Color pants;
            ScenarioCastPortraitResolver.ResolveColors(member, out hair, out skin, out shirt, out pants);
            string indexText = index.ToString(CultureInfo.InvariantCulture);
            ScenarioAuthoringInspectorAction primary = !string.IsNullOrEmpty(openEditorPrefix)
                ? Action(openEditorPrefix + indexText, "Edit Person", "Open this survivor in the focused family-style editor.", true, false, "ED", FormatAppearance(member))
                : null;

            return new ScenarioCastCardViewModel
            {
                Name = Safe(member != null ? member.Name : null),
                RoleLine = FormatAgeBand(member) + " " + (member != null ? member.Gender.ToString() : ScenarioGender.Any.ToString()),
                Status = status,
                ArrivalSummary = arrivalSummary,
                PortraitSprite = ScenarioCastPortraitResolver.Resolve(member),
                PortraitTexture = ScenarioCastPortraitResolver.ResolveTexture(member),
                HairColor = hair,
                SkinColor = skin,
                ShirtColor = shirt,
                PantsColor = pants,
                Stats = BuildAuthoredStats(member),
                Traits = BuildAuthoredTraits(member),
                PrimaryAction = primary,
                SecondaryActions = secondaryActions ?? new ScenarioAuthoringInspectorAction[0]
            };
        }

        private static ScenarioCastStatViewModel[] BuildAuthoredStats(FamilyMemberConfig member)
        {
            string[] statIds = ScenarioFamilyMemberFactory.StatIds;
            ScenarioCastStatViewModel[] stats = new ScenarioCastStatViewModel[statIds.Length];
            for (int i = 0; i < statIds.Length; i++)
            {
                string statId = statIds[i];
                stats[i] = Stat(statId, statId.Substring(0, 3), ClampStatDisplay(FindStatValue(member, statId, 5)));
            }

            return stats;
        }

        private static string[] BuildAuthoredTraits(FamilyMemberConfig member)
        {
            List<string> traits = new List<string>();
            for (int i = 0; member != null && member.Traits != null && i < member.Traits.Count; i++)
            {
                string trait = member.Traits[i];
                if (string.IsNullOrEmpty(trait))
                    continue;

                int separator = trait.IndexOf(':');
                traits.Add(separator >= 0 && separator < trait.Length - 1 ? trait.Substring(separator + 1) : trait);
            }

            return traits.Count == 0 ? new[] { "No traits selected" } : traits.ToArray();
        }

        private static ScenarioCastStatViewModel Stat(string id, string label, int value)
        {
            return new ScenarioCastStatViewModel
            {
                Id = id,
                Label = label,
                Value = ClampStatDisplay(value),
                Max = 20
            };
        }

        private static void AddFamilyMemberEditorItems(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioAuthoringState state,
            FamilyMemberConfig member,
            int index,
            string actionPrefix,
            bool includeOrdering,
            bool showAdvancedDetails)
        {
            if (items == null)
                return;

            if (member == null)
                member = new FamilyMemberConfig();

            string indexedPrefix = actionPrefix + index.ToString(CultureInfo.InvariantCulture) + ".";
            items.Add(Property(
                (index + 1).ToString(CultureInfo.InvariantCulture) + ". " + Safe(member.Name),
                BuildFamilyMemberSummary(member)));
            items.Add(ActionItem(Action(indexedPrefix + "name", "Name", "Cycle this survivor's name preset.", true, false, "NM", Safe(member.Name))));
            items.Add(ActionItem(Action(indexedPrefix + "gender", "Gender", "Cycle Any, Female, and Male.", true, false, "GN", member.Gender.ToString())));
            items.Add(ActionItem(Action(indexedPrefix + "adult", "Adult/Child", "Toggle the vanilla adult or child body mesh.", true, false, "BD", FormatBody(member, showAdvancedDetails))));

            string[] statIds = ScenarioFamilyMemberFactory.StatIds;
            for (int i = 0; i < statIds.Length; i++)
            {
                string statId = statIds[i];
                int rawValue = FindStatValue(member, statId, 5);
                int displayValue = ClampStatDisplay(rawValue);
                string statDetail = FormatStatDisplayDetail(rawValue, displayValue);
                bool canIncrease = displayValue < 20;
                bool canDecrease = displayValue > 0;
                items.Add(ActionItem(Action(indexedPrefix + "stat." + statId + ".1", statId + " +", "Increase " + statId + ".", canIncrease, false, "+", displayValue.ToString(CultureInfo.InvariantCulture), null, null, canIncrease ? statDetail : "Stats are limited to 0-20.")));
                items.Add(ActionItem(Action(indexedPrefix + "stat." + statId + ".-1", statId + " -", "Decrease " + statId + ".", canDecrease, false, "-", displayValue.ToString(CultureInfo.InvariantCulture), null, null, canDecrease ? statDetail : "Stats are limited to 0-20.")));
            }

            items.Add(ActionItem(Action(indexedPrefix + "strength_trait", "Strength Trait", "Cycle this survivor's strength characteristic. Vanilla paired trait conflicts are skipped.", true, false, "ST", FindTrait(member, "Strength:"))));
            items.Add(ActionItem(Action(indexedPrefix + "weakness_trait", "Weakness Trait", "Cycle this survivor's weakness characteristic. Vanilla paired trait conflicts are skipped.", true, false, "WT", FindTrait(member, "Weakness:"))));
            items.Add(ActionItem(Action(indexedPrefix + "randomize_person", "Randomize Person", "Randomize name, body, stats, traits, textures, and colors using vanilla-style character creation rules.", true, false, "RND")));
            items.Add(ActionItem(Action(indexedPrefix + "randomize_look", "Randomize Look", "Randomize head, top, bottom, and color choices.", true, false, "RLK", FormatAppearance(member))));
            AddAppearanceCycleActions(items, indexedPrefix, member, showAdvancedDetails);
            string copyReason;
            bool canCopySelected = CanCopySelectedFamilyMember(state, out copyReason);
            items.Add(ActionItem(Action(indexedPrefix + "copy_identity", "Copy Selected Identity", "Copy name, gender, stats, traits, and appearance from the selected live family member.", canCopySelected, false, "ID", canCopySelected ? "Selected live family member" : null, null, null, copyReason)));
            items.Add(ActionItem(Action(indexedPrefix + "copy_look", "Copy Selected Look", "Copy appearance from the currently selected live family member.", canCopySelected, false, "LK", FormatAppearance(member), null, null, copyReason)));
            items.Add(ActionItem(Action(indexedPrefix + "clear_look", "Clear Look", "Clear stored mesh, texture, and color overrides.", true, false, "CL", FormatAppearance(member))));

            if (includeOrdering)
            {
                items.Add(ActionItem(Action(actionPrefix + "move." + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Move Up", "Move this starting survivor earlier in the crew order.", true, false, "UP")));
                items.Add(ActionItem(Action(actionPrefix + "move." + index.ToString(CultureInfo.InvariantCulture) + ".1", "Move Down", "Move this starting survivor later in the crew order.", true, false, "DN")));
                items.Add(ActionItem(Action(actionPrefix + "remove." + index.ToString(CultureInfo.InvariantCulture), "Remove", "Remove this starting survivor from the start crew.", true, false, "RM")));
            }
        }

        private static void AddAppearanceCycleActions(List<ScenarioAuthoringInspectorItem> items, string indexedPrefix, FamilyMemberConfig member, bool showAdvancedDetails)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            items.Add(ActionItem(Action(indexedPrefix + "texture.head.-1", "Previous Head", "Switch to the previous vanilla head sprite.", true, false, "<H", FormatTexture(appearance, ScenarioCharacterTexturePart.Head, showAdvancedDetails))));
            items.Add(ActionItem(Action(indexedPrefix + "texture.head.1", "Next Head", "Switch to the next vanilla head sprite.", true, false, "H>", FormatTexture(appearance, ScenarioCharacterTexturePart.Head, showAdvancedDetails))));
            items.Add(ActionItem(Action(indexedPrefix + "texture.torso.-1", "Previous Top", "Switch to the previous vanilla torso/top sprite.", true, false, "<T", FormatTexture(appearance, ScenarioCharacterTexturePart.Torso, showAdvancedDetails))));
            items.Add(ActionItem(Action(indexedPrefix + "texture.torso.1", "Next Top", "Switch to the next vanilla torso/top sprite.", true, false, "T>", FormatTexture(appearance, ScenarioCharacterTexturePart.Torso, showAdvancedDetails))));
            items.Add(ActionItem(Action(indexedPrefix + "texture.legs.-1", "Previous Bottom", "Switch to the previous vanilla leg/bottom sprite.", true, false, "<B", FormatTexture(appearance, ScenarioCharacterTexturePart.Legs, showAdvancedDetails))));
            items.Add(ActionItem(Action(indexedPrefix + "texture.legs.1", "Next Bottom", "Switch to the next vanilla leg/bottom sprite.", true, false, "B>", FormatTexture(appearance, ScenarioCharacterTexturePart.Legs, showAdvancedDetails))));

            items.Add(ActionItem(Action(indexedPrefix + "color.hair.-1", "Previous Hair Color", "Switch to the previous vanilla hair color.", true, false, "<HC", FormatColor(appearance, ScenarioCharacterColorPart.Hair))));
            items.Add(ActionItem(Action(indexedPrefix + "color.hair.1", "Next Hair Color", "Switch to the next vanilla hair color.", true, false, "HC>", FormatColor(appearance, ScenarioCharacterColorPart.Hair))));
            items.Add(ActionItem(Action(indexedPrefix + "color.skin.-1", "Previous Skin Color", "Switch to the previous vanilla skin color.", true, false, "<SC", FormatColor(appearance, ScenarioCharacterColorPart.Skin))));
            items.Add(ActionItem(Action(indexedPrefix + "color.skin.1", "Next Skin Color", "Switch to the next vanilla skin color.", true, false, "SC>", FormatColor(appearance, ScenarioCharacterColorPart.Skin))));
            items.Add(ActionItem(Action(indexedPrefix + "color.shirt.-1", "Previous Shirt Color", "Switch to the previous vanilla shirt/top color.", true, false, "<TC", FormatColor(appearance, ScenarioCharacterColorPart.Shirt))));
            items.Add(ActionItem(Action(indexedPrefix + "color.shirt.1", "Next Shirt Color", "Switch to the next vanilla shirt/top color.", true, false, "TC>", FormatColor(appearance, ScenarioCharacterColorPart.Shirt))));
            items.Add(ActionItem(Action(indexedPrefix + "color.pants.-1", "Previous Pants Color", "Switch to the previous vanilla pants/bottom color.", true, false, "<BC", FormatColor(appearance, ScenarioCharacterColorPart.Pants))));
            items.Add(ActionItem(Action(indexedPrefix + "color.pants.1", "Next Pants Color", "Switch to the next vanilla pants/bottom color.", true, false, "BC>", FormatColor(appearance, ScenarioCharacterColorPart.Pants))));
        }

        private static bool CanCopySelectedFamilyMember(ScenarioAuthoringState state, out string reason)
        {
            reason = null;
            ScenarioAuthoringTarget target = state != null ? state.SelectedTarget : null;
            if (target == null)
            {
                reason = "No live family member is selected.";
                return false;
            }

            GameObject gameObject = ResolveGameObject(target);
            if (gameObject == null)
            {
                reason = "Selected target is not a live family member.";
                return false;
            }

            FamilyMember member = gameObject.GetComponentInParent<FamilyMember>();
            if (member == null)
            {
                reason = "No live family member matches this entry.";
                return false;
            }

            return true;
        }

        private static void AddInventoryChangeItems(List<ScenarioAuthoringInspectorItem> items, TimedInventoryChangeDefinition change, int index)
        {
            if (items == null || change == null)
                return;

            ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(change.ItemId);
            items.Add(Property(
                change.Kind.ToString() + " timed change",
                catalogEntry.DisplayName,
                catalogEntry.Detail + " | " + FormatSchedule(change.When),
                change.Kind + " x" + change.Quantity.ToString(CultureInfo.InvariantCulture),
                null,
                catalogEntry.PreviewSprite,
                change.Kind == ScenarioInventoryChangeKind.Add));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleItemPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Previous Item", "Switch this timed change to the previous stockpile item.", true, false, "<", catalogEntry.ItemId)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleItemPrefix + index.ToString(CultureInfo.InvariantCulture) + ".1", "Next Item", "Switch this timed change to the next stockpile item.", true, false, ">", catalogEntry.ItemId)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringLocalActionIds.ActionInventorySchedulePickerOpenPrefix + index.ToString(CultureInfo.InvariantCulture),
                "Choose Item",
                "Open the searchable stockpile item picker for this timed change.",
                true,
                false,
                "IT",
                catalogEntry.ItemId)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleKindPrefix + index.ToString(CultureInfo.InvariantCulture), "Toggle Add/Remove", "Switch this timed change between adding and removing items.", true, change.Kind == ScenarioInventoryChangeKind.Add, "AR", change.Kind.ToString())));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".1", "Quantity +", "Increase this timed change quantity by one.", true, false, "+", change.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Quantity -", "Decrease this timed change quantity by one.", true, false, "-", change.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".10", "Quantity +10", "Increase this timed change quantity by ten.", true, false, "+10", change.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-10", "Quantity -10", "Decrease this timed change quantity by ten.", true, false, "-10", change.Quantity.ToString(CultureInfo.InvariantCulture))));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionInventoryScheduleDayPrefix, ScenarioAuthoringActionIds.ActionInventoryScheduleHourPrefix, ScenarioAuthoringActionIds.ActionInventoryScheduleMinutePrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleDeletePrefix + index.ToString(), "Remove Timed Item Change", "Remove this timed stockpile change.", true, false, "RM")));
        }

        private static void AddStartingInventoryItems(List<ScenarioAuthoringInspectorItem> items, ItemEntry entry, int index)
        {
            if (items == null || entry == null)
                return;

            ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(entry.ItemId);
            items.Add(Property(
                "Starting item",
                catalogEntry.DisplayName,
                catalogEntry.Detail,
                "x" + entry.Quantity.ToString(CultureInfo.InvariantCulture),
                null,
                catalogEntry.PreviewSprite,
                true));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingItemPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Previous Item", "Switch this starting stack to the previous stockpile item.", true, false, "<", catalogEntry.ItemId)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingItemPrefix + index.ToString(CultureInfo.InvariantCulture) + ".1", "Next Item", "Switch this starting stack to the next stockpile item.", true, false, ">", catalogEntry.ItemId)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringLocalActionIds.ActionInventoryStartingPickerOpenPrefix + index.ToString(CultureInfo.InvariantCulture),
                "Choose Item",
                "Open the searchable stockpile item picker for this starting stack.",
                true,
                false,
                "IT",
                catalogEntry.ItemId)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".1", "Quantity +", "Increase this starting stack by one.", true, false, "+", entry.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Quantity -", "Decrease this starting stack by one.", true, false, "-", entry.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".10", "Quantity +10", "Increase this starting stack by ten.", true, false, "+10", entry.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingQuantityPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-10", "Quantity -10", "Decrease this starting stack by ten.", true, false, "-10", entry.Quantity.ToString(CultureInfo.InvariantCulture))));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryStartingRemovePrefix + index.ToString(CultureInfo.InvariantCulture), "Remove Starting Item", "Remove this stack from the starting stockpile.", true, false, "RM")));
        }

        private static void AddWeatherEventItems(List<ScenarioAuthoringInspectorItem> items, ScenarioAuthoringState state, WeatherEventDefinition weather, int index)
        {
            if (items == null || weather == null)
                return;

            items.Add(TimelineFact(state, "weather", index, FormatWeatherStateLabel(weather.WeatherState), FormatSchedule(weather.When), weather.DurationHours > 0 ? "Restores at " + FormatSchedule(AddHours(weather.When, weather.DurationHours)) : "No restore event"));
            AddWeatherStateActions(items, index, weather.WeatherState);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleDurationPrefix + index.ToString(CultureInfo.InvariantCulture) + ".1", "Duration +", "Increase weather duration by one hour.", true, false, "DU+")));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleDurationPrefix + index.ToString(CultureInfo.InvariantCulture) + ".-1", "Duration -", "Decrease weather duration by one hour.", true, false, "DU-")));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionWeatherScheduleDayPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleHourPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleMinutePrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleDeletePrefix + index.ToString(), "Remove Weather Event", "Remove this scheduled weather event.", true, false, "RM")));
        }

        private static void AddTriggerItems(List<ScenarioAuthoringInspectorItem> items, ScenarioAuthoringState state, ScenarioDefinition definition, TriggerDef trigger, int index)
        {
            if (items == null || trigger == null)
                return;

            string schedule = FormatTriggerSchedule(trigger);
            items.Add(TimelineFact(state, "trigger", index, string.IsNullOrEmpty(trigger.Id) ? ("Trigger " + (index + 1).ToString(CultureInfo.InvariantCulture)) : trigger.Id, FormatTriggerTypeLabel(trigger.Type) + " / " + schedule, "Defines what starts a timeline event."));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerTypePrefix + index.ToString(CultureInfo.InvariantCulture), "Cycle Trigger Type", "Switch between manual, scheduled, flag, quest, and item trigger templates.", true, false, "TY")));
            AddTriggerTargetActions(items, definition, trigger, index);
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionTriggerDayPrefix, ScenarioAuthoringActionIds.ActionTriggerHourPrefix, ScenarioAuthoringActionIds.ActionTriggerMinutePrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerDeletePrefix + index.ToString(CultureInfo.InvariantCulture), "Remove Trigger", "Remove this authored trigger.", true, false, "RM")));
        }

        private static void AddConditionItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioConditionRef condition, int gateIndex, int conditionIndex)
        {
            if (items == null || condition == null)
                return;

            string prefix = gateIndex.ToString(CultureInfo.InvariantCulture) + "." + conditionIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(Property("Condition " + (conditionIndex + 1).ToString(CultureInfo.InvariantCulture), condition.Kind + " / " + FormatConditionTarget(condition)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateConditionKindPrefix + prefix, "Cycle Condition Kind", "Switch this gate condition to the next supported template.", true, false, "CK")));
            AddConditionTargetActions(items, definition, condition, prefix);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateConditionDeletePrefix + prefix, "Remove Condition", "Remove this gate condition.", true, false, "RM")));
        }

        private static void AddEffectItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioEffectDefinition effect, int actionIndex, int effectIndex)
        {
            if (items == null || effect == null)
                return;

            string prefix = actionIndex.ToString(CultureInfo.InvariantCulture) + "." + effectIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(Property("Effect " + (effectIndex + 1).ToString(CultureInfo.InvariantCulture), effect.Kind + " / " + FormatEffectTarget(effect)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionEffectKindPrefix + prefix, "Cycle Effect Kind", "Switch this effect to the next supported template.", true, false, "EK")));
            AddEffectTargetActions(items, definition, effect, prefix);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionEffectDeletePrefix + prefix, "Remove Effect", "Remove this scheduled action effect.", true, false, "RM")));
        }

        private static void AddInventoryPickerActions(List<ScenarioAuthoringInspectorItem> items, string actionPrefix, int index, string currentItemId)
        {
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(12, catalog.Count);
            for (int i = 0; i < max; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = catalog[i];
                if (entry == null)
                    continue;
                items.Add(ActionItem(Action(
                    actionPrefix + index.ToString(CultureInfo.InvariantCulture) + "." + EncodeToken(entry.ItemId),
                    entry.DisplayName,
                    "Select this stockpile item.",
                    true,
                    string.Equals(currentItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase),
                    "IT",
                    entry.Detail,
                    null,
                    entry.PreviewSprite)));
            }
        }

        private static void AddWeatherStateActions(List<ScenarioAuthoringInspectorItem> items, int index, string current)
        {
            string[] states = { "None", "Rain", "BlackRain", "LightSand", "MediumSand", "HeavySand" };
            for (int i = 0; i < states.Length; i++)
            {
                string state = states[i];
                items.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionWeatherScheduleStatePrefix + index.ToString(CultureInfo.InvariantCulture) + "." + EncodeToken(state),
                    FormatWeatherStateLabel(state),
                    "Set this weather state.",
                    true,
                    string.Equals(current, state, StringComparison.OrdinalIgnoreCase),
                    "WE",
                    FormatWeatherGroupLabel(state))));
            }
        }

        private static void AddTriggerTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, TriggerDef trigger, int index)
        {
            string type = trigger != null ? trigger.Type : null;
            string idText = index.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(type, "ScenarioFlagSet", StringComparison.OrdinalIgnoreCase))
            {
                string flag = ScenarioPropertyBag.GetString(trigger.Properties, "flagId", null);
                items.Add(Property("Flag", !string.IsNullOrEmpty(flag) ? flag + "=" + ScenarioPropertyBag.GetString(trigger.Properties, "flagValue", "true") : "<choose target>"));
                AddTriggerTokenAction(items, ScenarioAuthoringActionIds.ActionTriggerTargetPrefix, idText, ScenarioEventReferenceFinder.FirstFlagId(definition), "Flag", "Use an existing scenario flag target.", "FL", flag);
            }
            else if (string.Equals(type, "ItemQuantityAvailable", StringComparison.OrdinalIgnoreCase))
            {
                AddInventoryPickerActions(items, ScenarioAuthoringActionIds.ActionTriggerTargetPrefix, index, ScenarioPropertyBag.GetString(trigger.Properties, "itemId", null));
            }
            else if (string.Equals(type, "QuestCompleted", StringComparison.OrdinalIgnoreCase))
            {
                string quest = ScenarioPropertyBag.GetString(trigger.Properties, "questId", null);
                items.Add(Property("Quest", !string.IsNullOrEmpty(quest) ? quest : "<choose target>"));
                AddQuestTriggerActions(items, definition, idText, quest);
            }
        }

        private static void AddConditionTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioConditionRef condition, string prefix)
        {
            if (condition == null)
                return;
            if (condition.Kind == ScenarioConditionKind.ItemQuantityAvailable)
            {
                AddConditionItemPickerActions(items, prefix, condition.TargetId);
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionGateConditionQuantityPrefix, prefix, "Qty +", "Increase required item quantity.", "1", "+");
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionGateConditionQuantityPrefix, prefix, "Qty -", "Decrease required item quantity.", "-1", "-");
            }
            else if (condition.Kind == ScenarioConditionKind.ScenarioFlagSet)
            {
                string flag = !string.IsNullOrEmpty(condition.FlagId) ? condition.FlagId : condition.TargetId;
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstFlagId(definition), "Flag", "Use an existing scenario flag target.", "FL", flag);
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionGateConditionFlagValuePrefix, prefix, "Flag true", "Require flag value true.", "true", "T");
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionGateConditionFlagValuePrefix, prefix, "Flag false", "Require flag value false.", "false", "F");
            }
            else if (condition.Kind == ScenarioConditionKind.QuestActive || condition.Kind == ScenarioConditionKind.QuestCompleted || condition.Kind == ScenarioConditionKind.QuestFailed)
                AddQuestPairActions(items, definition, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, condition.TargetId);
            else if (condition.Kind == ScenarioConditionKind.BunkerExpansionUnlocked)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstExpansionId(definition), "Expansion", "Use an existing bunker expansion target.", "EX", condition.TargetId);
            else if (condition.Kind == ScenarioConditionKind.CustomTrigger)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstTriggerId(definition), "Trigger", "Use an existing trigger target.", "TR", condition.TargetId);
            else if (condition.Kind == ScenarioConditionKind.SurvivorPresent || condition.Kind == ScenarioConditionKind.SurvivorStatCheck || condition.Kind == ScenarioConditionKind.SurvivorTraitCheck)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstSurvivorName(definition), "Survivor", "Use an existing survivor target.", "SV", condition.TargetId);
            else
            {
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, condition.TargetId, "Target", "Use this target id.", "TG", condition.TargetId);
            }
        }

        private static void AddEffectTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioEffectDefinition effect, string prefix)
        {
            if (effect == null)
                return;
            if (effect.Kind == ScenarioEffectKind.AddInventory || effect.Kind == ScenarioEffectKind.RemoveInventory)
            {
                AddInventoryEffectPickerActions(items, prefix, effect.ItemId);
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectQuantityPrefix, prefix, "Qty +", "Increase effect quantity.", "1", "+");
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectQuantityPrefix, prefix, "Qty -", "Decrease effect quantity.", "-1", "-");
            }
            else if (effect.Kind == ScenarioEffectKind.SetWeather || effect.Kind == ScenarioEffectKind.RestoreWeather)
            {
                string[] states = { "None", "Rain", "BlackRain", "LightSand", "MediumSand", "HeavySand" };
                for (int i = 0; i < states.Length; i++)
                    AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, FormatWeatherStateLabel(states[i]), "Set effect weather state.", states[i], "WE", string.Equals(effect.WeatherState, states[i], StringComparison.OrdinalIgnoreCase), FormatWeatherGroupLabel(states[i]), null);
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectWeatherDurationPrefix, prefix, "Duration +", "Increase weather duration.", "1", "DU+");
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectWeatherDurationPrefix, prefix, "Duration -", "Decrease weather duration.", "-1", "DU-");
            }
            else if (effect.Kind == ScenarioEffectKind.SetScenarioFlag)
            {
                string flag = !string.IsNullOrEmpty(effect.FlagId) ? effect.FlagId : effect.TargetId;
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstFlagId(definition), "Flag", "Use an existing scenario flag target.", "FL", flag);
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectFlagValuePrefix, prefix, "Flag true", "Set flag value true.", "true", "T");
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectFlagValuePrefix, prefix, "Flag false", "Set flag value false.", "false", "F");
            }
            else if (effect.Kind == ScenarioEffectKind.StartQuest)
                AddQuestPairActions(items, definition, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, effect.QuestId ?? effect.TargetId);
            else if (effect.Kind == ScenarioEffectKind.ActivateObject || effect.Kind == ScenarioEffectKind.DeactivateObject)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstObjectId(definition), "Object", "Use an existing object target.", "OB", effect.ObjectId ?? effect.TargetId);
            else if (effect.Kind == ScenarioEffectKind.SpawnFutureSurvivor)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstFutureSurvivorId(definition), "Future Survivor", "Use an existing future survivor target.", "SV", effect.SurvivorId ?? effect.TargetId);
            else if (effect.Kind == ScenarioEffectKind.UnlockBunkerExpansion)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstExpansionId(definition), "Expansion", "Use an existing bunker expansion target.", "EX", effect.BunkerExpansionId ?? effect.TargetId);
            else if (effect.Kind == ScenarioEffectKind.FireTrigger)
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, ScenarioEventReferenceFinder.FirstTriggerId(definition), "Trigger", "Use an existing trigger target.", "TR", effect.TriggerId ?? effect.TargetId);
            else
            {
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, effect.TargetId, "Target", "Use this target id.", "TG", effect.TargetId);
            }
        }

        private static void AddQuestTriggerActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, string indexText, string current)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count && count < 8; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id))
                    continue;
                AddTriggerTokenAction(items, ScenarioAuthoringActionIds.ActionTriggerTargetPrefix, indexText, quest.Id, "Quest", "Use this authored quest target.", "QT", current);
                count++;
            }
            if (count == 0)
                AddChooseTarget(items, "Quest", "Add a quest before selecting a quest trigger target.", "QT");
        }

        private static void AddQuestPairActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, string actionPrefix, string prefix, string current)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count && count < 8; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                if (quest == null || string.IsNullOrEmpty(quest.Id))
                    continue;
                AddPairTokenAction(items, actionPrefix, prefix, "Quest " + quest.Id, "Use this authored quest target.", quest.Id, "QT", string.Equals(current, quest.Id, StringComparison.OrdinalIgnoreCase));
                count++;
            }
            if (count == 0)
                AddChooseTarget(items, "Quest", "Add a quest before selecting this target.", "QT");
        }

        private static void AddTriggerTokenAction(List<ScenarioAuthoringInspectorItem> items, string actionPrefix, string indexText, string target, string label, string hint, string icon, string current)
        {
            if (string.IsNullOrEmpty(target))
            {
                AddChooseTarget(items, label, "Create an authored " + label.ToLowerInvariant() + " target first.", icon);
                return;
            }
            items.Add(ActionItem(Action(actionPrefix + indexText + "." + EncodeToken(target), label + " " + target, hint, true, string.Equals(current, target, StringComparison.OrdinalIgnoreCase), icon)));
        }

        private static void AddPairTokenActionOrChoose(List<ScenarioAuthoringInspectorItem> items, string actionPrefix, string prefix, string target, string label, string hint, string icon, string current)
        {
            if (string.IsNullOrEmpty(target))
            {
                AddChooseTarget(items, label, "Create an authored " + label.ToLowerInvariant() + " target first.", icon);
                return;
            }
            AddPairTokenAction(items, actionPrefix, prefix, label + " " + target, hint, target, icon, string.Equals(current, target, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddChooseTarget(List<ScenarioAuthoringInspectorItem> items, string label, string hint, string icon)
        {
            items.Add(ActionItem(Action("scenario.target.choose.missing." + EncodeToken(label), "Choose " + label, hint, false, false, icon, "<no valid target>")));
        }

        private static void AddInventoryEffectPickerActions(List<ScenarioAuthoringInspectorItem> items, string prefix, string currentItemId)
        {
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(12, catalog.Count);
            for (int i = 0; i < max; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = catalog[i];
                if (entry != null)
                    AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, entry.DisplayName, "Select this item.", entry.ItemId, "IT", string.Equals(currentItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase), entry.Detail, entry.PreviewSprite);
            }
        }

        private static void AddConditionItemPickerActions(List<ScenarioAuthoringInspectorItem> items, string prefix, string currentItemId)
        {
            List<ScenarioInventoryItemCatalogEntry> catalog = ScenarioInventoryItemCatalog.Build();
            int max = Math.Min(12, catalog.Count);
            for (int i = 0; i < max; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = catalog[i];
                if (entry != null)
                    AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionGateConditionTargetPrefix, prefix, entry.DisplayName, "Select this required item.", entry.ItemId, "IT", string.Equals(currentItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase), entry.Detail, entry.PreviewSprite);
            }
        }

        private static void AddPairTokenAction(List<ScenarioAuthoringInspectorItem> items, string actionPrefix, string prefix, string label, string hint, string token, string icon)
        {
            AddPairTokenAction(items, actionPrefix, prefix, label, hint, token, icon, false, null, null);
        }

        private static void AddPairTokenAction(List<ScenarioAuthoringInspectorItem> items, string actionPrefix, string prefix, string label, string hint, string token, string icon, bool emphasized)
        {
            AddPairTokenAction(items, actionPrefix, prefix, label, hint, token, icon, emphasized, null, null);
        }

        private static void AddPairTokenAction(List<ScenarioAuthoringInspectorItem> items, string actionPrefix, string prefix, string label, string hint, string token, string icon, bool emphasized, string detail, Sprite preview)
        {
            items.Add(ActionItem(Action(actionPrefix + prefix + "." + EncodeToken(token), label, hint, true, emphasized, icon, detail, null, preview)));
        }

        private static string EncodeToken(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        private static ScenarioScheduleTime AddHours(ScenarioScheduleTime time, int hours)
        {
            ScenarioScheduleTime result = new ScenarioScheduleTime();
            result.Day = time != null ? time.Day : 1;
            result.Hour = time != null ? time.Hour : 0;
            result.Minute = time != null ? time.Minute : 0;
            int total = result.Hour + hours;
            result.Day += total / 24;
            result.Hour = total % 24;
            return result;
        }

        private static void AddScheduleActions(List<ScenarioAuthoringInspectorItem> items, string dayPrefix, string hourPrefix, int index)
        {
            AddScheduleActions(items, dayPrefix, hourPrefix, null, index);
        }

        private static void AddScheduleActions(List<ScenarioAuthoringInspectorItem> items, string dayPrefix, string hourPrefix, string minutePrefix, int index)
        {
            items.Add(ActionItem(Action(dayPrefix + index.ToString() + ".1", "Day +", "Move this scheduled entry one day later.", true, false, "D+")));
            items.Add(ActionItem(Action(dayPrefix + index.ToString() + ".-1", "Day -", "Move this scheduled entry one day earlier.", true, false, "D-")));
            items.Add(ActionItem(Action(hourPrefix + index.ToString() + ".1", "Hour +", "Move this scheduled entry one hour later.", true, false, "H+")));
            items.Add(ActionItem(Action(hourPrefix + index.ToString() + ".-1", "Hour -", "Move this scheduled entry one hour earlier.", true, false, "H-")));
            if (!string.IsNullOrEmpty(minutePrefix))
            {
                items.Add(ActionItem(Action(minutePrefix + index.ToString() + ".15", "Min +15", "Move this scheduled entry fifteen minutes later. Game days roll at 06:00.", true, false, "M+")));
                items.Add(ActionItem(Action(minutePrefix + index.ToString() + ".-15", "Min -15", "Move this scheduled entry fifteen minutes earlier. Game days roll at 06:00.", true, false, "M-")));
            }
        }

        private static int CompareSchedule(ScenarioScheduleTime left, ScenarioScheduleTime right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            int byDay = left.Day.CompareTo(right.Day);
            if (byDay != 0)
                return byDay;
            int byHour = left.Hour.CompareTo(right.Hour);
            if (byHour != 0)
                return byHour;
            return left.Minute.CompareTo(right.Minute);
        }

        private static string BuildFamilyMemberSummary(FamilyMemberConfig member)
        {
            if (member == null)
                return "Empty survivor";

            return member.Gender
                + " / " + FormatBody(member, false)
                + " / " + FormatStatLine(member)
                + " / " + FindTrait(member, "Strength:")
                + " / " + FindTrait(member, "Weakness:")
                + " / look " + FormatAppearance(member);
        }

        private static string FormatStatLine(FamilyMemberConfig member)
        {
            string[] statIds = ScenarioFamilyMemberFactory.StatIds;
            List<string> parts = new List<string>();
            for (int i = 0; i < statIds.Length; i++)
            {
                int rawValue = FindStatValue(member, statIds[i], 5);
                int displayValue = ClampStatDisplay(rawValue);
                parts.Add(statIds[i].Substring(0, 3) + " " + displayValue.ToString(CultureInfo.InvariantCulture) + (rawValue != displayValue ? " !" : string.Empty));
            }
            return string.Join(", ", parts.ToArray());
        }

        private static int FindStatValue(FamilyMemberConfig member, string statId, int fallback)
        {
            for (int i = 0; member != null && member.Stats != null && i < member.Stats.Count; i++)
            {
                StatOverride stat = member.Stats[i];
                if (stat != null && string.Equals(stat.StatId, statId, StringComparison.OrdinalIgnoreCase))
                    return stat.Value;
            }

            return fallback;
        }

        private static int ClampStatDisplay(int value)
        {
            return Mathf.Clamp(value, 0, 20);
        }

        private static string FormatStatDisplayDetail(int rawValue, int displayValue)
        {
            if (rawValue == displayValue)
                return null;

            return "Warning: XML value " + rawValue.ToString(CultureInfo.InvariantCulture)
                + " is outside 0-20; showing "
                + displayValue.ToString(CultureInfo.InvariantCulture)
                + ".";
        }

        private static string FindTrait(FamilyMemberConfig member, string prefix)
        {
            for (int i = 0; member != null && member.Traits != null && i < member.Traits.Count; i++)
            {
                string trait = member.Traits[i];
                if (!string.IsNullOrEmpty(trait) && trait.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return trait.Substring(prefix.Length);
            }

            return "<none>";
        }

        private static string FormatBody(FamilyMemberConfig member, bool showAdvancedDetails)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            bool adult = appearance == null || !appearance.IsAdult.HasValue || appearance.IsAdult.Value;
            string mesh = appearance != null && !string.IsNullOrEmpty(appearance.MeshId) ? appearance.MeshId : "<auto>";
            string label = ResolveBodyLabel(member, adult, mesh);
            return showAdvancedDetails ? label + " (" + mesh + ")" : label;
        }

        private static string FormatAgeBand(FamilyMemberConfig member)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            return FormatAgeBand(appearance == null || !appearance.IsAdult.HasValue || appearance.IsAdult.Value);
        }

        private static string FormatAgeBand(bool adult)
        {
            return adult ? "Adult" : "Child";
        }

        private static string ResolveBodyLabel(FamilyMemberConfig member, bool adult, string mesh)
        {
            if (string.Equals(mesh, "man", StringComparison.OrdinalIgnoreCase))
                return "Adult Male";
            if (string.Equals(mesh, "woman", StringComparison.OrdinalIgnoreCase))
                return "Adult Female";
            if (string.Equals(mesh, "boy", StringComparison.OrdinalIgnoreCase))
                return "Child Male";
            if (string.Equals(mesh, "girl", StringComparison.OrdinalIgnoreCase))
                return "Child Female";

            ScenarioGender gender = member != null ? member.Gender : ScenarioGender.Any;
            if (gender == ScenarioGender.Male)
                return adult ? "Adult Male" : "Child Male";
            if (gender == ScenarioGender.Female)
                return adult ? "Adult Female" : "Child Female";
            return adult ? "Adult Body" : "Child Body";
        }

        private static string FormatAppearance(FamilyMemberConfig member)
        {
            FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
            if (appearance == null)
                return "default";

            int count = 0;
            if (!string.IsNullOrEmpty(appearance.MeshId) || appearance.IsAdult.HasValue)
                count++;
            if (!string.IsNullOrEmpty(appearance.HeadTextureId) || !string.IsNullOrEmpty(appearance.HeadTexturePath))
                count++;
            if (!string.IsNullOrEmpty(appearance.TorsoTextureId) || !string.IsNullOrEmpty(appearance.TorsoTexturePath))
                count++;
            if (!string.IsNullOrEmpty(appearance.LegTextureId) || !string.IsNullOrEmpty(appearance.LegTexturePath))
                count++;
            if (!string.IsNullOrEmpty(appearance.HairColorHex))
                count++;
            if (!string.IsNullOrEmpty(appearance.SkinColorHex))
                count++;
            if (!string.IsNullOrEmpty(appearance.ShirtColorHex))
                count++;
            if (!string.IsNullOrEmpty(appearance.PantsColorHex))
                count++;

            return count == 0 ? "default" : count.ToString(CultureInfo.InvariantCulture) + " custom choices";
        }

        private static string FormatTexture(FamilyMemberAppearanceConfig appearance, ScenarioCharacterTexturePart part, bool showAdvancedDetails)
        {
            if (appearance == null)
                return "default";

            string id = null;
            string path = null;
            switch (part)
            {
                case ScenarioCharacterTexturePart.Head:
                    id = appearance.HeadTextureId;
                    path = appearance.HeadTexturePath;
                    break;
                case ScenarioCharacterTexturePart.Torso:
                    id = appearance.TorsoTextureId;
                    path = appearance.TorsoTexturePath;
                    break;
                case ScenarioCharacterTexturePart.Legs:
                    id = appearance.LegTextureId;
                    path = appearance.LegTexturePath;
                    break;
            }

            if (!string.IsNullOrEmpty(id))
                return showAdvancedDetails ? id : "Vanilla " + FormatTexturePart(part);
            if (!string.IsNullOrEmpty(path))
                return showAdvancedDetails ? path : "Custom " + FormatTexturePart(part);
            return "default";
        }

        private static string FormatTexturePart(ScenarioCharacterTexturePart part)
        {
            switch (part)
            {
                case ScenarioCharacterTexturePart.Head: return "Head";
                case ScenarioCharacterTexturePart.Torso: return "Top";
                case ScenarioCharacterTexturePart.Legs: return "Bottom";
                default: return "Texture";
            }
        }

        private static string FormatColor(FamilyMemberAppearanceConfig appearance, ScenarioCharacterColorPart part)
        {
            if (appearance == null)
                return "default";

            string color = null;
            switch (part)
            {
                case ScenarioCharacterColorPart.Hair:
                    color = appearance.HairColorHex;
                    break;
                case ScenarioCharacterColorPart.Skin:
                    color = appearance.SkinColorHex;
                    break;
                case ScenarioCharacterColorPart.Shirt:
                    color = appearance.ShirtColorHex;
                    break;
                case ScenarioCharacterColorPart.Pants:
                    color = appearance.PantsColorHex;
                    break;
            }

            return !string.IsNullOrEmpty(color) ? color : "default";
        }

        private static string GetCurrentWeatherSummary()
        {
            WeatherManager manager = WeatherManager.Instance;
            if (manager == null)
                return "WeatherManager unavailable";
            return manager.currentState + " / day " + manager.currentDay;
        }

        private static string FormatWeatherStateLabel(string state)
        {
            if (string.Equals(state, "None", StringComparison.OrdinalIgnoreCase))
                return "Clear Weather";
            if (string.Equals(state, "BlackRain", StringComparison.OrdinalIgnoreCase))
                return "Black Rain";
            if (string.Equals(state, "LightSand", StringComparison.OrdinalIgnoreCase))
                return "Light Sandstorm";
            if (string.Equals(state, "MediumSand", StringComparison.OrdinalIgnoreCase))
                return "Sandstorm";
            if (string.Equals(state, "HeavySand", StringComparison.OrdinalIgnoreCase))
                return "Heavy Sandstorm";
            return Safe(state);
        }

        private static string FormatWeatherGroupLabel(string state)
        {
            if (string.Equals(state, "None", StringComparison.OrdinalIgnoreCase))
                return "Recovery";
            if (string.Equals(state, "Rain", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "BlackRain", StringComparison.OrdinalIgnoreCase))
                return "Rain";
            if (string.Equals(state, "LightSand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "MediumSand", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "HeavySand", StringComparison.OrdinalIgnoreCase))
                return "Sandstorm";
            return "Weather";
        }

        private static string FormatTriggerTypeLabel(string type)
        {
            if (string.Equals(type, "Manual", StringComparison.OrdinalIgnoreCase))
                return "Manual trigger";
            if (string.Equals(type, "Scheduled", StringComparison.OrdinalIgnoreCase))
                return "Scheduled time";
            if (string.Equals(type, "ScenarioFlagSet", StringComparison.OrdinalIgnoreCase))
                return "Scenario flag";
            if (string.Equals(type, "QuestState", StringComparison.OrdinalIgnoreCase))
                return "Quest state";
            if (string.Equals(type, "ItemQuantityAvailable", StringComparison.OrdinalIgnoreCase))
                return "Stockpile item count";
            return Safe(type);
        }

        private static string FormatScheduledActionTypeLabel(string type)
        {
            if (string.Equals(type, "Generic", StringComparison.OrdinalIgnoreCase))
                return "Custom change";
            if (string.Equals(type, "Inventory", StringComparison.OrdinalIgnoreCase))
                return "Stockpile change";
            if (string.Equals(type, "Weather", StringComparison.OrdinalIgnoreCase))
                return "Weather change";
            if (string.Equals(type, "Quest", StringComparison.OrdinalIgnoreCase))
                return "Story change";
            if (string.Equals(type, "Object", StringComparison.OrdinalIgnoreCase))
                return "Object state change";
            return Safe(type);
        }

        private static string FormatSchedule(ScenarioScheduleTime time)
        {
            return ScenarioScheduleFormatter.Format(time);
        }

        private static string FormatTriggerSchedule(TriggerDef trigger)
        {
            if (trigger == null || trigger.Properties == null)
                return "manual";

            int day = ScenarioPropertyBag.GetInt(trigger.Properties, "day", 0);
            if (day <= 0)
                return "manual";

            int hour = ScenarioPropertyBag.GetInt(trigger.Properties, "hour", 8);
            int minute = ScenarioPropertyBag.GetInt(trigger.Properties, "minute", 0);
            return "day " + day.ToString(CultureInfo.InvariantCulture) + " " + hour.ToString("D2") + ":" + minute.ToString("D2");
        }

        private static string FormatConditionTarget(ScenarioConditionRef condition)
        {
            if (condition == null)
                return "missing condition";
            if (condition.Kind == ScenarioConditionKind.TimeReached)
                return FormatSchedule(condition.Time);
            if (!string.IsNullOrEmpty(condition.FlagId))
                return "flag " + condition.FlagId + "=" + condition.FlagValue;
            if (!string.IsNullOrEmpty(condition.TargetId))
                return "target " + condition.TargetId;
            if (!string.IsNullOrEmpty(condition.StatId))
                return "stat " + condition.StatId + ">=" + condition.StatValue.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(condition.TraitId))
                return "trait " + condition.TraitId;
            return condition.Kind.ToString();
        }

        private static ScenarioAuthoringInspectorSection BuildBunkerRuntimeSection(ScenarioDefinition definition, ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioBunkerGridDefinition grid = definition != null ? definition.BunkerGrid : null;
            items.Add(Property("Foundations", grid != null && grid.Foundations != null ? grid.Foundations.Count.ToString() : "0"));
            items.Add(Property("Cells", grid != null && grid.Cells != null ? grid.Cells.Count.ToString() : "0"));
            items.Add(Property("Expansions", grid != null && grid.Expansions != null ? grid.Expansions.Count.ToString() : "0"));
            items.Add(Property("Boundaries", grid != null && grid.Boundaries != null ? grid.Boundaries.Count.ToString() : "0"));
            if (state != null && state.SelectedTarget != null)
                items.Add(Property("Current Pick", Safe(state.SelectedTarget.DisplayName)));

            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count && i < 6; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;
                string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : "object_" + (i + 1).ToString();
                string dependency = !string.IsNullOrEmpty(placement.RequiredFoundationId) ? "foundation " + placement.RequiredFoundationId : !string.IsNullOrEmpty(placement.RequiredBunkerExpansionId) ? "expansion " + placement.RequiredBunkerExpansionId : "no support id";
                items.Add(Property(id, placement.StartState + " / " + dependency));
            }

            if (items.Count == 4)
                items.Add(Text("No authored object support dependencies have been captured yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "bunker_runtime_model",
                Title = "Bunker Runtime Model",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static List<ScenarioAuthoringInspectorItem> BuildGateItems(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateAdd, "Add Condition", "The event only fires while this is true.", true, true, "C+")));
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                if (gate == null)
                    continue;
                ScenarioConditionGroup group = gate.Conditions;
                items.Add(TimelineFact(state, "gate", i, Safe(gate.Id), (group != null ? group.Mode.ToString() : "All") + " / " + CountConditions(group).ToString() + " condition(s)", "The event only fires while this is true."));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateModePrefix + i.ToString(CultureInfo.InvariantCulture), "Match All/Any", "Switch this condition between requiring all checks and any check.", true, group != null && group.Mode == ScenarioConditionGroupMode.Any, "ANY")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateConditionAddPrefix + i.ToString(CultureInfo.InvariantCulture), "Add Check", "Add another check to this condition.", true, false, "C+")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateDeletePrefix + i.ToString(CultureInfo.InvariantCulture), "Remove Condition", "Remove this condition and clear scheduled change references to it.", true, false, "RM")));
                for (int c = 0; group != null && group.Conditions != null && c < group.Conditions.Count; c++)
                    AddConditionItems(items, definition, group.Conditions[c], i, c);
            }
            if (items.Count == 1)
                items.Add(Text("No reusable conditions or scenario flags have been authored yet."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildScheduledActionItems(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionAdd, "Add Scheduled Change", "Create a timed scenario change with an optional condition, repeat policy, and effects.", true, true, "A+")));
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action == null)
                    continue;
                string gate = string.IsNullOrEmpty(action.GateId) ? "no condition" : "condition " + action.GateId;
                string repeat = action.Policy != null && action.Policy.Repeatable ? "repeat " + action.Policy.CooldownMinutes.ToString(CultureInfo.InvariantCulture) + "m" : "once";
                items.Add(TimelineFact(state, "scheduled_action", i, Safe(action.Id), FormatScheduledActionTypeLabel(action.ActionType) + " / " + FormatSchedule(action.DueTime) + " / " + gate + " / " + repeat, "Timed scenario change."));
                AddScheduleActions(items, ScenarioAuthoringActionIds.ActionScheduledActionDayPrefix, ScenarioAuthoringActionIds.ActionScheduledActionHourPrefix, i);
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionTypePrefix + i.ToString(CultureInfo.InvariantCulture), "Cycle Action Type", "Cycle the primary effect template for this scheduled action.", true, false, "TY")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionGatePrefix + i.ToString(CultureInfo.InvariantCulture), "Cycle Condition", "Attach the next authored condition, or clear it when the list wraps.", true, !string.IsNullOrEmpty(action.GateId), "CN", gate)));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionRepeatPrefix + i.ToString(CultureInfo.InvariantCulture), "Toggle Repeat", "Switch this action between once-only and repeatable execution.", true, action.Policy != null && action.Policy.Repeatable, "RP")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionCooldownPrefix + i.ToString(CultureInfo.InvariantCulture) + ".30", "Cooldown +30", "Increase repeat cooldown by 30 game minutes.", true, false, "C+")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionCooldownPrefix + i.ToString(CultureInfo.InvariantCulture) + ".-30", "Cooldown -30", "Decrease repeat cooldown by 30 game minutes.", true, false, "C-")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionEffectAddPrefix + i.ToString(CultureInfo.InvariantCulture), "Add Effect", "Add another effect to this scheduled action.", true, false, "E+")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionDeletePrefix + i.ToString(CultureInfo.InvariantCulture), "Remove Scheduled Change", "Remove this scheduled scenario change.", true, false, "RM")));
                for (int e = 0; action.Effects != null && e < action.Effects.Count; e++)
                    AddEffectItems(items, definition, action.Effects[e], i, e);
            }
            if (items.Count == 1)
                items.Add(Text("No scheduled changes have been authored yet. Legacy schedules are converted at runtime."));
            return items;
        }

        private static int CountConditions(ScenarioConditionGroup group)
        {
            int count = 0;
            if (group != null && group.Conditions != null)
                count += group.Conditions.Count;
            for (int i = 0; group != null && group.Groups != null && i < group.Groups.Count; i++)
                count += CountConditions(group.Groups[i]);
            return count;
        }

        private ScenarioAuthoringInspectorAction[] BuildWindowHeaderActions(
            ScenarioAuthoringWindowDefinition windowDefinition,
            ScenarioAuthoringWindowState windowState,
            ScenarioAuthoringState state)
        {
            if (windowDefinition == null || windowState == null)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            if (string.Equals(windowDefinition.Id, ScenarioAuthoringWindowIds.PixelEditor, StringComparison.OrdinalIgnoreCase))
            {
                ScenarioSpriteSwapAuthoringService.CustomEditorModel editor = _sectionHub != null && _sectionHub.SpriteSwap != null
                    ? _sectionHub.SpriteSwap.GetCustomEditorModel(state)
                    : null;
                actions.Add(Action(
                    ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel,
                    "X",
                    editor != null && editor.Dirty ? "Discard pixel edits and close the pixel editor." : "Close the pixel editor.",
                    true,
                    false,
                    "HD",
                    null,
                    null,
                    null,
                    null));
                return actions.ToArray();
            }

            actions.Add(Action(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix + windowDefinition.Id, "-", "Collapse this panel into the Windows list.", true, false, "CL"));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionWindowTogglePrefix + windowDefinition.Id, "X", "Hide this panel.", true, false, "HD"));
            return actions.ToArray();
        }

        private static ScenarioAuthoringInspectorAction[] BuildInspectorHeaderActions(ScenarioAuthoringState state)
        {
            bool advanced = state != null && state.Settings != null && state.Settings.GetBool("debug.show_advanced_details", false);
            bool editPins = state != null && state.Settings != null && state.Settings.GetBool("inspector.pin_edit_mode", false);
            return new[]
            {
                Action(ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "debug.show_advanced_details", advanced ? "Advanced On" : "Advanced", "Toggle advanced inspector details.", true, advanced, "GEAR"),
                Action(ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "inspector.pin_edit_mode", editPins ? "Done Pins" : "Edit Pins", "Show pin and unpin controls for pinned facts.", true, editPins, "PIN"),
                Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current scenario target selection.", true, false, "CL")
            };
        }


        private ScenarioAuthoringSettingsViewModel BuildSettingsViewModel(ScenarioAuthoringState state)
        {
            ScenarioAuthoringSettingDefinition[] definitions = _settingsService.GetDefinitions();
            List<ScenarioAuthoringSettingsSectionViewModel> sections = new List<ScenarioAuthoringSettingsSectionViewModel>();
            Dictionary<string, List<ScenarioAuthoringSettingsItemViewModel>> bySection = new Dictionary<string, List<ScenarioAuthoringSettingsItemViewModel>>(StringComparer.OrdinalIgnoreCase);
            bool showAdvancedDetails = ShowAdvancedDetails(state);

            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringSettingDefinition definition = definitions[i];
                if (definition == null)
                    continue;
                if (IsAdvancedSetting(definition) && !showAdvancedDetails)
                    continue;

                List<ScenarioAuthoringSettingsItemViewModel> items;
                if (!bySection.TryGetValue(definition.Section, out items))
                {
                    items = new List<ScenarioAuthoringSettingsItemViewModel>();
                    bySection[definition.Section] = items;
                }

                string value = state.Settings != null ? state.Settings.Get(definition.Id, definition.DefaultValue) : definition.DefaultValue;
                float numericValue = ParseSettingNumber(definition, value);
                bool numericSetting = definition.Kind == ScenarioAuthoringSettingKind.Float || definition.Kind == ScenarioAuthoringSettingKind.Integer;
                items.Add(new ScenarioAuthoringSettingsItemViewModel
                {
                    Id = definition.Id,
                    Label = definition.Label,
                    Description = definition.Description,
                    ValueText = value,
                    Kind = definition.Kind,
                    BoolValue = state.Settings != null && state.Settings.GetBool(definition.Id, string.Equals(definition.DefaultValue, "true", StringComparison.OrdinalIgnoreCase)),
                    Enabled = definition.Kind != ScenarioAuthoringSettingKind.ReadOnly,
                    CanIncrease = numericSetting && numericValue < definition.MaxValue,
                    CanDecrease = numericSetting && numericValue > definition.MinValue,
                    ChoiceLabels = definition.ChoiceLabels,
                    ChoiceValues = definition.ChoiceValues,
                    SelectedChoiceIndex = ResolveChoiceIndex(definition, value)
                });
            }

            foreach (KeyValuePair<string, List<ScenarioAuthoringSettingsItemViewModel>> pair in bySection)
            {
                sections.Add(new ScenarioAuthoringSettingsSectionViewModel
                {
                    Id = pair.Key.ToLowerInvariant(),
                    Title = pair.Key,
                    Items = pair.Value.ToArray()
                });
            }

            sections.Sort(delegate(ScenarioAuthoringSettingsSectionViewModel left, ScenarioAuthoringSettingsSectionViewModel right)
            {
                return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });

            return new ScenarioAuthoringSettingsViewModel
            {
                Title = "Editor Settings",
                Subtitle = "Shell, layout, input, visuals, sprite tools, and debug preferences.",
                HeaderActions = new[]
                {
                    Action(ScenarioAuthoringActionIds.ActionShellSettingsReset, "Reset Defaults", "Restore default editor settings.", true, false)
                },
                Sections = sections.ToArray()
            };
        }

        private static int ResolveChoiceIndex(ScenarioAuthoringSettingDefinition definition, string value)
        {
            if (definition == null || definition.ChoiceValues == null)
                return -1;

            for (int i = 0; i < definition.ChoiceValues.Length; i++)
            {
                if (string.Equals(definition.ChoiceValues[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static bool IsAdvancedSetting(ScenarioAuthoringSettingDefinition definition)
        {
            return definition != null
                && string.Equals(definition.Section, "Advanced", StringComparison.OrdinalIgnoreCase);
        }

        private static float ParseSettingNumber(ScenarioAuthoringSettingDefinition definition, string value)
        {
            if (definition == null)
                return 0f;

            float parsed;
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                float.TryParse(definition.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
            if (definition.Kind == ScenarioAuthoringSettingKind.Integer)
                parsed = Mathf.RoundToInt(parsed);
            return parsed;
        }

        private static string[] BuildStatusEntries(ScenarioAuthoringState state, ScenarioEditorSession editorSession, ScenarioAuthoringSession session)
        {
            return new[]
            {
                state.StatusMessage ?? string.Empty,
                "Mode: " + state.ActiveShellTab,
                "Grid: " + (state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "On" : "Off"),
                "Snap: " + (state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true) ? "On" : "Off"),
                "Playtest: " + (editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"),
                "Draft: " + (session != null ? session.DraftId : Safe(state.ActiveDraftId))
            };
        }

        private static bool IsWindowInShell(ScenarioAuthoringWindowState windowState)
        {
            return windowState != null && (windowState.Visible || windowState.Collapsed);
        }

        private static bool ShowAdvancedDetails(ScenarioAuthoringState state)
        {
            return state != null
                && state.Settings != null
                && state.Settings.GetBool("debug.show_advanced_details", false);
        }

        private ScenarioAuthoringInspectorAction[] BuildContextMenuActions(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            if (target == null || target.Kind == ScenarioAuthoringTargetKind.None || target.Kind == ScenarioAuthoringTargetKind.Unknown)
                return actions.ToArray();

            string scopeReason;
            bool scopeAllowed = _selectionScopeService.CanSelectTargetForCurrentStage(state, target, out scopeReason);
            string disabledScopeReason = !scopeAllowed ? ShortMenuReason(scopeReason, "Outside active workspace.") : null;

            if (target.SupportsInspect)
                actions.Add(Action(
                    ScenarioAuthoringActionIds.ActionShellShow,
                    "Inspect",
                    "Open the inspector for this target.",
                    scopeAllowed,
                    false,
                    null,
                    null,
                    null,
                    null,
                    disabledScopeReason));

            if (target.SupportsReplace)
            {
                actions.Add(Action(
                    ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                    "Replace Look",
                    "Open the asset editor for this target.",
                    scopeAllowed,
                    false,
                    null,
                    null,
                    null,
                    null,
                    disabledScopeReason));

                actions.Add(Action(
                    ScenarioAuthoringActionIds.ActionSpriteSwapCopy,
                    "Copy Look",
                    "Copy this target's current authored look.",
                    scopeAllowed,
                    false,
                    null,
                    null,
                    null,
                    null,
                    disabledScopeReason));
            }

            if (target.Kind == ScenarioAuthoringTargetKind.SceneSprite
                && !string.IsNullOrEmpty(target.ScenarioReferenceId))
            {
                actions.Add(Action(
                    ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove,
                    "Remove Scene Sprite",
                    "Remove this authored scene sprite from the draft.",
                    scopeAllowed,
                    false,
                    null,
                    null,
                    null,
                    null,
                    disabledScopeReason));
            }

            return actions.ToArray();
        }

        private static string ShortMenuReason(string reason, string fallback)
        {
            string value = !string.IsNullOrEmpty(reason) ? reason : fallback;
            if (string.IsNullOrEmpty(value))
                return null;
            if (value.Length <= 64)
                return value;

            return value.Substring(0, 61) + "...";
        }

        private static string FormatClockTime()
        {
            try
            {
                int hours = GameTime.Hour;
                int minutes = GameTime.Minute;
                return hours.ToString("00") + ":" + minutes.ToString("00");
            }
            catch
            {
                return DateTime.Now.ToString("HH:mm");
            }
        }

        private static string FormatBaseMode(ScenarioBaseGameMode mode)
        {
            if (mode == ScenarioBaseGameMode.Survival)
                return "Standard";
            return mode.ToString();
        }
        private static ScenarioAuthoringInspectorAction[] BuildHeaderActions(ScenarioEditorSession editorSession, bool hasSelection)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            string playStartReason = null;
            bool isPlaytesting = editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting;
            bool canStartPlay = isPlaytesting || new ScenarioPlayStartReadiness().CanStartPlay(editorSession != null ? editorSession.WorkingDefinition : null, out playStartReason);
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSave, "Save", "Persist the current scenario draft XML.", true, true, "SV", "Write the current draft to scenario.xml."));
            actions.Add(Action(
                ScenarioAuthoringActionIds.ActionPlaytest,
                isPlaytesting ? "Stop Playtest" : "Playtest",
                canStartPlay ? "Toggle scenario playtest mode." : playStartReason,
                canStartPlay,
                canStartPlay,
                "PL",
                isPlaytesting
                    ? "End playtest and restore frozen authoring."
                    : canStartPlay ? "Start a live playtest from the current draft." : playStartReason,
                null,
                null,
                canStartPlay ? null : playStartReason));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and release scene ownership.", true, false, "EX", "Leave the scenario editor."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current selected target.", hasSelection, false, "CL", "Drop the current target selection.", null, null, hasSelection ? null : "No target is selected."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionConvertToNormal, "Convert Save", "Convert the current scenario-bound save into a normal save.", true, false, "CV", "Detach this save from the scenario editor."));
            return actions.ToArray();
        }

        private static ScenarioAuthoringInspectorAction[] BuildModalCloseHeaderActions(string actionId, string hint)
        {
            return new[]
            {
                Action(actionId, "X", hint, true, false, "HD")
            };
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
            Sprite previewSprite = null,
            string disabledReason = null)
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
                Emphasized = emphasized,
                DisabledReason = disabledReason
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

        private static ScenarioAuthoringInspectorItem CastCardItem(ScenarioCastCardViewModel card)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                CastCard = card
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

        private static ScenarioAuthoringInspectorItem Fact(string label, string value, string hoverHint = null, string pulseKey = null, string pulseSignature = null)
        {
            ScenarioAuthoringInspectorItem item = Property(label, value);
            item.HoverHint = hoverHint;
            item.PulseKey = pulseKey;
            item.PulseSignature = pulseSignature;
            return item;
        }

        private static ScenarioAuthoringInspectorItem TimelineFact(ScenarioAuthoringState state, string kind, int index, string label, string value, string hoverHint)
        {
            ScenarioAuthoringInspectorItem item = Fact(label, value, hoverHint);
            if (state != null
                && string.Equals(state.TimelineSelectedEntryId, kind + ":" + index.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
            {
                item.PulseKey = "timeline." + kind + "." + index.ToString(CultureInfo.InvariantCulture);
                item.PulseSignature = kind + ":" + index.ToString(CultureInfo.InvariantCulture) + ":" + value;
            }

            return item;
        }

        private static ScenarioAuthoringInspectorSection FactSection(string id, string title, List<ScenarioAuthoringInspectorItem> items)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = items != null ? items.ToArray() : new ScenarioAuthoringInspectorItem[0]
            };
        }

        private static ScenarioAuthoringInspectorSection ActionSection(string id, string title, List<ScenarioAuthoringInspectorItem> items)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items != null ? items.ToArray() : new ScenarioAuthoringInspectorItem[0]
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

        internal static string FormatTarget(ScenarioAuthoringTarget target)
        {
            return target == null ? "No target selected" : (target.DisplayName + " [" + target.Kind + "]");
        }

        private static GameObject ResolveGameObject(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.gameObject : null;
        }

        private static List<string> GetComponentNames(GameObject gameObject)
        {
            List<string> names = new List<string>();
            if (gameObject == null)
                return names;

            Component[] components = gameObject.GetComponents<Component>();
            int componentCount = 0;
            for (int i = 0; components != null && i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                componentCount++;
                string name = component.GetType().Name;
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                    names.Add(name);
            }

            if (names.Count > 8)
            {
                int hiddenCount = Math.Max(0, componentCount - 8);
                names = names.GetRange(0, 8);
                if (hiddenCount > 0)
                    names.Add("+" + hiddenCount + " more");
            }

            return names;
        }

        private static int CountLikelyTriggerReferences(ScenarioDefinition definition, ScenarioAuthoringTarget target)
        {
            if (definition == null || definition.TriggersAndEvents == null || target == null)
                return 0;

            int count = 0;
            for (int i = 0; definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (TriggerLikelyReferencesTarget(trigger, target))
                    count++;
            }

            return count;
        }

        private static bool TriggerLikelyReferencesTarget(TriggerDef trigger, ScenarioAuthoringTarget target)
        {
            if (trigger == null)
                return false;

            if (StringEquals(trigger.Id, target.ScenarioReferenceId)
                || StringContains(trigger.Id, target.TransformPath)
                || StringContains(trigger.Id, target.GameObjectName))
            {
                return true;
            }

            for (int i = 0; trigger.Properties != null && i < trigger.Properties.Count; i++)
            {
                ScenarioProperty property = trigger.Properties[i];
                string key = property != null ? property.Key : null;
                string value = property != null ? property.Value : null;
                if (StringEquals(value, target.ScenarioReferenceId)
                    || StringEquals(value, target.TransformPath)
                    || StringEquals(value, target.GameObjectName)
                    || StringContains(value, target.ScenarioReferenceId)
                    || StringContains(value, target.TransformPath)
                    || StringContains(value, target.GameObjectName)
                    || StringContains(key, target.ScenarioReferenceId)
                    || StringContains(key, target.TransformPath)
                    || StringContains(key, target.GameObjectName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StringEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StringContains(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(token)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private Sprite ResolvePreviewSprite(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget;
            return _runtimeResolver.TryResolve(target, out resolvedTarget) && resolvedTarget != null
                ? resolvedTarget.CurrentSprite
                : null;
        }

        internal static string CleanCandidateLabel(string label)
        {
            return string.IsNullOrEmpty(label) ? "<sprite>" : label;
        }

        internal static string BuildCandidateBadge(ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            if (candidate == null)
                return null;

            if (candidate.UserOwned)
                return "USER";

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.ScenarioCustom)
                return "MOD";

            return "LIVE";
        }

        private static string BuildSpriteCandidateBadge(
            ScenarioSpriteCatalogService.SpriteCandidate candidate,
            bool saved,
            bool previewed)
        {
            if (saved && previewed)
                return "SAVED / PREVIEW";
            if (previewed)
                return "PREVIEW";
            if (saved)
                return "SAVED";
            return BuildCandidateBadge(candidate);
        }

        internal static bool SameTarget(ScenarioAuthoringTarget left, ScenarioAuthoringTarget right)
        {
            if (left == null || right == null)
                return false;

            if (!string.IsNullOrEmpty(left.Id) && !string.IsNullOrEmpty(right.Id))
                return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(left.TransformPath) && !string.IsNullOrEmpty(right.TransformPath))
                return string.Equals(left.TransformPath, right.TransformPath, StringComparison.OrdinalIgnoreCase);

            return string.Equals(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(
            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker,
            string token)
        {
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(picker != null ? picker.VanillaCandidates : null, token);
            if (candidate != null)
                return candidate;

            return FindCandidate(picker != null ? picker.ModdedCandidates : null, token);
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string token)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.Token, token, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private static ScenarioAuthoringInspectorSection BuildSpriteCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage,
            string savedToken,
            string previewToken)
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

                    bool previewed = string.Equals(candidate.Token, previewToken, StringComparison.Ordinal);
                    bool saved = string.Equals(candidate.Token, savedToken, StringComparison.Ordinal);
                    items.Add(ActionItem(Action(
                        ScenarioSpriteSwapAuthoringService.BuildPreviewActionId(candidate.Token),
                        CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        previewed,
                        "RT",
                        candidate.SourceName,
                        BuildSpriteCandidateBadge(candidate, saved, previewed),
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

    }
}
