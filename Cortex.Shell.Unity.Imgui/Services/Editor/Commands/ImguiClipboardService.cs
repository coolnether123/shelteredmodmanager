using Cortex.Services.Editor.Commands;
using UnityEngine;

namespace Cortex.Shell.Unity.Imgui.Services.Editor.Commands
{
    internal sealed class ImguiClipboardService : IClipboardService
    {
        public string GetText()
        {
            return GUIUtility.systemCopyBuffer ?? string.Empty;
        }

        public void SetText(string text)
        {
            GUIUtility.systemCopyBuffer = text ?? string.Empty;
        }
    }
}
