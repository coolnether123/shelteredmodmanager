using System;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Domain.Stages;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    /// <summary>
    /// Ranks validation issues with errors before warnings while preserving validator order.
    /// Starting-survivor and unsaved cases use the playtest fix resolver; other issues use
    /// publish-row navigation.
    /// </summary>
    internal static class ScenarioTopIssueResolver
    {
        internal static ScenarioValidationIssue ResolveTopIssue(ScenarioAuthoringValidationSnapshot validation)
        {
            if (validation == null || !validation.ValidationAvailable)
                return null;

            ScenarioValidationIssue[] issues = validation.Issues;
            if (issues == null || issues.Length == 0)
                return null;

            for (int i = 0; i < issues.Length; i++)
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                    return issues[i];

            for (int i = 0; i < issues.Length; i++)
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Warning)
                    return issues[i];

            return null;
        }

        internal static ScenarioAuthoringInspectorAction BuildNextAction(ScenarioValidationIssue issue)
        {
            if (issue == null)
                return null;

            // Use the playtest fix resolver for its specific Open Cast and Save Draft
            // blockers; otherwise fall back to the
            // publish issue rows for stage-specific navigation actions.
            ScenarioAuthoringInspectorAction fix = ScenarioPlaytestFixActionResolver.BuildFixAction(issue.Message);
            if (fix != null && IsSpecificPlaytestFix(fix.Id))
                return fix;

            return ScenarioPublishAuthoringContentBuilder.BuildIssueNavigationAction(issue);
        }

        private static bool IsSpecificPlaytestFix(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
                return false;
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionSave, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.People, StringComparison.Ordinal);
        }
    }
}
