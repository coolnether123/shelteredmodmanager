using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services;
namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private ILanguageServiceClient _languageServiceClient;
        private readonly DocumentLanguageAnalysisService _documentLanguageAnalysisService = new DocumentLanguageAnalysisService();
        private readonly DocumentLanguageInteractionService _documentLanguageInteractionService = new DocumentLanguageInteractionService();
        private readonly EditorCompletionService _editorCompletionService = new EditorCompletionService();
        private readonly EditorSignatureHelpService _editorSignatureHelpService = new EditorSignatureHelpService();
        private const double LanguageAnalysisDebounceMs = 280d;

        private void InitializeLanguageService(string smmBin, CortexSettings settings)
        {
            InitializeCompletionAugmentation(settings);
            if (settings == null || !settings.EnableRoslynLanguageService)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Language service disabled by settings. ExistingClient=" + (_languageServiceClient != null) + ".");
                ShutdownLanguageService();
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = "disabled",
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                return;
            }

            var workerPath = ResolveLanguageWorkerPath(settings, smmBin);
            if (string.IsNullOrEmpty(workerPath))
            {
                MMLog.WriteWarning("[Cortex.Roslyn] Language service worker path could not be resolved. ExistingClient=" + (_languageServiceClient != null) + ".");
                ShutdownLanguageService();
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = "worker not found",
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                _state.Diagnostics.Add(_state.LanguageServiceStatus.StatusMessage);
                return;
            }

            var initializeRequest = BuildLanguageInitializeRequest();
            var configurationFingerprint = BuildLanguageServiceConfigurationFingerprint(workerPath, settings, initializeRequest);
            if (_languageServiceClient != null &&
                string.Equals(_languageRuntime.ServiceConfigurationFingerprint, configurationFingerprint, StringComparison.Ordinal))
            {
                _languageRuntime.InitializeRequest = initializeRequest;
                MMLog.WriteDebug("[Cortex.Roslyn] Reusing existing language service configuration. Generation=" + _languageRuntime.ServiceGeneration +
                    ", Ready=" + _languageRuntime.ServiceReady +
                    ", Initializing=" + _languageRuntime.ServiceInitializing +
                    ", WorkspaceRoot=" + (initializeRequest.WorkspaceRootPath ?? string.Empty) +
                    ", Projects=" + (initializeRequest.ProjectFilePaths != null ? initializeRequest.ProjectFilePaths.Length : 0) +
                    ", SourceRoots=" + (initializeRequest.SourceRoots != null ? initializeRequest.SourceRoots.Length : 0) +
                    ", References=" + (initializeRequest.ReferenceAssemblyPaths != null ? initializeRequest.ReferenceAssemblyPaths.Length : 0) + ".");
                return;
            }

            if (_languageServiceClient != null)
            {
                MMLog.WriteInfo("[Cortex.Roslyn] Reinitializing language service due to configuration change. PreviousGeneration=" + _languageRuntime.ServiceGeneration +
                    ", PreviousFingerprintLength=" + (_languageRuntime.ServiceConfigurationFingerprint ?? string.Empty).Length +
                    ", NewFingerprintLength=" + (configurationFingerprint ?? string.Empty).Length + ".");
            }

            ShutdownLanguageService();

            MMLog.WriteDebug("[Cortex.Roslyn] Resolved worker path: " + workerPath);
            _languageServiceClient = new RoslynLanguageServiceClient(
                workerPath,
                settings.RoslynServiceTimeoutMs,
                delegate(string message) { MMLog.WriteDebug(message); });
            ResetLanguageRuntimeState(initializeRequest, configurationFingerprint);
            Interlocked.Increment(ref _languageRuntime.ServiceGeneration);

            _state.LanguageServiceStatus = new LanguageServiceStatusResponse
            {
                Success = true,
                StatusMessage = "standby",
                IsRunning = false,
                Capabilities = new string[0],
                LoadedProjectPaths = new string[0]
            };
        }

        private void EnsureLanguageServiceStarted()
        {
            if (_languageServiceClient == null || _languageRuntime.ServiceReady || _languageRuntime.ServiceInitializing || !string.IsNullOrEmpty(_languageRuntime.InitializeRequestId))
            {
                return;
            }

            if (!_sessionCoordinator.Visible)
            {
                return;
            }

            var generation = _languageRuntime.ServiceGeneration;
            _languageRuntime.ServiceInitializing = true;
            MMLog.WriteInfo("[Cortex.Roslyn] Starting language service worker in background. Generation=" + generation +
                ", FingerprintLength=" + (_languageRuntime.ServiceConfigurationFingerprint ?? string.Empty).Length +
                ", WorkspaceRoot=" + (_languageRuntime.InitializeRequest != null ? (_languageRuntime.InitializeRequest.WorkspaceRootPath ?? string.Empty) : string.Empty) +
                ", Projects=" + (_languageRuntime.InitializeRequest != null && _languageRuntime.InitializeRequest.ProjectFilePaths != null ? _languageRuntime.InitializeRequest.ProjectFilePaths.Length : 0) +
                ", SourceRoots=" + (_languageRuntime.InitializeRequest != null && _languageRuntime.InitializeRequest.SourceRoots != null ? _languageRuntime.InitializeRequest.SourceRoots.Length : 0) +
                ", References=" + (_languageRuntime.InitializeRequest != null && _languageRuntime.InitializeRequest.ReferenceAssemblyPaths != null ? _languageRuntime.InitializeRequest.ReferenceAssemblyPaths.Length : 0) + ".");
            _state.LanguageServiceStatus = new LanguageServiceStatusResponse
            {
                Success = true,
                StatusMessage = "starting",
                IsRunning = false,
                Capabilities = new string[0],
                LoadedProjectPaths = new string[0]
            };
            _languageRuntime.InitializeRequestId = _languageServiceClient.QueueInitialize(_languageRuntime.InitializeRequest ?? BuildLanguageInitializeRequest());
            if (string.IsNullOrEmpty(_languageRuntime.InitializeRequestId))
            {
                _languageRuntime.ServiceInitializing = false;
                _state.LanguageServiceStatus = new LanguageServiceStatusResponse
                {
                    Success = false,
                    StatusMessage = string.IsNullOrEmpty(_languageServiceClient.LastError) ? "startup failed" : _languageServiceClient.LastError,
                    IsRunning = false,
                    Capabilities = new string[0],
                    LoadedProjectPaths = new string[0]
                };
                _state.Diagnostics.Add("Roslyn worker failed to queue initialization: " + _state.LanguageServiceStatus.StatusMessage);
                MMLog.WriteWarning("[Cortex.Roslyn] Worker initialize queue failed. Generation=" + generation +
                    ", Error=" + _state.LanguageServiceStatus.StatusMessage + ".");
                return;
            }

            _languageRuntime.InitializeQueuedUtc = DateTime.UtcNow;
            _languageRuntime.LastInitializationProgressLogUtc = DateTime.UtcNow;
            MMLog.WriteDebug("[Cortex.Roslyn] Worker initialize queued. Generation=" + generation +
                ", RequestId=" + _languageRuntime.InitializeRequestId + ".");
        }

        private void ShutdownLanguageService()
        {
            var hadClient = _languageServiceClient != null;
            if (hadClient || _languageRuntime.ServiceReady || _languageRuntime.ServiceInitializing || _languageRuntime.AnalysisInFlight || _languageRuntime.HoverInFlight || _languageRuntime.DefinitionInFlight)
            {
                MMLog.WriteDebug("[Cortex.Roslyn] Shutting down language service. HadClient=" + hadClient +
                    ", Ready=" + _languageRuntime.ServiceReady +
                    ", Initializing=" + _languageRuntime.ServiceInitializing +
                    ", AnalysisInFlight=" + _languageRuntime.AnalysisInFlight +
                    ", HoverInFlight=" + _languageRuntime.HoverInFlight +
                    ", DefinitionInFlight=" + _languageRuntime.DefinitionInFlight +
                    ", CompletionInFlight=" + _languageRuntime.CompletionInFlight +
                    ", SemanticOperationInFlight=" + _languageRuntime.SemanticOperationInFlight +
                    ", Generation=" + _languageRuntime.ServiceGeneration + ".");
            }

            Interlocked.Increment(ref _languageRuntime.ServiceGeneration);
            var disposable = _languageServiceClient as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }

            _languageServiceClient = null;
            ResetLanguageRuntimeState(null, string.Empty);
        }

        private void UpdateLanguageService()
        {
            _languageCoordinator.UpdateLanguageService(GetLanguageRuntimeContext(), _languageRequestDispatcher, _languageResponseProcessor);
        }

        private void ResetLanguageTrackingForInactiveDocument()
        {
            var activeDocument = _state.Documents.ActiveDocument;
            if (activeDocument == null || string.IsNullOrEmpty(activeDocument.FilePath))
            {
                _languageRuntime.LastAnalyzedDocumentFingerprint = string.Empty;
                _languageRuntime.PendingLanguageAnalysisFingerprint = string.Empty;
                _editorCompletionService.Reset(_state.Editor);
                _editorSignatureHelpService.Reset(_state.Editor);
            }
        }

        private void LogLanguageInitializationProgress()
        {
            if (!_languageRuntime.ServiceInitializing ||
                _languageServiceClient == null ||
                string.IsNullOrEmpty(_languageRuntime.InitializeRequestId) ||
                _languageRuntime.InitializeQueuedUtc == DateTime.MinValue)
            {
                return;
            }

            var now = DateTime.UtcNow;
            if ((now - _languageRuntime.LastInitializationProgressLogUtc).TotalSeconds < 5d)
            {
                return;
            }

            _languageRuntime.LastInitializationProgressLogUtc = now;
            MMLog.WriteDebug("[Cortex.Roslyn] Waiting for initialize response. RequestId=" + _languageRuntime.InitializeRequestId +
                ", ElapsedMs=" + (int)(now - _languageRuntime.InitializeQueuedUtc).TotalMilliseconds +
                ", ClientRunning=" + _languageServiceClient.IsRunning +
                ", LastError=" + (_languageServiceClient.LastError ?? string.Empty) + ".");
        }

        private CortexShellLanguageRuntimeContext GetLanguageRuntimeContext()
        {
            if (_languageRuntimeContext == null)
            {
                _languageRuntimeContext = new CortexShellLanguageRuntimeContext(
                    _state,
                    _languageRuntime,
                    _documentLanguageAnalysisService,
                    _documentLanguageInteractionService,
                    _editorCompletionService,
                    _editorSignatureHelpService,
                    delegate { return _languageServiceClient; },
                    delegate { return _navigationService; },
                    delegate { return _completionAugmentationInFlight; },
                    LanguageAnalysisDebounceMs,
                    delegate { EnsureLanguageServiceStarted(); },
                    delegate { ProcessCompletionAugmentationResponses(); },
                    delegate { LogLanguageInitializationProgress(); },
                    delegate { ResetLanguageTrackingForInactiveDocument(); },
                    delegate(string filePath) { return FindOpenDocument(filePath); },
                    delegate(string filePath) { return ResolveProjectForDocument(filePath); },
                    delegate(CortexSettings settings, CortexProjectDefinition project) { return BuildLanguageSourceRoots(settings, project); },
                    delegate(DocumentSession session) { return BuildLanguageFingerprint(session); },
                    delegate(DocumentSession session, DocumentLanguageCompletionRequestState pending) { return BuildCompletionAugmentationRequest(session, pending); },
                    delegate(DocumentSession session, DocumentLanguageCompletionRequestState pending, CompletionAugmentationRequest request, LanguageServiceCompletionResponse primaryResponse)
                    {
                        return TryQueueCompletionAugmentation(session, pending, request, primaryResponse);
                    },
                    delegate { DispatchDeferredCompletionAugmentation(); });
            }

            return _languageRuntimeContext;
        }

        private void ResetLanguageRuntimeState(LanguageServiceInitializeRequest initializeRequest, string configurationFingerprint)
        {
            _languageRuntime.InitializeRequest = initializeRequest;
            _languageRuntime.LastAnalyzedDocumentFingerprint = string.Empty;
            _languageRuntime.PendingLanguageAnalysisFingerprint = string.Empty;
            _languageRuntime.ServiceReady = false;
            _languageRuntime.ServiceInitializing = false;
            _languageRuntime.AnalysisInFlight = false;
            _languageRuntime.HoverInFlight = false;
            _languageRuntime.DefinitionInFlight = false;
            _languageRuntime.CompletionInFlight = false;
            _languageRuntime.SignatureHelpInFlight = false;
            _languageRuntime.SemanticOperationInFlight = false;
            _languageRuntime.MethodInspectorCallHierarchyInFlight = false;
            _languageRuntime.InitializeRequestId = string.Empty;
            _languageRuntime.StatusRequestId = string.Empty;
            _languageRuntime.PendingAnalysis = null;
            _languageRuntime.PendingHover = null;
            _languageRuntime.PendingDefinition = null;
            _languageRuntime.PendingCompletion = null;
            _languageRuntime.PendingSignatureHelp = null;
            _languageRuntime.PendingSemanticOperation = null;
            _languageRuntime.PendingMethodInspectorCallHierarchy = null;
            _languageRuntime.LastAnalysisRequestUtc = DateTime.MinValue;
            _languageRuntime.InitializeQueuedUtc = DateTime.MinValue;
            _languageRuntime.LastInitializationProgressLogUtc = DateTime.MinValue;
            _languageRuntime.ServiceConfigurationFingerprint = configurationFingerprint ?? string.Empty;
            _editorCompletionService.Reset(_state.Editor);
            _editorSignatureHelpService.Reset(_state.Editor);
        }

        private DocumentSession FindOpenDocument(string filePath)
        {
            return CortexModuleUtil.FindOpenDocument(_state, filePath);
        }

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

            if (_state.Documents.ActiveDocument != null)
            {
                AddLanguageWarmupProjectPath(projectPaths, ResolveProjectForDocument(_state.Documents.ActiveDocument.FilePath));
            }

            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var session = _state.Documents.OpenDocuments[i];
                if (session == null || string.IsNullOrEmpty(session.FilePath))
                {
                    continue;
                }

                AddLanguageWarmupProjectPath(projectPaths, ResolveProjectForDocument(session.FilePath));
            }

            return projectPaths.ToArray();
        }

        private static void AddLanguageWarmupProjectPath(List<string> projectPaths, CortexProjectDefinition project)
        {
            if (projectPaths == null || project == null || string.IsNullOrEmpty(project.ProjectFilePath))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(project.ProjectFilePath);
                if (File.Exists(fullPath) && !ContainsLanguageWarmupProjectPath(projectPaths, fullPath))
                {
                    projectPaths.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static bool ContainsLanguageWarmupProjectPath(List<string> projectPaths, string candidatePath)
        {
            if (projectPaths == null || string.IsNullOrEmpty(candidatePath))
            {
                return false;
            }

            for (var i = 0; i < projectPaths.Count; i++)
            {
                if (string.Equals(projectPaths[i], candidatePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string[] CollectLanguageReferenceAssemblyPaths()
        {
            var assemblyPaths = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectLoadedAssemblyPaths(assemblyPaths, seen);
            CollectAssemblyPathsFromDirectory(_state.Settings != null ? _state.Settings.ManagedAssemblyRootPath : string.Empty, assemblyPaths, seen);

            return assemblyPaths.ToArray();
        }

        private static void CollectLoadedAssemblyPaths(List<string> assemblyPaths, HashSet<string> seen)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                TryAddAssemblyPath(assemblyPaths, seen, SafeAssemblyLocation(assemblies[i]));
            }
        }

        private static void CollectAssemblyPathsFromDirectory(string rootPath, List<string> assemblyPaths, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                return;
            }

            string[] dllFiles;
            try
            {
                dllFiles = Directory.GetFiles(rootPath, "*.dll", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            for (var i = 0; i < dllFiles.Length; i++)
            {
                TryAddAssemblyPath(assemblyPaths, seen, dllFiles[i]);
            }
        }

        private static void TryAddAssemblyPath(List<string> assemblyPaths, HashSet<string> seen, string assemblyPath)
        {
            if (assemblyPaths == null || seen == null || string.IsNullOrEmpty(assemblyPath))
            {
                return;
            }

            try
            {
                var normalized = Path.GetFullPath(assemblyPath);
                if (!File.Exists(normalized) || !seen.Add(normalized))
                {
                    return;
                }

                assemblyPaths.Add(normalized);
            }
            catch
            {
            }
        }

        private static string SafeAssemblyLocation(Assembly assembly)
        {
            try
            {
                return assembly != null ? assembly.Location : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private CortexProjectDefinition ResolveProjectForDocument(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || _projectCatalog == null)
            {
                return _state.SelectedProject;
            }

            if (_state.SelectedProject != null &&
                IsPathWithinRoot(filePath, _state.SelectedProject.SourceRootPath))
            {
                return _state.SelectedProject;
            }

            var projects = _projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project != null && IsPathWithinRoot(filePath, project.SourceRootPath))
                {
                    return project;
                }
            }

            return _state.SelectedProject;
        }

        private static string[] BuildLanguageSourceRoots(CortexSettings settings, CortexProjectDefinition project)
        {
            return SourceRootSetBuilder.Build(project, settings, SourceRootSetBuilder.LanguageServiceRoots).ToArray();
        }

        private static bool IsPathWithinRoot(string filePath, string rootPath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var fullFile = Path.GetFullPath(filePath);
                var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return fullFile.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveLanguageWorkerPath(CortexSettings settings, string smmBin)
        {
            var candidates = new List<string>();
            if (settings != null && !string.IsNullOrEmpty(settings.RoslynServicePathOverride))
            {
                candidates.Add(settings.RoslynServicePathOverride);
            }

            candidates.Add(Path.Combine(Path.Combine(smmBin, "roslyn"), "Cortex.Roslyn.Worker.exe"));
            candidates.Add(Path.Combine(Path.Combine(smmBin, "roslyn"), "Cortex.Roslyn.Worker.dll"));

            for (var i = 0; i < candidates.Count; i++)
            {
                try
                {
                    var candidate = Path.GetFullPath(candidates[i]);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static string BuildLanguageFingerprint(DocumentSession session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            return (session.FilePath ?? string.Empty) + "|" + session.TextVersion;
        }

        private static string BuildLanguageServiceConfigurationFingerprint(
            string workerPath,
            CortexSettings settings,
            LanguageServiceInitializeRequest request)
        {
            var builder = new StringBuilder();
            builder.Append(workerPath ?? string.Empty);
            builder.Append("|timeout=");
            builder.Append(settings != null ? settings.RoslynServiceTimeoutMs : 0);
            builder.Append("|workspace=");
            builder.Append(request != null ? request.WorkspaceRootPath ?? string.Empty : string.Empty);
            builder.Append("|projects=");
            AppendFingerprintValues(builder, request != null ? request.ProjectFilePaths : null);
            builder.Append("|sources=");
            AppendFingerprintValues(builder, request != null ? request.SourceRoots : null);
            builder.Append("|references=");
            AppendFingerprintValues(builder, request != null ? request.ReferenceAssemblyPaths : null);
            return builder.ToString();
        }

        private static void AppendFingerprintValues(StringBuilder builder, string[] values)
        {
            if (builder == null || values == null || values.Length == 0)
            {
                return;
            }

            var copy = new List<string>(values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    copy.Add(values[i]);
                }
            }

            copy.Sort(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < copy.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(";");
                }

                builder.Append(copy[i]);
            }
        }

    }
}
