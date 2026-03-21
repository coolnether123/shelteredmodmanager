using System.Collections.Generic;
using System.IO;
using Cortex;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Core.Abstractions;
using Cortex.Host.Unity.Runtime;
using Cortex.Presentation.Abstractions;
using Cortex.Services;

namespace Cortex.Tests.Testing
{
    internal sealed class OnboardingTestContext
    {
        public readonly ContributionRegistry Registry;
        public readonly CortexOnboardingService Service;
        public readonly CortexOnboardingProjectSetupService ProjectSetupService;
        public readonly CortexOnboardingWorkspaceApplier WorkspaceApplier;
        public readonly CortexOnboardingCoordinator Coordinator;
        public readonly CortexShellState ShellState;
        public readonly IWorkbenchRuntime Runtime;
        public readonly IProjectCatalog ProjectCatalog;
        public readonly IProjectWorkspaceService ProjectWorkspaceService;
        public readonly ILoadedModCatalog LoadedModCatalog;
        public readonly ICortexPlatformModule PlatformModule;
        public readonly IOverlayInputCaptureService OverlayInputCaptureService;
        public readonly string WorkspaceRootPath;
        public readonly string ModSourceRootPath;

        public OnboardingTestContext()
        {
            Registry = OnboardingTestRegistryBuilder.CreateDefault();
            Service = new CortexOnboardingService();
            ProjectSetupService = new CortexOnboardingProjectSetupService();
            WorkspaceApplier = new CortexOnboardingWorkspaceApplier();
            Coordinator = new CortexOnboardingCoordinator(Service, ProjectSetupService, WorkspaceApplier, null);
            ShellState = new CortexShellState();
            ShellState.Settings = new CortexSettings();
            Runtime = new UnityWorkbenchRuntime();
            WorkspaceRootPath = CreateWorkspaceRoot();
            ModSourceRootPath = CreateModSourceRoot(WorkspaceRootPath, "TestMod");
            Directory.CreateDirectory(Path.Combine(WorkspaceRootPath, "LiveMods", "TestMod"));
            ProjectCatalog = new ProjectCatalog(new InMemoryProjectConfigurationStore());
            ProjectWorkspaceService = new ProjectWorkspaceService(new SourceLookupIndexService());
            LoadedModCatalog = new InMemoryLoadedModCatalog(new[]
            {
                new LoadedModInfo
                {
                    ModId = "TestMod",
                    DisplayName = "Test Mod",
                    RootPath = Path.Combine(WorkspaceRootPath, "LiveMods", "TestMod")
                }
            });
            OverlayInputCaptureService = new TestOverlayInputCaptureService();
            PlatformModule = new TestCortexPlatformModule(LoadedModCatalog, OverlayInputCaptureService);
            ShellState.Settings.WorkspaceRootPath = WorkspaceRootPath;
        }

        public CortexOnboardingCatalog BuildCatalog()
        {
            return Service.BuildCatalog(Registry);
        }

        public CortexOnboardingResolvedSelection ResolveSelection()
        {
            return Service.ResolveSelection(ShellState.Onboarding, ShellState.Settings, BuildCatalog());
        }

        private static string CreateWorkspaceRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "cortex-onboarding-tests", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "LiveMods"));
            return root;
        }

        private static string CreateModSourceRoot(string workspaceRoot, string modId)
        {
            var sourceRoot = Path.Combine(workspaceRoot, modId);
            Directory.CreateDirectory(sourceRoot);
            File.WriteAllText(Path.Combine(sourceRoot, modId + ".csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            File.WriteAllText(Path.Combine(sourceRoot, "Feature.cs"), "namespace Tests { public sealed class Feature { } }");
            return sourceRoot;
        }

        private sealed class InMemoryProjectConfigurationStore : IProjectConfigurationStore
        {
            private IList<CortexProjectDefinition> _definitions = new List<CortexProjectDefinition>();

            public IList<CortexProjectDefinition> LoadProjects()
            {
                return new List<CortexProjectDefinition>(_definitions);
            }

            public void SaveProjects(IList<CortexProjectDefinition> projects)
            {
                _definitions = projects != null ? new List<CortexProjectDefinition>(projects) : new List<CortexProjectDefinition>();
            }
        }
    }
}
