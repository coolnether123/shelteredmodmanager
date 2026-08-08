using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;

namespace ShelteredScenarioEditor.Application.Assets
{
    internal enum ScenarioAssetInventoryState
    {
        Available,
        Missing,
        Orphan
    }

    internal enum ScenarioAssetInventorySource
    {
        VanillaReplacement,
        Imported,
        PixelEdited
    }

    internal sealed class ScenarioAssetInventoryReference
    {
        public string Label;
        public string NavigationToken;
    }

    internal sealed class ScenarioAssetInventoryItem
    {
        public ScenarioAssetInventoryItem()
        {
            References = new List<ScenarioAssetInventoryReference>();
        }

        public string RelativePath;
        public string AbsolutePath;
        public string FileName;
        public int Width;
        public int Height;
        public long Size;
        public ScenarioAssetInventoryState State;
        public ScenarioAssetInventorySource Source;
        public List<ScenarioAssetInventoryReference> References;
        public string Credit;
        public Sprite Thumbnail;

        public bool IsLarge
        {
            get { return Width > ScenarioAssetInventoryService.LargeTextureDimension || Height > ScenarioAssetInventoryService.LargeTextureDimension || Size > ScenarioAssetInventoryService.LargeFileBytes; }
        }
    }

    internal sealed class ScenarioAssetInventory
    {
        public ScenarioAssetInventory()
        {
            Items = new List<ScenarioAssetInventoryItem>();
        }

        public List<ScenarioAssetInventoryItem> Items;
        public long TotalPayloadSize;
        public bool PayloadWarning { get { return TotalPayloadSize > ScenarioAssetInventoryService.PayloadWarningBytes; } }
    }

    internal sealed class ScenarioAssetInventoryService
    {
        internal const long PayloadWarningBytes = 25L * 1024L * 1024L;
        internal const long LargeFileBytes = 2L * 1024L * 1024L;
        internal const int LargeTextureDimension = 2048;

        private readonly Dictionary<string, ThumbnailCacheEntry> _thumbnails = new Dictionary<string, ThumbnailCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public ScenarioAssetInventory Build(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioAssetInventory inventory = new ScenarioAssetInventory();
            string packRoot = GetPackRoot(scenarioFilePath);
            List<string> exportedPaths = ScenarioPackagePlanner.CollectAssetPaths(definition);
            Dictionary<string, ScenarioAssetInventoryItem> byPath = new Dictionary<string, ScenarioAssetInventoryItem>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < exportedPaths.Count; i++)
            {
                string relativePath = NormalizeRelativePath(exportedPaths[i]);
                ScenarioAssetInventoryItem item = CreateReferencedItem(definition, packRoot, relativePath);
                byPath[relativePath] = item;
                inventory.Items.Add(item);
                inventory.TotalPayloadSize += item.State == ScenarioAssetInventoryState.Available ? item.Size : 0L;
            }

            AddOrphans(definition, packRoot, byPath, inventory);
            inventory.Items.Sort(CompareItems);
            return inventory;
        }

