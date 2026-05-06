using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioOverviewAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Scenario; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_summary",
                    Title = "Scenario",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Item.Property("Draft", Item.Safe(state != null ? state.ActiveDraftId : null)),
                        Item.Property("Base Mode", editorSession != null && editorSession.WorkingDefinition != null ? editorSession.WorkingDefinition.BaseGameMode.ToString() : "Unknown"),
                        Item.Property("Simulation", ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Running (test)" : "Paused for workshop"),
                        Item.Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"),
                        Item.Property("Applied To World", editorSession != null && editorSession.HasAppliedToCurrentWorld ? "Yes" : "No"),
                        Item.Property("Dirty Sections", Item.CountDirtyFlags(editorSession).ToString())
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_actions",
                    Title = "Actions",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = new[]
                    {
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScenarioModePrevious, "Mode -", "Switch to the previous scenario base mode.", true, false, "M-")),
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScenarioModeNext, "Mode +", "Switch to the next scenario base mode.", true, false, "M+")),
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario draft XML.", true, false, "SV")),
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionPlaytest, editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Test" : "Start Test Scenario", "Toggle scenario playtest mode.", true, false, "TS"))
                    }
                }
            };
        }
    }
}
