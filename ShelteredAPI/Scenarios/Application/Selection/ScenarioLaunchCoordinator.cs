using System;
using ModAPI.Core;
using ShelteredAPI.Hooks;
using ShelteredAPI.Saves;
using ModAPI.Scenarios;
using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Application.Selection{
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
        private readonly ICustomScenarioLifecycleService _scenarioLifecycle;

        public ScenarioLaunchCoordinator(
            IScenarioSaveLibrary saveLibrary,
            IScenarioSelectionCatalogService catalog,
            ICustomScenarioLifecycleService scenarioLifecycle)
        {
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (scenarioLifecycle == null) throw new ArgumentNullException("scenarioLifecycle");

            _saveLibrary = saveLibrary;
            _catalog = catalog;
            _scenarioLifecycle = scenarioLifecycle;
        }

        public SaveManager.SaveType GetVirtualSaveType(ScenarioCatalogEntry entry)
        {
            if (entry != null)
            {
                if (entry.BaseGameMode == ScenarioBaseGameMode.Surrounded
                    || entry.BaseGameMode == ScenarioBaseGameMode.Stasis)
                {
                    return ScenarioSelectionIds.GetDefaultSaveType(entry.BaseGameMode);
                }

                return entry.IsVanilla ? entry.DefaultSaveType : ScenarioSelectionIds.GetCustomScenarioTransportSaveType();
            }

            return ScenarioSelectionIds.GetCustomScenarioTransportSaveType();
        }

        /// <summary>
        /// Result of <see cref="PrepareNewGame"/> Ã¢â‚¬â€ an allocated, validated startup save plus
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

            CustomScenarioInfo scenario = entry.CustomScenario;
            bool usesCustomLifecycle = !entry.IsVanilla;
            if (usesCustomLifecycle && (scenario == null || string.IsNullOrEmpty(scenario.Id)))
            {
                error = "Modded scenario entry is missing its descriptor.";
                return false;
            }

            if (usesCustomLifecycle && !_scenarioLifecycle.MarkSelected(scenario.Id))
            {
                error = "MarkSelected failed for scenario " + scenario.Id + ".";
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error);
                return false;
            }

            SaveEntry startupSave;
            try
            {
                startupSave = _saveLibrary.CreateNext(entry.StorageScenarioId, new SaveCreateOptions
                {
                    name = string.IsNullOrEmpty(saveName) ? entry.DisplayName : saveName
                });
            }
            catch (Exception ex)
            {
                error = "Failed to allocate startup save: " + ex.Message;
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error);
                if (usesCustomLifecycle)
                    _scenarioLifecycle.ClearState();
                return false;
            }

            if (startupSave == null)
            {
                error = "Save library returned a null startup save.";
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error + " scenarioId=" + entry.StorageScenarioId + ".");
                if (usesCustomLifecycle)
                    _scenarioLifecycle.ClearState();
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
                + entry.StorageScenarioId + " saveId=" + startupSave.id + " slot=" + startupSave.absoluteSlot
                + " virtualSaveType=" + preparation.VirtualSaveType + ".");
            return true;
        }

        /// <summary>
        /// Queue the prepared save target and start the scenario. On
        /// failure rolls back both the queued target and the allocated save, and
        /// clears the pending custom scenario state. Draft authoring and survival
        /// starts still use the customisation panel; published scenario modes start
        /// their Sheltered scenario scene directly.
        /// </summary>
        public bool CommitNewGame(
            ScenarioBrowserPanelAdapter adapter,
            NewGamePreparation preparation,
            out string error)
        {
            error = null;

            if (adapter == null) { error = "adapter is null"; return false; }
            if (preparation == null || preparation.StartupSave == null || preparation.Entry == null)
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

            string launchTargetLabel = BuildLaunchTargetLabel(preparation);
            if (ShouldLaunchWithoutStartup(preparation.Entry))
            {
                if (!BeginDirectScenarioTransition(adapter, launchTargetLabel, virtualSaveType, preparation.Entry.BaseGameMode))
                {
                    error = "Could not launch the scenario scene.";
                    _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);
                    DiscardPreparation(preparation, false);
                    return false;
                }

                return true;
            }

            if (!BeginCustomisationTransition(adapter, launchTargetLabel, virtualSaveType))
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

            if (preparation.Scenario != null)
                _scenarioLifecycle.ClearState();
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

        public bool QueueAuthoringDraftSceneReload(
            string draftStorageScenarioId,
            SaveEntry draftStartupSave,
            SaveManager.SaveType virtualSaveType,
            string launchTargetLabel,
            ScenarioBaseGameMode baseGameMode,
            out string error)
        {
            error = null;

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

            string sceneName;
            if (!TryGetAuthoringLaunchScene(baseGameMode, out sceneName))
            {
                error = "Could not resolve the authoring scene for " + baseGameMode + ".";
                _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);
                return false;
            }

            if (!BeginDirectSceneTransition(sceneName, launchTargetLabel, virtualSaveType))
            {
                error = "Could not launch the " + sceneName + " scene for the draft.";
                _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);
                return false;
            }

            return true;
        }

        public bool LaunchVanillaScenario(
            ScenarioBrowserPanelAdapter adapter,
            ScenarioCatalogEntry entry,
            out string error)
        {
            error = null;

            if (adapter == null) { error = "adapter is null"; return false; }
            if (entry == null) { error = "scenario entry is null"; return false; }
            if (!entry.IsVanilla) { error = "entry is not a vanilla scenario"; return false; }
            if (!entry.CanStart)
            {
                error = "Scenario is locked by missing or mismatched dependencies.";
                return false;
            }

            int selectedScenario;
            int selectedSlot;
            if (!TryGetVanillaScenarioSelection(entry, out selectedScenario, out selectedSlot))
            {
                error = "Vanilla scenario cannot be launched from the scenario panel: " + entry.ScenarioId + ".";
                return false;
            }

            try
            {
                _scenarioLifecycle.ClearState();
                adapter.SetInputEnabled(true);
                adapter.SetSelectedScenario(selectedScenario);

                if (!adapter.SetSelectedSlot(selectedSlot))
                {
                    error = "Could not bind vanilla scenario slot " + (selectedSlot + 1) + ".";
                    MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error + " scenarioId=" + entry.ScenarioId + ".");
                    return false;
                }

                LoadingTransitionRecoveryService.NotifyMenuTransitionRequested(
                    "vanilla scenario '" + entry.ScenarioId + "'",
                    "ScenarioLaunchCoordinator.LaunchVanillaScenario");
                adapter.Panel.OnScenarioChosen();
                if (!adapter.ChooseSelectedSlot())
                {
                    error = "Could not invoke vanilla scenario slot " + (selectedSlot + 1) + ".";
                    MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error + " scenarioId=" + entry.ScenarioId + ".");
                    return false;
                }

                MMLog.WriteInfo("[ScenarioLaunchCoordinator] Vanilla scenario launch routed through stock selection flow. scenarioId="
                    + entry.ScenarioId + " selectedIndex=" + selectedScenario + " selectedSlot=" + selectedSlot + ".");
                return true;
            }
            catch (Exception ex)
            {
                error = "Vanilla scenario launch failed: " + ex.Message;
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] " + error);
                return false;
            }
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
            if (ScenarioSaveLibrary.IsVanillaScenarioSaveEntry(save))
                return LoadVanillaScenarioSave(adapter, save, virtualSaveType, out error);

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

        private bool LoadVanillaScenarioSave(
            ScenarioBrowserPanelAdapter adapter,
            SaveEntry save,
            SaveManager.SaveType virtualSaveType,
            out string error)
        {
            error = null;

            int slotNumber;
            if (!ScenarioSaveLibrary.TryGetVanillaScenarioSlotNumber(save, out slotNumber))
            {
                error = "Could not resolve vanilla scenario slot.";
                return false;
            }

            SaveManager saveManager = SaveManager.instance;
            if (saveManager == null)
            {
                error = "SaveManager is unavailable.";
                return false;
            }

            _saveLibrary.ClearQueuedLoad(virtualSaveType);
            _saveLibrary.ClearQueuedNewGameSave(virtualSaveType);

            try
            {
                if (!saveManager.SetSlotToLoad(slotNumber))
                {
                    error = "SaveManager rejected the vanilla scenario load request.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = "Vanilla scenario load failed: " + ex.Message;
                return false;
            }

            MMLog.WriteInfo("[ScenarioLaunchCoordinator] Vanilla scenario save load started. saveId="
                + save.id + " slot=" + slotNumber + " virtualSaveType=" + virtualSaveType + ".");
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

        public bool BeginDirectScenarioTransition(
            ScenarioBrowserPanelAdapter adapter,
            string launchTargetLabel,
            SaveManager.SaveType virtualSaveType,
            ScenarioBaseGameMode baseGameMode)
        {
            if (adapter == null)
                return false;

            string sceneName;
            if (!TryGetDirectLaunchScene(baseGameMode, out sceneName))
                return false;

            try
            {
                if (!adapter.GetInputEnabled())
                    adapter.SetInputEnabled(true);

                return BeginDirectSceneTransition(sceneName, launchTargetLabel, virtualSaveType);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] Direct scenario launch failed for "
                    + (launchTargetLabel ?? "<unknown>") + ": " + ex);
                return false;
            }
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
                    if (panelManager.IsPanelOnStack(customizationPanel))
                    {
                        MMLog.WriteInfo("[ScenarioLaunchCoordinator] Customisation panel already active or queued; duplicate push skipped. target="
                            + (launchTargetLabel ?? "<unknown>") + ".");
                        return true;
                    }

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

        private static bool ShouldLaunchWithoutStartup(ScenarioCatalogEntry entry)
        {
            return entry != null
                && entry.Source != ScenarioCatalogSource.Draft
                && entry.BaseGameMode != ScenarioBaseGameMode.Survival;
        }

        private static bool TryGetDirectLaunchScene(ScenarioBaseGameMode baseGameMode, out string sceneName)
        {
            switch (baseGameMode)
            {
                case ScenarioBaseGameMode.Surrounded:
                    sceneName = "ShelterScene_Surrounded";
                    return true;
                case ScenarioBaseGameMode.Stasis:
                    sceneName = "ShelterScene_Stasis";
                    return true;
                default:
                    sceneName = null;
                    return false;
            }
        }

        private static bool TryGetAuthoringLaunchScene(ScenarioBaseGameMode baseGameMode, out string sceneName)
        {
            if (baseGameMode == ScenarioBaseGameMode.Survival)
            {
                sceneName = "ShelterScene";
                return true;
            }

            return TryGetDirectLaunchScene(baseGameMode, out sceneName);
        }

        private static bool BeginDirectSceneTransition(
            string sceneName,
            string launchTargetLabel,
            SaveManager.SaveType virtualSaveType)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            SaveManager saveManager = SaveManager.instance;
            if (saveManager == null)
            {
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] SaveManager unavailable for scene launch. target="
                    + (launchTargetLabel ?? "<unknown>") + ".");
                return false;
            }

            if (LoadingScreen.Instance == null)
            {
                MMLog.WriteWarning("[ScenarioLaunchCoordinator] LoadingScreen unavailable for scene launch. target="
                    + (launchTargetLabel ?? "<unknown>") + ".");
                return false;
            }

            saveManager.SetCurrentSlot(GetSlotNumber(virtualSaveType));
            DifficultyManager.StoreMenuDifficultySettings(1, 1, 1, 1, 1, 0, false);
            LoadingScreen.Instance.ShowLoadingScreen(sceneName);
            MMLog.WriteInfo("[ScenarioLaunchCoordinator] Direct scene launch started. target="
                + (launchTargetLabel ?? "<unknown>") + " scene=" + sceneName
                + " virtualSaveType=" + virtualSaveType + ".");
            return true;
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

        private static bool TryGetVanillaScenarioSelection(ScenarioCatalogEntry entry, out int selectedScenario, out int selectedSlot)
        {
            selectedScenario = -1;
            selectedSlot = -1;
            if (entry == null)
                return false;

            switch (entry.LaunchMode)
            {
                case ScenarioLaunchMode.Surrounded:
                    selectedScenario = 0;
                    selectedSlot = 3;
                    return true;
                case ScenarioLaunchMode.Stasis:
                    selectedScenario = 1;
                    selectedSlot = 4;
                    return true;
                default:
                    return false;
            }
        }

        private static string BuildLaunchTargetLabel(NewGamePreparation preparation)
        {
            if (preparation == null || preparation.Entry == null)
                return "scenario";

            string id = preparation.Entry.StorageScenarioId;
            if (preparation.Scenario != null && !string.IsNullOrEmpty(preparation.Scenario.Id))
                id = preparation.Scenario.Id;

            return "scenario '" + id + "'";
        }
    }
}
