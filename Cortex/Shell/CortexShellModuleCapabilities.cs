using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;
using Cortex.Services;

namespace Cortex.Shell
{
    internal interface ICortexShellStateCapability
    {
        CortexShellState State { get; }
    }

    internal interface ICortexShellNavigationCapability
    {
        CortexNavigationService NavigationService { get; }
    }

    internal interface ICortexShellSourceCapability
    {
        ISourcePathResolver SourcePathResolver { get; }
    }

    internal interface ICortexShellRuntimeLogCapability
    {
        IRuntimeLogFeed RuntimeLogFeed { get; }
    }

    internal interface ICortexShellProjectCapability
    {
        IProjectCatalog ProjectCatalog { get; }
        IProjectWorkspaceService ProjectWorkspaceService { get; }
        ILoadedModCatalog LoadedModCatalog { get; }
    }

    internal interface ICortexShellWorkspaceBrowserCapability
    {
        IWorkspaceBrowserService WorkspaceBrowserService { get; }
        IDecompilerExplorerService DecompilerExplorerService { get; }
    }

    internal interface ICortexShellDocumentCapability
    {
        IDocumentService DocumentService { get; }
    }

    internal interface ICortexShellWorkbenchCapability
    {
        ICommandRegistry CommandRegistry { get; }
        IContributionRegistry ContributionRegistry { get; }
        ThemeState ThemeState { get; }
    }

    internal interface ICortexShellSearchCapability
    {
        WorkbenchSearchService WorkbenchSearchService { get; }
    }

    internal interface ICortexShellBuildCapability
    {
        IBuildCommandResolver BuildCommandResolver { get; }
        IBuildExecutor BuildExecutor { get; }
        IRestartCoordinator RestartCoordinator { get; }
    }

    internal interface ICortexShellReferenceCapability
    {
        IReferenceCatalogService ReferenceCatalogService { get; }
    }

    internal interface ICortexShellRuntimeToolCapability
    {
        IRuntimeToolBridge RuntimeToolBridge { get; }
    }

    internal interface ICortexShellSettingsCapability
    {
        ICortexSettingsStore SettingsStore { get; }
    }

