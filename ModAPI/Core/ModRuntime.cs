using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModAPI.Core
{
    /// <summary>
    /// Host-neutral read access to the active ModAPI runtime.
    /// </summary>
    public static class ModRuntime
    {
        public static event System.Action PluginsActivated;

        public static bool IsQuitting
        {
            get { return PluginRunner.IsQuitting; }
        }

        public static bool ArePluginsActivated
        {
            get { return PluginManager.PluginsActivated; }
        }

        public static ReadOnlyCollection<ModEntry> LoadedMods
        {
            get { return GetLoadedModsSnapshot().AsReadOnly(); }
        }

        public static List<ModEntry> GetLoadedModsSnapshot()
        {
            List<ModEntry> loadedMods = PluginManager.LoadedMods;
            if (loadedMods == null) return new List<ModEntry>();

            lock (loadedMods)
            {
                return new List<ModEntry>(loadedMods);
            }
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

        internal static void ResetQuitStateForHost()
        {
            PluginRunner.IsQuitting = false;
        }

        internal static void NotifyPluginsActivated()
        {
            System.Action handlers = PluginsActivated;
            if (handlers == null)
                return;

            System.Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                System.Action handler = invocationList[i] as System.Action;
                if (handler == null)
                    continue;

                try
                {
                    handler();
                }
                catch (System.Exception ex)
                {
                    MMLog.WritePluginError("ModRuntime.PluginsActivated", "PluginsActivated", ex);
                }
            }
        }
    }
}
