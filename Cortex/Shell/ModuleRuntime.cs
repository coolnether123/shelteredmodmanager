using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;

namespace Cortex.Shell
{
    internal sealed class WorkbenchModuleRuntimeFactory
    {
        private readonly CortexShellState _state;
        private readonly ShellServiceMap _services;
        private readonly Func<IWorkbenchRuntime> _runtimeProvider;

        public WorkbenchModuleRuntimeFactory(
            CortexShellState state,
            ShellServiceMap services,
            Func<IWorkbenchRuntime> runtimeProvider)
        {
            _state = state;
            _services = services;
            _runtimeProvider = runtimeProvider;
        }

        public IWorkbenchModuleRuntime Create(WorkbenchModuleDescriptor descriptor)
        {
            return new WorkbenchModuleRuntime(descriptor, _state, _services, _runtimeProvider);
        }
    }

    internal sealed class WorkbenchModuleRuntime : IWorkbenchModuleRuntime
    {
        public WorkbenchModuleRuntime(
            WorkbenchModuleDescriptor descriptor,
            CortexShellState state,
            ShellServiceMap services,
            Func<IWorkbenchRuntime> runtimeProvider)
        {
            Lifecycle = new WorkbenchModuleLifecycleRuntime(descriptor, state);
            Commands = new WorkbenchCommandRuntime(state, runtimeProvider);
            Navigation = new WorkbenchNavigationRuntime(state, services != null ? services.NavigationService : null);
            Documents = new WorkbenchDocumentRuntime(state, services != null ? services.DocumentService : null);
            Projects = new WorkbenchProjectRuntime(state, services != null ? services.ProjectCatalog : null, services != null ? services.LoadedModCatalog : null);
            Editor = new WorkbenchEditorRuntime(state, services != null ? services.EditorContextService : null);
            State = new WorkbenchModuleStateRuntime(descriptor, state);
        }

        public IWorkbenchModuleLifecycleRuntime Lifecycle { get; private set; }

        public IWorkbenchCommandRuntime Commands { get; private set; }

        public IWorkbenchNavigationRuntime Navigation { get; private set; }

        public IWorkbenchDocumentRuntime Documents { get; private set; }

        public IWorkbenchProjectRuntime Projects { get; private set; }

        public IWorkbenchEditorRuntime Editor { get; private set; }

        public IWorkbenchModuleStateRuntime State { get; private set; }
    }

    internal sealed class WorkbenchModuleLifecycleRuntime : IWorkbenchModuleLifecycleRuntime
    {
        private readonly CortexShellState _state;

        public WorkbenchModuleLifecycleRuntime(WorkbenchModuleDescriptor descriptor, CortexShellState state)
        {
            _state = state;
            ModuleId = descriptor != null ? descriptor.ModuleId ?? string.Empty : string.Empty;
            ContainerId = descriptor != null ? descriptor.ContainerId ?? string.Empty : string.Empty;
        }

        public string ModuleId { get; private set; }

        public string ContainerId { get; private set; }

        public void RequestContainer(string containerId)
        {
            RequestContainer(containerId, WorkbenchHostLocation.DocumentHost);
        }

        public void RequestContainer(string containerId, WorkbenchHostLocation hostLocation)
        {
            if (_state == null || _state.Workbench == null || string.IsNullOrEmpty(containerId))
            {
                return;
            }

            _state.Workbench.AssignHost(containerId, hostLocation);
            _state.Workbench.RequestedContainerId = containerId;
        }
    }

    internal sealed class WorkbenchCommandRuntime : IWorkbenchCommandRuntime
    {
        private readonly CortexShellState _state;
        private readonly Func<IWorkbenchRuntime> _runtimeProvider;

        public WorkbenchCommandRuntime(CortexShellState state, Func<IWorkbenchRuntime> runtimeProvider)
        {
            _state = state;
            _runtimeProvider = runtimeProvider;
        }

        public CommandDefinition Get(string commandId)
        {
            var registry = GetRegistry();
            return registry != null ? registry.Get(commandId) : null;
        }

        public IList<CommandDefinition> GetAll()
        {
            var registry = GetRegistry();
            return registry != null ? registry.GetAll() : new List<CommandDefinition>();
        }

        public bool CanExecute(string commandId, object parameter)
        {
            var registry = GetRegistry();
            return registry != null && registry.CanExecute(commandId, BuildContext(parameter));
        }

