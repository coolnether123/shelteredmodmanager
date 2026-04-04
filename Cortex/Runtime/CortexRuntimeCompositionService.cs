using System;
using System.IO;
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
using Cortex.Shell;

namespace Cortex.Runtime
{
    internal sealed class CortexRuntimeSettingsInitialization
    {
        public readonly ICortexSettingsStore SettingsStore;
        public readonly IWorkbenchPersistenceService PersistenceService;
        public readonly CortexSettings Settings;

        public CortexRuntimeSettingsInitialization(
            ICortexSettingsStore settingsStore,
            IWorkbenchPersistenceService persistenceService,
            CortexSettings settings)
        {
            SettingsStore = settingsStore;
            PersistenceService = persistenceService;
            Settings = settings;
        }
    }

    internal sealed class CortexRuntimeCompositionService
    {
        private readonly IWorkbenchExtensionRegistry _extensionRegistry;
        private readonly IWorkbenchRuntimeAccess _runtimeAccess;
        private readonly ILanguageRuntimeControl _languageRuntimeControl;
        private readonly ILanguageRuntimeQuery _languageRuntimeQuery;
        private readonly ILanguageEditorOperations _languageEditorOperations;
        private ICortexHostServices _hostServices;

        public CortexRuntimeCompositionService(
            IWorkbenchExtensionRegistry extensionRegistry,
            IWorkbenchRuntimeAccess runtimeAccess,
            ILanguageRuntimeControl languageRuntimeControl,
            ILanguageRuntimeQuery languageRuntimeQuery,
            ILanguageEditorOperations languageEditorOperations)
        {
            _extensionRegistry = extensionRegistry;
            _runtimeAccess = runtimeAccess;
            _languageRuntimeControl = languageRuntimeControl;
            _languageRuntimeQuery = languageRuntimeQuery;
            _languageEditorOperations = languageEditorOperations;
            _hostServices = NullCortexHostServices.Instance;
        }

        public ICortexHostEnvironment HostEnvironment
        {
            get
            {
                return _hostServices != null && _hostServices.Environment != null
                    ? _hostServices.Environment
                    : NullCortexHostServices.Instance.Environment;
            }
        }

        public IWorkbenchRuntimeFactory RuntimeFactory
        {
            get
            {
                return _hostServices != null ? _hostServices.WorkbenchRuntimeFactory : null;
            }
        }

        public ICortexPlatformModule PlatformModule
        {
            get
            {
                return _hostServices != null && _hostServices.PlatformModule != null
                    ? _hostServices.PlatformModule
                    : NullCortexPlatformModule.Instance;
            }
        }

        public IWorkbenchFrameContext FrameContext
        {
            get
            {
                return _hostServices != null && _hostServices.FrameContext != null
                    ? _hostServices.FrameContext
                    : NullWorkbenchFrameContext.Instance;
            }
        }

        public IPathInteractionService PathInteractionService
        {
            get
            {
                return _hostServices != null ? _hostServices.PathInteractionService : null;
            }
        }

        public void ConfigureHostServices(ICortexHostServices hostServices)
        {
            _hostServices = hostServices ?? NullCortexHostServices.Instance;
        }

        public CortexRuntimeSettingsInitialization InitializeSettings()
        {
            var hostEnvironment = HostEnvironment;
            if (!string.IsNullOrEmpty(hostEnvironment.HostBinPath) && !Directory.Exists(hostEnvironment.HostBinPath))
            {
                Directory.CreateDirectory(hostEnvironment.HostBinPath);
            }

            var settingsStore = new JsonCortexSettingsStore(hostEnvironment.SettingsFilePath);
            var persistenceService = new JsonWorkbenchPersistenceService(hostEnvironment.WorkbenchPersistenceFilePath);
            var settings = BuildEffectiveSettings(settingsStore.Load(), hostEnvironment);
            return new CortexRuntimeSettingsInitialization(settingsStore, persistenceService, settings);
        }

        public IWorkbenchRuntime InitializeWorkbenchRuntime(
            CortexSettings settings,
            IWorkbenchModuleRegistry moduleRegistry,
            Action<WorkbenchPluginLoadResult> pluginResultHandler)
        {
            var runtimeFactory = RuntimeFactory;
            if (runtimeFactory == null)
            {
                MMLog.WriteWarning("[Cortex] Workbench runtime initialization skipped because no host runtime factory was configured.");
                return null;
            }

            var runtime = runtimeFactory.Create();
            if (runtime == null)
            {
                MMLog.WriteWarning("[Cortex] Workbench runtime initialization skipped because the host runtime factory returned null.");
                return null;
            }

            RegisterWorkbenchPlugins(runtime, settings, moduleRegistry, pluginResultHandler);

            runtime.LayoutState.PrimarySideWidth = settings != null ? settings.ProjectsPaneWidth : 360f;
            runtime.LayoutState.SecondarySideWidth = settings != null ? settings.EditorFilePaneWidth : 320f;
            runtime.LayoutState.PanelSize = settings != null ? settings.PanelPaneSize : 280f;
            runtime.ThemeState.ThemeId = settings != null && !string.IsNullOrEmpty(settings.ThemeId)
                ? settings.ThemeId
                : runtime.ThemeState.ThemeId;

            return runtime;
        }

