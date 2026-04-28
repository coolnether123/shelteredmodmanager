namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringContext
    {
        public ScenarioAuthoringState State { get; set; }
        public ScenarioEditorSession EditorSession { get; set; }
        public ScenarioAuthoringSession AuthoringSession { get; set; }
    }
}
