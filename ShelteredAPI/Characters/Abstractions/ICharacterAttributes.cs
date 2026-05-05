using System;
using System.Collections.Generic;

using ShelteredAPI.Characters.Models;
namespace ShelteredAPI.Characters.Abstractions{
    /// <summary>
    /// Mod-facing attribute modifiers for a character.
    /// Use this for temporary or mod-scoped adjustments instead of mutating vanilla stats directly.
    /// </summary>
    public interface ICharacterAttributes
    {
        AttributeModifier Apply(string attributeName, float value, float duration, string sourceModId);
        bool Remove(AttributeModifier modifier);
        float GetModifier(string attributeName);
        IReadOnlyList<AttributeModifier> GetModifiers(string attributeName);
        int RemoveAllFromMod(string modId);

        event Action<string> ModifierChanged;
    }
}
