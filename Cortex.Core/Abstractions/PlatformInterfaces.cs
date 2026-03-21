namespace Cortex.Core.Abstractions
{
    public interface IOverlayInputCaptureService
    {
        bool IsMouseCaptured { get; }

        bool IsKeyboardCaptured { get; }

        void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard);

        void ReleaseCapture(string ownerId);
    }

    public interface ICortexPlatformModule
    {
        ILoadedModCatalog LoadedModCatalog { get; }

        IRuntimeLogFeed RuntimeLogFeed { get; }

        IRuntimeToolBridge RuntimeToolBridge { get; }

        IRestartCoordinator RestartCoordinator { get; }

        IOverlayInputCaptureService OverlayInputCaptureService { get; }

        IRuntimeSourceNavigationService CreateRuntimeSourceNavigationService(ISourcePathResolver sourcePathResolver);

        string ResolveDecompilerPath(string configuredPathOverride);

        void ConfigureRuntimeLogging(bool enabled);

        void EnsureShellToggleRegistered(string actionId);

        bool IsShellTogglePressed(string actionId);
    }
}