    internal sealed class CortexShellModuleServices :
        ICortexShellStateCapability,
        ICortexShellNavigationCapability,
        ICortexShellSourceCapability,
        ICortexShellRuntimeLogCapability,
        ICortexShellProjectCapability,
        ICortexShellWorkspaceBrowserCapability,
        ICortexShellDocumentCapability,
        ICortexShellWorkbenchCapability,
        ICortexShellSearchCapability,
        ICortexShellBuildCapability,
        ICortexShellReferenceCapability,
        ICortexShellRuntimeToolCapability,
        ICortexShellSettingsCapability
    {
        private readonly CortexShellState _state;
        private readonly Func<ICortexSettingsStore> _settingsStoreAccessor;
        private readonly Func<IProjectCatalog> _projectCatalogAccessor;
        private readonly Func<ILoadedModCatalog> _loadedModCatalogAccessor;
        private readonly Func<IProjectWorkspaceService> _projectWorkspaceServiceAccessor;
        private readonly Func<IWorkspaceBrowserService> _workspaceBrowserServiceAccessor;
        private readonly Func<IDecompilerExplorerService> _decompilerExplorerServiceAccessor;
        private readonly Func<IDocumentService> _documentServiceAccessor;
        private readonly Func<IBuildCommandResolver> _buildCommandResolverAccessor;
        private readonly Func<IBuildExecutor> _buildExecutorAccessor;
        private readonly Func<IReferenceCatalogService> _referenceCatalogServiceAccessor;
        private readonly Func<ISourcePathResolver> _sourcePathResolverAccessor;
        private readonly Func<IRuntimeLogFeed> _runtimeLogFeedAccessor;
        private readonly Func<IRuntimeToolBridge> _runtimeToolBridgeAccessor;
        private readonly Func<IRestartCoordinator> _restartCoordinatorAccessor;
        private readonly Func<CortexNavigationService> _navigationServiceAccessor;
        private readonly Func<UnityWorkbenchRuntime> _workbenchRuntimeAccessor;
        private readonly Func<WorkbenchSearchService> _workbenchSearchServiceAccessor;

        public CortexShellModuleServices(
            CortexShellState state,
            Func<ICortexSettingsStore> settingsStoreAccessor,
            Func<IProjectCatalog> projectCatalogAccessor,
            Func<ILoadedModCatalog> loadedModCatalogAccessor,
            Func<IProjectWorkspaceService> projectWorkspaceServiceAccessor,
            Func<IWorkspaceBrowserService> workspaceBrowserServiceAccessor,
            Func<IDecompilerExplorerService> decompilerExplorerServiceAccessor,
            Func<IDocumentService> documentServiceAccessor,
            Func<IBuildCommandResolver> buildCommandResolverAccessor,
            Func<IBuildExecutor> buildExecutorAccessor,
            Func<IReferenceCatalogService> referenceCatalogServiceAccessor,
            Func<ISourcePathResolver> sourcePathResolverAccessor,
            Func<IRuntimeLogFeed> runtimeLogFeedAccessor,
            Func<IRuntimeToolBridge> runtimeToolBridgeAccessor,
            Func<IRestartCoordinator> restartCoordinatorAccessor,
            Func<CortexNavigationService> navigationServiceAccessor,
            Func<UnityWorkbenchRuntime> workbenchRuntimeAccessor,
            Func<WorkbenchSearchService> workbenchSearchServiceAccessor)
        {
            _state = state;
            _settingsStoreAccessor = settingsStoreAccessor;
            _projectCatalogAccessor = projectCatalogAccessor;
            _loadedModCatalogAccessor = loadedModCatalogAccessor;
            _projectWorkspaceServiceAccessor = projectWorkspaceServiceAccessor;
            _workspaceBrowserServiceAccessor = workspaceBrowserServiceAccessor;
            _decompilerExplorerServiceAccessor = decompilerExplorerServiceAccessor;
            _documentServiceAccessor = documentServiceAccessor;
            _buildCommandResolverAccessor = buildCommandResolverAccessor;
            _buildExecutorAccessor = buildExecutorAccessor;
            _referenceCatalogServiceAccessor = referenceCatalogServiceAccessor;
            _sourcePathResolverAccessor = sourcePathResolverAccessor;
            _runtimeLogFeedAccessor = runtimeLogFeedAccessor;
            _runtimeToolBridgeAccessor = runtimeToolBridgeAccessor;
            _restartCoordinatorAccessor = restartCoordinatorAccessor;
            _navigationServiceAccessor = navigationServiceAccessor;
            _workbenchRuntimeAccessor = workbenchRuntimeAccessor;
            _workbenchSearchServiceAccessor = workbenchSearchServiceAccessor;
        }

        public CortexShellState State { get { return _state; } }
        public CortexNavigationService NavigationService { get { return _navigationServiceAccessor != null ? _navigationServiceAccessor() : null; } }
        public ISourcePathResolver SourcePathResolver { get { return _sourcePathResolverAccessor != null ? _sourcePathResolverAccessor() : null; } }
        public IRuntimeLogFeed RuntimeLogFeed { get { return _runtimeLogFeedAccessor != null ? _runtimeLogFeedAccessor() : null; } }
        public IProjectCatalog ProjectCatalog { get { return _projectCatalogAccessor != null ? _projectCatalogAccessor() : null; } }
        public IProjectWorkspaceService ProjectWorkspaceService { get { return _projectWorkspaceServiceAccessor != null ? _projectWorkspaceServiceAccessor() : null; } }
        public ILoadedModCatalog LoadedModCatalog { get { return _loadedModCatalogAccessor != null ? _loadedModCatalogAccessor() : null; } }
        public IWorkspaceBrowserService WorkspaceBrowserService { get { return _workspaceBrowserServiceAccessor != null ? _workspaceBrowserServiceAccessor() : null; } }
        public IDecompilerExplorerService DecompilerExplorerService { get { return _decompilerExplorerServiceAccessor != null ? _decompilerExplorerServiceAccessor() : null; } }
        public IDocumentService DocumentService { get { return _documentServiceAccessor != null ? _documentServiceAccessor() : null; } }
        public WorkbenchSearchService WorkbenchSearchService { get { return _workbenchSearchServiceAccessor != null ? _workbenchSearchServiceAccessor() : null; } }
        public IBuildCommandResolver BuildCommandResolver { get { return _buildCommandResolverAccessor != null ? _buildCommandResolverAccessor() : null; } }
        public IBuildExecutor BuildExecutor { get { return _buildExecutorAccessor != null ? _buildExecutorAccessor() : null; } }
        public IRestartCoordinator RestartCoordinator { get { return _restartCoordinatorAccessor != null ? _restartCoordinatorAccessor() : null; } }
        public IReferenceCatalogService ReferenceCatalogService { get { return _referenceCatalogServiceAccessor != null ? _referenceCatalogServiceAccessor() : null; } }
        public IRuntimeToolBridge RuntimeToolBridge { get { return _runtimeToolBridgeAccessor != null ? _runtimeToolBridgeAccessor() : null; } }
        public ICortexSettingsStore SettingsStore { get { return _settingsStoreAccessor != null ? _settingsStoreAccessor() : null; } }

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
