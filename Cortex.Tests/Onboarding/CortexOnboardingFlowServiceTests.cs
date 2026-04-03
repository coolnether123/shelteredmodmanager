using System.Linq;
using Cortex.Services.Onboarding;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Onboarding
{
    public sealed class CortexOnboardingFlowServiceTests
    {
        [Fact]
        public void BuildFlow_IncludesProjectsStep_ForModderProfile()
        {
            var context = new OnboardingTestContext();
            var flowService = new CortexOnboardingFlowService();

            context.ShellState.Onboarding.SelectedProfileId = OnboardingTestRegistryBuilder.ModderProfileId;
            var flow = flowService.BuildFlow(context.ShellState.Onboarding, context.BuildCatalog(), context.Service);

            Assert.Contains(flow.Steps, step => step.StepKind == CortexOnboardingStepKind.Projects);
            Assert.Equal(4, flow.Steps.Count);
        }

        [Fact]
        public void ClampActiveStepIndex_RemovesStaleProjectsStep_WhenProfileChanges()
        {
            var context = new OnboardingTestContext();
            var flowService = new CortexOnboardingFlowService();

            context.ShellState.Onboarding.SelectedProfileId = OnboardingTestRegistryBuilder.ModderProfileId;
            context.ShellState.Onboarding.ActiveStepIndex = 3;
            context.ShellState.Onboarding.SelectedProfileId = OnboardingTestRegistryBuilder.DecompilerProfileId;

            var flow = flowService.BuildFlow(context.ShellState.Onboarding, context.BuildCatalog(), context.Service);
            flowService.ClampActiveStepIndex(context.ShellState.Onboarding, flow);

            Assert.DoesNotContain(flow.Steps, step => step.StepKind == CortexOnboardingStepKind.Projects);
            Assert.Equal(2, context.ShellState.Onboarding.ActiveStepIndex);
            Assert.Equal(flow.Steps.Last().StepKind, CortexOnboardingStepKind.Theme);
        }
    }
}
