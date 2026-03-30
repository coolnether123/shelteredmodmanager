using Cortex.Plugins.Abstractions;

namespace Cortex.Plugin.Harmony
{
    public sealed class HarmonyPluginContributor : IWorkbenchPluginContributor
    {
        public string PluginId
        {
            get { return HarmonyPluginIds.PluginId; }
        }

        public string DisplayName
        {
            get { return "Harmony"; }
        }

        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            new HarmonyPluginComposition().Register(context);
        }
    }
}
