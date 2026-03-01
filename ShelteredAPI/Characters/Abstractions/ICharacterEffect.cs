using System;

namespace ModAPI.Characters
{
    public interface ICharacterEffect
    {
        string EffectId { get; }
        string DisplayName { get; }
        float Duration { get; }
        StackBehavior StackBehavior { get; }

        void OnApplied(ICharacterEffectContext context);
        void OnTick(ICharacterEffectContext context, float deltaTime);
        void OnRemoved(ICharacterEffectContext context, RemovalReason reason);
        bool CanApply(ICharacterEffectContext context);

        string SerializeData();
        void DeserializeData(string data);
    }

    public enum StackBehavior
    {
        Replace,
        Extend,
        Refresh,
        Ignore,
        Custom
    }

    public enum RemovalReason
    {
        Expired,
        Manually,
        Cured,
        Death,
        Custom
    }

    public interface ICharacterEffectContext
    {
        ICharacterProxy Character { get; }
        EffectInstance Effect { get; }
        float TimeApplied { get; }
        float TimeRemaining { get; }
        float Elapsed { get; }

        T GetData<T>(string key);
        void SetData<T>(string key, T value);
    }
}
