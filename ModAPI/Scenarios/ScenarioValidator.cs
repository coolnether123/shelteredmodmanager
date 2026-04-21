using System;
using System.IO;
using ModAPI.Core;

namespace ModAPI.Scenarios
{
    public interface IScenarioDependencyResolver
    {
        bool IsLoaded(string modId);
    }

    public interface IScenarioDependencyVersionResolver : IScenarioDependencyResolver
    {
        string GetLoadedVersion(string modId);
    }

    public sealed class ModRegistryScenarioDependencyResolver : IScenarioDependencyVersionResolver
    {
        public bool IsLoaded(string modId)
        {
            return !string.IsNullOrEmpty(modId) && ModRegistry.GetMod(modId) != null;
        }

        public string GetLoadedVersion(string modId)
        {
            ModEntry loaded = !string.IsNullOrEmpty(modId) ? ModRegistry.GetMod(modId) : null;
            return loaded != null ? loaded.Version : null;
        }
    }

    public sealed class ScenarioValidator
    {
        private readonly IScenarioDependencyResolver _dependencyResolver;

        public ScenarioValidator()
            : this(new ModRegistryScenarioDependencyResolver())
        {
        }

        public ScenarioValidator(IScenarioDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
        }

        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            if (definition == null)
            {
                result.AddError("Scenario definition is null.");
                return result;
            }

            if (TrimToNull(definition.Id) == null)
                result.AddError("Scenario Id is required.");
            if (TrimToNull(definition.DisplayName) == null)
                result.AddError("Scenario DisplayName is required.");
            if (!Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                result.AddError("Scenario BaseMode is invalid: " + definition.BaseGameMode);

            ValidateDependencies(definition, result);
            ValidateAssets(definition, scenarioFilePath, result);
            ValidateInventory(definition, result);
            ValidateBunker(definition, result);
            return result;
        }

        private void ValidateDependencies(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition.Dependencies == null || _dependencyResolver == null)
                return;

            for (int i = 0; i < definition.Dependencies.Count; i++)
            {
                ModAPI.Saves.LoadedModInfo dependency = ScenarioDependencyManifest.ParseDependency(definition.Dependencies[i]);
                if (dependency == null)
                    continue;

                if (!_dependencyResolver.IsLoaded(dependency.modId))
                {
                    result.AddError("Required dependency mod is not loaded: " + dependency.modId);
                    continue;
                }

                if (!string.IsNullOrEmpty(dependency.version))
                {
                    string activeVersion = GetLoadedVersion(dependency.modId);
                    if (!string.Equals(activeVersion ?? string.Empty, dependency.version, StringComparison.OrdinalIgnoreCase))
                        result.AddError("Required dependency mod version mismatch: " + dependency.modId
                            + " requires " + dependency.version + " but active version is " + (activeVersion ?? "<unknown>") + ".");
                }
            }
        }

        private string GetLoadedVersion(string modId)
        {
            IScenarioDependencyVersionResolver versionResolver = _dependencyResolver as IScenarioDependencyVersionResolver;
            if (versionResolver != null)
                return versionResolver.GetLoadedVersion(modId);

            ModEntry loaded = !string.IsNullOrEmpty(modId) ? ModRegistry.GetMod(modId) : null;
            return loaded != null ? loaded.Version : null;
        }

        private static void ValidateAssets(ScenarioDefinition definition, string scenarioFilePath, ScenarioValidationResult result)
        {
            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            if (string.IsNullOrEmpty(packRoot) || definition.AssetReferences == null)
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                ValidateAssetPath(packRoot, sprite != null ? sprite.RelativePath : null, "sprite", result);
            }

            for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
            {
                IconRef icon = definition.AssetReferences.CustomIcons[i];
                ValidateAssetPath(packRoot, icon != null ? icon.RelativePath : null, "icon", result);
            }

            ValidateSpriteSwaps(definition.AssetReferences, packRoot, result);
        }

        private static void ValidateInventory(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition.StartingInventory == null || definition.StartingInventory.Items == null)
                return;

            for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry item = definition.StartingInventory.Items[i];
                if (item == null || TrimToNull(item.ItemId) == null)
                    result.AddError("Starting inventory item #" + i + " is missing itemId.");
                else if (item.Quantity <= 0)
                    result.AddError("Starting inventory item '" + item.ItemId + "' must have quantity greater than zero.");
            }
        }

        private static void ValidateBunker(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition.BunkerEdits == null || definition.BunkerEdits.RoomChanges == null)
                return;

            for (int i = 0; i < definition.BunkerEdits.RoomChanges.Count; i++)
            {
                RoomEdit room = definition.BunkerEdits.RoomChanges[i];
                if (room == null)
                    continue;
                if (room.GridX < 0 || room.GridY < 0)
                    result.AddError("Room edit #" + i + " has negative grid coordinates.");
                if (room.WallSpriteIndex.HasValue && room.WallSpriteIndex.Value < 0)
                    result.AddError("Room edit #" + i + " has negative wallSpriteIndex.");
                if (room.WireSpriteIndex.HasValue && room.WireSpriteIndex.Value < 0)
                    result.AddError("Room edit #" + i + " has negative wireSpriteIndex.");
            }
        }

        private static void ValidateSpriteSwaps(AssetReferencesDefinition assets, string packRoot, ScenarioValidationResult result)
        {
            if (assets == null || assets.SpriteSwaps == null)
                return;

            for (int i = 0; i < assets.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule swap = assets.SpriteSwaps[i];
                if (swap == null)
                {
                    result.AddError("Sprite swap #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(swap.TargetPath) == null)
                    result.AddError("Sprite swap #" + i + " is missing targetPath.");

                bool hasSpriteId = TrimToNull(swap.SpriteId) != null;
                bool hasRelativePath = TrimToNull(swap.RelativePath) != null;
                if (!hasSpriteId && !hasRelativePath)
                    result.AddError("Sprite swap #" + i + " must specify spriteId or path.");

                if (swap.Day.HasValue && swap.Day.Value < 1)
                    result.AddError("Sprite swap #" + i + " has day less than 1.");

                if (!Enum.IsDefined(typeof(ScenarioSpriteTargetComponentKind), swap.TargetComponent))
                    result.AddError("Sprite swap #" + i + " has invalid targetComponent '" + swap.TargetComponent + "'.");

                if (hasSpriteId && !HasSpriteReference(assets, swap.SpriteId))
                    result.AddError("Sprite swap #" + i + " references unknown spriteId '" + swap.SpriteId + "'.");

                if (hasRelativePath)
                    ValidateAssetPath(packRoot, swap.RelativePath, "sprite swap", result);
            }
        }

        private static bool HasSpriteReference(AssetReferencesDefinition assets, string spriteId)
        {
            if (assets == null || assets.CustomSprites == null || string.IsNullOrEmpty(spriteId))
                return false;

            for (int i = 0; i < assets.CustomSprites.Count; i++)
            {
                SpriteRef sprite = assets.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void ValidateAssetPath(string packRoot, string relativePath, string assetKind, ScenarioValidationResult result)
        {
            string trimmed = TrimToNull(relativePath);
            if (trimmed == null)
            {
                result.AddError("Scenario " + assetKind + " reference is missing a relative path.");
                return;
            }

            string fullPath = Path.GetFullPath(Path.Combine(packRoot, trimmed));
            string fullRoot = EnsureTrailingDirectorySeparator(Path.GetFullPath(packRoot));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("Scenario " + assetKind + " path escapes the scenario pack folder: " + trimmed);
                return;
            }

            if (!File.Exists(fullPath))
                result.AddError("Scenario " + assetKind + " file does not exist: " + trimmed);
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
