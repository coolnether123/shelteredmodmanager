using System;
using ModAPI.Core;
using ShelteredAPI.Events;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredAPI.Content;
using ShelteredAPI.Core;
using ShelteredAPI.Saves.Runtime;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioAuthoringBootstrapService
    {
        private const float DraftWarmupSeconds = 2f;
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringBackendService _backend;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly ScenarioAuthoringMenuService _menuService;
        private readonly ScenarioAuthoringPresentationService _presentation;
        private readonly IScenarioEditorService _editorService;
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioAuthoringInventoryProjectionService _inventoryProjectionService;
        private readonly ScenarioAuthoringEntryFlowService _entryFlowService;
        private ScenarioAuthoringSession _pendingSession;
        private ScenarioAuthoringSession _activeSession;
        private string _lastPendingDraftId;
        private string _lastPendingBlockingReason;
        private string _warmupDraftId;
        private float _warmupElapsedSeconds;
        private string _worldLoadingShellDraftId;

        public static ScenarioAuthoringBootstrapService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioAuthoringBootstrapService>(); }
        }

        internal ScenarioAuthoringBootstrapService(
            ScenarioAuthoringBackendService backend,
            ScenarioAuthoringDraftRepository draftRepository,
            ScenarioAuthoringMenuService menuService,
            ScenarioAuthoringPresentationService presentation,
            IScenarioEditorService editorService,
            IScenarioSaveLibrary saveLibrary,
            IScenarioRuntimeBindingService runtimeBindingService,
            ScenarioAuthoringCaptureService captureService,
            ScenarioAuthoringInventoryProjectionService inventoryProjectionService,
            ScenarioAuthoringEntryFlowService entryFlowService)
        {
            _backend = backend;
            _draftRepository = draftRepository;
            _menuService = menuService;
            _presentation = presentation;
            _editorService = editorService;
            _saveLibrary = saveLibrary;
            _runtimeBindingService = runtimeBindingService;
            _captureService = captureService;
            _inventoryProjectionService = inventoryProjectionService;
            _entryFlowService = entryFlowService;
            try { GameEvents.OnAfterLoad += HandleAfterLoad; }
            catch { }
        }

        public ScenarioAuthoringSession QueueNewDraft(ScenarioBaseGameMode baseMode, SaveManager.SaveType launchSaveType)
        {
            return QueueNewDraft(baseMode, launchSaveType, false);
        }

        public ScenarioAuthoringSession QueueNewDraft(ScenarioBaseGameMode baseMode, SaveManager.SaveType launchSaveType, bool showBaselinePicker)
        {
            ScenarioAuthoringSession obsolete = null;
            ScenarioAuthoringSession result;
            lock (_sync)
            {
                if (_pendingSession != null)
                {
                    if (_pendingSession.BaseMode == baseMode)
                    {
                        if (_entryFlowService != null)
                            _entryFlowService.BeginNewDraftLaunch(_pendingSession, showBaselinePicker);
                        MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Reusing pending draft bootstrap: " + _pendingSession.DraftId + ".");
                        return _pendingSession;
                    }

                    // Stale draft with a different base mode - replace it. Cleanup happens
                    // after releasing the lock so file I/O doesn't block other callers.
                    obsolete = _pendingSession;
                    _pendingSession = null;
                    _lastPendingDraftId = null;
                    _lastPendingBlockingReason = null;
                    _worldLoadingShellDraftId = null;
                    ResetPendingWarmup();
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Discarding pending draft '" + obsolete.DraftId
                        + "' (mode=" + obsolete.BaseMode + ") to create new " + baseMode + " draft.");
                }

                ScenarioAuthoringDraftRepository.DraftRecord draft = _draftRepository.CreateDraft(baseMode);
                _pendingSession = ScenarioAuthoringSession.Create(
                    draft.Info,
                    baseMode,
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    draft.StartupSave != null ? draft.StartupSave.id : null,
                    draft.Slot,
                    draft.StartupSave,
                    launchSaveType);
                _pendingSession.RequestStartingCastAutoPopulateAfterBootstrap();
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Queued draft authoring bootstrap: " + _pendingSession.DraftId + ".");
                result = _pendingSession;
            }

            if (_entryFlowService != null)
                _entryFlowService.BeginNewDraftLaunch(result, showBaselinePicker);

            if (obsolete != null)
                CleanupPendingDraftArtifacts(obsolete, "Replaced by new " + baseMode + " draft.");

            return result;
        }

        public ScenarioAuthoringSession QueueExistingDraft(string draftId, SaveManager.SaveType launchSaveType)
        {
            if (string.IsNullOrEmpty(draftId))
                return null;

            ScenarioInfo draftInfo;
            if (!_draftRepository.TryGet(draftId, out draftInfo) || draftInfo == null)
                return null;

            SaveEntry startupSave;
            if (!_draftRepository.TryGetDraftSaveEntry(draftId, out startupSave) || startupSave == null)
                return null;

            ScenarioBaseGameMode baseMode = ResolveDraftBaseMode(draftInfo);
            lock (_sync)
            {
                if (_pendingSession != null
                    && string.Equals(_pendingSession.DraftId, draftId, StringComparison.OrdinalIgnoreCase))
                {
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Reusing pending existing draft bootstrap: " + _pendingSession.DraftId + ".");
                    return _pendingSession;
                }

                if (_pendingSession != null)
                {
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Replacing pending draft bootstrap '" + _pendingSession.DraftId
                        + "' with existing draft '" + draftId + "'.");
                    _pendingSession = null;
                    _lastPendingDraftId = null;
                    _lastPendingBlockingReason = null;
                    _worldLoadingShellDraftId = null;
                    ResetPendingWarmup();
                }

                _pendingSession = ScenarioAuthoringSession.Create(
                    draftInfo,
                    baseMode,
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    startupSave.id,
                    startupSave.absoluteSlot,
                    startupSave,
                    launchSaveType);
                if (_entryFlowService != null)
                    _entryFlowService.BeginExistingDraftLaunch(_pendingSession);
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Queued existing draft authoring bootstrap: " + _pendingSession.DraftId + ".");
                return _pendingSession;
            }
        }

        public void CancelPendingDraft(string reason)
        {
            CancelPendingDraft(reason, true);
        }

        public void CancelPendingDraft(string reason, bool cleanupDraftArtifacts)
        {
            ScenarioAuthoringSession pending = null;
            bool clearedActiveShell = false;
            lock (_sync)
            {
                if (_pendingSession == null)
                    return;

                pending = _pendingSession;
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared pending draft bootstrap '" + _pendingSession.DraftId
                    + "'. Reason=" + (reason ?? "unspecified") + ".");
                _pendingSession = null;
                if (_activeSession != null && string.Equals(_activeSession.DraftId, pending.DraftId, StringComparison.Ordinal))
                {
                    _activeSession = null;
                    clearedActiveShell = true;
                }
                _lastPendingDraftId = null;
                _lastPendingBlockingReason = null;
                _worldLoadingShellDraftId = null;
                ResetPendingWarmup();
            }

            ClearLaunchRedirects(pending, reason);
            if (clearedActiveShell)
                _backend.ClearActiveSession(reason ?? "Pending draft canceled.");
            if (_entryFlowService != null)
                _entryFlowService.Hide("Pending draft canceled.");
            if (cleanupDraftArtifacts && pending != null)
                CleanupPendingDraftArtifacts(pending, reason);
        }

        private static ScenarioBaseGameMode ResolveDraftBaseMode(ScenarioInfo draftInfo)
        {
            if (draftInfo == null || string.IsNullOrEmpty(draftInfo.FilePath))
                return ScenarioBaseGameMode.Survival;

            try
            {
                ScenarioDefinition definition = new ScenarioDefinitionSerializer().Load(draftInfo.FilePath);
                if (definition != null && Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                    return definition.BaseGameMode;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Could not resolve draft base mode for '"
                    + draftInfo.Id + "': " + ex.Message);
            }

            return ScenarioBaseGameMode.Survival;
        }

        public void RequestCloseActiveSession(string reason, bool resumeGame)
        {
            CloseActiveSession(reason ?? "Closed from authoring shell.", resumeGame);
        }

        public void RequestReloadActiveSession(ScenarioAuthoringSession pendingSession, string reason)
        {
            ScenarioAuthoringSession previous = null;
            lock (_sync)
            {
                previous = _activeSession;
                _activeSession = null;
                _worldLoadingShellDraftId = null;
            }

            _backend.BeginReloadPending(pendingSession, reason ?? "Reloading authoring world.");
            if (_entryFlowService != null)
                _entryFlowService.BeginReload(pendingSession, reason ?? "Reloading authoring world.");

            try
            {
                _editorService.CloseEditor(false);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Editor close failed while preparing authoring reload: " + ex.Message);
            }

            SaveRuntimeState.ClearActiveCustomSession();
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Prepared active authoring session for reload. previousDraft="
                + (previous != null ? previous.DraftId : "<none>")
                + " pendingDraft=" + (pendingSession != null ? pendingSession.DraftId : "<none>")
                + " reason=" + (reason ?? "unspecified") + ".");
        }

        public bool RequestCloseActiveSessionToMainMenu(string reason, out string message)
        {
            message = null;
            ScenarioAuthoringSession active = GetActiveSession();
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            ScenarioAuthoringState backendState = _backend.CurrentState;
            bool closeableActiveSession = IsEditingDraftSession(active);
            bool closeableRuntimeState = !closeableActiveSession && IsCloseableRuntimeState(backendState, editorSession);

            if (!closeableActiveSession && !closeableRuntimeState)
            {
                message = backendState != null && backendState.ReloadPending
                    ? "Scenario editor is restarting; close is disabled until the reload completes."
                    : "Scenario editor is already closed.";
                return true;
            }

            if (HasUnsavedDraftChanges(editorSession))
            {
                MessageBox.Show(MessageBoxButtons.YesNo_Buttons, "UI.Save", new MessageBoxResponse(delegate(int response)
                {
                    if (response != 1)
                    {
                        MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Close-to-menu canceled by unsaved-changes prompt.");
                        return;
                    }

                    if (closeableRuntimeState)
                        CloseRuntimeStateToMainMenu(reason ?? "Closed from authoring shell.", backendState);
                    else
                        CloseActiveSessionToMainMenu(reason ?? "Closed from authoring shell.");
                }));
                message = "Close requested; confirm saving the draft before returning to the main menu.";
                return true;
            }

            bool closed = closeableRuntimeState
                ? CloseRuntimeStateToMainMenu(reason ?? "Closed from authoring shell.", backendState)
                : CloseActiveSessionToMainMenu(reason ?? "Closed from authoring shell.");
            message = closed
                ? "Closed from authoring shell and returning to the main menu."
                : "Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.";
            return true;
        }

        public void PrepareActiveSessionForVanillaShutdown(string reason)
        {
            ScenarioAuthoringSession active = GetActiveSession();
            if (!IsEditingDraftSession(active))
                return;

            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(active.ScenarioFilePath);
                if (validation != null && !validation.IsValid)
                    MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Saved active draft with validation errors before vanilla shutdown. draftId="
                        + active.DraftId + ".");
                else
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Saved active draft before vanilla shutdown. draftId="
                        + active.DraftId + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Active draft save before vanilla shutdown failed: " + ex.Message);
            }

            CloseActiveSession(reason ?? "Vanilla Save & Exit confirmed.", true);
        }

        public bool HasPendingDraftLaunch()
        {
            lock (_sync)
            {
                return IsDraftAuthoringSession(_pendingSession);
            }
        }

        public bool IsEditingDraftActive()
        {
            ScenarioAuthoringSession active = GetActiveSession();
            return IsEditingDraftSession(active);
        }

        internal ScenarioAuthoringSession CurrentOrPendingSessionForEntryFlow()
        {
            lock (_sync)
            {
                return _pendingSession ?? _activeSession;
            }
        }

        public void Update()
        {
            if (!HasPendingOrActiveDraftSession())
            {
                EnsureInactiveAuthoringState("No draft scenario authoring session is pending or active.");
                return;
            }

            HandleActiveSessionBoundaries();
            TryBootstrapPendingDraft();

            ScenarioAuthoringSession active = GetActiveSession();
            if (!IsEditingDraftSession(active))
            {
                _presentation.Update();
                _menuService.Update(null);
                return;
            }

            ScenarioAuthoringState activeState = _backend.CurrentState;
            if (activeState == null || !activeState.WorldLoading)
                _editorService.MaintainAuthoringPause();
            _backend.Update();
            _presentation.Update();
            _menuService.Update(active);
            if (_inventoryProjectionService != null)
                _inventoryProjectionService.UpdateLiveTruth(_editorService.CurrentSession);
        }

        private void TryBootstrapPendingDraft()
        {
            ScenarioAuthoringSession pending = null;
            lock (_sync)
            {
                pending = _pendingSession;
            }

            if (pending == null)
                return;

            if (!string.Equals(_lastPendingDraftId, pending.DraftId, StringComparison.Ordinal))
            {
                _lastPendingDraftId = pending.DraftId;
                _lastPendingBlockingReason = null;
                ResetPendingWarmup();
                if (_entryFlowService != null)
                    _entryFlowService.SetLoadingStatus("Status: game loading - waiting for the shelter scene.");
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Pending draft '" + pending.DraftId + "' waiting to bootstrap. BaseMode="
                    + pending.BaseMode + ", ScenarioFile=" + pending.ScenarioFilePath + ".");
            }

            bool warmupStarted = !pending.ReenterPlaytestAfterBootstrap
                && string.Equals(_warmupDraftId, pending.DraftId, StringComparison.Ordinal);
            string blockingReason;
            if (!warmupStarted)
            {
                HideCompletedShelterLoadingScreen(pending.ExpectedSceneName);
                if (!IsExpectedSceneActive(pending.ExpectedSceneName, out blockingReason))
                {
                    if (!string.Equals(_lastPendingBlockingReason, blockingReason, StringComparison.Ordinal))
                    {
                        _lastPendingBlockingReason = blockingReason;
                        if (_entryFlowService != null)
                            _entryFlowService.SetLoadingStatus("Status: game loading - " + blockingReason);
                        MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId + "' is waiting for target scene. Reason="
                            + blockingReason + ".");
                    }

                    return;
                }

                if (pending.ReenterPlaytestAfterBootstrap || pending.SuppressIntroCutsceneAfterSceneLoad)
                {
                    string cutsceneBlockingReason;
                    if (!ScenarioPlaytestRestartCutsceneGuard.TryClearBlockingIntroCutscene(pending.DraftId, out cutsceneBlockingReason)
                        && pending.ReenterPlaytestAfterBootstrap)
                    {
                        FailPlaytestRestartBackToEditor("Playtest restart failed; returned to the editor. " + cutsceneBlockingReason);
                        CancelPendingDraft("Intro cutscene could not be cleared for playtest restart.", false);
                        return;
                    }
                }

                if (!TryOpenWorldLoadingShell(pending))
                    return;

                if (!ScenarioWorldReady.Evaluate(out blockingReason))
                {
                    if (!string.Equals(_lastPendingBlockingReason, blockingReason, StringComparison.Ordinal))
                    {
                        _lastPendingBlockingReason = blockingReason;
                        if (_backend != null)
                            _backend.SetWorldLoadingStatus("Loading game... " + blockingReason);
                        if (_entryFlowService != null)
                            _entryFlowService.SetLoadingStatus("Status: world loading - " + blockingReason);
                        MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId + "' is waiting for world readiness. Reason="
                            + blockingReason + ".");
                    }

                    return;
                }
            }

            if (!pending.ReenterPlaytestAfterBootstrap && !TryCompleteDraftWarmup(pending))
                return;

            if (pending.ReenterPlaytestAfterBootstrap)
                ResetPendingWarmup();

            if (!string.IsNullOrEmpty(_lastPendingBlockingReason))
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] World readiness satisfied for draft '" + pending.DraftId
                    + "'. Continuing authoring bootstrap.");
                _lastPendingBlockingReason = null;
            }

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Loading editor session for draft '" + pending.DraftId
                + "' from " + pending.ScenarioFilePath + ".");
            if (_entryFlowService != null)
                _entryFlowService.SetLoadingStatus("Status: editor loading - opening the draft tools.");
            ScenarioEditorSession editorSession;
            try
            {
                editorSession = IsEditorSessionForDraft(pending)
                    ? _editorService.CurrentSession
                    : _editorService.LoadEditMode(pending.ScenarioFilePath);
            }
            catch (Exception ex)
            {
                // A corrupt or missing draft file must not bubble up through Update() and crash
                // the game. Cancel the draft so the player can try again cleanly.
                string loadFailureMessage = (ex != null
                    && !string.IsNullOrEmpty(ex.Message)
                    && ex.Message.IndexOf("recovery copy could not be loaded", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? "This draft could not be opened: its scenario file and backup are both unreadable."
                    : "Could not load draft session. " + (ex != null ? ex.Message : "unknown error");
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Editor session load failed for draft '"
                    + pending.DraftId + "': " + ex.Message);
                _backend.SetStatusMessage(loadFailureMessage);
                CancelPendingDraft(loadFailureMessage);
                return;
            }
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Editor session loaded for draft '" + pending.DraftId + "'. DefinitionId="
                + (editorSession != null && editorSession.WorkingDefinition != null ? editorSession.WorkingDefinition.Id : "<null>") + ".");
            CaptureBaseDefaultFamilyIfRequested(pending, editorSession);
            AutoPopulateStartingCastIfRequested(pending, editorSession);
            ActivateScenarioBinding(pending);
            ClearLaunchRedirects(pending, "Authoring bootstrap completed.");
            bool worldLoadingShellOpen = string.Equals(_worldLoadingShellDraftId, pending.DraftId, StringComparison.Ordinal);
            lock (_sync)
            {
                _activeSession = pending;
                _pendingSession = null;
                _lastPendingDraftId = null;
                _lastPendingBlockingReason = null;
                _worldLoadingShellDraftId = null;
                ResetPendingWarmup();
            }

            if (worldLoadingShellOpen)
                _backend.CompleteWorldLoadingSession(pending, null);
            else
                _backend.SetActiveSession(pending);
            if (_entryFlowService != null)
                _entryFlowService.MarkEditorReady(pending);
            ProjectStartingInventoryAfterBootstrap(pending, editorSession);
            if (editorSession != null && !string.IsNullOrEmpty(editorSession.LoadWarning))
                _backend.SetStatusMessage(editorSession.LoadWarning);
            if (pending.ReenterPlaytestAfterBootstrap)
                ReenterPlaytest(pending);
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Activated authoring session for draft '" + pending.DraftId
                + "'. Opening authoring shell.");
            if (!worldLoadingShellOpen)
                _menuService.Open(pending, true);
        }

        private bool TryOpenWorldLoadingShell(ScenarioAuthoringSession pending)
        {
            if (pending == null || pending.ReenterPlaytestAfterBootstrap)
                return true;

            if (string.Equals(_worldLoadingShellDraftId, pending.DraftId, StringComparison.Ordinal))
                return true;

            ScenarioEditorSession editorSession;
            try
            {
                editorSession = IsEditorSessionForDraft(pending)
                    ? _editorService.CurrentSession
                    : _editorService.LoadEditMode(pending.ScenarioFilePath);
            }
            catch (Exception ex)
            {
                string loadFailureMessage = (ex != null
                    && !string.IsNullOrEmpty(ex.Message)
                    && ex.Message.IndexOf("recovery copy could not be loaded", StringComparison.OrdinalIgnoreCase) >= 0)
                    ? "This draft could not be opened: its scenario file and backup are both unreadable."
                    : "Could not load draft session. " + (ex != null ? ex.Message : "unknown error");
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Early editor session load failed for draft '"
                    + pending.DraftId + "': " + (ex != null ? ex.Message : "unknown error"));
                _backend.SetStatusMessage(loadFailureMessage);
                CancelPendingDraft(loadFailureMessage);
                return false;
            }

            lock (_sync)
            {
                _activeSession = pending;
                _worldLoadingShellDraftId = pending.DraftId;
            }

            string status = "Loading game... waiting for the shelter world to finish loading.";
            _backend.BeginWorldLoadingSession(pending, status);
            if (_entryFlowService != null)
                _entryFlowService.MarkEditorReady(pending);
            _menuService.Open(pending, true);
            ScenarioAuthoringPauseService.Instance.ReleasePause("World-loading shell opened before draft warmup completed.");
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Opened navigable draft shell while world loads. draftId="
                + pending.DraftId + " definitionLoaded=" + (editorSession != null && editorSession.WorkingDefinition != null) + ".");
            return true;
        }

        private void ReenterPlaytest(ScenarioAuthoringSession pending)
        {
            try
            {
                string cutsceneBlockingReason;
                if (!ScenarioPlaytestRestartCutsceneGuard.TryClearBlockingIntroCutscene(
                        pending != null ? pending.DraftId : null,
                        out cutsceneBlockingReason))
                {
                    FailPlaytestRestartBackToEditor("Playtest restart failed; returned to the editor. " + cutsceneBlockingReason);
                    return;
                }

                ScenarioApplyResult result = _editorService.BeginPlaytest();
                int messages = result != null && result.Messages != null ? result.Messages.Length : 0;
                ScenarioEditorSession editorSession = _editorService.CurrentSession;
                bool running = editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting;
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Re-entered playtest after authoring reload. draftId="
                    + (pending != null ? pending.DraftId : "<none>") + " messages=" + messages + ".");
                if (!running)
                {
                    string message = "Playtest restart failed; returned to the editor.";
                    if (result != null && result.Messages != null && result.Messages.Length > 0)
                        message = result.Messages[0];
                    MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Playtest restart did not enter running state. " + message);
                    FailPlaytestRestartBackToEditor(message);
                    return;
                }

                _backend.SetStatusMessage("Playtest restarted.");
                _backend.Refresh();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Failed to re-enter playtest after authoring reload: " + ex.Message);
                FailPlaytestRestartBackToEditor("Playtest restart failed: " + ex.Message);
            }
        }

        private void CaptureBaseDefaultFamilyIfRequested(ScenarioAuthoringSession pending, ScenarioEditorSession editorSession)
        {
            if (pending == null || !pending.CaptureBaseDefaultFamilyAfterBootstrap)
                return;

            if (_captureService == null || editorSession == null || editorSession.WorkingDefinition == null)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Base default family capture skipped because the capture service or editor session was unavailable. draftId="
                    + (pending != null ? pending.DraftId : "<none>") + ".");
                return;
            }

            string captureMessage;
            if (!_captureService.CaptureCurrentFamily(editorSession, out captureMessage))
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Base default family capture failed for draft '"
                    + pending.DraftId + "': " + (captureMessage ?? "unknown error") + ".");
                return;
            }

            editorSession.WorkingDefinition.BaseFamilyChoice = ScenarioBaseFamilyChoices.UseBaseDefaultFamily;
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(pending.ScenarioFilePath);
                if (validation != null && !validation.IsValid)
                    MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Captured base default family and saved draft with validation errors for draft '"
                        + pending.DraftId + "'.");
                else
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Captured base default family for draft '"
                        + pending.DraftId + "'. " + (captureMessage ?? string.Empty));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Captured base default family but failed to save draft '"
                    + pending.DraftId + "': " + ex.Message);
            }
        }

        private void AutoPopulateStartingCastIfRequested(ScenarioAuthoringSession pending, ScenarioEditorSession editorSession)
        {
            if (pending == null || !pending.AutoPopulateStartingCastAfterBootstrap)
                return;

            if (_captureService == null || editorSession == null || editorSession.WorkingDefinition == null)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Starting cast auto-populate skipped because the capture service or editor session was unavailable. draftId="
                    + (pending != null ? pending.DraftId : "<none>") + ".");
                return;
            }

            string captureMessage;
            if (!_captureService.CaptureCurrentFamilyIfEmpty(editorSession, out captureMessage))
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Starting cast auto-populate failed for draft '"
                    + pending.DraftId + "': " + (captureMessage ?? "unknown error") + ".");
                return;
            }

            if (!string.IsNullOrEmpty(captureMessage)
                && captureMessage.IndexOf("skipped", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] " + captureMessage + " draftId=" + pending.DraftId + ".");
                return;
            }

            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(pending.ScenarioFilePath);
                if (validation != null && !validation.IsValid)
                    MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Auto-populated starting cast and saved draft with validation errors for draft '"
                        + pending.DraftId + "'.");
                else
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Auto-populated starting cast for draft '"
                        + pending.DraftId + "'. " + (captureMessage ?? string.Empty));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Auto-populated starting cast but failed to save draft '"
                    + pending.DraftId + "': " + ex.Message);
            }
        }

        private void ProjectStartingInventoryAfterBootstrap(ScenarioAuthoringSession pending, ScenarioEditorSession editorSession)
        {
            if (_inventoryProjectionService == null || pending == null || pending.ReenterPlaytestAfterBootstrap)
                return;

            _inventoryProjectionService.ResetForCurrentWorld(editorSession);
            string projectionMessage;
            if (!_inventoryProjectionService.TryProject(editorSession, "authoring bootstrap", out projectionMessage)
                && !string.IsNullOrEmpty(projectionMessage))
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] " + projectionMessage);
            }
        }

        private static bool IsExpectedSceneActive(string expectedSceneName, out string blockingReason)
        {
            if (string.IsNullOrEmpty(expectedSceneName))
            {
                blockingReason = null;
                return true;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                blockingReason = "The active scene is not valid yet; expected '" + expectedSceneName + "'.";
                return false;
            }

            if (!string.Equals(activeScene.name, expectedSceneName, StringComparison.Ordinal))
            {
                blockingReason = "Expected scene '" + expectedSceneName + "' but active scene is '" + activeScene.name + "'.";
                return false;
            }

            blockingReason = null;
            return true;
        }

        private void HideCompletedShelterLoadingScreen(string expectedSceneName)
        {
            if (LoadingScreen.Instance == null || !LoadingScreen.Instance.isShowing)
                return;

            if (!ScenarioWorldReady.IsShelterSceneActive())
                return;

            if (!string.IsNullOrEmpty(expectedSceneName)
                && !string.Equals(SceneManager.GetActiveScene().name, expectedSceneName, StringComparison.Ordinal))
                return;

            if (!string.IsNullOrEmpty(LoadingScreen.nextLevel))
                return;

            if (SaveManager.instance != null && (SaveManager.instance.isLoading || SaveManager.instance.isSaving))
                return;

            LoadingScreen.Instance.HideLoadingScreen();
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Hid completed authoring reload loading screen for scene "
                + SceneManager.GetActiveScene().name + ".");
        }

        private void FailPlaytestRestartBackToEditor(string message)
        {
            try
            {
                if (_editorService.CurrentSession != null)
                    _editorService.EndPlaytest();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Failed to restore authoring pause after playtest restart failure: " + ex.Message);
            }

            _backend.SetStatusMessage(string.IsNullOrEmpty(message)
                ? "Playtest restart failed; returned to the editor."
                : message);
        }

        private bool TryCompleteDraftWarmup(ScenarioAuthoringSession pending)
        {
            if (pending == null)
                return false;

            if (!string.Equals(_warmupDraftId, pending.DraftId, StringComparison.Ordinal))
            {
                _warmupDraftId = pending.DraftId;
                _warmupElapsedSeconds = 0f;
                if (_backend != null)
                    _backend.SetWorldLoadingStatus("Loading game... shelter ready, letting the first moments settle.");
                if (_entryFlowService != null)
                    _entryFlowService.SetLoadingStatus("Status: world ready - letting the shelter settle before opening tools.");
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId
                    + "' world is ready. Letting the shelter run for "
                    + DraftWarmupSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " seconds before authoring pause.");
            }

            float warmupDelta = Time.deltaTime;
            if (warmupDelta <= 0f)
                warmupDelta = Time.unscaledDeltaTime;
            if (warmupDelta > 0f)
                _warmupElapsedSeconds += warmupDelta;

            if (_warmupElapsedSeconds < DraftWarmupSeconds)
                return false;

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId
                + "' warmup completed after "
                + _warmupElapsedSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + " seconds of running simulation. Loading editor session.");
            return true;
        }

        private void ResetPendingWarmup()
        {
            _warmupDraftId = null;
            _warmupElapsedSeconds = 0f;
        }

        private void ActivateScenarioBinding(ScenarioAuthoringSession session)
        {
            if (session == null)
                return;

            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            ShelteredScenarioRuntimeBindingManager.Instance.SetBinding(new ScenarioRuntimeBinding
            {
                ScenarioId = session.DraftId,
                VersionApplied = editorSession != null && editorSession.WorkingDefinition != null
                    ? editorSession.WorkingDefinition.Version
                    : session.Version,
                IsActive = true,
                IsConvertedToNormalSave = false,
                DayCreated = GameTime.Day
            });
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Runtime binding activated. ScenarioId=" + session.DraftId
                + ", Version=" + (editorSession != null && editorSession.WorkingDefinition != null
                    ? editorSession.WorkingDefinition.Version
                    : session.Version)
                + ", DayCreated=" + GameTime.Day + ".");
        }

        private void HandleAfterLoad(SaveData data)
        {
            if (IsExpectedAuthoringSaveLoaded())
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Ignored save-load event for the active authoring draft.");
                return;
            }

            CancelPendingDraft("An existing save loaded before the authoring bootstrap completed.");
            if (GetActiveSession() != null)
                CloseActiveSession("A save loaded while scenario authoring was active.", true);
        }

        private bool IsExpectedAuthoringSaveLoaded()
        {
            ScenarioAuthoringSession pending;
            ScenarioAuthoringSession active;
            lock (_sync)
            {
                pending = _pendingSession;
                active = _activeSession;
            }

            SaveEntry activeSave = SaveRuntimeState.ActiveCustomSave;
            if (MatchesAuthoringSave(pending, activeSave) || MatchesAuthoringSave(active, activeSave))
                return true;

            return HasQueuedAuthoringStartupSave(pending);
        }

        private static bool MatchesAuthoringSave(ScenarioAuthoringSession session, SaveEntry activeSave)
        {
            if (!IsDraftAuthoringSession(session) || activeSave == null)
                return false;

            return string.Equals(activeSave.scenarioId, session.StorageScenarioId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(activeSave.id, session.StartupSaveId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasQueuedAuthoringStartupSave(ScenarioAuthoringSession session)
        {
            if (!IsDraftAuthoringSession(session))
                return false;

            PlatformSaveProxy.Target pendingTarget;
            if (!PlatformSaveProxy.TryGetNextSave(session.LaunchSaveType, out pendingTarget) || pendingTarget == null)
                return false;

            return string.Equals(pendingTarget.scenarioId, session.StorageScenarioId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pendingTarget.saveId, session.StartupSaveId, StringComparison.OrdinalIgnoreCase);
        }

        private void HandleActiveSessionBoundaries()
        {
            ScenarioAuthoringSession active = GetActiveSession();
            if (active == null)
                return;

            if (!IsDraftAuthoringSession(active))
            {
                CloseActiveSession("Active authoring session was not a draft scenario session.", true);
                return;
            }

            if (!IsEditorSessionForDraft(active))
            {
                CloseActiveSession("Scenario editor session no longer matches the active draft.", true);
                return;
            }

            ScenarioAuthoringSession pending;
            lock (_sync)
            {
                pending = _pendingSession;
            }
            bool activeStillPending = pending != null
                && string.Equals(pending.DraftId, active.DraftId, StringComparison.Ordinal);

            if (IsNormalSavePlaying(active))
            {
                if (!activeStillPending)
                {
                    CloseActiveSession("Draft scenario binding is no longer active; normal save play resumed.", true);
                    return;
                }
            }

            if (!ScenarioWorldReady.IsShelterSceneActive())
            {
                CloseActiveSession("Left the shelter scene.", false);
                return;
            }

            if (_editorService.CurrentSession == null)
                CloseActiveSession("Scenario editor session was no longer available.", true);
        }

        private void CloseActiveSession(string reason, bool resumeGame)
        {
            ScenarioAuthoringSession previous = null;
            lock (_sync)
            {
                if (_activeSession == null)
                    return;

                previous = _activeSession;
                _activeSession = null;
                _worldLoadingShellDraftId = null;
            }

            try
            {
                _editorService.CloseEditor(resumeGame);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Editor close failed for authoring session '"
                    + previous.DraftId + "': " + ex.Message);
            }
            finally
            {
                if (_inventoryProjectionService != null)
                    _inventoryProjectionService.Clear();
                _backend.ClearActiveSession(reason);
                if (_entryFlowService != null)
                    _entryFlowService.Hide("Authoring session closed.");
                ClearLaunchRedirects(previous, reason);
                SaveRuntimeState.ClearActiveCustomSession();
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared authoring save routing for closed authoring session. reason="
                    + (reason ?? "unspecified") + ".");
            }

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Closed active authoring session '" + previous.DraftId
                + "'. Reason=" + (reason ?? "unspecified") + ", resumeGame=" + resumeGame
                + ", scene=" + SceneManager.GetActiveScene().name + ".");
        }

        private bool CloseActiveSessionToMainMenu(string reason)
        {
            ScenarioAuthoringSession active = GetActiveSession();
            if (!IsEditingDraftSession(active))
                return false;

            if (!CommitActiveDraftForClose(active))
            {
                _backend.SetStatusMessage("Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.");
                return false;
            }

            CloseActiveSession(reason, false);
            ReturnToMainMenu();
            return true;
        }

        private bool CloseRuntimeStateToMainMenu(string reason, ScenarioAuthoringState state)
        {
            string scenarioFilePath = state != null ? state.ActiveScenarioFilePath : null;
            if (!CommitActiveDraftForClose(scenarioFilePath))
            {
                _backend.SetStatusMessage("Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.");
                return false;
            }

            CloseRuntimeState(reason);
            ReturnToMainMenu();
            return true;
        }

        private void CloseRuntimeState(string reason)
        {
            try
            {
                _editorService.CloseEditor(false);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Runtime-state editor close failed: " + ex.Message);
            }
            finally
            {
                _backend.ClearActiveSession(reason);
                SaveRuntimeState.ClearActiveCustomSession();
            }

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Closed authoring runtime from backend/editor state. Reason="
                + (reason ?? "unspecified") + ", scene=" + SceneManager.GetActiveScene().name + ".");
        }

        private bool CommitActiveDraftForClose(ScenarioAuthoringSession active)
        {
            if (active == null)
                return false;

            return CommitActiveDraftForClose(active.ScenarioFilePath);
        }

        private bool CommitActiveDraftForClose(string scenarioFilePath)
        {
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(scenarioFilePath);
                if (validation != null && !validation.IsValid)
                    MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Close-to-menu saved draft with validation errors.");
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Close-to-menu save failed: " + ex.Message);
                return false;
            }
        }

        private static bool HasUnsavedDraftChanges(ScenarioEditorSession session)
        {
            return session != null
                && session.DirtyFlags != null
                && session.DirtyFlags.Count > 0;
        }

        private static void ReturnToMainMenu()
        {
            try
            {
                if (LoadingScreen.Instance != null)
                {
                    PauseManager.Resume();
                    ScenarioLoadingTransitionGuard.PrepareForManagedTransition("MenuScene after authoring close");
                    LoadingScreen.Instance.ShowLoadingScreen("MenuScene");
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Requested MenuScene through LoadingScreen after editor close.");
                    return;
                }

                if (LoadingTransitionRuntime.TryReturnToMainMenu())
                    return;

                SceneManager.LoadScene("MenuScene");
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Requested MenuScene through SceneManager after editor close.");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Failed to return to main menu after editor close: " + ex.Message);
            }
        }

        private void CleanupPendingDraftArtifacts(ScenarioAuthoringSession pending, string reason)
        {
            if (pending == null || string.IsNullOrEmpty(pending.DraftId))
                return;

            bool deleted = _draftRepository.DeleteDraft(pending.DraftId, reason);
            if (!deleted && !string.IsNullOrEmpty(pending.StorageScenarioId) && !string.IsNullOrEmpty(pending.StartupSaveId))
            {
                bool saveDeleted = _saveLibrary.Delete(pending.StorageScenarioId, pending.StartupSaveId);
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Fallback draft save cleanup. draftId=" + pending.DraftId
                    + " startupSaveId=" + pending.StartupSaveId + " deleted=" + saveDeleted + ".");
            }
        }

        private ScenarioAuthoringSession GetActiveSession()
        {
            lock (_sync)
            {
                return _activeSession;
            }
        }

        private bool HasPendingOrActiveDraftSession()
        {
            lock (_sync)
            {
                return IsDraftAuthoringSession(_pendingSession) || IsDraftAuthoringSession(_activeSession);
            }
        }

        private bool IsEditingDraftSession(ScenarioAuthoringSession session)
        {
            if (!IsDraftAuthoringSession(session) || !IsEditorSessionForDraft(session))
                return false;

            ScenarioAuthoringState state = _backend.CurrentState;
            return state != null
                && state.IsActive
                && (state.WorldLoading || !IsNormalSavePlaying(session))
                && string.Equals(state.ActiveDraftId, session.DraftId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDraftAuthoringSession(ScenarioAuthoringSession session)
        {
            return session != null
                && !string.IsNullOrEmpty(session.DraftId)
                && string.Equals(
                    session.StorageScenarioId,
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(session.ScenarioFilePath);
        }

        private bool IsEditorSessionForDraft(ScenarioAuthoringSession session)
        {
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            if (session == null || editorSession == null || editorSession.WorkingDefinition == null)
                return false;

            return string.Equals(editorSession.WorkingDefinition.Id, session.DraftId, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsNormalSavePlaying(ScenarioAuthoringSession session)
        {
            ScenarioRuntimeBinding binding = _runtimeBindingService != null ? _runtimeBindingService.CurrentBinding : null;
            if (session == null || binding == null || binding.IsConvertedToNormalSave || !binding.IsActive)
                return true;

            return !string.Equals(binding.ScenarioId, session.DraftId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCloseableRuntimeState(ScenarioAuthoringState state, ScenarioEditorSession editorSession)
        {
            if (state == null || !state.IsActive || state.ReloadPending || editorSession == null || editorSession.WorkingDefinition == null)
                return false;

            if (string.IsNullOrEmpty(state.ActiveDraftId))
                return true;

            return string.Equals(state.ActiveDraftId, editorSession.WorkingDefinition.Id, StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureInactiveAuthoringState(string reason)
        {
            ScenarioAuthoringState state = _backend.CurrentState;
            bool hasEditorSession = _editorService.CurrentSession != null;
            bool hasBackendState = state != null && state.IsActive;
            if (!hasEditorSession && !hasBackendState)
            {
                _presentation.Update();
                return;
            }

            if (hasEditorSession)
            {
                try { _editorService.CloseEditor(false); }
                catch (Exception ex) { MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Inactive editor cleanup failed: " + ex.Message); }
            }

            if (hasBackendState)
                _backend.ClearActiveSession(reason);

            _presentation.Update();
        }

        private void ClearLaunchRedirects(ScenarioAuthoringSession session, string reason)
        {
            SaveManager.SaveType launchSaveType = session != null ? session.LaunchSaveType : SaveManager.SaveType.Slot1;
            bool clearByMatch = session != null
                && !string.IsNullOrEmpty(session.StorageScenarioId)
                && !string.IsNullOrEmpty(session.StartupSaveId);
            bool clearedSave = clearByMatch
                ? _saveLibrary.ClearQueuedNewGameSaveIfMatches(launchSaveType, session.StorageScenarioId, session.StartupSaveId)
                : _saveLibrary.ClearQueuedNewGameSave(launchSaveType);
            bool clearedLoad = clearByMatch
                ? _saveLibrary.ClearQueuedLoadIfMatches(launchSaveType, session.StorageScenarioId, session.StartupSaveId)
                : _saveLibrary.ClearQueuedLoad(launchSaveType);
            if (clearedSave || clearedLoad)
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared pending save/load redirects. launchSaveType=" + launchSaveType
                    + ", clearedSave=" + clearedSave + ", clearedLoad=" + clearedLoad
                    + ", reason=" + (reason ?? "unspecified") + ".");
            }
        }
    }
}
