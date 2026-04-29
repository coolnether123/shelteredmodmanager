using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal class ScenarioObjectRuntimeStateRecord
    {
        public string ScenarioObjectId { get; set; }
        public string RuntimeBindingKey { get; set; }
        public ScenarioObjectStartState State { get; set; }
        public bool Active { get; set; }
        public bool Locked { get; set; }
        public bool Hidden { get; set; }
    }
}
