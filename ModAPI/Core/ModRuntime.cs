using System.Collections.Generic;

namespace ModAPI.Core
{
    /// <summary>
    /// Host-neutral read access to the active ModAPI runtime.
    /// </summary>
    public static class ModRuntime
    {
        public static bool IsQuitting
        {
            get { return PluginRunner.IsQuitting; }
            set { PluginRunner.IsQuitting = value; }
        }

        public static List<ModEntry> LoadedMods
        {
            get { return PluginManager.LoadedMods; }
        }

        public static IEnumerable<IModPlugin> GetPlugins()
        {
            return PluginManager.getInstance().GetPlugins();
        }

        public static List<ModEntry> DiscoverAllMods()
        {
            return ModDiscovery.DiscoverAllMods();
        }

        public static void MarkSaveExit(string step, string detail = null)
        {
            SaveExitTracker.Mark(step, detail);
        }
    }
}