        public bool Execute(string commandId, object parameter)
        {
            var registry = GetRegistry();
            return registry != null && registry.Execute(commandId, BuildContext(parameter));
        }

        private Cortex.Core.Abstractions.ICommandRegistry GetRegistry()
        {
            var runtime = _runtimeProvider != null ? _runtimeProvider() : null;
            return runtime != null ? runtime.CommandRegistry : null;
        }

        private CommandExecutionContext BuildContext(object parameter)
        {
            var target = parameter as EditorCommandTarget;
            var runtime = _runtimeProvider != null ? _runtimeProvider() : null;
            return new CommandExecutionContext
            {
                ActiveContainerId = _state != null && _state.Workbench != null ? _state.Workbench.FocusedContainerId : string.Empty,
                ActiveDocumentId = target != null && !string.IsNullOrEmpty(target.DocumentPath)
                    ? target.DocumentPath
                    : _state != null && _state.Documents != null ? _state.Documents.ActiveDocumentPath : string.Empty,
                FocusedRegionId = runtime != null && runtime.FocusState != null
                    ? runtime.FocusState.FocusedRegionId ?? string.Empty
                    : _state != null && _state.Workbench != null ? _state.Workbench.FocusedContainerId : string.Empty,
                Parameter = parameter
            };
        }
    }

    internal sealed class WorkbenchNavigationRuntime : IWorkbenchNavigationRuntime
    {
        private readonly CortexShellState _state;
        private readonly ICortexNavigationService _navigationService;

        public WorkbenchNavigationRuntime(CortexShellState state, ICortexNavigationService navigationService)
        {
            _state = state;
            _navigationService = navigationService;
        }

        public DocumentSession OpenDocument(string filePath, int highlightedLine)
        {
            return _navigationService != null
                ? _navigationService.OpenDocument(_state, filePath, highlightedLine, string.Empty, string.Empty)
                : null;
        }

        public void PreloadDocument(string filePath)
        {
            if (_navigationService != null)
            {
                _navigationService.PreloadDocument(_state, filePath);
            }
        }

        public DecompilerResponse RequestDecompilerSource(string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache)
        {
            return _navigationService != null
                ? _navigationService.RequestDecompilerSource(_state, assemblyPath, metadataToken, entityKind, ignoreCache)
                : null;
        }

        public bool OpenDecompilerResult(DecompilerResponse response, int highlightedLine)
        {
            return _navigationService != null &&
                _navigationService.OpenDecompilerResult(_state, response, highlightedLine, string.Empty, string.Empty);
        }

        public bool OpenSourceTarget(SourceNavigationTarget target)
        {
            return _navigationService != null &&
                _navigationService.OpenRuntimeTarget(_state, target, string.Empty, string.Empty);
        }

        public bool OpenNavigationTarget(EditorHoverNavigationTarget target)
        {
            return _navigationService != null &&
                _navigationService.OpenHoverNavigationTarget(_state, target, string.Empty, string.Empty);
        }
    }

    internal sealed class WorkbenchProjectRuntime : IWorkbenchProjectRuntime
    {
        private readonly CortexShellState _state;
        private readonly Cortex.Core.Abstractions.IProjectCatalog _projectCatalog;
        private readonly Cortex.Core.Abstractions.ILoadedModCatalog _loadedModCatalog;

        public WorkbenchProjectRuntime(
            CortexShellState state,
            Cortex.Core.Abstractions.IProjectCatalog projectCatalog,
            Cortex.Core.Abstractions.ILoadedModCatalog loadedModCatalog)
        {
            _state = state;
            _projectCatalog = projectCatalog;
            _loadedModCatalog = loadedModCatalog;
        }

        public CortexProjectDefinition GetSelectedProject()
        {
            return CloneProject(_state != null ? _state.SelectedProject : null);
        }

        public IList<CortexProjectDefinition> GetProjects()
        {
            var projects = _projectCatalog != null ? _projectCatalog.GetProjects() : null;
            var results = new List<CortexProjectDefinition>();
            if (projects == null)
            {
                return results;
            }

            for (var i = 0; i < projects.Count; i++)
            {
                results.Add(CloneProject(projects[i]));
            }

            return results;
        }

        public CortexProjectDefinition GetProject(string modId)
        {
            return CloneProject(_projectCatalog != null ? _projectCatalog.GetProject(modId) : null);
        }

