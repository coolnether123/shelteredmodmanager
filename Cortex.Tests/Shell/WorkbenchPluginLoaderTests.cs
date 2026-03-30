using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Plugin.Harmony;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class WorkbenchPluginLoaderTests
    {
        [Fact]
        public void LoadPlugins_LoadsBundledHarmonyAndThirdPartyContributorThroughSharedPipeline()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "cortex-plugin-loader-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                    var bundledRoot = Path.Combine(tempRoot, "bin", "plugins");
                    var harmonyManifestPath = Path.Combine(bundledRoot, "Harmony", "cortex.plugin.json");
                    WriteManifest(harmonyManifestPath, typeof(HarmonyPluginContributor));

                    var externalRoot = Path.Combine(tempRoot, "external");
                    var externalManifestPath = Path.Combine(externalRoot, "Review", "cortex.plugin.json");
                    WriteManifest(externalManifestPath, typeof(ReviewNotesWorkbenchPlugin));

                    var state = new CortexShellState();
                    var runtime = new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry());
                    var loader = new WorkbenchPluginLoader();
                    var moduleRegistry = new CortexShellModuleContributionRegistry();
                    var extensionRegistry = new WorkbenchExtensionRegistry();
                    var runtimeAccess = new WorkbenchRuntimeAccess(state, delegate { return null; });
                    var hostEnvironment = new TestHostEnvironment(Path.Combine(tempRoot, "bin"), bundledRoot);
                    var settings = new CortexSettings
                    {
                        CortexPluginSearchRoots = externalRoot,
                        ModsRootPath = Path.Combine(tempRoot, "mods")
                    };

                    var results = loader.LoadPlugins(
                        settings,
                        hostEnvironment,
                        runtime.CommandRegistry,
                        runtime.ContributionRegistry,
                        moduleRegistry,
                        extensionRegistry,
                        runtimeAccess);

                    Assert.Equal(2, results.Count(result => result != null && result.Loaded));
                    Assert.Contains(results, result => result != null &&
                        result.Loaded &&
                        string.Equals(result.PluginId, HarmonyPluginIds.PluginId, StringComparison.Ordinal));
                    Assert.Contains(results, result => result != null &&
                        result.Loaded &&
                        string.Equals(result.PluginId, ReviewNotesWorkbenchPlugin.PluginIdentity, StringComparison.Ordinal));
                    Assert.Contains(runtime.ContributionRegistry.GetViewContainers(), container => string.Equals(container.ContainerId, HarmonyPluginIds.ContainerId, StringComparison.Ordinal));
                    Assert.Contains(runtime.ContributionRegistry.GetViewContainers(), container => string.Equals(container.ContainerId, ReviewNotesWorkbenchPlugin.ContainerId, StringComparison.Ordinal));
                    Assert.NotNull(moduleRegistry.FindContribution(HarmonyPluginIds.ContainerId));
                    Assert.NotNull(moduleRegistry.FindContribution(ReviewNotesWorkbenchPlugin.ContainerId));
                    Assert.NotNull(runtime.CommandRegistry.Get("cortex.window.harmony"));
                    Assert.NotNull(runtime.CommandRegistry.Get(ReviewNotesWorkbenchPlugin.CommandId));
                }
                finally
                {
                    TryDeleteDirectory(tempRoot);
                }
            });
        }

        [Fact]
        public void BuiltInModuleRegistrar_DoesNotRegisterHarmonyModule()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var state = new CortexShellState();
                var runtime = new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry());
                var services = CreateModuleServices(state, runtime);
                var registry = new CortexShellModuleContributionRegistry();

                new CortexShellBuiltInModuleRegistrar().RegisterBuiltIns(
                    registry,
                    new WorkbenchExtensionRegistry(),
                    new WorkbenchRuntimeAccess(state, delegate { return null; }),
                    services);

                Assert.Null(registry.FindContribution(HarmonyPluginIds.ContainerId));
                Assert.NotNull(registry.FindContribution(CortexWorkbenchIds.EditorContainer));
                Assert.NotNull(registry.FindContribution(CortexWorkbenchIds.ProjectsContainer));
            });
        }

        [Fact]
        public void LoadPlugins_IgnoresDllsWithoutPluginManifest()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var tempRoot = Path.Combine(Path.GetTempPath(), "cortex-plugin-loader-manifest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempRoot);

                try
                {
                    var bundledRoot = Path.Combine(tempRoot, "bin", "plugins");
                    Directory.CreateDirectory(bundledRoot);
                    var copiedAssemblyPath = Path.Combine(bundledRoot, "ReviewNotesWorkbenchPlugin.dll");
                    File.Copy(typeof(ReviewNotesWorkbenchPlugin).Assembly.Location, copiedAssemblyPath, true);

                    var runtime = new TestWorkbenchRuntime(new CommandRegistry(), new ContributionRegistry());
                    var loader = new WorkbenchPluginLoader();
                    var moduleRegistry = new CortexShellModuleContributionRegistry();
                    var extensionRegistry = new WorkbenchExtensionRegistry();
                    var runtimeAccess = new WorkbenchRuntimeAccess(new CortexShellState(), delegate { return null; });
                    var hostEnvironment = new TestHostEnvironment(Path.Combine(tempRoot, "bin"), bundledRoot);

                    var results = loader.LoadPlugins(
                        new CortexSettings { ModsRootPath = Path.Combine(tempRoot, "mods") },
                        hostEnvironment,
                        runtime.CommandRegistry,
                        runtime.ContributionRegistry,
                        moduleRegistry,
                        extensionRegistry,
                        runtimeAccess);

                    Assert.Empty(results);
                    Assert.DoesNotContain(runtime.ContributionRegistry.GetViewContainers(), container => string.Equals(container.ContainerId, ReviewNotesWorkbenchPlugin.ContainerId, StringComparison.Ordinal));
                    Assert.Null(moduleRegistry.FindContribution(ReviewNotesWorkbenchPlugin.ContainerId));
                }
                finally
                {
                    TryDeleteDirectory(tempRoot);
                }
            });
        }

        private static CortexShellModuleServices CreateModuleServices(CortexShellState state, TestWorkbenchRuntime runtime)
        {
            return new CortexShellModuleServices(
                state,
                new ShellServiceMap
                {
                    FeatureRegistry = new ShellFeatureRegistry(),
                    ProjectCatalog = new EmptyProjectCatalog(),
                    LoadedModCatalog = new InMemoryLoadedModCatalog(new List<LoadedModInfo>()),
                    EditorContextService = new Cortex.Services.Semantics.Context.EditorContextService(
                        new EditorService(),
                        new Cortex.Services.Editor.Context.EditorCommandContextFactory(),
                        new Cortex.Services.Editor.Context.EditorSymbolInteractionService())
                },
                delegate { return null; },
                delegate { return runtime; },
                delegate { return null; },
                null);
        }

        private static void WriteManifest(string manifestPath, Type entryType)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? string.Empty);
            var assemblyPath = entryType != null && entryType.Assembly != null
                ? entryType.Assembly.Location ?? string.Empty
                : string.Empty;
            var entryTypeName = entryType != null ? entryType.FullName ?? string.Empty : string.Empty;
            var json =
                "{\r\n" +
                "  \"PluginId\": \"" + EscapeJson(entryTypeName) + "\",\r\n" +
                "  \"DisplayName\": \"" + EscapeJson(entryType != null ? entryType.Name : string.Empty) + "\",\r\n" +
                "  \"AssemblyPath\": \"" + EscapeJson(assemblyPath) + "\",\r\n" +
                "  \"EntryTypeName\": \"" + EscapeJson(entryTypeName) + "\",\r\n" +
                "  \"Enabled\": true\r\n" +
                "}";
            File.WriteAllText(manifestPath, json);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private sealed class EmptyProjectCatalog : IProjectCatalog
        {
            public IList<CortexProjectDefinition> GetProjects()
            {
                return new List<CortexProjectDefinition>();
            }

            public CortexProjectDefinition GetProject(string modId)
            {
                return null;
            }

            public void Upsert(CortexProjectDefinition definition)
            {
            }

            public void Remove(string modId)
            {
            }
        }

        private sealed class TestHostEnvironment : ICortexHostEnvironment
        {
            private readonly string _hostBinPath;
            private readonly string _bundledPluginSearchRoots;

            public TestHostEnvironment(string hostBinPath, string bundledPluginSearchRoots)
            {
                _hostBinPath = hostBinPath ?? string.Empty;
                _bundledPluginSearchRoots = bundledPluginSearchRoots ?? string.Empty;
            }

            public string GameRootPath => string.Empty;
            public string HostRootPath => string.Empty;
            public string HostBinPath => _hostBinPath;
            public string BundledPluginSearchRoots => _bundledPluginSearchRoots;
            public string ManagedAssemblyRootPath => string.Empty;
            public string ModsRootPath => string.Empty;
            public string SettingsFilePath => string.Empty;
            public string WorkbenchPersistenceFilePath => string.Empty;
            public string LogFilePath => string.Empty;
            public string ProjectCatalogPath => string.Empty;
            public string DecompilerCachePath => string.Empty;
        }

        private sealed class TestWorkbenchRuntime : IWorkbenchRuntime
        {
            public TestWorkbenchRuntime(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry)
            {
                CommandRegistry = commandRegistry;
                ContributionRegistry = contributionRegistry;
                WorkbenchState = new WorkbenchState();
                LayoutState = new LayoutState();
                StatusState = new StatusState();
                ThemeState = new ThemeState();
                FocusState = new FocusState();
            }

            public ICommandRegistry CommandRegistry { get; private set; }

            public IContributionRegistry ContributionRegistry { get; private set; }

            public WorkbenchState WorkbenchState { get; private set; }

            public LayoutState LayoutState { get; private set; }

            public StatusState StatusState { get; private set; }

            public ThemeState ThemeState { get; private set; }

            public FocusState FocusState { get; private set; }

            public Cortex.Presentation.Models.WorkbenchPresentationSnapshot CreateSnapshot()
            {
                return new Cortex.Presentation.Models.WorkbenchPresentationSnapshot();
            }
        }
    }

    public sealed class ReviewNotesWorkbenchPlugin : IWorkbenchPluginContributor
    {
        public const string PluginIdentity = "review.notes";
        public const string ContainerId = "container.review.notes";
        public const string CommandId = "review.notes.open";

        public string PluginId
        {
            get { return PluginIdentity; }
        }

        public string DisplayName
        {
            get { return "Review Notes"; }
        }

        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterViewContainer(
                ContainerId,
                "Review Notes",
                WorkbenchHostLocation.SecondarySideHost,
                35,
                true,
                ModuleActivationKind.OnCommand,
                CommandId,
                ContainerId);
            context.RegisterView(ContainerId + ".main", ContainerId, "Review Notes", ContainerId + ".main", 0, true);
            context.RegisterIcon(new IconContribution
            {
                IconId = ContainerId,
                Alias = "RV"
            });
            context.RegisterModule(new ReviewNotesModuleContribution());
            context.RegisterCommand(CommandId, "Open Review Notes", "Review", "Open review notes.", string.Empty, 10, true, true);
        }

        private sealed class ReviewNotesModuleContribution : IWorkbenchModuleContribution
        {
            public ReviewNotesModuleContribution()
            {
                Descriptor = new WorkbenchModuleDescriptor("review.notes", ContainerId, typeof(ReviewNotesModule));
            }

            public WorkbenchModuleDescriptor Descriptor { get; private set; }

            public IWorkbenchModule CreateModule(IWorkbenchModuleRuntime runtime)
            {
                return new ReviewNotesModule();
            }
        }

        private sealed class ReviewNotesModule : IWorkbenchModule
        {
            public string GetUnavailableMessage()
            {
                return string.Empty;
            }

            public void Render(WorkbenchModuleRenderContext context, bool detachedWindow)
            {
            }
        }
    }
}
