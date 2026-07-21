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
        private readonly Func<ScenarioLaunchCoordinator> _launchCoordinatorFactory;
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly Func<ScenarioDraftMetadataEditService> _draftMetadataEditServiceFactory;
        private readonly Func<ScenarioPackageImportService> _importServiceFactory;
        private readonly object _dependencySync = new object();
        private ScenarioLaunchCoordinator _launchCoordinator;
        private ScenarioDraftMetadataEditService _draftMetadataEditService;
        private ScenarioPackageImportService _importService;

        public ScenarioBookBrowserActionService(
            ScenarioBrowserPanelAdapter adapter,
            ScenarioLaunchCoordinator launchCoordinator,
            IScenarioSaveLibrary saveLibrary,
            ScenarioDraftMetadataEditService draftMetadataEditService,
            ScenarioPackageImportService importService)
            : this(
                adapter,
                delegate { return launchCoordinator; },
                saveLibrary,
                delegate { return draftMetadataEditService; },
                delegate { return importService; })
        {
            if (launchCoordinator == null) throw new ArgumentNullException("launchCoordinator");
            if (draftMetadataEditService == null) throw new ArgumentNullException("draftMetadataEditService");
            if (importService == null) throw new ArgumentNullException("importService");
        }

        internal ScenarioBookBrowserActionService(
            ScenarioBrowserPanelAdapter adapter,
            Func<ScenarioLaunchCoordinator> launchCoordinatorFactory,
            IScenarioSaveLibrary saveLibrary,
            Func<ScenarioDraftMetadataEditService> draftMetadataEditServiceFactory,
            Func<ScenarioPackageImportService> importServiceFactory)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            if (launchCoordinatorFactory == null) throw new ArgumentNullException("launchCoordinatorFactory");
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");
            if (draftMetadataEditServiceFactory == null) throw new ArgumentNullException("draftMetadataEditServiceFactory");
            if (importServiceFactory == null) throw new ArgumentNullException("importServiceFactory");

            _adapter = adapter;
            _launchCoordinatorFactory = launchCoordinatorFactory;
            _saveLibrary = saveLibrary;
            _draftMetadataEditServiceFactory = draftMetadataEditServiceFactory;
            _importServiceFactory = importServiceFactory;
        }

        private ScenarioLaunchCoordinator LaunchCoordinator
        {
            get
            {
                return ResolveDeferred(ref _launchCoordinator, _launchCoordinatorFactory, "scenario launch coordinator");
            }
        }

        private ScenarioDraftMetadataEditService DraftMetadataEditService
        {
            get
            {
                return ResolveDeferred(ref _draftMetadataEditService, _draftMetadataEditServiceFactory, "draft metadata edit service");
            }
        }

        private ScenarioPackageImportService ImportService
        {
            get
            {
                return ResolveDeferred(ref _importService, _importServiceFactory, "scenario package import service");
            }
        }

        public bool OpenScenarioDownloadsFolder(out string status)
        {
            return ImportService.OpenFolder(ImportService.StagingRoot, out status);
        }

        public bool OpenExportFolder(string path, out string status)
        {
            return ImportService.OpenFolder(path, out status);
        }

        public bool InstallPackage(ScenarioPackageImportCandidate candidate, out string status)
        {
            ScenarioPackageImportResult result = ImportService.Install(candidate);
            status = result != null ? result.Message : "Install service is unavailable.";
            return result != null && result.Success;
        }

        public string BuildUninstallConfirmation(ScenarioPackageImportCandidate candidate)
        {
            string name = candidate != null && !string.IsNullOrEmpty(candidate.DisplayName)
                ? candidate.DisplayName
                : "this scenario";
            int saveCount = candidate != null && !string.IsNullOrEmpty(candidate.ScenarioId)
                ? _saveLibrary.CountSaves(candidate.ScenarioId)
                : 0;
            return "Uninstall " + name + "?\n\nOnly the installed package folder will be deleted. "
                + saveCount.ToString() + (saveCount == 1 ? " saved run stays" : " saved runs stay")
                + " archived and will reconnect if the same scenario is installed again. Drafts and exports are not changed.";
        }

        public bool UninstallPackage(ScenarioPackageImportCandidate candidate, out string status)
        {
            ScenarioPackageImportResult result = ImportService.Uninstall(candidate);
            status = result != null ? result.Message : "Uninstall service is unavailable.";
            return result != null && result.Success;
        }

        internal static string CreateInteractiveDraftForLiveVerification()
        {
            SaveManager.SaveType launchSaveType = SaveManager.SaveType.Slot1;
            EnsureEditorRuntime("ScenarioBookBrowser live verification CreateDraftInteractive");
            // The harness create-draft flow stays non-interactive: it launches a
            // blank Standard base and auto-opens the editor when the world is
            // ready, without raising the interactive setup wizard.
            ScenarioAuthoringSession draft = ScenarioAuthoringBootstrapService.Instance.QueueNewDraft(
                ScenarioBaseGameMode.Survival,
                launchSaveType,
                false);
            if (draft == null || string.IsNullOrEmpty(draft.StartupSaveId))
                return "failed: draft session did not provide a startup save";

            SaveEntry startupSave = draft.StartupSave;
            if (startupSave == null
                && (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(draft.DraftId, out startupSave) || startupSave == null))
            {
                ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Live verification launch failed.");
                return "failed: could not resolve draft startup save";
            }

            string error;
            bool queued = ScenarioCompositionRoot.Resolve<ScenarioLaunchCoordinator>().QueueAuthoringDraftSceneReload(
                ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                startupSave,
                launchSaveType,
                "authoring draft '" + draft.DraftId + "'",
                draft.BaseMode,
                out error);
            if (!queued)
            {
                ScenarioCompositionRoot.Resolve<IScenarioSaveLibrary>().ClearQueuedNewGameSave(launchSaveType);
                ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Live verification launch failed.");
                return "failed: " + (error ?? "could not launch draft scene");
            }

            return "queued:" + draft.DraftId;
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
                + " virtualSaveType=" + LaunchCoordinator.GetVirtualSaveType(entry) + ".");

            if (entry.IsVanilla && entry.BaseGameMode == ScenarioBaseGameMode.Survival)
            {
                string vanillaError;
                if (!LaunchCoordinator.LaunchVanillaScenario(_adapter, entry, out vanillaError))
                {
                    status = "Start failed: " + Safe(vanillaError, "unknown error");
                    return false;
                }

                return true;
            }

            ScenarioLaunchCoordinator.NewGamePreparation preparation;
            string prepareError;
            if (!LaunchCoordinator.PrepareNewGame(entry, entry.DisplayName, out preparation, out prepareError))
            {
                status = "Start failed: " + Safe(prepareError, "unknown error");
                return false;
            }

            string commitError;
            if (!LaunchCoordinator.CommitNewGame(_adapter, preparation, out commitError))
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
                + " virtualSaveType=" + LaunchCoordinator.GetVirtualSaveType(entry) + ".");

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

            SaveManager.SaveType launchSaveType = LaunchCoordinator.GetVirtualSaveType(entry);
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
            if (!LaunchCoordinator.QueueAuthoringDraftLaunch(
                    _adapter,
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    draftStartupSave,
                    launchSaveType,
                    entry.BaseGameMode,
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
            return CreateDraft(false, out status);
        }

        public bool CreateDraftInteractive(out string status)
        {
            return CreateDraft(true, out status);
        }

        private bool CreateDraft(bool showBaselinePicker, out string status)
        {
            status = null;
            try
            {
                SaveManager.SaveType launchSaveType = SaveManager.SaveType.Slot1;
                EnsureEditorRuntime("ScenarioBookBrowser CreateDraft");
                ScenarioAuthoringSession draft = ScenarioAuthoringBootstrapService.Instance.QueueNewDraft(ScenarioBaseGameMode.Survival, launchSaveType, showBaselinePicker);
                if (draft == null || string.IsNullOrEmpty(draft.StartupSaveId))
                    throw new InvalidOperationException("The draft session did not provide a startup save.");

                SaveEntry draftStartupSave = draft.StartupSave;
                if (draftStartupSave == null
                    && (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(draft.DraftId, out draftStartupSave) || draftStartupSave == null))
                {
                    throw new InvalidOperationException("Could not resolve the draft save entry.");
                }

                string error;
                bool queued = showBaselinePicker
                    ? LaunchCoordinator.QueueAuthoringDraftSceneReload(
                        ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                        draftStartupSave,
                        launchSaveType,
                        "authoring draft '" + draft.DraftId + "'",
                        draft.BaseMode,
                        out error)
                    : LaunchCoordinator.QueueAuthoringDraftLaunch(
                        _adapter,
                        ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                        draftStartupSave,
                        launchSaveType,
                        draft.BaseMode,
                        "authoring draft '" + draft.DraftId + "'",
                        out error);
                if (!queued)
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

            if (!DraftMetadataEditService.TryUpdate(
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
                MMLog.WriteWarning("[ScenarioBookBrowserActionService] Refused draft delete because the selected entry was not a draft.");
                status = "Select a draft scenario first.";
                return false;
            }

            MMLog.WriteInfo("[ScenarioBookBrowserActionService] Deleting draft '" + entry.ScenarioId + "'.");
            _saveLibrary.ClearQueuedNewGameSave(LaunchCoordinator.GetVirtualSaveType(entry));
            bool deleted = ScenarioAuthoringDraftRepository.Instance.DeleteDraft(entry.ScenarioId, "Scenario browser draft delete.");
            if (deleted)
                ScenarioBookBrowserDataSource.BeginSharedRefreshAsync(ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>());
            status = deleted ? "Draft deleted." : "Draft delete failed.";
            MMLog.WriteInfo("[ScenarioBookBrowserActionService] Draft delete completed. draft='" + entry.ScenarioId
                + "' deleted=" + deleted + " status='" + status + "'.");
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
            if (!LaunchCoordinator.LoadSave(_adapter, entry, save, out error))
            {
                status = "Load failed: " + Safe(error, "unknown error");
                return false;
            }

            return true;
        }

        public bool DeleteSave(ScenarioCatalogEntry entry, SaveEntry save, out string status)
        {
            status = null;
            if (!LaunchCoordinator.DeleteSave(entry, save))
            {
                status = save != null ? "Delete failed for slot " + save.absoluteSlot + "." : "Delete failed.";
                return false;
            }

            if (entry != null && !string.IsNullOrEmpty(entry.StorageScenarioId))
            {
                try
                {
                    entry.SaveCount = _saveLibrary.CountSaves(entry.StorageScenarioId);
                }
                catch (Exception ex)
                {
                    entry.SaveCount = Math.Max(0, entry.SaveCount - 1);
                    MMLog.WriteWarning("[ScenarioBookBrowser] Deleted save but count refresh failed for "
                        + entry.StorageScenarioId + ": " + ex.Message);
                }
            }

            status = save != null ? "Deleted slot " + save.absoluteSlot + "." : "Deleted save.";
            return true;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }

        private static T Require<T>(T service, string name) where T : class
        {
            if (service == null)
                throw new InvalidOperationException("Deferred " + name + " resolution returned null.");
            return service;
        }

        private T ResolveDeferred<T>(ref T service, Func<T> factory, string name) where T : class
        {
            lock (_dependencySync)
            {
                if (service == null)
                    service = Require(factory(), name);
                return service;
            }
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
            ShelteredDeferredPatchTriggers.ApplyEditorDeferred(trigger);
        }
    }
}
