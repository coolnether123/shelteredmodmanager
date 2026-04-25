namespace ModAPI.Scenarios
{
    public enum ScenarioBunkerBuildPhase
    {
        Start = 0,
        Locked = 1,
        Scheduled = 2,
        GateUnlocked = 3
    }

    public class ScenarioBunkerCellDefinition
    {
        public ScenarioBunkerCellDefinition()
        {
            BuildPhase = ScenarioBunkerBuildPhase.Start;
            ActiveAtStart = true;
        }

        public string Id { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        public string Zone { get; set; }
        public string FoundationId { get; set; }
        public string ExpansionId { get; set; }
        public ScenarioBunkerBuildPhase BuildPhase { get; set; }
        public bool ActiveAtStart { get; set; }
        public bool LockedAtStart { get; set; }
        public string RequiredMaterialsId { get; set; }
        public string RequiredTechId { get; set; }
        public ScenarioScheduleTime RequiredTime { get; set; }
        public string UnlockGateId { get; set; }
    }
}
