using System;
using System.Collections.Generic;
using System.Globalization;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class MapValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            ValidateMap(definition, result);
            CopyIssues(result, summary);
        }

        private static void ValidateMap(ScenarioDefinition definition, ScenarioValidationResult result)
        {
            if (definition == null || definition.Map == null)
                return;

            MapAuthoringDefinition map = definition.Map;
            Dictionary<string, bool> locationIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> markerIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> boundaryIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> lootTableIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> encounterTableIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> questIds = BuildQuestIndex(definition);

            if (map.Width < 0f)
                result.AddError("Map width cannot be negative.");
            if (map.Height < 0f)
                result.AddError("Map height cannot be negative.");

            IndexLocations(map, locationIds, result);
            IndexMarkers(map, markerIds, result);
            IndexBoundaries(map, boundaryIds, result);
            IndexLootTables(map, lootTableIds, result);
            IndexEncounterTables(map, encounterTableIds, result);

            ValidateReferences(definition, locationIds, markerIds, boundaryIds, lootTableIds, encounterTableIds, questIds, result);
            ValidateTerrain(map, boundaryIds, result);
            ValidateRoutes(map, locationIds, result);

            string startLocationId = TrimToNull(map.StartLocationId);
            if (startLocationId != null && !locationIds.ContainsKey(startLocationId))
                result.AddError("Map references unknown startLocationId '" + startLocationId + "'.");
        }

        private static void IndexLocations(MapAuthoringDefinition map, Dictionary<string, bool> locationIds, ScenarioValidationResult result)
        {
            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                string id = TrimToNull(location != null ? location.Id : null);
                if (id == null)
                {
                    result.AddError("Map location #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                AddUnique(locationIds, id, "Duplicate map location id: " + id, result);
                ValidatePoint("Map location '" + id + "'", location.X, location.Y, map, result);
                if (location.Radius < 0f)
                    result.AddError("Map location '" + id + "' radius cannot be negative.");
                if (location.Danger < 0)
                    result.AddError("Map location '" + id + "' danger cannot be negative.");
            }
        }

        private static void IndexMarkers(MapAuthoringDefinition map, Dictionary<string, bool> markerIds, ScenarioValidationResult result)
        {
            for (int i = 0; map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                string id = TrimToNull(marker != null ? marker.Id : null);
                if (id == null)
                {
                    result.AddError("Map marker #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                AddUnique(markerIds, id, "Duplicate map marker id: " + id, result);
                ValidatePoint("Map marker '" + id + "'", marker.X, marker.Y, map, result);
            }
        }

        private static void IndexBoundaries(MapAuthoringDefinition map, Dictionary<string, bool> boundaryIds, ScenarioValidationResult result)
        {
            for (int i = 0; map.Boundaries != null && i < map.Boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                string id = TrimToNull(boundary != null ? boundary.Id : null);
                if (id == null)
                {
                    result.AddError("Map boundary #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                AddUnique(boundaryIds, id, "Duplicate map boundary id: " + id, result);
                ValidateBoundaryShape(map, boundary, id, result);
            }
        }

        private static void ValidateBoundaryShape(MapAuthoringDefinition map, MapBoundaryDefinition boundary, string id, ScenarioValidationResult result)
        {
            bool hasRectangle = boundary.MinX.HasValue || boundary.MinY.HasValue || boundary.MaxX.HasValue || boundary.MaxY.HasValue;
            if (hasRectangle && (!boundary.MinX.HasValue || !boundary.MinY.HasValue || !boundary.MaxX.HasValue || !boundary.MaxY.HasValue))
                result.AddError("Map boundary '" + id + "' must define all rectangle extents or none.");
            if (hasRectangle)
            {
                if (boundary.MinX.Value > boundary.MaxX.Value || boundary.MinY.Value > boundary.MaxY.Value)
                    result.AddError("Map boundary '" + id + "' has inverted rectangle extents.");
                ValidatePoint("Map boundary '" + id + "' minimum", boundary.MinX.Value, boundary.MinY.Value, map, result);
                ValidatePoint("Map boundary '" + id + "' maximum", boundary.MaxX.Value, boundary.MaxY.Value, map, result);
            }

            if (!hasRectangle && (boundary.Points == null || boundary.Points.Count < 3))
                result.AddError("Map boundary '" + id + "' must define a rectangle or at least three polygon points.");
            ValidatePoints("Map boundary '" + id + "'", boundary.Points, map, result);
        }

        private static void IndexLootTables(MapAuthoringDefinition map, Dictionary<string, bool> lootTableIds, ScenarioValidationResult result)
        {
            for (int i = 0; map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                string id = TrimToNull(table != null ? table.Id : null);
                if (id == null)
                {
                    result.AddError("Map loot table #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                AddUnique(lootTableIds, id, "Duplicate map loot table id: " + id, result);
                if (table.Entries == null || table.Entries.Count == 0)
                    result.AddWarning("Map loot table '" + id + "' has no entries.");

                ValidateLootEntries(table, id, result);
            }
        }

        private static void ValidateLootEntries(MapLootTableDefinition table, string tableId, ScenarioValidationResult result)
        {
            for (int i = 0; table.Entries != null && i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                string itemId = TrimToNull(entry != null ? entry.ItemId : null);
                string label = itemId ?? ("#" + i.ToString(CultureInfo.InvariantCulture));
                if (itemId == null)
                    result.AddError("Map loot table '" + tableId + "' entry #" + i.ToString(CultureInfo.InvariantCulture) + " is missing itemId.");
                if (entry != null && entry.MinQuantity <= 0)
                    result.AddError("Map loot table '" + tableId + "' entry '" + label + "' min quantity must be greater than zero.");
                if (entry != null && entry.MaxQuantity < entry.MinQuantity)
                    result.AddError("Map loot table '" + tableId + "' entry '" + label + "' max quantity is lower than min quantity.");
                if (entry != null && entry.Weight <= 0)
                    result.AddError("Map loot table '" + tableId + "' entry '" + label + "' weight must be greater than zero.");
                if (entry != null && (entry.Chance < 0f || entry.Chance > 1f))
                    result.AddError("Map loot table '" + tableId + "' entry '" + label + "' chance must be between 0 and 1.");
            }
        }

        private static void IndexEncounterTables(MapAuthoringDefinition map, Dictionary<string, bool> encounterTableIds, ScenarioValidationResult result)
        {
            for (int i = 0; map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                string id = TrimToNull(table != null ? table.Id : null);
                if (id == null)
                {
                    result.AddError("Map encounter table #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                    continue;
                }

                AddUnique(encounterTableIds, id, "Duplicate map encounter table id: " + id, result);
                if (table.Entries == null || table.Entries.Count == 0)
                    result.AddWarning("Map encounter table '" + id + "' has no entries.");

                ValidateEncounterEntries(table, id, result);
            }
        }

        private static void ValidateEncounterEntries(MapEncounterTableDefinition table, string tableId, ScenarioValidationResult result)
        {
            for (int i = 0; table.Entries != null && i < table.Entries.Count; i++)
            {
                MapEncounterEntryDefinition entry = table.Entries[i];
                string entryId = TrimToNull(entry != null ? entry.Id : null) ?? ("#" + i.ToString(CultureInfo.InvariantCulture));
                if (entry != null && TrimToNull(entry.EncounterType) == null)
                    result.AddError("Map encounter table '" + tableId + "' entry '" + entryId + "' is missing encounter type.");
                if (entry != null && entry.MinCount <= 0)
                    result.AddError("Map encounter table '" + tableId + "' entry '" + entryId + "' min count must be greater than zero.");
                if (entry != null && entry.MaxCount < entry.MinCount)
                    result.AddError("Map encounter table '" + tableId + "' entry '" + entryId + "' max count is lower than min count.");
                if (entry != null && entry.Weight <= 0)
                    result.AddError("Map encounter table '" + tableId + "' entry '" + entryId + "' weight must be greater than zero.");
                if (entry != null && (entry.Chance < 0f || entry.Chance > 1f))
                    result.AddError("Map encounter table '" + tableId + "' entry '" + entryId + "' chance must be between 0 and 1.");
            }
        }

        private static void ValidateReferences(
            ScenarioDefinition definition,
            Dictionary<string, bool> locationIds,
            Dictionary<string, bool> markerIds,
            Dictionary<string, bool> boundaryIds,
            Dictionary<string, bool> lootTableIds,
            Dictionary<string, bool> encounterTableIds,
            Dictionary<string, bool> questIds,
            ScenarioValidationResult result)
        {
            MapAuthoringDefinition map = definition.Map;
            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                string label = "Map location '" + Label(location != null ? location.Id : null, i) + "'";
                ValidateOptionalReference(label, "markerId", location != null ? location.MarkerId : null, markerIds, result);
                ValidateOptionalReference(label, "boundaryId", location != null ? location.BoundaryId : null, boundaryIds, result);
                ValidateOptionalReference(label, "lootTableId", location != null ? location.LootTableId : null, lootTableIds, result);
                ValidateOptionalReference(label, "encounterTableId", location != null ? location.EncounterTableId : null, encounterTableIds, result);
                ValidateOptionalGateReference(definition, label, location != null ? location.RequiredGateId : null, result);
            }

            for (int i = 0; map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                string label = "Map marker '" + Label(marker != null ? marker.Id : null, i) + "'";
                ValidateOptionalReference(label, "locationId", marker != null ? marker.LocationId : null, locationIds, result);
                ValidateOptionalReference(label, "boundaryId", marker != null ? marker.BoundaryId : null, boundaryIds, result);
            }

            ValidateBoundaryReferences(definition, boundaryIds, lootTableIds, encounterTableIds, result);
            ValidateEncounterEntryReferences(map, lootTableIds, questIds, result);
        }

        private static void ValidateBoundaryReferences(
            ScenarioDefinition definition,
            Dictionary<string, bool> boundaryIds,
            Dictionary<string, bool> lootTableIds,
            Dictionary<string, bool> encounterTableIds,
            ScenarioValidationResult result)
        {
            MapAuthoringDefinition map = definition.Map;
            for (int i = 0; map.Boundaries != null && i < map.Boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                string label = "Map boundary '" + Label(boundary != null ? boundary.Id : null, i) + "'";
                ValidateOptionalReference(label, "lootTableId", boundary != null ? boundary.LootTableId : null, lootTableIds, result);
                ValidateOptionalReference(label, "encounterTableId", boundary != null ? boundary.EncounterTableId : null, encounterTableIds, result);
                ValidateOptionalGateReference(definition, label, boundary != null ? boundary.RequiredGateId : null, result);
            }
        }

        private static void ValidateEncounterEntryReferences(
            MapAuthoringDefinition map,
            Dictionary<string, bool> lootTableIds,
            Dictionary<string, bool> questIds,
            ScenarioValidationResult result)
        {
            for (int i = 0; map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                string tableId = table != null ? table.Id : ("#" + i.ToString(CultureInfo.InvariantCulture));
                for (int entryIndex = 0; table != null && table.Entries != null && entryIndex < table.Entries.Count; entryIndex++)
                {
                    MapEncounterEntryDefinition entry = table.Entries[entryIndex];
                    string label = "Map encounter table '" + tableId + "' entry '" + Label(entry != null ? entry.Id : null, entryIndex) + "'";
                    ValidateOptionalReference(label, "lootTableId", entry != null ? entry.LootTableId : null, lootTableIds, result);
                    ValidateOptionalReference(label, "questId", entry != null ? entry.QuestId : null, questIds, result);
                }
            }
        }

        private static void ValidateTerrain(MapAuthoringDefinition map, Dictionary<string, bool> boundaryIds, ScenarioValidationResult result)
        {
            for (int i = 0; map.TerrainPatches != null && i < map.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = map.TerrainPatches[i];
                string label = "Map terrain patch '" + Label(patch != null ? patch.Id : null, i) + "'";
                if (TrimToNull(patch != null ? patch.Id : null) == null)
                    result.AddError("Map terrain patch #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                if (patch != null && TrimToNull(patch.TerrainId) == null)
                    result.AddError(label + " is missing terrainId.");
                if (patch != null)
                    ValidateTerrainPatch(map, boundaryIds, patch, label, result);
            }
        }

        private static void ValidateTerrainPatch(
            MapAuthoringDefinition map,
            Dictionary<string, bool> boundaryIds,
            MapTerrainPatchDefinition patch,
            string label,
            ScenarioValidationResult result)
        {
            if (patch.Width < 0f)
                result.AddError(label + " width cannot be negative.");
            if (patch.Height < 0f)
                result.AddError(label + " height cannot be negative.");
            if (patch.Radius < 0f)
                result.AddError(label + " radius cannot be negative.");
            if (patch.Shape == MapTerrainBrushShape.Rectangle && (patch.Width <= 0f || patch.Height <= 0f) && TrimToNull(patch.BoundaryId) == null)
                result.AddError(label + " rectangle brush needs width/height or a boundaryId.");
            if (patch.Shape == MapTerrainBrushShape.Circle && patch.Radius <= 0f)
                result.AddError(label + " circle brush needs a positive radius.");
            if (patch.Shape == MapTerrainBrushShape.Polygon && (patch.Points == null || patch.Points.Count < 3))
                result.AddError(label + " polygon brush needs at least three points.");

            ValidatePoint(label, patch.X, patch.Y, map, result);
            ValidatePoints(label, patch.Points, map, result);
            ValidateOptionalReference(label, "boundaryId", patch.BoundaryId, boundaryIds, result);
        }

        private static void ValidateRoutes(MapAuthoringDefinition map, Dictionary<string, bool> locationIds, ScenarioValidationResult result)
        {
            Dictionary<string, bool> routeIds = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; map.Routes != null && i < map.Routes.Count; i++)
            {
                ExpeditionRouteDefinition route = map.Routes[i];
                string label = "Expedition route '" + Label(route != null ? route.Id : null, i) + "'";
                string id = TrimToNull(route != null ? route.Id : null);
                if (id == null)
                    result.AddError("Expedition route #" + i.ToString(CultureInfo.InvariantCulture) + " is missing id.");
                else
                    AddUnique(routeIds, id, "Duplicate expedition route id: " + id, result);

                ValidateRequiredReference(label, "from", route != null ? route.FromLocationId : null, locationIds, result);
                ValidateRequiredReference(label, "to", route != null ? route.ToLocationId : null, locationIds, result);
                if (route != null && route.Distance < 0f)
                    result.AddError(label + " distance cannot be negative.");
                if (route != null && route.Risk < 0)
                    result.AddError(label + " risk cannot be negative.");
                if (route != null)
                    ValidatePoints(label, route.Waypoints, map, result);
            }
        }

        private static Dictionary<string, bool> BuildQuestIndex(ScenarioDefinition definition)
        {
            Dictionary<string, bool> result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                string id = TrimToNull(quest != null ? quest.Id : null);
                if (id != null && !result.ContainsKey(id))
                    result[id] = true;
            }

            return result;
        }

        private static void ValidatePoint(string label, float x, float y, MapAuthoringDefinition map, ScenarioValidationResult result)
        {
            if (x < 0f || y < 0f)
                result.AddError(label + " has negative map coordinates.");
            if (map != null && map.Width > 0f && x > map.Width)
                result.AddError(label + " x coordinate is outside map width.");
            if (map != null && map.Height > 0f && y > map.Height)
                result.AddError(label + " y coordinate is outside map height.");
        }

        private static void ValidatePoints(string label, List<MapPointDefinition> points, MapAuthoringDefinition map, ScenarioValidationResult result)
        {
            for (int i = 0; points != null && i < points.Count; i++)
            {
                MapPointDefinition point = points[i];
                if (point == null)
                {
                    result.AddError(label + " point #" + i.ToString(CultureInfo.InvariantCulture) + " is null.");
                    continue;
                }

                ValidatePoint(label + " point #" + i.ToString(CultureInfo.InvariantCulture), point.X, point.Y, map, result);
            }
        }

        private static void ValidateOptionalReference(string label, string field, string value, Dictionary<string, bool> knownIds, ScenarioValidationResult result)
        {
            string id = TrimToNull(value);
            if (id != null && (knownIds == null || !knownIds.ContainsKey(id)))
                result.AddError(label + " references unknown " + field + " '" + id + "'.");
        }

        private static void ValidateRequiredReference(string label, string field, string value, Dictionary<string, bool> knownIds, ScenarioValidationResult result)
        {
            string id = TrimToNull(value);
            if (id == null)
            {
                result.AddError(label + " is missing " + field + ".");
                return;
            }

            ValidateOptionalReference(label, field, id, knownIds, result);
        }

        private static void ValidateOptionalGateReference(ScenarioDefinition definition, string label, string gateId, ScenarioValidationResult result)
        {
            string id = TrimToNull(gateId);
            if (id != null && !ScenarioDefinitionLookup.HasGate(definition, id))
                result.AddError(label + " references unknown requiredGateId '" + id + "'.");
        }

        private static void AddUnique(Dictionary<string, bool> ids, string id, string duplicateMessage, ScenarioValidationResult result)
        {
            if (ids.ContainsKey(id))
                result.AddError(duplicateMessage);
            else
                ids[id] = true;
        }

        private static string Label(string id, int index)
        {
            string trimmed = TrimToNull(id);
            return trimmed ?? ("#" + index.ToString(CultureInfo.InvariantCulture));
        }

        private static void CopyIssues(ScenarioValidationResult source, ValidationSummary target)
        {
            if (source == null || target == null)
                return;

            ScenarioValidationIssue[] issues = source.Issues;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                ScenarioValidationIssue issue = issues[i];
                if (issue == null)
                    continue;

                if (issue.Severity == ScenarioIssueSeverity.Error)
                    target.AddError("map.error", issue.Message);
                else
                    target.AddWarning("map.warning", issue.Message);
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
