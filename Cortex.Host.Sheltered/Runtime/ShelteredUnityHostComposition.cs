using System;
using Cortex.Core.Abstractions;
using Cortex.Host.Unity.Runtime;
using Cortex.Shell.Unity.Imgui;

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
            var pathLayout = ShelteredHostPathLayout.FromApplicationRoot(environment.ApplicationRootPath);
            var renderHostCatalog = new ShelteredRenderHostCatalogBuilder().Build(
                pathLayout,
                ShelteredRenderHostSettings.LoadSelectedRenderHostId(environment));
            var frameContext = new UnityWorkbenchFrameContext();
            var hostServices = new UnityCortexHostServices(
                environment,
                new WindowsPathInteractionService(environment),
                new UnityWorkbenchRuntimeFactory(
                    new ShelteredUnityWorkbenchContributionRegistrar(renderHostCatalog, renderHostCatalog.StatusSummary),
                    ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(frameContext)),
                platformModule,
                frameContext,
                new ShelteredExternalAvaloniaHostStartupAction(renderHostCatalog));

            return new UnityCortexHostCompositionRoot(hostServices);
        }
    }
}
