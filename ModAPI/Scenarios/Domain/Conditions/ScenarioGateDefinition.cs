namespace ModAPI.Scenarios
{
    public class ScenarioGateDefinition
    {
        public ScenarioGateDefinition()
        {
            Conditions = new ScenarioConditionGroup();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public ScenarioConditionGroup Conditions { get; set; }
    }
}
