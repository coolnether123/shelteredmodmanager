using System;
using System.Collections.Generic;
using System.Linq;
using ShelteredAPI.Characters.Abstractions;
using ShelteredAPI.Characters.Models;
using ShelteredAPI.Characters.Internal;
namespace ShelteredAPI.Content { }
namespace ShelteredAPI.Saves { }
namespace ShelteredAPI.Characters.Abstractions { public interface ICharacterProxy { } }
namespace ModAPI.Util {
    public static class FixtureLists {
        public static IReadOnlyList<T> ToReadOnlyList<T>(this List<T> list) { return list.AsReadOnly(); }
    }
}
namespace ShelteredAPI.Characters.Internal {
    internal class LiveCharacterProxy : ICharacterProxy { internal void SyncToGameObject() { } }
    internal static class CharacterEffectSystemImpl { internal static float NowSeconds() { return 10f; } }
    internal static class CharacterEffectSystem {
        internal static readonly FixtureEffectSystem InternalInstance = new FixtureEffectSystem();
    }
    internal sealed class FixtureEffectSystem {
        internal bool TryCreateEffect(string id, out ICharacterEffect effect) { effect = null; return false; }
        internal void OnEffectApplied(ICharacterProxy character, EffectInstance instance) { }
        internal void OnEffectRemoved(ICharacterProxy character, EffectInstance instance, RemovalReason reason) { }
    }
}
public static class CharacterEffectsContractFixture {
    public static int Count;
    private static void Check(bool condition, string message) {
        Count++; if (!condition) throw new Exception(message);
    }
    public class Effect : ICharacterEffect {
        public static StackBehavior Behavior;
        public static bool Allowed = true;
        public static Action<ICharacterEffectContext> TickAction;
        public string EffectId { get { return "fixture.effect"; } }
        public string DisplayName { get { return EffectId; } }
        public float Duration { get { return 10; } }
        public StackBehavior StackBehavior { get { return Behavior; } }
        public bool CanApply(ICharacterEffectContext context) { return Allowed; }
        public void OnApplied(ICharacterEffectContext context) { }
        public void OnRemoved(ICharacterEffectContext context, RemovalReason reason) { }
        public void OnTick(ICharacterEffectContext context, float dt) { if (TickAction != null) TickAction(context); }
        public string SerializeData() { return ""; }
        public void DeserializeData(string data) { }
    }
    public static void Verify() {
        var effects = new BasicCharacterEffects(null);
        Effect.Behavior = StackBehavior.Refresh;
        var first = effects.Apply<Effect>(0);
        effects.Apply<Effect>(2);
        Check(!first.IsPermanent && first.Duration == 2 && first.TimeRemaining == 2, "Refreshing a permanent effect must make a timed effect expire");
        effects.Tick(2);
        Check(effects.Count == 0, "Refreshed timed effect must expire");
        first = effects.Apply<Effect>(2);
        effects.Apply<Effect>(-1);
        effects.Tick(3);
        Check(first.IsPermanent && effects.Count == 1, "Nonpositive refresh must make the effect permanent");
        Effect.Allowed = false;
        Check(effects.Apply<Effect>(5) == null && first.StackCount == 2 && first.IsPermanent, "Rejected refresh must leave the existing effect unchanged");
        Effect.Behavior = StackBehavior.Replace;
        Check(effects.Apply<Effect>(5) == null && effects.Get<Effect>() == first, "Rejected replacement must retain the old effect");
        Effect.Allowed = true;
        effects.Remove(first);
        Effect.Behavior = StackBehavior.Extend;
        first = effects.Apply<Effect>(2);
        effects.Apply<Effect>(0);
        Check(first.IsPermanent, "Extending with a permanent effect must remain permanent");
        effects.Remove(first);
        Effect.Behavior = StackBehavior.Custom;
        effects.Apply<Effect>(10, "fixture");
        effects.Apply<Effect>(10, "fixture");
        int ticks = 0;
        Effect.TickAction = delegate { ticks++; effects.RemoveAllFromMod("fixture"); };
        effects.Tick(1);
        Check(ticks == 1 && effects.Count == 0, "Removing effects during a tick must not repeat callbacks or index a removed entry");
        Effect.TickAction = null;
    }
}
