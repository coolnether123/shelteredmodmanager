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
        private readonly ScenarioValidationPipeline _pipeline;

        public ScenarioValidator()
            : this(new ModRegistryScenarioDependencyResolver())
        {
        }

        public ScenarioValidator(IScenarioDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
            _pipeline = new ScenarioValidationPipeline(new IScenarioValidationRule[]
            {
                new CoreScenarioRule(),
                new DependencyValidationRule(this),
                new AssetValidationRule(),
                new FamilyValidationRule(),
                new InventoryValidationRule(),
                new BunkerValidationRule(),
                new QuestMapValidationRule(),
                new SchedulingValidationRule(),
                new ObjectStartStateValidationRule(),
                new BunkerDependencyValidationRule(),
                new GateConditionValidationRule()
            });
        }

        public ScenarioValidationResult Validate(ScenarioDefinition definition, string scenarioFilePath)
        {
            return _pipeline.ValidateLegacy(definition, scenarioFilePath);
        }

        private void ValidateDependencies(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition.Dependencies == null || _dependencyResolver == null)
            {
                ValidateExplicitModDependencies(definition, result);
                return;
            }

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

            ValidateExplicitModDependencies(definition, result);
            ValidateAutoDetectedModReferences(definition, result);
        }

        private void ValidateExplicitModDependencies(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.ModDependencies == null || _dependencyResolver == null)
                return;

            for (int i = 0; i < definition.ModDependencies.Count; i++)
            {
                ScenarioModDependencyDefinition dependency = definition.ModDependencies[i];
                if (dependency == null || string.IsNullOrEmpty(dependency.ModId))
                {
                    result.AddError("Manual scenario mod dependency #" + i + " is missing mod id.");
                    continue;
                }

                if (dependency.Kind == ScenarioModDependencyKind.Required && !_dependencyResolver.IsLoaded(dependency.ModId))
                {
                    result.AddError("Required scenario mod dependency is not loaded: " + dependency.ModId);
                    continue;
                }

                if (dependency.Kind == ScenarioModDependencyKind.Required && !string.IsNullOrEmpty(dependency.Version))
                {
                    string activeVersion = GetLoadedVersion(dependency.ModId);
                    if (!string.Equals(activeVersion ?? string.Empty, dependency.Version, StringComparison.OrdinalIgnoreCase))
                        result.AddError("Required scenario mod dependency version mismatch: " + dependency.ModId
                            + " requires " + dependency.Version + " but active version is " + (activeVersion ?? "<unknown>") + ".");
                }
            }
        }

        private void ValidateAutoDetectedModReferences(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || _dependencyResolver == null)
                return;

            ValidateContentOwner(definition, result, definition.StartingInventory != null ? definition.StartingInventory.Items : null);
            for (int i = 0; definition.StartingInventory != null && definition.StartingInventory.ScheduledChanges != null && i < definition.StartingInventory.ScheduledChanges.Count; i++)
                ValidateModOwnedContent(result, definition.StartingInventory.ScheduledChanges[i] != null ? definition.StartingInventory.ScheduledChanges[i].ItemId : null, "scheduled inventory");
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                ValidateModOwnedContent(result, placement != null ? placement.PrefabReference : null, "bunker object prefab");
                ValidateModOwnedContent(result, placement != null ? placement.DefinitionReference : null, "bunker object definition");
            }
            for (int i = 0; definition.AssetReferences != null && definition.AssetReferences.CustomSprites != null && i < definition.AssetReferences.CustomSprites.Count; i++)
                ValidateModOwnedContent(result, definition.AssetReferences.CustomSprites[i] != null ? definition.AssetReferences.CustomSprites[i].Id : null, "sprite asset");
            for (int i = 0; definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                    ValidateModOwnedContent(result, action.Effects[e] != null ? action.Effects[e].ItemId : null, "scheduled effect");
            }
        }

        private void ValidateContentOwner(ScenarioDefinition definition, ScenarioValidationResult result, System.Collections.Generic.List<ItemEntry> items)
        {
            for (int i = 0; items != null && i < items.Count; i++)
                ValidateModOwnedContent(result, items[i] != null ? items[i].ItemId : null, "starting inventory");
        }

        private void ValidateModOwnedContent(ScenarioValidationResult result, string contentId, string scope)
        {
            string modId = ExtractOwnerPrefix(contentId);
            if (modId == null)
                return;
            if (!_dependencyResolver.IsLoaded(modId))
                result.AddError("Referenced mod content is unavailable: " + scope + " '" + contentId + "' requires missing mod '" + modId + "'.");
        }

        private static string ExtractOwnerPrefix(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
                return null;
            int separator = contentId.IndexOf(':');
            return separator > 0 ? contentId.Substring(0, separator) : null;
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
                if (sprite == null)
                    continue;

                if (TrimToNull(sprite.RelativePath) != null)
                    ValidateAssetPath(packRoot, sprite.RelativePath, "sprite", result);

                if (TrimToNull(sprite.PatchId) != null && !HasSpritePatch(definition.AssetReferences, sprite.PatchId))
                    result.AddError("Custom sprite '" + (sprite.Id ?? ("#" + i.ToString(CultureInfo.InvariantCulture)))
                        + "' references unknown patchId '" + sprite.PatchId + "'.");
            }

            for (int i = 0; i < definition.AssetReferences.CustomIcons.Count; i++)
            {
                IconRef icon = definition.AssetReferences.CustomIcons[i];
                ValidateAssetPath(packRoot, icon != null ? icon.RelativePath : null, "icon", result);
            }

            ValidateSpritePatches(definition.AssetReferences, packRoot, result);

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

        private static void ValidateQuestsAndMap(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null)
                return;

            Dictionary<string, bool> questIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (definition.Quests != null && definition.Quests.Quests != null)
            {
                for (int i = 0; i < definition.Quests.Quests.Count; i++)
                {
                    QuestDefinition quest = definition.Quests.Quests[i];
                    string id = TrimToNull(quest != null ? quest.Id : null);
                    if (id == null)
                    {
                        result.AddError("Quest #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                        continue;
                    }

                    if (questIds.ContainsKey(id))
                        result.AddError("Duplicate quest id: " + id);
                    else
                        questIds[id] = true;

                    string startTriggerId = TrimToNull(quest.StartTriggerId);
                    if (startTriggerId != null && !HasTrigger(definition.TriggersAndEvents, startTriggerId))
                        result.AddError("Quest '" + id + "' references unknown startTriggerId '" + startTriggerId + "'.");

                    string completionConditionId = TrimToNull(quest.CompletionConditionId);
                    if (completionConditionId != null && !HasCondition(definition.WinLossConditions, completionConditionId))
                        result.AddError("Quest '" + id + "' references unknown completionConditionId '" + completionConditionId + "'.");
                }
            }

            Dictionary<string, bool> locationIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (definition.Map != null && definition.Map.Locations != null)
            {
                for (int i = 0; i < definition.Map.Locations.Count; i++)
                {
                    MapLocationDefinition location = definition.Map.Locations[i];
                    string id = TrimToNull(location != null ? location.Id : null);
                    if (id == null)
                    {
                        result.AddError("Map location #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                        continue;
                    }

                    if (locationIds.ContainsKey(id))
                        result.AddError("Duplicate map location id: " + id);
                    else
                        locationIds[id] = true;
                }

                string startLocationId = TrimToNull(definition.Map.StartLocationId);
                if (startLocationId != null && !locationIds.ContainsKey(startLocationId))
                    result.AddError("Map references unknown startLocationId '" + startLocationId + "'.");
            }
        }

        private static bool HasTrigger(TriggersAndEventsDefinition triggersAndEvents, string triggerId)
        {
            for (int i = 0; triggersAndEvents != null && triggersAndEvents.Triggers != null && i < triggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = triggersAndEvents.Triggers[i];
                if (trigger != null && string.Equals(trigger.Id, triggerId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasCondition(WinLossConditionsDefinition conditions, string conditionId)
        {
            for (int i = 0; conditions != null && conditions.WinConditions != null && i < conditions.WinConditions.Count; i++)
            {
                ConditionDef condition = conditions.WinConditions[i];
                if (condition != null && string.Equals(condition.Id, conditionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            for (int i = 0; conditions != null && conditions.LossConditions != null && i < conditions.LossConditions.Count; i++)
            {
                ConditionDef condition = conditions.LossConditions[i];
                if (condition != null && string.Equals(condition.Id, conditionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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

        private static void ValidateSpritePatches(AssetReferencesDefinition assets, string packRoot, ScenarioValidationResult result)
        {
            if (assets == null || assets.SpritePatches == null)
                return;

            for (int i = 0; i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition patch = assets.SpritePatches[i];
                if (patch == null)
                {
                    result.AddError("Sprite patch #" + i + " is null.");
                    continue;
                }

                if (TrimToNull(patch.Id) == null)
                    result.AddError("Sprite patch #" + i + " is missing id.");

                bool hasBaseSpriteId = TrimToNull(patch.BaseSpriteId) != null;
                bool hasBaseRelativePath = TrimToNull(patch.BaseRelativePath) != null;
                bool hasRuntimeSpriteKey = TrimToNull(patch.BaseRuntimeSpriteKey) != null;
                if (!hasBaseSpriteId && !hasBaseRelativePath && !hasRuntimeSpriteKey)
                    result.AddError("Sprite patch #" + i + " must define a base sprite reference.");

                if (hasBaseRelativePath)
                    ValidateAssetPath(packRoot, patch.BaseRelativePath, "sprite patch base", result);

                for (int operationIndex = 0; operationIndex < patch.Operations.Count; operationIndex++)
                {
                    SpritePatchOperation operation = patch.Operations[operationIndex];
                    if (operation == null)
                    {
                        result.AddError("Sprite patch '" + (patch.Id ?? ("#" + i)) + "' has a null operation.");
                        continue;
                    }

                    if (operation.Kind == SpritePatchOperationKind.Pixels && (operation.Runs == null || operation.Runs.Count == 0))
                        result.AddError("Sprite patch '" + (patch.Id ?? ("#" + i)) + "' has a pixel operation with no runs.");

                    for (int runIndex = 0; operation.Runs != null && runIndex < operation.Runs.Count; runIndex++)
                    {
                        SpritePatchDeltaRun run = operation.Runs[runIndex];
                        if (run == null || !run.IsValid())
                            result.AddError("Sprite patch '" + (patch.Id ?? ("#" + i)) + "' has an invalid delta run #" + runIndex + ".");
                    }
                }
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

        private static bool HasSpritePatch(AssetReferencesDefinition assets, string patchId)
        {
            if (assets == null || assets.SpritePatches == null || string.IsNullOrEmpty(patchId))
                return false;

            for (int i = 0; i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition patch = assets.SpritePatches[i];
                if (patch != null && string.Equals(patch.Id, patchId, StringComparison.OrdinalIgnoreCase))
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

        private sealed class CoreScenarioRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                if (summary == null)
                    return;

                if (definition == null)
                {
                    summary.AddError("core.definition.null", "Scenario definition is null.");
                    return;
                }

                if (TrimToNull(definition.Id) == null)
                    summary.AddError("core.meta.id_required", "Scenario Id is required.");
                if (TrimToNull(definition.DisplayName) == null)
                    summary.AddError("core.meta.display_name_required", "Scenario DisplayName is required.");
                if (!Enum.IsDefined(typeof(ScenarioBaseGameMode), definition.BaseGameMode))
                    summary.AddError("core.meta.invalid_base_mode", "Scenario BaseMode is invalid: " + definition.BaseGameMode);
            }
        }

        private sealed class DependencyValidationRule : IScenarioValidationRule
        {
            private readonly ScenarioValidator _owner;

            public DependencyValidationRule(ScenarioValidator owner)
            {
                _owner = owner;
            }

            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                _owner.ValidateDependencies(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class AssetValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateAssets(definition, scenarioFilePath, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class FamilyValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateFamily(definition, scenarioFilePath, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class InventoryValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateInventory(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class BunkerValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateBunker(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private sealed class QuestMapValidationRule : IScenarioValidationRule
        {
            public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
            {
                ScenarioValidationResult legacy = new ScenarioValidationResult();
                ValidateQuestsAndMap(definition, legacy);
                CopyIssues(legacy, summary);
            }
        }

        private static void CopyIssues(ScenarioValidationResult source, ValidationSummary target)
        {
            if (source == null || target == null)
                return;

            ScenarioValidationIssue[] issues = source.Issues;
            for (int i = 0; i < issues.Length; i++)
            {
                ScenarioValidationIssue issue = issues[i];
                if (issue == null)
                    continue;

                if (issue.Severity == ScenarioIssueSeverity.Error)
                    target.AddError("legacy.error", issue.Message);
                else
                    target.AddWarning("legacy.warning", issue.Message);
            }
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
