using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Domain.Stages;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    /// <summary>
    /// Single source of truth for "what should the author fix next?". Ranks the
    /// validation issues (blocking errors before advisory warnings, each in the
    /// order the validator produced them) and resolves the one top issue to the
    /// existing fix/navigation actions - the playtest fix resolver for the crisp
    /// starting-survivor / unsaved cases, and the publish issue-row navigation for
    /// every other domain. Home surfaces this so "6 warnings" also says what to do.
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

            // Prefer the playtest fix resolver only where it recognized a specific,
            // crisp blocker (Open Cast / Save Draft); otherwise fall back to the
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
