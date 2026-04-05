using System;
using System.Diagnostics;
using Cortex.Bridge;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Rendering.Models;
using Cortex.Shell;

namespace Cortex.Shell.Bridge
{
    internal sealed class OverlayPresentationSnapshotBuilder
    {
        public const string ControlSurfaceId = "overlay.control";
        public const string PrimarySurfaceId = "overlay.primary";
        public const string DocumentSurfaceId = "overlay.document";
        public const string SecondarySurfaceId = "overlay.secondary";
        public const string PanelSurfaceId = "overlay.panel";

        public OverlayPresentationSnapshot Build(
            CortexShellState shellState,
            CortexShellViewState viewState,
            WorkbenchPresentationSnapshot snapshot,
            long revision,
            double viewportWidth,
            double viewportHeight,
            string presentationModeId)
        {
            var resolvedShellState = shellState ?? new CortexShellState();
            var resolvedViewState = viewState ?? new CortexShellViewState();
            var overlaySnapshot = new OverlayPresentationSnapshot
            {
                Revision = revision,
                PresentationModeId = presentationModeId ?? string.Empty,
                FocusedRegionId = snapshot != null ? snapshot.FocusedRegionId ?? string.Empty : string.Empty,
                RendererSummary = snapshot != null ? snapshot.RendererSummary ?? string.Empty : string.Empty,
                GameWindow = new OverlayGameWindowSnapshot
                {
                    ProcessId = Process.GetCurrentProcess().Id,
                    Title = "Cortex Runtime",
                    ClientBounds = new OverlayRect
                    {
                        X = 0d,
                        Y = 0d,
                        Width = Math.Max(0d, viewportWidth),
                        Height = Math.Max(0d, viewportHeight)
                    }
                }
            };

            overlaySnapshot.Surfaces.Add(BuildSurface(
                ControlSurfaceId,
                "control",
                ResolveControlViewId(resolvedShellState),
                resolvedShellState.Workbench.FocusedContainerId,
                resolvedViewState.OverlayControlWindow,
                "Cortex Overlay",
                "Runtime and presentation status",
                true));
            overlaySnapshot.Surfaces.Add(BuildSurface(
                PrimarySurfaceId,
                "primary",
                ResolveContentViewId(resolvedShellState.Workbench.SideContainerId),
                resolvedShellState.Workbench.SideContainerId,
                resolvedViewState.OverlayPrimaryWindow,
                "Cortex Explorer",
                "Primary tools",
                !resolvedShellState.Workbench.IsHidden(resolvedShellState.Workbench.SideContainerId)));
            overlaySnapshot.Surfaces.Add(BuildSurface(
                DocumentSurfaceId,
                "document",
                ResolveContentViewId(resolvedShellState.Workbench.EditorContainerId),
                resolvedShellState.Workbench.EditorContainerId,
                resolvedViewState.OverlayDocumentWindow,
                "Cortex Editor",
                "Document host",
                true));
            overlaySnapshot.Surfaces.Add(BuildSurface(
                SecondarySurfaceId,
                "secondary",
                ResolveContentViewId(resolvedShellState.Workbench.SecondarySideContainerId),
                resolvedShellState.Workbench.SecondarySideContainerId,
                resolvedViewState.OverlaySecondaryWindow,
                "Cortex References",
                "Secondary tools",
                !resolvedShellState.Workbench.IsHidden(resolvedShellState.Workbench.SecondarySideContainerId)));
            overlaySnapshot.Surfaces.Add(BuildSurface(
                PanelSurfaceId,
                "panel",
                ResolveContentViewId(resolvedShellState.Workbench.PanelContainerId),
                resolvedShellState.Workbench.PanelContainerId,
                resolvedViewState.OverlayPanelWindow,
                "Cortex Panel",
                "Search, logs, and runtime output",
                !resolvedShellState.Workbench.IsHidden(resolvedShellState.Workbench.PanelContainerId)));

            overlaySnapshot.ActiveSurfaceId = ResolveActiveSurfaceId(resolvedShellState);
            return overlaySnapshot;
        }

