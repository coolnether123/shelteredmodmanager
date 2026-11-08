public enum CharacterSource { RealFamily, Visitor, Synthetic }
public interface ICharacterProxy { int UniqueId { get; } CharacterSource Source { get; } }
public class LiveCharacterProxy : ICharacterProxy {
    public int UniqueId { get; set; }
    public CharacterSource Source { get; set; }
    public bool IsActive = true;
    public void Unregister() { IsActive = false; }
}
public class SyntheticCharacterProxy : ICharacterProxy {
    public int UniqueId { get; set; }
    public CharacterSource Source { get { return CharacterSource.Synthetic; } }
    public string PersistenceKey { get; set; }
    public bool IsActive = true;
    public void Unregister() { IsActive = false; }
}
public static class ShelteredActors {
    public static readonly FixtureActors Instance = new FixtureActors();
}
public class FixtureActors { public void Destroy(ActorId id, ActorDestroyReason reason) { } }
public sealed partial class CharacterLifetimeContractFixture {
    private readonly object _sync = new object();
    private readonly Dictionary<int, LiveCharacterProxy> _liveFamily = new Dictionary<int, LiveCharacterProxy>();
    private readonly Dictionary<int, LiveCharacterProxy> _liveVisitors = new Dictionary<int, LiveCharacterProxy>();
    private readonly Dictionary<int, SyntheticCharacterProxy> _syntheticById = new Dictionary<int, SyntheticCharacterProxy>();
    private readonly Dictionary<string, SyntheticCharacterProxy> _syntheticByKey = new Dictionary<string, SyntheticCharacterProxy>();
    private readonly HashSet<int> _temporarySyntheticIds = new HashSet<int>();
    public event Action<ICharacterProxy> SyntheticCharacterUnloaded;
    private ActorId ResolveActorId(ICharacterProxy character) { return new ActorId(ActorKind.Custom, character.UniqueId, "fixture"); }
    private void WriteError(string message) { throw new Exception(message); }
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var owner = new CharacterLifetimeContractFixture();
        var family = new LiveCharacterProxy { UniqueId = 1, Source = CharacterSource.RealFamily };
        var visitor = new LiveCharacterProxy { UniqueId = 1, Source = CharacterSource.Visitor };
        owner._liveFamily[1] = family; owner._liveVisitors[1] = visitor;
        owner.UnregisterCharacter(family);
        Check(!family.IsActive && owner._liveVisitors.ContainsKey(1) && visitor.IsActive, "Unregistering a family member must not remove a visitor sharing its numeric ID");
        var stale = new SyntheticCharacterProxy { UniqueId = 3, PersistenceKey = "fixture.same" };
        var replacement = new SyntheticCharacterProxy { UniqueId = 3, PersistenceKey = "fixture.same" };
        owner._syntheticById[3] = replacement; owner._syntheticByKey[replacement.PersistenceKey] = replacement;
        owner.UnregisterCharacter(stale);
        Check(owner._syntheticById[3] == replacement && owner._syntheticByKey[replacement.PersistenceKey] == replacement, "A stale synthetic handle must not unregister its replacement");
        owner._liveFamily[1] = new LiveCharacterProxy { UniqueId = 1, Source = CharacterSource.RealFamily };
        owner.UnregisterCharacter(family);
        Check(owner._liveFamily.ContainsKey(1), "A stale live handle must not unregister a current handle with the same ID");
        var current = owner._liveFamily[1];
        owner.HandleSessionStarted();
        Check(!current.IsActive && !visitor.IsActive && !replacement.IsActive, "A session boundary must deactivate every retained handle");
        Check(owner._liveFamily.Count == 0 && owner._liveVisitors.Count == 0 && owner._syntheticById.Count == 0, "A session boundary must clear all character registries");
    }
}
