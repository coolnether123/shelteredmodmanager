using System;

using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal enum ScenarioAuthoringSessionPhase
    {
        Inactive,
        Queued,
        WorldLoading,
        Active,
        ReloadPending,
        Closing
    }

    internal enum ScenarioAuthoringSessionTransitionKind
    {
        PhaseChanged,
        StatusChanged,
        QueuedReloadStatusChanged
    }

    internal sealed class ScenarioAuthoringSessionTransition
    {
        internal ScenarioAuthoringSessionTransition(
            ScenarioAuthoringSessionTransitionKind kind,
            ScenarioAuthoringSessionPhase previousPhase,
            ScenarioAuthoringSessionPhase phase,
            ScenarioAuthoringSession pending,
            ScenarioAuthoringSession active,
            string reason,
            string status,
            long revision)
        {
            Kind = kind;
            PreviousPhase = previousPhase;
            Phase = phase;
            Pending = pending;
            Active = active;
            Reason = reason;
            Status = status;
            Revision = revision;
        }

        internal ScenarioAuthoringSessionTransitionKind Kind { get; private set; }
        internal ScenarioAuthoringSessionPhase PreviousPhase { get; private set; }
        internal ScenarioAuthoringSessionPhase Phase { get; private set; }
        internal ScenarioAuthoringSession Pending { get; private set; }
        internal ScenarioAuthoringSession Active { get; private set; }
        internal string Reason { get; private set; }
        internal string Status { get; private set; }
        internal long Revision { get; private set; }
        internal ScenarioAuthoringSession CurrentOrPending { get { return Active ?? Pending; } }
    }

    /// <summary>
    /// Sole owner of pending/active authoring-session transitions and teardown.
    /// Shell state is a projection of immutable transition notifications.
    /// </summary>
    internal sealed class ScenarioAuthoringSessionLifecycleService
    {
        private readonly object _sync = new object();
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringDraftRepository _draftRepository;
        private readonly ScenarioAuthoringInventoryProjectionService _inventoryProjectionService;
        private ScenarioAuthoringSession _pending;
        private ScenarioAuthoringSession _active;
        private ScenarioAuthoringSessionPhase _phase;
        private long _revision;

        internal ScenarioAuthoringSessionLifecycleService(
            IScenarioEditorService editorService,
            ScenarioAuthoringDraftRepository draftRepository,
            ScenarioAuthoringInventoryProjectionService inventoryProjectionService)
        {
            if (editorService == null) throw new ArgumentNullException("editorService");
            if (draftRepository == null) throw new ArgumentNullException("draftRepository");
            _editorService = editorService;
            _draftRepository = draftRepository;
            _inventoryProjectionService = inventoryProjectionService;
            _phase = ScenarioAuthoringSessionPhase.Inactive;
        }

        internal event Action<ScenarioAuthoringSessionTransition> Transitioned;

        internal ScenarioAuthoringSession Pending { get { lock (_sync) return _pending; } }
        internal ScenarioAuthoringSession Active { get { lock (_sync) return _active; } }
        internal ScenarioAuthoringSession CurrentOrPending { get { lock (_sync) return _active ?? _pending; } }
        internal ScenarioAuthoringSessionPhase Phase { get { lock (_sync) return _phase; } }
        internal long Revision { get { lock (_sync) return _revision; } }

        internal void Queue(ScenarioAuthoringSession session, string reason)
        {
            if (session == null) throw new ArgumentNullException("session");
            PublishPhase(ScenarioAuthoringSessionPhase.Queued, session, Active, reason, reason);
        }

        internal ScenarioAuthoringSession QueueExistingDraft(string draftId, SaveManager.SaveType launchSaveType)
        {
            if (string.IsNullOrEmpty(draftId))
                return null;

            ScenarioAuthoringDraftRepository.DraftRecord draft;
            string lookupError;
            if (!_draftRepository.TryGetDraftRecord(draftId, out draft, out lookupError)
                || draft == null || draft.Info == null || draft.StartupSave == null)
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Could not resolve draft '" + draftId
                    + "': " + (lookupError ?? "indexed metadata was unavailable") + ".");
                return null;
            }

            ScenarioBaseGameMode baseMode = ResolveDraftBaseMode(draft.Info);
            ScenarioAuthoringSession existing = Pending;
            if (existing != null
                && string.Equals(existing.DraftId, draftId, StringComparison.OrdinalIgnoreCase)
                && existing.BaseMode == baseMode
                && existing.LaunchSaveType == launchSaveType
                && string.Equals(existing.StartupSaveId, draft.StartupSave.id, StringComparison.Ordinal)
                && existing.StartupSaveSlot == draft.StartupSave.absoluteSlot)
            {
                return existing;
            }

            if (existing != null)
            {
                MMLog.WriteInfo("[ScenarioAuthoringLifecycle] Replacing pending draft bootstrap because the launch identity changed. "
                    + "staleDraft=" + existing.DraftId
                    + ", staleBase=" + existing.BaseMode
                    + ", staleSaveType=" + existing.LaunchSaveType
                    + ", staleStartupSave=" + existing.StartupSaveId
                    + ", staleSlot=" + existing.StartupSaveSlot
                    + "; replacementDraft=" + draftId
                    + ", replacementBase=" + baseMode
                    + ", replacementSaveType=" + launchSaveType
                    + ", replacementStartupSave=" + draft.StartupSave.id
                    + ", replacementSlot=" + draft.StartupSave.absoluteSlot + ".");
            }

            ScenarioAuthoringSession session = ScenarioAuthoringSession.Create(
                draft.Info,
                baseMode,
                ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                draft.StartupSave.id,
                draft.StartupSave.absoluteSlot,
                draft.StartupSave,
                launchSaveType);
            Queue(session, "Queued existing draft authoring bootstrap.");
            return session;
        }

        internal ScenarioAuthoringSession QueueCurrentDraftReload(
            string draftId,
            ScenarioBaseGameMode baseMode,
            SaveManager.SaveType launchSaveType)
        {
            ScenarioAuthoringSession current = CurrentOrPending;
            if (!IsDraftAuthoringSession(current)
                || !string.Equals(current.DraftId, draftId, StringComparison.OrdinalIgnoreCase))
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Could not queue reload because the current lifecycle identity did not match draft '"
                    + (draftId ?? "<none>") + "'.");
                return null;
            }

            ScenarioAuthoringSession reload = current.CreateReloadSession(baseMode, launchSaveType);
            Queue(reload, "Queued current draft authoring reload.");
            return reload;
        }

        internal void BeginWorldLoading(ScenarioAuthoringSession session, string status)
        {
            if (session == null) return;
            PublishPhase(ScenarioAuthoringSessionPhase.WorldLoading, session, null, status, status);
        }

        internal void CompleteActivation(ScenarioAuthoringSession session, string status)
        {
            if (session == null) return;
            ClearLaunchRedirects(session, "Authoring bootstrap completed.");
            PublishPhase(ScenarioAuthoringSessionPhase.Active, null, session, status, status);
        }

        internal void BeginReload(ScenarioAuthoringSession session, string reason)
        {
            if (session == null) return;
            PublishPhase(ScenarioAuthoringSessionPhase.ReloadPending, session, null, reason, reason);
            try
            {
                _editorService.CloseEditor(false);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Editor close failed while preparing authoring reload: " + ex.Message);
            }
            ShelteredSaves.ClearActiveScenarioSession();
        }

        internal void SetStatus(string status)
        {
            ScenarioAuthoringSessionTransition transition;
            lock (_sync)
            {
                transition = NewTransition(ScenarioAuthoringSessionTransitionKind.StatusChanged, _phase, _phase, status, status);
            }
            Publish(transition);
        }

        internal void SetQueuedReloadStatus(string status)
        {
            ScenarioAuthoringSessionTransition transition;
            lock (_sync)
                transition = NewTransition(ScenarioAuthoringSessionTransitionKind.QueuedReloadStatusChanged, _phase, _phase, status, status);
            Publish(transition);
        }

        internal bool HasPendingDraftLaunch()
        {
            return IsDraftAuthoringSession(Pending);
        }

        internal bool IsEditingDraftActive()
        {
            ScenarioAuthoringSession active = Active;
            return IsDraftAuthoringSession(active) && IsEditorSessionForDraft(active) && Phase == ScenarioAuthoringSessionPhase.Active;
        }

        internal void CancelPending(string reason, bool cleanupDraftArtifacts)
        {
            ScenarioAuthoringSession pending;
            lock (_sync)
            {
                pending = _pending;
                if (pending == null)
                    return;
            }

            ClearLaunchRedirects(pending, reason);
            ModRandomBridge.SetScenarioFixedSeedActive(false);
            if (cleanupDraftArtifacts)
                CleanupDraftArtifacts(pending, reason);
            PublishPhase(ScenarioAuthoringSessionPhase.Inactive, null, null, reason, reason);
        }

        internal bool CancelUncommittedWizardDraft(string draftId, string reason, out string message)
        {
            ScenarioAuthoringSession pending = Pending;
            ScenarioAuthoringSession active = Active;
            if (!string.IsNullOrEmpty(draftId) && IsDraftAuthoringSession(pending)
                && string.Equals(pending.DraftId, draftId, StringComparison.Ordinal))
            {
                CancelPending(reason, true);
                message = "Canceled the scenario setup and discarded its draft.";
                return true;
            }

            if (!string.IsNullOrEmpty(draftId) && IsDraftAuthoringSession(active)
                && string.Equals(active.DraftId, draftId, StringComparison.Ordinal))
            {
                Close(reason, false);
                CleanupDraftArtifacts(active, reason);
                ReturnToMainMenu();
                message = "Canceled the scenario setup and discarded its draft.";
                return true;
            }

            message = "Scenario setup is already closed.";
            return true;
        }

        internal bool RequestCloseToMainMenu(ScenarioAuthoringState shellState, string reason, out string message)
        {
            ScenarioAuthoringSession active = Active;
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            ScenarioAuthoringSessionPhase phase = Phase;
            long revision = Revision;
            bool closeableActive = IsDraftAuthoringSession(active) && IsEditorSessionForDraft(active);
            bool closeableRecovery = !closeableActive
                && shellState != null && shellState.IsActive && !shellState.ReloadPending
                && editorSession != null && editorSession.WorkingDefinition != null;

            if (!closeableActive && !closeableRecovery)
            {
                message = phase == ScenarioAuthoringSessionPhase.ReloadPending
                    ? "Scenario editor is restarting; close is disabled until the reload completes."
                    : "Scenario editor is already closed.";
                return true;
            }

            string draftId = closeableActive ? active.DraftId : editorSession.WorkingDefinition.Id;
            if (HasUnsavedDraftChanges(editorSession))
            {
                MessageBox.Show(MessageBoxButtons.YesNo_Buttons, "UI.Save", new MessageBoxResponse(delegate(int response)
                {
                    if (!MatchesCloseRequest(draftId, revision, closeableRecovery))
                    {
                        SetStatus("Exit canceled because the active draft changed while confirmation was open.");
                        return;
                    }

                    if (response == 1 && !CommitActiveDraftForClose(closeableActive ? active.ScenarioFilePath : null))
                    {
                        SetStatus("Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.");
                        return;
                    }

                    Close(reason ?? (response == 1 ? "Closed from authoring shell." : "Discarded unsaved authoring changes from shell."), false);
                    ReturnToMainMenu();
                }));
                message = "Close requested; confirm saving the draft before returning to the main menu.";
                return true;
            }

            if (!CommitActiveDraftForClose(closeableActive ? active.ScenarioFilePath : null))
            {
                SetStatus("Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.");
                message = "Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.";
                return true;
            }

            Close(reason ?? "Closed from authoring shell.", false);
            ReturnToMainMenu();
            message = "Closed from authoring shell and returning to the main menu.";
            return true;
        }

        internal void PrepareForVanillaShutdown(string reason)
        {
            ScenarioAuthoringSession active = Active;
            if (!IsDraftAuthoringSession(active))
                return;
            try { CommitActiveDraftForClose(active.ScenarioFilePath); }
            catch { }
            Close(reason ?? "Vanilla Save & Exit confirmed.", true);
        }

        internal void Close(string reason, bool resumeGame)
        {
            ScenarioAuthoringSession previous = Active ?? Pending;
            if (previous == null && _editorService.CurrentSession == null && Phase == ScenarioAuthoringSessionPhase.Inactive)
                return;

            PublishPhase(ScenarioAuthoringSessionPhase.Closing, null, previous, reason, reason);
            try
            {
                _editorService.CloseEditor(resumeGame);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Editor close failed: " + ex.Message);
            }
            finally
            {
                if (_inventoryProjectionService != null)
                    _inventoryProjectionService.Clear();
                ClearLaunchRedirects(previous, reason);
                ShelteredSaves.ClearActiveScenarioSession();
                ModRandomBridge.SetScenarioFixedSeedActive(false);
                PublishPhase(ScenarioAuthoringSessionPhase.Inactive, null, null, reason, reason);
            }

            MMLog.WriteInfo("[ScenarioAuthoringLifecycle] Closed session '" + (previous != null ? previous.DraftId : "<orphan>")
                + "'. reason=" + (reason ?? "unspecified") + " resumeGame=" + resumeGame
                + " scene=" + SceneManager.GetActiveScene().name + ".");
        }

        internal void CloseRuntimeOrphan(string reason)
        {
            PublishPhase(ScenarioAuthoringSessionPhase.Closing, null, null, reason, reason);
            try
            {
                _editorService.CloseEditor(false);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Orphan editor close failed: " + ex.Message);
            }
            finally
            {
                if (_inventoryProjectionService != null)
                    _inventoryProjectionService.Clear();
                ShelteredSaves.ClearActiveScenarioSession();
                ModRandomBridge.SetScenarioFixedSeedActive(false);
                PublishPhase(ScenarioAuthoringSessionPhase.Inactive, null, null, reason, reason);
            }
        }

        private void PublishPhase(ScenarioAuthoringSessionPhase phase, ScenarioAuthoringSession pending, ScenarioAuthoringSession active, string reason, string status)
        {
            ScenarioAuthoringSessionTransition transition;
            lock (_sync)
            {
                ScenarioAuthoringSessionPhase previous = _phase;
                _pending = pending;
                _active = active;
                _phase = phase;
                transition = NewTransition(ScenarioAuthoringSessionTransitionKind.PhaseChanged, previous, phase, reason, status);
            }
            Publish(transition);
        }

        private ScenarioAuthoringSessionTransition NewTransition(ScenarioAuthoringSessionTransitionKind kind, ScenarioAuthoringSessionPhase previous, ScenarioAuthoringSessionPhase phase, string reason, string status)
        {
            _revision++;
            return new ScenarioAuthoringSessionTransition(kind, previous, phase, _pending, _active, reason, status, _revision);
        }

        private void Publish(ScenarioAuthoringSessionTransition transition)
        {
            Action<ScenarioAuthoringSessionTransition> handler = Transitioned;
            if (handler == null)
                return;

            Delegate[] subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<ScenarioAuthoringSessionTransition>)subscribers[i])(transition);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Transition subscriber failed: " + ex.Message);
                }
            }
        }

        private bool MatchesCloseRequest(string draftId, long revision, bool recovery)
        {
            lock (_sync)
            {
                if (_revision != revision)
                    return false;
                ScenarioAuthoringSession current = _active;
                if (recovery)
                    return current == null && _editorService.CurrentSession != null
                        && _editorService.CurrentSession.WorkingDefinition != null
                        && string.Equals(_editorService.CurrentSession.WorkingDefinition.Id, draftId, StringComparison.OrdinalIgnoreCase);
                return current != null && string.Equals(current.DraftId, draftId, StringComparison.OrdinalIgnoreCase);
            }
        }

        private bool CommitActiveDraftForClose(string scenarioFilePath)
        {
            try
            {
                ScenarioValidationResult validation = _editorService.CommitChanges(scenarioFilePath);
                if (validation != null && !validation.IsValid)
                    MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Close-to-menu saved draft with validation errors.");
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Close-to-menu save failed: " + ex.Message);
                return false;
            }
        }

        private static bool HasUnsavedDraftChanges(ScenarioEditorSession session)
        {
            return session != null && session.DirtyFlags != null && session.DirtyFlags.Count > 0;
        }

        private bool IsEditorSessionForDraft(ScenarioAuthoringSession session)
        {
            ScenarioEditorSession editorSession = _editorService.CurrentSession;
            return session != null && editorSession != null && editorSession.WorkingDefinition != null
                && string.Equals(editorSession.WorkingDefinition.Id, session.DraftId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDraftAuthoringSession(ScenarioAuthoringSession session)
        {
            return session != null && !string.IsNullOrEmpty(session.DraftId)
                && string.Equals(session.StorageScenarioId, ScenarioAuthoringDraftRepository.DraftStorageScenarioId, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(session.ScenarioFilePath);
        }

        private ScenarioBaseGameMode ResolveDraftBaseMode(ScenarioInfo draftInfo)
        {
            if (draftInfo == null || string.IsNullOrEmpty(draftInfo.FilePath))
                return ScenarioBaseGameMode.Survival;
            try
            {
                ScenarioDefinition definition = new ScenarioEditorDefinitionSerializer().Load(draftInfo.FilePath);
                if (definition != null && Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                    return definition.BaseGameMode;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Could not resolve base mode for '" + draftInfo.Id + "': " + ex.Message);
            }
            return ScenarioBaseGameMode.Survival;
        }

        private void CleanupDraftArtifacts(ScenarioAuthoringSession session, string reason)
        {
            if (session == null || string.IsNullOrEmpty(session.DraftId))
                return;
            bool deleted = _draftRepository.DeleteDraft(session.DraftId, reason);
            if (!deleted && !string.IsNullOrEmpty(session.StorageScenarioId) && !string.IsNullOrEmpty(session.StartupSaveId))
                ShelteredSaves.DeleteScenario(session.StorageScenarioId, session.StartupSaveId);
        }

        private static void ClearLaunchRedirects(ScenarioAuthoringSession session, string reason)
        {
            SaveManager.SaveType launchSaveType = session != null ? session.LaunchSaveType : SaveManager.SaveType.Slot1;
            bool match = session != null && !string.IsNullOrEmpty(session.StorageScenarioId) && !string.IsNullOrEmpty(session.StartupSaveId);
            if (match)
            {
                ShelteredSaves.ClearQueuedScenarioNewGame(launchSaveType, session.StorageScenarioId, session.StartupSaveId);
                ShelteredSaves.ClearQueuedScenarioLoad(launchSaveType, session.StorageScenarioId, session.StartupSaveId);
            }
            else
            {
                ShelteredSaves.ClearQueuedScenarioNewGame(launchSaveType);
                ShelteredSaves.ClearQueuedScenarioLoad(launchSaveType);
            }
        }

        private static void ReturnToMainMenu()
        {
            string message;
            if (!ShelteredScenarioRuntime.TryReturnToMainMenu(out message))
                MMLog.WriteWarning("[ScenarioAuthoringLifecycle] Failed to return to main menu: " + (message ?? "unknown transition error") + ".");
        }
    }
}
