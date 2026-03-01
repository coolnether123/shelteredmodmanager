using System.Collections.Generic;

namespace ModAPI.Characters
{
    public interface ICharacterEffects
    {
        EffectInstance Apply<T>(float? duration = null, string sourceModId = null) where T : ICharacterEffect, new();
        EffectInstance Apply(string effectId, float? duration = null, string sourceModId = null);

        bool Remove(EffectInstance effect, RemovalReason reason = RemovalReason.Manually);
        int RemoveAllOfType<T>(RemovalReason reason = RemovalReason.Manually) where T : ICharacterEffect;
        int RemoveAllOfType(string effectId, RemovalReason reason = RemovalReason.Manually);
        int RemoveAllFromMod(string modId, RemovalReason reason = RemovalReason.Manually);

        bool Has<T>() where T : ICharacterEffect;
        bool Has(string effectId);

        EffectInstance Get<T>() where T : ICharacterEffect;
        EffectInstance Get(string effectId);

        IReadOnlyList<EffectInstance> GetAll<T>() where T : ICharacterEffect;
        IReadOnlyList<EffectInstance> GetAll(string effectId);
        IReadOnlyList<EffectInstance> GetAll();
        IReadOnlyList<EffectInstance> GetAllFromMod(string modId);

        int Count { get; }
    }
}
