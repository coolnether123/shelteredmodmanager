using System;
using ModAPI.Core;
using ModAPI.InputActions;
using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringBackendService : IScenarioAuthoringBackend
    {
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringSelectionService _selectionService;
        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly ScenarioAuthoringPresentationBuilder _presentationBuilder;
        private readonly ScenarioAuthoringContextMenuService _contextMenuService;
        private readonly ScenarioAuthoringCommandService _commandService;
        private readonly ScenarioAuthoringHistoryService _historyService;
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly ScenarioStageCoordinator _stageCoordinator;
        private readonly ScenarioSelectionScopeService _selectionScopeService;
        private readonly ScenarioAuthoringTutorialService _tutorialService;
        private readonly ScenarioAuthoringSetupStateService _setupStateService;
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
            IScenarioEditorSessionStore sessionStore,
            ScenarioAuthoringPresentationBuilder presentationBuilder,
            ScenarioAuthoringContextMenuService contextMenuService,
            ScenarioAuthoringCommandService commandService,
            ScenarioAuthoringHistoryService historyService,
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            ScenarioStageCoordinator stageCoordinator,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioAuthoringTutorialService tutorialService,
            ScenarioAuthoringSetupStateService setupStateService)
        {
            _selectionService = selectionService;
            _sessionStore = sessionStore;
            _presentationBuilder = presentationBuilder;
            _contextMenuService = contextMenuService;
            _commandService = commandService;
            _historyService = historyService;
            _sectionHub = sectionHub;
            _settingsService = settingsService;
            _layoutService = layoutService;
            _stageCoordinator = stageCoordinator;
            _selectionScopeService = selectionScopeService;
            _tutorialService = tutorialService;
            _setupStateService = setupStateService;
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
                    ActiveStage = ScenarioStageKind.BunkerInside,
                    ActiveBunkerStage = ScenarioStageKind.BunkerInside,
                    ActiveTool = ScenarioAuthoringTool.Objects,
                    ActiveShellTab = ScenarioAuthoringShellTab.Build,
                    AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting,
                    ActiveLayoutPreset = "default",
                    InspectorTab = ScenarioAuthoringInspectorTab.Properties,
                    ActiveDraftId = session.DraftId,
                    ActiveScenarioFilePath = session.ScenarioFilePath,
                    StatusMessage = "Scenario authoring shell is active. Use playtest to make live shelter changes, then capture them back into the draft.",
                    Settings = _settingsService.Load(),
                    SetupState = _setupStateService != null ? _setupStateService.LoadForScenarioFile(session.ScenarioFilePath) : new ScenarioAuthoringSetupState()
                };
                _layoutService.InitializeState(_state);
            }

            ResetInteractiveSubsystems();
            RefreshAuthoringArtifacts();
            _historyService.BindSession(session.DraftId);
            ScenarioSpriteSwapClipboard.Clear();
            ScenarioHoverVisualService.Instance.ClearSecondary();
            _layoutService.ApplyStageWorkspace(_state);
            _stageCoordinator.Synchronize(BuildContext(CurrentState, session));
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
                    StatusMessage = reason ?? string.Empty,
                    Settings = _settingsService.Load(),
                    SetupState = new ScenarioAuthoringSetupState()
                };
            }

            ScenarioHoverVisualService.Instance.Clear();
            ResetInteractiveSubsystems();
            RefreshAuthoringArtifacts();
            _historyService.Reset();
            ScenarioSpriteSwapClipboard.Clear();
            try
            {
                ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
                if (inputCapture != null)
                    inputCapture.Clear();
            }
            catch
            {
            }
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
            ScenarioAuthoringContext context = BuildContext(snapshot, GetActiveSession());
            _contextMenuService.SyncTarget(snapshot.SelectedTarget);

            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.ToggleShell))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionShellToggle);
            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.SaveDraft))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSave);
            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.TogglePlaytest))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionPlaytest);
            if (_tutorialService != null && _tutorialService.CurrentTour() != null && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionTourExit);

            if (ScenarioAuthoringInputActions.IsUndoDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionHistoryUndo);
            if (ScenarioAuthoringInputActions.IsRedoDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionHistoryRedo);
            if (ScenarioAuthoringInputActions.IsCopyDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapCopy);
            if (ScenarioAuthoringInputActions.IsPasteDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapPaste);
            if (ScenarioAuthoringInputActions.IsRevertDown())
                changed |= _commandService.Execute(snapshot, ScenarioAuthoringActionIds.ActionSpriteSwapRevert);

            string sectionMessage;
            if (_sectionHub.Update(context, out sectionMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(sectionMessage))
                    snapshot.StatusMessage = sectionMessage;
            }

            if (_sectionHub.ShouldSuppressSelection)
            {
                changed |= ClearTransientSelection(snapshot);
                ScenarioHoverVisualService.Instance.UpdateFromState(snapshot);
            }
            else
            {
                changed |= _selectionService.Update(snapshot);
            }

            _stageCoordinator.Synchronize(context);
            changed |= _selectionScopeService.ClearSelectionIfOutOfScope(snapshot);
            string tutorialMessage;
            if (_tutorialService != null && _tutorialService.Synchronize(snapshot, _sessionStore.Current, out tutorialMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(tutorialMessage))
                    snapshot.StatusMessage = tutorialMessage;
            }
            ScenarioAuthoringUiDebugService.Instance.DumpSceneEntities(snapshot);

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
            ScenarioAuthoringActionExecutionResult result = ExecuteActionWithResult(actionId);
            return result != null && result.Result;
        }

        public ScenarioAuthoringActionExecutionResult ExecuteActionWithResult(string actionId)
        {
            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (inputCapture != null)
                inputCapture.SuppressWorldInputForAction();

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionCloseEditor, StringComparison.Ordinal))
            {
                ScenarioAuthoringBootstrapService.Instance.RequestCloseActiveSession("Closed from authoring shell.", true);
                return ScenarioAuthoringActionExecutionResult.Success(actionId, true, "Closed from authoring shell.");
            }

            ScenarioAuthoringState snapshot;
            lock (_sync)
            {
                snapshot = _state.Copy();
            }

            if (snapshot == null || !snapshot.IsActive)
                return ScenarioAuthoringActionExecutionResult.Unavailable(actionId, "Scenario authoring is not active.");

            ScenarioAuthoringContext context = BuildContext(snapshot, GetActiveSession());
            string beforeStatus = snapshot.StatusMessage;
            ScenarioAuthoringActionExecutionResult result = _commandService.ExecuteWithResult(snapshot, actionId);
            bool changed = result != null && result.Result;
            string sectionMessage;
            if (_sectionHub.SynchronizeAfterAction(snapshot, out sectionMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(sectionMessage))
                    snapshot.StatusMessage = sectionMessage;
            }
            _stageCoordinator.Synchronize(context);
            changed |= _selectionScopeService.ClearSelectionIfOutOfScope(snapshot);
            string tutorialMessage;
            if (_tutorialService != null && _tutorialService.Synchronize(snapshot, _sessionStore.Current, out tutorialMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(tutorialMessage))
                    snapshot.StatusMessage = tutorialMessage;
            }
            lock (_sync)
            {
                _state = snapshot;
            }

            if (result == null)
                result = ScenarioAuthoringActionExecutionResult.Failure(actionId, null, snapshot.StatusMessage);
            else
                result.StatusMessage = snapshot.StatusMessage ?? string.Empty;

            if (!result.Result && string.IsNullOrEmpty(result.Reason))
                result.Reason = !string.Equals(beforeStatus, snapshot.StatusMessage) && !string.IsNullOrEmpty(snapshot.StatusMessage)
                    ? snapshot.StatusMessage
                    : "Action did not complete.";

            if (changed || !string.Equals(beforeStatus, snapshot.StatusMessage))
                RaiseStateChanged();
            return result;
        }

        internal bool UpdateWindowFrame(string windowId, float x, float y, float width, float height, bool persist)
        {
            bool changed;
            lock (_sync)
            {
                changed = _state != null
                    && _state.IsActive
                    && _layoutService.SetWindowFrame(_state, windowId, x, y, width, height, persist);
            }

            if (changed)
                RaiseStateChanged();
            return changed;
        }

        internal bool BringWindowToFront(string windowId)
        {
            bool changed;
            lock (_sync)
            {
                changed = _state != null
                    && _state.IsActive
                    && _layoutService.BringWindowToFront(_state, windowId);
            }

            if (changed)
                RaiseStateChanged();
            return changed;
        }

        internal void OpenContextMenu(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            OpenContextMenu(state, target, false);
        }

        internal void OpenContextMenu(ScenarioAuthoringState state, ScenarioAuthoringTarget target, bool centerOnScreen)
        {
            _presentationBuilder.OpenContextMenu(state, target, _contextMenuService, centerOnScreen);
        }

        public ScenarioAuthoringShellViewModel GetShellViewModel()
        {
            ScenarioAuthoringContext context = BuildContext(CurrentState, GetActiveSession());
            return _presentationBuilder.BuildShellViewModel(
                context,
                _contextMenuService.Current);
        }

        public ScenarioAuthoringInspectorDocument GetShellDocument()
        {
            return _presentationBuilder.BuildShellDocument(BuildContext(CurrentState, GetActiveSession()));
        }

        public ScenarioAuthoringInspectorDocument GetInspectorDocument()
        {
            return _presentationBuilder.BuildInspectorDocument(BuildContext(CurrentState, GetActiveSession()));
        }

        public ScenarioAuthoringInspectorDocument GetHoverDocument()
        {
            return _presentationBuilder.BuildHoverDocument(CurrentState);
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

        private ScenarioAuthoringSession GetActiveSession()
        {
            lock (_sync)
            {
                return _activeSession;
            }
        }

        private static bool ClearTransientSelection(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            bool changed = false;
            if (state.SelectionModeActive)
            {
                state.SelectionModeActive = false;
                changed = true;
            }

            if (state.HoveredTarget != null)
            {
                state.HoveredTarget = null;
                changed = true;
            }

            if (state.SelectionStack != null && state.SelectionStack.Count > 0)
            {
                state.SelectionStack.Clear();
                state.SelectionStackSignature = null;
                state.ActiveSelectionStackIndex = 0;
                changed = true;
            }

            return changed;
        }

        private ScenarioAuthoringContext BuildContext(ScenarioAuthoringState state, ScenarioAuthoringSession authoringSession)
        {
            return new ScenarioAuthoringContext
            {
                State = state,
                EditorSession = _sessionStore.Current,
                AuthoringSession = authoringSession
            };
        }

        private void ResetInteractiveSubsystems()
        {
            ScenarioAuthoringSelectionMenuService.Instance.Reset();
            _contextMenuService.Close();
            _sectionHub.ResetInteractiveSubsystems();
        }

        private void RefreshAuthoringArtifacts()
        {
            _sectionHub.RefreshAuthoringArtifacts();
        }
    }
}
