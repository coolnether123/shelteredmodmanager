using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Core;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioDependencyResolver
    {
        bool IsLoaded(string modId);
    }

    internal interface IScenarioDependencyVersionResolver : IScenarioDependencyResolver
    {
        string GetLoadedVersion(string modId);
    }

    internal sealed class ModRegistryScenarioDependencyResolver : IScenarioDependencyVersionResolver
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

    internal sealed class ScenarioValidator
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
                ScenarioModDependency dependency = ScenarioDependencyManifest.ParseDependency(definition.Dependencies[i]);
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
            Dictionary<string, bool> markerIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> boundaryIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> lootTableIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> encounterTableIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (definition.Map != null && definition.Map.Locations != null)
            {
                if (definition.Map.Width < 0f)
                    result.AddError("Map width cannot be negative.");
                if (definition.Map.Height < 0f)
                    result.AddError("Map height cannot be negative.");

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

                    ValidateMapPoint("Map location '" + id + "'", location.X, location.Y, definition.Map, result);
                    if (location.Radius < 0f)
                        result.AddError("Map location '" + id + "' radius cannot be negative.");
                    if (location.Danger < 0)
                        result.AddError("Map location '" + id + "' danger cannot be negative.");
                }

                IndexMapMarkers(definition.Map, markerIds, result);
                IndexMapBoundaries(definition.Map, boundaryIds, result);
                IndexMapLootTables(definition.Map, lootTableIds, result);
                IndexMapEncounterTables(definition.Map, encounterTableIds, result);
                ValidateMapReferences(definition, locationIds, markerIds, boundaryIds, lootTableIds, encounterTableIds, questIds, result);
                ValidateMapTerrain(definition.Map, boundaryIds, result);
                ValidateExpeditionRoutes(definition.Map, locationIds, result);

                string startLocationId = TrimToNull(definition.Map.StartLocationId);
                if (startLocationId != null && !locationIds.ContainsKey(startLocationId))
                    result.AddError("Map references unknown startLocationId '" + startLocationId + "'.");
            }
        }

        private static void IndexMapMarkers(MapAuthoringDefinition map, Dictionary<string, bool> markerIds, ScenarioValidationResult result)
        {
            for (int i = 0; map != null && map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                string id = TrimToNull(marker != null ? marker.Id : null);
                if (id == null)
                {
                    result.AddError("Map marker #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                if (markerIds.ContainsKey(id))
                    result.AddError("Duplicate map marker id: " + id);
                else
                    markerIds[id] = true;

                ValidateMapPoint("Map marker '" + id + "'", marker.X, marker.Y, map, result);
            }
        }

        private static void IndexMapBoundaries(MapAuthoringDefinition map, Dictionary<string, bool> boundaryIds, ScenarioValidationResult result)
        {
            for (int i = 0; map != null && map.Boundaries != null && i < map.Boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                string id = TrimToNull(boundary != null ? boundary.Id : null);
                if (id == null)
                {
                    result.AddError("Map boundary #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                if (boundaryIds.ContainsKey(id))
                    result.AddError("Duplicate map boundary id: " + id);
                else
                    boundaryIds[id] = true;

                bool hasRectangle = boundary.MinX.HasValue || boundary.MinY.HasValue || boundary.MaxX.HasValue || boundary.MaxY.HasValue;
                if (hasRectangle && (!boundary.MinX.HasValue || !boundary.MinY.HasValue || !boundary.MaxX.HasValue || !boundary.MaxY.HasValue))
                    result.AddError("Map boundary '" + id + "' must define all rectangle extents or none.");
                if (hasRectangle)
                {
                    if (boundary.MinX.Value > boundary.MaxX.Value || boundary.MinY.Value > boundary.MaxY.Value)
                        result.AddError("Map boundary '" + id + "' has inverted rectangle extents.");
                    ValidateMapPoint("Map boundary '" + id + "' minimum", boundary.MinX.Value, boundary.MinY.Value, map, result);
                    ValidateMapPoint("Map boundary '" + id + "' maximum", boundary.MaxX.Value, boundary.MaxY.Value, map, result);
                }

                if (!hasRectangle && (boundary.Points == null || boundary.Points.Count < 3))
                    result.AddError("Map boundary '" + id + "' must define a rectangle or at least three polygon points.");
                ValidateMapPoints("Map boundary '" + id + "'", boundary.Points, map, result);
            }
        }

        private static void IndexMapLootTables(MapAuthoringDefinition map, Dictionary<string, bool> lootTableIds, ScenarioValidationResult result)
        {
            for (int i = 0; map != null && map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                string id = TrimToNull(table != null ? table.Id : null);
                if (id == null)
                {
                    result.AddError("Map loot table #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                if (lootTableIds.ContainsKey(id))
                    result.AddError("Duplicate map loot table id: " + id);
                else
                    lootTableIds[id] = true;

                if (table.Entries == null || table.Entries.Count == 0)
                    result.AddWarning("Map loot table '" + id + "' has no entries.");

                for (int entryIndex = 0; table.Entries != null && entryIndex < table.Entries.Count; entryIndex++)
                {
                    MapLootEntryDefinition entry = table.Entries[entryIndex];
                    string itemId = TrimToNull(entry != null ? entry.ItemId : null);
                    if (itemId == null)
                        result.AddError("Map loot table '" + id + "' entry #" + entryIndex.ToString(CultureInfo.InvariantCulture) + " is missing itemId.");
                    if (entry != null && entry.MinQuantity <= 0)
                        result.AddError("Map loot table '" + id + "' entry '" + (itemId ?? ("#" + entryIndex)) + "' min quantity must be greater than zero.");
                    if (entry != null && entry.MaxQuantity < entry.MinQuantity)
                        result.AddError("Map loot table '" + id + "' entry '" + (itemId ?? ("#" + entryIndex)) + "' max quantity is lower than min quantity.");
                    if (entry != null && entry.Weight <= 0)
                        result.AddError("Map loot table '" + id + "' entry '" + (itemId ?? ("#" + entryIndex)) + "' weight must be greater than zero.");
                    if (entry != null && (entry.Chance < 0f || entry.Chance > 1f))
                        result.AddError("Map loot table '" + id + "' entry '" + (itemId ?? ("#" + entryIndex)) + "' chance must be between 0 and 1.");
                }
            }
        }

        private static void IndexMapEncounterTables(MapAuthoringDefinition map, Dictionary<string, bool> encounterTableIds, ScenarioValidationResult result)
        {
            for (int i = 0; map != null && map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                string id = TrimToNull(table != null ? table.Id : null);
                if (id == null)
                {
                    result.AddError("Map encounter table #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                if (encounterTableIds.ContainsKey(id))
                    result.AddError("Duplicate map encounter table id: " + id);
                else
                    encounterTableIds[id] = true;

                if (table.Entries == null || table.Entries.Count == 0)
                    result.AddWarning("Map encounter table '" + id + "' has no entries.");

                for (int entryIndex = 0; table.Entries != null && entryIndex < table.Entries.Count; entryIndex++)
                {
                    MapEncounterEntryDefinition entry = table.Entries[entryIndex];
                    string entryId = TrimToNull(entry != null ? entry.Id : null) ?? ("#" + entryIndex.ToString(CultureInfo.InvariantCulture));
                    if (entry != null && TrimToNull(entry.EncounterType) == null)
                        result.AddError("Map encounter table '" + id + "' entry '" + entryId + "' is missing encounter type.");
                    if (entry != null && entry.MinCount <= 0)
                        result.AddError("Map encounter table '" + id + "' entry '" + entryId + "' min count must be greater than zero.");
                    if (entry != null && entry.MaxCount < entry.MinCount)
                        result.AddError("Map encounter table '" + id + "' entry '" + entryId + "' max count is lower than min count.");
                    if (entry != null && entry.Weight <= 0)
                        result.AddError("Map encounter table '" + id + "' entry '" + entryId + "' weight must be greater than zero.");
                    if (entry != null && (entry.Chance < 0f || entry.Chance > 1f))
                        result.AddError("Map encounter table '" + id + "' entry '" + entryId + "' chance must be between 0 and 1.");
                }
            }
        }

        private static void ValidateMapReferences(
            ScenarioDefinition definition,
            Dictionary<string, bool> locationIds,
            Dictionary<string, bool> markerIds,
            Dictionary<string, bool> boundaryIds,
            Dictionary<string, bool> lootTableIds,
            Dictionary<string, bool> encounterTableIds,
            Dictionary<string, bool> questIds,
            ScenarioValidationResult result)
        {
            MapAuthoringDefinition map = definition != null ? definition.Map : null;
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                string label = "Map location '" + (location != null && TrimToNull(location.Id) != null ? location.Id : ("#" + i.ToString(CultureInfo.InvariantCulture))) + "'";
                ValidateOptionalMapReference(label, "markerId", location != null ? location.MarkerId : null, markerIds, result);
                ValidateOptionalMapReference(label, "boundaryId", location != null ? location.BoundaryId : null, boundaryIds, result);
                ValidateOptionalMapReference(label, "lootTableId", location != null ? location.LootTableId : null, lootTableIds, result);
                ValidateOptionalMapReference(label, "encounterTableId", location != null ? location.EncounterTableId : null, encounterTableIds, result);
                ValidateOptionalGateReference(definition, label, location != null ? location.RequiredGateId : null, result);
            }

            for (int i = 0; map != null && map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                string label = "Map marker '" + (marker != null && TrimToNull(marker.Id) != null ? marker.Id : ("#" + i.ToString(CultureInfo.InvariantCulture))) + "'";
                ValidateOptionalMapReference(label, "locationId", marker != null ? marker.LocationId : null, locationIds, result);
                ValidateOptionalMapReference(label, "boundaryId", marker != null ? marker.BoundaryId : null, boundaryIds, result);
            }

            for (int i = 0; map != null && map.Boundaries != null && i < map.Boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                string label = "Map boundary '" + (boundary != null && TrimToNull(boundary.Id) != null ? boundary.Id : ("#" + i.ToString(CultureInfo.InvariantCulture))) + "'";
                ValidateOptionalMapReference(label, "lootTableId", boundary != null ? boundary.LootTableId : null, lootTableIds, result);
                ValidateOptionalMapReference(label, "encounterTableId", boundary != null ? boundary.EncounterTableId : null, encounterTableIds, result);
                ValidateOptionalGateReference(definition, label, boundary != null ? boundary.RequiredGateId : null, result);
            }

            for (int i = 0; map != null && map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                string tableId = table != null ? table.Id : ("#" + i.ToString(CultureInfo.InvariantCulture));
                for (int entryIndex = 0; table != null && table.Entries != null && entryIndex < table.Entries.Count; entryIndex++)
                {
                    MapEncounterEntryDefinition entry = table.Entries[entryIndex];
                    string label = "Map encounter table '" + tableId + "' entry '" + (entry != null && TrimToNull(entry.Id) != null ? entry.Id : ("#" + entryIndex.ToString(CultureInfo.InvariantCulture))) + "'";
                    ValidateOptionalMapReference(label, "lootTableId", entry != null ? entry.LootTableId : null, lootTableIds, result);
                    ValidateOptionalMapReference(label, "questId", entry != null ? entry.QuestId : null, questIds, result);
                }
            }
        }

        private static void ValidateMapTerrain(MapAuthoringDefinition map, Dictionary<string, bool> boundaryIds, ScenarioValidationResult result)
        {
            for (int i = 0; map != null && map.TerrainPatches != null && i < map.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = map.TerrainPatches[i];
                string id = TrimToNull(patch != null ? patch.Id : null);
                string label = "Map terrain patch '" + (id ?? ("#" + i.ToString(CultureInfo.InvariantCulture))) + "'";
                if (id == null)
                    result.AddError("Map terrain patch #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                if (patch != null && TrimToNull(patch.TerrainId) == null)
                    result.AddError(label + " is missing terrainId.");
                if (patch != null && patch.Width < 0f)
                    result.AddError(label + " width cannot be negative.");
                if (patch != null && patch.Height < 0f)
                    result.AddError(label + " height cannot be negative.");
                if (patch != null && patch.Radius < 0f)
                    result.AddError(label + " radius cannot be negative.");
                if (patch != null && patch.Shape == MapTerrainBrushShape.Rectangle && (patch.Width <= 0f || patch.Height <= 0f) && TrimToNull(patch.BoundaryId) == null)
                    result.AddError(label + " rectangle brush needs width/height or a boundaryId.");
                if (patch != null && patch.Shape == MapTerrainBrushShape.Circle && patch.Radius <= 0f)
                    result.AddError(label + " circle brush needs a positive radius.");
                if (patch != null && patch.Shape == MapTerrainBrushShape.Polygon && (patch.Points == null || patch.Points.Count < 3))
                    result.AddError(label + " polygon brush needs at least three points.");
                if (patch != null)
                {
                    ValidateMapPoint(label, patch.X, patch.Y, map, result);
                    ValidateMapPoints(label, patch.Points, map, result);
                    ValidateOptionalMapReference(label, "boundaryId", patch.BoundaryId, boundaryIds, result);
                }
            }
        }

        private static void ValidateExpeditionRoutes(MapAuthoringDefinition map, Dictionary<string, bool> locationIds, ScenarioValidationResult result)
        {
            Dictionary<string, bool> routeIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; map != null && map.Routes != null && i < map.Routes.Count; i++)
            {
                ExpeditionRouteDefinition route = map.Routes[i];
                string id = TrimToNull(route != null ? route.Id : null);
                string label = "Expedition route '" + (id ?? ("#" + i.ToString(CultureInfo.InvariantCulture))) + "'";
                if (id == null)
                    result.AddError("Expedition route #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                else if (routeIds.ContainsKey(id))
                    result.AddError("Duplicate expedition route id: " + id);
                else
                    routeIds[id] = true;

                ValidateRequiredMapReference(label, "from", route != null ? route.FromLocationId : null, locationIds, result);
                ValidateRequiredMapReference(label, "to", route != null ? route.ToLocationId : null, locationIds, result);
                if (route != null && route.Distance < 0f)
                    result.AddError(label + " distance cannot be negative.");
                if (route != null && route.Risk < 0)
                    result.AddError(label + " risk cannot be negative.");
                if (route != null)
                    ValidateMapPoints(label, route.Waypoints, map, result);
            }
        }

        private static void ValidateMapPoint(string label, float x, float y, MapAuthoringDefinition map, ScenarioValidationResult result)
        {
            if (x < 0f || y < 0f)
                result.AddError(label + " has negative map coordinates.");
            if (map != null && map.Width > 0f && x > map.Width)
                result.AddError(label + " x coordinate is outside map width.");
            if (map != null && map.Height > 0f && y > map.Height)
                result.AddError(label + " y coordinate is outside map height.");
        }

        private static void ValidateMapPoints(string label, List<MapPointDefinition> points, MapAuthoringDefinition map, ScenarioValidationResult result)
        {
            for (int i = 0; points != null && i < points.Count; i++)
            {
                MapPointDefinition point = points[i];
                if (point == null)
                {
                    result.AddError(label + " point #" + i.ToString(CultureInfo.InvariantCulture) + " is null.");
                    continue;
                }

                ValidateMapPoint(label + " point #" + i.ToString(CultureInfo.InvariantCulture), point.X, point.Y, map, result);
            }
        }

        private static void ValidateOptionalMapReference(string label, string field, string value, Dictionary<string, bool> knownIds, ScenarioValidationResult result)
        {
            string id = TrimToNull(value);
            if (id != null && (knownIds == null || !knownIds.ContainsKey(id)))
                result.AddError(label + " references unknown " + field + " '" + id + "'.");
        }

        private static void ValidateRequiredMapReference(string label, string field, string value, Dictionary<string, bool> knownIds, ScenarioValidationResult result)
        {
            string id = TrimToNull(value);
            if (id == null)
            {
                result.AddError(label + " is missing " + field + ".");
                return;
            }

            ValidateOptionalMapReference(label, field, id, knownIds, result);
        }

        private static void ValidateOptionalGateReference(ScenarioDefinition definition, string label, string gateId, ScenarioValidationResult result)
        {
            string id = TrimToNull(gateId);
            if (id != null && !HasGate(definition, id))
                result.AddError(label + " references unknown requiredGateId '" + id + "'.");
        }

        private static bool HasGate(ScenarioDefinition definition, string gateId)
        {
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                if (gate != null && string.Equals(gate.Id, gateId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
