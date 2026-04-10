using System;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Renderers.DearImgui;
using Cortex.Shell.Unity.Imgui;
using Cortex.Core.Models;

namespace Cortex.Host.Unity.Runtime
{
    public static class UnityWorkbenchRuntimeUiFactorySelector
    {
        public static IWorkbenchRuntimeUiFactory Select(UnityRenderHostCatalog renderHostCatalog, IWorkbenchFrameContext frameContext)
        {
            var effectiveRenderHostId = renderHostCatalog != null
                ? UnityRenderHostSettings.NormalizeRenderHostId(renderHostCatalog.EffectiveRenderHostId)
                : UnityRenderHostSettings.DearImguiRenderHostId;

            if (string.Equals(effectiveRenderHostId, UnityRenderHostSettings.DearImguiRenderHostId, StringComparison.OrdinalIgnoreCase))
            {
                return DearImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(frameContext);
            }

            return ImguiWorkbenchRuntimeUiComposition.CreateRuntimeUiFactory(frameContext);
        }
    }
}
