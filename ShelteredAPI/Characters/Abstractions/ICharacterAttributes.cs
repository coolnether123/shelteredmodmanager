using System;
using System.Collections.Generic;

namespace ModAPI.Characters
{
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
