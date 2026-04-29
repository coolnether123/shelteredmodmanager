using System;
using ModAPI.InputServices;
using ModAPI.InputActions;
using UnityEngine;

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
                    if (!ShouldRouteGameplayTouchpadPan())
                    {
                        value = 0f;
                        return false;
                    }
                    value = ReadTouchpadOrFallbackHorizontal(raw, CameraHorizontalAxes);
                    return true;

                case PlatformInput.InputAxis.CameraVertical:
                    if (!ShouldRouteGameplayTouchpadPan())
                    {
                        value = 0f;
                        return false;
                    }
                    value = ReadTouchpadOrFallbackVertical(raw, CameraVerticalAxes);
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
                    if (!ShouldRouteMenuTouchpadPan())
                    {
                        value = 0f;
                        return false;
                    }
                    value = ReadTouchpadOrFallbackHorizontal(raw, MenuHorizontalAxes);
                    return true;

                case PlatformInput.MenuInputAxis.UIvertical:
                    if (!ShouldRouteMenuTouchpadPan())
                    {
                        value = 0f;
                        return false;
                    }
                    value = ReadTouchpadOrFallbackVertical(raw, MenuVerticalAxes);
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

        private static float ReadTouchpadOrFallbackHorizontal(bool raw, params string[] fallbackAxisNames)
        {
            Vector2 pan;
            if (UnityTouchpadPanReader.TryReadCurrentPanVector(out pan))
                return UnityLegacyAxisReader.IsSignificant(pan.x) ? pan.x : 0f;

            return UnityTouchpadPanReader.ReadHorizontalPan(raw, fallbackAxisNames);
        }

        private static float ReadTouchpadOrFallbackVertical(bool raw, params string[] fallbackAxisNames)
        {
            Vector2 pan;
            if (UnityTouchpadPanReader.TryReadCurrentPanVector(out pan))
                return UnityLegacyAxisReader.IsSignificant(pan.y) ? pan.y : 0f;

            return UnityTouchpadPanReader.ReadVerticalPan(raw, fallbackAxisNames);
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
            if (IsMapPanPanelOpen())
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
            return IsMapPanPanelOpen() || IsZoomModifierHeld();
        }

        private static bool ShouldRouteMenuScrollToUi()
        {
            var panelManager = UIPanelManager.instance;
            if (panelManager == null)
                return false;

            BasePanel topPanel = panelManager.GetTopPanel();
            if (topPanel == null)
                return false;

            return !IsMapPanPanel(topPanel);
        }

        private static bool ShouldRouteGameplayTouchpadPan()
        {
            if (!UnityIndirectScrollClassifier.IsCurrentFrameIndirectScroll())
                return false;

            return GetTopPanel() == null;
        }

        private static bool ShouldRouteMenuTouchpadPan()
        {
            if (!UnityIndirectScrollClassifier.IsCurrentFrameIndirectScroll())
                return false;

            return IsMapPanPanelOpen();
        }

        private static bool IsMapPanPanelOpen()
        {
            BasePanel topPanel = GetTopPanel();
            return IsMapPanPanel(topPanel);
        }

        private static bool IsMapPanPanel(BasePanel panel)
        {
            if (panel == null)
                return false;

            string panelName = panel.GetType().Name;
            if (string.Equals(panelName, "PartyMapPanel", StringComparison.Ordinal))
                return true;

            if (!string.Equals(panelName, "ExpeditionMainPanelNew", StringComparison.Ordinal))
                return false;

            return IsPanelMapScreenVisible(panel);
        }

        private static bool IsPanelMapScreenVisible(BasePanel panel)
        {
            var field = panel.GetType().GetField("MapScreen");
            if (field == null)
                return false;

            var mapScreen = field.GetValue(panel) as GameObject;
            return mapScreen != null && mapScreen.activeInHierarchy;
        }

        private static BasePanel GetTopPanel()
        {
            var panelManager = UIPanelManager.instance;
            if (panelManager == null)
                return null;

            return panelManager.GetTopPanel();
        }
    }
}
