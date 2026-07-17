using System;
using System.Collections.Generic;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>
    /// Dark Slice 9 entry point for the document-only publish checklist. Composition
    /// is intentionally deferred to the final integration slice.
    /// </summary>
    internal sealed class ScenarioPublishStatusFlowWorkspaceBuilder
    {
        private readonly ScenarioPublishAuthoringContentBuilder _contentBuilder;
        private readonly ScenarioAuthoringWorkspaceViewModelFactory _factory;

        public ScenarioPublishStatusFlowWorkspaceBuilder(
            ScenarioPublishAuthoringContentBuilder contentBuilder)
        {
            if (contentBuilder == null)
                throw new ArgumentNullException("contentBuilder");
            _contentBuilder = contentBuilder;
            _factory = new ScenarioAuthoringWorkspaceViewModelFactory();
        }

        public ScenarioAuthoringWorkspaceViewModel Build(ScenarioAuthoringWindowContentContext context)
        {
            if (context == null)
                return null;

            ScenarioDefinition definition = context.Definition;
            string scenarioName = ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                definition != null ? definition.DisplayName : null,
                null,
                definition != null ? definition.Id : null,
                "Current scenario").Text;
            ScenarioAuthoringInspectorSection[] sections = _contentBuilder.BuildStatusFlowSections(context);

            ScenarioAuthoringWorkspaceViewModel workspace = _factory.CreateWorkspace(
                "publish",
                ScenarioAuthoringWorkspaceLayoutKind.DocumentOnly,
                string.Empty);
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument(
                "publish.status_flow",
                "Publish " + scenarioName);
            document.Subtitle = "Resolve blockers, review the package, then export it locally.";
            document.Sections = sections;
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null
                    || !string.Equals(section.Id, "publish_validation", StringComparison.OrdinalIgnoreCase)
                    || section.StatusChips == null)
                    continue;
                ScenarioAuthoringStatusChipViewModel[] chips = new ScenarioAuthoringStatusChipViewModel[section.StatusChips.Length];
                Array.Copy(section.StatusChips, chips, chips.Length);
                document.StatusChips = chips;
                break;
            }
            workspace.Document = document;
            return workspace;
        }
    }

    /// <summary>Shared construction primitives for Slice 9 status-flow projections.</summary>
    internal static class ScenarioAuthoringStatusFlowSupport
    {
        public static ScenarioAuthoringInspectorSection Section(
            string id,
            string title,
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioAuthoringInspectorSectionLayout layout)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = layout,
                StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                Items = items != null ? items.ToArray() : new ScenarioAuthoringInspectorItem[0]
            };
        }

        public static ScenarioAuthoringStatusChipViewModel Chip(
            string id,
            string text,
            ScenarioAuthoringStatusTone tone,
            ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringStatusChipViewModel
            {
                Id = id,
                Text = text,
                Tone = tone,
                Action = action
            };
        }
    }
}
