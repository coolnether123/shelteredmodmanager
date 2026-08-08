using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Domain.Stages;
namespace ShelteredScenarioEditor.Application.Stages{
    internal sealed class ScenarioStageContext
    {
        public ScenarioStageDefinition Stage { get; set; }
        public ScenarioAuthoringState State { get; set; }
        public ScenarioEditorSession EditorSession { get; set; }
        public ScenarioAuthoringSession AuthoringSession { get; set; }
    }
}