        public IList<LoadedModInfo> GetLoadedMods()
        {
            var mods = _loadedModCatalog != null ? _loadedModCatalog.GetLoadedMods() : null;
            var results = new List<LoadedModInfo>();
            if (mods == null)
            {
                return results;
            }

            for (var i = 0; i < mods.Count; i++)
            {
                results.Add(CloneLoadedMod(mods[i]));
            }

            return results;
        }

        public LoadedModInfo GetLoadedMod(string modId)
        {
            return CloneLoadedMod(_loadedModCatalog != null ? _loadedModCatalog.GetMod(modId) : null);
        }

        public LanguageRuntimeSnapshot GetLanguageRuntime()
        {
            return CloneLanguageRuntime(_state != null ? _state.LanguageRuntime : null);
        }

        private static CortexProjectDefinition CloneProject(CortexProjectDefinition project)
        {
            return project == null
                ? null
                : new CortexProjectDefinition
                {
                    ModId = project.ModId ?? string.Empty,
                    SourceRootPath = project.SourceRootPath ?? string.Empty,
                    ProjectFilePath = project.ProjectFilePath ?? string.Empty,
                    BuildCommandOverride = project.BuildCommandOverride ?? string.Empty,
                    OutputAssemblyPath = project.OutputAssemblyPath ?? string.Empty,
                    OutputPdbPath = project.OutputPdbPath ?? string.Empty
                };
        }

        private static LoadedModInfo CloneLoadedMod(LoadedModInfo mod)
        {
            return mod == null
                ? null
                : new LoadedModInfo
                {
                    ModId = mod.ModId ?? string.Empty,
                    DisplayName = mod.DisplayName ?? string.Empty,
                    RootPath = mod.RootPath ?? string.Empty
                };
        }

        private static LanguageRuntimeSnapshot CloneLanguageRuntime(LanguageRuntimeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return new LanguageRuntimeSnapshot();
            }

