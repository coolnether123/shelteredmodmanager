using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using GameModding.Shared.Restart;

namespace Cortex.Core.Abstractions
{
    public interface IProjectCatalog
    {
        IList<CortexProjectDefinition> GetProjects();
        CortexProjectDefinition GetProject(string modId);
        void Upsert(CortexProjectDefinition definition);
        void Remove(string modId);
    }

    public interface ILoadedModCatalog
    {
        IList<LoadedModInfo> GetLoadedMods();
        LoadedModInfo GetMod(string modId);
    }

    public interface IProjectConfigurationStore
    {
        IList<CortexProjectDefinition> LoadProjects();
        void SaveProjects(IList<CortexProjectDefinition> projects);
    }

    public interface ICortexSettingsStore
    {
        CortexSettings Load();
        void Save(CortexSettings settings);
    }

    public interface IWorkspaceLocator
    {
        CortexWorkspacePaths GetWorkspace(CortexProjectDefinition project);
    }

    public interface IProjectWorkspaceService
    {
        ProjectWorkspaceAnalysis AnalyzeSourceRoot(string sourceRoot, string preferredModId);
        ProjectWorkspaceImportResult DiscoverWorkspaceProjects(string workspaceRoot);
        ProjectValidationResult Validate(CortexProjectDefinition definition);
        string FindLikelySourceRoot(string modRootPath);
    }

    public interface IWorkspaceBrowserService
    {
        WorkspaceTreeNode BuildTree(string rootPath, WorkspaceTreeKind kind);
        void Refresh(string rootPath, WorkspaceTreeKind kind);
    }

    public interface IDecompilerExplorerService
    {
        WorkspaceTreeNode BuildTree(string preferredRootPath);
        void EnsureChildren(WorkspaceTreeNode node);
    }

    public interface IDocumentService
    {
        DocumentSession Open(string filePath);
        bool Save(DocumentSession session);
        bool Reload(DocumentSession session);
        bool HasExternalChanges(DocumentSession session);
    }

    public interface ILanguageServiceClient : System.IDisposable
    {
        bool IsEnabled { get; }
        bool IsRunning { get; }
        string LastError { get; }
        string QueueInitialize(LanguageServiceInitializeRequest request);
        string QueueStatus();
        string QueueAnalyzeDocument(LanguageServiceDocumentRequest request);
        string QueueHover(LanguageServiceHoverRequest request);
        string QueueGoToDefinition(LanguageServiceDefinitionRequest request);
        bool TryDequeueResponse(out LanguageServiceEnvelope envelope);
        LanguageServiceInitializeResponse Initialize(LanguageServiceInitializeRequest request);
        LanguageServiceStatusResponse GetStatus();
        LanguageServiceAnalysisResponse AnalyzeDocument(LanguageServiceDocumentRequest request);
        LanguageServiceHoverResponse GetHover(LanguageServiceHoverRequest request);
        LanguageServiceDefinitionResponse GoToDefinition(LanguageServiceDefinitionRequest request);
        void Shutdown();
    }

    public interface ISourcePathResolver
    {
        string ResolveCandidatePath(CortexProjectDefinition project, CortexSettings settings, string rawPath);
        SourceLocationMatch ResolveTextLocation(string text, CortexProjectDefinition project, CortexSettings settings);
        IList<string> GetSearchRoots(CortexProjectDefinition project, CortexSettings settings);
    }

    public interface ISourceLookupIndex
    {
        void RefreshRoot(string rootPath);
        void RefreshRoots(IList<string> rootPaths);
        string ResolvePath(IList<string> searchRoots, string rawPath);
        string ResolveAssemblyPath(IList<string> searchRoots, string assemblyName);
        IList<string> GetProjectFiles(string rootPath);
        WorkspaceTreeNode BuildTree(string rootPath, WorkspaceTreeKind kind);
    }

    public interface IBuildCommandResolver
    {
        BuildCommand Resolve(CortexProjectDefinition project, bool clean, string configuration);
    }

    public interface IBuildExecutor
    {
        BuildResult Execute(BuildCommand command);
    }

    public interface IBuildOutputParser
    {
        IList<BuildDiagnostic> Parse(IList<string> outputLines);
    }

    public interface IDecompilerClient
    {
        DecompilerResponse Decompile(DecompilerRequest request);
    }

    public interface ISourceReferenceService
    {
        DecompilerResponse GetSource(DecompilerRequest request);
        int MapSourceLineToOffset(string mapText, int sourceLine);
        int MapOffsetToSourceLine(string mapText, int ilOffset);
    }

    public interface IReferenceCatalogService
    {
        IList<ReferenceAssemblyDescriptor> GetAssemblies(string preferredRootPath);
        IList<ReferenceTypeDescriptor> GetTypes(string assemblyPath);
        IList<ReferenceMemberDescriptor> GetMembers(string assemblyPath, string typeName);
    }

    public interface IRuntimeLogFeed
    {
        IList<RuntimeLogEntry> ReadRecent(string minimumLevel, int maxCount);
        IList<string> ReadBacklog(string logPath, int maxCount);
    }

    public interface IRuntimeSymbolResolver
    {
        SourceNavigationTarget Resolve(RuntimeStackFrame frame, CortexProjectDefinition project, CortexSettings settings);
    }

    public interface IRuntimeSourceNavigationService
    {
        SourceNavigationTarget Resolve(RuntimeLogEntry entry, int frameIndex, CortexProjectDefinition project, CortexSettings settings);
    }

    public interface IRuntimeToolBridge
    {
        IList<RuntimeToolStatus> GetTools();
        bool Execute(string toolId, out string statusMessage);
        void ToggleRuntimeInspector();
        void ToggleIlInspector();
        void ToggleUiDebugger();
        void ToggleRuntimeDebugger();
    }

    public interface IRestartCoordinator
    {
        bool RequestCurrentSessionRestart(out string errorMessage);
        bool RequestManifestRestart(RestartRequest request, out string errorMessage);
    }
}
