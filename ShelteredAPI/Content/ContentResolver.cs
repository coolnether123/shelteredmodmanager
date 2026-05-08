using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Content
{
    /// <summary>
    /// Bridges registry metadata to loaded assets (sprites/prefabs) with graceful fallbacks.
    /// Call ResolveItems() during game init to prepare assets for registration into game systems.
    /// </summary>
    internal static class ContentResolver
    {
        public static List<ResolvedItem> ResolveItems()
        {
            MMLog.WriteDebug($"[ContentResolver] ResolveItems called. ContentRegistry.Items count: {ContentRegistry.Items.Count}");
            var resolved = new List<ResolvedItem>();
            foreach (var def in ContentRegistry.Items)
            {
                if (def == null) continue;
                def.NormalizeLegacyFields();
                var asm = def.OwnerAssembly ?? SafeCaller(def);
                var icon = def.Icon ?? TryLoadSprite(asm, def.IconPath);
                var prefab = TryLoadPrefab(asm, def.PrefabPath);

                resolved.Add(new ResolvedItem
                {
                    Definition = def,
                    Icon = icon,
                    Prefab = prefab
                });
                MMLog.WriteDebug($"[ContentResolver] Resolved item: {def.Id} (assembly: {asm?.GetName().Name ?? "null"})");
            }
            MMLog.WriteDebug($"[ContentResolver] ResolveItems complete: {resolved.Count} items");
            return resolved;
        }

        private static Assembly SafeCaller(ItemDefinition def)
        {
            try { return ContentOwnerAssemblyResolver.ResolveCallingAssembly(); } catch { return null; }
        }

        private static Sprite TryLoadSprite(Assembly asm, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var sprite = AssetLoader.LoadSprite(asm, path);
            if (sprite == null)
            {
                MMLog.WarnOnce("ContentResolver.Icon." + OwnerKey(asm) + "." + path, $"Failed to load icon at '{path}'");
            }
            return sprite;
        }

        private static GameObject TryLoadPrefab(Assembly asm, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // Support "bundlePath|assetName" to load a prefab from an asset bundle.
            string bundlePath = null;
            string assetName = null;
            var parts = path.Split(new[] { '|' }, 2);
            if (parts.Length == 2)
            {
                bundlePath = parts[0];
                assetName = parts[1];
            }

            if (!string.IsNullOrEmpty(bundlePath))
            {
                var bundle = AssetLoader.LoadBundle(asm, bundlePath);
                if (bundle == null)
                {
                    MMLog.WarnOnce("ContentResolver.Bundle." + OwnerKey(asm) + "." + bundlePath, $"Failed to load bundle '{bundlePath}'");
                    return null;
                }
                var prefab = AssetLoader.LoadPrefabFromBundle(bundle, assetName);
                if (prefab == null)
                {
                    MMLog.WarnOnce("ContentResolver.Prefab." + OwnerKey(asm) + "." + bundlePath + "." + assetName, $"Failed to load prefab '{assetName}' from bundle '{bundlePath}'");
                }
                return prefab;
            }

            // Direct prefab path (Resources-like) is not supported here; would require AssetDatabase/Resources.
            MMLog.WarnOnce("ContentResolver.PrefabPath." + OwnerKey(asm) + "." + path, $"Prefab path '{path}' not recognized. Use 'Assets/Bundles/xxx.bundle|PrefabName'.");
            return null;
        }

        private static string OwnerKey(Assembly asm)
        {
            if (asm == null)
                return "unknown";

            try
            {
                ModAPI.Core.ModEntry entry;
                if (ModAPI.Core.ModRegistry.TryGetModByAssembly(asm, out entry) && entry != null && !string.IsNullOrEmpty(entry.Id))
                    return entry.Id;

                return asm.GetName().Name;
            }
            catch
            {
                return "unknown";
            }
        }
    }

    /// <summary>
    /// Item definition paired with loaded assets.
    /// </summary>
    internal class ResolvedItem
    {
        public ItemDefinition Definition;
        public Sprite Icon;
        public GameObject Prefab;
    }
}
