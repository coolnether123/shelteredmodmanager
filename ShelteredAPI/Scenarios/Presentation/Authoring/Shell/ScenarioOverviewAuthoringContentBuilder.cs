using System;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
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
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            ScenarioScoringAuthoringSummary.Summary scoring = ScenarioScoringAuthoringSummary.Build(definition);
            bool showAdvancedDetails = state != null && state.Settings != null && state.Settings.GetBool("debug.show_advanced_details", false);
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_summary",
                    Title = "Scenario",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = BuildSummaryItems(state, editorSession, definition, scoring, showAdvancedDetails)
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_actions",
                    Title = "Actions",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = new[]
                    {
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScenarioModePrevious, "Mode: " + ResolveAdjacentModeName(definition, -1), "Switch to the previous scenario base mode.", true, false, "M-")),
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScenarioModeNext, "Mode: " + ResolveAdjacentModeName(definition, 1), "Switch to the next scenario base mode.", true, false, "M+")),
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario draft XML.", true, false, "SV")),
                        Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionPlaytest, editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Test" : "Start Test Scenario", "Toggle scenario playtest mode.", true, false, "TS"))
                    }
                }
            };
        }

        private static ScenarioAuthoringInspectorItem[] BuildSummaryItems(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition,
            ScenarioScoringAuthoringSummary.Summary scoring,
            bool showAdvancedDetails)
        {
            System.Collections.Generic.List<ScenarioAuthoringInspectorItem> items = new System.Collections.Generic.List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Title", Item.Safe(definition != null ? definition.DisplayName : null)));
            items.Add(Item.Property("Base Mode", definition != null ? definition.BaseGameMode.ToString() : "Unknown"));
            items.Add(Item.Property("Save State", Item.CountDirtyFlags(editorSession) == 0 ? "Saved" : "Unsaved changes"));
            items.Add(Item.Property("Simulation", ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Running (test)" : "Paused for workshop"));
            items.Add(Item.Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"));
            items.Add(Item.Property("Applied To World", FormatAppliedState(editorSession)));
            items.Add(Item.Property("Scoring", scoring.Status));
            items.Add(Item.Property("Score Rules", scoring.RuleCount.ToString()));
            if (showAdvancedDetails)
            {
                items.Add(Item.Property("Draft Id", Item.Safe(state != null ? state.ActiveDraftId : null)));
                items.Add(Item.Property("Dirty Sections", Item.CountDirtyFlags(editorSession).ToString()));
            }

            return items.ToArray();
        }

        private static string ResolveAdjacentModeName(ScenarioDefinition definition, int direction)
        {
            ScenarioBaseGameMode mode = definition != null ? definition.BaseGameMode : ScenarioBaseGameMode.Survival;
            int count = Enum.GetValues(typeof(ScenarioBaseGameMode)).Length;
            int next = ((int)mode + direction + count) % count;
            return ((ScenarioBaseGameMode)next).ToString();
        }

        private static string FormatAppliedState(ScenarioEditorSession editorSession)
        {
            if (editorSession == null || !editorSession.HasAppliedToCurrentWorld)
                return "No";
            return editorSession.HasUnappliedDraftChanges ? "Stale" : "Yes";
        }
    }
}
