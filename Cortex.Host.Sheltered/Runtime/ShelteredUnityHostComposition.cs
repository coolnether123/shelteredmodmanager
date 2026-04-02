using System;
using Cortex.Core.Abstractions;
using Cortex.Host.Unity.Runtime;
using Cortex.Renderers.Imgui;

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
            var frameContext = new UnityWorkbenchFrameContext();
            var hostServices = new UnityCortexHostServices(
                environment,
                new WindowsPathInteractionService(environment),
                new UnityWorkbenchRuntimeFactory(
                    new ShelteredUnityWorkbenchContributionRegistrar(),
                    new ImguiWorkbenchRuntimeUiFactory(new CortexWorkbenchUiSurface(), frameContext)),
                platformModule,
                frameContext);

            return new UnityCortexHostCompositionRoot(hostServices);
        }
    }
}
