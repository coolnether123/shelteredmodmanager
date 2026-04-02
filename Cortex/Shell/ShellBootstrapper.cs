using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Services.Navigation;
using Cortex.Services.Navigation.Metadata;
using Cortex.Services.Editor.Context;
using Cortex.Services.Semantics.Context;
using UnityEngine;

namespace Cortex.Shell
{
    internal sealed class ShellBootstrapper
    {
        private readonly CortexShellState _state;
        private readonly CortexShellModuleContributionRegistry _moduleContributionRegistry;
        private readonly CortexShellModuleServices _moduleServices;
        private readonly CortexShellBuiltInModuleRegistrar _moduleRegistrar;
        private readonly IWorkbenchExtensionRegistry _extensionRegistry;
        private readonly IWorkbenchRuntimeAccess _runtimeAccess;
        private readonly ILanguageRuntimeControl _languageRuntimeControl;
        private readonly ILanguageRuntimeQuery _languageRuntimeQuery;
        private readonly ILanguageEditorOperations _languageEditorOperations;

        private ICortexSettingsStore _settingsStore;
        private IWorkbenchPersistenceService _workbenchPersistenceService;
        private IWorkbenchRuntimeFactory _workbenchRuntimeFactory;
        private ICortexPlatformModule _platformModule;
        private ICortexHostEnvironment _hostEnvironment;
        private IWorkbenchFrameContext _frameContext;
        private IPathInteractionService _pathInteractionService;
        private string _preferredLanguageProviderId;
        private WorkbenchPluginLoader _pluginLoader;

        public ShellBootstrapper(
            CortexShellState state,
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
            _moduleContributionRegistry = moduleContributionRegistry;
            _moduleServices = moduleServices;
            _moduleRegistrar = moduleRegistrar;
            _extensionRegistry = extensionRegistry;
            _runtimeAccess = runtimeAccess;
            _languageRuntimeControl = languageRuntimeControl;
            _languageRuntimeQuery = languageRuntimeQuery;
            _languageEditorOperations = languageEditorOperations;
        }

        public ICortexSettingsStore SettingsStore => _settingsStore;
        public IWorkbenchPersistenceService PersistenceService => _workbenchPersistenceService;
        public IWorkbenchRuntimeFactory RuntimeFactory => _workbenchRuntimeFactory;
        public ICortexPlatformModule PlatformModule => _platformModule;
        public ICortexHostEnvironment HostEnvironment => _hostEnvironment;
        public IWorkbenchFrameContext FrameContext => _frameContext;
        public IPathInteractionService PathInteractionService => _pathInteractionService;
        public WorkbenchPluginLoader PluginLoader => _pluginLoader;

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            var resolvedHostServices = hostServices ?? NullCortexHostServices.Instance;
            _workbenchRuntimeFactory = resolvedHostServices.WorkbenchRuntimeFactory;
            _platformModule = resolvedHostServices.PlatformModule ?? NullCortexPlatformModule.Instance;
            _hostEnvironment = resolvedHostServices.Environment ?? NullCortexHostServices.Instance.Environment;
            _frameContext = resolvedHostServices.FrameContext ?? NullWorkbenchFrameContext.Instance;
            _pathInteractionService = resolvedHostServices.PathInteractionService;
            _preferredLanguageProviderId = resolvedHostServices.PreferredLanguageProviderId ?? string.Empty;
        }

        public void InitializeSettings(out Rect windowRect, out Rect logWindowRect)
        {
            var hostEnvironment = _hostEnvironment ?? NullCortexHostServices.Instance.Environment;
            if (!string.IsNullOrEmpty(hostEnvironment.HostBinPath) && !Directory.Exists(hostEnvironment.HostBinPath))
            {
                Directory.CreateDirectory(hostEnvironment.HostBinPath);
            }

            _settingsStore = new JsonCortexSettingsStore(hostEnvironment.SettingsFilePath);
            _workbenchPersistenceService = new JsonWorkbenchPersistenceService(hostEnvironment.WorkbenchPersistenceFilePath);
            var settings = BuildEffectiveSettings(_settingsStore.Load(), hostEnvironment);
            _state.Settings = settings;

            windowRect = new Rect(settings.WindowX, settings.WindowY, settings.WindowWidth, settings.WindowHeight);
            logWindowRect = new Rect(settings.WindowX + 30f, settings.WindowY + 30f, Math.Max(760f, settings.WindowWidth - 120f), Math.Max(460f, settings.WindowHeight - 140f));
        }

