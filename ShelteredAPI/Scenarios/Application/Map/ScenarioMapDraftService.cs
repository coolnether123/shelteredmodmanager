using System;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
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
