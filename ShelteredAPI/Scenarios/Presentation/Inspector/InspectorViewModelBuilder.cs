using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class InspectorViewModelBuilder
    {
        public ScenarioAuthoringInspectorSection BuildSessionSection(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession,
            string stageLabel)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Draft", Safe(state != null ? state.ActiveDraftId : null)));
            items.Add(Property("Scenario File", Safe(state != null ? state.ActiveScenarioFilePath : null)));
            items.Add(Property("Stage", Safe(stageLabel)));
            items.Add(Property("Tool", state != null ? state.ActiveTool.ToString() : "Unknown"));
            items.Add(Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"));
            items.Add(Property("Dirty Sections", editorSession != null ? editorSession.DirtyFlags.Count.ToString() : "0"));
            items.Add(Property("Base Mode", authoringSession != null ? authoringSession.BaseMode.ToString() : "Unknown"));
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
                    new ScenarioAuthoringInspectorItem
                    {
                        Kind = ScenarioAuthoringInspectorItemKind.Text,
                        Value = string.IsNullOrEmpty(statusMessage) ? "Ready." : statusMessage
                    }
                }
            };
        }

        private static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value
            };
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }
    }
}