        public IWorkbenchRuntime InitializeRuntime()
        {
            if (_workbenchRuntimeFactory == null)
            {
                MMLog.WriteWarning("[Cortex] Workbench runtime initialization skipped because no host runtime factory was configured.");
                return null;
            }

            var runtime = _workbenchRuntimeFactory.Create();
            if (runtime == null)
            {
                MMLog.WriteWarning("[Cortex] Workbench runtime initialization skipped because the host runtime factory returned null.");
                return null;
            }

            if (_pluginLoader == null)
            {
                _pluginLoader = new WorkbenchPluginLoader();
            }

            RegisterWorkbenchPlugins(runtime);

            runtime.LayoutState.PrimarySideWidth = _state.Settings != null ? _state.Settings.ProjectsPaneWidth : 360f;
            runtime.LayoutState.SecondarySideWidth = _state.Settings != null ? _state.Settings.EditorFilePaneWidth : 320f;
            runtime.LayoutState.PanelSize = _state.Settings != null ? _state.Settings.PanelPaneSize : 280f;
            runtime.ThemeState.ThemeId = _state.Settings != null && !string.IsNullOrEmpty(_state.Settings.ThemeId)
                ? _state.Settings.ThemeId
                : runtime.ThemeState.ThemeId;

            return runtime;
        }

        private void RegisterWorkbenchPlugins(IWorkbenchRuntime runtime)
        {
            var results = _pluginLoader.LoadPlugins(
                _state.Settings,
                _hostEnvironment,
                runtime.CommandRegistry,
                runtime.ContributionRegistry,
                _moduleContributionRegistry,
                _extensionRegistry,
                _runtimeAccess);

            for (var i = 0; i < results.Count; i++)
            {
                var result = results[i];
                if (result == null) continue;

                if (result.Loaded)
                {
                    _state.Diagnostics.Add("Loaded Cortex plugin " + result.DisplayName + " from " + result.AssemblyPath + ".");
                }
                else if (!string.IsNullOrEmpty(result.StatusMessage))
                {
                    _state.Diagnostics.Add("Cortex plugin skip: " + result.StatusMessage);
                }
            }
        }

        public ShellServiceMap InitializeServices(CortexSettings settings)
        {
            var hostEnvironment = _hostEnvironment ?? NullCortexHostServices.Instance.Environment;
            var platformModule = _platformModule ?? NullCortexPlatformModule.Instance;
            var languageRuntimeControl = _languageRuntimeControl ?? new NullLanguageRuntimeService();
            var languageRuntimeQuery = _languageRuntimeQuery ?? (languageRuntimeControl as ILanguageRuntimeQuery) ?? new NullLanguageRuntimeService();
            var languageEditorOperations = _languageEditorOperations ?? (languageRuntimeControl as ILanguageEditorOperations) ?? new NullLanguageRuntimeService();
            var resolvedLanguageProviderId = ResolveLanguageProviderId(settings);

            MMLog.WriteInfo("[Cortex] Initializing runtime. WorkspaceRoot=" +
                (settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty) +
                ", ReferenceAssemblies=" + (settings != null ? settings.ReferenceAssemblyRootPath ?? string.Empty : string.Empty) +
                ", LanguageProvider=" + resolvedLanguageProviderId + ".");

            var projectCatalogPath = string.IsNullOrEmpty(settings.ProjectCatalogPath)
                ? hostEnvironment.ProjectCatalogPath
                : settings.ProjectCatalogPath;

            var decompilerPath = platformModule.ResolveDecompilerPath(settings.DecompilerPathOverride);

            var projectCatalog = new ProjectCatalog(new JsonProjectConfigurationStore(projectCatalogPath));
            var sourceLookupIndex = new SourceLookupIndexService();
            var projectWorkspaceService = new ProjectWorkspaceService(sourceLookupIndex);
            var workspaceBrowserService = new WorkspaceBrowserService(sourceLookupIndex);
            var referenceCatalogService = new ReferenceCatalogService();
            var decompilerExplorerService = new DecompilerExplorerService(referenceCatalogService);
            var documentService = new FileDocumentService();
            var buildCommandResolver = new CsprojBuildCommandResolver();
            var buildExecutor = new ProcessBuildExecutor();
            var sourcePathResolver = new SourcePathResolver(sourceLookupIndex);
            var sourceReferenceService = new SourceReferenceService(new DecompilerCliClient(decompilerPath, settings.DecompilerCachePath, 15000));
            var metadataNavigationService = new AssemblyMetadataNavigationService();
            var navigationService = new CortexNavigationService(documentService, sourceReferenceService, platformModule.CreateRuntimeSourceNavigationService(sourcePathResolver), sourceLookupIndex);
            var featureRegistry = new ShellFeatureRegistry();
            platformModule.RegisterFeatures(featureRegistry);
            var editorContextService = new EditorContextService(
                new EditorService(),
                new EditorCommandContextFactory(),
                new EditorSymbolInteractionService());

            return new ShellServiceMap
            {
                ProjectCatalog = projectCatalog,
                LoadedModCatalog = platformModule.LoadedModCatalog,
                SourceLookupIndex = sourceLookupIndex,
                ProjectWorkspaceService = projectWorkspaceService,
                WorkspaceBrowserService = workspaceBrowserService,
                ReferenceCatalogService = referenceCatalogService,
                DecompilerExplorerService = decompilerExplorerService,
                DocumentService = documentService,
                BuildCommandResolver = buildCommandResolver,
                BuildExecutor = buildExecutor,
                SourcePathResolver = sourcePathResolver,
                SourceReferenceService = sourceReferenceService,
                RuntimeLogFeed = platformModule.RuntimeLogFeed,
                PathInteractionService = _pathInteractionService,
                RuntimeToolBridge = platformModule.RuntimeToolBridge,
                RestartCoordinator = platformModule.RestartCoordinator,
                OverlayInputCaptureService = platformModule.OverlayInputCaptureService,
                TextSearchService = new TextSearchService(),
                NavigationService = navigationService,
                FeatureRegistry = featureRegistry,
                EditorContextService = editorContextService,
                LanguageRuntimeControl = languageRuntimeControl,
                LanguageRuntimeQuery = languageRuntimeQuery,
                LanguageEditorOperations = languageEditorOperations
            };
        }

