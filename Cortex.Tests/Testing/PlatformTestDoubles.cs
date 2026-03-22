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
        private readonly IHarmonyRuntimeInspectionService _harmonyRuntimeInspectionService = new TestHarmonyRuntimeInspectionService();
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

        public IHarmonyRuntimeInspectionService HarmonyRuntimeInspectionService
        {
            get { return _harmonyRuntimeInspectionService; }
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

    internal sealed class TestHarmonyRuntimeInspectionService : IHarmonyRuntimeInspectionService
    {
        public bool IsAvailable
        {
            get { return true; }
        }

        public HarmonyPatchSnapshot CaptureSnapshot()
        {
            return new HarmonyPatchSnapshot
            {
                GeneratedUtc = System.DateTime.UtcNow,
                Methods = new HarmonyMethodPatchSummary[0],
                StatusMessage = "Test snapshot."
            };
        }

        public HarmonyMethodPatchSummary Inspect(HarmonyPatchInspectionRequest request)
        {
            return new HarmonyMethodPatchSummary
            {
                CapturedUtc = System.DateTime.UtcNow,
                Counts = new HarmonyPatchCounts(),
                Entries = new HarmonyPatchEntry[0],
                Owners = new string[0],
                Order = new HarmonyPatchOrderExplanation[0],
                Target = new HarmonyPatchNavigationTarget
                {
                    AssemblyPath = request != null ? request.AssemblyPath ?? string.Empty : string.Empty,
                    MetadataToken = request != null ? request.MetadataToken : 0,
                    DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                    CachePath = request != null ? request.CachePath ?? string.Empty : string.Empty,
                    DeclaringTypeName = request != null ? request.DeclaringTypeName ?? string.Empty : string.Empty,
                    MethodName = request != null ? request.MethodName ?? string.Empty : string.Empty,
                    Signature = request != null ? request.Signature ?? string.Empty : string.Empty,
                    DisplayName = request != null ? request.DisplayName ?? string.Empty : string.Empty,
                    Line = 1,
                    Column = 1
                }
            };
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
