using System;
using ModAPI.Core;
using ModAPI.InputActions;
using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Application.Authoring.Tutorial;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Application.Stages;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Infrastructure.Assets;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioAuthoringBackendService : IScenarioAuthoringBackend
    {
        private enum CachedDocumentKind
        {
            Shell,
            Inspector,
            Hover
        }

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
        private readonly ScenarioAuthoringShortcutRouter _shortcutRouter;
        private readonly ScenarioDraftSnapshotService _snapshotService;
        private readonly ScenarioAuthoringSessionLifecycleService _sessionLifecycle;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;
        private readonly ScenarioAuthoringInputCaptureService _inputCapture;
        private readonly ScenarioMapAuthoringRuntimeService _mapAuthoring;
        private readonly ScenarioStorageAuthoringRuntimeService _storageAuthoring;
        private readonly ScenarioVanillaInteractionRuntimeService _vanillaInteraction;
        private readonly ScenarioAuthoringSelectionMenuService _selectionMenu;
        private readonly ScenarioAuthoringUiDebugService _uiDebug;
        private readonly ScenarioAuthoringStatusPort _statusPort;
        private readonly ScenarioHoverVisualService _hoverVisuals;
        private ScenarioAuthoringState _state = new ScenarioAuthoringState();
        private int _presentationRevision;
        private int _cachedShellPresentationRevision = -1;
        private int _cachedShellDraftRevision = -1;
        private int _cachedShellContextMenuRevision = -1;
        private ScenarioEditorSession _cachedShellEditorSession;
        private ScenarioAuthoringSession _cachedShellAuthoringSession;
        private ScenarioAuthoringShellViewModel _cachedShellViewModel;
        private ScenarioAuthoringInspectorDocument _cachedShellDocument;
        private ScenarioAuthoringInspectorDocument _cachedInspectorDocument;
        private ScenarioAuthoringInspectorDocument _cachedHoverDocument;
        private string _queuedReloadStatus;

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
            ScenarioAuthoringInputCaptureService inputCaptureService,
            ScenarioDraftSnapshotService snapshotService,
            ScenarioAuthoringSessionLifecycleService sessionLifecycle,
            ScenarioAuthoringRendererInteractionState rendererInteraction,
            ScenarioMapAuthoringRuntimeService mapAuthoring,
            ScenarioStorageAuthoringRuntimeService storageAuthoring,
            ScenarioVanillaInteractionRuntimeService vanillaInteraction,
            ScenarioAuthoringSelectionMenuService selectionMenu,
            ScenarioAuthoringUiDebugService uiDebug,
            ScenarioAuthoringStatusPort statusPort,
            ScenarioHoverVisualService hoverVisuals)
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
            _shortcutRouter = new ScenarioAuthoringShortcutRouter(
                commandService,
                sectionHub,
                inputCaptureService);
            _snapshotService = snapshotService;
            _sessionLifecycle = sessionLifecycle;
            _rendererInteraction = rendererInteraction;
            _inputCapture = inputCaptureService;
            _mapAuthoring = mapAuthoring;
            _storageAuthoring = storageAuthoring;
            _vanillaInteraction = vanillaInteraction;
            _selectionMenu = selectionMenu;
            _uiDebug = uiDebug;
            _statusPort = statusPort;
            _hoverVisuals = hoverVisuals;
            if (_sessionLifecycle != null)
                _sessionLifecycle.Transitioned += HandleSessionTransition;
            if (_statusPort != null)
                _statusPort.Published += SetStatusMessage;
        }

        private void HandleSessionTransition(ScenarioAuthoringSessionTransition transition)
        {
            if (transition == null)
                return;
            if (transition.Kind == ScenarioAuthoringSessionTransitionKind.StatusChanged)
            {
                SetStatusMessage(transition.Status);
                return;
            }
            if (transition.Kind == ScenarioAuthoringSessionTransitionKind.QueuedReloadStatusChanged)
            {
                if (string.IsNullOrEmpty(transition.Status))
                    ClearQueuedReloadStatus();
                else
                    SetQueuedReloadStatus(transition.Status);
                return;
            }

            switch (transition.Phase)
            {
                case ScenarioAuthoringSessionPhase.WorldLoading:
                    BeginWorldLoadingSession(transition.Pending, transition.Status);
                    break;
                case ScenarioAuthoringSessionPhase.Active:
                    if (transition.PreviousPhase == ScenarioAuthoringSessionPhase.WorldLoading
                        || transition.PreviousPhase == ScenarioAuthoringSessionPhase.ReloadPending)
                        CompleteWorldLoadingSession(transition.Active, transition.Status);
                    else
                        SetActiveSession(transition.Active);
                    break;
                case ScenarioAuthoringSessionPhase.ReloadPending:
                    BeginReloadPending(transition.Pending, transition.Status);
                    break;
                case ScenarioAuthoringSessionPhase.Inactive:
                    ClearActiveSession(transition.Reason);
                    break;
            }
        }

        internal void SetActiveSession(ScenarioAuthoringSession session)
        {
            if (session == null)
                return;

            _rendererInteraction.Bind(session.DraftId);

            lock (_sync)
            {
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
                    StatusMessage = ResolveInitialStatusMessage(_sessionStore.Current),
                    Settings = _settingsService.Load()
                };
                _layoutService.InitializeState(_state);
            }

            ResetInteractiveSubsystems();
            RefreshAuthoringArtifacts();
            _historyService.BindSession(session.DraftId);
            ScenarioSpriteSwapClipboard.Clear();
            _hoverVisuals.ClearSecondary();
            _layoutService.ApplyStageWorkspace(_state);
            _stageCoordinator.Synchronize(BuildContext(CurrentState, session));
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Active session set. DraftId=" + session.DraftId
                + ", ScenarioFile=" + session.ScenarioFilePath + ".");
            RaiseStateChanged();
        }

        internal void BeginWorldLoadingSession(ScenarioAuthoringSession session, string statusMessage)
        {
            if (session == null)
                return;

            _rendererInteraction.Bind(session.DraftId);

            string status = string.IsNullOrEmpty(statusMessage)
                ? "Loading game"
                : statusMessage;
            lock (_sync)
            {
                bool preserveBaseModeDialog = _state != null
                    && string.Equals(_state.FocusedEditorKind, ScenarioBaseModeAuthoringActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase);
                int focusedBaseMode = preserveBaseModeDialog ? _state.FocusedEditorIndex : -1;
                _state = new ScenarioAuthoringState
                {
                    IsActive = true,
                    ShellVisible = true,
                    SelectionModeActive = false,
                    WorldLoading = true,
                    WorldLoadingStatus = AppendQueuedReloadStatus(status),
                    ActiveStage = ScenarioStageKind.None,
                    ActiveBunkerStage = ScenarioStageKind.BunkerInside,
                    ActiveTool = ScenarioAuthoringTool.Objects,
                    ActiveShellTab = ScenarioAuthoringShellTab.Shell,
                    AssetMode = ScenarioAssetAuthoringMode.ReplaceExisting,
                    ActiveLayoutPreset = "default",
                    InspectorTab = ScenarioAuthoringInspectorTab.Properties,
                    ActiveDraftId = session.DraftId,
                    ActiveScenarioFilePath = session.ScenarioFilePath,
                    StatusMessage = AppendQueuedReloadStatus(status),
                    FocusedEditorKind = preserveBaseModeDialog ? ScenarioBaseModeAuthoringActions.FocusedEditorKind : null,
                    FocusedEditorIndex = focusedBaseMode,
                    Settings = _settingsService.Load()
                };
                _layoutService.InitializeState(_state);
            }

            ResetInteractiveSubsystems();
            RefreshAuthoringArtifacts();
            _historyService.BindSession(session.DraftId);
            ScenarioSpriteSwapClipboard.Clear();
            _hoverVisuals.ClearSecondary();
            _layoutService.ApplyStageWorkspace(_state);
            _stageCoordinator.Synchronize(BuildContext(CurrentState, session));
            MMLog.WriteInfo("[ScenarioAuthoringBackend] World-loading shell opened. DraftId=" + session.DraftId
                + ", ScenarioFile=" + session.ScenarioFilePath + ".");
            RaiseStateChanged();
        }

        internal void SetWorldLoadingStatus(string statusMessage)
        {
            lock (_sync)
            {
                if (_state == null || !_state.IsActive || !_state.WorldLoading)
                    return;

                string status = string.IsNullOrEmpty(statusMessage)
                    ? "Loading game"
                    : statusMessage;
                _state.WorldLoadingStatus = AppendQueuedReloadStatus(status);
                _state.StatusMessage = _state.WorldLoadingStatus;
            }

            RaiseStateChanged();
        }

        internal void CompleteWorldLoadingSession(ScenarioAuthoringSession session, string statusMessage)
        {
            if (session == null)
                return;

            _rendererInteraction.Bind(session.DraftId);

            lock (_sync)
            {
                if (_state == null || !_state.IsActive)
                    _state = new ScenarioAuthoringState();

                _state.IsActive = true;
                _state.ReloadPending = false;
                _state.ReloadPendingReason = null;
                _state.WorldLoading = false;
                _state.WorldLoadingStatus = null;
                _state.ShellVisible = true;
                _state.ActiveDraftId = session.DraftId;
                _state.ActiveScenarioFilePath = session.ScenarioFilePath;
                if (_state.ActiveStage == ScenarioStageKind.None)
                {
                    _state.ActiveStage = ScenarioStageKind.BunkerInside;
                    _state.ActiveBunkerStage = ScenarioStageKind.BunkerInside;
                    _state.ActiveTool = ScenarioAuthoringTool.Objects;
                    _state.ActiveShellTab = ScenarioAuthoringShellTab.Build;
                }
                _state.StatusMessage = AppendQueuedReloadStatus(string.IsNullOrEmpty(statusMessage)
                    ? ResolveInitialStatusMessage(_sessionStore.Current)
                    : statusMessage);
                if (_state.Settings == null)
                    _state.Settings = _settingsService.Load();
            }

            _layoutService.ApplyStageWorkspace(_state);
            _stageCoordinator.Synchronize(BuildContext(CurrentState, session));
            MMLog.WriteInfo("[ScenarioAuthoringBackend] World-loading shell completed. DraftId=" + session.DraftId + ".");
            RaiseStateChanged();
        }

        private static string ResolveInitialStatusMessage(ScenarioEditorSession editorSession)
        {
            if (editorSession != null && !string.IsNullOrEmpty(editorSession.LoadWarning))
                return editorSession.LoadWarning;

            return "Scenario authoring shell is active. Use playtest to make live shelter changes, then capture them back into the draft.";
        }

        internal void ClearActiveSession(string reason)
        {
            _rendererInteraction.Reset();
            lock (_sync)
            {
                _state = new ScenarioAuthoringState
                {
                    IsActive = false,
                    StatusMessage = reason ?? string.Empty,
                    Settings = _settingsService.Load()
                };
            }

            _hoverVisuals.Clear();
            ResetInteractiveSubsystems();
            RefreshAuthoringArtifacts();
            _historyService.Reset();
            ScenarioSpriteSwapClipboard.Clear();
            if (_inputCapture != null)
                _inputCapture.Clear();
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Active session cleared. Reason=" + (reason ?? "unspecified") + ".");
            RaiseStateChanged();
        }

        internal void BeginReloadPending(ScenarioAuthoringSession pendingSession, string reason)
        {
            lock (_sync)
            {
                if (_state == null)
                    _state = new ScenarioAuthoringState();

                _state.IsActive = true;
                _state.ReloadPending = true;
                _state.ReloadPendingReason = AppendQueuedReloadStatus(
                    string.IsNullOrEmpty(reason) ? "Reloading authoring world." : reason);
                _state.ShellVisible = true;
                _state.SelectionModeActive = false;
                _state.HoveredTarget = null;
                _state.SelectedTarget = null;
                _state.ActiveDraftId = pendingSession != null ? pendingSession.DraftId : _state.ActiveDraftId;
                _state.ActiveScenarioFilePath = pendingSession != null ? pendingSession.ScenarioFilePath : _state.ActiveScenarioFilePath;
                _state.StatusMessage = _state.ReloadPendingReason;
                if (_state.Settings == null)
                    _state.Settings = _settingsService.Load();
                ClearTransientSelection(_state);
            }

            _contextMenuService.Close();
            _hoverVisuals.Clear();
            MMLog.WriteInfo("[ScenarioAuthoringBackend] Reload pending. Reason=" + (reason ?? "unspecified") + ".");
            RaiseStateChanged();
        }

        // Trusted integration seam for selecting a live world object. CurrentState
        // is a defensive copy, so selection must be applied while the backend owns
        // the mutable state or it will disappear before the next command executes.
        public bool TrySelectRuntimeObject(
            UnityEngine.GameObject gameObject,
            out ScenarioAuthoringTarget target,
            out string message)
        {
            bool selected;
            lock (_sync)
            {
                selected = _selectionService.TrySelectRuntimeObject(_state, gameObject, out target, out message);
            }

            if (selected)
                RaiseStateChanged();
            return selected;
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

            if (_snapshotService != null)
                _snapshotService.Tick();

            if (snapshot.ReloadPending)
            {
                _hoverVisuals.Clear();
                lock (_sync)
                {
                    _state = snapshot;
                }
                return;
            }

            bool changed = false;
            ScenarioAuthoringContext context = BuildContext(snapshot, GetActiveSession());
            _contextMenuService.SyncTarget(snapshot.SelectedTarget);

            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.ToggleShell))
                changed |= _commandService.ExecuteWithResult(snapshot,
                    ShellUxCommand.Simple(ShellUxCommandKind.ToggleShell, ScenarioAuthoringActionIds.ActionShellToggle)).Changed;
            if (InputActionRegistry.IsDown(ScenarioAuthoringActionIds.SaveDraft))
                changed |= _commandService.ExecuteWithResult(snapshot, EditorLifecycleCommand.SaveDraft).Changed;
            if (!snapshot.WorldLoading && InputActionRegistry.IsDown(ScenarioAuthoringActionIds.TogglePlaytest))
                changed |= _commandService.ExecuteWithResult(snapshot, EditorLifecycleCommand.TogglePlaytest).Changed;
            if (_tutorialService != null && _tutorialService.CurrentTour() != null && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
                changed |= _commandService.ExecuteWithResult(snapshot,
                    ShellUxCommand.Simple(ShellUxCommandKind.TourExit, ScenarioAuthoringActionIds.ActionTourExit)).Changed;
            else if (_tutorialService != null && _tutorialService.GetActiveStep(snapshot) != null && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
                changed |= _commandService.ExecuteWithResult(snapshot,
                    ShellUxCommand.Simple(ShellUxCommandKind.TutorialSkipPrompt, ScenarioAuthoringActionIds.ActionTutorialSkipPrompt)).Changed;
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F1))
                changed |= _commandService.ExecuteWithResult(snapshot,
                    snapshot.HelpWindowOpen && snapshot.HelpShortcutsView
                        ? ShellUxCommand.Simple(ShellUxCommandKind.CloseHelp, ScenarioAuthoringActionIds.ActionShellCloseHelp)
                        : ShellUxCommand.Simple(ShellUxCommandKind.OpenShortcuts, ScenarioAuthoringActionIds.ActionShellOpenShortcuts)).Changed;

            bool shortcutChanged;
            if (_shortcutRouter != null && _shortcutRouter.TryRoute(snapshot, out shortcutChanged))
                changed |= shortcutChanged;

            string sectionMessage;
            if (!snapshot.WorldLoading && _sectionHub.Update(context, out sectionMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(sectionMessage))
                    snapshot.StatusMessage = sectionMessage;
            }

            if (snapshot.WorldLoading)
            {
                _hoverVisuals.Clear();
            }
            else if (_sectionHub.ShouldSuppressSelection)
            {
                changed |= ClearTransientSelection(snapshot);
                _hoverVisuals.UpdateFromState(snapshot);
            }
            else
            {
                changed |= _selectionService.Update(snapshot);
            }

            if (!snapshot.WorldLoading && _mapAuthoring != null && _mapAuthoring.Synchronize(snapshot, _sessionStore.Current))
                changed = true;
            if (!snapshot.WorldLoading && _storageAuthoring != null && _storageAuthoring.Synchronize(snapshot))
                changed = true;
            if (!snapshot.WorldLoading && _vanillaInteraction != null && _vanillaInteraction.Synchronize(snapshot))
                changed = true;

            _stageCoordinator.Synchronize(context);
            changed |= _selectionScopeService.ClearSelectionIfOutOfScope(snapshot);
            string tutorialMessage;
            if (_tutorialService != null && _tutorialService.Synchronize(snapshot, _sessionStore.Current, out tutorialMessage))
            {
                changed = true;
                if (!string.IsNullOrEmpty(tutorialMessage))
                    snapshot.StatusMessage = tutorialMessage;
            }
            _uiDebug.DumpSceneEntities(snapshot);

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

        internal void SetStatusMessage(string message)
        {
            lock (_sync)
            {
                if (_state == null)
                    _state = new ScenarioAuthoringState();
                _state.StatusMessage = AppendQueuedReloadStatus(message ?? string.Empty);
            }

            RaiseStateChanged();
        }

        internal void SetQueuedReloadStatus(string status)
        {
            lock (_sync)
            {
                _queuedReloadStatus = status ?? string.Empty;
                if (_state == null)
                    _state = new ScenarioAuthoringState();
                _state.StatusMessage = _queuedReloadStatus;
                if (_state.ReloadPending)
                    _state.ReloadPendingReason = _queuedReloadStatus;
                if (_state.WorldLoading)
                    _state.WorldLoadingStatus = _queuedReloadStatus;
            }

            RaiseStateChanged();
        }

        internal void ClearQueuedReloadStatus()
        {
            lock (_sync)
                _queuedReloadStatus = null;
        }

        internal void BeginVanillaInteractionSession(string kind, string assistNote)
        {
            lock (_sync)
            {
                if (_state == null)
                    _state = new ScenarioAuthoringState();

                if (!_state.VanillaInteractionActive)
                    _state.VanillaInteractionPreviousShellVisible = _state.ShellVisible;

                _state.VanillaInteractionActive = true;
                _state.VanillaInteractionKind = kind;
                _state.VanillaInteractionAssistNote = assistNote ?? string.Empty;
                _state.ShellVisible = false;
                if (!string.IsNullOrEmpty(assistNote))
                    _state.StatusMessage = assistNote;
            }

            RaiseStateChanged();
        }

        internal void ApplyMapSelection(ScenarioMapRegionSelection selection)
        {
            lock (_sync)
            {
                if (_state == null || !_state.IsActive)
                    return;

                _state.MapSelection = selection != null ? selection.Copy() : null;
                if (_state.MapSelection != null)
                    _state.StatusMessage = "Selected map region " + _state.MapSelection.DisplayName + ".";
            }

            RaiseStateChanged();
        }

        public bool ExecuteCommand(ScenarioAuthoringCommand command)
        {
            ScenarioCommandExecutionResult result = ExecuteCommandWithResult(command);
            return result.Changed;
        }

        public ScenarioCommandExecutionResult ExecuteCommandWithResult(ScenarioAuthoringCommand command)
        {
            return ExecuteWithResult(command);
        }

        private ScenarioCommandExecutionResult ExecuteWithResult(ScenarioAuthoringCommand command)
        {
            string automationId = command != null ? command.AutomationId : null;
            if (_inputCapture != null)
                _inputCapture.SuppressWorldInputForAction();

            ScenarioAuthoringState snapshot;
            lock (_sync)
            {
                snapshot = _state.Copy();
            }

            if (snapshot == null || !snapshot.IsActive)
                return ScenarioCommandExecutionResult.Unavailable(automationId, "Scenario authoring is not active.");

            ScenarioAuthoringContext context = BuildContext(snapshot, GetActiveSession());
            string beforeStatus = snapshot.StatusMessage;
            bool allowedDuringReload = command != null && command.Policy.AllowedDuringReload;
            ScenarioCommandExecutionResult result = _commandService.ExecuteWithResult(snapshot, command);
            bool changed = result.Changed;

            ScenarioAuthoringState transitionedState = CurrentState;
            if (transitionedState != null && transitionedState.ReloadPending)
            {
                if (snapshot.ReloadPending && allowedDuringReload)
                {
                    snapshot.ReloadPending = true;
                    snapshot.ReloadPendingReason = transitionedState.ReloadPendingReason;
                    snapshot.WorldLoading = transitionedState.WorldLoading;
                    snapshot.WorldLoadingStatus = transitionedState.WorldLoadingStatus;
                    snapshot.StatusMessage = transitionedState.StatusMessage;
                    lock (_sync)
                        _state = snapshot;
                }
                result.StatusMessage = transitionedState.StatusMessage ?? string.Empty;
                RaiseStateChanged();
                return result;
            }

            string sectionMessage;
            if (!snapshot.WorldLoading && _sectionHub.SynchronizeAfterAction(snapshot, out sectionMessage))
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

            result.StatusMessage = snapshot.StatusMessage ?? string.Empty;

            if (!result.Changed && string.IsNullOrEmpty(result.Reason))
                result.Reason = !string.Equals(beforeStatus, snapshot.StatusMessage) && !string.IsNullOrEmpty(snapshot.StatusMessage)
                    ? snapshot.StatusMessage
                    : "Action did not complete.";

            if (changed || !string.Equals(beforeStatus, snapshot.StatusMessage))
                RaiseStateChanged();
            return result;
        }

        private string AppendQueuedReloadStatus(string status)
        {
            if (string.IsNullOrEmpty(_queuedReloadStatus))
                return status ?? string.Empty;
            if (string.IsNullOrEmpty(status)
                || string.Equals(status, _queuedReloadStatus, StringComparison.Ordinal))
            {
                return _queuedReloadStatus;
            }

            if (status.EndsWith(_queuedReloadStatus, StringComparison.Ordinal))
                return status;

            return status + " " + _queuedReloadStatus;
        }

        public bool UpdateWindowFrame(string windowId, float x, float y, float width, float height, bool persist)
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

        public bool BringWindowToFront(string windowId)
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
            ScenarioEditorSession editorSession = _sessionStore.Current;
            int draftRevision = editorSession != null ? editorSession.DraftRevision : -1;
            int contextMenuRevision = _contextMenuService.Revision;
            ScenarioAuthoringState state;
            ScenarioAuthoringSession authoringSession;
            int presentationRevision;

            lock (_sync)
            {
                presentationRevision = _presentationRevision;
                authoringSession = _sessionLifecycle.Active;
                EnsurePresentationCacheIdentity(presentationRevision, draftRevision, contextMenuRevision, editorSession, authoringSession);
                if (_cachedShellViewModel != null)
                    return _cachedShellViewModel;

                state = _state.Copy();
            }

            ScenarioAuthoringShellViewModel viewModel = _presentationBuilder.BuildShellViewModel(
                BuildContext(state, authoringSession),
                _contextMenuService.Current);

            lock (_sync)
            {
                if (PresentationCacheIdentityMatches(presentationRevision, draftRevision, contextMenuRevision, editorSession, authoringSession))
                    _cachedShellViewModel = viewModel;
            }
            return viewModel;
        }

        public ScenarioAuthoringInspectorDocument GetShellDocument()
        {
            return GetCachedDocument(CachedDocumentKind.Shell);
        }

        public ScenarioAuthoringInspectorDocument GetInspectorDocument()
        {
            return GetCachedDocument(CachedDocumentKind.Inspector);
        }

        public ScenarioAuthoringInspectorDocument GetHoverDocument()
        {
            return GetCachedDocument(CachedDocumentKind.Hover);
        }

        private ScenarioAuthoringInspectorDocument GetCachedDocument(CachedDocumentKind documentKind)
        {
            ScenarioEditorSession editorSession = _sessionStore.Current;
            int draftRevision = editorSession != null ? editorSession.DraftRevision : -1;
            int contextMenuRevision = _contextMenuService.Revision;
            ScenarioAuthoringState state;
            ScenarioAuthoringSession authoringSession;
            int presentationRevision;
            ScenarioAuthoringInspectorDocument cached;

            lock (_sync)
            {
                presentationRevision = _presentationRevision;
                authoringSession = _sessionLifecycle.Active;
                EnsurePresentationCacheIdentity(presentationRevision, draftRevision, contextMenuRevision, editorSession, authoringSession);
                cached = documentKind == CachedDocumentKind.Shell
                    ? _cachedShellDocument
                    : documentKind == CachedDocumentKind.Inspector ? _cachedInspectorDocument : _cachedHoverDocument;
                if (cached != null)
                    return cached;
                state = _state.Copy();
            }

            ScenarioAuthoringInspectorDocument document = documentKind == CachedDocumentKind.Shell
                ? _presentationBuilder.BuildShellDocument(BuildContext(state, authoringSession))
                : documentKind == CachedDocumentKind.Inspector
                    ? _presentationBuilder.BuildInspectorDocument(BuildContext(state, authoringSession))
                    : _presentationBuilder.BuildHoverDocument(state);
            lock (_sync)
            {
                if (PresentationCacheIdentityMatches(presentationRevision, draftRevision, contextMenuRevision, editorSession, authoringSession))
                {
                    if (documentKind == CachedDocumentKind.Shell) _cachedShellDocument = document;
                    else if (documentKind == CachedDocumentKind.Inspector) _cachedInspectorDocument = document;
                    else _cachedHoverDocument = document;
                }
            }
            return document;
        }

        private void EnsurePresentationCacheIdentity(
            int presentationRevision,
            int draftRevision,
            int contextMenuRevision,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession)
        {
            if (PresentationCacheIdentityMatches(presentationRevision, draftRevision, contextMenuRevision, editorSession, authoringSession))
                return;

            _cachedShellPresentationRevision = presentationRevision;
            _cachedShellDraftRevision = draftRevision;
            _cachedShellContextMenuRevision = contextMenuRevision;
            _cachedShellEditorSession = editorSession;
            _cachedShellAuthoringSession = authoringSession;
            _cachedShellViewModel = null;
            _cachedShellDocument = null;
            _cachedInspectorDocument = null;
            _cachedHoverDocument = null;
        }

        private bool PresentationCacheIdentityMatches(
            int presentationRevision,
            int draftRevision,
            int contextMenuRevision,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession)
        {
            return _cachedShellPresentationRevision == presentationRevision
                && _cachedShellDraftRevision == draftRevision
                && _cachedShellContextMenuRevision == contextMenuRevision
                && object.ReferenceEquals(_cachedShellEditorSession, editorSession)
                && object.ReferenceEquals(_cachedShellAuthoringSession, authoringSession);
        }

        private void RaiseStateChanged()
        {
            lock (_sync)
            {
                _presentationRevision++;
            }
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
            return _sessionLifecycle.Active;
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
            _selectionMenu.Reset();
            _contextMenuService.Close();
            _sectionHub.ResetInteractiveSubsystems();
        }

        private void RefreshAuthoringArtifacts()
        {
            _sectionHub.RefreshAuthoringArtifacts();
        }
    }
}
