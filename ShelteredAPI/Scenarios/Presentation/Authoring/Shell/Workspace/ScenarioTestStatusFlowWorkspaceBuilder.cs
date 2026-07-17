using System;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>
    /// Dark Slice 9 entry point for the document-only playtest flow. Composition is
    /// intentionally deferred to the final integration slice.
    /// </summary>
    internal sealed class ScenarioTestStatusFlowWorkspaceBuilder
    {
        private readonly ScenarioRuntimeTestAuthoringContentBuilder _contentBuilder;
        private readonly ScenarioAuthoringWorkspaceViewModelFactory _factory;

        public ScenarioTestStatusFlowWorkspaceBuilder(
            ScenarioRuntimeTestAuthoringContentBuilder contentBuilder)
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
            bool active = context.EditorSession != null
                && context.EditorSession.PlaytestState == ScenarioPlaytestState.Playtesting;

            ScenarioAuthoringWorkspaceViewModel workspace = _factory.CreateWorkspace(
                "test",
                ScenarioAuthoringWorkspaceLayoutKind.DocumentOnly,
                string.Empty);
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument(
                "test.status_flow",
                "Test " + scenarioName);
            document.Subtitle = "Prepare, run, and review one playtest in order.";
            document.StatusChips = new[]
            {
                ScenarioAuthoringStatusFlowSupport.Chip(
                    "test.document.status",
                    active ? "Playtest running" : "Ready for review",
                    active ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Informational,
                    null)
            };
            document.Sections = _contentBuilder.BuildStatusFlowSections(context);
            workspace.Document = document;
            return workspace;
        }
    }
}
