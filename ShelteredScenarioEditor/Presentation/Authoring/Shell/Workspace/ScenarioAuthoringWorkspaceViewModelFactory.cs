using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    /// <summary>
    /// Shared null-safe defaults and semantic action construction for workspace builders.
    /// </summary>
    internal sealed class ScenarioAuthoringWorkspaceViewModelFactory
    {
        public ScenarioAuthoringWorkspaceViewModel CreateWorkspace(
            string id,
            ScenarioAuthoringWorkspaceLayoutKind layoutKind,
            string activeSubtabId)
        {
            return new ScenarioAuthoringWorkspaceViewModel
            {
                Id = id,
                LayoutKind = layoutKind,
                ActiveSubtabId = activeSubtabId,
                Subtabs = new ScenarioAuthoringWorkspaceSubtabViewModel[0]
            };
        }

        public ScenarioAuthoringNavigatorViewModel CreateNavigator(string id)
        {
            return new ScenarioAuthoringNavigatorViewModel
            {
                Id = id,
                SearchText = string.Empty,
                Groups = new ScenarioAuthoringNavigatorGroupViewModel[0]
            };
        }

        public ScenarioAuthoringWorkspaceDocumentViewModel CreateDocument(string id, string title)
        {
            return new ScenarioAuthoringWorkspaceDocumentViewModel
            {
                Id = id,
                Title = title,
                Breadcrumbs = new ScenarioAuthoringBreadcrumbViewModel[0],
                StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                HeaderActions = new ScenarioAuthoringInspectorAction[0],
                Sections = new ScenarioAuthoringInspectorSection[0]
            };
        }

        public ScenarioAuthoringInspectorSection CreateSection(string id, string title, bool isAdvanced)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                IsAdvanced = isAdvanced,
                StatusChips = new ScenarioAuthoringStatusChipViewModel[0],
                Items = new ScenarioAuthoringInspectorItem[0]
            };
        }

        public ScenarioAuthoringCompactChoiceViewModel CreateChoice(string id, string label, int columnCount)
        {
            return new ScenarioAuthoringCompactChoiceViewModel
            {
                Id = id,
                Label = label,
                ColumnCount = columnCount,
                Options = new ScenarioAuthoringCompactChoiceOptionViewModel[0]
            };
        }

        public ScenarioAuthoringInspectorAction CreateSubtabAction(string workspaceId, string subtabId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.SelectWorkspaceSubtab, RendererInteractionAutomationIds.ActionRendererWorkspaceSubtabSelectPrefix, workspaceId, subtabId, string.Empty, label);
        }

        public ScenarioAuthoringInspectorAction CreateEntityAction(string workspaceId, string subtabId, string entityId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.SelectWorkspaceEntity, RendererInteractionAutomationIds.ActionRendererWorkspaceEntitySelectPrefix, workspaceId, subtabId, entityId, label);
        }

        public ScenarioAuthoringInspectorAction CreateWarningAction(string workspaceId, string subtabId, string entityId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.OpenWorkspaceWarning, RendererInteractionAutomationIds.ActionRendererWorkspaceWarningOpenPrefix, workspaceId, subtabId, entityId, label);
        }

        public ScenarioAuthoringInspectorAction CreateGroupToggleAction(string workspaceId, string subtabId, string groupId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.ToggleWorkspaceGroup, RendererInteractionAutomationIds.ActionRendererWorkspaceGroupTogglePrefix, workspaceId, subtabId, groupId, label);
        }

        public ScenarioAuthoringInspectorAction CreateRowToggleAction(string workspaceId, string subtabId, string entityId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.ToggleWorkspaceRow, RendererInteractionAutomationIds.ActionRendererWorkspaceRowTogglePrefix, workspaceId, subtabId, entityId, label);
        }

        public ScenarioAuthoringInspectorAction CreateSearchAction(string workspaceId, string subtabId, string value, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.SetWorkspaceSearch, RendererInteractionAutomationIds.ActionRendererWorkspaceSearchSetPrefix, workspaceId, subtabId, value, label);
        }

        public ScenarioAuthoringInspectorAction CreateBreadcrumbAction(string workspaceId, string subtabId, string entityId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.SelectWorkspaceBreadcrumb, RendererInteractionAutomationIds.ActionRendererWorkspaceBreadcrumbSelectPrefix, workspaceId, subtabId, entityId, label);
        }

        public ScenarioAuthoringInspectorAction CreateBackAction(string workspaceId, string subtabId, string label)
        {
            return CreateWorkspaceAction(RendererInteractionCommandKind.ShowWorkspaceNavigator, RendererInteractionAutomationIds.ActionRendererWorkspaceBackPrefix, workspaceId, subtabId, string.Empty, label);
        }

        public ScenarioAuthoringInspectorAction CreateWorkspaceAction(
            RendererInteractionCommandKind kind,
            string prefix,
            string workspaceId,
            string subtabId,
            string value,
            string label)
        {
            RendererInteractionCommand command = RendererInteractionCommand.ForWorkspace(kind, prefix, workspaceId, subtabId, value);
            return RendererInteractionActionFactory.Create(command, label, false);
        }

        public static string BuildWorkspaceActionId(
            string prefix,
            string workspaceId,
            string subtabId,
            string value)
        {
            string payload = (workspaceId ?? string.Empty)
                + "\n" + (subtabId ?? string.Empty)
                + "\n" + (value ?? string.Empty);
            return (prefix ?? string.Empty) + ScenarioAutomationIdCodec.EncodeToken(payload);
        }
    }
}
