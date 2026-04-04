using System;
using System.Collections.Generic;
using System.IO;
using Cortex;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Runtime;
using Cortex.Shell;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class CortexRuntimeCompositionServiceTests
    {
        [Fact]
        public void CompositionService_InitializesSettingsAndServices_WithoutConcreteUiHost()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var root = Path.Combine(Path.GetTempPath(), "cortex-runtime-composition-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    var environment = new TestHostEnvironment(root);
                    var state = new CortexShellState();
                    var runtime = new NullLanguageRuntimeService();
                    var service = new CortexRuntimeCompositionService(
                        new WorkbenchExtensionRegistry(),
                        new WorkbenchRuntimeAccess(state, delegate { return null; }),
                        runtime,
                        runtime,
                        runtime);

                    service.ConfigureHostServices(new TestHostServices(environment));

                    var initialization = service.InitializeSettings();
                    var services = service.InitializeServices(initialization.Settings);

                    Assert.NotNull(initialization.SettingsStore);
                    Assert.NotNull(initialization.PersistenceService);
                    Assert.Equal(environment.ConfiguredPluginSearchRoots, initialization.Settings.CortexPluginSearchRoots);
                    Assert.NotNull(services);
                    Assert.NotNull(services.ProjectCatalog);
                    Assert.NotNull(services.DocumentService);
                    Assert.Null(service.InitializeWorkbenchRuntime(initialization.Settings, new CortexShellModuleContributionRegistry(), null));
                }
                finally
                {
                    if (Directory.Exists(root))
                    {
                        Directory.Delete(root, true);
                    }
                }
            });
        }

        private sealed class TestHostServices : ICortexHostServices
        {
            private readonly ICortexHostEnvironment _environment;

            public TestHostServices(ICortexHostEnvironment environment)
            {
                _environment = environment;
            }

            public ICortexHostEnvironment Environment
            {
                get { return _environment; }
            }

            public IPathInteractionService PathInteractionService
            {
                get { return null; }
            }

            public IWorkbenchRuntimeFactory WorkbenchRuntimeFactory
            {
                get { return null; }
            }

            public ICortexPlatformModule PlatformModule
            {
                get { return null; }
            }

            public IWorkbenchFrameContext FrameContext
            {
                get { return new TestWorkbenchFrameContext(); }
            }

            public string PreferredLanguageProviderId
            {
                get { return string.Empty; }
            }

            public IList<ILanguageProviderFactory> LanguageProviderFactories
            {
                get { return new List<ILanguageProviderFactory>(); }
            }
        }

        private sealed class TestHostEnvironment : ICortexHostEnvironment
        {
            private readonly string _rootPath;

            public TestHostEnvironment(string rootPath)
            {
                _rootPath = rootPath;
                Directory.CreateDirectory(Path.Combine(_rootPath, "bin"));
            }

            public string ApplicationRootPath { get { return _rootPath; } }
            public string HostRootPath { get { return _rootPath; } }
            public string HostBinPath { get { return Path.Combine(_rootPath, "bin"); } }
            public string BundledPluginSearchRoots { get { return string.Empty; } }
            public string BundledToolRootPath { get { return Path.Combine(_rootPath, "tooling"); } }
            public string ConfiguredPluginSearchRoots { get { return Path.Combine(_rootPath, "plugins"); } }
            public string ReferenceAssemblyRootPath { get { return Path.Combine(_rootPath, "refs"); } }
            public string RuntimeContentRootPath { get { return Path.Combine(_rootPath, "runtime"); } }
            public string SettingsFilePath { get { return Path.Combine(_rootPath, "settings.json"); } }
            public string WorkbenchPersistenceFilePath { get { return Path.Combine(_rootPath, "workbench.json"); } }
            public string LogFilePath { get { return Path.Combine(_rootPath, "cortex.log"); } }
            public string ProjectCatalogPath { get { return Path.Combine(_rootPath, "projects.json"); } }
            public string DecompilerCachePath { get { return Path.Combine(_rootPath, "cache"); } }
        }

        private sealed class TestWorkbenchFrameContext : IWorkbenchFrameContext
        {
            public WorkbenchFrameInputSnapshot Snapshot
            {
                get
                {
                    return new WorkbenchFrameInputSnapshot
                    {
                        ViewportSize = new RenderSize(1600f, 900f),
                        CurrentEventKind = WorkbenchInputEventKind.None,
                        CurrentRawEventKind = WorkbenchInputEventKind.None,
                        CurrentKey = WorkbenchInputKey.None,
                        CurrentMouseButton = -1,
                        CurrentMousePosition = RenderPoint.Zero,
                        PointerPosition = RenderPoint.Zero
                    };
                }
            }

            public void ConsumeCurrentInput()
            {
            }
        }
    }
}