        private static OverlaySurfaceSnapshot BuildSurface(
            string surfaceId,
            string hostLocationId,
            string contentViewId,
            string activeContainerId,
            CortexShellWindowViewState windowState,
            string title,
            string subtitle,
            bool visible)
        {
            var rect = ResolveSurfaceRect(windowState);
            var snapshot = new OverlaySurfaceSnapshot
            {
                SurfaceId = surfaceId ?? string.Empty,
                HostLocationId = hostLocationId ?? string.Empty,
                ContentViewId = contentViewId ?? string.Empty,
                ActiveContainerId = activeContainerId ?? string.Empty,
                Visible = visible,
                IsCollapsed = windowState != null && windowState.IsCollapsed,
                ZOrder = ResolveZOrder(surfaceId),
                Bounds = new OverlayRect
                {
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height
                },
                Chrome = new OverlaySurfaceChrome
                {
                    Title = title ?? string.Empty,
                    Subtitle = subtitle ?? string.Empty,
                    ShowCloseButton = false,
                    ShowCollapseButton = true
                }
            };

            snapshot.HitRegions.Add(new OverlayHitRegion
            {
                RegionId = (surfaceId ?? string.Empty) + ".client",
                Interactive = visible,
                Bounds = new OverlayRect
                {
                    X = 0d,
                    Y = 0d,
                    Width = rect.Width,
                    Height = rect.Height
                }
            });

            return snapshot;
        }

        private static RenderRect ResolveSurfaceRect(CortexShellWindowViewState windowState)
        {
            if (windowState == null)
            {
                return new RenderRect(0f, 0f, 0f, 0f);
            }

            if (windowState.CurrentRect.Width > 0f && windowState.CurrentRect.Height > 0f)
            {
                return windowState.CurrentRect;
            }

            if (windowState.ExpandedRect.Width > 0f && windowState.ExpandedRect.Height > 0f)
            {
                return windowState.ExpandedRect;
            }

            return new RenderRect(0f, 0f, 0f, 0f);
        }

        private static int ResolveZOrder(string surfaceId)
        {
            return string.Equals(surfaceId, ControlSurfaceId, StringComparison.OrdinalIgnoreCase) ? 100 : 90;
        }

        private static string ResolveControlViewId(CortexShellState shellState)
        {
            return shellState != null && shellState.Onboarding != null && shellState.Onboarding.IsActive
                ? "cortex.surface.onboarding"
                : "cortex.surface.status";
        }

        private static string ResolveContentViewId(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return "cortex.surface.status";
            }

            if (string.Equals(containerId, CortexWorkbenchIds.EditorContainer, StringComparison.OrdinalIgnoreCase))
            {
                return "cortex.surface.editor";
            }

            if (string.Equals(containerId, CortexWorkbenchIds.SettingsContainer, StringComparison.OrdinalIgnoreCase))
            {
                return "cortex.surface.settings";
            }

            if (string.Equals(containerId, CortexWorkbenchIds.ReferenceContainer, StringComparison.OrdinalIgnoreCase))
            {
                return "cortex.surface.reference";
            }

            if (string.Equals(containerId, CortexWorkbenchIds.SearchContainer, StringComparison.OrdinalIgnoreCase))
            {
                return "cortex.surface.search";
            }

            if (string.Equals(containerId, CortexWorkbenchIds.FileExplorerContainer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(containerId, CortexWorkbenchIds.ProjectsContainer, StringComparison.OrdinalIgnoreCase))
            {
                return "cortex.surface.workspace";
            }

            return "cortex.surface.status";
        }

        private static string ResolveActiveSurfaceId(CortexShellState shellState)
        {
            if (shellState == null || shellState.Workbench == null)
            {
                return DocumentSurfaceId;
            }

            var focusedContainerId = shellState.Workbench.FocusedContainerId;
            if (string.Equals(focusedContainerId, shellState.Workbench.SideContainerId, StringComparison.OrdinalIgnoreCase))
            {
                return PrimarySurfaceId;
            }

            if (string.Equals(focusedContainerId, shellState.Workbench.SecondarySideContainerId, StringComparison.OrdinalIgnoreCase))
            {
                return SecondarySurfaceId;
            }

            if (string.Equals(focusedContainerId, shellState.Workbench.PanelContainerId, StringComparison.OrdinalIgnoreCase))
            {
                return PanelSurfaceId;
            }

            return DocumentSurfaceId;
        }
    }
}
