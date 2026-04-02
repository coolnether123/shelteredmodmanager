using System;
using Cortex.Core.Abstractions;
using Cortex.Host.Unity.Runtime;

namespace Cortex.Host.Sheltered.Runtime
{
    public static class ShelteredUnityHostComposition
    {
        public static UnityCortexHostCompositionRoot Create(ICortexPlatformModule platformModule)
        {
            if (platformModule == null)
            {
                throw new ArgumentNullException("platformModule");
            }

            var environment = new ShelteredCortexHostEnvironment();
            var hostServices = new UnityCortexHostServices(
                environment,
                new WindowsPathInteractionService(environment),
                new UnityWorkbenchRuntimeFactory(new ShelteredUnityWorkbenchContributionRegistrar()),
                platformModule,
                new UnityCortexShellHostUi());

            return new UnityCortexHostCompositionRoot(hostServices);
        }
    }
}
