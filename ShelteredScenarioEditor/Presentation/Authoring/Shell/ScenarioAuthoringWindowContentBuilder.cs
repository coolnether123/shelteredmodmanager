using System;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
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
            ScenarioDefinition definition,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
            : this(state, editorSession, session, definition, null, rendererInteraction)
        {
        }

        public ScenarioAuthoringWindowContentContext(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition,
            ScenarioAuthoringInspectorSection[] backdropSections,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            State = state;
            EditorSession = editorSession;
            Session = session;
            Definition = definition;
            BackdropSections = backdropSections ?? new ScenarioAuthoringInspectorSection[0];
            RendererInteraction = rendererInteraction;
        }

        public ScenarioAuthoringState State { get; private set; }
        public ScenarioEditorSession EditorSession { get; private set; }
        public ScenarioAuthoringSession Session { get; private set; }
        public ScenarioDefinition Definition { get; private set; }
        public ScenarioAuthoringInspectorSection[] BackdropSections { get; private set; }
        public ScenarioAuthoringRendererInteractionState RendererInteraction { get; private set; }
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
