using Cortex.Core.Abstractions;
using Cortex.Core.Services;
using ModAPI.Core;
using ModAPI.Inspector;
using ModAPI.InputActions;
using UnityEngine;
using CortexOverlayInputCaptureService = global::Cortex.Core.Abstractions.IOverlayInputCaptureService;

namespace Cortex.Platform.ModAPI.Runtime
{
    public sealed class ModApiCortexPlatformModule : ICortexPlatformModule
    {
        private const KeyCode ToggleKey = KeyCode.F8;
        private readonly IHarmonyRuntimeInspectionService _harmonyRuntimeInspectionService = new HarmonyRuntimeInspectionService();
        private readonly ILoadedModCatalog _loadedModCatalog = new ModApiLoadedModCatalog();
        private readonly MmLogRuntimeLogFeed _runtimeLogFeed = new MmLogRuntimeLogFeed();
        private readonly IRuntimeToolBridge _runtimeToolBridge = new ModApiRuntimeToolBridge();
        private readonly IRestartCoordinator _restartCoordinator = new ModApiRestartCoordinator(new RestartRequestWriter());
        private CortexOverlayInputCaptureService _overlayInputCaptureService;

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

        public CortexOverlayInputCaptureService OverlayInputCaptureService
        {
            get
            {
                if (_overlayInputCaptureService == null)
                {
                    _overlayInputCaptureService = ResolveOverlayInputCaptureService();
                }

                return _overlayInputCaptureService;
            }
        }

        public IRuntimeSourceNavigationService CreateRuntimeSourceNavigationService(ISourcePathResolver sourcePathResolver)
        {
            return new RuntimeSourceNavigationService(new ModApiRuntimeSymbolResolver(sourcePathResolver), sourcePathResolver);
        }

        public string ResolveDecompilerPath(string configuredPathOverride)
        {
            return string.IsNullOrEmpty(configuredPathOverride)
                ? new ExternalProcessManager().ResolveDecompilerPath()
                : configuredPathOverride;
        }

        public void ConfigureRuntimeLogging(bool enabled)
        {
            if (enabled)
            {
                _runtimeLogFeed.Attach();
                MMLog.ConfigureRuntimeIntegration(MMLogRuntimeOptions.CortexDefaults());
                return;
            }

            _runtimeLogFeed.Detach();
            MMLog.ConfigureRuntimeIntegration(MMLogRuntimeOptions.Disabled());
        }

        public void EnsureShellToggleRegistered(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) || InputActionRegistry.IsRegistered(actionId))
            {
                return;
            }

            InputActionRegistry.Register(new ModInputAction(
                actionId,
                "Toggle Cortex",
                "Cortex",
                new InputBinding(ToggleKey, KeyCode.None),
                "Open or close the Cortex IDE shell."));
        }

        public bool IsShellTogglePressed(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) && InputActionRegistry.IsDown(actionId);
        }

        private static CortexOverlayInputCaptureService ResolveOverlayInputCaptureService()
        {
            if (!ModAPIRegistry.IsAPIRegistered(OverlayInputCaptureApi.Name))
            {
                return null;
            }

            global::ModAPI.Core.IOverlayInputCaptureService captureService;
            if (!ModAPIRegistry.TryGetAPI<global::ModAPI.Core.IOverlayInputCaptureService>(OverlayInputCaptureApi.Name, out captureService))
            {
                return null;
            }

            return new ModApiOverlayInputCaptureServiceAdapter(captureService);
        }

        private sealed class ModApiOverlayInputCaptureServiceAdapter : CortexOverlayInputCaptureService
        {
            private readonly global::ModAPI.Core.IOverlayInputCaptureService _inner;

            public ModApiOverlayInputCaptureServiceAdapter(global::ModAPI.Core.IOverlayInputCaptureService inner)
            {
                _inner = inner;
            }

            public bool IsMouseCaptured
            {
                get { return _inner != null && _inner.IsMouseCaptured; }
            }

            public bool IsKeyboardCaptured
            {
                get { return _inner != null && _inner.IsKeyboardCaptured; }
            }

            public void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard)
            {
                if (_inner != null)
                {
                    _inner.ReportCapture(ownerId, captureMouse, captureKeyboard);
                }
            }

            public void ReleaseCapture(string ownerId)
            {
                if (_inner != null)
                {
                    _inner.ReleaseCapture(ownerId);
                }
            }
        }
    }
}
