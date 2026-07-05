using System;
using ModAPI.Core;
using ShelteredAPI.Events;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

using ShelteredAPI.Content;
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
        private ScenarioAuthoringSession _pendingSession;
        private ScenarioAuthoringSession _activeSession;
        private string _lastPendingDraftId;
        private string _lastPendingBlockingReason;
        private string _warmupDraftId;
        private float _warmupElapsedSeconds;

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
            IScenarioRuntimeBindingService runtimeBindingService)
        {
            _backend = backend;
            _draftRepository = draftRepository;
            _menuService = menuService;
            _presentation = presentation;
            _editorService = editorService;
            _saveLibrary = saveLibrary;
            _runtimeBindingService = runtimeBindingService;
            try { GameEvents.OnAfterLoad += HandleAfterLoad; }
            catch { }
        }

        public ScenarioAuthoringSession QueueNewDraft(ScenarioBaseGameMode baseMode, SaveManager.SaveType launchSaveType)
        {
            ScenarioAuthoringSession obsolete = null;
            ScenarioAuthoringSession result;
            lock (_sync)
            {
                if (_pendingSession != null)
                {
                    if (_pendingSession.BaseMode == baseMode)
                    {
                        MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Reusing pending draft bootstrap: " + _pendingSession.DraftId + ".");
                        return _pendingSession;
                    }

                    // Stale draft with a different base mode - replace it. Cleanup happens
                    // after releasing the lock so file I/O doesn't block other callers.
                    obsolete = _pendingSession;
                    _pendingSession = null;
                    _lastPendingDraftId = null;
                    _lastPendingBlockingReason = null;
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
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Queued draft authoring bootstrap: " + _pendingSession.DraftId + ".");
                result = _pendingSession;
            }

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
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Queued existing draft authoring bootstrap: " + _pendingSession.DraftId + ".");
                return _pendingSession;
            }
        }

        public void CancelPendingDraft(string reason)
        {
            ScenarioAuthoringSession pending = null;
            lock (_sync)
            {
                if (_pendingSession == null)
                    return;

                pending = _pendingSession;
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared pending draft bootstrap '" + _pendingSession.DraftId
                    + "'. Reason=" + (reason ?? "unspecified") + ".");
                _pendingSession = null;
                _lastPendingDraftId = null;
                _lastPendingBlockingReason = null;
                ResetPendingWarmup();
            }

            ClearLaunchRedirects(pending, reason);
            if (pending != null)
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

        public void PrepareActiveSessionForVanillaShutdown(string reason)
        {
            ScenarioAuthoringSession active = GetActiveSession();
            if (!IsEditingDraftSession(active))
                return;

            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(active.ScenarioFilePath);
                if (validation != null && validation.IsValid)
                {
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Saved active draft before vanilla shutdown. draftId="
                        + active.DraftId + ".");
                }
                else
                {
                    MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Active draft save before vanilla shutdown was blocked by validation.");
                }
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

            _editorService.MaintainAuthoringPause();
            _backend.Update();
            _presentation.Update();
            _menuService.Update(active);
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
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Pending draft '" + pending.DraftId + "' waiting to bootstrap. BaseMode="
                    + pending.BaseMode + ", ScenarioFile=" + pending.ScenarioFilePath + ".");
            }

            string blockingReason;
            if (!ScenarioWorldReady.Evaluate(out blockingReason))
            {
                if (!string.Equals(_lastPendingBlockingReason, blockingReason, StringComparison.Ordinal))
                {
                    _lastPendingBlockingReason = blockingReason;
                    MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId + "' is waiting for world readiness. Reason="
                        + blockingReason + ".");
                }

                return;
            }

            if (!TryCompleteDraftWarmup(pending))
                return;

            if (!string.IsNullOrEmpty(_lastPendingBlockingReason))
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] World readiness satisfied for draft '" + pending.DraftId
                    + "'. Continuing authoring bootstrap.");
                _lastPendingBlockingReason = null;
            }

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Loading editor session for draft '" + pending.DraftId
                + "' from " + pending.ScenarioFilePath + ".");
            ScenarioEditorSession editorSession;
            try
            {
                editorSession = _editorService.LoadEditMode(pending.ScenarioFilePath);
            }
            catch (Exception ex)
            {
                // A corrupt or missing draft file must not bubble up through Update() and crash
                // the game. Cancel the draft so the player can try again cleanly.
                MMLog.WriteWarning("[ScenarioAuthoringBootstrap] Editor session load failed for draft '"
                    + pending.DraftId + "': " + ex.Message);
                CancelPendingDraft("Editor session failed to load: " + ex.Message);
                return;
            }
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Editor session loaded for draft '" + pending.DraftId + "'. DefinitionId="
                + (editorSession != null && editorSession.WorkingDefinition != null ? editorSession.WorkingDefinition.Id : "<null>") + ".");
            ActivateScenarioBinding(pending);
            ClearLaunchRedirects(pending, "Authoring bootstrap completed.");
            lock (_sync)
            {
                _activeSession = pending;
                _pendingSession = null;
                _lastPendingDraftId = null;
                _lastPendingBlockingReason = null;
                ResetPendingWarmup();
            }

            _backend.SetActiveSession(pending);
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Activated authoring session for draft '" + pending.DraftId
                + "'. Opening authoring shell.");
            _menuService.Open(pending, true);
        }

        private bool TryCompleteDraftWarmup(ScenarioAuthoringSession pending)
        {
            if (pending == null)
                return false;

            if (!string.Equals(_warmupDraftId, pending.DraftId, StringComparison.Ordinal))
            {
                _warmupDraftId = pending.DraftId;
                _warmupElapsedSeconds = 0f;
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Draft '" + pending.DraftId
                    + "' world is ready. Letting the shelter run for "
                    + DraftWarmupSeconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + " seconds before authoring pause.");
            }

            if (!PauseManager.isPaused && Time.timeScale > 0f)
                _warmupElapsedSeconds += Time.deltaTime;

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
            CancelPendingDraft("An existing save loaded before the authoring bootstrap completed.");
            if (GetActiveSession() != null)
                CloseActiveSession("A save loaded while scenario authoring was active.", true);
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

            if (IsNormalSavePlaying(active))
            {
                CloseActiveSession("Draft scenario binding is no longer active; normal save play resumed.", true);
                return;
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
                _backend.ClearActiveSession(reason);
                ClearLaunchRedirects(previous, reason);
                SaveRuntimeState.ClearActiveCustomSession();
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared authoring save routing for closed authoring session. reason="
                    + (reason ?? "unspecified") + ".");
            }

            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Closed active authoring session '" + previous.DraftId
                + "'. Reason=" + (reason ?? "unspecified") + ", resumeGame=" + resumeGame
                + ", scene=" + SceneManager.GetActiveScene().name + ".");
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
            if (!IsDraftAuthoringSession(session) || !IsEditorSessionForDraft(session) || IsNormalSavePlaying(session))
                return false;

            ScenarioAuthoringState state = _backend.CurrentState;
            return state != null
                && state.IsActive
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
            bool clearedSave = _saveLibrary.ClearQueuedNewGameSave(launchSaveType);
            bool clearedLoad = _saveLibrary.ClearQueuedLoad(launchSaveType);
            if (clearedSave || clearedLoad)
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared pending save/load redirects. launchSaveType=" + launchSaveType
                    + ", clearedSave=" + clearedSave + ", clearedLoad=" + clearedLoad
                    + ", reason=" + (reason ?? "unspecified") + ".");
            }
        }
    }
}
