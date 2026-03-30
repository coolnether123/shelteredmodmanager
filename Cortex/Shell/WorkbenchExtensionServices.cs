using System;
using System.Collections.Generic;
using Cortex.Plugins.Abstractions;

namespace Cortex.Shell
{
    internal sealed class WorkbenchExtensionRegistry : IWorkbenchExtensionRegistry
    {
        private readonly Dictionary<string, WorkbenchMethodInspectorSectionContribution> _inspectorSections =
            new Dictionary<string, WorkbenchMethodInspectorSectionContribution>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WorkbenchMethodRelationshipActionContribution> _relationshipActions =
            new Dictionary<string, WorkbenchMethodRelationshipActionContribution>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WorkbenchEditorAdornmentContribution> _editorAdornments =
            new Dictionary<string, WorkbenchEditorAdornmentContribution>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, WorkbenchEditorWorkflowContribution> _editorWorkflows =
            new Dictionary<string, WorkbenchEditorWorkflowContribution>(StringComparer.OrdinalIgnoreCase);

        public void RegisterMethodInspectorSection(WorkbenchMethodInspectorSectionContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ContributionId))
            {
                return;
            }

            _inspectorSections[contribution.ContributionId] = contribution;
        }

        public void RegisterMethodRelationshipAction(WorkbenchMethodRelationshipActionContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ContributionId))
            {
                return;
            }

            _relationshipActions[contribution.ContributionId] = contribution;
        }

        public void RegisterEditorAdornment(WorkbenchEditorAdornmentContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ContributionId))
            {
                return;
            }

            _editorAdornments[contribution.ContributionId] = contribution;
        }

        public void RegisterEditorWorkflow(WorkbenchEditorWorkflowContribution contribution)
        {
            if (contribution == null || string.IsNullOrEmpty(contribution.ContributionId))
            {
                return;
            }

            _editorWorkflows[contribution.ContributionId] = contribution;
        }

        public IList<WorkbenchMethodInspectorSectionContribution> GetMethodInspectorSections()
        {
            var results = new List<WorkbenchMethodInspectorSectionContribution>(_inspectorSections.Values);
            results.Sort(delegate(WorkbenchMethodInspectorSectionContribution left, WorkbenchMethodInspectorSectionContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.ContributionId, right.ContributionId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<WorkbenchMethodRelationshipActionContribution> GetMethodRelationshipActions()
        {
            var results = new List<WorkbenchMethodRelationshipActionContribution>(_relationshipActions.Values);
            results.Sort(delegate(WorkbenchMethodRelationshipActionContribution left, WorkbenchMethodRelationshipActionContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.ContributionId, right.ContributionId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<WorkbenchEditorAdornmentContribution> GetEditorAdornments()
        {
            var results = new List<WorkbenchEditorAdornmentContribution>(_editorAdornments.Values);
            results.Sort(delegate(WorkbenchEditorAdornmentContribution left, WorkbenchEditorAdornmentContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.ContributionId, right.ContributionId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<WorkbenchEditorWorkflowContribution> GetEditorWorkflows()
        {
            var results = new List<WorkbenchEditorWorkflowContribution>(_editorWorkflows.Values);
            results.Sort(delegate(WorkbenchEditorWorkflowContribution left, WorkbenchEditorWorkflowContribution right)
            {
                var order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.ContributionId, right.ContributionId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }
    }

    internal sealed class WorkbenchRuntimeAccess : IWorkbenchRuntimeAccess, IWorkbenchModuleRuntimeResolver, IWorkbenchFeedbackRuntime
    {
        private readonly CortexShellState _state;
        private readonly Func<CortexShellModuleCompositionService> _moduleCompositionProvider;

        public WorkbenchRuntimeAccess(
            CortexShellState state,
            Func<CortexShellModuleCompositionService> moduleCompositionProvider)
        {
            _state = state;
            _moduleCompositionProvider = moduleCompositionProvider;
        }

        public IWorkbenchModuleRuntimeResolver Modules
        {
            get { return this; }
        }

        public IWorkbenchFeedbackRuntime Feedback
        {
            get { return this; }
        }

        public IWorkbenchModuleRuntime Get(string moduleId)
        {
            var composition = _moduleCompositionProvider != null ? _moduleCompositionProvider() : null;
            return composition != null ? composition.GetRuntimeByModuleId(moduleId) : null;
        }

        public IWorkbenchModuleRuntime GetByContainer(string containerId)
        {
            var composition = _moduleCompositionProvider != null ? _moduleCompositionProvider() : null;
            return composition != null ? composition.GetRuntime(containerId) : null;
        }

        public string GetStatusMessage()
        {
            return _state != null ? _state.StatusMessage ?? string.Empty : string.Empty;
        }

        public void SetStatusMessage(string message)
        {
            if (_state != null)
            {
                _state.StatusMessage = message ?? string.Empty;
            }
        }
    }
}
