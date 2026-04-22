using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringSelectionMenuService
    {
        private static readonly ScenarioAuthoringSelectionMenuService _instance = new ScenarioAuthoringSelectionMenuService();
        private readonly ScenarioAuthoringCaptureService _captureService = ScenarioAuthoringCaptureService.Instance;
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

            ContextMenuPanel panel = ResolveContextMenuPanel();
            if (panel == null)
            {
                MMLog.WarnOnce("ScenarioAuthoringSelectionMenu.ResolvePanel", "InteractionMenuPanel was not available for scenario authoring target menus.");
                return;
            }

            List<string> optionIds = new List<string>();
            List<string> optionTexts = new List<string>();
            BuildOptions(state, target, optionIds, optionTexts);
            if (optionIds.Count == 0)
                return;

            Vector2 menuPosition;
            if (!TryResolveMenuPosition(target, out menuPosition))
                return;

            CloseMenu();
            panel.ShowContextMenu(
                menuPosition,
                string.Empty,
                optionIds,
                optionTexts,
                OnMenuOptionSelected);
            _openMenuTargetId = target.Id;
            MMLog.WriteInfo("[ScenarioAuthoringSelectionMenu] Opened target menu for '" + target.DisplayName
                + "' with " + optionIds.Count + " option(s).");
        }

        private void BuildOptions(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            List<string> optionIds,
            List<string> optionTexts)
        {
            AddOption(
                optionIds,
                optionTexts,
                ScenarioAuthoringActionIds.ActionShellShow,
                state.ShellVisible ? "Focus Inspector" : "Open Inspector");

            switch (target.Kind)
            {
                case ScenarioAuthoringTargetKind.Character:
                    AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionToolFamily, "Switch To Family Tool");
                    break;
                default:
                    if (target.SupportsReplace)
                    {
                        AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionToolAssets, "Switch To Assets Tool");
                        AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionToolObjects, "Switch To Shelter Objects Tool");
                    }
                    break;
            }

            string captureReason;
            if (_captureService.CanCaptureTarget(target, out captureReason))
            {
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Selected Object");
            }

            if (_captureService.HasCapturedPlacementForTarget(ScenarioEditorController.Instance.CurrentSession, target))
            {
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Captured Placement");
            }

            if (!string.IsNullOrEmpty(target.ScenarioReferenceId))
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove, "Remove Scene Sprite");

            if (target.SupportsReplace)
            {
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Copy Sprite Swap");
                if (ScenarioSpriteSwapClipboard.HasRule)
                    AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionSpriteSwapPaste, "Paste Sprite Swap");
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionSpriteSwapRevert, "Revert Sprite");
            }

            if (ScenarioAuthoringHistoryService.Instance.CanUndo)
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionHistoryUndo, "Undo");
            if (ScenarioAuthoringHistoryService.Instance.CanRedo)
                AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionHistoryRedo, "Redo");

            AddOption(optionIds, optionTexts, ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection");
        }

        private static void AddOption(List<string> optionIds, List<string> optionTexts, string optionId, string label)
        {
            if (optionIds == null || optionTexts == null || string.IsNullOrEmpty(optionId) || string.IsNullOrEmpty(label))
                return;

            optionIds.Add(optionId);
            optionTexts.Add(label);
        }

        private static void OnMenuOptionSelected(string optionId)
        {
            if (string.IsNullOrEmpty(optionId))
                return;

            if (!ScenarioAuthoringBackendService.Instance.ExecuteAction(optionId))
            {
                MMLog.WriteWarning("[ScenarioAuthoringSelectionMenu] Unhandled target-menu option: " + optionId);
            }
        }

        private static bool TryResolveMenuPosition(ScenarioAuthoringTarget target, out Vector2 position)
        {
            position = Vector2.zero;
            Camera worldCamera = Camera.main;
            Camera uiCamera = UICamera.mainCamera;
            if (worldCamera == null || uiCamera == null || target == null)
                return false;

            Vector3 worldPosition = target.WorldPosition;
            Obj_Base objBase = ResolveObjBase(target);
            if (objBase != null)
            {
                worldPosition = objBase.transform.position;
                BoxCollider2D collider = objBase.GetComponent<BoxCollider2D>();
                if (collider != null)
                    worldPosition += (Vector3)collider.offset;
            }
            else
            {
                Component component = target.RuntimeObject as Component;
                if (component != null)
                    worldPosition = component.transform.position;
            }

            position = uiCamera.ViewportToWorldPoint(worldCamera.WorldToViewportPoint(worldPosition));
            return true;
        }

        private static ContextMenuPanel ResolveContextMenuPanel()
        {
            InteractionManager interactionManager = InteractionManager.Instance;
            if (interactionManager != null)
            {
                ContextMenuPanel panel = interactionManager.GetInteractionMenu();
                if (panel != null)
                    return panel;
            }

            GameObject uiRoot = GameObject.Find("UI Root");
            if (uiRoot == null)
                return null;

            Transform menu = uiRoot.transform.Find("InteractionMenuPanel");
            return menu != null ? menu.GetComponent<ContextMenuPanel>() : null;
        }

        private void CloseMenu()
        {
            ContextMenuPanel panel = ResolveContextMenuPanel();
            if (panel != null && UIPanelManager.instance != null && UIPanelManager.instance.IsPanelOnStack(panel))
                UIPanelManager.instance.PopPanel(panel);

            _openMenuTargetId = null;
        }

        private static Obj_Base ResolveObjBase(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject.GetComponent<Obj_Base>();

            Component component = target.RuntimeObject as Component;
            return component != null ? component.GetComponent<Obj_Base>() : null;
        }
    }
}
