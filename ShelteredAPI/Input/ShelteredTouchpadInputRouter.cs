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
        private static readonly string[] InfoPaneScrollAxes = { "PC_InfoPaneScroll" };
        private const string VanillaInfoPaneScrollAxis = "PC_InfoPaneScroll";
        private const string VanillaMenuScrollAxis = "PC_MouseScroll";
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
            switch (axis)
            {
                case PlatformInput.InputAxis.CameraHorizontal:
                    if (!ScrollInputService.IsIndirectScrollActive())
                    {
                        value = 0f;
                        return false;
                    }
                    value = UnityTouchpadPanReader.ReadHorizontalPan(raw, CameraHorizontalAxes);
                    return true;

                case PlatformInput.InputAxis.CameraVertical:
                    if (!ScrollInputService.IsIndirectScrollActive())
                    {
                        value = 0f;
                        return false;
                    }
                    value = UnityTouchpadPanReader.ReadVerticalPan(raw, CameraVerticalAxes);
                    return true;

                case PlatformInput.InputAxis.InfoPaneScroll:
                    if (!ScrollInputService.IsIndirectScrollActive())
                        return TryGetVanillaInfoPaneScroll(raw, out value);

                    if (ShouldSuppressIndirectInfoPaneScroll())
                        return TryGetVanillaInfoPaneScroll(raw, out value);

                    TryGetInfoPaneScroll(raw, out value);
                    return true;

                default:
                    value = 0f;
                    return false;
            }
        }

        public static bool TryGetMenuAxis(PlatformInput.MenuInputAxis axis, bool raw, out float value)
        {
            switch (axis)
            {
                case PlatformInput.MenuInputAxis.UIhorizontal:
                    if (!ScrollInputService.IsIndirectScrollActive())
                    {
                        value = 0f;
                        return false;
                    }
                    value = UnityTouchpadPanReader.ReadHorizontalPan(raw, MenuHorizontalAxes);
                    return true;

                case PlatformInput.MenuInputAxis.UIvertical:
                    if (!ScrollInputService.IsIndirectScrollActive())
                    {
                        value = 0f;
                        return false;
                    }
                    value = UnityTouchpadPanReader.ReadVerticalPan(raw, MenuVerticalAxes);
                    return true;

                case PlatformInput.MenuInputAxis.UIscroll:
                    if (!ScrollInputService.IsIndirectScrollActive())
                        return TryGetVanillaMenuScroll(raw, out value);

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

            if (ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(raw), out value))
                value *= ShelteredInputTuning.ZoomSpeed;
        }

        private static void TryGetInfoPaneScroll(bool raw, out float value)
        {
            value = 0f;
            if (ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.Anywhere(raw), out value))
                return;

            value = UnityLegacyAxisReader.ReadStrongest(raw, InfoPaneScrollAxes);
            if (UnityLegacyAxisReader.IsSignificant(value))
                value *= ShelteredInputTuning.MouseScrollSpeed;
        }

        private static bool TryGetVanillaInfoPaneScroll(bool raw, out float value)
        {
            value = UnityLegacyAxisReader.ReadStrongest(raw, VanillaInfoPaneScrollAxis);
            if (!UnityLegacyAxisReader.IsSignificant(value))
                return false;

            value *= ShelteredInputTuning.MouseScrollSpeed;
            return true;
        }

        private static bool TryGetVanillaMenuScroll(bool raw, out float value)
        {
            value = UnityLegacyAxisReader.ReadStrongest(raw, VanillaMenuScrollAxis);
            if (!UnityLegacyAxisReader.IsSignificant(value))
                return false;

            value *= ShelteredInputTuning.MouseScrollSpeed;
            if (IsPartyMapPanelOpen())
                value *= ShelteredInputTuning.ZoomSpeed;

            return true;
        }

        private static bool IsZoomModifierHeld()
        {
            InputBinding binding;
            return ShelteredVanillaInputActions.TryGetBinding(PlatformInput.InputButton.Zoom, out binding)
                && binding.IsHeld();
        }

        private static bool ShouldSuppressIndirectInfoPaneScroll()
        {
            return IsPartyMapPanelOpen() || IsZoomModifierHeld();
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

        private static bool IsPartyMapPanelOpen()
        {
            var panelManager = UIPanelManager.instance;
            if (panelManager == null)
                return false;

            BasePanel topPanel = panelManager.GetTopPanel();
            if (topPanel == null)
                return false;

            return string.Equals(topPanel.GetType().Name, "PartyMapPanel", StringComparison.Ordinal);
        }
    }
}
