using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Diagnostics{
    internal sealed class ScenarioValidatorImpl
    {
        private readonly ScenarioValidator _neutralValidator;

        public ScenarioValidatorImpl()
            : this(new ScenarioValidator())
        {
        }

        public ScenarioValidatorImpl(ScenarioValidator neutralValidator)
        {
            _neutralValidator = neutralValidator ?? new ScenarioValidator();
        }

        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            ScenarioValidationResult result = _neutralValidator.Validate(definition, scenarioFilePath);
            if (result == null)
                result = new ScenarioValidationResult();
            ValidateSeed(definition, result);
            ValidateLoadableAssets(definition, scenarioFilePath, result);
            ValidateBunkerAuthoringPlacements(definition, result);
            return result;
        }

        private static void ValidateSeed(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || result == null || !definition.SeedOverride.HasValue)
                return;

            long seed = definition.SeedOverride.Value;
            if (seed < int.MinValue || seed > int.MaxValue)
                result.AddError("Fixed scenario seed must fit in a signed 32-bit integer.");
        }

        private static void ValidateLoadableAssets(ScenarioDefinition definition, string scenarioFilePath, ScenarioValidationResult result)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(scenarioFilePath))
                return;

            string packRoot = Path.GetDirectoryName(scenarioFilePath);
            if (string.IsNullOrEmpty(packRoot))
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                ValidateSprite(packRoot, sprite != null ? sprite.RelativePath : null, "sprite", result);
            }

            for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
            {
                IconRef icon = definition.AssetReferences.CustomIcons[i];
                ValidateSprite(packRoot, icon != null ? icon.RelativePath : null, "icon", result);
            }

            for (int i = 0; i < definition.AssetReferences.SpriteSwaps.Count; i++)
            {
                SpriteSwapRule swap = definition.AssetReferences.SpriteSwaps[i];
                if (swap != null && !string.IsNullOrEmpty(swap.RelativePath))
                    ValidateSprite(packRoot, swap.RelativePath, "sprite swap", result);
            }

            if (definition.FamilySetup != null && definition.FamilySetup.Members != null)
            {
                for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
                {
                    FamilyMemberConfig member = definition.FamilySetup.Members[i];
                    FamilyMemberAppearanceConfig appearance = member != null ? member.Appearance : null;
                    if (appearance == null)
                        continue;

                    if (!string.IsNullOrEmpty(appearance.HeadTexturePath))
                        ValidateSprite(packRoot, appearance.HeadTexturePath, "family head texture", result);
                    if (!string.IsNullOrEmpty(appearance.TorsoTexturePath))
                        ValidateSprite(packRoot, appearance.TorsoTexturePath, "family torso texture", result);
                    if (!string.IsNullOrEmpty(appearance.LegTexturePath))
                        ValidateSprite(packRoot, appearance.LegTexturePath, "family leg texture", result);
                }
            }
        }

        private static void ValidateSprite(string packRoot, string relativePath, string kind, ScenarioValidationResult result)
        {
            if (string.IsNullOrEmpty(relativePath))
                return;

            try
            {
                if (AssetLoader.LoadSprite(packRoot, relativePath, 100f) == null)
                    result.AddError("Scenario " + kind + " could not be loaded as a sprite through AssetLoader: " + relativePath);
            }
            catch (Exception ex)
            {
                result.AddError("Scenario " + kind + " could not be loaded through AssetLoader: " + relativePath + " (" + ex.Message + ")");
            }
        }

        private static void ValidateBunkerAuthoringPlacements(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.ObjectPlacements == null || result == null)
                return;

            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;

                ScenarioPlacementDefinitionKind kind;
                if (ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind))
                {
                    ValidateSpecialPlacement(placement, i, kind, result);
                    continue;
                }

                if (!string.IsNullOrEmpty(placement.PrefabReference))
                {
                    result.AddWarning("Object placement #" + i.ToString(CultureInfo.InvariantCulture)
                        + " uses PrefabReference and will be skipped by the safe pre-alpha bunker apply path.");
                }

                if (string.IsNullOrEmpty(placement.DefinitionReference))
                    continue;

                ObjectManager.ObjectType objectType;
                if (!TryParseObjectType(placement.DefinitionReference, out objectType))
                {
                    result.AddError("Object placement #" + i.ToString(CultureInfo.InvariantCulture)
                        + " has unknown DefinitionReference '" + placement.DefinitionReference + "'.");
                }
            }
        }

        private static void ValidateSpecialPlacement(
            ObjectPlacement placement,
            int index,
            ScenarioPlacementDefinitionKind kind,
            ScenarioValidationResult result)
        {
            bool hasGridX = HasProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridX);
            bool hasGridY = HasProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridY);
            if (hasGridX != hasGridY)
            {
                result.AddError("Special bunker placement #" + index.ToString(CultureInfo.InvariantCulture)
                    + " must include both gridX and gridY when either coordinate is present.");
            }

            if (kind == ScenarioPlacementDefinitionKind.Ladder
                && !HasProperty(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyHorizontalPos))
            {
                result.AddWarning("Ladder placement #" + index.ToString(CultureInfo.InvariantCulture)
                    + " is missing horizontalPos; replay will fall back to the stored world position.");
            }
        }

        private static bool TryParseObjectType(string value, out ObjectManager.ObjectType objectType)
        {
            objectType = ObjectManager.ObjectType.Undefined;
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                objectType = (ObjectManager.ObjectType)Enum.Parse(typeof(ObjectManager.ObjectType), value, true);
                return objectType != ObjectManager.ObjectType.Undefined && objectType != ObjectManager.ObjectType.Max;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasProperty(List<ScenarioProperty> properties, string key)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return false;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null
                    && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(property.Value))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
