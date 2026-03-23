using UnityEngine;

namespace Cortex.Services
{
    internal interface IClipboardService
    {
        string GetText();
        void SetText(string text);
    }

    internal sealed class EditorClipboardService : IClipboardService
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
