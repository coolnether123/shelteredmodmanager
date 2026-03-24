using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Shell;
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
        private readonly ShellServiceMap _services;
        private readonly Func<ICortexSettingsStore> _settingsStoreProvider;
        private readonly Func<IWorkbenchRuntime> _runtimeProvider;
        private readonly WorkbenchSearchService _workbenchSearchService;

        public CortexShellModuleServices(
            CortexShellState state,
            ShellServiceMap services,
            Func<ICortexSettingsStore> settingsStoreProvider,
            Func<IWorkbenchRuntime> runtimeProvider,
            WorkbenchSearchService workbenchSearchService)
        {
            _state = state;
            _services = services;
            _settingsStoreProvider = settingsStoreProvider;
            _runtimeProvider = runtimeProvider;
            _workbenchSearchService = workbenchSearchService;
        }

        public CortexShellState State => _state;
        public CortexNavigationService NavigationService => _services.NavigationService;
        public ISourcePathResolver SourcePathResolver => _services.SourcePathResolver;
        public IRuntimeLogFeed RuntimeLogFeed => _services.RuntimeLogFeed;
        public IProjectCatalog ProjectCatalog => _services.ProjectCatalog;
        public IProjectWorkspaceService ProjectWorkspaceService => _services.ProjectWorkspaceService;
        public ILoadedModCatalog LoadedModCatalog => _services.LoadedModCatalog;
        public IPathInteractionService PathInteractionService => _services.PathInteractionService;
        public IWorkspaceBrowserService WorkspaceBrowserService => _services.WorkspaceBrowserService;
        public IDecompilerExplorerService DecompilerExplorerService => _services.DecompilerExplorerService;
        public IDocumentService DocumentService => _services.DocumentService;
        public WorkbenchSearchService WorkbenchSearchService => _workbenchSearchService;
        public IBuildCommandResolver BuildCommandResolver => _services.BuildCommandResolver;
        public IBuildExecutor BuildExecutor => _services.BuildExecutor;
        public IRestartCoordinator RestartCoordinator => _services.RestartCoordinator;
        public IReferenceCatalogService ReferenceCatalogService => _services.ReferenceCatalogService;
        public ISourceLookupIndex SourceLookupIndex => _services.SourceLookupIndex;
        public ITextSearchService TextSearchService => _services.TextSearchService;
        public IRuntimeToolBridge RuntimeToolBridge => _services.RuntimeToolBridge;
        public ICortexSettingsStore SettingsStore => _settingsStoreProvider?.Invoke();
        public HarmonyPatchInspectionService HarmonyPatchInspectionService => _services.HarmonyPatchInspectionService;
        public HarmonyPatchResolutionService HarmonyPatchResolutionService => _services.HarmonyPatchResolutionService;
        public HarmonyPatchDisplayService HarmonyPatchDisplayService => _services.HarmonyPatchDisplayService;
        public HarmonyPatchGenerationService HarmonyPatchGenerationService => _services.HarmonyPatchGenerationService;
        public GeneratedTemplateNavigationService GeneratedTemplateNavigationService => _services.GeneratedTemplateNavigationService;

        public ICommandRegistry CommandRegistry
        {
            get
            {
                var runtime = _runtimeProvider?.Invoke();
                return runtime?.CommandRegistry;
            }
        }

        public IContributionRegistry ContributionRegistry
        {
            get
            {
                var runtime = _runtimeProvider?.Invoke();
                return runtime?.ContributionRegistry;
            }
        }

        public ThemeState ThemeState
        {
            get
            {
                var runtime = _runtimeProvider?.Invoke();
                return runtime?.ThemeState;
            }
        }
    }
}
