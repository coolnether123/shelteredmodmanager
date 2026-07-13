namespace ModAPI.Core
{
    /// <summary>
    /// Shared contract for runtime overlays that need the host game to suspend its own input handling.
    /// </summary>
    public interface IOverlayInputCaptureService
    {
        /// <summary>Whether any overlay currently owns mouse input.</summary>
        bool IsMouseCaptured { get; }

        /// <summary>Whether any overlay currently owns keyboard input.</summary>
        bool IsKeyboardCaptured { get; }

        /// <summary>Creates or replaces the capture request for one overlay owner.</summary>
        void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard);

        /// <summary>Removes the capture request for one overlay owner.</summary>
        void ReleaseCapture(string ownerId);
    }

    /// <summary>Registry identifier for the host-provided overlay input capture service.</summary>
    public static class OverlayInputCaptureApi
    {
        /// <summary>Stable API registry name.</summary>
        public const string Name = "ModAPI.OverlayInputCapture";
    }
}