        public CortexSettings BuildEffectiveSettings(CortexSettings settings, ICortexHostEnvironment hostEnvironment)
        {
            var effective = settings ?? new CortexSettings();

            if (string.IsNullOrEmpty(effective.RuntimeContentRootPath)) effective.RuntimeContentRootPath = hostEnvironment.RuntimeContentRootPath;
            if (string.IsNullOrEmpty(effective.WorkspaceRootPath)) effective.WorkspaceRootPath = CortexHostPathSettings.GetEffectiveWorkspaceRoot(effective);
            if (string.IsNullOrEmpty(effective.ReferenceAssemblyRootPath)) effective.ReferenceAssemblyRootPath = hostEnvironment.ReferenceAssemblyRootPath;
            if (string.IsNullOrEmpty(effective.AdditionalSourceRoots)) effective.AdditionalSourceRoots = effective.WorkspaceRootPath;
            if (string.IsNullOrEmpty(effective.LogFilePath)) effective.LogFilePath = hostEnvironment.LogFilePath;
            if (string.IsNullOrEmpty(effective.ProjectCatalogPath)) effective.ProjectCatalogPath = hostEnvironment.ProjectCatalogPath;
            if (string.IsNullOrEmpty(effective.CortexPluginSearchRoots)) effective.CortexPluginSearchRoots = hostEnvironment.ConfiguredPluginSearchRoots;
            if (string.IsNullOrEmpty(effective.DecompilerCachePath)) effective.DecompilerCachePath = hostEnvironment.DecompilerCachePath;
            if (string.IsNullOrEmpty(effective.AdditionalDecompilerCacheRoots)) effective.AdditionalDecompilerCacheRoots = (_platformModule ?? NullCortexPlatformModule.Instance).AdditionalDecompilerCacheRoots;
            if (effective.LanguageProviderConfigurations == null) effective.LanguageProviderConfigurations = new LanguageProviderConfiguration[0];

            if (string.IsNullOrEmpty(effective.DefaultBuildConfiguration)) effective.DefaultBuildConfiguration = "Debug";
            if (effective.BuildTimeoutMs <= 0) effective.BuildTimeoutMs = 300000;

            if (string.IsNullOrEmpty(effective.CompletionAugmentationProviderId)) effective.CompletionAugmentationProviderId = CompletionAugmentationProviderIds.Tabby;
            if (effective.EnableTabbyCompletion && !effective.EnableCompletionAugmentation)
            {
                effective.EnableCompletionAugmentation = true;
                effective.CompletionAugmentationProviderId = CompletionAugmentationProviderIds.Tabby;
            }

            if (effective.CompletionAugmentationSnippetDocumentLimit < 0) effective.CompletionAugmentationSnippetDocumentLimit = 3;
            if (effective.CompletionAugmentationSnippetCharacterLimit <= 0) effective.CompletionAugmentationSnippetCharacterLimit = 800;
            if (effective.TabbyRequestTimeoutMs <= 0) effective.TabbyRequestTimeoutMs = 8000;

            if (string.IsNullOrEmpty(effective.OllamaServerUrl)) effective.OllamaServerUrl = "http://localhost:11434";
            if (string.IsNullOrEmpty(effective.OllamaSystemPrompt)) effective.OllamaSystemPrompt = CompletionAugmentationPromptDefaults.OllamaSystemPrompt;
            if (effective.OllamaRequestTimeoutMs <= 0) effective.OllamaRequestTimeoutMs = 8000;

            if (string.IsNullOrEmpty(effective.OpenRouterBaseUrl)) effective.OpenRouterBaseUrl = "https://openrouter.ai/api/v1";
            if (string.IsNullOrEmpty(effective.OpenRouterPromptPreamble)) effective.OpenRouterPromptPreamble = CompletionAugmentationPromptDefaults.OpenRouterPromptPreamble;
            if (string.IsNullOrEmpty(effective.OpenRouterAppTitle)) effective.OpenRouterAppTitle = "Cortex";
            if (effective.OpenRouterRequestTimeoutMs <= 0) effective.OpenRouterRequestTimeoutMs = 10000;

            if (string.IsNullOrEmpty(effective.DefaultOnboardingProfileId)) effective.DefaultOnboardingProfileId = "cortex.onboarding.profile.ide";
            if (string.IsNullOrEmpty(effective.DefaultOnboardingLayoutPresetId)) effective.DefaultOnboardingLayoutPresetId = "cortex.onboarding.layout.visual-studio";
            if (string.IsNullOrEmpty(effective.DefaultOnboardingThemeId)) effective.DefaultOnboardingThemeId = "cortex.vs-dark";

            if (effective.MaxRecentLogs <= 0) effective.MaxRecentLogs = 300;
            if (effective.LogsPaneWidth < 360f) effective.LogsPaneWidth = 520f;
            if (effective.ProjectsPaneWidth < 280f) effective.ProjectsPaneWidth = 360f;
            if (effective.EditorFilePaneWidth < 240f) effective.EditorFilePaneWidth = 420f;
            if (effective.PanelPaneSize < 150f) effective.PanelPaneSize = 280f;

            if (effective.WindowWidth < 980f || effective.WindowHeight < 620f)
            {
                var frameSnapshot = (_frameContext ?? NullWorkbenchFrameContext.Instance).Snapshot;
                effective.WindowWidth = Math.Max(980f, frameSnapshot.ViewportSize.Width * 0.82f);
                effective.WindowHeight = Math.Max(620f, frameSnapshot.ViewportSize.Height * 0.82f);
            }

            if (effective.WindowX < 0f) effective.WindowX = 40f;
            if (effective.WindowY < 0f) effective.WindowY = 40f;

            return effective;
        }

