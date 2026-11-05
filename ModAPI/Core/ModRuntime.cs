using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ModAPI.Core
{
    /// <summary>
    /// Host-neutral read access to the active ModAPI runtime.
    /// </summary>
    public static class ModRuntime
    {
        /// <summary>
        /// Raised after a mod passes compatibility checks and before its plugin
        /// types initialize. Host integrations can use this ordered boundary to
        /// register data-only content owned by the activating mod.
        /// </summary>
        public static event System.Action<ModEntry> ModActivating;

        /// <summary>Raised after all prepared mods finish activation.</summary>
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
            SubscriberDispatch.Invoke(PluginsActivated, "ModRuntime.PluginsActivated");
        }

        internal static void NotifyModActivating(ModEntry entry)
        {
            if (entry != null) SubscriberDispatch.Invoke(ModActivating, "ModRuntime.ModActivating", entry);
        }
    }
}
