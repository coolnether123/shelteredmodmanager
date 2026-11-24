// Production methods are inserted into this class by the contract gate.
public struct Color { public float r; }
public class FixtureStat { public int Level = 6; }
public class FixtureStats {
    public FixtureStat Strength = new FixtureStat(), Dexterity = new FixtureStat(), Intelligence = new FixtureStat(), Charisma = new FixtureStat(), Perception = new FixtureStat();
}
public class FamilyMember {
    public string firstName = "first", lastName = "last";
    public bool isMale = true;
    public string meshId = "fixture.mesh";
    public Color skinColor = new Color { r = 1 }, hairColor = new Color { r = 2 }, shirtColor = new Color { r = 3 }, pantsColor = new Color { r = 4 };
    public int health = 70, maxHealth = 100;
    public FixtureStats BaseStats = new FixtureStats();
}
public class NpcVisitor : FamilyMember { }
public static class JsonUtility {
    public static string ToJson(object value) { throw new InvalidOperationException("Fixture serializer"); }
    public static T FromJson<T>(string value) { throw new InvalidOperationException("Fixture serializer"); }
}
namespace ModAPI.Core {
    public class ModEntry { public string Id = "fixture"; }
    public static class ModRegistry { public static bool TryGetModByAssembly(System.Reflection.Assembly value, out ModEntry entry) { entry = null; return false; } }
    public static class MMLog { public static void WarnOnce(string key, string message) { } }
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
    private readonly object _sync = new object();
    private readonly List<ActorSubscription> _subscriptions = new List<ActorSubscription>();
    private ActorEventEnvelope PublishLocked(ActorEventType type, string source, ActorId id, string component, string message) { return new ActorEventEnvelope { ActorId = id }; }
    public event Action<IActorRecord> ActorCreated, ActorStateChanged;
    public event Action<IActorRecord, ActorDestroyReason> ActorDestroyed;
    public event Action<ActorEventEnvelope> EventPublished;
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var familyProfile = BuildProfile(new FamilyMember());
        var visitorProfile = BuildProfile(new NpcVisitor());
        Check(familyProfile.StrengthLevel == 6 && familyProfile.DexterityLevel == 6 && familyProfile.IntelligenceLevel == 6 && familyProfile.CharismaLevel == 6 && familyProfile.PerceptionLevel == 6, "Family profiles must include all current stat levels");
        Check(visitorProfile.StrengthLevel == 6 && visitorProfile.DexterityLevel == 6 && visitorProfile.IntelligenceLevel == 6 && visitorProfile.CharismaLevel == 6 && visitorProfile.PerceptionLevel == 6, "Visitor profiles must include all current stat levels");
        Check(visitorProfile.IsMale && visitorProfile.MeshId == "fixture.mesh" && visitorProfile.SkinColor.r == 1 && visitorProfile.HairColor.r == 2 && visitorProfile.ShirtColor.r == 3 && visitorProfile.PantsColor.r == 4, "Visitor profiles must include appearance");
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
        int notifications = 0;
        owner.ActorCreated += delegate { throw new Exception("fixture"); };
        owner.ActorCreated += delegate { notifications++; };
        owner.ActorDestroyed += delegate { throw new Exception("fixture"); };
        owner.ActorDestroyed += delegate { notifications++; };
        owner.ActorStateChanged += delegate { throw new Exception("fixture"); };
        owner.ActorStateChanged += delegate { notifications++; };
        owner.EventPublished += delegate { throw new Exception("fixture"); };
        owner.EventPublished += delegate { notifications++; };
        owner._subscriptions.Add(new ActorSubscription(2, null, delegate { notifications++; }, null));
        owner.RaiseActorCreated(record); owner.RaiseActorDestroyed(record, ActorDestroyReason.Explicit); owner.RaiseActorStateChanged(record);
        owner.Publish(ActorEventType.ActorCreated, "fixture", other, null, "fixture");
        Check(notifications == 5, "Throwing actor event handlers must not suppress later handlers or subscription delivery");
    }
}
