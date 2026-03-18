using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Host.Unity.Runtime;
using Cortex.Services;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleRenderContext
    {
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
        private readonly Func<string, string> _buildActivationBlockedMessage;

        public CortexShellModuleRenderContext(
            CortexShellModuleActivationContext activationContext,
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
            Func<WorkbenchSearchService> workbenchSearchServiceAccessor,
            Func<string, string> buildActivationBlockedMessage)
        {
            ActivationContext = activationContext;
            State = state;
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
            _buildActivationBlockedMessage = buildActivationBlockedMessage;
            Capabilities = BuildCapabilities();
        }

        public CortexShellModuleActivationContext ActivationContext { get; private set; }

        public CortexShellModuleCapabilityCollection Capabilities { get; private set; }

        public CortexShellState State { get; private set; }

        public ICortexSettingsStore SettingsStore { get { return _settingsStoreAccessor != null ? _settingsStoreAccessor() : null; } }
        public IProjectCatalog ProjectCatalog { get { return _projectCatalogAccessor != null ? _projectCatalogAccessor() : null; } }
        public ILoadedModCatalog LoadedModCatalog { get { return _loadedModCatalogAccessor != null ? _loadedModCatalogAccessor() : null; } }
        public IProjectWorkspaceService ProjectWorkspaceService { get { return _projectWorkspaceServiceAccessor != null ? _projectWorkspaceServiceAccessor() : null; } }
        public IWorkspaceBrowserService WorkspaceBrowserService { get { return _workspaceBrowserServiceAccessor != null ? _workspaceBrowserServiceAccessor() : null; } }
        public IDecompilerExplorerService DecompilerExplorerService { get { return _decompilerExplorerServiceAccessor != null ? _decompilerExplorerServiceAccessor() : null; } }
        public IDocumentService DocumentService { get { return _documentServiceAccessor != null ? _documentServiceAccessor() : null; } }
        public IBuildCommandResolver BuildCommandResolver { get { return _buildCommandResolverAccessor != null ? _buildCommandResolverAccessor() : null; } }
        public IBuildExecutor BuildExecutor { get { return _buildExecutorAccessor != null ? _buildExecutorAccessor() : null; } }
        public IReferenceCatalogService ReferenceCatalogService { get { return _referenceCatalogServiceAccessor != null ? _referenceCatalogServiceAccessor() : null; } }
        public ISourcePathResolver SourcePathResolver { get { return _sourcePathResolverAccessor != null ? _sourcePathResolverAccessor() : null; } }
        public IRuntimeLogFeed RuntimeLogFeed { get { return _runtimeLogFeedAccessor != null ? _runtimeLogFeedAccessor() : null; } }
        public IRuntimeToolBridge RuntimeToolBridge { get { return _runtimeToolBridgeAccessor != null ? _runtimeToolBridgeAccessor() : null; } }
        public IRestartCoordinator RestartCoordinator { get { return _restartCoordinatorAccessor != null ? _restartCoordinatorAccessor() : null; } }
        public CortexNavigationService NavigationService { get { return _navigationServiceAccessor != null ? _navigationServiceAccessor() : null; } }
        public UnityWorkbenchRuntime WorkbenchRuntime { get { return _workbenchRuntimeAccessor != null ? _workbenchRuntimeAccessor() : null; } }
        public WorkbenchSearchService WorkbenchSearchService { get { return _workbenchSearchServiceAccessor != null ? _workbenchSearchServiceAccessor() : null; } }

        public bool CanActivateContainer(string containerId)
        {
            return ActivationContext != null && ActivationContext.CanActivateContainer(containerId);
        }

        public string BuildActivationBlockedMessage(string containerId)
        {
            return _buildActivationBlockedMessage != null ? _buildActivationBlockedMessage(containerId) : string.Empty;
        }

        private CortexShellModuleCapabilityCollection BuildCapabilities()
        {
            var capabilities = new CortexShellModuleCapabilityCollection();
            capabilities.Add<ICortexShellStateCapability>(new StateCapability(this));
            capabilities.Add<ICortexShellNavigationCapability>(new NavigationCapability(this));
            capabilities.Add<ICortexShellSourceCapability>(new SourceCapability(this));
            capabilities.Add<ICortexShellRuntimeLogCapability>(new RuntimeLogCapability(this));
            capabilities.Add<ICortexShellProjectCapability>(new ProjectCapability(this));
            capabilities.Add<ICortexShellWorkspaceBrowserCapability>(new WorkspaceBrowserCapability(this));
            capabilities.Add<ICortexShellDocumentCapability>(new DocumentCapability(this));
            capabilities.Add<ICortexShellWorkbenchCapability>(new WorkbenchCapability(this));
            capabilities.Add<ICortexShellSearchCapability>(new SearchCapability(this));
            capabilities.Add<ICortexShellBuildCapability>(new BuildCapability(this));
            capabilities.Add<ICortexShellReferenceCapability>(new ReferenceCapability(this));
            capabilities.Add<ICortexShellRuntimeToolCapability>(new RuntimeToolCapability(this));
            capabilities.Add<ICortexShellSettingsCapability>(new SettingsCapability(this));
            return capabilities;
        }

        private sealed class StateCapability : ICortexShellStateCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public StateCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public CortexShellState State
            {
                get { return _context != null ? _context.State : null; }
            }
        }

        private sealed class NavigationCapability : ICortexShellNavigationCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public NavigationCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public CortexNavigationService NavigationService
            {
                get { return _context != null ? _context.NavigationService : null; }
            }
        }

        private sealed class SourceCapability : ICortexShellSourceCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public SourceCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public ISourcePathResolver SourcePathResolver
            {
                get { return _context != null ? _context.SourcePathResolver : null; }
            }
        }

        private sealed class RuntimeLogCapability : ICortexShellRuntimeLogCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public RuntimeLogCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IRuntimeLogFeed RuntimeLogFeed
            {
                get { return _context != null ? _context.RuntimeLogFeed : null; }
            }
        }

        private sealed class ProjectCapability : ICortexShellProjectCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public ProjectCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IProjectCatalog ProjectCatalog
            {
                get { return _context != null ? _context.ProjectCatalog : null; }
            }

            public IProjectWorkspaceService ProjectWorkspaceService
            {
                get { return _context != null ? _context.ProjectWorkspaceService : null; }
            }

            public ILoadedModCatalog LoadedModCatalog
            {
                get { return _context != null ? _context.LoadedModCatalog : null; }
            }
        }

        private sealed class WorkspaceBrowserCapability : ICortexShellWorkspaceBrowserCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public WorkspaceBrowserCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IWorkspaceBrowserService WorkspaceBrowserService
            {
                get { return _context != null ? _context.WorkspaceBrowserService : null; }
            }

            public IDecompilerExplorerService DecompilerExplorerService
            {
                get { return _context != null ? _context.DecompilerExplorerService : null; }
            }
        }

        private sealed class DocumentCapability : ICortexShellDocumentCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public DocumentCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IDocumentService DocumentService
            {
                get { return _context != null ? _context.DocumentService : null; }
            }
        }

        private sealed class WorkbenchCapability : ICortexShellWorkbenchCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public WorkbenchCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public UnityWorkbenchRuntime WorkbenchRuntime
            {
                get { return _context != null ? _context.WorkbenchRuntime : null; }
            }
        }

        private sealed class SearchCapability : ICortexShellSearchCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public SearchCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public WorkbenchSearchService WorkbenchSearchService
            {
                get { return _context != null ? _context.WorkbenchSearchService : null; }
            }
        }

        private sealed class BuildCapability : ICortexShellBuildCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public BuildCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IBuildCommandResolver BuildCommandResolver
            {
                get { return _context != null ? _context.BuildCommandResolver : null; }
            }

            public IBuildExecutor BuildExecutor
            {
                get { return _context != null ? _context.BuildExecutor : null; }
            }

            public IRestartCoordinator RestartCoordinator
            {
                get { return _context != null ? _context.RestartCoordinator : null; }
            }
        }

        private sealed class ReferenceCapability : ICortexShellReferenceCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public ReferenceCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IReferenceCatalogService ReferenceCatalogService
            {
                get { return _context != null ? _context.ReferenceCatalogService : null; }
            }
        }

        private sealed class RuntimeToolCapability : ICortexShellRuntimeToolCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public RuntimeToolCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public IRuntimeToolBridge RuntimeToolBridge
            {
                get { return _context != null ? _context.RuntimeToolBridge : null; }
            }
        }

        private sealed class SettingsCapability : ICortexShellSettingsCapability
        {
            private readonly CortexShellModuleRenderContext _context;

            public SettingsCapability(CortexShellModuleRenderContext context)
            {
                _context = context;
            }

            public ICortexSettingsStore SettingsStore
            {
                get { return _context != null ? _context.SettingsStore : null; }
            }
        }
    }
}
