using System;

namespace ShelteredAPI.Characters
{
    /// <summary>
    /// Custom behavior attached to a Sheltered character for a duration.
    /// Implement this when a mod needs ticking, stackable, or serialized character effects.
    /// </summary>
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

    /// <summary>
    /// How a character effect should behave when applied while the same effect is already active.
    /// </summary>
    public enum StackBehavior
    {
        Replace,
        Extend,
        Refresh,
        Ignore,
        Custom
    }

    /// <summary>
    /// Reason an active character effect was removed.
    /// Effect implementations can use this to decide whether to clean up, cure, or persist state.
    /// </summary>
    public enum RemovalReason
    {
        Expired,
        Manually,
        Cured,
        Death,
        Custom
    }

    /// <summary>
    /// Runtime context passed to character effect callbacks.
    /// Store temporary effect state through <c>GetData</c> and <c>SetData</c>.
    /// </summary>
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
