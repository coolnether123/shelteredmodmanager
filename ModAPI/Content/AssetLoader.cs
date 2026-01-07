using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Content
{
    /// <summary>
    /// Utilities for loading mod-local assets (textures, sprites, asset bundles) with caching.
    /// Paths are resolved relative to the owning mod's root:
    ///   - Icons/Textures:  Assets/Textures/<file>.png
    ///   - Bundles/Prefabs: Assets/Prefabs/<file>.prefab or Assets/Bundles/<bundle>.bundle
    /// </summary>
    public static class AssetLoader
    {
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AssetBundle> _bundleCache = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Loads a Texture2D from a relative path under the mod root (e.g., Assets/Textures/icon.png). Cached per mod.
        /// </summary>
        public static Texture2D LoadTexture(Assembly asm, string relativePath)
        {
            var key = CacheKey(asm, relativePath);
            if (key == null) return null;
            Texture2D cached;
            if (_textureCache.TryGetValue(key, out cached)) return cached;

            var fullPath = ResolvePath(asm, relativePath);
            if (string.IsNullOrEmpty(fullPath))
                return null;

            return LoadTextureFromPath(key, fullPath, false);
        }

        /// <summary>
        /// Explicit mod-root overload. Throws if the root/path are invalid so mod authors see configuration issues early.
        /// </summary>
        public static Texture2D LoadTexture(string modRootPath, string relativePath)
        {
            var fullPath = ResolveExplicitPath(modRootPath, relativePath);
            var key = "root:" + fullPath;
            Texture2D cached;
            if (_textureCache.TryGetValue(key, out cached)) return cached;

            return LoadTextureFromPath(key, fullPath, true);
        }

        /// <summary>
        /// Loads a Sprite from a relative texture path (default PPU 100). Cached per mod.
        /// </summary>
        public static Sprite LoadSprite(Assembly asm, string relativePath, float pixelsPerUnit = 100f)
        {
            var key = CacheKey(asm, "sprite:" + relativePath);
            if (key == null) return null;
            Sprite cached;
            if (_spriteCache.TryGetValue(key, out cached)) return cached;

            var tex = LoadTexture(asm, relativePath);
            if (tex == null) return null;

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            _spriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Explicit mod-root overload for sprites.
        /// </summary>
        public static Sprite LoadSprite(string modRootPath, string relativePath, float pixelsPerUnit = 100f)
        {
            var tex = LoadTexture(modRootPath, relativePath);
            if (tex == null) return null;

            var key = "root:sprite:" + Path.GetFullPath(Path.Combine(modRootPath ?? string.Empty, relativePath ?? string.Empty));
            Sprite cached;
            if (_spriteCache.TryGetValue(key, out cached)) return cached;

            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            _spriteCache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Loads an AssetBundle from a relative path under the mod root (e.g., Assets/Bundles/mybundle.bundle). Cached per mod.
        /// </summary>
        public static AssetBundle LoadBundle(Assembly asm, string relativePath)
        {
            var key = CacheKey(asm, "bundle:" + relativePath);
            if (key == null) return null;
            AssetBundle cached;
            if (_bundleCache.TryGetValue(key, out cached)) return cached;

            var fullPath = ResolvePath(asm, relativePath);
            if (string.IsNullOrEmpty(fullPath))
                return null;

            return LoadBundleFromPath(key, fullPath, false);
        }

        /// <summary>
        /// Explicit mod-root overload for asset bundles.
        /// </summary>
        public static AssetBundle LoadBundle(string modRootPath, string relativePath)
        {
            var fullPath = ResolveExplicitPath(modRootPath, relativePath);
            var key = "root:bundle:" + fullPath;
            AssetBundle cached;
            if (_bundleCache.TryGetValue(key, out cached)) return cached;

            return LoadBundleFromPath(key, fullPath, true);
        }

        /// <summary>
        /// Loads a prefab GameObject from an AssetBundle previously loaded for the mod.
        /// </summary>
        public static GameObject LoadPrefabFromBundle(AssetBundle bundle, string assetPath)
        {
            if (bundle == null || string.IsNullOrEmpty(assetPath)) return null;
            try { return bundle.LoadAsset<GameObject>(assetPath); }
            catch { return null; }
        }

        private static Texture2D LoadTextureFromPath(string cacheKey, string fullPath, bool throwOnMissing)
        {
            if (string.IsNullOrEmpty(fullPath))
                return null;
            if (!File.Exists(fullPath))
            {
                var msg = $"Asset not found at '{fullPath}'";
                if (throwOnMissing) throw new FileNotFoundException(msg, fullPath);
                MMLog.WarnOnce("AssetLoader.Texture.Missing." + cacheKey, msg);
                return null;
            }

            try
            {
                var data = File.ReadAllBytes(fullPath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(data))
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                _textureCache[cacheKey] = tex;
                return tex;
            }
            catch (Exception ex)
            {
                if (throwOnMissing) throw;
                MMLog.WarnOnce("AssetLoader.Texture.Error." + cacheKey, "Failed to load texture: " + ex.Message);
                return null;
            }
        }

        private static AssetBundle LoadBundleFromPath(string cacheKey, string fullPath, bool throwOnMissing)
        {
            if (string.IsNullOrEmpty(fullPath))
                return null;
            if (!File.Exists(fullPath))
            {
                var msg = $"Asset bundle not found at '{fullPath}'";
                if (throwOnMissing) throw new FileNotFoundException(msg, fullPath);
                MMLog.WarnOnce("AssetLoader.Bundle.Missing." + cacheKey, msg);
                return null;
            }

            try
            {
                var bundle = AssetBundle.LoadFromFile(fullPath);
                if (bundle != null)
                {
                    _bundleCache[cacheKey] = bundle;
                }
                return bundle;
            }
            catch (Exception ex)
            {
                if (throwOnMissing) throw;
                MMLog.WarnOnce("AssetLoader.Bundle.Error." + cacheKey, "Failed to load bundle: " + ex.Message);
                return null;
            }
        }

        private static string ResolveExplicitPath(string modRootPath, string relativePath)
        {
            if (string.IsNullOrEmpty(modRootPath))
                throw new InvalidOperationException("Mod root path is not set; cannot resolve asset.");
            if (string.IsNullOrEmpty(relativePath))
                throw new ArgumentNullException(nameof(relativePath));
            return Path.GetFullPath(Path.Combine(modRootPath, relativePath));
        }

        private static string ResolvePath(Assembly asm, string relativePath)
        {
            if (asm == null || string.IsNullOrEmpty(relativePath)) return null;
            Core.ModEntry entry;
            if (ModRegistry.TryGetModByAssembly(asm, out entry) && entry != null && !string.IsNullOrEmpty(entry.RootPath))
            {
                return Path.GetFullPath(Path.Combine(entry.RootPath, relativePath));
            }
            try
            {
                var asmDir = Path.GetDirectoryName(asm.Location);
                var path = Path.GetFullPath(Path.Combine(asmDir ?? string.Empty, relativePath));
                return path;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("AssetLoader.ResolvePath", "Failed to resolve asset path: " + ex.Message);
                return null;
            }
        }

        private static string CacheKey(Assembly asm, string relativePath)
        {
            if (asm == null || string.IsNullOrEmpty(relativePath)) return null;
            Core.ModEntry entry;
            var modId = ModRegistry.TryGetModByAssembly(asm, out entry) && entry != null ? entry.Id ?? entry.RootPath : null;
            var asmName = asm.GetName().Name;
            return (modId ?? asmName) + "|" + relativePath;
        }
    }
}
