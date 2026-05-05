using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Rectangular bunker grid boundary, usually used to group cells under an expansion.
    /// </summary>
    public class ScenarioBunkerBoundaryDefinition
    {
        public string Id { get; set; }
        public int MinGridX { get; set; }
        public int MinGridY { get; set; }
        public int MaxGridX { get; set; }
        public int MaxGridY { get; set; }
        public string ExpansionId { get; set; }
    }
}
