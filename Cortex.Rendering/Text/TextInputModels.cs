namespace Cortex.Rendering.Text
{
    public sealed class TextInputEvent
    {
        public char Character;
        public string Text;
        public bool Shift;
        public bool Control;
        public bool Alt;
    }

    public sealed class CaretPosition
    {
        public int Line;
        public int Column;
    }
}
