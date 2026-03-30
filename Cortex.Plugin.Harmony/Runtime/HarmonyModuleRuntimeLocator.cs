using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    internal static class HarmonyModuleRuntimeLocator
    {
        public static IWorkbenchModuleRuntime Get(IWorkbenchRuntimeAccess runtimeAccess)
        {
            if (runtimeAccess == null || runtimeAccess.Modules == null)
            {
                return null;
            }

            var runtime = runtimeAccess.Modules.GetByContainer(HarmonyPluginIds.ContainerId);
            if (runtime != null)
            {
                return runtime;
            }

            return runtimeAccess.Modules.Get(HarmonyPluginIds.ModuleId);
        }
    }
}
