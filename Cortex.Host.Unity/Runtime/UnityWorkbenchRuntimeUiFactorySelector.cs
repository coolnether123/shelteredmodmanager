using System;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Shell.Unity.Imgui;
using Cortex.Shell.Unity.Overlay;

namespace Cortex.Host.Unity.Runtime
{
    public static class UnityWorkbenchRuntimeUiFactorySelector
    {
        public static IWorkbenchRuntimeUiFactory Select(UnityRenderHostCatalog renderHostCatalog, IWorkbenchFrameContext frameContext)
        {
            var effectiveRenderHostId = renderHostCatalog != null
                ? UnityRenderHostSettings.NormalizeRenderHostId(renderHostCatalog.EffectiveRenderHostId)
                : UnityRenderHostSettings.ImguiRenderHostId;

            if (string.Equals(effectiveRenderHostId, UnityRenderHostSettings.OverlayInProcessRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                return OverlayWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(frameContext);
            }

            return ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(frameContext);
        }
    }
}
