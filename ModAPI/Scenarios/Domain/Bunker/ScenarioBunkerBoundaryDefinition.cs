namespace ModAPI.Scenarios
{
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
