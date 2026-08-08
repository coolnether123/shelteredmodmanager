using System;
using System.Globalization;
using System.IO;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Infrastructure.Assets;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioPngImportService
    {
        private const string ImportFolderName = "Imports";
        private const string UserSpriteFolderName = "UserSprites";

        internal sealed class ImportedSpriteAsset
        {
            public string SpriteId;
            public string RelativePath;
            public string SourceFileName;
            public Texture2D Texture;
            public Sprite Sprite;
        }

        public static string GetImportFolderPath(string scenarioFilePath)
        {
            return GetImportFolderPath(scenarioFilePath, false);
        }

        public static string GetImportFolderPath(string scenarioFilePath, bool create)
        {
            string packRoot = GetPackRoot(scenarioFilePath);
            if (string.IsNullOrEmpty(packRoot))
                return null;

            string path = Path.Combine(packRoot, ImportFolderName);
            if (create && !Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }

        public bool TryImportLatestSpriteReplacement(
            ScenarioDefinition definition,
            string scenarioFilePath,
            string targetLabel,
            Sprite referenceSprite,
            out ImportedSpriteAsset imported,
            out string message)
        {
            imported = null;
            message = null;
            if (referenceSprite == null)
            {
                message = "No reference sprite is available for PNG import.";
                return false;
            }

            Rect rect = referenceSprite.rect;
            int expectedWidth = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int expectedHeight = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            return TryImportLatest(
                definition,
                scenarioFilePath,
                targetLabel,
                expectedWidth,
                expectedHeight,
                referenceSprite,
                out imported,
                out message);
        }

        public bool TryImportLatestCharacterTexture(
            ScenarioDefinition definition,
            string scenarioFilePath,
            string targetLabel,
            ScenarioEditorCharacterTexturePart part,
            Texture2D referenceTexture,
            out ImportedSpriteAsset imported,
            out string message)
        {
            imported = null;
            message = null;
            if (referenceTexture == null)
            {
                message = "No reference character texture is available for PNG import.";
                return false;
            }

            string label = (string.IsNullOrEmpty(targetLabel) ? "character" : targetLabel)
                + "_" + ScenarioEditorCharacterAppearanceService.BuildPartLabel(part);
            return TryImportLatest(
                definition,
                scenarioFilePath,
                label,
                referenceTexture.width,
                referenceTexture.height,
                null,
                out imported,
                out message);
        }

        private static bool TryImportLatest(
            ScenarioDefinition definition,
            string scenarioFilePath,
            string targetLabel,
            int expectedWidth,
            int expectedHeight,
            Sprite referenceSprite,
            out ImportedSpriteAsset imported,
            out string message)
        {
            imported = null;
            message = null;
            if (definition == null)
            {
                message = "No scenario definition is available for PNG import.";
                return false;
            }

            string packRoot = GetPackRoot(scenarioFilePath);
            if (string.IsNullOrEmpty(packRoot))
            {
                message = "Scenario pack path is unavailable, so PNG import cannot run.";
                return false;
            }

            string importFolder = GetImportFolderPath(scenarioFilePath, true);
            string[] files = Directory.GetFiles(importFolder, "*.png", SearchOption.TopDirectoryOnly);
            if (files == null || files.Length == 0)
            {
                message = "No PNG files were found in the scenario import folder: " + importFolder;
                return false;
            }

            Array.Sort(files, CompareByLastWriteTimeDescending);
            string firstMismatch = null;
            for (int i = 0; i < files.Length; i++)
            {
                Texture2D texture;
                string loadError;
                if (!TryLoadPng(files[i], out texture, out loadError))
                {
                    if (firstMismatch == null)
                        firstMismatch = Path.GetFileName(files[i]) + " could not be loaded: " + loadError;
                    continue;
                }

                if (texture.width != expectedWidth || texture.height != expectedHeight)
                {
                    if (firstMismatch == null)
                    {
                        firstMismatch = Path.GetFileName(files[i]) + " is " + texture.width.ToString(CultureInfo.InvariantCulture)
                            + "x" + texture.height.ToString(CultureInfo.InvariantCulture);
                    }
                    continue;
                }

                imported = CopyIntoScenarioPack(
                    definition,
                    packRoot,
                    files[i],
                    targetLabel,
                    texture,
                    referenceSprite);
                message = "Imported user-owned PNG '" + imported.SourceFileName + "' as '" + imported.RelativePath + "'.";
                return true;
            }

            message = "No compatible PNG was found in " + importFolder
                + ". Expected " + expectedWidth.ToString(CultureInfo.InvariantCulture)
                + "x" + expectedHeight.ToString(CultureInfo.InvariantCulture)
                + (string.IsNullOrEmpty(firstMismatch) ? "." : ". Newest mismatch: " + firstMismatch + ".");
            return false;
        }

        private static ImportedSpriteAsset CopyIntoScenarioPack(
            ScenarioDefinition definition,
            string packRoot,
            string sourcePath,
            string targetLabel,
            Texture2D texture,
            Sprite referenceSprite)
        {
            if (definition.AssetReferences == null)
                definition.AssetReferences = new AssetReferencesDefinition();

            string safeName = SanitizeName(targetLabel);
            if (string.IsNullOrEmpty(safeName))
                safeName = "sprite";

            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            string fileName = safeName + "_" + timestamp + ".png";
            string relativePath = "Assets/" + UserSpriteFolderName + "/" + fileName;
            string outputDirectory = Path.Combine(Path.Combine(packRoot, "Assets"), UserSpriteFolderName);
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            string outputPath = Path.GetFullPath(Path.Combine(outputDirectory, fileName));
            EnsureInsidePack(packRoot, outputPath);
            File.Copy(sourcePath, outputPath, false);

            string spriteId = "user_sprite_" + safeName + "_" + timestamp;
            UpsertUserOwnedSprite(definition, spriteId, relativePath);

            Sprite sprite = CreateSprite(texture, referenceSprite, spriteId);
            return new ImportedSpriteAsset
            {
                SpriteId = spriteId,
                RelativePath = relativePath,
                SourceFileName = Path.GetFileName(sourcePath),
                Texture = texture,
                Sprite = sprite
            };
        }

        private static void UpsertUserOwnedSprite(ScenarioDefinition definition, string spriteId, string relativePath)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(spriteId))
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                {
                    sprite.RelativePath = relativePath;
                    sprite.PatchId = null;
                    sprite.UserOwned = true;
                    return;
                }
            }

            definition.AssetReferences.CustomSprites.Add(new SpriteRef
            {
                Id = spriteId,
                RelativePath = relativePath,
                UserOwned = true
            });
        }

        private static bool TryLoadPng(string path, out Texture2D texture, out string message)
        {
            texture = null;
            message = null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                Texture2D loaded = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                loaded.filterMode = FilterMode.Point;
                loaded.wrapMode = TextureWrapMode.Clamp;
                if (!loaded.LoadImage(data))
                {
                    message = "Unity rejected the PNG data.";
                    return false;
                }

                loaded.name = Path.GetFileNameWithoutExtension(path);
                texture = loaded;
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static Sprite CreateSprite(Texture2D texture, Sprite referenceSprite, string spriteId)
        {
            if (texture == null)
                return null;

            Vector2 pivot = new Vector2(0.5f, 0.5f);
            float pixelsPerUnit = 100f;
            if (referenceSprite != null)
            {
                Rect rect = referenceSprite.rect;
                if (rect.width > 0f && rect.height > 0f)
                    pivot = new Vector2(referenceSprite.pivot.x / rect.width, referenceSprite.pivot.y / rect.height);
                if (referenceSprite.pixelsPerUnit > 0f)
                    pixelsPerUnit = referenceSprite.pixelsPerUnit;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, pixelsPerUnit);
            if (sprite != null)
                sprite.name = spriteId;
            return sprite;
        }

        private static int CompareByLastWriteTimeDescending(string left, string right)
        {
            DateTime leftTime = GetWriteTime(left);
            DateTime rightTime = GetWriteTime(right);
            int time = rightTime.CompareTo(leftTime);
            return time != 0 ? time : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime GetWriteTime(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch { return DateTime.MinValue; }
        }

        private static string GetPackRoot(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return null;

            try
            {
                return Path.GetFullPath(Path.GetDirectoryName(scenarioFilePath));
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureInsidePack(string packRoot, string path)
        {
            string fullRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(packRoot));
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Imported asset path escaped the scenario pack.");
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private static string SanitizeName(string value)
        {
            string raw = string.IsNullOrEmpty(value) ? "sprite" : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            char[] buffer = new char[Math.Min(raw.Length, 48)];
            int count = 0;
            for (int i = 0; i < raw.Length && count < buffer.Length; i++)
            {
                char c = raw[i];
                bool ok = char.IsLetterOrDigit(c) || c == '_' || c == '-';
                if (!ok)
                {
                    for (int j = 0; invalid != null && j < invalid.Length; j++)
                    {
                        if (c == invalid[j])
                        {
                            ok = false;
                            break;
                        }
                    }
                }

                buffer[count++] = ok ? c : '_';
            }

            return new string(buffer, 0, count).Trim('_').ToLowerInvariant();
        }
    }
}
