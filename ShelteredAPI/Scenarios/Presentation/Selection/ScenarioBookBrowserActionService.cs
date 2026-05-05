using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Selection;
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

            if (entry.IsVanilla)
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
                return false;

            SaveEntry draftStartupSave;
            if (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(entry.ScenarioId, out draftStartupSave) || draftStartupSave == null)
            {
                status = "Could not resolve the draft authoring save.";
                return false;
            }

            SaveManager.SaveType launchSaveType = _launchCoordinator.GetVirtualSaveType(entry);
            ScenarioAuthoringSession session = ScenarioAuthoringBootstrapService.Instance.QueueExistingDraft(entry.ScenarioId, launchSaveType);
            if (session == null)
            {
                status = "Could not queue the draft for authoring.";
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
                return false;
            }

            return true;
        }

        public bool CreateDraft(out string status)
        {
            status = null;
            try
            {
                SaveManager.SaveType launchSaveType = SaveManager.SaveType.Slot1;
                ScenarioAuthoringSession draft = ScenarioAuthoringBootstrapService.Instance.QueueNewDraft(ScenarioBaseGameMode.Survival, launchSaveType);
                if (draft == null || string.IsNullOrEmpty(draft.StartupSaveId))
                    throw new InvalidOperationException("The draft session did not provide a startup save.");

                SaveEntry draftStartupSave;
                if (!ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(draft.DraftId, out draftStartupSave) || draftStartupSave == null)
                    throw new InvalidOperationException("Could not resolve the draft save entry.");

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
            updatedInfo = null;
            status = null;
            if (entry == null || entry.Source != ScenarioCatalogSource.Draft)
            {
                status = "Select a draft scenario first.";
                return false;
            }

            string error;
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

            status = "Draft details saved.";
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
    }
}
