using System;
using System.Collections.Generic;
using System.Reflection;

namespace ModAPI.Core
{
    /**
     * Mod-to-Assembly registry to resolve a plugin's mod root/id at runtime.
     * Author: Coolnether123
     */
    public static class ModRegistry
    {
        // Map assembly.Location (full path) -> ModEntry (discovered)
        private static readonly Dictionary<string, ModEntry> _byAssemblyPath = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);

        // Registers an assembly as belonging to a discovered mod
        public static void RegisterAssemblyForMod(Assembly asm, ModEntry entry)
        {
            if (asm == null || entry == null) return;
            string key = SafeLocation(asm);
            if (key == null) return;
            _byAssemblyPath[key] = entry;
        }

        // Attempts to resolve the ModEntry for a given assembly 
        public static bool TryGetModByAssembly(Assembly asm, out ModEntry entry)
        {
            entry = null;
            string key = SafeLocation(asm);
            if (key == null) return false;
            return _byAssemblyPath.TryGetValue(key, out entry);
        }

        private static string SafeLocation(Assembly asm)
        {
            try { return asm.Location; } catch { return null; }
        }
        
        // ============================================================================
        // Mod Discovery Utilities (Phase 1: Event System Expansion)
        // ============================================================================
        
        // Track all registered mods by ID
        private static readonly Dictionary<string, ModEntry> _byModId = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Register a mod entry by its ID for quick lookup.
        /// Called internally by PluginManager during mod discovery.
        /// </summary>
        internal static void RegisterModById(ModEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id))
                return;
            
            _byModId[entry.Id] = entry;
            MMLog.WriteDebug($"[ModRegistry] Registered mod: {entry.Id} ({entry.Name})");
        }
        
        /// <summary>
        /// Check if a mod with the given ID is loaded and enabled.
        /// Example: ModRegistry.Find("com.myname.mymod")
        /// </summary>
        /// <param name="modId">Mod ID to check</param>
        /// <returns>True if the mod is loaded and enabled</returns>
        public static bool Find(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return false;
            
            return _byModId.ContainsKey(modId);
        }
        
        /// <summary>
        /// Get a mod entry by its ID.
        /// </summary>
        /// <param name="modId">Mod ID to retrieve</param>
        /// <returns>ModEntry or null if not found</returns>
        public static ModEntry GetMod(string modId)
        {
            if (string.IsNullOrEmpty(modId))
                return null;
            
            ModEntry entry;
            return _byModId.TryGetValue(modId, out entry) ? entry : null;
        }
        
        /// <summary>
        /// Try to get a mod entry by its ID.
        /// </summary>
        /// <param name="modId">Mod ID to retrieve</param>
        /// <param name="entry">Output ModEntry</param>
        /// <returns>True if mod was found</returns>
        public static bool TryGetMod(string modId, out ModEntry entry)
        {
            entry = GetMod(modId);
            return entry != null;
        }
        
        /// <summary>
        /// Get all loaded mod IDs.
        /// </summary>
        /// <returns>List of loaded mod IDs</returns>
        public static List<string> GetLoadedModIds()
        {
            return new List<string>(_byModId.Keys);
        }
        
        /// <summary>
        /// Get all loaded mod entries.
        /// </summary>
        /// <returns>List of ModEntry objects</returns>
        public static List<ModEntry> GetLoadedMods()
        {
            return new List<ModEntry>(_byModId.Values);
        }
        
        /// <summary>
        /// Get count of loaded mods.
        /// </summary>
        /// <returns>Number of loaded mods</returns>
        public static int GetLoadedModCount()
        {
            return _byModId.Count;
        }
        
        /// <summary>
        /// Check if any mods are loaded.
        /// </summary>
        /// <returns>True if at least one mod is loaded</returns>
        public static bool HasLoadedMods()
        {
            return _byModId.Count > 0;
        }
    }
}