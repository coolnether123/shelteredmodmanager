using System;
using System.Collections.Generic;
using System.Globalization;
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
            ValidateFamily(definition, scenarioFilePath, result);
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
            ValidateSceneSpritePlacements(definition.AssetReferences, packRoot, result);
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

        private static void ValidateFamily(ScenarioDefinition definition, string scenarioFilePath, ScenarioValidationResult result)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null)
                return;

            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? Path.GetDirectoryName(scenarioFilePath) : null;
            for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
            {
                FamilyMemberConfig member = definition.FamilySetup.Members[i];
                FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
                if (appearance == null || string.IsNullOrEmpty(packRoot))
                    continue;

                if (TrimToNull(appearance.HeadTexturePath) != null)
                    ValidateAssetPath(packRoot, appearance.HeadTexturePath, "family head texture", result);
                if (TrimToNull(appearance.TorsoTexturePath) != null)
                    ValidateAssetPath(packRoot, appearance.TorsoTexturePath, "family torso texture", result);
                if (TrimToNull(appearance.LegTexturePath) != null)
                    ValidateAssetPath(packRoot, appearance.LegTexturePath, "family leg texture", result);
            }
        }

        private static void ValidateBunker(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.BunkerEdits == null)
                return;

            if (definition.BunkerEdits.RoomChanges != null)
            {
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

            if (definition.BunkerEdits.ObjectPlacements == null)
                return;

            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                {
                    result.AddError("Object placement #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(placement.PrefabReference) == null && TrimToNull(placement.DefinitionReference) == null)
                    result.AddError("Object placement #" + i + " must define prefab or definition.");

                ScenarioPlacementDefinitionKind kind;
                if (ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind))
                    ValidateSpecialPlacement(placement, i, kind, result);
            }
        }

        private static void ValidateSpecialPlacement(
            ObjectPlacement placement,
            int index,
            ScenarioPlacementDefinitionKind kind,
            ScenarioValidationResult result)
        {
            int gridX;
            int gridY;
            bool hasGrid = TryGetGridCoordinates(placement, out gridX, out gridY);
            if (hasGrid && (gridX < 0 || gridY < 0))
                result.AddError("Object placement #" + index + " has negative grid coordinates.");

            switch (kind)
            {
                case ScenarioPlacementDefinitionKind.Room:
                    if (!hasGrid && placement.Position == null)
                        result.AddError("Room placement #" + index + " must include grid coordinates or a position.");
                    break;

                case ScenarioPlacementDefinitionKind.Ladder:
                    if (!hasGrid && placement.Position == null)
                        result.AddError("Ladder placement #" + index + " must include grid coordinates or a position.");

                    float horizontalPos;
                    if (TryGetFloatProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyHorizontalPos, out horizontalPos)
                        && (horizontalPos < 0f || horizontalPos > 1f))
                    {
                        result.AddError("Ladder placement #" + index + " has horizontalPos outside the 0..1 range.");
                    }
                    break;

                case ScenarioPlacementDefinitionKind.RoomLight:
                    if (!hasGrid && placement.Position == null)
                        result.AddError("Room light placement #" + index + " must include grid coordinates or a position.");
                    break;
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
                bool hasRuntimeSpriteKey = TrimToNull(swap.RuntimeSpriteKey) != null;
                if (!hasSpriteId && !hasRelativePath && !hasRuntimeSpriteKey)
                    result.AddError("Sprite swap #" + i + " must specify spriteId, path, or runtimeSpriteKey.");

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

        private static void ValidateSceneSpritePlacements(AssetReferencesDefinition assets, string packRoot, ScenarioValidationResult result)
        {
            if (assets == null || assets.SceneSpritePlacements == null)
                return;

            for (int i = 0; i < assets.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = assets.SceneSpritePlacements[i];
                if (placement == null)
                {
                    result.AddError("Scene sprite placement #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(placement.Id) == null)
                    result.AddError("Scene sprite placement #" + i + " is missing id.");

                bool hasSpriteId = TrimToNull(placement.SpriteId) != null;
                bool hasRelativePath = TrimToNull(placement.RelativePath) != null;
                bool hasRuntimeSpriteKey = TrimToNull(placement.RuntimeSpriteKey) != null;
                if (!hasSpriteId && !hasRelativePath && !hasRuntimeSpriteKey)
                    result.AddError("Scene sprite placement #" + i + " must specify spriteId, path, or runtimeSpriteKey.");

                if (hasSpriteId && !HasSpriteReference(assets, placement.SpriteId))
                    result.AddError("Scene sprite placement #" + i + " references unknown spriteId '" + placement.SpriteId + "'.");

                if (hasRelativePath)
                    ValidateAssetPath(packRoot, placement.RelativePath, "scene sprite placement", result);

                if (placement.SnapToGrid && (!placement.GridX.HasValue || !placement.GridY.HasValue))
                    result.AddError("Scene sprite placement #" + i + " is snapToGrid but missing gridX/gridY.");

                if (placement.GridX.HasValue && placement.GridX.Value < 0)
                    result.AddError("Scene sprite placement #" + i + " has negative gridX.");

                if (placement.GridY.HasValue && placement.GridY.Value < 0)
                    result.AddError("Scene sprite placement #" + i + " has negative gridY.");
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

        private static bool TryGetGridCoordinates(ObjectPlacement placement, out int gridX, out int gridY)
        {
            gridX = -1;
            gridY = -1;
            int parsedGridX;
            int parsedGridY;
            if (!TryGetIntProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridX, out parsedGridX)
                || !TryGetIntProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridY, out parsedGridY))
            {
                return false;
            }

            gridX = parsedGridX;
            gridY = parsedGridY;
            return true;
        }

        private static bool TryGetIntProperty(List<ScenarioProperty> properties, string key, out int value)
        {
            value = 0;
            string propertyValue = GetProperty(properties, key);
            return !string.IsNullOrEmpty(propertyValue) && int.TryParse(propertyValue, out value);
        }

        private static bool TryGetFloatProperty(List<ScenarioProperty> properties, string key, out float value)
        {
            value = 0f;
            string propertyValue = GetProperty(properties, key);
            return !string.IsNullOrEmpty(propertyValue)
                && float.TryParse(propertyValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string GetProperty(List<ScenarioProperty> properties, string key)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return null;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }

            return null;
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
