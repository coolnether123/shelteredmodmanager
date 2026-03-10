namespace ModAPI.Core
{
    /// <summary>
    /// Shared contract for runtime overlays that need the host game to suspend its own input handling.
    /// </summary>
    public interface IOverlayInputCaptureService
    {
        bool IsMouseCaptured { get; }

        bool IsKeyboardCaptured { get; }

        void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard);

        void ReleaseCapture(string ownerId);
    }

    public static class OverlayInputCaptureApi
    {
        public const string Name = "ModAPI.OverlayInputCapture";
    }
}
