using System;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Presentation.Inspector;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal static class ScenarioPlaytestFixActionResolver
    {
        public static ScenarioAuthoringInspectorAction BuildFixAction(string disabledReason)
        {
            if (string.IsNullOrEmpty(disabledReason))
                return null;

            if (Contains(disabledReason, "starting survivor"))
                return Item.Action(
                    ShellUxCommand.SelectStage(ScenarioStageKind.People),
                    "Open Cast",
                    "Add at least one starting survivor before playtest starts.",
                    true,
                    false,
                    "CAST");

            if (Contains(disabledReason, "unsaved")
                || Contains(disabledReason, "save draft")
                || Contains(disabledReason, "save before"))
                return Item.Action(
                    EditorLifecycleCommand.SaveDraft,
                    "Save Draft",
                    "Persist the current draft so playtest can apply a stable snapshot.",
                    true,
                    true,
                    "SV");

            if (Contains(disabledReason, "validation")
                || Contains(disabledReason, "blocked")
                || Contains(disabledReason, "error"))
                return Item.Action(
                    ShellUxCommand.SelectStage(ScenarioStageKind.Publish),
                    "Open Package / Export",
                    "Review validation blockers and fix them in their source pages.",
                    true,
                    false,
                    "PUB");

            return Item.Action(
                ShellUxCommand.SelectStage(ScenarioStageKind.Publish),
                "Open Package / Export",
                "Open the package checks for the concrete fix.",
                true,
                false,
                "PUB");
        }

        private static bool Contains(string source, string token)
        {
            return source != null && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
