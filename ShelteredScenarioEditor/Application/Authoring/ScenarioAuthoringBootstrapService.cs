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
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioAuthoringBootstrapService
    {
        private const float DraftReadinessFloorSeconds = 2f;
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringBackendService _backend;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly ScenarioAuthoringMenuService _menuService;
        private readonly ScenarioAuthoringPresentationService _presentation;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioAuthoringInventoryProjectionService _inventoryProjectionService;
        private readonly ScenarioAuthoringEntryFlowService _entryFlowService;
        private readonly ScenarioAuthoringBaseModeReloadService _baseModeReloadService;
        private readonly ScenarioAuthoringSessionLifecycleService _sessionLifecycle;
        private readonly ScenarioPreviewSessionHost _previewHost;
        private readonly ScenarioAuthoringPauseService _pauseService;
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
            ScenarioAuthoringCaptureService captureService,
            ScenarioAuthoringInventoryProjectionService inventoryProjectionService,
            ScenarioAuthoringEntryFlowService entryFlowService,
            ScenarioAuthoringBaseModeReloadService baseModeReloadService,
            ScenarioAuthoringSessionLifecycleService sessionLifecycle,
            ScenarioPreviewSessionHost previewHost,
            ScenarioAuthoringPauseService pauseService)
        {
            _backend = backend;
            _draftRepository = draftRepository;
            _menuService = menuService;
            _presentation = presentation;
            _editorService = editorService;
            _captureService = captureService;
            _inventoryProjectionService = inventoryProjectionService;
            _entryFlowService = entryFlowService;
            _baseModeReloadService = baseModeReloadService;
            _sessionLifecycle = sessionLifecycle;
            _previewHost = previewHost;
            _pauseService = pauseService;
            _sessionLifecycle.Transitioned += HandleSessionTransition;
            try { ShelteredAPI.Events.ShelteredEvents.AfterLoad += HandleAfterLoad; }
            catch { }
        }

        private void HandleSessionTransition(ScenarioAuthoringSessionTransition transition)
        {
            if (transition == null || transition.Kind != ScenarioAuthoringSessionTransitionKind.PhaseChanged)
                return;
            if (transition.Phase != ScenarioAuthoringSessionPhase.ReloadPending
                && transition.Phase != ScenarioAuthoringSessionPhase.Inactive)
                return;

            lock (_sync)
            {
                _lastPendingDraftId = null;
                _lastPendingBlockingReason = null;
                _worldLoadingShellDraftId = null;
                ResetPendingWarmup();
            }
        }

        public ScenarioAuthoringSession QueueNewDraft(ScenarioBaseGameMode baseMode, SaveManager.SaveType launchSaveType)
        {
            return QueueNewDraft(baseMode, launchSaveType, false);
        }

        public ScenarioAuthoringSession QueueNewDraft(ScenarioBaseGameMode baseMode, SaveManager.SaveType launchSaveType, bool showBaselinePicker)
        {
            ScenarioAuthoringSession pending = _sessionLifecycle.Pending;
            if (pending != null)
            {
                if (pending.BaseMode == baseMode)
                {
                    if (_entryFlowService != null)
                        _entryFlowService.BeginNewDraftLaunch(pending, showBaselinePicker);
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Reusing pending draft bootstrap: " + pending.DraftId + ".");
                    return pending;
                }
                _sessionLifecycle.CancelPending("Replaced by new " + baseMode + " draft.", true);
            }

            ScenarioAuthoringDraftRepository.DraftRecord draft = _draftRepository.CreateDraft(baseMode);
            ScenarioAuthoringSession result = ScenarioAuthoringSession.Create(
                draft.Info,
                baseMode,
                ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                draft.StartupSave != null ? draft.StartupSave.id : null,
                draft.Slot,
                draft.StartupSave,
                launchSaveType);
            result.RequestStartingCastAutoPopulateAfterBootstrap();
            _sessionLifecycle.Queue(result, "Queued draft authoring bootstrap.");
            if (_entryFlowService != null)
                _entryFlowService.BeginNewDraftLaunch(result, showBaselinePicker);
            return result;
        }

        public ScenarioAuthoringSession QueueExistingDraft(string draftId, SaveManager.SaveType launchSaveType)
        {
            ScenarioAuthoringSession result = _sessionLifecycle.QueueExistingDraft(draftId, launchSaveType);
            if (result != null && _entryFlowService != null)
                _entryFlowService.BeginExistingDraftLaunch(result);
            return result;
        }

        public void CancelPendingDraft(string reason)
        {
            CancelPendingDraft(reason, true);
        }

        public void CancelPendingDraft(string reason, bool cleanupDraftArtifacts)
        {
            ScenarioAuthoringSession pending = _sessionLifecycle.Pending;
            _sessionLifecycle.CancelPending(reason, cleanupDraftArtifacts);
            if (_baseModeReloadService != null)
                _baseModeReloadService.CancelQueuedReload(pending != null ? pending.DraftId : null, reason);
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
                return;
            }

            ScenarioAuthoringState activeState = _backend.CurrentState;
            if (activeState == null || !activeState.WorldLoading)
                _editorService.MaintainAuthoringPause();
            _backend.Update();
            _presentation.Update();
            if (_inventoryProjectionService != null)
                _inventoryProjectionService.UpdateLiveTruth(_editorService.CurrentSession);
        }

        private void TryBootstrapPendingDraft()
        {
            ScenarioAuthoringSession pending = null;
            lock (_sync)
            {
                pending = _sessionLifecycle.Pending;
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
                ShelteredScenarioRuntime.TryCompleteScenarioWorldLaunch(
                    pending.ExpectedSceneName,
                    "authoring draft '" + (pending.DraftId ?? string.Empty) + "'");
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

                if (!ShelteredScenarioRuntime.IsWorldReady(out blockingReason))
                {
                    if (!string.Equals(_lastPendingBlockingReason, blockingReason, StringComparison.Ordinal))
                    {
                        _lastPendingBlockingReason = blockingReason;
                        if (_backend != null)
                            _backend.SetWorldLoadingStatus("Loading game - " + blockingReason);
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
            if (!pending.ReenterPlaytestAfterBootstrap
                && editorSession != null
                && editorSession.WorkingDefinition != null)
            {
                // The serializer restores the selected backend world into the editor model,
                // but loading a base-mode save creates a fresh live shelter. Apply only after
                // the target scene passed ShelteredScenarioRuntime.IsWorldReady and its warmup so authored rooms
                // and objects exist in the authoring world before the shell becomes interactive.
                ScenarioEditorPlaytestResult authoringApply = ScenarioEditorPlaytestResult.FromPreview(
                    _previewHost.StartOrRefresh(
                        editorSession.WorkingDefinition,
                        pending.ScenarioFilePath));
                if (authoringApply.Started)
                    editorSession.MarkAppliedToCurrentWorld();
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Materialized authored backend world for draft '"
                    + pending.DraftId + "' in authoring bootstrap. BunkerChanges="
                    + authoringApply.BunkerChanges + ", started=" + authoringApply.Started + ".");
            }
            bool worldLoadingShellOpen = string.Equals(_worldLoadingShellDraftId, pending.DraftId, StringComparison.Ordinal);
            lock (_sync)
            {
                _lastPendingDraftId = null;
                _lastPendingBlockingReason = null;
                _worldLoadingShellDraftId = null;
                ResetPendingWarmup();
            }

            _sessionLifecycle.CompleteActivation(pending, null);
            if (_entryFlowService != null)
                _entryFlowService.MarkEditorReady(pending, true);
            ProjectStartingInventoryAfterBootstrap(pending, editorSession);
            if (editorSession != null && !string.IsNullOrEmpty(editorSession.LoadWarning))
                _backend.SetStatusMessage(editorSession.LoadWarning);
            if (pending.ReenterPlaytestAfterBootstrap)
                ReenterPlaytest(pending);
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Activated authoring session for draft '" + pending.DraftId
                + "'. Opening authoring shell.");
            if (!worldLoadingShellOpen)
                _menuService.Open(pending, true);

            string queuedReloadMessage;
            if (_baseModeReloadService != null
                && _baseModeReloadService.TryStartQueuedReload(editorSession, pending.DraftId, out queuedReloadMessage))
            {
                if (!_sessionLifecycle.HasPendingDraftLaunch() && !string.IsNullOrEmpty(queuedReloadMessage))
                    _backend.SetStatusMessage(queuedReloadMessage);
                return;
            }
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
                _worldLoadingShellDraftId = pending.DraftId;
            }

            string status = "Loading game - waiting for the shelter world to finish loading.";
            _sessionLifecycle.BeginWorldLoading(pending, status);
            if (_entryFlowService != null)
                _entryFlowService.MarkEditorReady(pending, false);
            _menuService.Open(pending, true);
            _pauseService.ReleasePauseForRunningSimulation("World-loading shell opened before draft warmup completed.");
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

                ScenarioEditorPlaytestResult result = _editorService.BeginPlaytest();
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

            editorSession.WorkingDefinition.BaseFamilyChoice = ShelteredAPI.Scenarios.Definitions.ScenarioBaseFamilyChoices.UseBaseDefaultFamily;
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
                    _backend.SetWorldLoadingStatus("Loading game - shelter ready, completing a brief readiness floor.");
                if (_entryFlowService != null)
                    _entryFlowService.SetLoadingStatus("Status: world ready - confirming readiness before opening tools.");
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId
                    + "' world readiness signals are satisfied. Allowing a minimum "
                    + DraftReadinessFloorSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " seconds of simulation before the final readiness check and authoring pause.");
            }

            float warmupDelta = Time.deltaTime;
            if (warmupDelta <= 0f)
                warmupDelta = Time.unscaledDeltaTime;
            if (warmupDelta > 0f)
                _warmupElapsedSeconds += warmupDelta;

            if (_warmupElapsedSeconds < DraftReadinessFloorSeconds)
                return false;

            // ShelteredScenarioRuntime.IsWorldReady covers the scene transition plus the vanilla managers that
            // authoring immediately reads or mutates (map, grid, family, inventory, objects,
            // quests, interaction, and UI). Re-evaluate after the short multi-frame floor so
            // a readiness regression cannot be hidden by the elapsed-time gate.
            string blockingReason;
            if (!ShelteredScenarioRuntime.IsWorldReady(out blockingReason))
            {
                if (!string.Equals(_lastPendingBlockingReason, blockingReason, StringComparison.Ordinal))
                {
                    _lastPendingBlockingReason = blockingReason;
                    if (_backend != null)
                        _backend.SetWorldLoadingStatus("Loading game - " + blockingReason);
                    if (_entryFlowService != null)
                        _entryFlowService.SetLoadingStatus("Status: world readiness changed - " + blockingReason);
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId
                        + "' readiness changed during the brief simulation floor. Reason=" + blockingReason + ".");
                }

                return false;
            }

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId
                + "' readiness-based warmup completed after "
                + _warmupElapsedSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + " seconds of running simulation with all world-readiness signals satisfied. Loading editor session.");
            return true;
        }

        private void ResetPendingWarmup()
        {
            _warmupDraftId = null;
            _warmupElapsedSeconds = 0f;
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
                _sessionLifecycle.Close("A save loaded while scenario authoring was active.", true);
        }

        private bool IsExpectedAuthoringSaveLoaded()
        {
            ScenarioAuthoringSession pending;
            ScenarioAuthoringSession active;
            lock (_sync)
            {
                pending = _sessionLifecycle.Pending;
                active = _sessionLifecycle.Active;
            }

            SaveEntry activeSave = ShelteredSaves.GetActiveScenarioSave();
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

            return ShelteredSaves.IsScenarioNewGameQueued(
                session.LaunchSaveType,
                session.StorageScenarioId,
                session.StartupSaveId);
        }

        private void HandleActiveSessionBoundaries()
        {
            ScenarioAuthoringSession active = GetActiveSession();
            if (active == null)
                return;

            if (!IsDraftAuthoringSession(active))
            {
                _sessionLifecycle.Close("Active authoring session was not a draft scenario session.", true);
                return;
            }

            if (!IsEditorSessionForDraft(active))
            {
                _sessionLifecycle.Close("Scenario editor session no longer matches the active draft.", true);
                return;
            }

            ScenarioAuthoringSession pending;
            lock (_sync)
            {
                pending = _sessionLifecycle.Pending;
            }
            bool activeStillPending = pending != null
                && string.Equals(pending.DraftId, active.DraftId, StringComparison.Ordinal);

            if (IsNormalSavePlaying(active))
            {
                if (!activeStillPending)
                {
                    _sessionLifecycle.Close("Draft scenario binding is no longer active; normal save play resumed.", true);
                    return;
                }
            }

            if (!ShelteredScenarioRuntime.IsShelterSceneActive())
            {
                _sessionLifecycle.Close("Left the shelter scene.", false);
                return;
            }

            if (_editorService.CurrentSession == null)
                _sessionLifecycle.Close("Scenario editor session was no longer available.", true);
        }

        private ScenarioAuthoringSession GetActiveSession()
        {
            return _sessionLifecycle.Active;
        }

        private bool HasPendingOrActiveDraftSession()
        {
            return IsDraftAuthoringSession(_sessionLifecycle.Pending) || IsDraftAuthoringSession(_sessionLifecycle.Active);
        }

        private bool IsEditingDraftSession(ScenarioAuthoringSession session)
        {
            if (!IsDraftAuthoringSession(session) || !IsEditorSessionForDraft(session))
                return false;

            ScenarioAuthoringState state = _backend.CurrentState;
            return state != null
                && state.IsActive
                && (state.WorldLoading || !IsNormalSavePlaying(session))
                && ReferenceEquals(_sessionLifecycle.Active, session);
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
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            if (session == null || editorSession == null || editorSession.IsConvertedToNormalSave)
                return true;

            return editorSession.WorkingDefinition == null
                || !string.Equals(editorSession.WorkingDefinition.Id, session.DraftId, StringComparison.OrdinalIgnoreCase);
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

            _sessionLifecycle.CloseRuntimeOrphan(reason);
            _presentation.Update();
        }
    }
}
