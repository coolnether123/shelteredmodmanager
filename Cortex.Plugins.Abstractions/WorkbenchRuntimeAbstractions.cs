using System;
using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// Composition root for runtime services made available to a workbench module.
    /// </summary>
    public interface IWorkbenchModuleRuntime
    {
        IWorkbenchModuleLifecycleRuntime Lifecycle { get; }

        IWorkbenchCommandRuntime Commands { get; }

        IWorkbenchNavigationRuntime Navigation { get; }

        IWorkbenchDocumentRuntime Documents { get; }

        IWorkbenchProjectRuntime Projects { get; }

        IWorkbenchEditorRuntime Editor { get; }

        IWorkbenchModuleStateRuntime State { get; }
    }

    /// <summary>
    /// Workbench and module lifecycle operations available at runtime.
    /// </summary>
    public interface IWorkbenchModuleLifecycleRuntime
    {
        string ModuleId { get; }

        string ContainerId { get; }

        void RequestContainer(string containerId);

        void RequestContainer(string containerId, WorkbenchHostLocation hostLocation);
    }

    /// <summary>
    /// Command query and execution access scoped to the active workbench runtime.
    /// </summary>
    public interface IWorkbenchCommandRuntime
    {
        CommandDefinition Get(string commandId);

        IList<CommandDefinition> GetAll();

        bool CanExecute(string commandId, object parameter);

        bool Execute(string commandId, object parameter);
    }

    /// <summary>
    /// Navigation and document access exposed to runtime modules.
    /// </summary>
    public interface IWorkbenchNavigationRuntime
    {
        DocumentSession OpenDocument(string filePath, int highlightedLine);

        void PreloadDocument(string filePath);

        DecompilerResponse RequestDecompilerSource(string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache);

        bool OpenDecompilerResult(DecompilerResponse response, int highlightedLine);

        bool OpenSourceTarget(SourceNavigationTarget target);

        bool OpenNavigationTarget(EditorHoverNavigationTarget target);
    }

    /// <summary>
    /// Document-session access for modules that edit, preview, or save files.
    /// </summary>
    public interface IWorkbenchDocumentRuntime
    {
        DocumentSession GetActive();

        DocumentSession Get(string filePath);

        DocumentSession Open(string filePath, int highlightedLine);

        bool Save(DocumentSession session);

        bool Reload(DocumentSession session);
    }

    /// <summary>
    /// Read-only project and runtime metadata available to modules.
    /// </summary>
    public interface IWorkbenchProjectRuntime
    {
        CortexProjectDefinition GetSelectedProject();

        IList<CortexProjectDefinition> GetProjects();

        CortexProjectDefinition GetProject(string modId);

        IList<LoadedModInfo> GetLoadedMods();

        LoadedModInfo GetLoadedMod(string modId);

        LanguageRuntimeSnapshot GetLanguageRuntime();
    }

    /// <summary>
    /// Editor context access for modules that respond to active documents and surfaces.
    /// </summary>
    public interface IWorkbenchEditorRuntime
    {
        EditorContextSnapshot GetActiveContext();

        EditorContextSnapshot GetHoveredContext();

        EditorContextSnapshot GetContext(string contextKey);

        EditorContextSnapshot GetSurfaceContext(string surfaceId);

        WorkbenchContextStateScope CreateDocumentScope(EditorContextSnapshot context);

        WorkbenchContextStateScope CreateEditorScope(EditorContextSnapshot context);
    }

    /// <summary>
    /// Runtime state access owned by a module.
    /// </summary>
    public interface IWorkbenchModuleStateRuntime
    {
        IWorkbenchPersistentStateStore Persistent { get; }

        IWorkbenchWorkflowStateStore Workflow { get; }

        IWorkbenchContextStateStore Contexts { get; }
    }

    /// <summary>
    /// Persistent module-owned state stored as serialized values.
    /// </summary>
    public interface IWorkbenchPersistentStateStore
    {
        bool Contains(string key);

        string GetValue(string key, string defaultValue);

        void SetValue(string key, string serializedValue);

        void Remove(string key);
    }

    /// <summary>
    /// Session-local workflow state owned by a module.
    /// </summary>
    public interface IWorkbenchWorkflowStateStore
    {
        bool Contains(string key);

        TState Get<TState>(string key) where TState : class;

        TState GetOrCreate<TState>(string key, Func<TState> factory) where TState : class;

        void Set(string key, object value);

        void Remove(string key);

        void Clear();
    }

    /// <summary>
    /// Distinguishes whether state is attached to a logical document or an editor session.
    /// </summary>
    public enum WorkbenchContextStateScopeKind
    {
        Document = 0,
        EditorSession = 1
    }

    /// <summary>
    /// Identifies a document-bound or editor-session-bound state scope.
    /// </summary>
    public sealed class WorkbenchContextStateScope
    {
        public WorkbenchContextStateScope(
            WorkbenchContextStateScopeKind scopeKind,
            string scopeId,
            string documentPath,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind)
        {
            ScopeKind = scopeKind;
            ScopeId = scopeId ?? string.Empty;
            DocumentPath = documentPath ?? string.Empty;
            SurfaceId = surfaceId ?? string.Empty;
            PaneId = paneId ?? string.Empty;
            SurfaceKind = surfaceKind;
        }

        public WorkbenchContextStateScopeKind ScopeKind { get; private set; }

        public string ScopeId { get; private set; }

        public string DocumentPath { get; private set; }

        public string SurfaceId { get; private set; }

        public string PaneId { get; private set; }

        public EditorSurfaceKind SurfaceKind { get; private set; }
    }

    /// <summary>
    /// State attached to a document or editor-session scope.
    /// </summary>
    public interface IWorkbenchContextStateStore
    {
        bool Contains(WorkbenchContextStateScope scope, string key);

        TState Get<TState>(WorkbenchContextStateScope scope, string key) where TState : class;

        TState GetOrCreate<TState>(WorkbenchContextStateScope scope, string key, Func<TState> factory) where TState : class;

        void Set(WorkbenchContextStateScope scope, string key, object value);

        void Remove(WorkbenchContextStateScope scope, string key);

        void Clear(WorkbenchContextStateScope scope);
    }
}
