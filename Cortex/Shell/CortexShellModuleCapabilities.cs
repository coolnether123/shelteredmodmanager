using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services;

namespace Cortex
{
    internal interface IHarmonyFeatureServices
    {
        CortexShellState State { get; }
        ICommandRegistry CommandRegistry { get; }
        IContributionRegistry ContributionRegistry { get; }
        CortexNavigationService NavigationService { get; }
        IDocumentService DocumentService { get; }
        IProjectCatalog ProjectCatalog { get; }
        ILoadedModCatalog LoadedModCatalog { get; }
        IPathInteractionService PathInteractionService { get; }
        ISourceLookupIndex SourceLookupIndex { get; }
        HarmonyPatchInspectionService HarmonyPatchInspectionService { get; }
        HarmonyPatchResolutionService HarmonyPatchResolutionService { get; }
        HarmonyPatchDisplayService HarmonyPatchDisplayService { get; }
        HarmonyPatchGenerationService HarmonyPatchGenerationService { get; }
        GeneratedTemplateNavigationService GeneratedTemplateNavigationService { get; }
    }

    internal interface ILogsModuleServices
    {
        CortexShellState State { get; }
        CortexNavigationService NavigationService { get; }
        ISourcePathResolver SourcePathResolver { get; }
        IRuntimeLogFeed RuntimeLogFeed { get; }
    }

    internal interface IProjectsModuleServices
    {
        CortexShellState State { get; }
        IProjectCatalog ProjectCatalog { get; }
        IProjectWorkspaceService ProjectWorkspaceService { get; }
        ILoadedModCatalog LoadedModCatalog { get; }
        IPathInteractionService PathInteractionService { get; }
    }

    internal interface IFileExplorerModuleServices
    {
        CortexShellState State { get; }
        CortexNavigationService NavigationService { get; }
        IWorkspaceBrowserService WorkspaceBrowserService { get; }
        IDecompilerExplorerService DecompilerExplorerService { get; }
    }

    internal interface IEditorModuleServices : IHarmonyFeatureServices
    {
        WorkbenchSearchService WorkbenchSearchService { get; }
    }

    internal interface IBuildModuleServices
    {
        CortexShellState State { get; }
        CortexNavigationService NavigationService { get; }
        ISourcePathResolver SourcePathResolver { get; }
        IBuildCommandResolver BuildCommandResolver { get; }
        IBuildExecutor BuildExecutor { get; }
        IRestartCoordinator RestartCoordinator { get; }
    }

    internal interface IReferenceModuleServices
    {
        CortexShellState State { get; }
        CortexNavigationService NavigationService { get; }
        IReferenceCatalogService ReferenceCatalogService { get; }
    }

    internal interface ISearchModuleServices
    {
        CortexShellState State { get; }
        CortexNavigationService NavigationService { get; }
        IDocumentService DocumentService { get; }
        IProjectCatalog ProjectCatalog { get; }
        ISourceLookupIndex SourceLookupIndex { get; }
        ITextSearchService TextSearchService { get; }
        WorkbenchSearchService WorkbenchSearchService { get; }
    }

    internal interface IRuntimeToolsModuleServices
    {
        CortexShellState State { get; }
        IRuntimeToolBridge RuntimeToolBridge { get; }
    }

    internal interface ISettingsModuleServices
    {
        CortexShellState State { get; }
        ICortexSettingsStore SettingsStore { get; }
        IProjectCatalog ProjectCatalog { get; }
        IProjectWorkspaceService ProjectWorkspaceService { get; }
        ILoadedModCatalog LoadedModCatalog { get; }
        IPathInteractionService PathInteractionService { get; }
        ThemeState ThemeState { get; }
    }

    internal interface IHarmonyModuleServices : IHarmonyFeatureServices
    {
    }

