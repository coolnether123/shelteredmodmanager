using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
namespace ShelteredAPI.Scenarios.Application.Map{
    internal sealed class ScenarioMapDraftService
    {
        public MapAuthoringDefinition EnsureMap(ScenarioEditorSession session)
        {
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
                return null;

            if (definition.Map == null)
                definition.Map = new MapAuthoringDefinition();

            return definition.Map;
        }

        public bool UpsertLocation(ScenarioEditorSession session, MapLocationDefinition location)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.Locations, location, GetId, location != null ? location.Id : null);
        }

        public MapLocationDefinition CreateLocationAtGrid(
            ScenarioEditorSession session,
            int gridX,
            int gridY,
            float worldX,
            float worldY)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null)
                return null;

            EnsureMapSize(map);
            string id = BuildAuthoredLocationId(map, gridX, gridY);
            MapLocationDefinition location = new MapLocationDefinition();
            location.Id = id;
            location.DisplayName = "Authored Location " + (map.Locations.Count + 1).ToString(CultureInfo.InvariantCulture);
            location.Kind = "PointOfInterest";
            location.X = gridX;
            location.Y = gridY;
            location.GridX = gridX;
            location.GridY = gridY;
            location.Searchable = true;
            location.VisibleAtStart = true;
            location.DiscoveredAtStart = false;
            location.HiddenUntilDiscovered = false;
            SetProperty(location.Properties, "authoring.createdFrom", "map-click");
            SetProperty(location.Properties, "authoring.world", FormatFloat(worldX) + "," + FormatFloat(worldY));
            map.Locations.Add(location);
            return location;
        }

        public bool MoveLocation(ScenarioEditorSession session, string id, int gridX, int gridY, float worldX, float worldY, out MapLocationDefinition location)
        {
            location = null;
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || string.IsNullOrEmpty(id))
                return false;

            location = FindLocation(map, id);
            if (location == null)
                return false;

            EnsureMapSize(map);
            location.X = gridX;
            location.Y = gridY;
            location.GridX = gridX;
            location.GridY = gridY;
            SetProperty(location.Properties, "authoring.world", FormatFloat(worldX) + "," + FormatFloat(worldY));
            return true;
        }

        public bool UpsertLocationFromSelection(
            ScenarioEditorSession session,
            ScenarioMapRegionSelection selection,
            out MapLocationDefinition location,
            out bool wasExisting)
        {
            location = null;
            wasExisting = false;
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || selection == null)
                return false;

            EnsureMapSize(map);

            string id = BuildLocationId(selection);
            location = FindLocation(map, id);
            wasExisting = location != null;
            if (location == null)
            {
                location = new MapLocationDefinition();
                location.Id = id;
            }

            location.DisplayName = !string.IsNullOrEmpty(selection.DisplayName) ? selection.DisplayName : id;
            location.Kind = !string.IsNullOrEmpty(selection.Topography) ? selection.Topography : selection.Category;
            location.X = selection.GridX;
            location.Y = selection.GridY;
            location.GridX = selection.GridX;
            location.GridY = selection.GridY;
            location.Searchable = selection.Searchable;
            location.DiscoveredAtStart = selection.Discovered;
            location.VisibleAtStart = selection.VisibleOnMap;
            location.HiddenUntilDiscovered = selection.HiddenUntilDiscovered;
            location.Danger = Math.Max(0, selection.OpenGroundEncounterChance);
            if (ScenarioMapIconCatalog.IsKnownIconId(selection.IconId))
                location.IconId = selection.IconId;

            SetProperty(location.Properties, "vanilla.regionName", selection.RegionName);
            SetProperty(location.Properties, "vanilla.townName", selection.TownName);
            SetProperty(location.Properties, "vanilla.category", selection.Category);
            SetProperty(location.Properties, "vanilla.topography", selection.Topography);
            SetProperty(location.Properties, "vanilla.grid", selection.GridX.ToString(CultureInfo.InvariantCulture) + "," + selection.GridY.ToString(CultureInfo.InvariantCulture));
            SetProperty(location.Properties, "vanilla.world", FormatFloat(selection.WorldX) + "," + FormatFloat(selection.WorldY));
            SetProperty(location.Properties, "vanilla.visibleOnMap", selection.VisibleOnMap ? "true" : "false");
            SetProperty(location.Properties, "vanilla.discovered", selection.Discovered ? "true" : "false");
            SetProperty(location.Properties, "vanilla.hiddenUntilDiscovered", selection.HiddenUntilDiscovered ? "true" : "false");
            SetProperty(location.Properties, "vanilla.searchable", selection.Searchable ? "true" : "false");
            SetProperty(location.Properties, "vanilla.iconId", selection.IconId);
            SetProperty(location.Properties, "vanilla.searchTime", FormatFloat(selection.MinSearchTime) + "-" + FormatFloat(selection.MaxSearchTime));
            SetProperty(location.Properties, "vanilla.hasItems", selection.HasItems ? "true" : "false");
            SetProperty(location.Properties, "vanilla.hasHiddenItems", selection.HasHiddenItems ? "true" : "false");
            SetProperty(location.Properties, "vanilla.hasQuest", selection.HasQuest ? "true" : "false");
            SetProperty(location.Properties, "vanilla.maxItems", selection.MaxItems.ToString(CultureInfo.InvariantCulture));
            SetProperty(location.Properties, "vanilla.locationLootTypeCount", selection.LocationSpecificLootTypeCount.ToString(CultureInfo.InvariantCulture));
            SetProperty(location.Properties, "vanilla.searchNpcRevealChance", selection.SearchNpcRevealChance.ToString(CultureInfo.InvariantCulture));
            SetProperty(location.Properties, "vanilla.openGroundEncounterChance", selection.OpenGroundEncounterChance.ToString(CultureInfo.InvariantCulture));
            SetProperty(location.Properties, "vanilla.openGroundFactionEncounterChance", selection.OpenGroundFactionEncounterChance.ToString(CultureInfo.InvariantCulture));
            SetProperty(location.Properties, "vanilla.animalEncounterChance", selection.AnimalEncounterChance.ToString(CultureInfo.InvariantCulture));

            return UpsertLocation(session, location);
        }

        public bool UpsertMarker(ScenarioEditorSession session, MapMarkerDefinition marker)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.Markers, marker, GetId, marker != null ? marker.Id : null);
        }

        public bool UpsertBoundary(ScenarioEditorSession session, MapBoundaryDefinition boundary)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.Boundaries, boundary, GetId, boundary != null ? boundary.Id : null);
        }

        public bool UpsertTerrainPatch(ScenarioEditorSession session, MapTerrainPatchDefinition patch)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.TerrainPatches, patch, GetId, patch != null ? patch.Id : null);
        }

        public bool UpsertLootTable(ScenarioEditorSession session, MapLootTableDefinition table)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.LootTables, table, GetId, table != null ? table.Id : null);
        }

        public bool UpsertEncounterTable(ScenarioEditorSession session, MapEncounterTableDefinition table)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.EncounterTables, table, GetId, table != null ? table.Id : null);
        }

        public bool UpsertRoute(ScenarioEditorSession session, ExpeditionRouteDefinition route)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && Upsert(map.Routes, route, GetId, route != null ? route.Id : null);
        }

        public bool RemoveLocation(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || !RemoveById(map.Locations, GetId, id))
                return false;

            ClearLocationReferences(map, id);
            if (string.Equals(map.StartLocationId, id, StringComparison.OrdinalIgnoreCase))
                map.StartLocationId = null;
            return true;
        }

        public bool RemoveMarker(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || !RemoveById(map.Markers, GetId, id))
                return false;

            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.MarkerId, id, StringComparison.OrdinalIgnoreCase))
                    location.MarkerId = null;
            }

            return true;
        }

        public bool RemoveBoundary(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || !RemoveById(map.Boundaries, GetId, id))
                return false;

            ClearBoundaryReferences(map, id);
            return true;
        }

        public bool RemoveTerrainPatch(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && RemoveById(map.TerrainPatches, GetId, id);
        }

        public bool RemoveLootTable(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || !RemoveById(map.LootTables, GetId, id))
                return false;

            ClearLootTableReferences(map, id);
            return true;
        }

        public bool RemoveEncounterTable(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            if (map == null || !RemoveById(map.EncounterTables, GetId, id))
                return false;

            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.EncounterTableId, id, StringComparison.OrdinalIgnoreCase))
                    location.EncounterTableId = null;
            }

            for (int i = 0; map.Boundaries != null && i < map.Boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                if (boundary != null && string.Equals(boundary.EncounterTableId, id, StringComparison.OrdinalIgnoreCase))
                    boundary.EncounterTableId = null;
            }

            return true;
        }

        public bool RemoveRoute(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && RemoveById(map.Routes, GetId, id);
        }

        private static bool Upsert<T>(List<T> items, T value, Func<T, string> getId, string id)
        {
            if (items == null || value == null || string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < items.Count; i++)
            {
                T current = items[i];
                if (current != null && string.Equals(getId(current), id, StringComparison.OrdinalIgnoreCase))
                {
                    items[i] = value;
                    return true;
                }
            }

            items.Add(value);
            return true;
        }

        private static bool RemoveById<T>(List<T> items, Func<T, string> getId, string id)
        {
            if (items == null || string.IsNullOrEmpty(id))
                return false;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                T item = items[i];
                if (item != null && string.Equals(getId(item), id, StringComparison.OrdinalIgnoreCase))
                {
                    items.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public bool HasLocation(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return map != null && FindLocation(map, id) != null;
        }

        public MapLocationDefinition GetLocation(ScenarioEditorSession session, string id)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            return FindLocation(map, id);
        }

        public MapLocationDefinition FindLocationAtGrid(ScenarioEditorSession session, int gridX, int gridY)
        {
            MapAuthoringDefinition map = EnsureMap(session);
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && location.GridX == gridX && location.GridY == gridY)
                    return location;
            }

            return null;
        }

        public string BuildLocationId(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return null;

            return "vanilla-" + selection.GridX.ToString(CultureInfo.InvariantCulture)
                + "-" + selection.GridY.ToString(CultureInfo.InvariantCulture);
        }

        private static MapLocationDefinition FindLocation(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.Id, id, StringComparison.OrdinalIgnoreCase))
                    return location;
            }

            return null;
        }

        private static void EnsureMapSize(MapAuthoringDefinition map)
        {
            if (map == null || ExpeditionMap.Instance == null)
                return;

            if (map.Width <= 0f)
                map.Width = ExpeditionMap.Instance.width;
            if (map.Height <= 0f)
                map.Height = ExpeditionMap.Instance.height;
        }

        private static string BuildAuthoredLocationId(MapAuthoringDefinition map, int gridX, int gridY)
        {
            string root = "authored-" + gridX.ToString(CultureInfo.InvariantCulture)
                + "-" + gridY.ToString(CultureInfo.InvariantCulture);
            string candidate = root;
            int suffix = 2;
            while (FindLocation(map, candidate) != null)
            {
                candidate = root + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            return candidate;
        }

        private static void SetProperty(List<ScenarioProperty> properties, string key, string value)
        {
            if (properties == null || string.IsNullOrEmpty(key))
                return;

            for (int i = 0; i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    property.Value = value ?? string.Empty;
                    return;
                }
            }

            properties.Add(new ScenarioProperty { Key = key, Value = value ?? string.Empty });
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void ClearLocationReferences(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                if (marker != null && string.Equals(marker.LocationId, id, StringComparison.OrdinalIgnoreCase))
                    marker.LocationId = null;
            }

            for (int i = 0; map.Routes != null && i < map.Routes.Count; i++)
            {
                ExpeditionRouteDefinition route = map.Routes[i];
                if (route == null)
                    continue;
                if (string.Equals(route.FromLocationId, id, StringComparison.OrdinalIgnoreCase))
                    route.FromLocationId = null;
                if (string.Equals(route.ToLocationId, id, StringComparison.OrdinalIgnoreCase))
                    route.ToLocationId = null;
            }
        }

        private static void ClearBoundaryReferences(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.BoundaryId, id, StringComparison.OrdinalIgnoreCase))
                    location.BoundaryId = null;
            }

            for (int i = 0; map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                if (marker != null && string.Equals(marker.BoundaryId, id, StringComparison.OrdinalIgnoreCase))
                    marker.BoundaryId = null;
            }

            for (int i = 0; map.TerrainPatches != null && i < map.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = map.TerrainPatches[i];
                if (patch != null && string.Equals(patch.BoundaryId, id, StringComparison.OrdinalIgnoreCase))
                    patch.BoundaryId = null;
            }
        }

        private static void ClearLootTableReferences(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && string.Equals(location.LootTableId, id, StringComparison.OrdinalIgnoreCase))
                    location.LootTableId = null;
            }

            for (int i = 0; map.Boundaries != null && i < map.Boundaries.Count; i++)
            {
                MapBoundaryDefinition boundary = map.Boundaries[i];
                if (boundary != null && string.Equals(boundary.LootTableId, id, StringComparison.OrdinalIgnoreCase))
                    boundary.LootTableId = null;
            }

            for (int i = 0; map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                for (int entryIndex = 0; table != null && table.Entries != null && entryIndex < table.Entries.Count; entryIndex++)
                {
                    MapEncounterEntryDefinition entry = table.Entries[entryIndex];
                    if (entry != null && string.Equals(entry.LootTableId, id, StringComparison.OrdinalIgnoreCase))
                        entry.LootTableId = null;
                }
            }
        }

        private static string GetId(MapLocationDefinition value) { return value != null ? value.Id : null; }
        private static string GetId(MapMarkerDefinition value) { return value != null ? value.Id : null; }
        private static string GetId(MapBoundaryDefinition value) { return value != null ? value.Id : null; }
        private static string GetId(MapTerrainPatchDefinition value) { return value != null ? value.Id : null; }
        private static string GetId(MapLootTableDefinition value) { return value != null ? value.Id : null; }
        private static string GetId(MapEncounterTableDefinition value) { return value != null ? value.Id : null; }
        private static string GetId(ExpeditionRouteDefinition value) { return value != null ? value.Id : null; }
    }
}
