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
            var renderHostCatalog = new UnityRenderHostCatalogBuilder().Build(
                environment,
                UnityRenderHostSettings.LoadSelectedRenderHostId(environment));
            var frameContext = new UnityWorkbenchFrameContext();
            var runtimeUiFactory = UnityWorkbenchRuntimeUiFactorySelector.Select(renderHostCatalog, frameContext);
            var hostServices = new UnityCortexHostServices(
                environment,
                new WindowsPathInteractionService(environment),
                new UnityWorkbenchRuntimeFactory(
                    new ShelteredUnityWorkbenchContributionRegistrar(renderHostCatalog, renderHostCatalog.StatusSummary),
                    runtimeUiFactory),
                platformModule,
                frameContext);

            return new UnityCortexHostCompositionRoot(hostServices);
        }
    }
}
