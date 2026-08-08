using ShelteredAPI.Scenarios.Infrastructure.Runtime;

namespace ShelteredAPI.Scenarios.Public
{
    /// <summary>Read-only capability descriptor for one authored map encounter field.</summary>
    public sealed class ScenarioMapProjectionField
    {
        public string Group { get; internal set; }
        public string Field { get; internal set; }
        public bool AppliesInGame { get; internal set; }
        internal ScenarioMapEncounterProjectionAction Apply { get; set; }

        public string StatusText
        {
            get { return AppliesInGame ? "Applies in game" : "Saved with the scenario; not yet applied in game"; }
        }
    }
}
