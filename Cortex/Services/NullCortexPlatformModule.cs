using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class NullCortexPlatformModule : ICortexPlatformModule
    {
        public static readonly NullCortexPlatformModule Instance = new NullCortexPlatformModule();

        private readonly ILoadedModCatalog _loadedModCatalog = new NullLoadedModCatalog();
        private readonly IRuntimeLogFeed _runtimeLogFeed = new NullRuntimeLogFeed();
        private readonly IRuntimeToolBridge _runtimeToolBridge = new NullRuntimeToolBridge();
        private readonly IRestartCoordinator _restartCoordinator = new NullRestartCoordinator();
        private readonly IOverlayInputCaptureService _overlayInputCaptureService = new NullOverlayInputCaptureService();
        private readonly IRuntimeSourceNavigationService _runtimeSourceNavigationService = new NullRuntimeSourceNavigationService();

        private NullCortexPlatformModule()
        {
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
            return _runtimeSourceNavigationService;
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

        private sealed class NullLoadedModCatalog : ILoadedModCatalog
        {
            public IList<LoadedModInfo> GetLoadedMods()
            {
                return new List<LoadedModInfo>();
            }

            public LoadedModInfo GetMod(string modId)
            {
                return null;
            }
        }

        private sealed class NullRuntimeLogFeed : IRuntimeLogFeed
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

        private sealed class NullRuntimeToolBridge : IRuntimeToolBridge
        {
            public IList<RuntimeToolStatus> GetTools()
            {
                return new List<RuntimeToolStatus>();
            }

            public bool Execute(string toolId, out string statusMessage)
            {
                statusMessage = "No runtime tools are available for this platform.";
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

        private sealed class NullRestartCoordinator : IRestartCoordinator
        {
            public bool RequestCurrentSessionRestart(out string errorMessage)
            {
                errorMessage = "Restart is not available for this platform.";
                return false;
            }

            public bool RequestManifestRestart(GameModding.Shared.Restart.RestartRequest request, out string errorMessage)
            {
                errorMessage = "Restart is not available for this platform.";
                return false;
            }
        }

        private sealed class NullOverlayInputCaptureService : IOverlayInputCaptureService
        {
            public bool IsMouseCaptured
            {
                get { return false; }
            }

            public bool IsKeyboardCaptured
            {
                get { return false; }
            }

            public void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard)
            {
            }

            public void ReleaseCapture(string ownerId)
            {
            }
        }

        private sealed class NullRuntimeSourceNavigationService : IRuntimeSourceNavigationService
        {
            public SourceNavigationTarget Resolve(RuntimeLogEntry entry, int frameIndex, CortexProjectDefinition project, CortexSettings settings)
            {
                return new SourceNavigationTarget
                {
                    Success = false,
                    FilePath = string.Empty,
                    StatusMessage = "Runtime source navigation is not available for this platform."
                };
            }
        }
    }
}
