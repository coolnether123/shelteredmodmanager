using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Content
{
    /// <summary>
    /// Bridges registry metadata to loaded assets (sprites/prefabs) with graceful fallbacks.
    /// Call ResolveItems() during game init to prepare assets for registration into game systems.
    /// </summary>
    public static class ContentResolver
    {
        public static List<ResolvedItem> ResolveItems()
        {
            MMLog.Write($"[ContentResolver] ResolveItems called. ContentRegistry.Items count: {ContentRegistry.Items.Count}");
            var resolved = new List<ResolvedItem>();
            foreach (var def in ContentRegistry.Items)
            {
                if (def == null) continue;
                var asm = def.OwnerAssembly ?? SafeCaller(def);
                var icon = TryLoadSprite(asm, def.IconPath);
                var prefab = TryLoadPrefab(asm, def.PrefabPath);

                resolved.Add(new ResolvedItem
                {
                    Definition = def,
                    Icon = icon,
                    Prefab = prefab
                });
                MMLog.Write($"[ContentResolver] Resolved item: {def.Id} (assembly: {asm?.GetName().Name ?? "null"})");
            }
            MMLog.Write($"[ContentResolver] ResolveItems complete: {resolved.Count} items");
            return resolved;
        }

        private static Assembly SafeCaller(ItemDefinition def)
        {
            try { return Assembly.GetCallingAssembly(); } catch { return null; }
        }

        private static Sprite TryLoadSprite(Assembly asm, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var sprite = AssetLoader.LoadSprite(asm, path);
            if (sprite == null)
            {
                MMLog.WarnOnce("ContentResolver.Icon", $"Failed to load icon at '{path}'");
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
                    MMLog.WarnOnce("ContentResolver.Bundle", $"Failed to load bundle '{bundlePath}'");
                    return null;
                }
                var prefab = AssetLoader.LoadPrefabFromBundle(bundle, assetName);
                if (prefab == null)
                {
                    MMLog.WarnOnce("ContentResolver.Prefab", $"Failed to load prefab '{assetName}' from bundle '{bundlePath}'");
                }
                return prefab;
            }

            // Direct prefab path (Resources-like) is not supported here; would require AssetDatabase/Resources.
            MMLog.WarnOnce("ContentResolver.PrefabPath", $"Prefab path '{path}' not recognized. Use 'Assets/Bundles/xxx.bundle|PrefabName'.");
            return null;
        }
    }

    /// <summary>
    /// Item definition paired with loaded assets.
    /// </summary>
    public class ResolvedItem
    {
        public ItemDefinition Definition;
        public Sprite Icon;
        public GameObject Prefab;
    }
}
