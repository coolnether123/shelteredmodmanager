using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Services.Editor.Context;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using Cortex.Rendering.Models;
using Cortex.Runtime;

namespace Cortex.Shell
{
    internal sealed class ShellBootstrapper
    {
        private readonly CortexShellState _state;
        private readonly CortexShellViewState _viewState;
        private readonly CortexShellModuleContributionRegistry _moduleContributionRegistry;
        private readonly CortexRuntimeCompositionService _runtimeComposition;

        private ICortexSettingsStore _settingsStore;
        private IWorkbenchPersistenceService _workbenchPersistenceService;

        public ShellBootstrapper(
            CortexShellState state,
            CortexShellViewState viewState,
            CortexShellModuleContributionRegistry moduleContributionRegistry,
            CortexShellModuleServices moduleServices,
            CortexShellBuiltInModuleRegistrar moduleRegistrar,
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtimeAccess,
            ILanguageRuntimeControl languageRuntimeControl,
            ILanguageRuntimeQuery languageRuntimeQuery,
            ILanguageEditorOperations languageEditorOperations)
        {
            _state = state;
            _viewState = viewState ?? new CortexShellViewState();
            _moduleContributionRegistry = moduleContributionRegistry;
            _runtimeComposition = new CortexRuntimeCompositionService(
                extensionRegistry,
                runtimeAccess,
                languageRuntimeControl,
                languageRuntimeQuery,
                languageEditorOperations);
        }

        public ICortexSettingsStore SettingsStore => _settingsStore;
        public IWorkbenchPersistenceService PersistenceService => _workbenchPersistenceService;
        public IWorkbenchRuntimeFactory RuntimeFactory => _runtimeComposition.RuntimeFactory;
        public ICortexPlatformModule PlatformModule => _runtimeComposition.PlatformModule;
        public ICortexHostEnvironment HostEnvironment => _runtimeComposition.HostEnvironment;
        public IWorkbenchFrameContext FrameContext => _runtimeComposition.FrameContext;
        public IPathInteractionService PathInteractionService => _runtimeComposition.PathInteractionService;

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            _runtimeComposition.ConfigureHostServices(hostServices);
        }

        public void InitializeSettings()
        {
            var initialization = _runtimeComposition.InitializeSettings();
            _settingsStore = initialization.SettingsStore;
            _workbenchPersistenceService = initialization.PersistenceService;
            _state.Settings = initialization.Settings;
            ApplyShellWindowSettings(initialization.Settings);
        }

        public IWorkbenchRuntime InitializeRuntime()
        {
            return _runtimeComposition.InitializeWorkbenchRuntime(
                _state.Settings,
                _moduleContributionRegistry,
                HandlePluginLoadResult);
        }

        public ShellServiceMap InitializeServices(CortexSettings settings)
        {
            return _runtimeComposition.InitializeServices(settings);
        }

        public CortexSettings BuildEffectiveSettings(CortexSettings settings, ICortexHostEnvironment hostEnvironment)
        {
            return _runtimeComposition.BuildEffectiveSettings(settings, hostEnvironment);
        }

        public LanguageRuntimeConfiguration BuildLanguageRuntimeConfiguration(ICortexHostEnvironment hostEnvironment, CortexSettings settings)
        {
            return _runtimeComposition.BuildLanguageRuntimeConfiguration(hostEnvironment, settings);
        }

        public string ResolveLanguageProviderId(CortexSettings settings)
        {
            return _runtimeComposition.ResolveLanguageProviderId(settings);
        }

        public void ApplyShellWindowSettings(CortexSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            InitializeWindowState(
                _viewState.MainWindow,
                new RenderRect(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight));
            InitializeWindowState(
                _viewState.LogsWindow,
                new RenderRect(
                    settings.WindowX + 30f,
                    settings.WindowY + 30f,
                    Math.Max(760f, settings.WindowWidth - 120f),
                    Math.Max(460f, settings.WindowHeight - 140f)));
        }

