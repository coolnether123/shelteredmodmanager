using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Services.Inspector;
using Cortex.Services.Semantics.Context;

namespace Cortex.Modules.Editor
{
    internal interface IEditorContributionRuntime
    {
        IList<WorkbenchEditorAdornment> BuildAdornments(CortexShellState state, DocumentSession session, string surfaceId);

        void Synchronize(CortexShellState state, DocumentSession session, string surfaceId, bool editingEnabled);

        WorkbenchEditorWorkflowResult HandlePointer(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            bool editingEnabled,
            int lineNumber,
            int absolutePosition);

        WorkbenchEditorWorkflowResult HandleKeyboard(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            bool editingEnabled,
            WorkbenchEditorInteractionKey key,
            bool shift,
            bool control,
            bool alt);

        EditorMethodInspectorPreparedView PrepareInspector(CortexShellState state, DocumentSession session);

        WorkbenchMethodInspectorActionResult HandleInspectorAction(string actionId, EditorMethodInspectorPreparedView preparedView);
    }

    internal sealed class EditorContributionRuntime : IEditorContributionRuntime
    {
        private readonly IWorkbenchExtensionRegistry _extensionRegistry;
        private readonly IWorkbenchRuntimeAccess _runtimeAccess;
        private readonly IEditorContextService _contextService;
        private readonly EditorMethodInspectorHostPresentationService _inspectorPresentationService;

        public EditorContributionRuntime(
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtimeAccess,
            IEditorContextService contextService)
        {
            _extensionRegistry = extensionRegistry;
            _runtimeAccess = runtimeAccess;
            _contextService = contextService;
            _inspectorPresentationService = new EditorMethodInspectorHostPresentationService(contextService);
        }

        public IList<WorkbenchEditorAdornment> BuildAdornments(CortexShellState state, DocumentSession session, string surfaceId)
        {
            var contributions = _extensionRegistry != null
                ? _extensionRegistry.GetEditorAdornments()
                : new List<WorkbenchEditorAdornmentContribution>();
            var context = BuildAdornmentContext(state, session, surfaceId);
            var results = new List<WorkbenchEditorAdornment>();
            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (contribution == null || contribution.BuildAdornments == null)
                {
                    continue;
                }

                var adornments = contribution.BuildAdornments(context) ?? new WorkbenchEditorAdornment[0];
                for (var adornmentIndex = 0; adornmentIndex < adornments.Length; adornmentIndex++)
                {
                    if (adornments[adornmentIndex] != null)
                    {
                        results.Add(adornments[adornmentIndex]);
                    }
                }
            }

            results.Sort(delegate(WorkbenchEditorAdornment left, WorkbenchEditorAdornment right)
            {
                var placement = left.Placement.CompareTo(right.Placement);
                if (placement != 0)
                {
                    return placement;
                }

                var sortOrder = left.SortOrder.CompareTo(right.SortOrder);
                return sortOrder != 0
                    ? sortOrder
                    : string.Compare(left.AdornmentId, right.AdornmentId, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public void Synchronize(CortexShellState state, DocumentSession session, string surfaceId, bool editingEnabled)
        {
            var contributions = _extensionRegistry != null
                ? _extensionRegistry.GetEditorWorkflows()
                : new List<WorkbenchEditorWorkflowContribution>();
            var context = BuildWorkflowContext(state, session, surfaceId, editingEnabled);
            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (!IsWorkflowActive(contribution, context) || contribution.Synchronize == null)
                {
                    continue;
                }

                contribution.Synchronize(context);
            }
        }

        public WorkbenchEditorWorkflowResult HandlePointer(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            bool editingEnabled,
            int lineNumber,
            int absolutePosition)
        {
            var contributions = _extensionRegistry != null
                ? _extensionRegistry.GetEditorWorkflows()
                : new List<WorkbenchEditorWorkflowContribution>();
            var context = BuildPointerContext(state, session, surfaceId, editingEnabled, lineNumber, absolutePosition);
            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (!IsWorkflowActive(contribution, context) || contribution.TryHandlePointer == null)
                {
                    continue;
                }

                var result = contribution.TryHandlePointer(context) ?? new WorkbenchEditorWorkflowResult();
                if (result.Handled)
                {
                    return result;
                }
            }

            return new WorkbenchEditorWorkflowResult();
        }

        public WorkbenchEditorWorkflowResult HandleKeyboard(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            bool editingEnabled,
            WorkbenchEditorInteractionKey key,
            bool shift,
            bool control,
            bool alt)
        {
            var contributions = _extensionRegistry != null
                ? _extensionRegistry.GetEditorWorkflows()
                : new List<WorkbenchEditorWorkflowContribution>();
            var context = BuildKeyboardContext(state, session, surfaceId, editingEnabled, key, shift, control, alt);
            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (!IsWorkflowActive(contribution, context) || contribution.TryHandleKeyboard == null)
                {
                    continue;
                }

                var result = contribution.TryHandleKeyboard(context) ?? new WorkbenchEditorWorkflowResult();
                if (result.Handled)
                {
                    return result;
                }
            }

            return new WorkbenchEditorWorkflowResult();
        }

        public EditorMethodInspectorPreparedView PrepareInspector(CortexShellState state, DocumentSession session)
        {
            return _inspectorPresentationService.Prepare(state, session, _extensionRegistry, _runtimeAccess);
        }

