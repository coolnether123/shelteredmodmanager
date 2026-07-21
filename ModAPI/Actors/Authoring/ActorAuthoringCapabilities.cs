using System.Collections.Generic;

namespace ModAPI.Actors
{
    /// <summary>
    /// Small value set supported by scenario actor-authoring fields.
    /// </summary>
    public enum ActorAuthoringFieldValueType
    {
        Bool = 0,
        Int = 1,
        Float = 2,
        String = 3,
        StringEnum = 4,
        Color = 5
    }

    /// <summary>
    /// One mod-owned field that can be authored on a scenario actor.
    /// Values are persisted by field ID inside the declared actor component envelope.
    /// </summary>
    public sealed class ActorAuthoringFieldDefinition
    {
        public ActorAuthoringFieldDefinition()
        {
            ApplicableActorKinds = new ActorKind[0];
            EnumValues = new string[0];
            ComponentVersion = 1;
            IntStep = 1;
            FloatStep = 1f;
        }

        public string Id { get; set; }
        public string Label { get; set; }
        public ActorAuthoringFieldValueType ValueType { get; set; }
        public string ComponentId { get; set; }
        public int ComponentVersion { get; set; }
        public ActorKind[] ApplicableActorKinds { get; set; }
        public string RequiredModId { get; set; }
        public string HelpText { get; set; }
        public string DefaultValue { get; set; }
        public string[] EnumValues { get; set; }
        public int? MinInt { get; set; }
        public int? MaxInt { get; set; }
        public int IntStep { get; set; }
        public float? MinFloat { get; set; }
        public float? MaxFloat { get; set; }
        public float FloatStep { get; set; }
    }

    /// <summary>
    /// Narrow provider contract for scenario actor-authoring fields owned by one mod/API.
    /// </summary>
    public interface IActorAuthoringCapabilityProvider
    {
        string ProviderId { get; }
        string ProviderModId { get; }
        int Priority { get; }
        IList<ActorAuthoringFieldDefinition> GetFields();
    }

    /// <summary>
    /// Registry API exposed by the game runtime for actor-authoring capability providers.
    /// This is intentionally scoped to scenario actor fields, not a generic capability registry.
    /// </summary>
    public interface IActorAuthoringCapabilityRegistry
    {
        bool RegisterProvider(IActorAuthoringCapabilityProvider provider);
        bool UnregisterProvider(string providerId);
        IList<IActorAuthoringCapabilityProvider> GetProviders();
        IList<ActorAuthoringFieldDefinition> GetFields(ActorKind actorKind);
    }
}
