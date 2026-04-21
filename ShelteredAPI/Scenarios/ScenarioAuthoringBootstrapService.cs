using System;
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Saves;
using ModAPI.Scenarios;
using UnityEngine.SceneManagement;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringBootstrapService
    {
        private static readonly ScenarioAuthoringBootstrapService _instance = new ScenarioAuthoringBootstrapService();
        private readonly object _sync = new object();
        private readonly ScenarioAuthoringBackendService _backend = ScenarioAuthoringBackendService.Instance;
        private readonly ScenarioAuthoringDraftRepository _draftRepository = ScenarioAuthoringDraftRepository.Instance;
        private readonly ScenarioAuthoringMenuService _menuService = ScenarioAuthoringMenuService.Instance;
        private readonly ScenarioAuthoringPresentationService _presentation = ScenarioAuthoringPresentationService.Instance;
        private ScenarioAuthoringSession _pendingSession;
        private ScenarioAuthoringSession _activeSession;
        private string _lastPendingDraftId;
        private string _lastPendingBlockingReason;

        public static ScenarioAuthoringBootstrapService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringBootstrapService()
        {
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

                    // Stale draft with a different base mode — replace it. Cleanup happens
                    // after releasing the lock so file I/O doesn't block other callers.
                    obsolete = _pendingSession;
                    _pendingSession = null;
                    _lastPendingDraftId = null;
                    _lastPendingBlockingReason = null;
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
                    launchSaveType);
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Queued draft authoring bootstrap: " + _pendingSession.DraftId + ".");
                result = _pendingSession;
            }

            if (obsolete != null)
                CleanupPendingDraftArtifacts(obsolete, "Replaced by new " + baseMode + " draft.");

            return result;
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
            }

            ClearLaunchRedirects(pending, reason);
            if (pending != null)
                CleanupPendingDraftArtifacts(pending, reason);
        }

        public void RequestCloseActiveSession(string reason, bool resumeGame)
        {
            CloseActiveSession(reason ?? "Closed from authoring shell.", resumeGame);
        }

        public void Update()
        {
            HandleActiveSessionBoundaries();
            TryBootstrapPendingDraft();
            ScenarioEditorController.Instance.MaintainAuthoringPause();
            _backend.Update();
            _presentation.Update();
            _menuService.Update(GetActiveSession());
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
                editorSession = ScenarioEditorController.Instance.LoadEditMode(pending.ScenarioFilePath);
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
            }

            _backend.SetActiveSession(pending);
            MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Activated authoring session for draft '" + pending.DraftId
                + "'. Opening authoring shell.");
            _menuService.Open(pending, true);
        }

        private static void ActivateScenarioBinding(ScenarioAuthoringSession session)
        {
            if (session == null)
                return;

            ScenarioEditorSession editorSession = ScenarioEditorController.Instance.CurrentSession;
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
        }

        private void HandleActiveSessionBoundaries()
        {
            ScenarioAuthoringSession active = GetActiveSession();
            if (active == null)
                return;

            if (!ScenarioWorldReady.IsShelterSceneActive())
            {
                CloseActiveSession("Left the shelter scene.", false);
                return;
            }

            if (ScenarioEditorController.Instance.CurrentSession == null)
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

            ScenarioEditorController.Instance.CloseEditor(resumeGame);
            _backend.ClearActiveSession(reason);
            ClearLaunchRedirects(previous, reason);
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
                bool saveDeleted = ModAPI.Saves.ScenarioSaves.Delete(pending.StorageScenarioId, pending.StartupSaveId);
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

        private static void ClearLaunchRedirects(ScenarioAuthoringSession session, string reason)
        {
            SaveManager.SaveType launchSaveType = session != null ? session.LaunchSaveType : SaveManager.SaveType.Slot1;
            bool clearedSave = PlatformSaveProxy.ClearNextSave(launchSaveType);
            bool clearedLoad = PlatformSaveProxy.ClearNextLoad(launchSaveType);
            if (clearedSave || clearedLoad)
            {
                MMLog.WriteInfo("[ScenarioAuthoringBootstrap] Cleared pending save/load redirects. launchSaveType=" + launchSaveType
                    + ", clearedSave=" + clearedSave + ", clearedLoad=" + clearedLoad
                    + ", reason=" + (reason ?? "unspecified") + ".");
            }
        }
    }
}
