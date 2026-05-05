using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Domain.Objects;
namespace ShelteredAPI.Scenarios.Domain.Runtime{
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
