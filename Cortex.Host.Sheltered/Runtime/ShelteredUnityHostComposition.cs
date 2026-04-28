using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Contracts.Integration;
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
            var launchContext = ShelteredProductAdapter.CreateLaunchContext(environment);
            var productAdapters = new List<ICortexProductAdapter> { ShelteredProductAdapter.Instance };
            var hostServices = new UnityCortexHostServices(
                environment,
                new WindowsPathInteractionService(environment),
                new UnityWorkbenchRuntimeFactory(
                    new ShelteredUnityWorkbenchContributionRegistrar(renderHostCatalog, renderHostCatalog.StatusSummary),
                    runtimeUiFactory),
                platformModule,
                frameContext,
                launchContext,
                productAdapters,
                null);

            return new UnityCortexHostCompositionRoot(hostServices);
        }
    }
}
