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

        public static void RegisterAssemblyForMod(Assembly assembly, ModEntry mod)
        {
            if (assembly != null && mod != null)
            {
                _modByAssembly[assembly] = mod;
            }
        }

        public static ModEntry GetMod(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return null;
            _modsById.TryGetValue(modId, out var entry);
            return entry;
        }

        public static bool TryGetModByAssembly(Assembly assembly, out ModEntry mod)
        {
            if (assembly == null)
            {
                mod = null;
                return false;
            }
            return _modByAssembly.TryGetValue(assembly, out mod);
        }

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

        public static bool HasLoadedMods()
            => GetLoadedModCount() > 0;


        public static void Clear()
        {
            _modsById.Clear();
            _modByAssembly.Clear();
        }
    }
}