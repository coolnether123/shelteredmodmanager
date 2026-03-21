using Cortex;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Host.Unity.Runtime;
using Cortex.Services;

namespace Cortex.Tests.Testing
{
    internal sealed class OnboardingTestContext
    {
        public readonly ContributionRegistry Registry;
        public readonly CortexOnboardingService Service;
        public readonly CortexOnboardingWorkspaceApplier WorkspaceApplier;
        public readonly CortexOnboardingCoordinator Coordinator;
        public readonly CortexShellState ShellState;
        public readonly UnityWorkbenchRuntime Runtime;

        public OnboardingTestContext()
        {
            Registry = OnboardingTestRegistryBuilder.CreateDefault();
            Service = new CortexOnboardingService();
            WorkspaceApplier = new CortexOnboardingWorkspaceApplier();
            Coordinator = new CortexOnboardingCoordinator(Service, WorkspaceApplier, null);
            ShellState = new CortexShellState();
            ShellState.Settings = new CortexSettings();
            Runtime = new UnityWorkbenchRuntime();
        }

        public CortexOnboardingCatalog BuildCatalog()
        {
            return Service.BuildCatalog(Registry);
        }

        public CortexOnboardingResolvedSelection ResolveSelection()
        {
            return Service.ResolveSelection(ShellState.Onboarding, ShellState.Settings, BuildCatalog());
        }
    }
}