    internal sealed class CortexShellModuleServices :
        ILogsModuleServices,
        IProjectsModuleServices,
        IFileExplorerModuleServices,
        IEditorModuleServices,
        IBuildModuleServices,
        IReferenceModuleServices,
        ISearchModuleServices,
        IRuntimeToolsModuleServices,
        ISettingsModuleServices,
        IHarmonyModuleServices
    {
        private readonly CortexShellState _state;
        private readonly Func<ICortexSettingsStore> _settingsStoreAccessor;
        private readonly Func<IProjectCatalog> _projectCatalogAccessor;
        private readonly Func<ILoadedModCatalog> _loadedModCatalogAccessor;
        private readonly Func<IProjectWorkspaceService> _projectWorkspaceServiceAccessor;
        private readonly Func<IPathInteractionService> _pathInteractionServiceAccessor;
        private readonly Func<IWorkspaceBrowserService> _workspaceBrowserServiceAccessor;
        private readonly Func<IDecompilerExplorerService> _decompilerExplorerServiceAccessor;
        private readonly Func<IDocumentService> _documentServiceAccessor;
        private readonly Func<IBuildCommandResolver> _buildCommandResolverAccessor;
        private readonly Func<IBuildExecutor> _buildExecutorAccessor;
        private readonly Func<IReferenceCatalogService> _referenceCatalogServiceAccessor;
        private readonly Func<ISourcePathResolver> _sourcePathResolverAccessor;
        private readonly Func<ISourceLookupIndex> _sourceLookupIndexAccessor;
        private readonly Func<ITextSearchService> _textSearchServiceAccessor;
        private readonly Func<IRuntimeLogFeed> _runtimeLogFeedAccessor;
        private readonly Func<IRuntimeToolBridge> _runtimeToolBridgeAccessor;
        private readonly Func<IRestartCoordinator> _restartCoordinatorAccessor;
        private readonly Func<CortexNavigationService> _navigationServiceAccessor;
        private readonly Func<IWorkbenchRuntime> _workbenchRuntimeAccessor;
        private readonly Func<WorkbenchSearchService> _workbenchSearchServiceAccessor;
        private readonly Func<HarmonyPatchInspectionService> _harmonyPatchInspectionServiceAccessor;
        private readonly Func<HarmonyPatchResolutionService> _harmonyPatchResolutionServiceAccessor;
        private readonly Func<HarmonyPatchDisplayService> _harmonyPatchDisplayServiceAccessor;
        private readonly Func<HarmonyPatchGenerationService> _harmonyPatchGenerationServiceAccessor;
        private readonly Func<GeneratedTemplateNavigationService> _generatedTemplateNavigationServiceAccessor;

        public CortexShellModuleServices(
            CortexShellState state,
            Func<ICortexSettingsStore> settingsStoreAccessor,
            Func<IProjectCatalog> projectCatalogAccessor,
            Func<ILoadedModCatalog> loadedModCatalogAccessor,
            Func<IProjectWorkspaceService> projectWorkspaceServiceAccessor,
            Func<IPathInteractionService> pathInteractionServiceAccessor,
            Func<IWorkspaceBrowserService> workspaceBrowserServiceAccessor,
            Func<IDecompilerExplorerService> decompilerExplorerServiceAccessor,
            Func<IDocumentService> documentServiceAccessor,
            Func<IBuildCommandResolver> buildCommandResolverAccessor,
            Func<IBuildExecutor> buildExecutorAccessor,
            Func<IReferenceCatalogService> referenceCatalogServiceAccessor,
            Func<ISourcePathResolver> sourcePathResolverAccessor,
            Func<ISourceLookupIndex> sourceLookupIndexAccessor,
            Func<ITextSearchService> textSearchServiceAccessor,
            Func<IRuntimeLogFeed> runtimeLogFeedAccessor,
            Func<IRuntimeToolBridge> runtimeToolBridgeAccessor,
            Func<IRestartCoordinator> restartCoordinatorAccessor,
            Func<CortexNavigationService> navigationServiceAccessor,
            Func<IWorkbenchRuntime> workbenchRuntimeAccessor,
            Func<WorkbenchSearchService> workbenchSearchServiceAccessor,
            Func<HarmonyPatchInspectionService> harmonyPatchInspectionServiceAccessor,
            Func<HarmonyPatchResolutionService> harmonyPatchResolutionServiceAccessor,
            Func<HarmonyPatchDisplayService> harmonyPatchDisplayServiceAccessor,
            Func<HarmonyPatchGenerationService> harmonyPatchGenerationServiceAccessor,
            Func<GeneratedTemplateNavigationService> generatedTemplateNavigationServiceAccessor)
        {
            _state = state;
            _settingsStoreAccessor = settingsStoreAccessor;
            _projectCatalogAccessor = projectCatalogAccessor;
            _loadedModCatalogAccessor = loadedModCatalogAccessor;
            _projectWorkspaceServiceAccessor = projectWorkspaceServiceAccessor;
            _pathInteractionServiceAccessor = pathInteractionServiceAccessor;
            _workspaceBrowserServiceAccessor = workspaceBrowserServiceAccessor;
            _decompilerExplorerServiceAccessor = decompilerExplorerServiceAccessor;
            _documentServiceAccessor = documentServiceAccessor;
            _buildCommandResolverAccessor = buildCommandResolverAccessor;
            _buildExecutorAccessor = buildExecutorAccessor;
            _referenceCatalogServiceAccessor = referenceCatalogServiceAccessor;
            _sourcePathResolverAccessor = sourcePathResolverAccessor;
            _sourceLookupIndexAccessor = sourceLookupIndexAccessor;
            _textSearchServiceAccessor = textSearchServiceAccessor;
            _runtimeLogFeedAccessor = runtimeLogFeedAccessor;
            _runtimeToolBridgeAccessor = runtimeToolBridgeAccessor;
            _restartCoordinatorAccessor = restartCoordinatorAccessor;
            _navigationServiceAccessor = navigationServiceAccessor;
            _workbenchRuntimeAccessor = workbenchRuntimeAccessor;
            _workbenchSearchServiceAccessor = workbenchSearchServiceAccessor;
            _harmonyPatchInspectionServiceAccessor = harmonyPatchInspectionServiceAccessor;
            _harmonyPatchResolutionServiceAccessor = harmonyPatchResolutionServiceAccessor;
            _harmonyPatchDisplayServiceAccessor = harmonyPatchDisplayServiceAccessor;
            _harmonyPatchGenerationServiceAccessor = harmonyPatchGenerationServiceAccessor;
            _generatedTemplateNavigationServiceAccessor = generatedTemplateNavigationServiceAccessor;
        }

        public CortexShellState State { get { return _state; } }
        public CortexNavigationService NavigationService { get { return _navigationServiceAccessor != null ? _navigationServiceAccessor() : null; } }
        public ISourcePathResolver SourcePathResolver { get { return _sourcePathResolverAccessor != null ? _sourcePathResolverAccessor() : null; } }
        public IRuntimeLogFeed RuntimeLogFeed { get { return _runtimeLogFeedAccessor != null ? _runtimeLogFeedAccessor() : null; } }
        public IProjectCatalog ProjectCatalog { get { return _projectCatalogAccessor != null ? _projectCatalogAccessor() : null; } }
        public IProjectWorkspaceService ProjectWorkspaceService { get { return _projectWorkspaceServiceAccessor != null ? _projectWorkspaceServiceAccessor() : null; } }
        public ILoadedModCatalog LoadedModCatalog { get { return _loadedModCatalogAccessor != null ? _loadedModCatalogAccessor() : null; } }
        public IPathInteractionService PathInteractionService { get { return _pathInteractionServiceAccessor != null ? _pathInteractionServiceAccessor() : null; } }
        public IWorkspaceBrowserService WorkspaceBrowserService { get { return _workspaceBrowserServiceAccessor != null ? _workspaceBrowserServiceAccessor() : null; } }
        public IDecompilerExplorerService DecompilerExplorerService { get { return _decompilerExplorerServiceAccessor != null ? _decompilerExplorerServiceAccessor() : null; } }
        public IDocumentService DocumentService { get { return _documentServiceAccessor != null ? _documentServiceAccessor() : null; } }
        public WorkbenchSearchService WorkbenchSearchService { get { return _workbenchSearchServiceAccessor != null ? _workbenchSearchServiceAccessor() : null; } }
        public IBuildCommandResolver BuildCommandResolver { get { return _buildCommandResolverAccessor != null ? _buildCommandResolverAccessor() : null; } }
        public IBuildExecutor BuildExecutor { get { return _buildExecutorAccessor != null ? _buildExecutorAccessor() : null; } }
        public IRestartCoordinator RestartCoordinator { get { return _restartCoordinatorAccessor != null ? _restartCoordinatorAccessor() : null; } }
        public IReferenceCatalogService ReferenceCatalogService { get { return _referenceCatalogServiceAccessor != null ? _referenceCatalogServiceAccessor() : null; } }
        public ISourceLookupIndex SourceLookupIndex { get { return _sourceLookupIndexAccessor != null ? _sourceLookupIndexAccessor() : null; } }
        public ITextSearchService TextSearchService { get { return _textSearchServiceAccessor != null ? _textSearchServiceAccessor() : null; } }
        public IRuntimeToolBridge RuntimeToolBridge { get { return _runtimeToolBridgeAccessor != null ? _runtimeToolBridgeAccessor() : null; } }
        public ICortexSettingsStore SettingsStore { get { return _settingsStoreAccessor != null ? _settingsStoreAccessor() : null; } }
        public HarmonyPatchInspectionService HarmonyPatchInspectionService { get { return _harmonyPatchInspectionServiceAccessor != null ? _harmonyPatchInspectionServiceAccessor() : null; } }
        public HarmonyPatchResolutionService HarmonyPatchResolutionService { get { return _harmonyPatchResolutionServiceAccessor != null ? _harmonyPatchResolutionServiceAccessor() : null; } }
        public HarmonyPatchDisplayService HarmonyPatchDisplayService { get { return _harmonyPatchDisplayServiceAccessor != null ? _harmonyPatchDisplayServiceAccessor() : null; } }
        public HarmonyPatchGenerationService HarmonyPatchGenerationService { get { return _harmonyPatchGenerationServiceAccessor != null ? _harmonyPatchGenerationServiceAccessor() : null; } }
        public GeneratedTemplateNavigationService GeneratedTemplateNavigationService { get { return _generatedTemplateNavigationServiceAccessor != null ? _generatedTemplateNavigationServiceAccessor() : null; } }

        public ICommandRegistry CommandRegistry
        {
            get
            {
                var runtime = _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null;
                return runtime != null ? runtime.CommandRegistry : null;
            }
        }

        public IContributionRegistry ContributionRegistry
        {
            get
            {
                var runtime = _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null;
                return runtime != null ? runtime.ContributionRegistry : null;
            }
        }

        public ThemeState ThemeState
        {
            get
            {
                var runtime = _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null;
                return runtime != null ? runtime.ThemeState : null;
            }
        }
    }
}
