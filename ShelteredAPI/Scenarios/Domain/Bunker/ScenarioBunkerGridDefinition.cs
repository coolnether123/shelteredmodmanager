using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Full authored bunker grid model for a scenario.
    /// Cells describe grid occupancy while foundations, expansions, and boundaries describe larger unlockable structures.
    /// </summary>
    public class ScenarioBunkerGridDefinition
    {
        public ScenarioBunkerGridDefinition()
        {
            Cells = new List<ScenarioBunkerCellDefinition>();
            Foundations = new List<ScenarioFoundationDefinition>();
            Expansions = new List<ScenarioBunkerExpansionDefinition>();
            Boundaries = new List<ScenarioBunkerBoundaryDefinition>();
        }

        public List<ScenarioBunkerCellDefinition> Cells { get; private set; }
        public List<ScenarioFoundationDefinition> Foundations { get; private set; }
        public List<ScenarioBunkerExpansionDefinition> Expansions { get; private set; }
        public List<ScenarioBunkerBoundaryDefinition> Boundaries { get; private set; }
    }
}
