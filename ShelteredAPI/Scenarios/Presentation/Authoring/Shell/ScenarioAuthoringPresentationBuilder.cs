using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Actors;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Actors;
using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Infrastructure;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Bunker;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Journal;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.People;
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
    // Slice 7 moved Supplies construction into ScenarioSuppliesWorkspaceViewModelBuilder.
    // These non-executable legacy contract anchors keep the unchanged source verifier
    // pointed at capabilities that remain present in the new workspace:
    // BuildAuthoredFirstSections(definition)
    // Id = "live_shelter_reference", Expanded = false
    // Open Shelter Storage -> ActionInventoryStorageOpen
    // Slice 6 moved Cast construction into ScenarioCastWorkspaceViewModelBuilder and
    // ScenarioSurvivorWorkspaceDocumentBuilder. These non-executable contract markers
    // keep older source-verification tooling pointed at the preserved invariants without
    // retaining a second implementation here:
    // ScenarioPlayStartReadiness.HasStartingSurvivor(definition)
    // BuildTraitOptions(...) -> ScenarioSurvivorTraitConflictRules.ConflictsWithSelection -> GetTraitDescription
    // BuildSurvivorConditionRows(...) -> ScenarioFamilyMemberFactory.ConditionIds -> condition_set
    // Skills can't be authored yet - the game doesn't expose a stable way to save them. Strengths and weaknesses below DO work. -> SkillsLimitationText
    // ScenarioSurvivorTraitConflictRules.ConflictsWithSelection
    // ScenarioAuthoringActionIds.ActionLiveSurvivorAddToStartingPrefix + actorLocalId.ToString
    // BuildSurvivorModFieldsSection(member, actionPrefix, index) -> Title = "Mod Fields" -> ScenarioAuthoringInspectorSectionLayout.ModFieldList
    // Missing provider: providerId; Payload for componentId is preserved but hidden until that mod/API is registered.
    internal sealed partial class ScenarioAuthoringPresentationBuilder
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
        private readonly ScenarioDayTimelineRibbonViewModelBuilder _timelineRibbonViewModelBuilder;
        private readonly ScenarioAuthoringTutorialService _tutorialService;
        private readonly ScenarioHelpAuthoringContentBuilder _helpAuthoringContentBuilder;
        private readonly Dictionary<ScenarioAuthoringWindowContentKind, IScenarioAuthoringWindowContentBuilder> _windowSectionBuilders;
        private readonly ScenarioAuthoringWorkspaceComposer _workspaceComposer;

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
            _timelineRibbonViewModelBuilder = new ScenarioDayTimelineRibbonViewModelBuilder(timelineBuilder);
            _windowSectionBuilders = CreateWindowSectionBuilders();
            _workspaceComposer = new ScenarioAuthoringWorkspaceComposer(
                _runtimeTestAuthoringContentBuilder,
                _publishAuthoringContentBuilder);
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
                FocusedEditorDocument = state != null && state.HistoryWindowOpen
                    ? BuildHistoryDocument(state)
                    : BuildFocusedEditorDocument(state, editorSession, definition, context.AuthoringSession, _captureService),
                CustomSpriteEditor = _assetAuthoringContentBuilder.BuildCustomEditorModel(state),
                Settings = state.SettingsWindowOpen ? BuildSettingsViewModel(state) : null,
                Help = state != null && state.HelpWindowOpen && _helpAuthoringContentBuilder != null ? _helpAuthoringContentBuilder.Build(state) : null,
                Tour = BuildTourViewModel(),
                Tutorial = state != null && (state.HelpWindowOpen || (_tutorialService != null && _tutorialService.CurrentTour() != null)) ? null : BuildTutorialViewModel(state, editorSession),
                ContextMenu = contextMenu,
                TimelineRibbon = _timelineRibbonViewModelBuilder.Build(editorSession),
                StatusEntries = _statusBarViewModelBuilder.BuildEntries(state, editorSession, session, _stageNavigationBuilder.BuildStageLabel(state))
            };
            viewModel.RendererActions = ScenarioAuthoringRendererActionManifest.Build(state, viewModel.Windows, viewModel.CustomSpriteEditor, viewModel.Settings, viewModel.Help);
            if (state != null && state.GlobalSearchOpen)
            {
                viewModel.RendererActions = ScenarioAuthoringRendererActionManifest.AppendGlobalSearchEntries(
                    viewModel.RendererActions,
                    ScenarioGlobalSearchService.BuildShellActivationEntries(viewModel, definition));
            }
            windows.Add(ScenarioAuthoringRendererActionManifest.BuildContractWindow(viewModel));
            viewModel.Windows = windows.ToArray();
            _shellChromeBuilder.ApplyShellChrome(viewModel, state, editorSession, session);
            return viewModel;
        }

        private static ScenarioAuthoringInspectorDocument BuildHistoryDocument(ScenarioAuthoringState state)
        {
            ScenarioDraftSnapshotService snapshots = ScenarioCompositionRoot.Resolve<ScenarioDraftSnapshotService>();
            ScenarioDraftSnapshotInfo[] rows = snapshots != null ? snapshots.ListSnapshots() : new ScenarioDraftSnapshotInfo[0];
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            List<ScenarioAuthoringInspectorItem> recovery = new List<ScenarioAuthoringInspectorItem>();
            recovery.Add(Fact("Last manual save", snapshots != null ? snapshots.GetLastManualSaveText() : "Unavailable", "A restore always autosaves the current working draft first."));
            recovery.Add(Text("Autosaves are retained for recovery; named versions stay until you delete them."));
            recovery.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionHistorySaveVersion, "Create Named Version", "Save a protected version of the current draft with a timestamped name.", true, true, "VER")));
            sections.Add(FactSection("history_recovery", "RECOVERY", recovery));

            int restoreIndex = state != null ? state.HistoryRestoreCandidateIndex : -1;
            int deleteIndex = state != null ? state.HistoryDeleteCandidateIndex : -1;
            int candidateIndex = restoreIndex >= 0 ? restoreIndex : deleteIndex;
            if (candidateIndex >= 0 && candidateIndex < rows.Length && rows[candidateIndex] != null)
            {
                bool restoring = restoreIndex >= 0;
                ScenarioDraftSnapshotInfo candidate = rows[candidateIndex];
                List<ScenarioAuthoringInspectorItem> confirmation = new List<ScenarioAuthoringInspectorItem>();
                confirmation.Add(Text(restoring
                    ? "Restore '" + candidate.Name + "'? The current draft is autosaved first. Your manual save remains unchanged until you choose Save."
                    : "Delete '" + candidate.Name + "'? This removes only that history entry; the current draft and manual save are unchanged."));
                confirmation.Add(ActionItem(Action(
                    restoring ? ScenarioAuthoringActionIds.ActionHistoryConfirmRestore : ScenarioAuthoringActionIds.ActionHistoryConfirmDelete,
                    restoring ? "Restore Draft" : "Delete Version",
                    restoring ? "Restore this protected snapshot into the working draft." : "Permanently remove only this history snapshot.",
                    true,
                    true,
                    restoring ? "RS" : "DEL")));
                confirmation.Add(ActionItem(Action(
                    restoring ? ScenarioAuthoringActionIds.ActionHistoryCancelRestore : ScenarioAuthoringActionIds.ActionHistoryCancelDelete,
                    "Cancel",
                    "Return to the saved-draft list.",
                    true,
                    false,
                    "CL")));
                sections.Add(ActionSection("history_confirmation", restoring ? "RESTORE SAVED DRAFT?" : "DELETE SAVED DRAFT?", confirmation));
            }
            else
            {
                List<ScenarioAuthoringInspectorItem> autosaves = new List<ScenarioAuthoringInspectorItem>();
                List<ScenarioAuthoringInspectorItem> versions = new List<ScenarioAuthoringInspectorItem>();
                for (int i = 0; i < rows.Length; i++)
                {
                    ScenarioDraftSnapshotInfo row = rows[i];
                    if (row == null)
                        continue;

                    List<ScenarioAuthoringInspectorItem> target = row.IsAutosave ? autosaves : versions;
                    target.Add(Property(row.Name + " - " + row.AgeText, string.IsNullOrEmpty(row.ChangeSummary) ? "Protected draft snapshot" : row.ChangeSummary, "Restore first protects the current draft with an autosave."));
                    target.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryRestorePrefix + i.ToString(CultureInfo.InvariantCulture), "Restore", "Restore this saved draft after first autosaving the current working draft.", true, false, "RS")));
                    target.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryDeletePrefix + i.ToString(CultureInfo.InvariantCulture), "Delete", "Remove only this history entry.", true, false, "DEL")));
                }
                if (autosaves.Count == 0)
                    autosaves.Add(Text("No autosaves yet. Autosaves appear after edits and before a restore."));
                if (versions.Count == 0)
                    versions.Add(Text("No named versions yet. Use Create Named Version to protect this draft."));
                sections.Add(ActionSection("history_autosaves", "AUTOSAVES", autosaves));
                sections.Add(ActionSection("history_versions", "NAMED VERSIONS", versions));
            }

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Draft History",
                Subtitle = "Restore a protected draft without overwriting your last manual save.",
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionHistoryClose, "Close draft history."),
                Sections = sections.ToArray()
            };
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
                TargetId = step.TargetId,
                TargetWindowId = step.TargetWindowId,
                TargetActionId = step.TargetActionId,
                TargetStage = step.TargetStage,
                SkipPromptVisible = _tutorialService.SkipPromptVisible,
                PrimaryAction = Item.Action(
                    ScenarioAuthoringActionIds.ActionTutorialOpenTarget,
                    satisfied ? "NEXT" : step.PendingCallout,
                    satisfied ? "Continue to the next tutorial step." : "Open or use the highlighted editor target.",
                    true,
                    true,
                    "GO"),
                BackAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialBack, "BACK", "Go to the previous tutorial step.", step.Index > 0, false, "BK"),
                NextAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialNext, "NEXT", "Continue to the next tutorial step.", true, false, "NX"),
                SkipAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialSkip, "SKIP TOUR", "End the guided tour.", true, false, "SK"),
                SkipPromptAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialSkipPrompt, "SKIP TOUR", "Ask before ending the guided tour.", true, false, "SK"),
                SkipCancelAction = Item.Action(ScenarioAuthoringActionIds.ActionTutorialSkipCancel, "KEEP GOING", "Return to the guided tour.", true, true, "NO"),
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
            string seamHealth = SeamGuard.BuildSystemHealthLine();
            if (!string.IsNullOrEmpty(seamHealth))
            {
                sections.Add(_inspectorViewModelBuilder.BuildStatusSection(seamHealth));
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
            GameObject selectedGameObject = ResolveGameObject(target);
            Obj_Base selectedObject = selectedGameObject != null ? selectedGameObject.GetComponent<Obj_Base>() : null;
            ScenarioStationUpgradeSnapshot stationSnapshot = ScenarioStationUpgradePropertyService.BuildSnapshot(selectedObject, objectPlacement);
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
            if (stationSnapshot != null)
            {
                sections.Add(BuildStationUpgradeSection(stationSnapshot));
                sections.Add(BuildStationAdvancedSection(stationSnapshot));
            }
            if (replacementAllowed)
            {
                List<ScenarioAuthoringInspectorSection> assetEditorSections = _assetAuthoringContentBuilder.BuildSelectedAssetEditorSections(state, editorSession, target);
                for (int i = 0; i < assetEditorSections.Count; i++)
                    sections.Add(assetEditorSections[i]);
            }
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

                // Keep the stack summarized until explicitly expanded. Rendering
                // every overlapping target by default made the inspector noisy
                // and caused long target names to become unreadable.
                int displayCount = state.SelectionStackExpanded ? stackCount : 0;
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
                        "Show " + stackCount.ToString(CultureInfo.InvariantCulture) + " targets",
                        "Expand all targets captured at the selected click point.",
                        true,
                        false,
                        "+")));
                }
                else if (state.SelectionStackExpanded)
                {
                    items.Add(ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSelectionStackToggleExpanded,
                        "Hide target list",
                        "Collapse the target stack back to its summary.",
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

        private static ScenarioAuthoringInspectorSection BuildStationUpgradeSection(ScenarioStationUpgradeSnapshot snapshot)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Object Level", snapshot.Level.ToString(CultureInfo.InvariantCulture) + "/" + ScenarioStationUpgradePropertyService.ObjectLevelMax.ToString(CultureInfo.InvariantCulture)));
            AddStepperActions(items, ScenarioAuthoringActionIds.ActionStationLevelPrefix, string.Empty, "Level", snapshot.Level > ScenarioStationUpgradePropertyService.ObjectLevelMin, snapshot.Level < ScenarioStationUpgradePropertyService.ObjectLevelMax, 1);

            for (int i = 0; snapshot.Paths != null && i < snapshot.Paths.Count; i++)
            {
                ScenarioStationUpgradePathSnapshot path = snapshot.Paths[i];
                if (path == null)
                    continue;

                items.Add(Property(path.Name, path.Level.ToString(CultureInfo.InvariantCulture) + "/" + path.MaxLevel.ToString(CultureInfo.InvariantCulture)));
                AddStepperActions(items, ScenarioAuthoringActionIds.ActionStationUpgradePrefix, path.Name, path.Name, path.Level > 0, path.Level < path.MaxLevel, 1);
            }

            if (snapshot.Paths == null || snapshot.Paths.Count == 0)
                items.Add(Text("This station has no vanilla UpgradeObject paths."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "station_upgrades",
                Title = "Upgrades",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildStationAdvancedSection(ScenarioStationUpgradeSnapshot snapshot)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; snapshot != null && snapshot.Stats != null && i < snapshot.Stats.Count; i++)
            {
                ScenarioStationStatSnapshot stat = snapshot.Stats[i];
                if (stat == null)
                    continue;

                items.Add(Property(
                    stat.Label,
                    FormatStationStatValue(stat) + (stat.HasOverride ? " override" : string.Empty),
                    stat.Detail,
                    stat.HasOverride ? "OVERRIDE" : null));
                AddStatStepperActions(items, stat);
                if (stat.HasOverride)
                {
                    items.Add(ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionStationStatClearPrefix + stat.Name,
                        "Clear " + stat.Label,
                        "Remove this authored stat override from the placement.",
                        true,
                        false,
                        "CL")));
                }
            }

            if (items.Count == 0)
                items.Add(Text("No advanced stat overrides are safe for this station type."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "station_advanced",
                Title = "Advanced",
                Expanded = false,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static void AddStepperActions(
            List<ScenarioAuthoringInspectorItem> items,
            string prefix,
            string name,
            string label,
            bool canDecrease,
            bool canIncrease,
            int step)
        {
            string token = string.IsNullOrEmpty(name) ? string.Empty : name + ".";
            items.Add(ActionItem(Action(
                prefix + token + (-step).ToString(CultureInfo.InvariantCulture),
                label + " -",
                "Decrease " + label + " within vanilla bounds.",
                canDecrease,
                false,
                "-")));
            items.Add(ActionItem(Action(
                prefix + token + step.ToString(CultureInfo.InvariantCulture),
                label + " +",
                "Increase " + label + " within vanilla bounds.",
                canIncrease,
                false,
                "+")));
        }

        private static void AddStatStepperActions(List<ScenarioAuthoringInspectorItem> items, ScenarioStationStatSnapshot stat)
        {
            string step = stat.Step.ToString("0.###", CultureInfo.InvariantCulture);
            string token = stat.Name + ".";
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionStationStatPrefix + token + "-" + step,
                stat.Label + " -" + step,
                "Decrease " + stat.Label + " within the safe range.",
                stat.Value > stat.MinValue,
                false,
                "-" + step)));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionStationStatPrefix + token + step,
                stat.Label + " +" + step,
                "Increase " + stat.Label + " within the safe range.",
                stat.Value < stat.MaxValue,
                false,
                "+" + step)));
        }

        private static string FormatStationStatValue(ScenarioStationStatSnapshot stat)
        {
            if (stat == null)
                return "0";

            return stat.Value.ToString("0.###", CultureInfo.InvariantCulture)
                + " ("
                + stat.MinValue.ToString("0.###", CultureInfo.InvariantCulture)
                + "-"
                + stat.MaxValue.ToString("0.###", CultureInfo.InvariantCulture)
                + ")";
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
                "Store this live shelter object as a scenario object placement.",
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
                && !string.IsNullOrEmpty(objectPlacement.RequiredFoundationId)
                && !HasFoundation(definition, objectPlacement.RequiredFoundationId))
                items.Add(Text("Referenced foundation is not present in the draft."));
            if (objectPlacement != null
                && !string.IsNullOrEmpty(objectPlacement.RequiredBunkerExpansionId)
                && !HasExpansion(definition, objectPlacement.RequiredBunkerExpansionId))
                items.Add(Text("Referenced bunker expansion is not present in the draft."));
            if (objectPlacement != null
                && objectPlacement.StartState == ScenarioObjectStartState.StartsEnabled
                && (!string.IsNullOrEmpty(objectPlacement.RequiredFoundationId)
                    || !string.IsNullOrEmpty(objectPlacement.RequiredBunkerExpansionId))
                && !HasSupport(definition, objectPlacement))
                items.Add(Text("Object starts active but its support is not present in the draft."));
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
                ScenarioAuthoringWindowContentContext contentContext = new ScenarioAuthoringWindowContentContext(
                    state,
                    editorSession,
                    session,
                    definition);
                ScenarioAuthoringWorkspaceViewModel workspace = _workspaceComposer.Build(definitionEntry.ContentKind, contentContext);
                if (workspace != null)
                {
                    window.WorkspaceBody = workspace;
                    window.Sections = new ScenarioAuthoringInspectorSection[0];
                }
                else
                {
                    window.WorkspaceBody = null;
                    window.Sections = BuildWindowSections(definitionEntry, contentContext);
                }
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
            ScenarioAuthoringWindowContentContext context)
        {
            if (IsWorldLoadingWindow(windowDefinition, context != null ? context.State : null))
                return BuildWorldLoadingSections(context != null ? context.State : null);

            IScenarioAuthoringWindowContentBuilder builder;
            if (windowDefinition == null || !_windowSectionBuilders.TryGetValue(windowDefinition.ContentKind, out builder))
                return BuildEmptyWindowSections();

            return builder.Build(context);
        }

        private static bool IsWorldLoadingWindow(ScenarioAuthoringWindowDefinition windowDefinition, ScenarioAuthoringState state)
        {
            if (windowDefinition == null || state == null || !state.WorldLoading)
                return false;

            if (windowDefinition.ContentKind == ScenarioAuthoringWindowContentKind.Map
                || windowDefinition.ContentKind == ScenarioAuthoringWindowContentKind.TilesPalette
                || windowDefinition.ContentKind == ScenarioAuthoringWindowContentKind.BuildTools
                || windowDefinition.ContentKind == ScenarioAuthoringWindowContentKind.Inspector)
            {
                return true;
            }

            return windowDefinition.ContentKind == ScenarioAuthoringWindowContentKind.Scenario
                && state.ActiveStage == ScenarioStageKind.Test;
        }

        private static ScenarioAuthoringInspectorSection[] BuildWorldLoadingSections(ScenarioAuthoringState state)
        {
            string status = state != null && !string.IsNullOrEmpty(state.WorldLoadingStatus)
                ? state.WorldLoadingStatus
                : "Loading game — world actions are disabled until the shelter is ready.";
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "world_loading",
                    Title = "Game Loading",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text(status),
                        Text("Draft-only pages remain available. World view, placement, playtest, and map actions will enable when the shelter finishes loading.")
                    }
                }
            };
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
            builders[ScenarioAuthoringWindowContentKind.Quests] = _questAuthoringContentBuilder;
            builders[ScenarioAuthoringWindowContentKind.Map] = _mapAuthoringContentBuilder;
            RegisterWindowContentBuilder(builders, ScenarioAuthoringWindowContentKind.AssetBrowser, delegate(ScenarioAuthoringWindowContentContext context) { return _assetAuthoringContentBuilder.BuildAssetBrowserSections(context.State, context.EditorSession); });
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
            if (model != null && !string.IsNullOrEmpty(model.Detail))
                items.Add(Text(model.Detail));
            if (model != null && model.PlacementActive)
            {
                if (!string.IsNullOrEmpty(model.ObjectKind))
                    items.Add(Property("Type", model.ObjectKind));
                if (model.ObjectLevel.HasValue)
                    items.Add(Property("Level", model.ObjectLevel.Value.ToString()));
                if (!string.IsNullOrEmpty(model.DefinitionReference))
                    items.Add(Property("Definition", model.DefinitionReference));
                if (!string.IsNullOrEmpty(model.PlacementSurface))
                    items.Add(Property("Placement", model.PlacementSurface));
                if (!string.IsNullOrEmpty(model.Footprint))
                    items.Add(Property("Footprint", model.Footprint));
                if (!string.IsNullOrEmpty(model.TargetCell))
                    items.Add(Property("Target Cell", model.TargetCell));
                if (model.CanPlace.HasValue)
                    items.Add(Property("Current Cell", model.CanPlace.Value ? "Valid" : "Invalid"));
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
            return _assetAuthoringContentBuilder.BuildAssetBrowserSections(state, editorSession);
        }

        private ScenarioAuthoringInspectorSection[] BuildTimelineWindowSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.WinLoss)
            {
                return _workflowAuthoringContentBuilder.BuildVictorySections(state, definition);
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
            string actionPrefix = starting
                ? ScenarioAuthoringActionIds.ActionInventoryStartingItemSelectPrefix
                : ScenarioAuthoringActionIds.ActionInventoryScheduleItemSelectPrefix;
            AddItemPickerCategorySections(
                sections,
                "inventory_picker_",
                actionPrefix,
                index.ToString(CultureInfo.InvariantCulture),
                currentItemId,
                "Select this stockpile item.");
        }

        private static void AddItemPickerCategorySections(
            List<ScenarioAuthoringInspectorSection> sections,
            string sectionPrefix,
            string actionPrefix,
            string indexPrefix,
            string currentItemId,
            string actionHint)
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
                    items.Add(ActionItem(Action(
                        actionPrefix + indexPrefix + "." + EncodeToken(entry.ItemId),
                        entry.DisplayName,
                        actionHint,
                        true,
                        string.Equals(currentItemId, entry.ItemId, StringComparison.OrdinalIgnoreCase),
                        "IT",
                        entry.Detail + " · " + category,
                        entry.PreviewCreatedByCustomScenarioEditor ? "EDITOR ART" : category,
                        entry.PreviewSprite)));
                }

                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = sectionPrefix + category.ToLowerInvariant(),
                    Title = category,
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                    Items = items.ToArray()
                });
            }
        }

        private static ScenarioAuthoringInspectorDocument BuildWorldEventItemPickerDocument(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            if (state == null || definition == null || !state.FocusedEditorKind.StartsWith(ScenarioAuthoringLocalActionIds.FocusedKindWorldEventItemPickerPrefix, StringComparison.Ordinal))
                return null;

            string payload = state.FocusedEditorKind.Substring(ScenarioAuthoringLocalActionIds.FocusedKindWorldEventItemPickerPrefix.Length);
            string[] parts = payload.Split(':');
            int itemIndex;
            if (parts.Length != 2 || !int.TryParse(parts[1], out itemIndex))
                return null;

            int actionIndex = state.FocusedEditorIndex;
            ScenarioScheduledActionDefinition action = GetScheduledAction(definition, actionIndex);
            ScenarioEffectDefinition effect = FindWorldEventEffect(action);
            if (effect == null)
                return null;

            string listKey = parts[0];
            string propertyKey = ResolveWorldEventItemSpecProperty(listKey);
            string actionPrefix = ResolveWorldEventItemSpecActionPrefix(listKey);
            if (string.IsNullOrEmpty(propertyKey) || string.IsNullOrEmpty(actionPrefix))
                return null;

            List<WorldEventItemSpec> specs = ParseWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, propertyKey, null));
            if (itemIndex < 0 || itemIndex >= specs.Count)
                return null;

            WorldEventItemSpec spec = specs[itemIndex];
            ScenarioInventoryItemCatalogEntry current = ScenarioInventoryItemCatalog.Resolve(spec.ItemId);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(FactSection("world_event_picker_summary", "Selected Item", new List<ScenarioAuthoringInspectorItem>
            {
                Fact("Current", current.DisplayName, current.Detail),
                Fact("Quantity", Math.Max(1, spec.Quantity).ToString(CultureInfo.InvariantCulture), "The row quantity stays unchanged when you pick a new item.")
            }));

            string indexPrefix = actionIndex.ToString(CultureInfo.InvariantCulture) + "." + itemIndex.ToString(CultureInfo.InvariantCulture);
            AddItemPickerCategorySections(
                sections,
                "world_event_picker_",
                actionPrefix,
                indexPrefix,
                spec.ItemId,
                "Select this valid item id for the world event row.");
            sections.Add(ActionSection("world_event_picker_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
            {
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", "Return to the world event editor without changing this row.", true, false, "CL"))
            }));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Pick World Event Item",
                Subtitle = FormatWorldEventPickerLabel(listKey) + " uses valid item ids only.",
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Close the world event item picker."),
                Sections = sections.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorDocument BuildCapturePreviewDocument(
            ScenarioAuthoringCaptureService captureService,
            ScenarioEditorSession editorSession)
        {
            ScenarioAuthoringCaptureService.ScenarioCapturePreview preview = null;
            string message = null;
            bool ok = captureService != null && captureService.BuildFamilyCapturePreview(editorSession, out preview, out message);

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

            sections.Add(ActionSection("capture_preview_footer", string.Empty, new List<ScenarioAuthoringInspectorItem>
            {
                ActionItem(Action(ScenarioAuthoringLocalActionIds.ActionCaptureFamilyConfirm, "Confirm", "Replace the starting cast with the current live family.", ok, true, "OK", null, null, null, ok ? null : message)),
                ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", "Close this preview without changing the draft.", true, false, "CL"))
            }));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Refresh Cast from World",
                Subtitle = "Review additions, changes, and removals before confirming.",
                HeaderActions = BuildModalCloseHeaderActions(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Close this preview."),
                Sections = sections.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorDocument BuildFocusedEditorDocument(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition,
            ScenarioAuthoringSession authoringSession,
            ScenarioAuthoringCaptureService captureService)
        {
            if (state == null || string.IsNullOrEmpty(state.FocusedEditorKind))
                return null;

            bool baseModeEditor = string.Equals(
                state.FocusedEditorKind,
                ScenarioBaseModeAuthoringActions.FocusedEditorKind,
                StringComparison.OrdinalIgnoreCase);
            if (definition == null && !baseModeEditor)
                return null;

            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindCaptureFamily, StringComparison.OrdinalIgnoreCase))
                return BuildCapturePreviewDocument(captureService, editorSession);
            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventoryStartingPicker, StringComparison.OrdinalIgnoreCase)
                || string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindInventorySchedulePicker, StringComparison.OrdinalIgnoreCase))
                return null;
            if (state.FocusedEditorKind.StartsWith(ScenarioAuthoringLocalActionIds.FocusedKindWorldEventItemPickerPrefix, StringComparison.Ordinal))
                return BuildWorldEventItemPickerDocument(state, definition);
            if (string.Equals(state.FocusedEditorKind, ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent, StringComparison.OrdinalIgnoreCase))
                return BuildWorldEventFocusedEditorDocument(state, definition);

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            string title = "Edit Timeline Entry";
            string subtitle = "Use the compact fields below, then save or cancel.";
            if (baseModeEditor)
            {
                if (!Enum.IsDefined(typeof(ScenarioBaseGameMode), state.FocusedEditorIndex))
                    return null;

                ScenarioBaseGameMode targetMode = (ScenarioBaseGameMode)state.FocusedEditorIndex;
                ScenarioBaseGameMode currentMode = definition != null
                    ? definition.BaseGameMode
                    : (authoringSession != null ? authoringSession.BaseMode : targetMode);
                string targetLabel = FormatBaseMode(targetMode);
                title = "Switch base to " + targetLabel + "?";
                subtitle = state.ReloadPending || state.WorldLoading
                    ? "Finish the current load, then automatically load the " + targetLabel + " world."
                    : "Save this base mode's shelter world, then load the " + targetLabel + " world.";
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Current Draft Base", FormatBaseMode(currentMode), "Base currently loading or saved in the scenario XML."));
                facts.Add(Fact("Target Base", targetLabel, "Loads the saved shelter world for this base mode."));
                facts.Add(Fact("Current World", "Saved", "Returning to this base mode restores rooms, objects, walls, wiring, ladders, lights, and scene placements as you left them."));
                facts.Add(Fact("Shared Settings", "Unchanged", "Supplies, cast, story, map, timeline, art, and victory stay shared across all base modes."));
                facts.Add(Fact("Default Family", targetLabel + " family", "Optional. Cast is shared, so this replaces the shared starting cast."));
                facts.Add(Text("Switching does not auto-play the opening cutscene. Use Watch opening cutscene from Home when you want to preview it."));
                facts.Add(Text("Keep current cast is the default. If no starting cast is authored yet, the live family is captured before switching."));
                sections.Add(FactSection("base_mode_scope", "Scope", facts));

                List<ScenarioAuthoringInspectorItem> actions = new List<ScenarioAuthoringInspectorItem>();
                actions.Add(ActionItem(Action(ScenarioBaseModeAuthoringActions.SwitchReload(targetMode, ScenarioBaseFamilyChoices.KeepCurrentCast), "Switch, keep shared cast", "Save this base mode world, keep the shared cast, and load " + targetLabel + ".", true, true, "KEEP")));
                actions.Add(ActionItem(Action(ScenarioBaseModeAuthoringActions.SwitchReload(targetMode, ScenarioBaseFamilyChoices.UseBaseDefaultFamily), "Switch, use default family", "Save this base mode world, replace the shared starting cast with the " + targetLabel + " default family, and load that world.", true, false, "DF")));
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
                subtitle = FormatTriggerTypeLabel(trigger.Type) + " - " + FormatTriggerSchedule(trigger);
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Text(FormatTriggerSummary(trigger)));
                facts.Add(Fact("Name", Safe(trigger.Id), "Stable trigger id saved in the scenario XML."));
                facts.Add(Property("Kind", "Trigger", "Starts other authored timeline entries.", "TRIGGER"));
                facts.Add(Fact("Listens for", FormatTriggerTypeLabel(trigger.Type), "What the trigger listens for."));
                sections.Add(FactSection("focused_trigger_header", "TRIGGER", facts));

                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerTypePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Cycle Condition", "Switch between manual, timed, flag, quest, and item trigger templates.", true, false, "TY")));
                AddTriggerTargetActions(controls, definition, trigger, state.FocusedEditorIndex);
                sections.Add(ActionSection("focused_trigger_what", "WHAT / SIGNAL", controls));
                List<ScenarioAuthoringInspectorItem> when = new List<ScenarioAuthoringInspectorItem>();
                when.Add(Property("Schedule", FormatTriggerSchedule(trigger), "Used only by scheduled-time triggers."));
                if (string.Equals(trigger.Type, "Scheduled", StringComparison.OrdinalIgnoreCase))
                    AddScheduleActions(when, ScenarioAuthoringActionIds.ActionTriggerDayPrefix, ScenarioAuthoringActionIds.ActionTriggerHourPrefix, ScenarioAuthoringActionIds.ActionTriggerMinutePrefix, state.FocusedEditorIndex);
                else
                    when.Add(Text("This trigger waits for its signal instead of a clock time."));
                sections.Add(ActionSection("focused_trigger_when", "WHEN", when));
                if (trigger.Properties != null && trigger.Properties.Count > 0)
                    sections.Add(FactSection("focused_trigger_advanced", "ADVANCED / RAW PROPERTIES", BuildRawPropertyItems(trigger.Properties)));
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
                subtitle = ScenarioTimelineCreatorText.ScheduledActionName(definition, action) + " - " + FormatSchedule(action.DueTime);
                string condition = string.IsNullOrEmpty(action.GateId) ? "No condition" : action.GateId;
                string repeat = action.Policy != null && action.Policy.Repeatable ? "Repeat" : "Once";
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Text(FormatSchedule(action.DueTime) + " - " + ScenarioTimelineCreatorText.ScheduledActionName(definition, action)));
                facts.Add(Fact("Name", Safe(action.Id), "Stable scheduled-change id saved in the scenario XML."));
                facts.Add(Property("Kind", FormatScheduledActionTypeLabel(action.ActionType), "Primary effect template.", "ACTION"));
                sections.Add(FactSection("focused_action_header", "SCHEDULED CHANGE", facts));

                List<ScenarioAuthoringInspectorItem> when = new List<ScenarioAuthoringInspectorItem>();
                when.Add(Property("Fires", FormatSchedule(action.DueTime), "Scenario day and time."));
                AddScheduleActions(when, ScenarioAuthoringActionIds.ActionScheduledActionDayPrefix, ScenarioAuthoringActionIds.ActionScheduledActionHourPrefix, state.FocusedEditorIndex);
                sections.Add(ActionSection("focused_action_when", "WHEN", when));

                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionTypePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Cycle Kind", "Cycle the primary effect template for this scheduled change.", true, false, "TY")));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionEffectAddPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Add Effect", "Add another effect to this scheduled change.", true, false, "E+")));
                if (action.Effects == null || action.Effects.Count == 0)
                    controls.Add(Text("No effects yet - this entry does nothing when it fires."));
                for (int e = 0; action.Effects != null && e < action.Effects.Count; e++)
                    AddEffectItems(controls, definition, action.Effects[e], state.FocusedEditorIndex, e);
                sections.Add(ActionSection("focused_action_what", "WHAT / EFFECTS", controls));
                AddScheduledActionCastPickerSections(sections, definition, action, state.FocusedEditorIndex);

                bool hasConditions = !string.IsNullOrEmpty(action.GateId) || (action.ConditionRefs != null && action.ConditionRefs.Count > 0);
                List<ScenarioAuthoringInspectorItem> conditions = new List<ScenarioAuthoringInspectorItem>();
                conditions.Add(Property("Gate", condition, "The entry fires only while this gate passes."));
                conditions.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionGatePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), hasConditions ? "Cycle Condition" : "Add Condition", "Attach the next authored condition, or clear it when the list wraps.", true, !string.IsNullOrEmpty(action.GateId), "CN", condition)));
                if (!hasConditions)
                    conditions.Add(Text("No condition yet. This scheduled change fires on its clock time."));
                for (int c = 0; action.ConditionRefs != null && c < action.ConditionRefs.Count; c++)
                    conditions.Add(Property("Condition " + (c + 1).ToString(CultureInfo.InvariantCulture), ScenarioTimelineCreatorText.ConditionName(action.ConditionRefs[c].Kind), FormatConditionTarget(definition, action.ConditionRefs[c])));
                sections.Add(ActionSection("focused_action_conditions", "CONDITIONS", conditions));

                List<ScenarioAuthoringInspectorItem> advanced = new List<ScenarioAuthoringInspectorItem>();
                advanced.Add(Property("Repeat", repeat, "Whether the scheduled change can fire more than once."));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionRepeatPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Toggle Repeat", "Switch this change between once-only and repeatable execution.", true, action.Policy != null && action.Policy.Repeatable, "RP")));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionCooldownPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".30", "Cooldown +30m", "Increase repeat cooldown by 30 game minutes.", true, false, "C+")));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionCooldownPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".-30", "Cooldown -30m", "Decrease repeat cooldown by 30 game minutes.", true, false, "C-")));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionWindowEndDayPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".1", "Window +1d", "Extend the valid run window by one day.", true, false, "W+")));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionChancePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".5", "Chance +5%", "Increase the chance of a successful run.", true, false, "%+")));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionJitterPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".30", "Jitter +30m", "Increase random timing jitter by 30 minutes.", true, false, "J+")));
                advanced.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionMaxRunsPrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture) + ".1", "Max runs +1", "Increase the maximum successful run count.", true, false, "M+")));
                sections.Add(ActionSection("focused_action_advanced", "ADVANCED / RUN POLICY", advanced));
            }
            else if (string.Equals(state.FocusedEditorKind, "journal_entry", StringComparison.OrdinalIgnoreCase))
            {
                JournalEntryDefinition entry = definition.Journal != null
                    && definition.Journal.Entries != null
                    && state.FocusedEditorIndex >= 0
                    && state.FocusedEditorIndex < definition.Journal.Entries.Count
                        ? definition.Journal.Entries[state.FocusedEditorIndex]
                        : null;
                if (entry == null)
                    return null;

                title = "Journal Entry";
                subtitle = "Write authored text into the in-game journal when the schedule and condition pass.";
                string condition = string.IsNullOrEmpty(entry.GateId) ? "No condition" : entry.GateId;
                string writer = FormatJournalWriter(definition, entry.Writer);
                List<ScenarioAuthoringInspectorItem> facts = new List<ScenarioAuthoringInspectorItem>();
                facts.Add(Fact("Id", Safe(entry.Id), "Stable journal entry id saved in the scenario XML."));
                facts.Add(Fact("When", FormatJournalSchedule(entry), "Scenario day/time, trigger, or condition-only timing."));
                facts.Add(Fact("Condition", condition, "The entry only writes while this is true."));
                facts.Add(Fact("Writer", writer, "Rendered as a journal text prefix because vanilla entries are anonymous."));
                facts.Add(Fact("Repeat", entry.Mode == ScenarioJournalEntryMode.Repeat ? "Repeat" : "Once", "Whether the entry can write more than once."));
                facts.Add(Fact("Preview", JournalPreview(entry.Text), "Text supports {writer} and {day}."));
                sections.Add(FactSection("focused_journal_facts", "Journal Entry", facts));

                string indexText = state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture);
                List<ScenarioAuthoringInspectorItem> controls = new List<ScenarioAuthoringInspectorItem>();
                controls.Add(EditableProperty("Id", Safe(entry.Id), ScenarioAuthoringActionIds.ActionJournalEntryIdPrefix + indexText + ".", "Edit the stable journal entry id."));
                controls.Add(EditableProperty("Text", Safe(entry.Text), ScenarioAuthoringActionIds.ActionJournalEntryTextPrefix + indexText + ".", "Edit journal text. Supports {writer} and {day}."));
                AddScheduleActions(controls, ScenarioAuthoringActionIds.ActionJournalEntryDayPrefix, ScenarioAuthoringActionIds.ActionJournalEntryHourPrefix, ScenarioAuthoringActionIds.ActionJournalEntryMinutePrefix, state.FocusedEditorIndex);
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryGatePrefix + indexText, "Cycle Condition", "Attach the next authored condition, or clear it when the list wraps.", true, !string.IsNullOrEmpty(entry.GateId), "CN", condition)));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryRepeatPrefix + indexText, "Toggle Repeat", "Switch this entry between once-only and repeatable execution.", true, entry.Mode == ScenarioJournalEntryMode.Repeat, "RP")));
                controls.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryWriterAnyPrefix + indexText, "Any Present Member", "Let runtime choose any shelter member who is present.", true, entry.Writer == null, "ANY")));
                sections.Add(ActionSection("focused_journal_controls", "Fields", controls));
                AddJournalCastPickerSection(sections, definition, entry, state.FocusedEditorIndex);
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
                AddGateCastPickerSections(sections, definition, group, state.FocusedEditorIndex);
            }

            List<ScenarioAuthoringInspectorItem> footer = new List<ScenarioAuthoringInspectorItem>();
            footer.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorSave, "Save", "Close this focused editor and keep the entry.", true, true, "SV")));
            footer.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFocusedEditorCancel, "Cancel", state.FocusedEditorIsNew ? "Discard this new entry and close the editor." : "Close this editor.", true, false, "CL")));
            if (string.Equals(state.FocusedEditorKind, "trigger", StringComparison.OrdinalIgnoreCase))
                footer.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerDeletePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Remove Trigger", "Remove this authored trigger.", true, false, "RM")));
            else if (string.Equals(state.FocusedEditorKind, "scheduled_action", StringComparison.OrdinalIgnoreCase))
                footer.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionDeletePrefix + state.FocusedEditorIndex.ToString(CultureInfo.InvariantCulture), "Remove Scheduled Change", "Remove this scheduled change.", true, false, "RM")));
            sections.Add(ActionSection("focused_editor_footer", string.Empty, footer));

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
                        Text((editor.SourceLabel ?? "<sprite>") + (editor.Dirty ? " · Unsaved changes" : " · Saved draft"))
                    }
                }
            };
        }

        internal static ScenarioAuthoringInspectorSection[] BuildTriggerWindowSections(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> triggerItems = new List<ScenarioAuthoringInspectorItem>();
            triggerItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerAddManual, "Add Manual Trigger", "Create a trigger that can be fired by code or another scheduled effect.", true, true, "T+")));
            triggerItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionTriggerAddScheduled, "Timed Trigger", "Create a trigger that fires on a specific scenario day and hour.", true, true, "TS")));
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
            List<ScenarioAuthoringInspectorItem> worldEventItems = BuildWorldEventItems(state, definition);
            List<ScenarioAuthoringInspectorItem> vanillaSuppressionItems = BuildVanillaSuppressionItems(definition);
            List<ScenarioAuthoringInspectorItem> journalItems = BuildJournalEntryItems(state, definition);
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
                    Id = "world_events",
                    Title = "World Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = worldEventItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "vanilla_world_event_suppression",
                    Title = "Vanilla Suppression",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = vanillaSuppressionItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "journal_entries",
                    Title = "Journal Entries",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = journalItems.ToArray()
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
            return FormatEffectTarget(null, effect);
        }

        internal static string FormatEffectTarget(ScenarioDefinition definition, ScenarioEffectDefinition effect)
        {
            if (effect == null)
                return "missing effect";
            if (effect.ActorRef != null)
                return "survivor " + ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, effect.ActorRef, false, true, effect.SurvivorId ?? effect.TargetId);
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
            if (effect.Kind == ScenarioEffectKind.WriteJournalEntry)
                return "journal " + ScenarioPropertyBag.GetString(effect.Properties, "text", "<empty>");
            if (!string.IsNullOrEmpty(effect.ConversationId))
                return "conversation " + effect.ConversationId;
            if (effect.Kind == ScenarioEffectKind.WorldEvent)
                return FormatWorldEventEffect(effect);
            if (!string.IsNullOrEmpty(effect.SurvivorId))
                return "survivor " + effect.SurvivorId;
            if (!string.IsNullOrEmpty(effect.BunkerExpansionId))
                return "bunker " + effect.BunkerExpansionId;
            if (!string.IsNullOrEmpty(effect.TargetId))
                return "target " + effect.TargetId;
            return ScenarioTimelineCreatorText.EffectName(effect.Kind);
        }

        private static void AddInventoryChangeItems(List<ScenarioAuthoringInspectorItem> items, TimedInventoryChangeDefinition change, int index)
        {
            if (items == null || change == null)
                return;

            ScenarioInventoryItemCatalogEntry catalogEntry = ScenarioInventoryItemCatalog.Resolve(change.ItemId);
            items.Add(Property(
                change.Kind.ToString() + " timed change",
                catalogEntry.DisplayName,
                catalogEntry.Detail + " · " + FormatSchedule(change.When),
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
            items.Add(Property("Condition " + (conditionIndex + 1).ToString(CultureInfo.InvariantCulture), ScenarioTimelineCreatorText.ConditionName(condition.Kind) + " / " + FormatConditionTarget(definition, condition), ScenarioTimelineCreatorText.ConditionAdvancedDetail(condition.Kind)));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateConditionKindPrefix + prefix, "Cycle Condition Kind", "Switch this gate condition to the next supported template.", true, false, "CK")));
            AddConditionTargetActions(items, definition, condition, prefix);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionGateConditionDeletePrefix + prefix, "Remove Condition", "Remove this gate condition.", true, false, "RM")));
        }

        private static void AddEffectItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioEffectDefinition effect, int actionIndex, int effectIndex)
        {
            if (items == null || effect == null)
                return;

            string prefix = actionIndex.ToString(CultureInfo.InvariantCulture) + "." + effectIndex.ToString(CultureInfo.InvariantCulture);
            items.Add(Property("Effect " + (effectIndex + 1).ToString(CultureInfo.InvariantCulture), ScenarioTimelineCreatorText.EffectName(effect.Kind) + " / " + FormatEffectTarget(definition, effect), ScenarioTimelineCreatorText.EffectAdvancedDetail(effect.Kind)));
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
            else if (effect.Kind == ScenarioEffectKind.StartConversation)
                AddConversationEffectTargetActions(items, definition, effect, prefix);
            else if (effect.Kind == ScenarioEffectKind.WriteJournalEntry)
                AddJournalEffectTargetActions(items, prefix, effect);
            else if (effect.Kind == ScenarioEffectKind.WorldEvent)
                AddWorldEventEffectTargetActions(items, prefix, effect);
            else
            {
                AddPairTokenActionOrChoose(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, effect.TargetId, "Target", "Use this target id.", "TG", effect.TargetId);
            }
        }

        private static void AddConversationEffectTargetActions(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition, ScenarioEffectDefinition effect, string prefix)
        {
            bool added = false;
            for (int i = 0; definition != null && definition.Conversations != null && definition.Conversations.Conversations != null && i < definition.Conversations.Conversations.Count; i++)
            {
                ScenarioConversationDefinition conversation = definition.Conversations.Conversations[i];
                if (conversation == null || string.IsNullOrEmpty(conversation.Id))
                    continue;
                added = true;
                AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Conversation " + conversation.Id, "Start this authored conversation.", conversation.Id, "CV", string.Equals(effect.ConversationId ?? effect.TargetId, conversation.Id, StringComparison.OrdinalIgnoreCase));
            }
            if (!added)
                items.Add(Text("No authored conversations exist yet. Add one in Story before this effect can resolve."));
        }

        private static void AddJournalEffectTargetActions(List<ScenarioAuthoringInspectorItem> items, string prefix, ScenarioEffectDefinition effect)
        {
            string text = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "text", null);
            items.Add(Property("Journal Text", string.IsNullOrEmpty(text) ? "<empty>" : text));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Default Text", "Seed this journal effect with editable XML-safe placeholder text.", "Authored journal entry for day {day}.", "J");
        }

        private static void AddWorldEventEffectTargetActions(List<ScenarioAuthoringInspectorItem> items, string prefix, ScenarioEffectDefinition effect)
        {
            string eventType = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "eventType", "NpcVisit");
            string npcType = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "npcType", "Trader");
            string outcome = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "outcome", "None");
            items.Add(Property("World Event", FormatWorldEventEffect(effect)));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Visitor", "Queue a scripted visitor through NpcVisitManager.", "eventType:NpcVisit", "WE", string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Raid", "Start a scripted breach through BreachMan.", "eventType:Raid", "WE", string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Broadcast", "Force a radio broadcast outcome.", "eventType:Broadcast", "WE", string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Trader", "Use a trader visitor.", "npcType:Trader", "NP", string.Equals(npcType, "Trader", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Joiner", "Use a recruit visitor.", "npcType:Joiner", "NP", string.Equals(npcType, "Joiner", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Passerby", "Use a passerby visitor.", "npcType:Passerby", "NP", string.Equals(npcType, "Passerby", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Broadcast None", "Force no radio visitor.", "outcome:None", "BC", string.Equals(outcome, "None", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Broadcast Trader", "Force a trader radio outcome.", "outcome:Trader", "BC", string.Equals(outcome, "Trader", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectTargetPrefix, prefix, "Broadcast Recruit", "Force a recruit radio outcome.", "outcome:Recruit", "BC", string.Equals(outcome, "Recruit", StringComparison.OrdinalIgnoreCase));
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectQuantityPrefix, prefix, "Count +", "Increase scripted visitor count.", "1", "+");
            AddPairTokenAction(items, ScenarioAuthoringActionIds.ActionScheduledActionEffectQuantityPrefix, prefix, "Count -", "Decrease scripted visitor count.", "-1", "-");
        }

        private static void AddGateCastPickerSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, ScenarioConditionGroup group, int gateIndex)
        {
            if (sections == null || group == null || group.Conditions == null)
                return;

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                ScenarioConditionRef condition = group.Conditions[i];
                if (condition == null || !IsSurvivorCondition(condition.Kind))
                    continue;

                string prefix = gateIndex.ToString(CultureInfo.InvariantCulture) + "." + i.ToString(CultureInfo.InvariantCulture);
                sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                    "focused_condition_actor_" + prefix.Replace('.', '_'),
                    "Condition " + (i + 1).ToString(CultureInfo.InvariantCulture) + " Cast Member",
                    definition,
                    true,
                    true,
                    condition.ActorRef,
                    ScenarioAuthoringActionIds.ActionGateConditionActorPrefix,
                    prefix,
                    "Add starting or future survivors before selecting a cast member."));
            }
        }

        private static void AddScheduledActionCastPickerSections(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, ScenarioScheduledActionDefinition action, int actionIndex)
        {
            if (sections == null || action == null || action.Effects == null)
                return;

            for (int i = 0; i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null || effect.Kind != ScenarioEffectKind.SpawnFutureSurvivor)
                    continue;

                string prefix = actionIndex.ToString(CultureInfo.InvariantCulture) + "." + i.ToString(CultureInfo.InvariantCulture);
                sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                    "focused_effect_actor_" + prefix.Replace('.', '_'),
                    "Effect " + (i + 1).ToString(CultureInfo.InvariantCulture) + " Future Survivor",
                    definition,
                    false,
                    true,
                    effect.ActorRef,
                    ScenarioAuthoringActionIds.ActionScheduledActionEffectActorPrefix,
                    prefix,
                    "Add a future survivor before selecting a cast member."));
            }
        }

        private static void AddJournalCastPickerSection(List<ScenarioAuthoringInspectorSection> sections, ScenarioDefinition definition, JournalEntryDefinition entry, int entryIndex)
        {
            if (sections == null || entry == null)
                return;

            sections.Add(ScenarioCastMemberPickerBuilder.BuildSection(
                "focused_journal_writer",
                "Journal Writer",
                definition,
                true,
                true,
                entry.Writer,
                ScenarioAuthoringActionIds.ActionJournalEntryWriterPrefix,
                entryIndex.ToString(CultureInfo.InvariantCulture),
                "Add starting or future survivors before selecting a writer."));
        }

        private static bool IsSurvivorCondition(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.SurvivorPresent
                || kind == ScenarioConditionKind.SurvivorStatCheck
                || kind == ScenarioConditionKind.SurvivorTraitCheck;
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
            string creatorName = ScenarioTimelineCreatorText.EffectNameFromRaw(type);
            if (!string.Equals(creatorName, type, StringComparison.Ordinal))
                return creatorName;
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

        private static string FormatTriggerSummary(TriggerDef trigger)
        {
            if (trigger == null)
                return "Missing trigger.";
            if (string.Equals(trigger.Type, "Scheduled", StringComparison.OrdinalIgnoreCase))
                return "At " + FormatTriggerSchedule(trigger) + ", fire trigger '" + Safe(trigger.Id) + "'.";
            return "When " + FormatTriggerTypeLabel(trigger.Type).ToLowerInvariant() + " matches, fire trigger '" + Safe(trigger.Id) + "'.";
        }

        private static List<ScenarioAuthoringInspectorItem> BuildRawPropertyItems(List<ScenarioProperty> properties)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; properties != null && i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null)
                    items.Add(Property(Safe(property.Key), Safe(property.Value), "Stored trigger property."));
            }
            return items;
        }

        private static string FormatConditionTarget(ScenarioConditionRef condition)
        {
            return FormatConditionTarget(null, condition);
        }

        private static string FormatConditionTarget(ScenarioDefinition definition, ScenarioConditionRef condition)
        {
            if (condition == null)
                return "missing condition";
            if (condition.Kind == ScenarioConditionKind.TimeReached)
                return FormatSchedule(condition.Time);
            if (condition.ActorRef != null)
                return "survivor " + ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, condition.ActorRef, true, true, condition.TargetId);
            if (!string.IsNullOrEmpty(condition.FlagId))
                return "flag " + condition.FlagId + "=" + condition.FlagValue;
            if (!string.IsNullOrEmpty(condition.TargetId))
                return "target " + condition.TargetId;
            if (!string.IsNullOrEmpty(condition.StatId))
                return "stat " + condition.StatId + ">=" + condition.StatValue.ToString(CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(condition.TraitId))
                return "trait " + condition.TraitId;
            return ScenarioTimelineCreatorText.ConditionName(condition.Kind);
        }

        private static string FormatWorldEventEffect(ScenarioEffectDefinition effect)
        {
            string eventType = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "eventType", "WorldEvent");
            string npcType = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "npcType", null);
            string outcome = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "outcome", null);
            int count = ScenarioPropertyBag.GetInt(effect != null ? effect.Properties : null, "count", effect != null ? Math.Max(1, effect.Quantity) : 1);
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
                return "visitor " + Safe(npcType) + " x" + count.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
                return "broadcast " + Safe(outcome);
            return Safe(eventType);
        }

        private static ScenarioScheduledActionDefinition GetScheduledAction(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.ScheduledActions != null
                && index >= 0
                && index < definition.ScheduledActions.Count
                    ? definition.ScheduledActions[index]
                    : null;
        }

        private static ScenarioEffectDefinition FindWorldEventEffect(ScenarioScheduledActionDefinition action)
        {
            int index = FindWorldEventEffectIndex(action);
            return action != null && action.Effects != null && index >= 0 && index < action.Effects.Count
                ? action.Effects[index]
                : null;
        }

        private static int FindWorldEventEffectIndex(ScenarioScheduledActionDefinition action)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
                if (action.Effects[i] != null && action.Effects[i].Kind == ScenarioEffectKind.WorldEvent)
                    return i;
            return -1;
        }

        private static bool HasWorldEventEffect(ScenarioScheduledActionDefinition action)
        {
            return FindWorldEventEffect(action) != null;
        }

        private static List<WorldEventItemSpec> ParseWorldEventItemSpec(string spec)
        {
            List<WorldEventItemSpec> entries = new List<WorldEventItemSpec>();
            if (string.IsNullOrEmpty(spec))
                return entries;

            string[] parts = spec.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] pair = parts[i].Split(':');
                string itemId = pair.Length > 0 ? pair[0].Trim() : string.Empty;
                int quantity = 1;
                if (pair.Length > 1)
                    int.TryParse(pair[1], out quantity);
                if (!string.IsNullOrEmpty(itemId))
                    entries.Add(new WorldEventItemSpec { ItemId = itemId, Quantity = Math.Max(1, quantity) });
            }
            return entries;
        }

        private static string ResolveWorldEventItemSpecProperty(string listKey)
        {
            if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
                return "tradeItems";
            if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
                return "weapons";
            if (string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
                return "armor";
            return null;
        }

        private static string ResolveWorldEventItemSpecActionPrefix(string listKey)
        {
            if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventTradeItemPrefix;
            if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventWeaponItemPrefix;
            if (string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventArmorItemPrefix;
            return null;
        }

        private static string ResolveWorldEventItemSpecAddPrefix(string listKey)
        {
            if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventTradeAddPrefix;
            if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventWeaponAddPrefix;
            if (string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventArmorAddPrefix;
            return null;
        }

        private static string ResolveWorldEventItemSpecDeletePrefix(string listKey)
        {
            if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventTradeDeletePrefix;
            if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventWeaponDeletePrefix;
            if (string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventArmorDeletePrefix;
            return null;
        }

        private static string ResolveWorldEventItemSpecQuantityPrefix(string listKey)
        {
            if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventTradeQuantityPrefix;
            if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventWeaponQuantityPrefix;
            if (string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
                return ScenarioAuthoringActionIds.ActionWorldEventArmorQuantityPrefix;
            return null;
        }

        private static string FormatWorldEventPickerLabel(string listKey)
        {
            if (string.Equals(listKey, "trade", StringComparison.OrdinalIgnoreCase))
                return "Trader stock";
            if (string.Equals(listKey, "weapon", StringComparison.OrdinalIgnoreCase))
                return "Raid weapons";
            if (string.Equals(listKey, "armor", StringComparison.OrdinalIgnoreCase))
                return "Raid gear";
            return "World event item";
        }

        private static string FormatWorldEventTypeLabel(string eventType)
        {
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
                return "NPC Visit";
            if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
                return "Raid";
            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
                return "Broadcast / Radio";
            return Safe(eventType);
        }

        private static string FormatBroadcastOutcome(string outcome)
        {
            if (string.Equals(outcome, "Recruit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outcome, "Joiner", StringComparison.OrdinalIgnoreCase))
                return "Recruit";
            if (string.Equals(outcome, "Trader", StringComparison.OrdinalIgnoreCase))
                return "Trader";
            return "None";
        }

        private static string FormatWorldEventScheduleSummary(ScenarioScheduledActionDefinition action)
        {
            if (action == null)
                return "unscheduled";

            ScenarioSchedulePolicy policy = action.Policy ?? new ScenarioSchedulePolicy();
            int dueDay = action.DueTime != null ? Math.Max(1, action.DueTime.Day) : 1;
            int endDay = policy.WindowEndDay > 0 ? Math.Max(dueDay, policy.WindowEndDay) : dueDay;
            int chance = ScenarioAuthoringSchedule.Clamp((int)Math.Round(policy.Chance * 100f), 0, 100);
            string cadence = policy.Repeatable
                ? "about every " + FormatCooldownDays(policy.CooldownMinutes) + " between day " + dueDay.ToString(CultureInfo.InvariantCulture) + " and " + endDay.ToString(CultureInfo.InvariantCulture)
                : "once on day " + dueDay.ToString(CultureInfo.InvariantCulture) + (endDay > dueDay ? " through day " + endDay.ToString(CultureInfo.InvariantCulture) : string.Empty);
            return cadence + " at " + FormatClock(action.DueTime) + ", " + chance.ToString(CultureInfo.InvariantCulture) + "% chance"
                + (policy.JitterMinutes > 0 ? ", +/-" + policy.JitterMinutes.ToString(CultureInfo.InvariantCulture) + "m jitter" : string.Empty)
                + (policy.MaxRuns > 0 ? ", max " + policy.MaxRuns.ToString(CultureInfo.InvariantCulture) + " run(s)" : string.Empty);
        }

        private static string FormatCooldownDays(int cooldownMinutes)
        {
            if (cooldownMinutes <= 0)
                return "eligible check";
            if (cooldownMinutes % 1440 == 0)
                return (cooldownMinutes / 1440).ToString(CultureInfo.InvariantCulture) + " day(s)";
            if (cooldownMinutes % 60 == 0)
                return (cooldownMinutes / 60).ToString(CultureInfo.InvariantCulture) + " hour(s)";
            return cooldownMinutes.ToString(CultureInfo.InvariantCulture) + " minute(s)";
        }

        private static string FormatClock(ScenarioScheduleTime time)
        {
            if (time == null)
                return "--:--";
            return time.Hour.ToString("D2", CultureInfo.InvariantCulture) + ":" + time.Minute.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string FormatWorldEventValidationState(ScenarioScheduledActionDefinition action, ScenarioEffectDefinition effect)
        {
            string fix = FormatWorldEventValidationFix(effect);
            if (!string.IsNullOrEmpty(fix))
                return "Fix needed";
            if (action == null || action.DueTime == null || action.DueTime.Day < 1 || action.DueTime.Hour < 0 || action.DueTime.Hour > 23 || action.DueTime.Minute < 0 || action.DueTime.Minute > 59)
                return "Fix needed";
            return "OK";
        }

        private static string FormatWorldEventValidationFix(ScenarioEffectDefinition effect)
        {
            if (effect == null)
                return "Reason: missing WorldEvent effect. Fix: add a world event effect.";
            string eventType = ScenarioPropertyBag.GetString(effect.Properties, "eventType", null);
            if (string.IsNullOrEmpty(eventType))
                return "Reason: missing event type. Fix: choose NPC Visit, Raid, or Broadcast.";
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
            {
                string npcType = ScenarioPropertyBag.GetString(effect.Properties, "npcType", "Passerby");
                if (!string.Equals(npcType, "Trader", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(npcType, "Joiner", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(npcType, "Recruit", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(npcType, "Passerby", StringComparison.OrdinalIgnoreCase))
                    return "Reason: unsupported NPC type. Fix: choose Trader, Joiner, or Passerby.";
                string tradeIssue = FindInvalidWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, "tradeItems", null));
                if (!string.IsNullOrEmpty(tradeIssue))
                    return "Reason: invalid trader stock " + tradeIssue + ". Fix: open the item picker and choose a valid item id.";
            }
            else if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
            {
                int minNpcs = ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", 1);
                int maxNpcs = ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", minNpcs);
                if (minNpcs < 1 || maxNpcs < minNpcs)
                    return "Reason: invalid raid min/max NPCs. Fix: adjust min and max counts.";
                string weaponIssue = FindInvalidWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, "weapons", null));
                if (!string.IsNullOrEmpty(weaponIssue))
                    return "Reason: invalid raid weapon " + weaponIssue + ". Fix: open the item picker and choose a valid item id.";
                string armorIssue = FindInvalidWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, "armor", null));
                if (!string.IsNullOrEmpty(armorIssue))
                    return "Reason: invalid raid gear " + armorIssue + ". Fix: open the item picker and choose a valid item id.";
            }
            else if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
            {
                string outcome = ScenarioPropertyBag.GetString(effect.Properties, "outcome", "None");
                if (!string.Equals(outcome, "None", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Trader", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Recruit", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(outcome, "Joiner", StringComparison.OrdinalIgnoreCase))
                    return "Reason: unsupported broadcast outcome. Fix: choose Trader, Recruit, or None.";
            }
            else
            {
                return "Reason: unsupported world event type. Fix: choose NPC Visit, Raid, or Broadcast.";
            }

            return null;
        }

        private static string FindInvalidWorldEventItemSpec(string spec)
        {
            List<WorldEventItemSpec> entries = ParseWorldEventItemSpec(spec);
            for (int i = 0; i < entries.Count; i++)
            {
                ScenarioInventoryItemCatalogEntry entry = ScenarioInventoryItemCatalog.Resolve(entries[i].ItemId);
                if (entry == null || entry.ItemType == ItemManager.ItemType.Undefined)
                    return "'" + Safe(entries[i].ItemId) + "'";
                if (entries[i].Quantity < 1)
                    return "'" + Safe(entries[i].ItemId) + "' quantity";
            }
            return null;
        }

        private sealed class WorldEventItemSpec
        {
            public string ItemId;
            public int Quantity;
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

        private static List<ScenarioAuthoringInspectorItem> BuildWorldEventItems(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWorldEventAdd, "Add World Event", "Create a scripted visitor, raid, or radio event with typed fields.", true, true, "WEV")));
            int count = 0;
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                ScenarioEffectDefinition effect = FindWorldEventEffect(action);
                if (action == null || effect == null)
                    continue;

                count++;
                string eventType = ScenarioPropertyBag.GetString(effect.Properties, "eventType", "WorldEvent");
                string validation = FormatWorldEventValidationState(action, effect);
                string fix = FormatWorldEventValidationFix(effect);
                string detail = FormatWorldEventScheduleSummary(action) + " / " + FormatWorldEventEffect(effect);
                items.Add(TimelineFact(state, ScenarioAuthoringLocalActionIds.FocusedKindWorldEvent, i, ResolveWorldEventGlyph(eventType) + " " + Safe(action.Id), detail, string.IsNullOrEmpty(fix) ? "World event is valid." : fix));
                items.Add(Property("Type", FormatWorldEventTypeLabel(eventType), FormatWorldEventKeyParams(effect), validation, ResolveWorldEventGlyph(eventType), null, !string.IsNullOrEmpty(fix)));
                items.Add(ActionItem(Action(ScenarioAuthoringLocalActionIds.ActionWorldEventEditorOpenPrefix + i.ToString(CultureInfo.InvariantCulture), "Open Editor", "Edit this world event using typed fields.", true, true, "ED", validation)));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionScheduledActionDeletePrefix + i.ToString(CultureInfo.InvariantCulture), "Remove World Event", "Remove this scheduled world event.", true, false, "RM")));
            }

            if (count == 0)
                items.Add(Text("No world events have been authored yet. Add one for scripted visitors, raids, or radio outcomes."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildVanillaSuppressionItems(ScenarioDefinition definition)
        {
            ScenarioVanillaSuppressionDefinition suppression = definition != null ? definition.VanillaSuppression : null;
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Text("Use these only when authored World Events should replace vanilla random systems."));
            AddVanillaSuppressionAction(items, "randomVisitors", "Random Visitors", suppression != null && suppression.RandomVisitors, "Stops vanilla random visitors so only authored visitor events arrive.");
            AddVanillaSuppressionAction(items, "binman", "Binman", suppression != null && suppression.Binman, "Stops the vanilla binman visit loop.");
            AddVanillaSuppressionAction(items, "raids", "Raids", suppression != null && suppression.Raids, "Stops vanilla raid checks so authored Raid events control breaches.");
            AddVanillaSuppressionAction(items, "stasisVisitors", "Stasis Visitors", suppression != null && suppression.StasisVisitors, "Stops stasis-triggered visitor spawns.");
            AddVanillaSuppressionAction(items, "radioBroadcastOdds", "Radio Odds", suppression != null && suppression.RadioBroadcastOdds, "Stops vanilla trader/recruit radio odds so authored Broadcast events decide outcomes.");
            return items;
        }

        private static void AddVanillaSuppressionAction(List<ScenarioAuthoringInspectorItem> items, string key, string label, bool suppressed, string consequence)
        {
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionWorldEventSuppressionPrefix + key,
                label,
                consequence,
                true,
                suppressed,
                suppressed ? "OFF" : "ON",
                consequence,
                suppressed ? "Suppressed" : "Allowed")));
        }

        private static string ResolveWorldEventGlyph(string eventType)
        {
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
                return "NPC";
            if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
                return "RAID";
            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
                return "RAD";
            return "WEV";
        }

        private static string FormatWorldEventKeyParams(ScenarioEffectDefinition effect)
        {
            if (effect == null)
                return "missing effect";
            string eventType = ScenarioPropertyBag.GetString(effect.Properties, "eventType", null);
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
            {
                string npcType = ScenarioPropertyBag.GetString(effect.Properties, "npcType", "Passerby");
                int stock = ParseWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, "tradeItems", null)).Count;
                int count = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "count", effect.Quantity > 0 ? effect.Quantity : 1));
                return npcType + " x" + count.ToString(CultureInfo.InvariantCulture) + (string.Equals(npcType, "Trader", StringComparison.OrdinalIgnoreCase) ? " / " + stock.ToString(CultureInfo.InvariantCulture) + " stock row(s)" : string.Empty);
            }
            if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
            {
                int min = Math.Max(1, ScenarioPropertyBag.GetInt(effect.Properties, "minNpcs", 1));
                int max = Math.Max(min, ScenarioPropertyBag.GetInt(effect.Properties, "maxNpcs", min));
                int weapons = ParseWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, "weapons", null)).Count;
                int armor = ParseWorldEventItemSpec(ScenarioPropertyBag.GetString(effect.Properties, "armor", null)).Count;
                return min.ToString(CultureInfo.InvariantCulture) + "-" + max.ToString(CultureInfo.InvariantCulture) + " raiders / " + weapons.ToString(CultureInfo.InvariantCulture) + " weapons / " + armor.ToString(CultureInfo.InvariantCulture) + " gear";
            }
            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
                return "forced " + FormatBroadcastOutcome(ScenarioPropertyBag.GetString(effect.Properties, "outcome", "None"));
            return Safe(eventType);
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
                if (HasWorldEventEffect(action))
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

        private static List<ScenarioAuthoringInspectorItem> BuildJournalEntryItems(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryAdd, "Add Journal Entry", "Create authored text that writes to the in-game journal when its timing and condition pass.", true, true, "J+")));
            for (int i = 0; definition != null && definition.Journal != null && definition.Journal.Entries != null && i < definition.Journal.Entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.Journal.Entries[i];
                if (entry == null)
                    continue;
                string gate = string.IsNullOrEmpty(entry.GateId) ? "no condition" : "condition " + entry.GateId;
                string mode = entry.Mode == ScenarioJournalEntryMode.Repeat ? "repeat" : "once";
                string writer = FormatJournalWriter(definition, entry.Writer);
                items.Add(TimelineFact(state, "journal_entry", i, Safe(entry.Id), FormatJournalSchedule(entry) + " / " + gate + " / " + writer + " / " + mode, JournalPreview(entry.Text)));
                AddScheduleActions(items, ScenarioAuthoringActionIds.ActionJournalEntryDayPrefix, ScenarioAuthoringActionIds.ActionJournalEntryHourPrefix, ScenarioAuthoringActionIds.ActionJournalEntryMinutePrefix, i);
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryGatePrefix + i.ToString(CultureInfo.InvariantCulture), "Cycle Condition", "Attach the next authored condition, or clear it when the list wraps.", true, !string.IsNullOrEmpty(entry.GateId), "CN", gate)));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryRepeatPrefix + i.ToString(CultureInfo.InvariantCulture), "Toggle Repeat", "Switch this journal entry between once-only and repeatable execution.", true, entry.Mode == ScenarioJournalEntryMode.Repeat, "RP")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryWriterAnyPrefix + i.ToString(CultureInfo.InvariantCulture), "Any Present Member", "Let runtime choose any shelter member who is present.", true, entry.Writer == null, "ANY")));
                items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalEntryDeletePrefix + i.ToString(CultureInfo.InvariantCulture), "Remove Journal Entry", "Remove this authored journal entry.", true, false, "RM")));
            }
            if (items.Count == 1)
                items.Add(Text("No authored journal entries have been added yet."));
            AddJournalPolicyItems(items, definition);
            return items;
        }

        private static void AddJournalPolicyItems(List<ScenarioAuthoringInspectorItem> items, ScenarioDefinition definition)
        {
            JournalVanillaPolicyDefinition policy = definition != null && definition.Journal != null ? definition.Journal.VanillaPolicy : null;
            bool suppressFirst = policy != null && policy.SuppressFirstEntry;
            items.Add(Property("Vanilla Policy", suppressFirst ? "suppress first entry" : "allow first entry"));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionJournalVanillaSuppressFirst, "Suppress First Entry", "Block the vanilla opening journal entry for this scenario.", true, suppressFirst, "1X")));

            Array categories = Enum.GetValues(typeof(ScenarioJournalVanillaCategory));
            for (int i = 0; i < categories.Length; i++)
            {
                ScenarioJournalVanillaCategory category = (ScenarioJournalVanillaCategory)categories.GetValue(i);
                bool suppressed = ContainsJournalCategory(policy, category);
                items.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionJournalVanillaCategoryPrefix + category.ToString(),
                    category.ToString(),
                    "Toggle vanilla journal suppression for this category.",
                    true,
                    suppressed,
                    "VX",
                    suppressed ? "Suppressed" : "Allowed")));
            }
        }

        private static string FormatJournalSchedule(JournalEntryDefinition entry)
        {
            if (entry == null)
                return "unscheduled";
            if (entry.DueTime != null)
                return FormatSchedule(entry.DueTime);
            if (!string.IsNullOrEmpty(entry.TriggerId))
                return "trigger " + entry.TriggerId;
            return "condition only";
        }

        private static string FormatJournalWriter(ScenarioDefinition definition, ScenarioActorRef actorRef)
        {
            if (actorRef == null)
                return "any present member";
            return ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, actorRef, true, true, actorRef.DisplayNameFallback);
        }

        private static string JournalPreview(string text)
        {
            string value = Safe(text);
            if (value.Length <= 72)
                return value;
            return value;
        }

        private static bool ContainsJournalCategory(JournalVanillaPolicyDefinition policy, ScenarioJournalVanillaCategory category)
        {
            for (int i = 0; policy != null && policy.SuppressedCategories != null && i < policy.SuppressedCategories.Count; i++)
                if (policy.SuppressedCategories[i] == category)
                    return true;
            return false;
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
                int rankCompare = SettingsSectionRank(left != null ? left.Title : null).CompareTo(SettingsSectionRank(right != null ? right.Title : null));
                if (rankCompare != 0)
                    return rankCompare;
                return string.Compare(left != null ? left.Title : null, right != null ? right.Title : null, StringComparison.OrdinalIgnoreCase);
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

        private static int SettingsSectionRank(string title)
        {
            if (string.IsNullOrEmpty(title))
                return 50;
            if (string.Equals(title, "Shell", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (string.Equals(title, "Layout", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (string.Equals(title, "Visuals", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (string.Equals(title, "Input", StringComparison.OrdinalIgnoreCase))
                return 3;
            if (string.Equals(title, "Sprite Tools", StringComparison.OrdinalIgnoreCase))
                return 4;
            if (string.Equals(title, "Advanced", StringComparison.OrdinalIgnoreCase))
                return 99;
            return 50;
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

            return value;
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

        private static ScenarioAuthoringInspectorItem EditableProperty(string label, string value, string actionPrefix, string hoverHint)
        {
            ScenarioAuthoringInspectorItem item = Property(label, value);
            item.Editable = true;
            item.HoverHint = hoverHint;
            item.Action = Action(actionPrefix, label, hoverHint, true, false, "ED");
            return item;
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