            return new LanguageRuntimeSnapshot
            {
                Provider = snapshot.Provider == null
                    ? new LanguageProviderDescriptor()
                    : new LanguageProviderDescriptor
                    {
                        ProviderId = snapshot.Provider.ProviderId ?? string.Empty,
                        DisplayName = snapshot.Provider.DisplayName ?? string.Empty,
                        Version = snapshot.Provider.Version ?? string.Empty,
                        Source = snapshot.Provider.Source ?? string.Empty
                    },
                LifecycleState = snapshot.LifecycleState,
                HealthState = snapshot.HealthState,
                Capabilities = snapshot.Capabilities == null
                    ? new LanguageCapabilitiesSnapshot()
                    : new LanguageCapabilitiesSnapshot
                    {
                        SupportsAnalysis = snapshot.Capabilities.SupportsAnalysis,
                        SupportsDiagnostics = snapshot.Capabilities.SupportsDiagnostics,
                        SupportsSemanticTokens = snapshot.Capabilities.SupportsSemanticTokens,
                        SupportsHover = snapshot.Capabilities.SupportsHover,
                        SupportsDefinition = snapshot.Capabilities.SupportsDefinition,
                        SupportsCompletion = snapshot.Capabilities.SupportsCompletion,
                        SupportsSignatureHelp = snapshot.Capabilities.SupportsSignatureHelp,
                        SupportsRename = snapshot.Capabilities.SupportsRename,
                        SupportsReferences = snapshot.Capabilities.SupportsReferences,
                        SupportsImplementations = snapshot.Capabilities.SupportsImplementations,
                        SupportsBaseSymbols = snapshot.Capabilities.SupportsBaseSymbols,
                        SupportsCallHierarchy = snapshot.Capabilities.SupportsCallHierarchy,
                        SupportsValueSource = snapshot.Capabilities.SupportsValueSource,
                        SupportsDocumentTransforms = snapshot.Capabilities.SupportsDocumentTransforms,
                        CapabilityIds = snapshot.Capabilities.CapabilityIds != null
                            ? (string[])snapshot.Capabilities.CapabilityIds.Clone()
                            : new string[0]
                    },
                StatusMessage = snapshot.StatusMessage ?? string.Empty,
                LastErrorSummary = snapshot.LastErrorSummary ?? string.Empty,
                ActiveGeneration = snapshot.ActiveGeneration
            };
        }
    }

    internal sealed class WorkbenchDocumentRuntime : IWorkbenchDocumentRuntime
    {
        private readonly CortexShellState _state;
        private readonly Cortex.Core.Abstractions.IDocumentService _documentService;

        public WorkbenchDocumentRuntime(CortexShellState state, Cortex.Core.Abstractions.IDocumentService documentService)
        {
            _state = state;
            _documentService = documentService;
        }

        public DocumentSession GetActive()
        {
            return _state != null && _state.Documents != null
                ? _state.Documents.ActiveDocument
                : null;
        }

        public DocumentSession Get(string filePath)
        {
            if (_state == null || _state.Documents == null || string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = System.IO.Path.GetFullPath(filePath);
            }
            catch
            {
                return null;
            }

            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var session = _state.Documents.OpenDocuments[i];
                if (session != null &&
                    string.Equals(session.FilePath ?? string.Empty, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }

            return null;
        }

        public DocumentSession Open(string filePath, int highlightedLine)
        {
            return _documentService != null && _state != null
                ? Cortex.Modules.Shared.CortexModuleUtil.OpenDocument(_documentService, _state, filePath, highlightedLine)
                : null;
        }

        public bool Save(DocumentSession session)
        {
            return _documentService != null && _documentService.Save(session);
        }

        public bool Reload(DocumentSession session)
        {
            return _documentService != null && _documentService.Reload(session);
        }
    }

    internal sealed class WorkbenchEditorRuntime : IWorkbenchEditorRuntime
    {
        private readonly CortexShellState _state;
        private readonly IEditorContextService _editorContextService;

        public WorkbenchEditorRuntime(CortexShellState state, IEditorContextService editorContextService)
        {
            _state = state;
            _editorContextService = editorContextService;
        }

        public EditorContextSnapshot GetActiveContext()
        {
            return CloneContext(_editorContextService != null ? _editorContextService.GetActiveContext(_state) : null);
        }

        public EditorContextSnapshot GetHoveredContext()
        {
            return CloneContext(_editorContextService != null ? _editorContextService.GetHoveredContext(_state) : null);
        }

        public EditorContextSnapshot GetContext(string contextKey)
        {
            return CloneContext(_editorContextService != null ? _editorContextService.GetContext(_state, contextKey) : null);
        }

        public EditorContextSnapshot GetSurfaceContext(string surfaceId)
        {
            return CloneContext(_editorContextService != null ? _editorContextService.GetSurfaceContext(_state, surfaceId) : null);
        }

        public WorkbenchContextStateScope CreateDocumentScope(EditorContextSnapshot context)
        {
            if (context == null || string.IsNullOrEmpty(context.DocumentPath))
            {
                return null;
            }

            return new WorkbenchContextStateScope(
                WorkbenchContextStateScopeKind.Document,
                context.DocumentPath,
                context.DocumentPath,
                context.SurfaceId,
                context.PaneId,
                context.SurfaceKind);
        }

        public WorkbenchContextStateScope CreateEditorScope(EditorContextSnapshot context)
        {
            if (context == null)
            {
                return null;
            }

            var scopeId = !string.IsNullOrEmpty(context.SurfaceId)
                ? context.SurfaceId
                : BuildFallbackSurfaceId(context);
            if (string.IsNullOrEmpty(scopeId))
            {
                return null;
            }

            return new WorkbenchContextStateScope(
                WorkbenchContextStateScopeKind.EditorSession,
                scopeId,
                context.DocumentPath,
                scopeId,
                context.PaneId,
                context.SurfaceKind);
        }

        private static EditorContextSnapshot CloneContext(EditorContextSnapshot context)
        {
            return context != null ? context.Clone() : null;
        }

        private static string BuildFallbackSurfaceId(EditorContextSnapshot context)
        {
            return ((context.SurfaceKind.ToString() ?? string.Empty) + "|" +
                (context.PaneId ?? string.Empty) + "|" +
                (context.DocumentPath ?? string.Empty)).ToLowerInvariant();
        }
    }

    internal sealed class WorkbenchModuleStateRuntime : IWorkbenchModuleStateRuntime
    {
        public WorkbenchModuleStateRuntime(WorkbenchModuleDescriptor descriptor, CortexShellState state)
        {
            var moduleId = descriptor != null ? descriptor.ModuleId ?? string.Empty : string.Empty;
            Persistent = new WorkbenchPersistentStateStore(state, moduleId);
            Workflow = new WorkbenchWorkflowStateStore(state, moduleId);
            Contexts = new WorkbenchContextStateStore(state, moduleId);
        }

        public IWorkbenchPersistentStateStore Persistent { get; private set; }

        public IWorkbenchWorkflowStateStore Workflow { get; private set; }

        public IWorkbenchContextStateStore Contexts { get; private set; }
    }

    internal sealed class WorkbenchPersistentStateStore : IWorkbenchPersistentStateStore
    {
        private readonly CortexShellState _state;
        private readonly string _moduleId;

        public WorkbenchPersistentStateStore(CortexShellState state, string moduleId)
        {
            _state = state;
            _moduleId = moduleId ?? string.Empty;
        }

        public bool Contains(string key)
        {
            CortexModuleStateBucket moduleState;
            return TryGetPersistentValues(out moduleState) && moduleState.PersistentValues.ContainsKey(key ?? string.Empty);
        }

        public string GetValue(string key, string defaultValue)
        {
            CortexModuleStateBucket moduleState;
            string value;
            return TryGetPersistentValues(out moduleState) &&
                moduleState.PersistentValues.TryGetValue(key ?? string.Empty, out value)
                ? value ?? string.Empty
                : defaultValue;
        }

        public void SetValue(string key, string serializedValue)
        {
            if (string.IsNullOrEmpty(key) || _state == null || _state.Modules == null)
            {
                return;
            }

            if (serializedValue == null)
            {
                Remove(key);
                return;
            }

            _state.Modules.GetOrCreateModule(_moduleId).PersistentValues[key] = serializedValue;
        }

        public void Remove(string key)
        {
            CortexModuleStateBucket moduleState;
            if (string.IsNullOrEmpty(key) || !TryGetPersistentValues(out moduleState))
            {
                return;
            }

            moduleState.PersistentValues.Remove(key);
        }

        private bool TryGetPersistentValues(out CortexModuleStateBucket moduleState)
        {
            moduleState = null;
            return _state != null && _state.Modules != null && _state.Modules.TryGetModule(_moduleId, out moduleState) && moduleState != null;
        }
    }

    internal sealed class WorkbenchWorkflowStateStore : IWorkbenchWorkflowStateStore
    {
        private readonly CortexShellState _state;
        private readonly string _moduleId;

        public WorkbenchWorkflowStateStore(CortexShellState state, string moduleId)
        {
            _state = state;
            _moduleId = moduleId ?? string.Empty;
        }

        public bool Contains(string key)
        {
            CortexModuleStateBucket moduleState;
            return TryGetModuleState(out moduleState) && moduleState.WorkflowValues.ContainsKey(key ?? string.Empty);
        }

        public TState Get<TState>(string key) where TState : class
        {
            CortexModuleStateBucket moduleState;
            object value;
            return TryGetModuleState(out moduleState) && moduleState.WorkflowValues.TryGetValue(key ?? string.Empty, out value)
                ? value as TState
                : null;
        }

        public TState GetOrCreate<TState>(string key, Func<TState> factory) where TState : class
        {
            if (string.IsNullOrEmpty(key) || factory == null || _state == null || _state.Modules == null)
            {
                return null;
            }

            var moduleState = _state.Modules.GetOrCreateModule(_moduleId);
            object existing;
            if (moduleState.WorkflowValues.TryGetValue(key, out existing))
            {
                var existingTyped = existing as TState;
                if (existing != null && existingTyped == null)
                {
                    throw new InvalidOperationException("Workflow state key '" + key + "' is already bound to a different state type.");
                }

                return existingTyped;
            }

            var created = factory();
            moduleState.WorkflowValues[key] = created;
            return created;
        }

        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key) || _state == null || _state.Modules == null)
            {
                return;
            }

            if (value == null)
            {
                Remove(key);
                return;
            }

            _state.Modules.GetOrCreateModule(_moduleId).WorkflowValues[key] = value;
        }

        public void Remove(string key)
        {
            CortexModuleStateBucket moduleState;
            if (string.IsNullOrEmpty(key) || !TryGetModuleState(out moduleState))
            {
                return;
            }

            moduleState.WorkflowValues.Remove(key);
        }

        public void Clear()
        {
            CortexModuleStateBucket moduleState;
            if (TryGetModuleState(out moduleState))
            {
                moduleState.WorkflowValues.Clear();
            }
        }

        private bool TryGetModuleState(out CortexModuleStateBucket moduleState)
        {
            moduleState = null;
            return _state != null && _state.Modules != null && _state.Modules.TryGetModule(_moduleId, out moduleState) && moduleState != null;
        }
    }

    internal sealed class WorkbenchContextStateStore : IWorkbenchContextStateStore
    {
        private readonly CortexShellState _state;
        private readonly string _moduleId;

        public WorkbenchContextStateStore(CortexShellState state, string moduleId)
        {
            _state = state;
            _moduleId = moduleId ?? string.Empty;
        }

        public bool Contains(WorkbenchContextStateScope scope, string key)
        {
            CortexContextStateBucket scopeState;
            return TryGetScope(scope, out scopeState) && scopeState.Values.ContainsKey(key ?? string.Empty);
        }

        public TState Get<TState>(WorkbenchContextStateScope scope, string key) where TState : class
        {
            CortexContextStateBucket scopeState;
            object value;
            return TryGetScope(scope, out scopeState) && scopeState.Values.TryGetValue(key ?? string.Empty, out value)
                ? value as TState
                : null;
        }

        public TState GetOrCreate<TState>(WorkbenchContextStateScope scope, string key, Func<TState> factory) where TState : class
        {
            if (!IsScopeValid(scope) || string.IsNullOrEmpty(key) || factory == null || _state == null || _state.Modules == null)
            {
                return null;
            }

            var scopeState = GetOrCreateScope(scope);
            object existing;
            if (scopeState.Values.TryGetValue(key, out existing))
            {
                var existingTyped = existing as TState;
                if (existing != null && existingTyped == null)
                {
                    throw new InvalidOperationException("Context state key '" + key + "' is already bound to a different state type.");
                }

                return existingTyped;
            }

            var created = factory();
            scopeState.Values[key] = created;
            return created;
        }

        public void Set(WorkbenchContextStateScope scope, string key, object value)
        {
            if (!IsScopeValid(scope) || string.IsNullOrEmpty(key) || _state == null || _state.Modules == null)
            {
                return;
            }

            if (value == null)
            {
                Remove(scope, key);
                return;
            }

            GetOrCreateScope(scope).Values[key] = value;
        }

        public void Remove(WorkbenchContextStateScope scope, string key)
        {
            CortexContextStateBucket scopeState;
            if (!IsScopeValid(scope) || string.IsNullOrEmpty(key) || !TryGetScope(scope, out scopeState))
            {
                return;
            }

            scopeState.Values.Remove(key);
        }

        public void Clear(WorkbenchContextStateScope scope)
        {
            CortexModuleStateBucket moduleState;
            if (!IsScopeValid(scope) || _state == null || _state.Modules == null || !_state.Modules.TryGetModule(_moduleId, out moduleState) || moduleState == null)
            {
                return;
            }

            moduleState.ContextScopes.Remove(BuildScopeKey(scope));
        }

        private CortexContextStateBucket GetOrCreateScope(WorkbenchContextStateScope scope)
        {
            var moduleState = _state.Modules.GetOrCreateModule(_moduleId);
            var scopeKey = BuildScopeKey(scope);
            CortexContextStateBucket scopeState;
            if (!moduleState.ContextScopes.TryGetValue(scopeKey, out scopeState))
            {
                scopeState = new CortexContextStateBucket
                {
                    Scope = new WorkbenchContextStateScope(
                        scope.ScopeKind,
                        scope.ScopeId,
                        scope.DocumentPath,
                        scope.SurfaceId,
                        scope.PaneId,
                        scope.SurfaceKind)
                };
                moduleState.ContextScopes[scopeKey] = scopeState;
            }

            return scopeState;
        }

        private bool TryGetScope(WorkbenchContextStateScope scope, out CortexContextStateBucket scopeState)
        {
            scopeState = null;
            CortexModuleStateBucket moduleState;
            return IsScopeValid(scope) &&
                _state != null &&
                _state.Modules != null &&
                _state.Modules.TryGetModule(_moduleId, out moduleState) &&
                moduleState != null &&
                moduleState.ContextScopes.TryGetValue(BuildScopeKey(scope), out scopeState);
        }

        private static bool IsScopeValid(WorkbenchContextStateScope scope)
        {
            return scope != null && !string.IsNullOrEmpty(scope.ScopeId);
        }

        private static string BuildScopeKey(WorkbenchContextStateScope scope)
        {
            return ((int)scope.ScopeKind).ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (scope.ScopeId ?? string.Empty);
        }
    }
}
