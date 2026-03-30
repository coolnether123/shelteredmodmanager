using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Presentation.Models;

namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// Composition root for runtime access needed by contributed workbench extensions.
    /// </summary>
    public interface IWorkbenchRuntimeAccess
    {
        IWorkbenchModuleRuntimeResolver Modules { get; }

        IWorkbenchFeedbackRuntime Feedback { get; }
    }

    /// <summary>
    /// Resolves module runtimes for contributed commands and extension callbacks.
    /// </summary>
    public interface IWorkbenchModuleRuntimeResolver
    {
        IWorkbenchModuleRuntime Get(string moduleId);

        IWorkbenchModuleRuntime GetByContainer(string containerId);
    }

    /// <summary>
    /// Allows extensions to publish short-lived host feedback without reaching into shell state.
    /// </summary>
    public interface IWorkbenchFeedbackRuntime
    {
        string GetStatusMessage();

        void SetStatusMessage(string message);
    }

    /// <summary>
    /// Registry of runtime-driven workbench extension contributions.
    /// </summary>
    public interface IWorkbenchExtensionRegistry
    {
        void RegisterMethodInspectorSection(WorkbenchMethodInspectorSectionContribution contribution);

        void RegisterMethodRelationshipAction(WorkbenchMethodRelationshipActionContribution contribution);

        void RegisterEditorAdornment(WorkbenchEditorAdornmentContribution contribution);

        void RegisterEditorWorkflow(WorkbenchEditorWorkflowContribution contribution);

        IList<WorkbenchMethodInspectorSectionContribution> GetMethodInspectorSections();

        IList<WorkbenchMethodRelationshipActionContribution> GetMethodRelationshipActions();

        IList<WorkbenchEditorAdornmentContribution> GetEditorAdornments();

        IList<WorkbenchEditorWorkflowContribution> GetEditorWorkflows();
    }

    /// <summary>
    /// Public snapshot of method relationships for inspector extensions.
    /// </summary>
    public sealed class WorkbenchMethodRelationshipsSnapshot
    {
        public WorkbenchMethodRelationshipsSnapshot()
        {
            IncomingCalls = new WorkbenchMethodRelationship[0];
            OutgoingCalls = new WorkbenchMethodRelationship[0];
            StatusMessage = string.Empty;
        }

        public bool IsExpanded { get; set; }

        public bool IsLoading { get; set; }

        public bool HasResponse { get; set; }

        public string StatusMessage { get; set; }

        public WorkbenchMethodRelationship[] IncomingCalls { get; set; }

        public WorkbenchMethodRelationship[] OutgoingCalls { get; set; }

        public int IncomingCallCount { get; set; }

        public int OutgoingCallCount { get; set; }
    }

    /// <summary>
    /// Public method-relationship entry used by inspector extensions.
    /// </summary>
    public sealed class WorkbenchMethodRelationship
    {
        public WorkbenchMethodRelationship()
        {
            Title = string.Empty;
            Detail = string.Empty;
            SymbolKind = string.Empty;
            MetadataName = string.Empty;
            ContainingTypeName = string.Empty;
            ContainingAssemblyName = string.Empty;
            DocumentationCommentId = string.Empty;
            DefinitionDocumentPath = string.Empty;
            Relationship = string.Empty;
        }

        public string Title { get; set; }

        public string Detail { get; set; }

        public string SymbolKind { get; set; }

        public string MetadataName { get; set; }

        public string ContainingTypeName { get; set; }

        public string ContainingAssemblyName { get; set; }

        public string DocumentationCommentId { get; set; }

        public string DefinitionDocumentPath { get; set; }

        public LanguageServiceRange DefinitionRange { get; set; }

        public string Relationship { get; set; }

        public int CallCount { get; set; }
    }

    /// <summary>
    /// Inspector context passed to contributed sections and actions.
    /// </summary>
    public class WorkbenchMethodInspectorContext
    {
        public WorkbenchMethodInspectorContext(
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandInvocation invocation,
            WorkbenchMethodRelationshipsSnapshot relationships,
            IWorkbenchRuntimeAccess runtime)
        {
            Session = session;
            EditorContext = editorContext != null ? editorContext.Clone() : null;
            Invocation = CloneInvocation(invocation);
            Relationships = relationships ?? new WorkbenchMethodRelationshipsSnapshot();
            Runtime = runtime;
        }

        public DocumentSession Session { get; private set; }

        public EditorContextSnapshot EditorContext { get; private set; }

        public EditorCommandInvocation Invocation { get; private set; }

        public EditorCommandTarget Target
        {
            get { return Invocation != null ? Invocation.Target : null; }
        }

        public WorkbenchMethodRelationshipsSnapshot Relationships { get; private set; }

        public IWorkbenchRuntimeAccess Runtime { get; private set; }

        protected static EditorCommandInvocation CloneInvocation(EditorCommandInvocation invocation)
        {
            if (invocation == null)
            {
                return null;
            }

            return new EditorCommandInvocation
            {
                ActiveContainerId = invocation.ActiveContainerId ?? string.Empty,
                ActiveDocumentId = invocation.ActiveDocumentId ?? string.Empty,
                FocusedRegionId = invocation.FocusedRegionId ?? string.Empty,
                Target = invocation.Target != null ? invocation.Target.Clone() : null
            };
        }
    }

    /// <summary>
    /// Inspector action context used when a contributed section action is invoked.
    /// </summary>
    public sealed class WorkbenchMethodInspectorActionContext : WorkbenchMethodInspectorContext
    {
        public WorkbenchMethodInspectorActionContext(
            string actionId,
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandInvocation invocation,
            WorkbenchMethodRelationshipsSnapshot relationships,
            IWorkbenchRuntimeAccess runtime)
            : base(session, editorContext, invocation, relationships, runtime)
        {
            ActionId = actionId ?? string.Empty;
        }

        public string ActionId { get; private set; }
    }

    /// <summary>
    /// Result returned when a contributed inspector action is invoked.
    /// </summary>
    public sealed class WorkbenchMethodInspectorActionResult
    {
        public bool Handled { get; set; }

        public bool CloseInspector { get; set; }
    }

    /// <summary>
    /// Relationship context passed to contributed relationship-action providers.
    /// </summary>
    public sealed class WorkbenchMethodRelationshipActionContext : WorkbenchMethodInspectorContext
    {
        public WorkbenchMethodRelationshipActionContext(
            WorkbenchMethodRelationship relationship,
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandInvocation invocation,
            WorkbenchMethodRelationshipsSnapshot relationships,
            IWorkbenchRuntimeAccess runtime)
            : base(session, editorContext, invocation, relationships, runtime)
        {
            Relationship = relationship ?? new WorkbenchMethodRelationship();
        }

        public WorkbenchMethodRelationship Relationship { get; private set; }
    }

    /// <summary>
    /// Contributes relationship-card actions for the method inspector.
    /// </summary>
    public sealed class WorkbenchMethodRelationshipActionContribution
    {
        public string ContributionId;
        public int SortOrder;
        public Func<WorkbenchMethodRelationshipActionContext, MethodInspectorActionViewModel[]> BuildActions;
    }

    /// <summary>
    /// Contributes an orderable method-inspector section and optional action handler.
    /// </summary>
    public sealed class WorkbenchMethodInspectorSectionContribution
    {
        public string ContributionId;
        public int SortOrder;
        public bool DefaultExpanded = true;
        public Func<WorkbenchMethodInspectorContext, bool> CanDisplay;
        public Func<WorkbenchMethodInspectorContext, MethodInspectorSectionViewModel> BuildSection;
        public Func<WorkbenchMethodInspectorActionContext, WorkbenchMethodInspectorActionResult> TryHandleAction;
    }

    public enum WorkbenchEditorAdornmentPlacement
    {
        TopRight = 0
    }

    /// <summary>
    /// Editor context passed to contributed adornments.
    /// </summary>
    public sealed class WorkbenchEditorAdornmentContext
    {
        public WorkbenchEditorAdornmentContext(
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandTarget target,
            IWorkbenchRuntimeAccess runtime)
        {
            Session = session;
            EditorContext = editorContext != null ? editorContext.Clone() : null;
            Target = target != null ? target.Clone() : null;
            Runtime = runtime;
        }

        public DocumentSession Session { get; private set; }

        public EditorContextSnapshot EditorContext { get; private set; }

        public EditorCommandTarget Target { get; private set; }

        public IWorkbenchRuntimeAccess Runtime { get; private set; }
    }

    /// <summary>
    /// Host-rendered editor adornment definition.
    /// </summary>
    public sealed class WorkbenchEditorAdornment
    {
        public string AdornmentId;
        public string Label;
        public string ToolTip;
        public string CommandId;
        public object CommandParameter;
        public WorkbenchEditorAdornmentPlacement Placement;
        public bool Enabled = true;
        public int SortOrder;
    }

    /// <summary>
    /// Contributes host-rendered editor adornments.
    /// </summary>
    public sealed class WorkbenchEditorAdornmentContribution
    {
        public string ContributionId;
        public int SortOrder;
        public Func<WorkbenchEditorAdornmentContext, WorkbenchEditorAdornment[]> BuildAdornments;
    }

    public enum WorkbenchEditorInteractionKey
    {
        None = 0,
        Tab = 1,
        Escape = 2,
        Enter = 3
    }

    public enum WorkbenchEditorPointerKind
    {
        PrimaryClick = 0
    }

    /// <summary>
    /// Base editor-workflow context shared by synchronization and interaction callbacks.
    /// </summary>
    public class WorkbenchEditorWorkflowContext
    {
        public WorkbenchEditorWorkflowContext(
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandTarget target,
            bool editingEnabled,
            IWorkbenchRuntimeAccess runtime)
        {
            Session = session;
            EditorContext = editorContext != null ? editorContext.Clone() : null;
            Target = target != null ? target.Clone() : null;
            EditingEnabled = editingEnabled;
            Runtime = runtime;
        }

        public DocumentSession Session { get; private set; }

        public EditorContextSnapshot EditorContext { get; private set; }

        public EditorCommandTarget Target { get; private set; }

        public bool EditingEnabled { get; private set; }

        public IWorkbenchRuntimeAccess Runtime { get; private set; }
    }

    /// <summary>
    /// Pointer interaction context for contributed editor workflows.
    /// </summary>
    public sealed class WorkbenchEditorPointerContext : WorkbenchEditorWorkflowContext
    {
        public WorkbenchEditorPointerContext(
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandTarget target,
            bool editingEnabled,
            WorkbenchEditorPointerKind pointerKind,
            int lineNumber,
            int absolutePosition,
            IWorkbenchRuntimeAccess runtime)
            : base(session, editorContext, target, editingEnabled, runtime)
        {
            PointerKind = pointerKind;
            LineNumber = lineNumber;
            AbsolutePosition = absolutePosition;
        }

        public WorkbenchEditorPointerKind PointerKind { get; private set; }

        public int LineNumber { get; private set; }

        public int AbsolutePosition { get; private set; }
    }

    /// <summary>
    /// Keyboard interaction context for contributed editor workflows.
    /// </summary>
    public sealed class WorkbenchEditorKeyboardContext : WorkbenchEditorWorkflowContext
    {
        public WorkbenchEditorKeyboardContext(
            DocumentSession session,
            EditorContextSnapshot editorContext,
            EditorCommandTarget target,
            bool editingEnabled,
            WorkbenchEditorInteractionKey key,
            bool shift,
            bool control,
            bool alt,
            IWorkbenchRuntimeAccess runtime)
            : base(session, editorContext, target, editingEnabled, runtime)
        {
            Key = key;
            Shift = shift;
            Control = control;
            Alt = alt;
        }

        public WorkbenchEditorInteractionKey Key { get; private set; }

        public bool Shift { get; private set; }

        public bool Control { get; private set; }

        public bool Alt { get; private set; }
    }

    /// <summary>
    /// Result returned by contributed editor workflow handlers.
    /// </summary>
    public sealed class WorkbenchEditorWorkflowResult
    {
        public bool Handled { get; set; }

        public bool ConsumeInput { get; set; }
    }

    /// <summary>
    /// Contributes a session-synchronized editor workflow.
    /// </summary>
    public sealed class WorkbenchEditorWorkflowContribution
    {
        public string ContributionId;
        public int SortOrder;
        public Func<WorkbenchEditorWorkflowContext, bool> IsActive;
        public Action<WorkbenchEditorWorkflowContext> Synchronize;
        public Func<WorkbenchEditorPointerContext, WorkbenchEditorWorkflowResult> TryHandlePointer;
        public Func<WorkbenchEditorKeyboardContext, WorkbenchEditorWorkflowResult> TryHandleKeyboard;
    }
}
