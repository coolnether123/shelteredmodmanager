namespace Cortex.Services.Editor.Commands
{
    internal interface IClipboardService
    {
        string GetText();
        void SetText(string text);
    }

    internal sealed class MemoryClipboardService : IClipboardService
    {
        private static string _text = string.Empty;

        public string GetText()
        {
            return _text ?? string.Empty;
        }

        public void SetText(string text)
        {
            _text = text ?? string.Empty;
        }
    }
}
