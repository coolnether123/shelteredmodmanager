using System;
using ModAPI.InputServices;
using ModAPI.InputActions;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Centralizes Sheltered-specific axis routing so Harmony patches stay thin and generic UI stays decoupled.
    /// </summary>
    internal static class ShelteredTouchpadInputRouter
    {
        private static readonly string[] CameraHorizontalAxes = { "PC_CameraHorizontal", "PC_UIhorizontal" };
        private static readonly string[] CameraVerticalAxes = { "PC_CameraVertical", "PC_UIvertical" };
        private static readonly string[] InfoPaneScrollAxes = { "PC_InfoPaneScroll", "PC_MouseScroll" };
        private static readonly string[] MenuHorizontalAxes = { "PC_UIhorizontal", "PC_CameraHorizontal" };
        private static readonly string[] MenuVerticalAxes = { "PC_UIvertical", "PC_CameraVertical" };

        public static bool IsTouchDragDown(float minUiX, float maxUiX)
        {
            return UnityTouchDragTracker.IsDragDown(minUiX, maxUiX);
        }

        public static bool IsTouchDragHeld(float minUiX, float maxUiX)
        {
            return UnityTouchDragTracker.IsDragHeld(minUiX, maxUiX);
        }

        public static bool IsTouchDragUp(float minUiX, float maxUiX)
        {
            return UnityTouchDragTracker.IsDragUp(minUiX, maxUiX);
        }

        public static bool TryGetGameplayAxis(PlatformInput.InputAxis axis, bool raw, out float value)
        {
            if (!ScrollInputService.IsIndirectScrollActive())
            {
                value = 0f;
                return false;
            }

            switch (axis)
            {
                case PlatformInput.InputAxis.CameraHorizontal:
                    value = UnityTouchpadPanReader.ReadHorizontalPan(raw, CameraHorizontalAxes);
                    return true;

                case PlatformInput.InputAxis.CameraVertical:
                    value = UnityTouchpadPanReader.ReadVerticalPan(raw, CameraVerticalAxes);
                    return true;

                case PlatformInput.InputAxis.InfoPaneScroll:
                    TryGetInfoPaneScroll(raw, out value);
                    return true;

                default:
                    value = 0f;
                    return false;
            }
        }

        public static bool TryGetMenuAxis(PlatformInput.MenuInputAxis axis, bool raw, out float value)
        {
            if (!ScrollInputService.IsIndirectScrollActive())
            {
                value = 0f;
                return false;
            }

            switch (axis)
            {
                case PlatformInput.MenuInputAxis.UIhorizontal:
                    value = UnityTouchpadPanReader.ReadHorizontalPan(raw, MenuHorizontalAxes);
                    return true;

                case PlatformInput.MenuInputAxis.UIvertical:
                    value = UnityTouchpadPanReader.ReadVerticalPan(raw, MenuVerticalAxes);
                    return true;

                case PlatformInput.MenuInputAxis.UIscroll:
                    TryGetMenuScroll(raw, out value);
                    return true;

                default:
                    value = 0f;
                    return false;
            }
        }

        private static void TryGetMenuScroll(bool raw, out float value)
        {
            value = 0f;
            if (ShouldRouteMenuScrollToUi())
            {
                ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(raw), out value);
                return;
            }

            if (!IsZoomModifierHeld())
                return;

            ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(raw), out value);
        }

        private static void TryGetInfoPaneScroll(bool raw, out float value)
        {
            value = 0f;
            if (ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(raw), out value))
                return;

            value = UnityLegacyAxisReader.ReadStrongest(raw, InfoPaneScrollAxes);
        }

        private static bool IsZoomModifierHeld()
        {
            InputBinding binding;
            return ShelteredVanillaInputActions.TryGetBinding(PlatformInput.InputButton.Zoom, out binding)
                && binding.IsHeld();
        }

        private static bool ShouldRouteMenuScrollToUi()
        {
            var panelManager = UIPanelManager.instance;
            if (panelManager == null)
                return false;

            BasePanel topPanel = panelManager.GetTopPanel();
            if (topPanel == null)
                return false;

            return !string.Equals(topPanel.GetType().Name, "PartyMapPanel", StringComparison.Ordinal);
        }
    }
}
