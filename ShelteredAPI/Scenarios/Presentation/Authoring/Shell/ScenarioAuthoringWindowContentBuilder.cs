using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioAuthoringWindowContentBuilder
    {
        ScenarioAuthoringWindowContentKind ContentKind { get; }

        ScenarioAuthoringInspectorSection[] Build(
            ScenarioAuthoringWindowContentContext context);
    }

    internal sealed class ScenarioAuthoringWindowContentContext
    {
        public ScenarioAuthoringWindowContentContext(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            State = state;
            EditorSession = editorSession;
            Session = session;
            Definition = definition;
        }

        public ScenarioAuthoringState State { get; private set; }
        public ScenarioEditorSession EditorSession { get; private set; }
        public ScenarioAuthoringSession Session { get; private set; }
        public ScenarioDefinition Definition { get; private set; }
    }

    internal sealed class DelegateScenarioAuthoringWindowContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        private readonly Func<ScenarioAuthoringWindowContentContext, ScenarioAuthoringInspectorSection[]> _build;

        public DelegateScenarioAuthoringWindowContentBuilder(
            ScenarioAuthoringWindowContentKind contentKind,
            Func<ScenarioAuthoringWindowContentContext, ScenarioAuthoringInspectorSection[]> build)
        {
            ContentKind = contentKind;
            _build = build;
        }

        public ScenarioAuthoringWindowContentKind ContentKind { get; private set; }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            return _build != null ? _build(context) : new ScenarioAuthoringInspectorSection[0];
        }
    }
}
