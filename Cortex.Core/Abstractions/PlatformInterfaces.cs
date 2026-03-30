using Cortex.Core.Diagnostics;

namespace Cortex.Core.Abstractions
{
    public interface IOverlayInputCaptureService
    {
        bool IsMouseCaptured { get; }

        bool IsKeyboardCaptured { get; }

        void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard);

        void ReleaseCapture(string ownerId);
    }

    public interface ICortexPlatformFeatureRegistry
    {
        void Add<TService>(TService service) where TService : class;

        bool TryGet<TService>(out TService service) where TService : class;

        TService Get<TService>() where TService : class;
    }

    public interface ICortexPlatformModule
    {
        ICortexLogSink LogSink { get; }

        ICortexDiagnosticConfiguration DiagnosticConfiguration { get; }

        ILoadedModCatalog LoadedModCatalog { get; }

        IRuntimeLogFeed RuntimeLogFeed { get; }

        IRuntimeToolBridge RuntimeToolBridge { get; }

        IRestartCoordinator RestartCoordinator { get; }

        IOverlayInputCaptureService OverlayInputCaptureService { get; }

        string AdditionalDecompilerCacheRoots { get; }

        void RegisterFeatures(ICortexPlatformFeatureRegistry registry);

        IRuntimeSourceNavigationService CreateRuntimeSourceNavigationService(ISourcePathResolver sourcePathResolver);

        string ResolveDecompilerPath(string configuredPathOverride);

        void ConfigureRuntimeLogging(bool enabled);

        void EnsureShellToggleRegistered(string actionId);

        bool IsShellTogglePressed(string actionId);
    }
}
