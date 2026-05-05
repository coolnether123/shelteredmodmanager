using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;
namespace ShelteredAPI.Scenarios.Application.Stages{
    internal sealed class ScenarioStageContext
    {
        public ScenarioStageDefinition Stage { get; set; }
        public ScenarioAuthoringState State { get; set; }
        public ScenarioEditorSession EditorSession { get; set; }
        public ScenarioAuthoringSession AuthoringSession { get; set; }
    }
}
