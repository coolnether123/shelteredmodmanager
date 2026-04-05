using Cortex.Bridge;
using Cortex.Presentation.Models;
using Cortex.Rendering.Models;
using Cortex.Shell;
using Cortex.Shell.Bridge;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class OverlayPresentationSnapshotBuilderTests
    {
        [Fact]
        public void Build_CreatesOverlaySurfaceSnapshots_FromWorkbenchLayoutState()
        {
            var shellState = new CortexShellState();
            shellState.Workbench.SideContainerId = CortexWorkbenchIds.FileExplorerContainer;
            shellState.Workbench.SecondarySideContainerId = CortexWorkbenchIds.ReferenceContainer;
            shellState.Workbench.EditorContainerId = CortexWorkbenchIds.EditorContainer;
            shellState.Workbench.PanelContainerId = CortexWorkbenchIds.SearchContainer;
            shellState.Workbench.FocusedContainerId = CortexWorkbenchIds.ReferenceContainer;

            var viewState = new CortexShellViewState();
            SetWindow(viewState.OverlayControlWindow, 10f, 20f, 300f, 80f);
            SetWindow(viewState.OverlayPrimaryWindow, 20f, 120f, 220f, 400f);
            SetWindow(viewState.OverlayDocumentWindow, 250f, 120f, 640f, 400f);
            SetWindow(viewState.OverlaySecondaryWindow, 900f, 120f, 220f, 400f);
            SetWindow(viewState.OverlayPanelWindow, 250f, 530f, 640f, 180f);

            var builder = new OverlayPresentationSnapshotBuilder();
            var snapshot = builder.Build(
                shellState,
                viewState,
                new WorkbenchPresentationSnapshot
                {
                    FocusedRegionId = "reference.focus",
                    RendererSummary = "External Overlay Presenter | Capabilities v1"
                },
                7,
                1600d,
                900d,
                "avalonia.external");

            Assert.Equal(7, snapshot.Revision);
            Assert.Equal("avalonia.external", snapshot.PresentationModeId);
            Assert.Equal("reference.focus", snapshot.FocusedRegionId);
            Assert.Equal("External Overlay Presenter | Capabilities v1", snapshot.RendererSummary);
            Assert.Equal(5, snapshot.Surfaces.Count);
            Assert.Equal(OverlayPresentationSnapshotBuilder.SecondarySurfaceId, snapshot.ActiveSurfaceId);
            Assert.Equal(1600d, snapshot.GameWindow.ClientBounds.Width);
            Assert.Equal(900d, snapshot.GameWindow.ClientBounds.Height);

            var primarySurface = FindSurface(snapshot, OverlayPresentationSnapshotBuilder.PrimarySurfaceId);
            Assert.NotNull(primarySurface);
            Assert.Equal("cortex.surface.workspace", primarySurface.ContentViewId);
            Assert.Equal(CortexWorkbenchIds.FileExplorerContainer, primarySurface.ActiveContainerId);
            Assert.Single(primarySurface.HitRegions);
            Assert.True(primarySurface.HitRegions[0].Interactive);

            var panelSurface = FindSurface(snapshot, OverlayPresentationSnapshotBuilder.PanelSurfaceId);
            Assert.NotNull(panelSurface);
            Assert.Equal("cortex.surface.search", panelSurface.ContentViewId);
        }

        private static OverlaySurfaceSnapshot FindSurface(OverlayPresentationSnapshot snapshot, string surfaceId)
        {
            for (var i = 0; i < snapshot.Surfaces.Count; i++)
            {
                if (snapshot.Surfaces[i] != null && snapshot.Surfaces[i].SurfaceId == surfaceId)
                {
                    return snapshot.Surfaces[i];
                }
            }

            return null;
        }

        private static void SetWindow(CortexShellWindowViewState window, float x, float y, float width, float height)
        {
            var rect = new RenderRect(x, y, width, height);
            window.CurrentRect = rect;
            window.ExpandedRect = rect;
            window.CollapsedRect = CortexShellWindowViewState.BuildCollapsedRect(rect, window.CollapsedWidth, window.CollapsedHeight);
            window.IsCollapsed = false;
        }
    }
}
