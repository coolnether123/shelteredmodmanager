using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioStageContext
    {
        public ScenarioStageDefinition Stage { get; set; }
        public ScenarioAuthoringState State { get; set; }
        public ScenarioEditorSession EditorSession { get; set; }
        public ScenarioAuthoringSession AuthoringSession { get; set; }
    }
}
