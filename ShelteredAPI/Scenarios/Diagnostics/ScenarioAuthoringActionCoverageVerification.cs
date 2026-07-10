using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioAuthoringActionCoverageVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioAuthoringInspectorAction[] rendererActions = ScenarioAuthoringRendererActionManifest.Build(
                new ScenarioAuthoringState(),
                new ScenarioAuthoringShellWindowViewModel[0],
                null);
            ScenarioAuthoringShellViewModel shell = new ScenarioAuthoringShellViewModel
            {
                RendererActions = rendererActions,
                Windows = new ScenarioAuthoringShellWindowViewModel[0]
            };
            ScenarioAuthoringShellWindowViewModel contractWindow = ScenarioAuthoringRendererActionManifest.BuildContractWindow(shell);
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            ScenarioAuthoringInspectorItem[] items = contractWindow != null
                && contractWindow.Sections != null
                && contractWindow.Sections.Length > 0
                ? contractWindow.Sections[0].Items
                : null;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = items[i] != null ? items[i].Action : null;
                if (action != null && !string.IsNullOrEmpty(action.Id)) ids.Add(action.Id);
            }

            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererMapFilterTogglePrefix, "map filter", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererPixelGroupTogglePrefix, "pixel group", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererHomeGroupTogglePrefix, "Home group", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererAssetCategorySelectPrefix, "asset category", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, "candidate search", result);
            RequireFamily(ids, ScenarioAuthoringActionIds.ActionRendererCandidateFilterPrefix, "candidate filter", result);
            Require(ids, ScenarioAuthoringActionIds.ActionRendererAssetSearchClear, "asset search clear", result);
            Require(ids, ScenarioAuthoringActionIds.ActionRendererPlacementBack, "placement Back", result);
            Require(ids, ScenarioAuthoringActionIds.ActionRendererPlacementDone, "placement Done", result);
            Require(ids, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.snap_to_grid", "snap toggle", result);
            Require(ids, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.show_grid", "grid toggle", result);
        }

        private static void RequireFamily(HashSet<string> ids, string prefix, string label, ScenarioValidationResult result)
        {
            foreach (string id in ids)
                if (id.StartsWith(prefix, StringComparison.Ordinal)) return;
            result.AddError("Authoring shell contract did not expose the " + label + " action family.");
        }

        private static void Require(HashSet<string> ids, string id, string label, ScenarioValidationResult result)
        {
            if (!ids.Contains(id)) result.AddError("Authoring shell contract did not expose " + label + " action '" + id + "'.");
        }
    }
}