        private void HandlePluginLoadResult(WorkbenchPluginLoadResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.Loaded)
            {
                _state.Diagnostics.Add("Loaded Cortex plugin " + result.DisplayName + " from " + result.AssemblyPath + ".");
            }
            else if (!string.IsNullOrEmpty(result.StatusMessage))
            {
                _state.Diagnostics.Add("Cortex plugin skip: " + result.StatusMessage);
            }
        }

        private static void InitializeWindowState(CortexShellWindowViewState viewState, RenderRect expandedRect)
        {
            if (viewState == null)
            {
                return;
            }

            viewState.CurrentRect = expandedRect;
            viewState.ExpandedRect = expandedRect;
            viewState.CollapsedRect = CortexShellWindowViewState.BuildCollapsedRect(expandedRect, viewState.CollapsedWidth, viewState.CollapsedHeight);
            viewState.IsCollapsed = false;
        }
    }

    internal sealed class ShellServiceMap
    {
        public static readonly ShellServiceMap Empty = new ShellServiceMap();

        public ShellServiceMap(
            IProjectCatalog projectCatalog = null,
            ILoadedModCatalog loadedModCatalog = null,
            ISourceLookupIndex sourceLookupIndex = null,
            IProjectWorkspaceService projectWorkspaceService = null,
            IWorkspaceBrowserService workspaceBrowserService = null,
            IReferenceCatalogService referenceCatalogService = null,
            IDecompilerExplorerService decompilerExplorerService = null,
            IDocumentService documentService = null,
            IBuildCommandResolver buildCommandResolver = null,
            IBuildExecutor buildExecutor = null,
            ISourcePathResolver sourcePathResolver = null,
            ISourceReferenceService sourceReferenceService = null,
            IRuntimeLogFeed runtimeLogFeed = null,
            IPathInteractionService pathInteractionService = null,
            IRuntimeToolBridge runtimeToolBridge = null,
            IRestartCoordinator restartCoordinator = null,
            IOverlayInputCaptureService overlayInputCaptureService = null,
            ITextSearchService textSearchService = null,
            ICortexNavigationService navigationService = null,
            ICortexPlatformFeatureRegistry featureRegistry = null,
            IEditorContextService editorContextService = null,
            ILanguageRuntimeControl languageRuntimeControl = null,
            ILanguageRuntimeQuery languageRuntimeQuery = null,
            ILanguageEditorOperations languageEditorOperations = null)
        {
            ProjectCatalog = projectCatalog;
            LoadedModCatalog = loadedModCatalog;
            SourceLookupIndex = sourceLookupIndex;
            ProjectWorkspaceService = projectWorkspaceService;
            WorkspaceBrowserService = workspaceBrowserService;
            ReferenceCatalogService = referenceCatalogService;
            DecompilerExplorerService = decompilerExplorerService;
            DocumentService = documentService;
            BuildCommandResolver = buildCommandResolver;
            BuildExecutor = buildExecutor;
            SourcePathResolver = sourcePathResolver;
            SourceReferenceService = sourceReferenceService;
            RuntimeLogFeed = runtimeLogFeed;
            PathInteractionService = pathInteractionService;
            RuntimeToolBridge = runtimeToolBridge;
            RestartCoordinator = restartCoordinator;
            OverlayInputCaptureService = overlayInputCaptureService;
            TextSearchService = textSearchService;
            NavigationService = navigationService;
            FeatureRegistry = featureRegistry;
            EditorContextService = editorContextService;
            LanguageRuntimeControl = languageRuntimeControl;
            LanguageRuntimeQuery = languageRuntimeQuery;
            LanguageEditorOperations = languageEditorOperations;
        }

        public IProjectCatalog ProjectCatalog { get; }
        public ILoadedModCatalog LoadedModCatalog { get; }
        public ISourceLookupIndex SourceLookupIndex { get; }
        public IProjectWorkspaceService ProjectWorkspaceService { get; }
        public IWorkspaceBrowserService WorkspaceBrowserService { get; }
        public IReferenceCatalogService ReferenceCatalogService { get; }
        public IDecompilerExplorerService DecompilerExplorerService { get; }
        public IDocumentService DocumentService { get; }
        public IBuildCommandResolver BuildCommandResolver { get; }
        public IBuildExecutor BuildExecutor { get; }
        public ISourcePathResolver SourcePathResolver { get; }
        public ISourceReferenceService SourceReferenceService { get; }
        public IRuntimeLogFeed RuntimeLogFeed { get; }
        public IPathInteractionService PathInteractionService { get; }
        public IRuntimeToolBridge RuntimeToolBridge { get; }
        public IRestartCoordinator RestartCoordinator { get; }
        public IOverlayInputCaptureService OverlayInputCaptureService { get; }
        public ITextSearchService TextSearchService { get; }
        public ICortexNavigationService NavigationService { get; }
        public ICortexPlatformFeatureRegistry FeatureRegistry { get; }
        public IEditorContextService EditorContextService { get; }
        public ILanguageRuntimeControl LanguageRuntimeControl { get; }
        public ILanguageRuntimeQuery LanguageRuntimeQuery { get; }
        public ILanguageEditorOperations LanguageEditorOperations { get; }
    }
}