        public LanguageRuntimeConfiguration BuildLanguageRuntimeConfiguration(ICortexHostEnvironment hostEnvironment, CortexSettings settings)
        {
            var resolvedProviderId = ResolveLanguageProviderId(settings);
            return new LanguageRuntimeConfiguration
            {
                ProviderId = resolvedProviderId,
                HostBinPath = hostEnvironment != null ? hostEnvironment.HostBinPath : string.Empty,
                Settings = settings,
                ProviderConfiguration = LanguageProviderConfigurationHelper.FindConfiguration(settings, resolvedProviderId)
            };
        }

        public string ResolveLanguageProviderId(CortexSettings settings)
        {
            if (settings == null)
            {
                return LanguageRuntimeConstants.NoneProviderId;
            }

            if (!string.IsNullOrEmpty(settings.LanguageProviderId))
            {
                return settings.LanguageProviderId;
            }

            return !string.IsNullOrEmpty(_preferredLanguageProviderId)
                ? _preferredLanguageProviderId
                : LanguageRuntimeConstants.NoneProviderId;
        }
    }

    internal sealed class ShellServiceMap
    {
        public IProjectCatalog ProjectCatalog;
        public ILoadedModCatalog LoadedModCatalog;
        public ISourceLookupIndex SourceLookupIndex;
        public IProjectWorkspaceService ProjectWorkspaceService;
        public IWorkspaceBrowserService WorkspaceBrowserService;
        public IReferenceCatalogService ReferenceCatalogService;
        public IDecompilerExplorerService DecompilerExplorerService;
        public IDocumentService DocumentService;
        public IBuildCommandResolver BuildCommandResolver;
        public IBuildExecutor BuildExecutor;
        public ISourcePathResolver SourcePathResolver;
        public ISourceReferenceService SourceReferenceService;
        public IRuntimeLogFeed RuntimeLogFeed;
        public IPathInteractionService PathInteractionService;
        public IRuntimeToolBridge RuntimeToolBridge;
        public IRestartCoordinator RestartCoordinator;
        public IOverlayInputCaptureService OverlayInputCaptureService;
        public ITextSearchService TextSearchService;
        public ICortexNavigationService NavigationService;
        public ICortexPlatformFeatureRegistry FeatureRegistry;
        public IEditorContextService EditorContextService;
        public ILanguageRuntimeControl LanguageRuntimeControl;
        public ILanguageRuntimeQuery LanguageRuntimeQuery;
        public ILanguageEditorOperations LanguageEditorOperations;
    }
}
