using System;
using System.Collections.Generic;
using System.Reflection;

/**
 * Mod-to-Assembly registry to resolve a plugin's mod root/id at runtime.
 * Author: Coolnether123
 */
public static class ModRegistry
{
    // Map assembly.Location (full path) -> ModEntry (discovered)
    private static readonly Dictionary<string, ModEntry> _byAssemblyPath = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);

    // Registers an assembly as belonging to a discovered mod (Coolnether123)
    public static void RegisterAssemblyForMod(Assembly asm, ModEntry entry)
    {
        if (asm == null || entry == null) return;
        string key = SafeLocation(asm);
        if (key == null) return;
        _byAssemblyPath[key] = entry;
    }

    // Attempts to resolve the ModEntry for a given assembly (Coolnether123)
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
}

