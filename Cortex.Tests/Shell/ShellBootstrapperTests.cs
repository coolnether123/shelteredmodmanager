using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Rendering.Models;
using Cortex.Shell;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class ShellBootstrapperTests
    {
        [Fact]
        public void ApplyShellWindowSettings_PreservesCollapsedWindowState()
        {
            var state = new CortexShellState();
            var viewState = new CortexShellViewState();
            var runtime = new NullLanguageRuntimeService();
            var bootstrapper = new ShellBootstrapper(
                state,
                viewState,
                new CortexShellModuleContributionRegistry(),
                null,
                new CortexShellBuiltInModuleRegistrar(),
                new WorkbenchExtensionRegistry(),
                new WorkbenchRuntimeAccess(state, delegate { return null; }),
                runtime,
                runtime,
                runtime);

            viewState.MainWindow.IsCollapsed = true;
            viewState.MainWindow.ExpandedRect = new RenderRect(10f, 20f, 900f, 700f);
            viewState.MainWindow.CollapsedRect = CortexShellWindowViewState.BuildCollapsedRect(
                viewState.MainWindow.ExpandedRect,
                viewState.MainWindow.CollapsedWidth,
                viewState.MainWindow.CollapsedHeight);
            viewState.MainWindow.CurrentRect = viewState.MainWindow.CollapsedRect;

            viewState.LogsWindow.IsCollapsed = true;
            viewState.LogsWindow.ExpandedRect = new RenderRect(40f, 60f, 780f, 520f);
            viewState.LogsWindow.CollapsedRect = CortexShellWindowViewState.BuildCollapsedRect(
                viewState.LogsWindow.ExpandedRect,
                viewState.LogsWindow.CollapsedWidth,
                viewState.LogsWindow.CollapsedHeight);
            viewState.LogsWindow.CurrentRect = viewState.LogsWindow.CollapsedRect;

            bootstrapper.ApplyShellWindowSettings(new CortexSettings
            {
                WindowX = 100f,
                WindowY = 150f,
                WindowWidth = 1200f,
                WindowHeight = 800f
            });

            Assert.True(viewState.MainWindow.IsCollapsed);
            Assert.Equal(100f, viewState.MainWindow.ExpandedRect.X);
            Assert.Equal(150f, viewState.MainWindow.ExpandedRect.Y);
            Assert.Equal(1200f, viewState.MainWindow.ExpandedRect.Width);
            Assert.Equal(800f, viewState.MainWindow.ExpandedRect.Height);
            Assert.Equal(viewState.MainWindow.CollapsedRect.X, viewState.MainWindow.CurrentRect.X);
            Assert.Equal(viewState.MainWindow.CollapsedRect.Y, viewState.MainWindow.CurrentRect.Y);
            Assert.Equal(viewState.MainWindow.CollapsedWidth, viewState.MainWindow.CurrentRect.Width);
            Assert.Equal(viewState.MainWindow.CollapsedHeight, viewState.MainWindow.CurrentRect.Height);

            Assert.True(viewState.LogsWindow.IsCollapsed);
            Assert.Equal(130f, viewState.LogsWindow.ExpandedRect.X);
            Assert.Equal(180f, viewState.LogsWindow.ExpandedRect.Y);
            Assert.Equal(1080f, viewState.LogsWindow.ExpandedRect.Width);
            Assert.Equal(660f, viewState.LogsWindow.ExpandedRect.Height);
            Assert.Equal(viewState.LogsWindow.CollapsedRect.X, viewState.LogsWindow.CurrentRect.X);
            Assert.Equal(viewState.LogsWindow.CollapsedRect.Y, viewState.LogsWindow.CurrentRect.Y);
            Assert.Equal(viewState.LogsWindow.CollapsedWidth, viewState.LogsWindow.CurrentRect.Width);
            Assert.Equal(viewState.LogsWindow.CollapsedHeight, viewState.LogsWindow.CurrentRect.Height);
        }
    }
}
