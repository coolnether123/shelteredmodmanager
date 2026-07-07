using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity{
    internal sealed class ScenarioStorageAuthoringRuntimeService
    {
        public bool OpenVanillaStorage()
        {
            if (UI_PanelContainer.Instance == null || UI_PanelContainer.Instance.StoragePanel == null)
                return false;
            if (UIPanelManager.Instance() == null)
                return false;

            BasePanel storagePanel = UI_PanelContainer.Instance.StoragePanel;
            if (!UIPanelManager.Instance().IsPanelOnStack(storagePanel))
                UIPanelManager.Instance().PushPanel(storagePanel);
            return true;
        }

        public bool CloseVanillaStorage()
        {
            if (UI_PanelContainer.Instance == null || UI_PanelContainer.Instance.StoragePanel == null)
                return false;
            if (UIPanelManager.Instance() == null)
                return false;

            BasePanel storagePanel = UI_PanelContainer.Instance.StoragePanel;
            if (UIPanelManager.Instance().IsPanelOnStack(storagePanel))
                UIPanelManager.Instance().PopPanel(storagePanel);
            return true;
        }

        public bool IsVanillaStorageOpen()
        {
            if (UI_PanelContainer.Instance == null || UI_PanelContainer.Instance.StoragePanel == null)
                return false;
            if (UIPanelManager.Instance() == null)
                return false;

            return UIPanelManager.Instance().IsPanelOnStack(UI_PanelContainer.Instance.StoragePanel);
        }

        public bool Synchronize(ScenarioAuthoringState state)
        {
            if (state == null || !state.StorageAuthoringActive)
                return false;

            if (!ScenarioAuthoringRuntimeGuards.IsPlaytesting() && IsVanillaStorageOpen())
                return false;

            RestoreSuppliesWorkspace(state);
            state.StatusMessage = "Shelter storage closed. Supplies workspace active.";
            return true;
        }

        public static void RestoreSuppliesWorkspace(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            state.StorageAuthoringActive = false;
            state.ShellVisible = state.StorageAuthoringPreviousShellVisible || !state.ShellVisible;
            state.StorageAuthoringPreviousShellVisible = false;
            state.ActiveStage = ScenarioStageKind.InventoryStorage;
            state.ActiveShellTab = ScenarioAuthoringShellTab.Stockpile;
        }
    }
}
