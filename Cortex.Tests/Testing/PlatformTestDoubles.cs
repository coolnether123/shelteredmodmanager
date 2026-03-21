using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Tests.Testing
{
    internal sealed class TestOverlayInputCaptureService : IOverlayInputCaptureService
    {
        public bool IsMouseCaptured { get; private set; }

        public bool IsKeyboardCaptured { get; private set; }

        public void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard)
        {
            IsMouseCaptured = captureMouse;
            IsKeyboardCaptured = captureKeyboard;
        }

        public void ReleaseCapture(string ownerId)
        {
            IsMouseCaptured = false;
            IsKeyboardCaptured = false;
        }
    }

    internal sealed class TestCortexPlatformModule : ICortexPlatformModule
    {
        private readonly ILoadedModCatalog _loadedModCatalog;
        private readonly IOverlayInputCaptureService _overlayInputCaptureService;
        private readonly IRuntimeLogFeed _runtimeLogFeed = new TestRuntimeLogFeed();
        private readonly IRuntimeToolBridge _runtimeToolBridge = new TestRuntimeToolBridge();
        private readonly IRestartCoordinator _restartCoordinator = new TestRestartCoordinator();

        public TestCortexPlatformModule(
            ILoadedModCatalog loadedModCatalog,
            IOverlayInputCaptureService overlayInputCaptureService)
        {
            _loadedModCatalog = loadedModCatalog;
            _overlayInputCaptureService = overlayInputCaptureService;
        }

        public ILoadedModCatalog LoadedModCatalog
        {
            get { return _loadedModCatalog; }
        }

        public IRuntimeLogFeed RuntimeLogFeed
        {
            get { return _runtimeLogFeed; }
        }

        public IRuntimeToolBridge RuntimeToolBridge
        {
            get { return _runtimeToolBridge; }
        }

        public IRestartCoordinator RestartCoordinator
        {
            get { return _restartCoordinator; }
        }

        public IOverlayInputCaptureService OverlayInputCaptureService
        {
            get { return _overlayInputCaptureService; }
        }

        public IRuntimeSourceNavigationService CreateRuntimeSourceNavigationService(ISourcePathResolver sourcePathResolver)
        {
            return new TestRuntimeSourceNavigationService();
        }

        public string ResolveDecompilerPath(string configuredPathOverride)
        {
            return configuredPathOverride ?? string.Empty;
        }

        public void ConfigureRuntimeLogging(bool enabled)
        {
        }

        public void EnsureShellToggleRegistered(string actionId)
        {
        }

        public bool IsShellTogglePressed(string actionId)
        {
            return false;
        }
    }

    internal sealed class TestRuntimeLogFeed : IRuntimeLogFeed
    {
        public IList<RuntimeLogEntry> ReadRecent(string minimumLevel, int maxCount)
        {
            return new List<RuntimeLogEntry>();
        }

        public IList<string> ReadBacklog(string logPath, int maxCount)
        {
            return new List<string>();
        }
    }

    internal sealed class TestRuntimeToolBridge : IRuntimeToolBridge
    {
        public IList<RuntimeToolStatus> GetTools()
        {
            return new List<RuntimeToolStatus>();
        }

        public bool Execute(string toolId, out string statusMessage)
        {
            statusMessage = string.Empty;
            return false;
        }

        public void ToggleRuntimeInspector()
        {
        }

        public void ToggleIlInspector()
        {
        }

        public void ToggleUiDebugger()
        {
        }

        public void ToggleRuntimeDebugger()
        {
        }
    }

    internal sealed class TestRestartCoordinator : IRestartCoordinator
    {
        public bool RequestCurrentSessionRestart(out string errorMessage)
        {
            errorMessage = string.Empty;
            return false;
        }

        public bool RequestManifestRestart(GameModding.Shared.Restart.RestartRequest request, out string errorMessage)
        {
            errorMessage = string.Empty;
            return false;
        }
    }

    internal sealed class TestRuntimeSourceNavigationService : IRuntimeSourceNavigationService
    {
        public SourceNavigationTarget Resolve(RuntimeLogEntry entry, int frameIndex, CortexProjectDefinition project, CortexSettings settings)
        {
            return new SourceNavigationTarget();
        }
    }

    internal sealed class InMemoryLoadedModCatalog : ILoadedModCatalog
    {
        private readonly IList<LoadedModInfo> _mods;

        public InMemoryLoadedModCatalog(IList<LoadedModInfo> mods)
        {
            _mods = mods ?? new List<LoadedModInfo>();
        }

        public IList<LoadedModInfo> GetLoadedMods()
        {
            return new List<LoadedModInfo>(_mods);
        }

        public LoadedModInfo GetMod(string modId)
        {
            for (var i = 0; i < _mods.Count; i++)
            {
                if (string.Equals(_mods[i].ModId, modId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return _mods[i];
                }
            }

            return null;
        }
    }
}
