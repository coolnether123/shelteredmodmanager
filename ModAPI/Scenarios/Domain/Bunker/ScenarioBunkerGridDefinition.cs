using System.Collections.Generic;

namespace ModAPI.Scenarios
{
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