        private ScenarioAssetInventoryItem CreateReferencedItem(ScenarioDefinition definition, string packRoot, string relativePath)
        {
            string absolutePath = ResolvePackPath(packRoot, relativePath);
            bool exists = !string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath);
            ScenarioAssetInventoryItem item = new ScenarioAssetInventoryItem
            {
                RelativePath = relativePath,
                AbsolutePath = absolutePath,
                FileName = Path.GetFileName(relativePath),
                State = exists ? ScenarioAssetInventoryState.Available : ScenarioAssetInventoryState.Missing,
                Source = ClassifySource(definition, relativePath),
                Credit = FindCredit(definition, relativePath)
            };
            AddReferences(definition, relativePath, item.References);
            if (exists)
                PopulateFileFacts(item);
            return item;
        }

        private void AddOrphans(
            ScenarioDefinition definition,
            string packRoot,
            Dictionary<string, ScenarioAssetInventoryItem> referenced,
            ScenarioAssetInventory inventory)
        {
            if (string.IsNullOrEmpty(packRoot))
                return;
            string assetsRoot = Path.Combine(packRoot, "Assets");
            if (!Directory.Exists(assetsRoot))
                return;

            string[] files = Directory.GetFiles(assetsRoot, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relativePath = NormalizeRelativePath(files[i].Substring(packRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1));
                if (referenced.ContainsKey(relativePath))
                    continue;

                ScenarioAssetInventoryItem item = new ScenarioAssetInventoryItem
                {
                    RelativePath = relativePath,
                    AbsolutePath = files[i],
                    FileName = Path.GetFileName(files[i]),
                    State = ScenarioAssetInventoryState.Orphan,
                    Source = ClassifySource(definition, relativePath),
                    Credit = FindCredit(definition, relativePath)
                };
                PopulateFileFacts(item);
                inventory.Items.Add(item);
            }
        }

        private void PopulateFileFacts(ScenarioAssetInventoryItem item)
        {
            try { item.Size = new FileInfo(item.AbsolutePath).Length; }
            catch { item.Size = 0L; }

            int width;
            int height;
            if (TryReadPngDimensions(item.AbsolutePath, out width, out height))
            {
                item.Width = width;
                item.Height = height;
                if (!item.IsLarge)
                    item.Thumbnail = GetThumbnail(item.AbsolutePath);
            }
        }

        private Sprite GetThumbnail(string path)
        {
            try
            {
                DateTime writeTime = File.GetLastWriteTimeUtc(path);
                ThumbnailCacheEntry cached;
                if (_thumbnails.TryGetValue(path, out cached) && cached.WriteTimeUtc == writeTime)
                    return cached.Sprite;

                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                if (!texture.LoadImage(bytes))
                    return null;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                _thumbnails[path] = new ThumbnailCacheEntry { WriteTimeUtc = writeTime, Sprite = sprite };
                return sprite;
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryReadPngDimensions(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            try
            {
                byte[] header = new byte[24];
                using (FileStream stream = File.OpenRead(path))
                {
                    int offset = 0;
                    while (offset < header.Length)
                    {
                        int read = stream.Read(header, offset, header.Length - offset);
                        if (read <= 0) return false;
                        offset += read;
                    }
                }
                if (header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71)
                    return false;
                width = ReadBigEndianInt32(header, 16);
                height = ReadBigEndianInt32(header, 20);
                return width > 0 && height > 0;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadBigEndianInt32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static void AddReferences(ScenarioDefinition definition, string path, List<ScenarioAssetInventoryReference> references)
        {
            if (definition == null)
                return;
            AssetReferencesDefinition assets = definition.AssetReferences;
            if (assets != null)
            {
                for (int i = 0; assets.SpriteSwaps != null && i < assets.SpriteSwaps.Count; i++)
                {
                    SpriteSwapRule swap = assets.SpriteSwaps[i];
                    if (swap != null && PathEquals(path, ResolveSpritePath(assets, swap.SpriteId, swap.RelativePath)))
                        AddReference(references, "Sprite replacement for " + SafeLabel(swap.TargetPath, swap.Id), null);
                }
                for (int i = 0; assets.SceneSpritePlacements != null && i < assets.SceneSpritePlacements.Count; i++)
                {
                    SceneSpritePlacement placement = assets.SceneSpritePlacements[i];
                    if (placement != null && PathEquals(path, ResolveSpritePath(assets, placement.SpriteId, placement.RelativePath)))
                        AddReference(references, "Scene art placement " + SafeLabel(placement.Id, placement.ScenarioObjectId), "placement:" + (placement.Id ?? string.Empty));
                }
                for (int i = 0; assets.CustomIcons != null && i < assets.CustomIcons.Count; i++)
                {
                    IconRef icon = assets.CustomIcons[i];
                    if (icon != null && PathEquals(path, icon.RelativePath))
                        AddReference(references, "Custom icon " + SafeLabel(icon.Id, null), null);
                }
                for (int i = 0; assets.SpritePatches != null && i < assets.SpritePatches.Count; i++)
                {
                    SpritePatchDefinition patch = assets.SpritePatches[i];
                    if (patch != null && PathEquals(path, patch.BaseRelativePath))
                        AddReference(references, "Pixel edit " + SafeLabel(patch.DisplayName, patch.Id), null);
                }
                for (int i = 0; assets.CustomSprites != null && i < assets.CustomSprites.Count; i++)
                {
                    SpriteRef sprite = assets.CustomSprites[i];
                    if (sprite != null && PathEquals(path, sprite.RelativePath) && !IsSpriteUsed(assets, sprite.Id))
                        AddReference(references, "Custom sprite " + SafeLabel(sprite.Id, null), null);
                }
            }
            AddCharacterReferences(definition, path, references);
        }

        private static void AddCharacterReferences(ScenarioDefinition definition, string path, List<ScenarioAssetInventoryReference> references)
        {
            List<FamilyMemberConfig> members = definition != null && definition.FamilySetup != null ? definition.FamilySetup.Members : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMemberConfig member = members[i];
                FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
                if (appearance == null)
                    continue;
                string name = SafeLabel(member.Name, "survivor " + (i + 1).ToString(CultureInfo.InvariantCulture));
                if (PathEquals(path, ResolveSpritePath(definition.AssetReferences, appearance.HeadTextureId, appearance.HeadTexturePath))) AddReference(references, name + " head texture", "character:" + i.ToString(CultureInfo.InvariantCulture));
                if (PathEquals(path, ResolveSpritePath(definition.AssetReferences, appearance.TorsoTextureId, appearance.TorsoTexturePath))) AddReference(references, name + " torso texture", "character:" + i.ToString(CultureInfo.InvariantCulture));
                if (PathEquals(path, ResolveSpritePath(definition.AssetReferences, appearance.LegTextureId, appearance.LegTexturePath))) AddReference(references, name + " leg texture", "character:" + i.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string ResolveSpritePath(AssetReferencesDefinition assets, string spriteId, string directPath)
        {
            if (!string.IsNullOrEmpty(directPath) || assets == null || string.IsNullOrEmpty(spriteId))
                return directPath;
            for (int i = 0; assets.CustomSprites != null && i < assets.CustomSprites.Count; i++)
            {
                SpriteRef sprite = assets.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                    return sprite.RelativePath;
            }
            return directPath;
        }

        private static bool IsSpriteUsed(AssetReferencesDefinition assets, string spriteId)
        {
            if (assets == null || string.IsNullOrEmpty(spriteId)) return false;
            for (int i = 0; assets.SpriteSwaps != null && i < assets.SpriteSwaps.Count; i++) if (assets.SpriteSwaps[i] != null && string.Equals(assets.SpriteSwaps[i].SpriteId, spriteId, StringComparison.OrdinalIgnoreCase)) return true;
            for (int i = 0; assets.SceneSpritePlacements != null && i < assets.SceneSpritePlacements.Count; i++) if (assets.SceneSpritePlacements[i] != null && string.Equals(assets.SceneSpritePlacements[i].SpriteId, spriteId, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void AddReference(List<ScenarioAssetInventoryReference> references, string label, string navigationToken)
        {
            for (int i = 0; i < references.Count; i++) if (string.Equals(references[i].Label, label, StringComparison.Ordinal)) return;
            references.Add(new ScenarioAssetInventoryReference { Label = label, NavigationToken = navigationToken });
        }

        private static ScenarioAssetInventorySource ClassifySource(ScenarioDefinition definition, string path)
        {
            AssetReferencesDefinition assets = definition != null ? definition.AssetReferences : null;
            for (int i = 0; assets != null && assets.SpritePatches != null && i < assets.SpritePatches.Count; i++)
                if (assets.SpritePatches[i] != null && PathEquals(path, assets.SpritePatches[i].BaseRelativePath)) return ScenarioAssetInventorySource.PixelEdited;
            for (int i = 0; assets != null && assets.CustomSprites != null && i < assets.CustomSprites.Count; i++)
                if (assets.CustomSprites[i] != null && PathEquals(path, assets.CustomSprites[i].RelativePath) && assets.CustomSprites[i].UserOwned) return ScenarioAssetInventorySource.Imported;
            for (int i = 0; assets != null && assets.SpriteSwaps != null && i < assets.SpriteSwaps.Count; i++)
                if (assets.SpriteSwaps[i] != null && PathEquals(path, assets.SpriteSwaps[i].RelativePath)) return ScenarioAssetInventorySource.VanillaReplacement;
            return ScenarioAssetInventorySource.Imported;
        }

        private static string FindCredit(ScenarioDefinition definition, string path)
        {
            List<ScenarioAssetCreditDefinition> credits = definition != null && definition.AssetReferences != null ? definition.AssetReferences.AssetCredits : null;
            for (int i = 0; credits != null && i < credits.Count; i++)
                if (credits[i] != null && PathEquals(path, credits[i].RelativePath)) return credits[i].Credit;
            return null;
        }

        internal static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        }

        internal static bool PathEquals(string left, string right)
        {
            return string.Equals(NormalizeRelativePath(left), NormalizeRelativePath(right), StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetPackRoot(string scenarioFilePath)
        {
            try { return !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetFullPath(Path.GetDirectoryName(scenarioFilePath)) : null; }
            catch { return null; }
        }

        internal static string ResolvePackPath(string packRoot, string relativePath)
        {
            return ScenarioPackagePlan.ResolveContainedPath(packRoot, relativePath);
        }

        private static string SafeLabel(string preferred, string fallback)
        {
            return !string.IsNullOrEmpty(preferred) ? "'" + preferred + "'" : (!string.IsNullOrEmpty(fallback) ? "'" + fallback + "'" : "(unnamed)");
        }

        private static int CompareItems(ScenarioAssetInventoryItem left, ScenarioAssetInventoryItem right)
        {
            int state = left.State.CompareTo(right.State);
            return state != 0 ? state : string.Compare(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ThumbnailCacheEntry
        {
            public DateTime WriteTimeUtc;
            public Sprite Sprite;
        }
    }
}
