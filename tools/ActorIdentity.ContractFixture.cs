// Production methods are inserted into this class by the contract gate.
public sealed partial class ActorIdentityContractFixture {
    private const string BuiltInOwner = "shelteredapi";
    private readonly Dictionary<ActorId, List<ActorBinding>> _bindings = new Dictionary<ActorId, List<ActorBinding>>();
    private readonly Dictionary<string, ActorId> _bindingIndex = new Dictionary<string, ActorId>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<ActorId>> _bindingTypeIndex = new Dictionary<string, HashSet<ActorId>>(StringComparer.OrdinalIgnoreCase);
    private void MarkRegistryChangedLocked() { }
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var owner = new ActorIdentityContractFixture();
        var id = new ActorId(ActorKind.Custom, 1, "fixture");
        Check(owner.BindLocked(id, new ActorBinding { BindingType = "fixture.type", BindingKey = "part|leaf", SourceModId = "fixture" }, false), "First binding must succeed");
        var other = new ActorId(ActorKind.Custom, 2, "fixture");
        Check(owner.BindLocked(other, new ActorBinding { BindingType = "fixture.type|part", BindingKey = "leaf", SourceModId = "fixture" }, false), "Distinct binding pairs must not collide at a delimiter");
        id.LocalId = 99;
        ActorId stored = owner._bindingIndex[BuildBindingIndexKey("fixture.type", "part|leaf")];
        Check(stored.LocalId == 1 && owner._bindings.ContainsKey(new ActorId(ActorKind.Custom, 1, "fixture")), "Mutating a caller's identity must not corrupt binding indexes");
        var record = new ActorRecord { Id = new ActorId(ActorKind.Custom, 3, "fixture"), Origin = new ActorOrigin { SourceModId = "fixture" } };
        var query = new ActorQuery { Predicate = delegate(IActorRecord value) { value.Id.LocalId = 99; value.Origin.SourceModId = "changed"; return true; } };
        Check(owner.MatchesQueryLocked(record, query) && record.Id.LocalId == 3 && record.Origin.SourceModId == "fixture", "Query predicates must receive a detached record");
        var envelope = new ActorEventEnvelope { ActorId = new ActorId(ActorKind.Custom, 4, "fixture"), Message = "original" };
        int calls = 0;
        var subscription = new ActorSubscription(1, delegate(ActorEventEnvelope value) { value.ActorId.LocalId = 99; value.Message = "changed"; return true; }, delegate { calls++; }, null);
        subscription.Dispatch(envelope);
        Check(calls == 1 && envelope.ActorId.LocalId == 4 && envelope.Message == "original", "Subscription filters must not mutate buffered events or later subscriptions");
    }
}