        public WorkbenchMethodInspectorActionResult HandleInspectorAction(string actionId, EditorMethodInspectorPreparedView preparedView)
        {
            var contributions = _extensionRegistry != null
                ? _extensionRegistry.GetMethodInspectorSections()
                : new List<WorkbenchMethodInspectorSectionContribution>();
            var context = new WorkbenchMethodInspectorActionContext(
                actionId,
                preparedView != null ? preparedView.Session : null,
                preparedView != null ? preparedView.EditorContext : null,
                preparedView != null ? preparedView.Invocation : null,
                preparedView != null ? preparedView.Relationships : null,
                _runtimeAccess);

            for (var i = 0; i < contributions.Count; i++)
            {
                var contribution = contributions[i];
                if (contribution == null || contribution.TryHandleAction == null)
                {
                    continue;
                }

                var result = contribution.TryHandleAction(context) ?? new WorkbenchMethodInspectorActionResult();
                if (result.Handled)
                {
                    return result;
                }
            }

            return new WorkbenchMethodInspectorActionResult();
        }

        private WorkbenchEditorAdornmentContext BuildAdornmentContext(CortexShellState state, DocumentSession session, string surfaceId)
        {
            var editorContext = ResolveEditorContext(state, surfaceId);
            return new WorkbenchEditorAdornmentContext(
                session,
                editorContext,
                ResolveTarget(state, editorContext),
                _runtimeAccess);
        }

        private WorkbenchEditorWorkflowContext BuildWorkflowContext(CortexShellState state, DocumentSession session, string surfaceId, bool editingEnabled)
        {
            var editorContext = ResolveEditorContext(state, surfaceId);
            return new WorkbenchEditorWorkflowContext(
                session,
                editorContext,
                ResolveTarget(state, editorContext),
                editingEnabled,
                _runtimeAccess);
        }

        private WorkbenchEditorPointerContext BuildPointerContext(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            bool editingEnabled,
            int lineNumber,
            int absolutePosition)
        {
            var editorContext = ResolveEditorContext(state, surfaceId);
            return new WorkbenchEditorPointerContext(
                session,
                editorContext,
                ResolveTarget(state, editorContext),
                editingEnabled,
                WorkbenchEditorPointerKind.PrimaryClick,
                lineNumber,
                absolutePosition,
                _runtimeAccess);
        }

        private WorkbenchEditorKeyboardContext BuildKeyboardContext(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            bool editingEnabled,
            WorkbenchEditorInteractionKey key,
            bool shift,
            bool control,
            bool alt)
        {
            var editorContext = ResolveEditorContext(state, surfaceId);
            return new WorkbenchEditorKeyboardContext(
                session,
                editorContext,
                ResolveTarget(state, editorContext),
                editingEnabled,
                key,
                shift,
                control,
                alt,
                _runtimeAccess);
        }

        private EditorContextSnapshot ResolveEditorContext(CortexShellState state, string surfaceId)
        {
            if (_contextService == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(surfaceId))
            {
                var surfaceContext = _contextService.GetSurfaceContext(state, surfaceId);
                if (surfaceContext != null)
                {
                    return surfaceContext;
                }
            }

            return _contextService.GetActiveContext(state);
        }

        private EditorCommandTarget ResolveTarget(CortexShellState state, EditorContextSnapshot context)
        {
            return _contextService != null && context != null
                ? _contextService.ResolveTarget(state, context.ContextKey ?? string.Empty)
                : null;
        }

        private static bool IsWorkflowActive(WorkbenchEditorWorkflowContribution contribution, WorkbenchEditorWorkflowContext context)
        {
            return contribution != null &&
                (contribution.IsActive == null || contribution.IsActive(context));
        }
    }

    internal sealed class NullEditorContributionRuntime : IEditorContributionRuntime
    {
        public static readonly NullEditorContributionRuntime Instance = new NullEditorContributionRuntime();

        private NullEditorContributionRuntime()
        {
        }

        public IList<WorkbenchEditorAdornment> BuildAdornments(CortexShellState state, DocumentSession session, string surfaceId)
        {
            return new List<WorkbenchEditorAdornment>();
        }

        public void Synchronize(CortexShellState state, DocumentSession session, string surfaceId, bool editingEnabled)
        {
        }

        public WorkbenchEditorWorkflowResult HandlePointer(CortexShellState state, DocumentSession session, string surfaceId, bool editingEnabled, int lineNumber, int absolutePosition)
        {
            return new WorkbenchEditorWorkflowResult();
        }

        public WorkbenchEditorWorkflowResult HandleKeyboard(CortexShellState state, DocumentSession session, string surfaceId, bool editingEnabled, WorkbenchEditorInteractionKey key, bool shift, bool control, bool alt)
        {
            return new WorkbenchEditorWorkflowResult();
        }

        public EditorMethodInspectorPreparedView PrepareInspector(CortexShellState state, DocumentSession session)
        {
            return null;
        }

        public WorkbenchMethodInspectorActionResult HandleInspectorAction(string actionId, EditorMethodInspectorPreparedView preparedView)
        {
            return new WorkbenchMethodInspectorActionResult();
        }
    }
}
