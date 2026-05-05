using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
namespace ShelteredAPI.Scenarios.Presentation.Inspector{
    internal sealed class InspectorViewModelBuilder
    {
        public ScenarioAuthoringInspectorSection BuildSessionSection(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession,
            string stageLabel)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Draft", ScenarioInspectorItemFactory.Safe(state != null ? state.ActiveDraftId : null)));
            items.Add(ScenarioInspectorItemFactory.Property("Scenario File", ScenarioInspectorItemFactory.Safe(state != null ? state.ActiveScenarioFilePath : null)));
            items.Add(ScenarioInspectorItemFactory.Property("Stage", ScenarioInspectorItemFactory.Safe(stageLabel)));
            items.Add(ScenarioInspectorItemFactory.Property("Tool", state != null ? state.ActiveTool.ToString() : "Unknown"));
            items.Add(ScenarioInspectorItemFactory.Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"));
            items.Add(ScenarioInspectorItemFactory.Property("Dirty Sections", editorSession != null ? editorSession.DirtyFlags.Count.ToString() : "0"));
            items.Add(ScenarioInspectorItemFactory.Property(
                "Base Mode",
                editorSession != null && editorSession.WorkingDefinition != null
                    ? editorSession.WorkingDefinition.BaseGameMode.ToString()
                    : authoringSession != null ? authoringSession.BaseMode.ToString() : "Unknown"));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "session",
                Title = "Session",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = items.ToArray()
            };
        }

        public ScenarioAuthoringInspectorSection BuildStatusSection(string statusMessage)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "status",
                Title = "Status",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text(string.IsNullOrEmpty(statusMessage) ? "Ready." : statusMessage)
                }
            };
        }
    }
}
