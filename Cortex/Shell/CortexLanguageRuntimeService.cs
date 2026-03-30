using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services.Semantics.Analysis;
using Cortex.Services.Semantics.Completion;
using Cortex.Services.Semantics.Requests;
using Cortex.Services.Semantics.SignatureHelp;

namespace Cortex.Shell
{
    internal sealed class CortexLanguageRuntimeService :
        ILanguageRuntimeControl,
        ILanguageRuntimeQuery,
        ILanguageEditorOperations
    {
        private readonly CortexShellState _state;
        private readonly Func<ShellServiceMap> _serviceMapAccessor;
        private readonly Func<bool> _completionAugmentationInFlightAccessor;
        private readonly Action _processCompletionAugmentationResponses;
        private readonly Action _dispatchDeferredCompletionAugmentation;
        private readonly Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest> _buildCompletionAugmentationRequest;
        private readonly Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest, LanguageServiceCompletionResponse, bool> _tryQueueCompletionAugmentation;
        private readonly Func<IList<ILanguageProviderFactory>> _hostProviderFactoriesAccessor;
        private readonly IList<ILanguageProviderFactory> _builtinProviderFactories = new List<ILanguageProviderFactory>();
        private readonly IDocumentLanguageAnalysisService _documentLanguageAnalysisService = new DocumentLanguageAnalysisService();
        private readonly IEditorLanguageRequestFactory _languageRequestFactory = new EditorLanguageRequestFactory();
        private readonly IEditorCompletionService _editorCompletionService = new EditorCompletionService();
        private readonly IEditorSignatureHelpService _editorSignatureHelpService = new EditorSignatureHelpService();
        private readonly CortexShellLanguageCoordinator _languageCoordinator = new CortexShellLanguageCoordinator();
        private readonly CortexShellLanguageRequestDispatcher _languageRequestDispatcher = new CortexShellLanguageRequestDispatcher();
        private readonly CortexShellLanguageResponseProcessor _languageResponseProcessor = new CortexShellLanguageResponseProcessor();
        private CortexShellLanguageRuntimeContext _runtimeContext;
        private ILanguageProviderSession _activeSession;
        private string _activeProviderId = string.Empty;
        private string _activeConfigurationFingerprint = string.Empty;
        private int _activeGeneration;
        private const double LanguageAnalysisDebounceMs = 280d;

        public CortexLanguageRuntimeService(
            CortexShellState state,
            Func<ShellServiceMap> serviceMapAccessor,
            Func<bool> completionAugmentationInFlightAccessor,
            Action processCompletionAugmentationResponses,
            Action dispatchDeferredCompletionAugmentation,
            Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest> buildCompletionAugmentationRequest,
            Func<DocumentSession, DocumentLanguageCompletionRequestState, CompletionAugmentationRequest, LanguageServiceCompletionResponse, bool> tryQueueCompletionAugmentation,
            Func<IList<ILanguageProviderFactory>> hostProviderFactoriesAccessor,
            IList<ILanguageProviderFactory> builtinProviderFactories)
        {
            _state = state;
            _serviceMapAccessor = serviceMapAccessor;
            _completionAugmentationInFlightAccessor = completionAugmentationInFlightAccessor;
            _processCompletionAugmentationResponses = processCompletionAugmentationResponses;
            _dispatchDeferredCompletionAugmentation = dispatchDeferredCompletionAugmentation;
            _buildCompletionAugmentationRequest = buildCompletionAugmentationRequest;
            _tryQueueCompletionAugmentation = tryQueueCompletionAugmentation;
            _hostProviderFactoriesAccessor = hostProviderFactoriesAccessor;
            if (builtinProviderFactories != null)
            {
                for (var i = 0; i < builtinProviderFactories.Count; i++)
                {
                    if (builtinProviderFactories[i] != null)
                    {
                        _builtinProviderFactories.Add(builtinProviderFactories[i]);
                    }
                }
            }
            else
            {
                _builtinProviderFactories.Add(new RoslynLanguageProviderFactory());
            }

            PublishSnapshot(
                LanguageRuntimeLifecycleState.Disabled,
                LanguageRuntimeHealthState.NoProviders,
                "No language providers are registered.",
                "No language providers are registered.",
                new LanguageProviderDescriptor());
        }

        public void Start(LanguageRuntimeConfiguration configuration)
        {
            ApplyConfiguration(configuration, false);
        }

        public void Reload(LanguageRuntimeConfiguration configuration)
        {
            ApplyConfiguration(configuration, true);
        }

        public void Advance()
        {
            if (_activeSession == null)
            {
                PublishSnapshotFromCurrentState();
                return;
            }

            _activeSession.Advance();
            _languageCoordinator.UpdateLanguageService(GetLanguageRuntimeContext(), _languageRequestDispatcher, _languageResponseProcessor);
            PublishSnapshotFromCurrentState();
        }

        public void Shutdown()
        {
            DisposeActiveSession();
            PublishSnapshot(
                LanguageRuntimeLifecycleState.Disabled,
                _state.LanguageRuntime != null ? _state.LanguageRuntime.HealthState : LanguageRuntimeHealthState.Healthy,
                _state.LanguageRuntime != null ? _state.LanguageRuntime.StatusMessage : string.Empty,
                _state.LanguageRuntime != null ? _state.LanguageRuntime.LastErrorSummary : string.Empty,
                _state.LanguageRuntime != null ? _state.LanguageRuntime.Provider : new LanguageProviderDescriptor());
        }

        public LanguageRuntimeSnapshot GetSnapshot()
        {
            return CloneSnapshot(_state != null ? _state.LanguageRuntime : null);
        }

        public void DispatchDocumentAnalysis() { _languageRequestDispatcher.ProcessDocumentLanguageAnalysis(GetLanguageRuntimeContext()); }
        public void DispatchHover() { _languageRequestDispatcher.UpdateLanguageHover(GetLanguageRuntimeContext()); }
        public void DispatchDefinition() { _languageRequestDispatcher.UpdateLanguageDefinition(GetLanguageRuntimeContext()); }
        public void DispatchCompletion() { _languageRequestDispatcher.UpdateLanguageCompletion(GetLanguageRuntimeContext()); }
        public void DispatchSignatureHelp() { _languageRequestDispatcher.UpdateLanguageSignatureHelp(GetLanguageRuntimeContext()); }
        public void DispatchSemanticOperations() { _languageRequestDispatcher.UpdateSemanticOperation(GetLanguageRuntimeContext()); }
        public void DispatchMethodInspectorCallHierarchy() { _languageRequestDispatcher.UpdateMethodInspectorCallHierarchy(GetLanguageRuntimeContext()); }

        private void ApplyConfiguration(LanguageRuntimeConfiguration configuration, bool isReload)
        {
            var providerId = configuration != null ? configuration.ProviderId ?? string.Empty : string.Empty;
            var descriptor = new LanguageProviderDescriptor
            {
                ProviderId = providerId,
                DisplayName = string.IsNullOrEmpty(providerId) ? "Language Runtime" : providerId,
                Source = "shell"
            };

            if (string.Equals(providerId, LanguageRuntimeConstants.NoneProviderId, StringComparison.OrdinalIgnoreCase))
            {
                DisposeActiveSession();
                PublishRuntimeStatus(false, "disabled");
                PublishSnapshot(LanguageRuntimeLifecycleState.Disabled, LanguageRuntimeHealthState.Healthy, "Language runtime disabled by settings.", string.Empty, descriptor);
                return;
            }

            var factories = GetFactories();
            if (factories.Count == 0)
            {
                DisposeActiveSession();
                PublishRuntimeStatus(false, "no providers");
                PublishSnapshot(LanguageRuntimeLifecycleState.Disabled, LanguageRuntimeHealthState.NoProviders, "No language providers are registered.", "No language providers are registered.", descriptor);
                return;
            }

            ILanguageProviderFactory selectedFactory = null;
            for (var i = 0; i < factories.Count; i++)
            {
                if (string.Equals(factories[i].Descriptor.ProviderId, providerId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedFactory = factories[i];
                    break;
                }
            }

            if (selectedFactory == null)
            {
                DisposeActiveSession();
                PublishRuntimeStatus(false, "provider unavailable");
                PublishSnapshot(LanguageRuntimeLifecycleState.Disabled, LanguageRuntimeHealthState.Unavailable, "The selected language provider is not registered.", "Provider '" + providerId + "' is not available in the current host.", descriptor);
                return;
            }

            var fingerprint = selectedFactory.BuildConfigurationFingerprint(configuration);
            if (_activeSession != null &&
                string.Equals(_activeProviderId, providerId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_activeConfigurationFingerprint, fingerprint, StringComparison.Ordinal))
            {
                PublishSnapshotFromCurrentState();
                return;
            }

            DisposeActiveSession();
            _activeGeneration++;
            _activeProviderId = providerId;
            _activeConfigurationFingerprint = fingerprint ?? string.Empty;
            _runtimeContext = null;

            var roslynFactory = selectedFactory as RoslynLanguageProviderFactory;
            if (roslynFactory != null)
            {
                _activeSession = roslynFactory.Create(configuration, _activeGeneration);
            }

            if (_activeSession == null)
            {
                ILanguageProviderSession fallbackSession;
                string unavailableReason;
                if (!selectedFactory.TryCreate(configuration, out fallbackSession, out unavailableReason) || fallbackSession == null)
                {
                    PublishRuntimeStatus(false, string.IsNullOrEmpty(unavailableReason) ? "provider unavailable" : unavailableReason);
                    PublishSnapshot(LanguageRuntimeLifecycleState.Disabled, LanguageRuntimeHealthState.Unavailable, string.IsNullOrEmpty(unavailableReason) ? "The selected language provider is unavailable." : unavailableReason, unavailableReason, selectedFactory.Descriptor);
                    return;
                }

                _activeSession = fallbackSession;
            }

            PublishRuntimeStatus(false, isReload ? "reloading" : "starting");
            PublishSnapshot(isReload ? LanguageRuntimeLifecycleState.Reloading : LanguageRuntimeLifecycleState.Starting, LanguageRuntimeHealthState.Healthy, isReload ? "Reloading language provider." : "Starting language provider.", string.Empty, selectedFactory.Descriptor);
            _activeSession.Start(BuildLanguageInitializeRequest());
            PublishSnapshotFromCurrentState();
        }

        private IList<ILanguageProviderFactory> GetFactories()
        {
            var results = new List<ILanguageProviderFactory>();
            for (var i = 0; i < _builtinProviderFactories.Count; i++)
            {
                results.Add(_builtinProviderFactories[i]);
            }

            var hostFactories = _hostProviderFactoriesAccessor != null ? _hostProviderFactoriesAccessor() : null;
            if (hostFactories != null)
            {
                for (var i = 0; i < hostFactories.Count; i++)
                {
                    if (hostFactories[i] != null)
                    {
                        results.Add(hostFactories[i]);
                    }
                }
            }

            return results;
        }

        private void DisposeActiveSession()
        {
            if (_activeSession != null)
            {
                _activeSession.Shutdown();
                _activeSession.Dispose();
                _activeSession = null;
            }

            _activeProviderId = string.Empty;
            _activeConfigurationFingerprint = string.Empty;
            _runtimeContext = null;
        }

        private CortexShellLanguageRuntimeContext GetLanguageRuntimeContext()
        {
            if (_activeSession == null)
            {
                return null;
            }

            if (_runtimeContext == null)
            {
                _runtimeContext = new CortexShellLanguageRuntimeContext(
                    _state,
                    GetRuntimeState(),
                    _documentLanguageAnalysisService,
                    _languageRequestFactory,
                    _editorCompletionService,
                    _editorSignatureHelpService,
                    GetServiceMap() != null ? GetServiceMap().EditorContextService : null,
                    delegate { return _activeSession; },
                    delegate { return GetServiceMap() != null ? GetServiceMap().NavigationService : null; },
                    delegate { return _completionAugmentationInFlightAccessor != null && _completionAugmentationInFlightAccessor(); },
                    LanguageAnalysisDebounceMs,
                    delegate { },
                    delegate { if (_processCompletionAugmentationResponses != null) _processCompletionAugmentationResponses(); },
                    LogLanguageInitializationProgress,
                    ResetLanguageTrackingForInactiveDocument,
                    FindOpenDocument,
                    ResolveProjectForDocument,
                    BuildLanguageSourceRoots,
                    BuildLanguageFingerprint,
                    delegate(DocumentSession session, DocumentLanguageCompletionRequestState pending)
                    {
                        return _buildCompletionAugmentationRequest != null ? _buildCompletionAugmentationRequest(session, pending) : null;
                    },
                    delegate(DocumentSession session, DocumentLanguageCompletionRequestState pending, CompletionAugmentationRequest request, LanguageServiceCompletionResponse primaryResponse)
                    {
                        return _tryQueueCompletionAugmentation != null && _tryQueueCompletionAugmentation(session, pending, request, primaryResponse);
                    },
                    delegate { if (_dispatchDeferredCompletionAugmentation != null) _dispatchDeferredCompletionAugmentation(); });
            }

            return _runtimeContext;
        }

        private ShellServiceMap GetServiceMap() { return _serviceMapAccessor != null ? _serviceMapAccessor() : null; }
        private CortexShellLanguageRuntimeState GetRuntimeState() { var session = _activeSession as ICortexLanguageProviderSession; return session != null ? session.RuntimeState : null; }

        private void PublishRuntimeStatus(bool isRunning, string statusMessage)
        {
            var runtime = GetRuntimeState();
            if (runtime == null)
            {
                return;
            }

            runtime.LastStatusSucceeded = isRunning;
            runtime.RuntimeStatusMessage = statusMessage ?? string.Empty;
            runtime.CachedProjectCount = 0;
            runtime.CapabilityIds = new string[0];
        }

        private void PublishSnapshot(LanguageRuntimeLifecycleState lifecycleState, LanguageRuntimeHealthState healthState, string statusMessage, string lastErrorSummary, LanguageProviderDescriptor provider)
        {
            _state.LanguageRuntime = new LanguageRuntimeSnapshot
            {
                Provider = CloneDescriptor(provider),
                LifecycleState = lifecycleState,
                HealthState = healthState,
                Capabilities = new LanguageCapabilitiesSnapshot(),
                StatusMessage = statusMessage ?? string.Empty,
                LastErrorSummary = lastErrorSummary ?? string.Empty,
                ActiveGeneration = _activeGeneration
            };
        }

        private void PublishSnapshotFromCurrentState()
        {
            var runtime = GetRuntimeState();
            var provider = _activeSession != null ? _activeSession.Descriptor : (_state.LanguageRuntime != null ? _state.LanguageRuntime.Provider : new LanguageProviderDescriptor());
            var lifecycleState = LanguageRuntimeLifecycleState.Disabled;
            if (_activeSession != null)
            {
                lifecycleState = runtime != null && runtime.ServiceInitializing
                    ? (_state.LanguageRuntime != null && _state.LanguageRuntime.LifecycleState == LanguageRuntimeLifecycleState.Reloading ? LanguageRuntimeLifecycleState.Reloading : LanguageRuntimeLifecycleState.Starting)
                    : LanguageRuntimeLifecycleState.Running;
            }

            var healthState = LanguageRuntimeHealthState.Healthy;
            if (_activeSession == null)
            {
                healthState = _state.LanguageRuntime != null ? _state.LanguageRuntime.HealthState : LanguageRuntimeHealthState.Healthy;
            }
            else if (!string.IsNullOrEmpty(_activeSession.LastError) && (runtime == null || !runtime.ServiceReady))
            {
                healthState = LanguageRuntimeHealthState.Faulted;
            }
            else if (runtime != null &&
                !runtime.LastStatusSucceeded &&
                !string.IsNullOrEmpty(runtime.RuntimeStatusMessage) &&
                !string.Equals(runtime.RuntimeStatusMessage, "starting", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(runtime.RuntimeStatusMessage, "reloading", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(runtime.RuntimeStatusMessage, "standby", StringComparison.OrdinalIgnoreCase))
            {
                healthState = LanguageRuntimeHealthState.Faulted;
            }

            var statusMessage = runtime != null && !string.IsNullOrEmpty(runtime.RuntimeStatusMessage)
                ? runtime.RuntimeStatusMessage
                : (_state.LanguageRuntime != null ? _state.LanguageRuntime.StatusMessage : string.Empty);
            var lastError = !string.IsNullOrEmpty(_activeSession != null ? _activeSession.LastError : string.Empty) ? _activeSession.LastError : (_state.LanguageRuntime != null ? _state.LanguageRuntime.LastErrorSummary : string.Empty);
            _state.LanguageRuntime = new LanguageRuntimeSnapshot
            {
                Provider = BuildProviderDescriptor(provider, runtime),
                LifecycleState = lifecycleState,
                HealthState = healthState,
                Capabilities = BuildCapabilities(runtime != null ? runtime.CapabilityIds : null),
                StatusMessage = statusMessage ?? string.Empty,
                LastErrorSummary = lastError ?? string.Empty,
                ActiveGeneration = runtime != null ? runtime.ServiceGeneration : _activeGeneration
            };
        }

        private static LanguageProviderDescriptor BuildProviderDescriptor(LanguageProviderDescriptor source, CortexShellLanguageRuntimeState runtime)
        {
            var descriptor = CloneDescriptor(source);
            if (runtime != null)
            {
                if (!string.IsNullOrEmpty(runtime.ProviderDisplayName)) descriptor.DisplayName = runtime.ProviderDisplayName;
                if (!string.IsNullOrEmpty(runtime.ProviderVersion)) descriptor.Version = runtime.ProviderVersion;
            }

            return descriptor;
        }

        private static LanguageCapabilitiesSnapshot BuildCapabilities(string[] ids)
        {
            var capabilities = new LanguageCapabilitiesSnapshot();
            ids = ids ?? new string[0];
            capabilities.CapabilityIds = (string[])ids.Clone();
            capabilities.SupportsAnalysis = HasCapability(ids, "analysis");
            capabilities.SupportsDiagnostics = HasCapability(ids, "diagnostics");
            capabilities.SupportsSemanticTokens = HasCapability(ids, "semantic-tokens");
            capabilities.SupportsHover = HasCapability(ids, "hover");
            capabilities.SupportsDefinition = HasCapability(ids, "definition");
            capabilities.SupportsCompletion = HasCapability(ids, "completion");
            capabilities.SupportsSignatureHelp = HasCapability(ids, "signature-help");
            capabilities.SupportsRename = HasCapability(ids, "rename");
            capabilities.SupportsReferences = HasCapability(ids, "references");
            capabilities.SupportsImplementations = HasCapability(ids, "implementations");
            capabilities.SupportsBaseSymbols = HasCapability(ids, "base-symbol");
            capabilities.SupportsCallHierarchy = HasCapability(ids, "call-hierarchy");
            capabilities.SupportsValueSource = HasCapability(ids, "value-source");
            capabilities.SupportsDocumentTransforms = HasCapability(ids, "document-transforms");
            return capabilities;
        }

        private static bool HasCapability(string[] capabilities, string capability)
        {
            if (capabilities == null || string.IsNullOrEmpty(capability)) return false;
            for (var i = 0; i < capabilities.Length; i++) if (string.Equals(capabilities[i], capability, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private void ResetLanguageTrackingForInactiveDocument()
        {
            var runtime = GetRuntimeState();
            var activeDocument = _state.Documents.ActiveDocument;
            if (runtime == null || (activeDocument != null && !string.IsNullOrEmpty(activeDocument.FilePath))) return;
            runtime.LastAnalyzedDocumentFingerprint = string.Empty;
            runtime.PendingLanguageAnalysisFingerprint = string.Empty;
            _editorCompletionService.Reset(_state.Editor.Completion);
            _editorSignatureHelpService.Reset(_state.Editor.SignatureHelp);
        }

        private void LogLanguageInitializationProgress()
        {
            var runtime = GetRuntimeState();
            if (runtime == null || !runtime.ServiceInitializing || _activeSession == null || string.IsNullOrEmpty(runtime.InitializeRequestId) || runtime.InitializeQueuedUtc == DateTime.MinValue) return;
            var now = DateTime.UtcNow;
            if ((now - runtime.LastInitializationProgressLogUtc).TotalSeconds < 5d) return;
            runtime.LastInitializationProgressLogUtc = now;
            MMLog.WriteDebug("[Cortex.LanguageRuntime] Waiting for initialize response. RequestId=" + runtime.InitializeRequestId + ", ElapsedMs=" + (int)(now - runtime.InitializeQueuedUtc).TotalMilliseconds + ", ProviderRunning=" + _activeSession.IsRunning + ", LastError=" + (_activeSession.LastError ?? string.Empty) + ".");
        }

        private DocumentSession FindOpenDocument(string filePath) { return CortexModuleUtil.FindOpenDocument(_state, filePath); }

        private LanguageServiceInitializeRequest BuildLanguageInitializeRequest()
        {
            return new LanguageServiceInitializeRequest
            {
                WorkspaceRootPath = _state.Settings != null ? _state.Settings.WorkspaceRootPath : string.Empty,
                SourceRoots = BuildLanguageSourceRoots(_state.Settings, _state.SelectedProject),
                ProjectFilePaths = CollectLanguageWarmupProjectPaths(),
                SolutionFilePaths = new string[0],
                ReferenceAssemblyPaths = CollectLanguageReferenceAssemblyPaths()
            };
        }

        private string[] CollectLanguageWarmupProjectPaths()
        {
            var projectPaths = new List<string>();
            AddLanguageWarmupProjectPath(projectPaths, _state.SelectedProject);
            if (_state.Documents.ActiveDocument != null) AddLanguageWarmupProjectPath(projectPaths, ResolveProjectForDocument(_state.Documents.ActiveDocument.FilePath));
            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var session = _state.Documents.OpenDocuments[i];
                if (session != null && !string.IsNullOrEmpty(session.FilePath)) AddLanguageWarmupProjectPath(projectPaths, ResolveProjectForDocument(session.FilePath));
            }
            return projectPaths.ToArray();
        }

        private static void AddLanguageWarmupProjectPath(List<string> projectPaths, CortexProjectDefinition project)
        {
            if (projectPaths == null || project == null || string.IsNullOrEmpty(project.ProjectFilePath)) return;
            try
            {
                var fullPath = Path.GetFullPath(project.ProjectFilePath);
                if (!File.Exists(fullPath)) return;
                for (var i = 0; i < projectPaths.Count; i++) if (string.Equals(projectPaths[i], fullPath, StringComparison.OrdinalIgnoreCase)) return;
                projectPaths.Add(fullPath);
            }
            catch { }
        }

        private string[] CollectLanguageReferenceAssemblyPaths()
        {
            var assemblyPaths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++) TryAddAssemblyPath(assemblyPaths, seen, SafeAssemblyLocation(assemblies[i]));
            CollectAssemblyPathsFromDirectory(_state.Settings != null ? _state.Settings.ManagedAssemblyRootPath : string.Empty, assemblyPaths, seen);
            return assemblyPaths.ToArray();
        }

        private static void CollectAssemblyPathsFromDirectory(string rootPath, List<string> assemblyPaths, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) return;
            string[] dllFiles;
            try { dllFiles = Directory.GetFiles(rootPath, "*.dll", SearchOption.TopDirectoryOnly); }
            catch { return; }
            for (var i = 0; i < dllFiles.Length; i++) TryAddAssemblyPath(assemblyPaths, seen, dllFiles[i]);
        }

        private static void TryAddAssemblyPath(List<string> assemblyPaths, HashSet<string> seen, string assemblyPath)
        {
            if (assemblyPaths == null || seen == null || string.IsNullOrEmpty(assemblyPath)) return;
            try
            {
                var normalized = Path.GetFullPath(assemblyPath);
                if (!File.Exists(normalized) || !seen.Add(normalized)) return;
                assemblyPaths.Add(normalized);
            }
            catch { }
        }

        private static string SafeAssemblyLocation(Assembly assembly)
        {
            try { return assembly != null ? assembly.Location : string.Empty; }
            catch { return string.Empty; }
        }

        private CortexProjectDefinition ResolveProjectForDocument(string filePath)
        {
            var serviceMap = GetServiceMap();
            var projectCatalog = serviceMap != null ? serviceMap.ProjectCatalog : null;
            if (string.IsNullOrEmpty(filePath) || projectCatalog == null) return _state.SelectedProject;
            if (_state.SelectedProject != null && IsPathWithinRoot(filePath, _state.SelectedProject.SourceRootPath)) return _state.SelectedProject;
            var projects = projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project != null && IsPathWithinRoot(filePath, project.SourceRootPath)) return project;
            }
            return _state.SelectedProject;
        }

        private static string[] BuildLanguageSourceRoots(CortexSettings settings, CortexProjectDefinition project) { return SourceRootSetBuilder.Build(project, settings, SourceRootSetBuilder.LanguageServiceRoots).ToArray(); }
        private static bool IsPathWithinRoot(string filePath, string rootPath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(rootPath)) return false;
            try
            {
                var fullFile = Path.GetFullPath(filePath);
                var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
        private static string BuildLanguageFingerprint(DocumentSession session) { return session == null ? string.Empty : (session.FilePath ?? string.Empty) + "|" + session.TextVersion; }

        private static LanguageRuntimeSnapshot CloneSnapshot(LanguageRuntimeSnapshot snapshot)
        {
            if (snapshot == null) return new LanguageRuntimeSnapshot();
            return new LanguageRuntimeSnapshot
            {
                Provider = CloneDescriptor(snapshot.Provider),
                LifecycleState = snapshot.LifecycleState,
                HealthState = snapshot.HealthState,
                Capabilities = CloneCapabilities(snapshot.Capabilities),
                StatusMessage = snapshot.StatusMessage ?? string.Empty,
                LastErrorSummary = snapshot.LastErrorSummary ?? string.Empty,
                ActiveGeneration = snapshot.ActiveGeneration
            };
        }

        private static LanguageProviderDescriptor CloneDescriptor(LanguageProviderDescriptor descriptor)
        {
            if (descriptor == null) return new LanguageProviderDescriptor();
            return new LanguageProviderDescriptor
            {
                ProviderId = descriptor.ProviderId ?? string.Empty,
                DisplayName = descriptor.DisplayName ?? string.Empty,
                Version = descriptor.Version ?? string.Empty,
                Source = descriptor.Source ?? string.Empty
            };
        }

        private static LanguageCapabilitiesSnapshot CloneCapabilities(LanguageCapabilitiesSnapshot capabilities)
        {
            if (capabilities == null) return new LanguageCapabilitiesSnapshot();
            return new LanguageCapabilitiesSnapshot
            {
                SupportsAnalysis = capabilities.SupportsAnalysis,
                SupportsDiagnostics = capabilities.SupportsDiagnostics,
                SupportsSemanticTokens = capabilities.SupportsSemanticTokens,
                SupportsHover = capabilities.SupportsHover,
                SupportsDefinition = capabilities.SupportsDefinition,
                SupportsCompletion = capabilities.SupportsCompletion,
                SupportsSignatureHelp = capabilities.SupportsSignatureHelp,
                SupportsRename = capabilities.SupportsRename,
                SupportsReferences = capabilities.SupportsReferences,
                SupportsImplementations = capabilities.SupportsImplementations,
                SupportsBaseSymbols = capabilities.SupportsBaseSymbols,
                SupportsCallHierarchy = capabilities.SupportsCallHierarchy,
                SupportsValueSource = capabilities.SupportsValueSource,
                SupportsDocumentTransforms = capabilities.SupportsDocumentTransforms,
                CapabilityIds = capabilities.CapabilityIds != null ? (string[])capabilities.CapabilityIds.Clone() : new string[0]
            };
        }
    }
}
