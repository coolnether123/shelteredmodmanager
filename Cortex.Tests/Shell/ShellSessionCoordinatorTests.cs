using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Rendering.Models;
using Cortex.Core.Services;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class ShellSessionCoordinatorTests
    {
        [Fact]
        public void PersistSession_RestoresDetachedLogsFlag_ThroughShellViewState()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var persistencePath = Path.Combine(Path.GetTempPath(), "cortex-shell-session-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    var initialState = new CortexShellState();
                    var initialViewState = new CortexShellViewState
                    {
                        ShowDetachedLogsWindow = true
                    };

                    CreateSessionCoordinator(initialState, initialViewState, persistencePath, null).PersistSession();

                    var restoredState = new CortexShellState();
                    var restoredViewState = new CortexShellViewState();
                    CreateSessionCoordinator(restoredState, restoredViewState, persistencePath, null).RestoreSession();

                    Assert.True(restoredViewState.ShowDetachedLogsWindow);
                }
                finally
                {
                    if (File.Exists(persistencePath))
                    {
                        File.Delete(persistencePath);
                    }
                }
            });
        }

        [Fact]
        public void PersistWindowSettings_UsesExpandedBounds_WhenMainWindowIsCollapsed()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var settingsPath = Path.Combine(Path.GetTempPath(), "cortex-shell-settings-" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    var state = new CortexShellState
                    {
                        Settings = new CortexSettings()
                    };
                    var viewState = new CortexShellViewState();
                    viewState.MainWindow.CurrentRect = new RenderRect(14f, 16f, 126f, 28f);
                    viewState.MainWindow.ExpandedRect = new RenderRect(48f, 64f, 1220f, 780f);
                    viewState.MainWindow.IsCollapsed = true;

                    var coordinator = CreateSessionCoordinator(state, viewState, null, settingsPath);
                    coordinator.PersistWindowSettings();

                    var persisted = new JsonCortexSettingsStore(settingsPath).Load();
                    Assert.Equal(48f, persisted.WindowX);
                    Assert.Equal(64f, persisted.WindowY);
                    Assert.Equal(1220f, persisted.WindowWidth);
                    Assert.Equal(780f, persisted.WindowHeight);
                }
                finally
                {
                    if (File.Exists(settingsPath))
                    {
                        File.Delete(settingsPath);
                    }
                }
            });
        }

        private static ShellSessionCoordinator CreateSessionCoordinator(CortexShellState state, CortexShellViewState viewState, string persistencePath, string settingsPath)
        {
            return new ShellSessionCoordinator(
                state,
                viewState,
                delegate { return null; },
                delegate { return null; },
                delegate { return null; },
                delegate { return string.IsNullOrEmpty(persistencePath) ? null : new JsonWorkbenchPersistenceService(persistencePath); },
                delegate { return string.IsNullOrEmpty(settingsPath) ? null : new JsonCortexSettingsStore(settingsPath); });
        }
    }
}
