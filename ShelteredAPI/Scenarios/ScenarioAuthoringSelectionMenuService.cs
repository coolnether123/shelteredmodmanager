using System;
namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSelectionMenuService
    {
        private static readonly ScenarioAuthoringSelectionMenuService _instance = new ScenarioAuthoringSelectionMenuService();
        private string _openMenuTargetId;

        public static ScenarioAuthoringSelectionMenuService Instance
        {
            get { return _instance; }
        }

        private ScenarioAuthoringSelectionMenuService()
        {
        }

        public void Sync(ScenarioAuthoringState state)
        {
            if (state == null || !state.IsActive || state.SelectedTarget == null)
            {
                CloseMenu();
                return;
            }

            if (!string.IsNullOrEmpty(_openMenuTargetId)
                && !string.Equals(_openMenuTargetId, state.SelectedTarget.Id, StringComparison.OrdinalIgnoreCase))
            {
                CloseMenu();
            }
        }

        public void Reset()
        {
            CloseMenu();
        }

        public void OpenMenu(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null || target == null)
                return;

            CloseMenu();
            ScenarioAuthoringBackendService.Instance.OpenContextMenu(state, target);
            _openMenuTargetId = target.Id;
        }

        private void CloseMenu()
        {
            ScenarioCompositionRoot.Resolve<ScenarioAuthoringContextMenuService>().Close();
            _openMenuTargetId = null;
        }
    }
}
