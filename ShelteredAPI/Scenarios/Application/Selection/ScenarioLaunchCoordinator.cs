using System;
using ModAPI.Core;
using ModAPI.Hooks;
using ModAPI.Saves;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Owns the "what happens after the player picks a scenario+save" flow.
    /// Routes new-game, load, and delete through <see cref="IScenarioSaveLibrary"/>
    /// and the existing customisation panel transition without forcing UI code to know
    /// about <see cref="PlatformSaveProxy"/>, <see cref="ScenarioSaves"/>, or
    /// <see cref="ExpandedVanillaSaves"/>.
    /// </summary>
    internal sealed class ScenarioLaunchCoordinator
    {
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly IScenarioSelectionCatalogService _catalog;

        public ScenarioLaunchCoordinator(IScenarioSaveLibrary saveLibrary, IScenarioSelectionCatalogService catalog)
        {
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");
            if (catalog == null) throw new ArgumentNullException("catalog");

            _saveLibrary = saveLibrary;
            _catalog = catalog;
        }

        public SaveManager.SaveType GetVirtualSaveType(ScenarioCatalogEntry entry)
        {
            if (entry != null && entry.IsVanilla)
                return entry.DefaultSaveType;

            return SaveManager.SaveType.Slot1;
        }

        /// <summary>
        /// Result of <see cref="PrepareNewGame"/> — an allocated, validated startup save plus
        /// the virtual save type the caller should commit through <see cref="CommitNewGame"/>.
        /// Held by the caller across the UI mode change so the browser can exit
        /// custom mode only after allocation has succeeded.
        /// </summary>
        internal sealed class NewGamePreparation
        {
            public ScenarioCatalogEntry Entry;
            public CustomScenarioInfo Scenario;
            public SaveEntry StartupSave;
            public SaveManager.SaveType VirtualSaveType;
        }

        /// <summary>
        /// Validate a modded scenario start and allocate the startup save. Does not
        /// touch UI state, so the caller may invoke this before exiting custom mode.
        /// On failure, no scenario state or save state is left behind. On success,
        /// the caller must either call <see cref="CommitNewGame"/> or
        /// <see cref="DiscardPreparation"/> to release the allocated resources.
        /// Vanilla scenarios should fall through to the stock <see cref="ScenarioSelectionPanel"/>
        /// flow and never call this.
        /// </summary>
        public bool PrepareNewGame(
            ScenarioCatalogEntry entry,
            string saveName,
            out NewGamePreparation preparation,
            out string error)
        {
            preparation = null;
            error = null;

            if (entry == null) { error = "scenario entry is null"; return false; }

            if (!entry.CanStart)
            {
                error = "Scenario is locked by missing or mismatched dependencies.";
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] PrepareNewGame refused; scenario is locked. scenarioId="
                    + entry.ScenarioId + " state=" + entry.DependencyState + ".");
                return false;
            }

            if (entry.IsVanilla)
            {
                error = "Vanilla scenarios are launched through the stock scenario panel flow.";
                return false;
            }

            CustomScenarioInfo scenario = entry.CustomScenario;
            if (scenario == null || string.IsNullOrEmpty(scenario.Id))
            {
                error = "Modded scenario entry is missing its descriptor.";
                return false;
            }

            if (!ShelteredCustomScenarioService.Instance.MarkSelected(scenario.Id))
            {
                error = "MarkSelected failed for scenario " + scenario.Id + ".";
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error);
                return false;
            }

            SaveEntry startupSave;
            try
            {
                startupSave = _saveLibrary.CreateNext(scenario.Id, new SaveCreateOptions
                {
                    name = string.IsNullOrEmpty(saveName) ? scenario.DisplayName : saveName
                });
            }
            catch (Exception ex)
            {
                error = "Failed to allocate startup save: " + ex.Message;
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error);
                ShelteredCustomScenarioService.Instance.ClearState();
                return false;
            }

            if (startupSave == null)
            {
                error = "Save library returned a null startup save.";
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error + " scenarioId=" + scenario.Id + ".");
                ShelteredCustomScenarioService.Instance.ClearState();
                return false;
            }

            preparation = new NewGamePreparation
            {
                Entry = entry,
                Scenario = scenario,
                StartupSave = startupSave,
                VirtualSaveType = GetVirtualSaveType(entry)
            };

            MMLog.WriteInfo("[ScenarioLaunchCoordinator] PrepareNewGame ready. scenarioId="
                + scenario.Id + " saveId=" + startupSave.id + " slot=" + startupSave.absoluteSlot
                + " virtualSaveType=" + preparation.VirtualSaveType + ".");
            return true;
        }

        /// <summary>
        /// Queue the prepared save target and push the customisation panel. On
        /// failure rolls back both the queued target and the allocated save, and
        /// clears the pending custom scenario state.
        /// </summary>
        public bool CommitNewGame(
            ScenarioBrowserPanelAdapter adapter,
            NewGamePreparation preparation,
            out string error)
        {
            error = null;

            if (adapter == null) { error = "adapter is null"; return false; }
            if (preparation == null || preparation.StartupSave == null || preparation.Scenario == null)
            {
                error = "preparation is incomplete";
                return false;
            }

            SaveEntry startupSave = preparation.StartupSave;
            SaveManager.SaveType virtualSaveType = preparation.VirtualSaveType;

            try
            {
                _saveLibrary.QueueNewGameSaveTarget(startupSave.scenarioId, startupSave, virtualSaveType);
            }
            catch (Exception ex)
            {
                error = "Failed to queue new-game target: " + ex.Message;
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error);
                DiscardPreparation(preparation, false);
                return false;
            }

            if (!BeginCustomisationTransition(adapter, "custom scenario '" + preparation.Scenario.Id + "'", virtualSaveType))
            {
                error = "Could not push the customisation panel.";
                _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);
                DiscardPreparation(preparation, false);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Release a preparation that will not be committed. Deletes the allocated
        /// startup save and clears the pending custom scenario state.
        /// </summary>
        public void DiscardPreparation(NewGamePreparation preparation, bool clearQueuedTarget)
        {
            if (preparation == null)
                return;

            if (clearQueuedTarget)
                _saveLibrary.ClearQueuedNewGameSave(preparation.VirtualSaveType);

            if (preparation.StartupSave != null && !string.IsNullOrEmpty(preparation.StartupSave.id))
            {
                try { _saveLibrary.Delete(preparation.StartupSave.scenarioId, preparation.StartupSave.id); }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioLaunchCoordinator] Discard delete failed: " + ex.Message);
                }
            }

            ShelteredCustomScenarioService.Instance.ClearState();
        }

        public bool QueueAuthoringDraftLaunch(
            ScenarioBrowserPanelAdapter adapter,
            string draftStorageScenarioId,
            SaveEntry draftStartupSave,
            SaveManager.SaveType virtualSaveType,
            string launchTargetLabel,
            out string error)
        {
            error = null;

            if (adapter == null) { error = "adapter is null"; return false; }
            if (draftStartupSave == null) { error = "draft save is null"; return false; }

            try
            {
                _saveLibrary.QueueNewGameSaveTarget(draftStorageScenarioId, draftStartupSave, virtualSaveType);
            }
            catch (Exception ex)
            {
                error = "Failed to queue draft target: " + ex.Message;
                return false;
            }

            if (!BeginCustomisationTransition(adapter, launchTargetLabel, virtualSaveType))
            {
                error = "Could not push the customisation panel for the draft.";
                _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);
                return false;
            }

            return true;
        }

        public bool LoadSave(
            ScenarioBrowserPanelAdapter adapter,
            ScenarioCatalogEntry entry,
            SaveEntry save,
            out string error)
        {
            error = null;

            if (adapter == null) { error = "adapter is null"; return false; }
            if (entry == null) { error = "scenario entry is null"; return false; }
            if (save == null) { error = "save is null"; return false; }

            if (!entry.CanStart)
            {
                error = "Scenario is locked by missing or mismatched dependencies.";
                return false;
            }

            SaveManager.SaveType virtualSaveType = GetVirtualSaveType(entry);
            try
            {
                _saveLibrary.QueueLoadTarget(entry.StorageScenarioId, save, virtualSaveType);
            }
            catch (Exception ex)
            {
                error = "Failed to queue load target: " + ex.Message;
                return false;
            }

            SaveManager saveManager = SaveManager.instance;
            if (saveManager == null)
            {
                error = "SaveManager is unavailable.";
                _saveLibrary.ClearQueuedLoad(virtualSaveType);
                return false;
            }

            bool started;
            try
            {
                started = saveManager.SetSlotToLoad(GetSlotNumber(virtualSaveType));
            }
            catch (Exception ex)
            {
                error = "SetSlotToLoad threw: " + ex.Message;
                _saveLibrary.ClearQueuedLoad(virtualSaveType);
                return false;
            }

            if (!started)
            {
                error = "SaveManager rejected the load request.";
                _saveLibrary.ClearQueuedLoad(virtualSaveType);
                return false;
            }

            MMLog.WriteInfo("[ScenarioLaunchCoordinator] Load queued. scenarioId="
                + entry.StorageScenarioId + " saveId=" + save.id + " slot=" + save.absoluteSlot
                + " virtualSaveType=" + virtualSaveType + ".");
            return true;
        }

        public bool DeleteSave(ScenarioCatalogEntry entry, SaveEntry save)
        {
            if (entry == null || save == null || string.IsNullOrEmpty(save.id))
                return false;

            bool deleted = _saveLibrary.Delete(entry.StorageScenarioId, save.id);
            MMLog.WriteInfo("[ScenarioLaunchCoordinator] Delete save. scenarioId="
                + entry.StorageScenarioId + " saveId=" + save.id + " result=" + deleted + ".");
            return deleted;
        }

        public void ClearPendingTargets(SaveManager.SaveType virtualSaveType)
        {
            _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);
            _saveLibrary.ClearQueuedLoad(virtualSaveType);
        }

        public bool BeginCustomisationTransition(
            ScenarioBrowserPanelAdapter adapter,
            string launchTargetLabel,
            SaveManager.SaveType virtualSaveType)
        {
            if (adapter == null)
                return false;

            try
            {
                if (!adapter.GetInputEnabled())
                {
                    adapter.SetInputEnabled(true);
                    MMLog.WriteWarning("[ScenarioLaunchCoordinator] Forced input enabled before transition. target="
                        + (launchTargetLabel ?? "<unknown>") + ".");
                }

                SaveManager saveManager = SaveManager.instance;
                if (saveManager == null)
                {
                    MMLog.WriteWarning("[ScenarioLaunchCoordinator] SaveManager unavailable for transition. target="
                        + (launchTargetLabel ?? "<unknown>") + ".");
                    return false;
                }

                saveManager.SetCurrentSlot(GetSlotNumber(virtualSaveType));

                BasePanel customizationPanel = adapter.GetCustomizationPanel();
                UIPanelManager panelManager = UIPanelManager.instance;
                if (customizationPanel != null && panelManager != null)
                {
                    panelManager.PushPanel(customizationPanel);
                    MMLog.WriteInfo("[ScenarioLaunchCoordinator] Customisation panel opened. target="
                        + (launchTargetLabel ?? "<unknown>") + ".");
                    return true;
                }

                MMLog.WriteWarning("[ScenarioLaunchCoordinator] No customisation panel available. target="
                    + (launchTargetLabel ?? "<unknown>") + ".");
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] Transition failed for "
                    + (launchTargetLabel ?? "<unknown>") + ": " + ex);
                return false;
            }
        }

        private static int GetSlotNumber(SaveManager.SaveType saveType)
        {
            switch (saveType)
            {
                case SaveManager.SaveType.Slot1: return 1;
                case SaveManager.SaveType.Slot2: return 2;
                case SaveManager.SaveType.Slot3: return 3;
                case SaveManager.SaveType.SlotSurrounded: return 4;
                case SaveManager.SaveType.SlotStasis: return 5;
                default: return 1;
            }
        }
    }
}
