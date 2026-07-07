using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Harmony;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal sealed class ScenarioBookBrowserActionService
    {
        private readonly ScenarioBrowserPanelAdapter _adapter;
        private readonly ScenarioLaunchCoordinator _launchCoordinator;
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly ScenarioDraftMetadataEditService _draftMetadataEditService;

        public ScenarioBookBrowserActionService(
            ScenarioBrowserPanelAdapter adapter,
            ScenarioLaunchCoordinator launchCoordinator,
            IScenarioSaveLibrary saveLibrary,
            ScenarioDraftMetadataEditService draftMetadataEditService)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            if (launchCoordinator == null) throw new ArgumentNullException("launchCoordinator");
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");
            if (draftMetadataEditService == null) throw new ArgumentNullException("draftMetadataEditService");

            _adapter = adapter;
            _launchCoordinator = launchCoordinator;
            _saveLibrary = saveLibrary;
            _draftMetadataEditService = draftMetadataEditService;
        }

        public bool StartScenario(ScenarioCatalogEntry entry, out string status)
        {
            status = null;
            if (entry == null)
                return false;

            MMLog.WriteInfo("[ScenarioBookBrowser] Start requested. scenarioId=" + entry.ScenarioId
                + " storageScenarioId=" + entry.StorageScenarioId
                + " source=" + entry.Source
                + " baseMode=" + entry.BaseGameMode
                + " virtualSaveType=" + _launchCoordinator.GetVirtualSaveType(entry) + ".");

            if (entry.IsVanilla && entry.BaseGameMode == ScenarioBaseGameMode.Survival)
            {
                string vanillaError;
                if (!_launchCoordinator.LaunchVanillaScenario(_adapter, entry, out vanillaError))
                {
                    status = "Start failed: " + Safe(vanillaError, "unknown error");
                    return false;
                }

                return true;
            }

            ScenarioLaunchCoordinator.NewGamePreparation preparation;
            string prepareError;
            if (!_launchCoordinator.PrepareNewGame(entry, entry.DisplayName, out preparation, out prepareError))
            {
                status = "Start failed: " + Safe(prepareError, "unknown error");
                return false;
            }

            string commitError;
            if (!_launchCoordinator.CommitNewGame(_adapter, preparation, out commitError))
            {
                status = "Start failed: " + Safe(commitError, "unknown error");
                return false;
            }

            return true;
        }

        public bool OpenDraft(ScenarioCatalogEntry entry, out string status)
        {
            status = null;
            if (entry == null)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Open Draft requested with no selected draft.");
                return false;
            }

            MMLog.WriteInfo("[ScenarioBookBrowser] Open Draft requested. scenarioId=" + entry.ScenarioId
                + " storageScenarioId=" + entry.StorageScenarioId
                + " source=" + entry.Source
                + " baseMode=" + entry.BaseGameMode
                + " virtualSaveType=" + _launchCoordinator.GetVirtualSaveType(entry) + ".");

            SaveEntry draftStartupSave;
            string draftSaveLookupError;
            if (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(entry.ScenarioId, out draftStartupSave, out draftSaveLookupError)
                || draftStartupSave == null)
            {
                if (!string.IsNullOrEmpty(draftSaveLookupError)
                    && draftSaveLookupError.IndexOf("recovery copy could not be loaded", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    status = "This draft could not be opened: its scenario file and backup are both unreadable.";
                }
                else
                {
                    status = "Could not resolve the draft authoring save.";
                }
                MMLog.WriteWarning("[ScenarioBookBrowser] Open Draft failed. scenarioId=" + entry.ScenarioId
                    + " reason=" + status);
                return false;
            }

            SaveManager.SaveType launchSaveType = _launchCoordinator.GetVirtualSaveType(entry);
            EnsureEditorRuntime("ScenarioBookBrowser OpenDraft");
            ScenarioAuthoringSession session = ScenarioAuthoringBootstrapService.Instance.QueueExistingDraft(entry.ScenarioId, launchSaveType);
            if (session == null)
            {
                status = "Could not queue the draft for authoring.";
                MMLog.WriteWarning("[ScenarioBookBrowser] Open Draft failed. scenarioId=" + entry.ScenarioId
                    + " reason=" + status);
                return false;
            }

            string error;
            if (!_launchCoordinator.QueueAuthoringDraftLaunch(
                    _adapter,
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    draftStartupSave,
                    launchSaveType,
                    "authoring draft '" + entry.ScenarioId + "'",
                    out error))
            {
                ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Authoring launch failed.");
                status = "Draft launch failed: " + Safe(error, "unknown error");
                MMLog.WriteWarning("[ScenarioBookBrowser] Open Draft failed. scenarioId=" + entry.ScenarioId
                    + " reason=" + status);
                return false;
            }

            MMLog.WriteInfo("[ScenarioBookBrowser] Open Draft queued authoring launch. scenarioId="
                + entry.ScenarioId + " saveId=" + draftStartupSave.id + " virtualSaveType=" + launchSaveType + ".");
            return true;
        }

        public bool CreateDraft(out string status)
        {
            status = null;
            try
            {
                SaveManager.SaveType launchSaveType = SaveManager.SaveType.Slot1;
                EnsureEditorRuntime("ScenarioBookBrowser CreateDraft");
                ScenarioAuthoringSession draft = ScenarioAuthoringBootstrapService.Instance.QueueNewDraft(ScenarioBaseGameMode.Survival, launchSaveType);
                if (draft == null || string.IsNullOrEmpty(draft.StartupSaveId))
                    throw new InvalidOperationException("The draft session did not provide a startup save.");

                SaveEntry draftStartupSave = draft.StartupSave;
                if (draftStartupSave == null
                    && (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(draft.DraftId, out draftStartupSave) || draftStartupSave == null))
                {
                    throw new InvalidOperationException("Could not resolve the draft save entry.");
                }

                string error;
                if (!_launchCoordinator.QueueAuthoringDraftLaunch(
                        _adapter,
                        ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                        draftStartupSave,
                        launchSaveType,
                        "authoring draft '" + draft.DraftId + "'",
                        out error))
                {
                    throw new InvalidOperationException(error ?? "Could not open the customisation panel for the draft.");
                }

                return true;
            }
            catch (Exception ex)
            {
                _saveLibrary.ClearQueuedNewGameSave(SaveManager.SaveType.Slot1);
                ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Authoring launch failed.");
                status = "Could not create draft: " + ex.Message;
                MMLog.WriteWarning("[ScenarioBookBrowser] Failed to create scenario authoring draft: " + ex.Message);
                return false;
            }
        }

        public bool UpdateDraftMetadata(ScenarioCatalogEntry entry, string displayName, string description, out ScenarioInfo updatedInfo, out string status)
        {
            return UpdateDraftMetadata(entry, null, displayName, description, out updatedInfo, out status);
        }

        public bool UpdateDraftMetadata(ScenarioCatalogEntry entry, string draftId, string displayName, string description, out ScenarioInfo updatedInfo, out string status)
        {
            updatedInfo = null;
            status = null;
            if (entry == null || entry.Source != ScenarioCatalogSource.Draft)
            {
                status = "Select a draft scenario first.";
                return false;
            }

            string error;
            if (!string.IsNullOrEmpty(draftId) && !string.Equals(draftId, entry.ScenarioId, StringComparison.OrdinalIgnoreCase))
            {
                if (!ScenarioAuthoringDraftRepository.Instance.TryRenameDraft(entry.ScenarioId, draftId, displayName, description, out updatedInfo, out error))
                {
                    status = "Could not rename draft: " + Safe(error, "unknown error");
                    return false;
                }

                ScenarioBookBrowserDataSource.BeginSharedRefreshAsync(ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>());
                status = "Draft renamed.";
                return true;
            }

            if (!_draftMetadataEditService.TryUpdate(
                    entry.ScenarioId,
                    new ScenarioDraftMetadataUpdate
                    {
                        DisplayName = displayName,
                        Description = description
                    },
                    out updatedInfo,
                    out error))
            {
                status = "Could not save draft details: " + Safe(error, "unknown error");
                return false;
            }

            ScenarioBookBrowserDataSource.BeginSharedRefreshAsync(ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>());
            status = "Draft details saved.";
            return true;
        }

        public bool DuplicateDraft(ScenarioCatalogEntry entry, out ScenarioInfo duplicateInfo, out string status)
        {
            duplicateInfo = null;
            status = null;
            if (entry == null || entry.Source != ScenarioCatalogSource.Draft)
            {
                status = "Select a draft scenario first.";
                return false;
            }

            string error;
            if (!ScenarioAuthoringDraftRepository.Instance.TryDuplicateDraft(entry.ScenarioId, out duplicateInfo, out error))
            {
                status = "Could not duplicate draft: " + Safe(error, "unknown error");
                return false;
            }

            ScenarioBookBrowserDataSource.BeginSharedRefreshAsync(ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>());
            status = "Draft duplicated.";
            return true;
        }

        public bool DeleteDraft(ScenarioCatalogEntry entry, out string status)
        {
            status = null;
            if (entry == null || entry.Source != ScenarioCatalogSource.Draft)
            {
                status = "Select a draft scenario first.";
                return false;
            }

            _saveLibrary.ClearQueuedNewGameSave(_launchCoordinator.GetVirtualSaveType(entry));
            bool deleted = ScenarioAuthoringDraftRepository.Instance.DeleteDraft(entry.ScenarioId, "Scenario browser draft delete.");
            if (deleted)
                ScenarioBookBrowserDataSource.BeginSharedRefreshAsync(ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>());
            status = deleted ? "Draft deleted." : "Draft delete failed.";
            return deleted;
        }

        public bool ResumeRecovery(ScenarioBookRowModel row, out string status)
        {
            status = null;
            if (row == null)
            {
                status = "No recovery item is selected.";
                return false;
            }

            status = "Pending redirect left in place. Continue from the game flow that was interrupted.";
            return true;
        }

        public bool CleanupRecovery(ScenarioBookRowModel row, out string status)
        {
            status = null;
            if (row == null)
            {
                status = "No recovery item is selected.";
                return false;
            }

            if (IsLaunchFlowPending())
            {
                status = "A scenario launch is still in progress; queued redirect state was left intact.";
                return false;
            }

            bool clearedSave = _saveLibrary.ClearQueuedNewGameSaveIfMatches(
                row.RecoverySaveType,
                row.RecoveryScenarioId,
                row.RecoverySaveId);
            bool clearedLoad = _saveLibrary.ClearQueuedLoadIfMatches(
                row.RecoverySaveType,
                row.RecoveryScenarioId,
                row.RecoverySaveId);

            status = clearedSave || clearedLoad
                ? "Pending redirect cleared. No save or draft files were deleted."
                : "Pending redirect no longer matched this recovery row; queued state was left intact.";
            return true;
        }

        public bool LoadSave(ScenarioCatalogEntry entry, SaveEntry save, out string status)
        {
            status = null;
            string error;
            if (!_launchCoordinator.LoadSave(_adapter, entry, save, out error))
            {
                status = "Load failed: " + Safe(error, "unknown error");
                return false;
            }

            return true;
        }

        public bool DeleteSave(ScenarioCatalogEntry entry, SaveEntry save, out string status)
        {
            status = null;
            if (!_launchCoordinator.DeleteSave(entry, save))
            {
                status = save != null ? "Delete failed for slot " + save.absoluteSlot + "." : "Delete failed.";
                return false;
            }

            status = save != null ? "Deleted slot " + save.absoluteSlot + "." : "Deleted save.";
            return true;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }

        private static bool IsLaunchFlowPending()
        {
            try
            {
                ScenarioAuthoringBootstrapService bootstrap = ScenarioAuthoringBootstrapService.Instance;
                return bootstrap != null && bootstrap.HasPendingDraftLaunch();
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureEditorRuntime(string trigger)
        {
            ShelteredDeferredPatchTriggers.EnsureEditorRuntime(trigger);
        }
    }
}
