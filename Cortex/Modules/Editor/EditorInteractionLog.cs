using ModAPI.Core;

namespace Cortex.Modules.Editor
{
    /// <summary>
    /// Centralized editor diagnostics sink.
    /// Hover diagnostics stay disabled by default to avoid noisy hit-test logging,
    /// while edit diagnostics can be enabled later without scattering direct log calls
    /// through the editor stack.
    /// </summary>
    internal static class EditorInteractionLog
    {
        private const bool HoverDiagnosticsEnabled = false;
        private const bool EditDiagnosticsEnabled = false;

        public static void WriteHover(string message)
        {
            if (!HoverDiagnosticsEnabled || string.IsNullOrEmpty(message))
            {
                return;
            }

            MMLog.WriteInfo("[Cortex.HoverUI] " + message);
        }

        public static void WriteEdit(string message)
        {
            if (!EditDiagnosticsEnabled || string.IsNullOrEmpty(message))
            {
                return;
            }

            MMLog.WriteInfo("[Cortex.Editor] " + message);
        }
    }
}
