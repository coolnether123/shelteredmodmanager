using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace ModAPI.Core
{
    /// <summary>
    /// Central registry for all discovered and loaded mods.
    /// </summary>
    public static class ModRegistry
    {
        private static Dictionary<string, ModEntry> _modsById = new Dictionary<string, ModEntry>(System.StringComparer.OrdinalIgnoreCase);
        private static Dictionary<Assembly, ModEntry> _modByAssembly = new Dictionary<Assembly, ModEntry>();

        /// <summary>
        /// Registers or updates a mod entry by id.
        /// </summary>
        public static void Register(ModEntry entry)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Id))
            {
                _modsById[entry.Id] = entry;
            }
        }

        /// <summary>
        /// Alias for Register - registers a mod by its ID.
        /// </summary>
        public static void RegisterModById(ModEntry entry)
        {
            Register(entry);
        }

        /// <summary>
        /// Associates a loaded assembly with its owning mod entry.
        /// </summary>
        public static void RegisterAssemblyForMod(Assembly assembly, ModEntry mod)
        {
            if (assembly != null && mod != null)
            {
                _modByAssembly[assembly] = mod;
            }
        }

        /// <summary>
        /// Returns a mod entry by id, or null when not registered.
        /// </summary>
        public static ModEntry GetMod(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return null;
            _modsById.TryGetValue(modId, out var entry);
            return entry;
        }

        /// <summary>
        /// Resolves the mod owner for a previously loaded assembly.
        /// </summary>
        public static bool TryGetModByAssembly(Assembly assembly, out ModEntry mod)
        {
            if (assembly == null)
            {
                mod = null;
                return false;
            }
            return _modByAssembly.TryGetValue(assembly, out mod);
        }

        /// <summary>
        /// Enumerates currently registered mod entries.
        /// </summary>
        public static IEnumerable<ModEntry> GetAllMods() => _modsById.Values;

        // Add these back as thin wrappers
        public static bool Find(string modId)
            => GetMod(modId) != null;

        public static bool TryGetMod(string modId, out ModEntry entry)
            => (entry = GetMod(modId)) != null;

        public static List<string> GetLoadedModIds()
            => new List<string>(GetAllMods().Select(m => m.Id));

        public static List<ModEntry> GetLoadedMods()
            => new List<ModEntry>(GetAllMods());

        public static int GetLoadedModCount()
            => GetAllMods().Count();


        // --- Service Locator (APIs) -----------------------------------------

        /// <summary>
        /// Register an API object that other mods can consume.
        /// </summary>
        public static void RegisterAPI(string apiId, object implementation)
        {
            ModAPIRegistry.RegisterAPI(apiId, implementation);
        }

        /// <summary>
        /// Try to get a registered API of type T.
        /// </summary>
        public static bool TryGetAPI<T>(string apiId, out T api) where T : class
        {
            return ModAPIRegistry.TryGetAPI<T>(apiId, out api);
        }

        /// <summary>
        /// Get a registered API of type T. Returns null if not found.
        /// </summary>
        public static T GetAPI<T>(string apiId) where T : class
        {
            return ModAPIRegistry.GetAPI<T>(apiId);
        }


        /// <summary>
        /// Clears all mod and assembly mappings. Intended for teardown/testing only.
        /// </summary>
        public static void Clear()
        {
            _modsById.Clear();
            _modByAssembly.Clear();
        }
    }
}