        public ShellServiceMap InitializeServices(CortexSettings settings)
        {
            var hostEnvironment = HostEnvironment;
            var platformModule = PlatformModule;
            var languageRuntimeControl = _languageRuntimeControl ?? new NullLanguageRuntimeService();
            var languageRuntimeQuery = _languageRuntimeQuery ?? (languageRuntimeControl as ILanguageRuntimeQuery) ?? new NullLanguageRuntimeService();
            var languageEditorOperations = _languageEditorOperations ?? (languageRuntimeControl as ILanguageEditorOperations) ?? new NullLanguageRuntimeService();
            var resolvedLanguageProviderId = ResolveLanguageProviderId(settings);

            MMLog.WriteInfo("[Cortex] Initializing runtime. WorkspaceRoot=" +
                (settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty) +
                ", ReferenceAssemblies=" + (settings != null ? settings.ReferenceAssemblyRootPath ?? string.Empty : string.Empty) +
                ", LanguageProvider=" + resolvedLanguageProviderId + ".");

            var projectCatalogPath = string.IsNullOrEmpty(settings != null ? settings.ProjectCatalogPath : string.Empty)
                ? hostEnvironment.ProjectCatalogPath
                : settings.ProjectCatalogPath;

            var decompilerPath = platformModule.ResolveDecompilerPath(settings != null ? settings.DecompilerPathOverride : string.Empty);

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
            var navigationService = new CortexNavigationService(documentService, sourceReferenceService, platformModule.CreateRuntimeSourceNavigationService(sourcePathResolver), sourceLookupIndex);
            var featureRegistry = new ShellFeatureRegistry();
            platformModule.RegisterFeatures(featureRegistry);
            var editorContextService = new EditorContextService(
                new EditorService(),
                new EditorCommandContextFactory(),
                new EditorSymbolInteractionService());

            return new ShellServiceMap(
                projectCatalog,
                platformModule.LoadedModCatalog,
                sourceLookupIndex,
                projectWorkspaceService,
                workspaceBrowserService,
                referenceCatalogService,
                decompilerExplorerService,
                documentService,
                buildCommandResolver,
                buildExecutor,
                sourcePathResolver,
                sourceReferenceService,
                platformModule.RuntimeLogFeed,
                PathInteractionService,
                platformModule.RuntimeToolBridge,
                platformModule.RestartCoordinator,
                platformModule.OverlayInputCaptureService,
                new TextSearchService(),
                navigationService,
                featureRegistry,
                editorContextService,
                languageRuntimeControl,
                languageRuntimeQuery,
                languageEditorOperations);
        }

        public CortexSettings BuildEffectiveSettings(CortexSettings settings, ICortexHostEnvironment hostEnvironment)
        {
            var effective = settings ?? new CortexSettings();
            var resolvedHostEnvironment = hostEnvironment ?? HostEnvironment;
            var platformModule = PlatformModule;

            if (string.IsNullOrEmpty(effective.RuntimeContentRootPath)) effective.RuntimeContentRootPath = resolvedHostEnvironment.RuntimeContentRootPath;
            if (string.IsNullOrEmpty(effective.WorkspaceRootPath)) effective.WorkspaceRootPath = CortexHostPathSettings.GetEffectiveWorkspaceRoot(effective);
            if (string.IsNullOrEmpty(effective.ReferenceAssemblyRootPath)) effective.ReferenceAssemblyRootPath = resolvedHostEnvironment.ReferenceAssemblyRootPath;
            if (string.IsNullOrEmpty(effective.AdditionalSourceRoots)) effective.AdditionalSourceRoots = effective.WorkspaceRootPath;
            if (string.IsNullOrEmpty(effective.LogFilePath)) effective.LogFilePath = resolvedHostEnvironment.LogFilePath;
            if (string.IsNullOrEmpty(effective.ProjectCatalogPath)) effective.ProjectCatalogPath = resolvedHostEnvironment.ProjectCatalogPath;
            if (string.IsNullOrEmpty(effective.CortexPluginSearchRoots)) effective.CortexPluginSearchRoots = resolvedHostEnvironment.ConfiguredPluginSearchRoots;
            if (string.IsNullOrEmpty(effective.DecompilerCachePath)) effective.DecompilerCachePath = resolvedHostEnvironment.DecompilerCachePath;
            if (string.IsNullOrEmpty(effective.AdditionalDecompilerCacheRoots)) effective.AdditionalDecompilerCacheRoots = platformModule.AdditionalDecompilerCacheRoots;
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
                var frameSnapshot = FrameContext.Snapshot;
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
                BundledToolRootPath = hostEnvironment != null ? hostEnvironment.BundledToolRootPath : string.Empty,
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

            return _hostServices != null && !string.IsNullOrEmpty(_hostServices.PreferredLanguageProviderId)
                ? _hostServices.PreferredLanguageProviderId
                : LanguageRuntimeConstants.NoneProviderId;
        }

        private void RegisterWorkbenchPlugins(
            IWorkbenchRuntime runtime,
            CortexSettings settings,
            IWorkbenchModuleRegistry moduleRegistry,
            Action<WorkbenchPluginLoadResult> pluginResultHandler)
        {
            var loader = new WorkbenchPluginLoader();
            var results = loader.LoadPlugins(
                settings,
                HostEnvironment,
                runtime.CommandRegistry,
                runtime.ContributionRegistry,
                moduleRegistry,
                _extensionRegistry,
                _runtimeAccess);

            for (var i = 0; i < results.Count; i++)
            {
                if (pluginResultHandler != null && results[i] != null)
                {
                    pluginResultHandler(results[i]);
                }
            }
        }
    }
}
