using ModAPI.Scenarios;

using ShelteredAPI.Content;
namespace ShelteredAPI.Scenarios.Domain.Bunker{
    /// <summary>
    /// Authored foundation footprint on the bunker grid.
    /// Object placements can depend on foundation IDs so runtime placement respects structural progression.
    /// </summary>
    public class ScenarioFoundationDefinition
    {
        public ScenarioFoundationDefinition()
        {
            BuildPhase = ScenarioBunkerBuildPhase.Start;
            ActiveAtStart = true;
        }

        public string Id { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string ExpansionId { get; set; }
        public ScenarioBunkerBuildPhase BuildPhase { get; set; }
        public bool ActiveAtStart { get; set; }
        public bool LockedAtStart { get; set; }
        public string UnlockGateId { get; set; }
    }
}
