using System.Collections.Generic;
using Cortex.Chrome;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Rendering.RuntimeUi.Shell;
using Cortex.Shell;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private const int OverlayPrimaryWindowId = 0xC080;
        private const int OverlayDocumentWindowId = 0xC081;
        private const int OverlaySecondaryWindowId = 0xC082;
        private const int OverlayPanelWindowId = 0xC083;

        private void RenderOverlayShell()
        {
            EnsureOverlayWindowLayout();

            UpdateOverlayInputCapture();
            if (_viewState.OverlayControlWindow.IsCollapsed)
            {
                DrawCollapsedWindowButton(_viewState.OverlayControlWindow, ">", "Cortex");
            }
            else
            {
                _viewState.OverlayControlWindow.CurrentRect = ToRenderRect(GUI.Window(MainWindowId, ToRect(_viewState.OverlayControlWindow.CurrentRect), DrawOverlayControlWindow, "Cortex Overlay", _windowStyle));
                _viewState.OverlayControlWindow.ExpandedRect = _viewState.OverlayControlWindow.CurrentRect;
            }

            DrawOverlayHostWindow(
                OverlayPrimaryWindowId,
                WorkbenchHostLocation.PrimarySideHost,
                _viewState.OverlayPrimaryWindow,
                "Cortex Explorer");
            DrawOverlayHostWindow(
                OverlayDocumentWindowId,
                WorkbenchHostLocation.DocumentHost,
                _viewState.OverlayDocumentWindow,
                "Cortex Editor");
            DrawOverlayHostWindow(
                OverlaySecondaryWindowId,
                WorkbenchHostLocation.SecondarySideHost,
                _viewState.OverlaySecondaryWindow,
                "Cortex References");
            DrawOverlayHostWindow(
                OverlayPanelWindowId,
                WorkbenchHostLocation.PanelHost,
                _viewState.OverlayPanelWindow,
                "Cortex Panel");
        }

        private void DrawOverlayControlWindow(int windowId)
        {
            var snapshot = _frameSnapshot ?? BuildPresentationSnapshot();
            var onboardingActive = IsOnboardingActive();
            var currentRect = ToRect(_viewState.OverlayControlWindow.CurrentRect);
            var contentRect = new Rect(6f, 24f, Mathf.Max(0f, currentRect.width - 12f), Mathf.Max(0f, currentRect.height - 30f));
            var headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 30f);
            var statusRect = new Rect(contentRect.x, headerRect.yMax + 4f, contentRect.width, Mathf.Max(22f, contentRect.height - 34f));

            _menuGroupRects.Clear();
            var previousEnabled = GUI.enabled;
            if (onboardingActive)
            {
                GUI.enabled = false;
                _openMenuGroup = string.Empty;
            }

            GUILayout.BeginArea(headerRect);
            DrawHeader(snapshot);
            GUILayout.EndArea();

            GUILayout.BeginArea(statusRect);
            DrawStatusStrip(snapshot);
            GUILayout.EndArea();

            GUI.enabled = previousEnabled;
            if (!onboardingActive)
            {
                DrawOpenMenuPanel(snapshot, headerRect);
                ApplyWindowResize(windowId, ref _viewState.OverlayControlWindow.CurrentRect, 640f, 88f);
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 28f));
            }
        }

        private void DrawOverlayPrimaryWindow(int windowId)
        {
            DrawOverlayHostWindow(windowId, WorkbenchHostLocation.PrimarySideHost, _viewState.OverlayPrimaryWindow);
        }

        private void DrawOverlayDocumentWindow(int windowId)
        {
            DrawOverlayHostWindow(windowId, WorkbenchHostLocation.DocumentHost, _viewState.OverlayDocumentWindow);
        }

        private void DrawOverlaySecondaryWindow(int windowId)
        {
            DrawOverlayHostWindow(windowId, WorkbenchHostLocation.SecondarySideHost, _viewState.OverlaySecondaryWindow);
        }

        private void DrawOverlayPanelWindow(int windowId)
        {
            DrawOverlayHostWindow(windowId, WorkbenchHostLocation.PanelHost, _viewState.OverlayPanelWindow);
        }

        private void DrawOverlayHostWindow(int windowId, WorkbenchHostLocation hostLocation, CortexShellWindowViewState windowState)
        {
            if (!_layoutCoordinator.ShouldDisplayHostSurface(_frameSnapshot, hostLocation))
            {
                return;
            }

            var snapshot = _frameSnapshot ?? BuildPresentationSnapshot();
            var currentRect = ToRect(windowState.CurrentRect);
            var contentRect = new Rect(6f, 24f, Mathf.Max(0f, currentRect.width - 12f), Mathf.Max(0f, currentRect.height - 30f));

            _layoutCoordinator.DrawOverlayHostSurface(
                snapshot,
                hostLocation,
                contentRect,
                _tabStyle,
                _activeTabStyle,
                _tabCloseButtonStyle,
                _captionStyle);
            ApplyWindowResize(windowId, ref windowState.CurrentRect, windowState.MinWidth, windowState.MinHeight);
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawOverlayHostWindow(int windowId, WorkbenchHostLocation hostLocation, CortexShellWindowViewState windowState, string title)
        {
            if (!_layoutCoordinator.ShouldDisplayHostSurface(_frameSnapshot, hostLocation))
            {
                return;
            }

            windowState.CurrentRect = ToRenderRect(GUI.Window(windowId, ToRect(windowState.CurrentRect), ResolveOverlayWindowDrawer(hostLocation), title, _windowStyle));
            windowState.ExpandedRect = windowState.CurrentRect;
        }

        private GUI.WindowFunction ResolveOverlayWindowDrawer(WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return DrawOverlayPrimaryWindow;
                case WorkbenchHostLocation.SecondarySideHost:
                    return DrawOverlaySecondaryWindow;
                case WorkbenchHostLocation.PanelHost:
                    return DrawOverlayPanelWindow;
                case WorkbenchHostLocation.DocumentHost:
                default:
                    return DrawOverlayDocumentWindow;
            }
        }

        private void EnsureOverlayWindowLayout()
        {
            if (_viewState.OverlayDocumentWindow.ExpandedRect.Width <= 0f || _viewState.OverlayDocumentWindow.ExpandedRect.Height <= 0f)
            {
                FitOverlayWindowsToScreen();
            }
        }

        private void FitOverlayWindowsToScreen()
        {
            var screenWidth = GetScreenWidth();
            var screenHeight = GetScreenHeight();
            const float margin = 12f;
            const float gap = 12f;

            var controlWidth = Mathf.Clamp(screenWidth * 0.58f, 640f, Mathf.Max(640f, screenWidth - (margin * 2f)));
            var controlRect = new RenderRect(
                Mathf.Max(margin, (screenWidth - controlWidth) * 0.5f),
                margin,
                controlWidth,
                92f);

            var bodyTop = controlRect.Y + controlRect.Height + gap;
            var availableWidth = Mathf.Max(840f, screenWidth - (margin * 4f));
            var sideWidth = Mathf.Clamp(availableWidth * 0.2f, 240f, 320f);
            var secondaryWidth = Mathf.Clamp(availableWidth * 0.2f, 240f, 320f);
            var documentWidth = Mathf.Max(420f, availableWidth - sideWidth - secondaryWidth);
            var availableHeight = Mathf.Max(420f, screenHeight - bodyTop - margin);
            var panelHeight = Mathf.Clamp(availableHeight * 0.24f, 180f, 260f);
            var documentHeight = Mathf.Max(280f, availableHeight - panelHeight - gap);

            var primaryRect = new RenderRect(margin, bodyTop, sideWidth, documentHeight);
            var documentRect = new RenderRect(primaryRect.X + primaryRect.Width + gap, bodyTop, documentWidth, documentHeight);
            var secondaryRect = new RenderRect(documentRect.X + documentRect.Width + gap, bodyTop, secondaryWidth, documentHeight);
            var panelRect = new RenderRect(documentRect.X, documentRect.Y + documentRect.Height + gap, documentRect.Width, panelHeight);

            ApplyOverlayWindowRect(_viewState.OverlayControlWindow, controlRect);
            ApplyOverlayWindowRect(_viewState.OverlayPrimaryWindow, primaryRect);
            ApplyOverlayWindowRect(_viewState.OverlayDocumentWindow, documentRect);
            ApplyOverlayWindowRect(_viewState.OverlaySecondaryWindow, secondaryRect);
            ApplyOverlayWindowRect(_viewState.OverlayPanelWindow, panelRect);
        }

        private void ApplyOverlayWindowRect(CortexShellWindowViewState windowState, RenderRect rect)
        {
            if (windowState == null)
            {
                return;
            }

            windowState.CurrentRect = rect;
            windowState.ExpandedRect = rect;
            windowState.CollapsedRect = CortexWindowChromeController.BuildCollapsedRect(rect, windowState.CollapsedWidth, windowState.CollapsedHeight);
            windowState.IsCollapsed = false;
        }

        private IList<ShellChromeHitRegion> BuildChromeHitRegions()
        {
            var regions = new List<ShellChromeHitRegion>();
            if (GetRuntimeUiLayoutMode() == WorkbenchRuntimeUiLayoutMode.OverlayWindows)
            {
                AddChromeHitRegion(regions, true, _viewState.OverlayControlWindow);
            }
            else
            {
                AddChromeHitRegion(regions, true, _viewState.MainWindow);
            }

            if (GetRuntimeUiLayoutMode() == WorkbenchRuntimeUiLayoutMode.OverlayWindows)
            {
                AddChromeHitRegion(regions, _layoutCoordinator.ShouldDisplayHostSurface(_frameSnapshot, WorkbenchHostLocation.PrimarySideHost), _viewState.OverlayPrimaryWindow);
                AddChromeHitRegion(regions, true, _viewState.OverlayDocumentWindow);
                AddChromeHitRegion(regions, _layoutCoordinator.ShouldDisplayHostSurface(_frameSnapshot, WorkbenchHostLocation.SecondarySideHost), _viewState.OverlaySecondaryWindow);
                AddChromeHitRegion(regions, _layoutCoordinator.ShouldDisplayHostSurface(_frameSnapshot, WorkbenchHostLocation.PanelHost), _viewState.OverlayPanelWindow);
            }

            AddChromeHitRegion(regions, _viewState.ShowDetachedLogsWindow && !_state.Onboarding.IsActive, _viewState.LogsWindow);
            return regions;
        }

        private static void AddChromeHitRegion(IList<ShellChromeHitRegion> regions, bool visible, CortexShellWindowViewState windowState)
        {
            if (regions == null || windowState == null || windowState.CurrentRect.Width <= 0f || windowState.CurrentRect.Height <= 0f)
            {
                return;
            }

            regions.Add(new ShellChromeHitRegion
            {
                Visible = visible,
                IsCollapsed = windowState.IsCollapsed,
                ExpandedRect = windowState.ExpandedRect.Width > 0f ? windowState.ExpandedRect : windowState.CurrentRect,
                CollapsedRect = windowState.CollapsedRect.Width > 0f ? windowState.CollapsedRect : windowState.CurrentRect
            });
        }
    }
}
