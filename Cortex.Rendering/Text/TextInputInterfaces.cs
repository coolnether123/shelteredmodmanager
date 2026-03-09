namespace Cortex.Rendering.Text
{
    public interface ITextInputService
    {
        bool SupportsIme { get; }
        bool SupportsClipboard { get; }
        string GetClipboardText();
        void SetClipboardText(string text);
    }

    public interface ICaretMappingService
    {
        CaretPosition GetCaretPosition(string text, int characterIndex);
        int GetCharacterIndex(string text, CaretPosition position);
    }
}
