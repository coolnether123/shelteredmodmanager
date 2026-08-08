using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.People
{
    /// <summary>
    /// Owns compatibility with definitions that stored a future survivor's actor
    /// reference on the nested member before the outer reference became canonical.
    /// </summary>
    internal static class ScenarioFutureSurvivorActorReference
    {
        internal static ScenarioActorRef Resolve(FutureSurvivorDefinition survivor)
        {
            if (survivor == null)
                return null;
            if (survivor.ActorRef != null)
                return survivor.ActorRef;
            return survivor.Survivor != null ? survivor.Survivor.ActorRef : null;
        }
    }
}
