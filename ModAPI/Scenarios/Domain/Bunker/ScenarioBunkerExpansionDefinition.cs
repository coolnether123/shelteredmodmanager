using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public class ScenarioBunkerExpansionDefinition
    {
        public ScenarioBunkerExpansionDefinition()
        {
            CellIds = new List<string>();
            BuildPhase = ScenarioBunkerBuildPhase.Start;
            ActiveAtStart = true;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public List<string> CellIds { get; private set; }
        public string BoundaryId { get; set; }
        public ScenarioBunkerBuildPhase BuildPhase { get; set; }
        public bool ActiveAtStart { get; set; }
        public bool LockedAtStart { get; set; }
        public string RequiredMaterialsId { get; set; }
        public string RequiredTechId { get; set; }
        public ScenarioScheduleTime RequiredTime { get; set; }
        public string UnlockGateId { get; set; }
    }
}
