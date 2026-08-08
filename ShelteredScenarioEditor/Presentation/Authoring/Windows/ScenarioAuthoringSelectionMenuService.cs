using System;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
namespace ShelteredScenarioEditor.Presentation.Authoring.Windows{
    internal sealed class ScenarioAuthoringSelectionMenuService
    {
        private readonly ScenarioAuthoringContextMenuService _contextMenu;
        private readonly ScenarioSelectionScopeService _selectionScope;
        private string _openMenuTargetId;

        internal ScenarioAuthoringSelectionMenuService(
            ScenarioAuthoringContextMenuService contextMenu,
            ScenarioSelectionScopeService selectionScope)
        {
            _contextMenu = contextMenu;
            _selectionScope = selectionScope;
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
            Vector3 mouse = UnityEngine.Input.mousePosition;
            ScenarioAuthoringInspectorAction[] actions = ScenarioAuthoringPresentationBuilder.BuildContextMenuActions(
                _selectionScope,
                state,
                target);
            _contextMenu.Open(
                string.IsNullOrEmpty(target.DisplayName) ? "<none>" : target.DisplayName,
                ScenarioAuthoringPresentationBuilder.FriendlyKindLabel(target.Kind),
                mouse.x,
                Screen.height - mouse.y,
                true,
                actions);
            _openMenuTargetId = target.Id;
        }

        private void CloseMenu()
        {
            if (_contextMenu != null)
                _contextMenu.Close();
            _openMenuTargetId = null;
        }
    }
}
