using System;

namespace Cortex.Core.Models
{
    /// <summary>
    /// Serializable editor keybinding entry stored in Cortex settings.
    /// </summary>
    [Serializable]
    public sealed class EditorKeybinding
    {
        public string BindingId;
        public string CommandId;
        public string Key;
        public bool Control;
        public bool Shift;
        public bool Alt;
    }
}
