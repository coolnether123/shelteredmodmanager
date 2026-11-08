// Production methods are inserted into this class by the contract gate.
public struct Color { }
public static class JsonUtility {
    public static string ToJson(object value) { throw new InvalidOperationException("Fixture serializer"); }
    public static T FromJson<T>(string value) { throw new InvalidOperationException("Fixture serializer"); }
}
public sealed partial class ActorIdentityContractFixture {
    private class NullSerializer : IActorComponentSerializer {
        public string ComponentId { get { return "fixture.null"; } }
        public Type ComponentType { get { return typeof(ActorProfileComponent); } }
        public int CurrentVersion { get { return 1; } }
        public string Serialize(IActorComponent component) { return ""; }
        public IActorComponent Deserialize(string payload, int version) { return null; }
    }
    private const string BuiltInOwner = "shelteredapi";
    private readonly Dictionary<ActorId, List<ActorBinding>> _bindings = new Dictionary<ActorId, List<ActorBinding>>();
    private readonly Dictionary<string, ActorId> _bindingIndex = new Dictionary<string, ActorId>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<ActorId>> _bindingTypeIndex = new Dictionary<string, HashSet<ActorId>>(StringComparer.OrdinalIgnoreCase);
    private void MarkRegistryChangedLocked() { }
    private readonly Dictionary<ActorId, ActorRecord> _records = new Dictionary<ActorId, ActorRecord>();
    private readonly Dictionary<ActorId, Dictionary<string, ActorComponentSlot>> _components = new Dictionary<ActorId, Dictionary<string, ActorComponentSlot>>();
    private readonly Dictionary<string, HashSet<ActorId>> _componentIndex = new Dictionary<string, HashSet<ActorId>>();
    private readonly Dictionary<string, IActorComponentSerializer> _serializers = new Dictionary<string, IActorComponentSerializer>();
    private void PublishLocked(ActorEventType type, string source, ActorId id, string component, string message) { }
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
        owner._records[other] = new ActorRecord { Id = other };
        var attrs = new ActorAttributeSetComponent();
        attrs.SetValue("reputation", 2, "fixture");
        ActorEventType eventType; string message;
        Check(owner.SetComponentLocked(other, attrs, "fixture", out eventType, out message) == ActorComponentWriteResult.Added, "Mods must be able to attach the shared attribute component under their own owner");
        var next = new ActorAttributeSetComponent();
        next.SetValue("reputation", 3, "other");
        owner.SetComponentLocked(other, next, "other", out eventType, out message);
        var merged = (ActorAttributeSetComponent)owner._components[other][ActorAttributeSetComponent.DefaultComponentId].Component;
        Check(merged.GetValue("reputation") == 5, "Attribute writes must preserve other mods' contributions");
        merged.Remove("reputation", "fixture");
        owner.SetComponentLocked(other, merged, "fixture", out eventType, out message);
        merged = (ActorAttributeSetComponent)owner._components[other][ActorAttributeSetComponent.DefaultComponentId].Component;
        Check(merged.GetValue("reputation") == 3, "Removing an attribute contribution must survive component merge");
        var removal = new ActorAttributeSetComponent();
        removal.SetValue("reputation", 2, "fixture");
        removal.Remove("reputation", "fixture");
        var existing = new ActorAttributeSetComponent(); existing.SetValue("reputation", 7, "fixture");
        Check(((ActorAttributeSetComponent)removal.Merge(existing)).GetValue("reputation") == 0, "Removal must also work when the incoming component is a detached copy");
        var serializer = new NullSerializer();
        owner._serializers[serializer.ComponentId] = serializer;
        owner.RestoreComponentLocked(other, new ActorComponentSaveEntry { ComponentId = serializer.ComponentId, OwnerModId = "fixture", Version = 1, PayloadJson = "saved-value" });
        var raw = owner._components[other][serializer.ComponentId];
        Check(raw.RawPayload == "saved-value", "A serializer returning null must preserve the saved payload");
        owner.HydrateUnknownSlotsLocked(serializer.ComponentId);
        Check(raw.RawPayload == "saved-value", "Deferred hydration returning null must preserve the original payload");
        Check(owner.BuildComponentSaveEntryLocked(raw).PayloadJson == "saved-value", "Undecodable components must round-trip without data loss");
        bool rejected = false;
        try { new ActorJsonComponentSerializer<ActorProfileComponent>("profile", 1).Deserialize("invalid-json", 1); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "The JSON serializer must report decode failure instead of silently constructing empty data");
    }
}
