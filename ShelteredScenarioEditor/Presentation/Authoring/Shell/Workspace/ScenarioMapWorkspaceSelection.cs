using System;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    internal enum ScenarioMapWorkspaceEntityKind
    {
        None = 0,
        Location = 1,
        Marker = 2,
        Loot = 3,
        Encounter = 4
    }

    /// <summary>
    /// Owns stable Map workspace entity routes and the shared location-selection bridge.
    /// The bridge is used by both map commands and cached shell composition so the
    /// navigator and the in-world authored-location overlay cannot drift apart.
    /// </summary>
    internal static class ScenarioMapWorkspaceSelection
    {
        public const string WorkspaceId = "map";
        public const string MainSubtabId = "map";

        private const string LocationPrefix = "location";
        private const string MarkerPrefix = "marker";
        private const string LootPrefix = "loot";
        private const string EncounterPrefix = "encounter";

        public static string LocationEntityId(MapAuthoringDefinition map, int index)
        {
            MapLocationDefinition value = map != null && map.Locations != null && index >= 0 && index < map.Locations.Count
                ? map.Locations[index]
                : null;
            return BuildEntityId(LocationPrefix, value != null ? value.Id : null, CountLocationIds(map, value != null ? value.Id : null), index);
        }

        public static string MarkerEntityId(MapAuthoringDefinition map, int index)
        {
            MapMarkerDefinition value = map != null && map.Markers != null && index >= 0 && index < map.Markers.Count
                ? map.Markers[index]
                : null;
            return BuildEntityId(MarkerPrefix, value != null ? value.Id : null, CountMarkerIds(map, value != null ? value.Id : null), index);
        }

        public static string LootEntityId(MapAuthoringDefinition map, int index)
        {
            MapLootTableDefinition value = map != null && map.LootTables != null && index >= 0 && index < map.LootTables.Count
                ? map.LootTables[index]
                : null;
            return BuildEntityId(LootPrefix, value != null ? value.Id : null, CountLootIds(map, value != null ? value.Id : null), index);
        }

        public static string EncounterEntityId(MapAuthoringDefinition map, int index)
        {
            MapEncounterTableDefinition value = map != null && map.EncounterTables != null && index >= 0 && index < map.EncounterTables.Count
                ? map.EncounterTables[index]
                : null;
            return BuildEntityId(EncounterPrefix, value != null ? value.Id : null, CountEncounterIds(map, value != null ? value.Id : null), index);
        }

        public static bool TryResolveLocation(MapAuthoringDefinition map, string entityId, out int index)
        {
            index = -1;
            string id;
            if (!TryParseEntityId(entityId, LocationPrefix, out id, out index))
                return false;
            if (index >= 0)
                return map != null && map.Locations != null && index < map.Locations.Count && map.Locations[index] != null;
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition value = map.Locations[i];
                if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveMarker(MapAuthoringDefinition map, string entityId, out int index)
        {
            index = -1;
            string id;
            if (!TryParseEntityId(entityId, MarkerPrefix, out id, out index))
                return false;
            if (index >= 0)
                return map != null && map.Markers != null && index < map.Markers.Count && map.Markers[index] != null;
            for (int i = 0; map != null && map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition value = map.Markers[i];
                if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveLoot(MapAuthoringDefinition map, string entityId, out int index)
        {
            index = -1;
            string id;
            if (!TryParseEntityId(entityId, LootPrefix, out id, out index))
                return false;
            if (index >= 0)
                return map != null && map.LootTables != null && index < map.LootTables.Count && map.LootTables[index] != null;
            for (int i = 0; map != null && map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition value = map.LootTables[i];
                if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryResolveEncounter(MapAuthoringDefinition map, string entityId, out int index)
        {
            index = -1;
            string id;
            if (!TryParseEntityId(entityId, EncounterPrefix, out id, out index))
                return false;
            if (index >= 0)
                return map != null && map.EncounterTables != null && index < map.EncounterTables.Count && map.EncounterTables[index] != null;
            for (int i = 0; map != null && map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition value = map.EncounterTables[i];
                if (value != null && string.Equals(value.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static ScenarioMapWorkspaceEntityKind ResolveKind(MapAuthoringDefinition map, string entityId, out int index)
        {
            if (TryResolveLocation(map, entityId, out index)) return ScenarioMapWorkspaceEntityKind.Location;
            if (TryResolveMarker(map, entityId, out index)) return ScenarioMapWorkspaceEntityKind.Marker;
            if (TryResolveLoot(map, entityId, out index)) return ScenarioMapWorkspaceEntityKind.Loot;
            if (TryResolveEncounter(map, entityId, out index)) return ScenarioMapWorkspaceEntityKind.Encounter;
            index = -1;
            return ScenarioMapWorkspaceEntityKind.None;
        }

        public static string FindLocationEntityId(MapAuthoringDefinition map, string locationId)
        {
            if (string.IsNullOrEmpty(locationId))
                return null;
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition value = map.Locations[i];
                if (value != null && string.Equals(value.Id, locationId, StringComparison.OrdinalIgnoreCase))
                    return LocationEntityId(map, i);
            }
            return null;
        }

        public static void SelectLocation(
            ScenarioAuthoringState state,
            ScenarioDefinition definition,
            MapLocationDefinition location,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            if (state == null || location == null)
                return;

            MapAuthoringDefinition map = definition != null ? definition.Map : null;
            string entityId = FindLocationEntityId(map, location.Id);
            state.MapSelectedLocationId = location.Id;
            if (string.IsNullOrEmpty(entityId))
                return;

            rendererInteraction.SetWorkspaceSubtab(WorkspaceId, MainSubtabId);
            rendererInteraction.SetWorkspaceSelection(WorkspaceId, MainSubtabId, entityId);
            rendererInteraction.SetWorkspaceNarrowPane(WorkspaceId, MainSubtabId, true);
        }

        public static void ClearLocationSelection(
            ScenarioAuthoringState state,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            if (state != null)
                state.MapSelectedLocationId = null;
            rendererInteraction.SetWorkspaceSelection(WorkspaceId, MainSubtabId, null);
        }

        private static string BuildEntityId(string prefix, string id, int matchingIds, int index)
        {
            return !string.IsNullOrEmpty(id) && matchingIds == 1
                ? prefix + ":id:" + ScenarioAutomationIdCodec.EncodeToken(id)
                : prefix + ":index:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryParseEntityId(string entityId, string prefix, out string id, out int index)
        {
            id = null;
            index = -1;
            string idPrefix = prefix + ":id:";
            if (!string.IsNullOrEmpty(entityId) && entityId.StartsWith(idPrefix, StringComparison.Ordinal))
            {
                id = ScenarioAutomationIdCodec.DecodeToken(entityId.Substring(idPrefix.Length));
                return !string.IsNullOrEmpty(id);
            }

            string indexPrefix = prefix + ":index:";
            return !string.IsNullOrEmpty(entityId)
                && entityId.StartsWith(indexPrefix, StringComparison.Ordinal)
                && int.TryParse(entityId.Substring(indexPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out index)
                && index >= 0;
        }

        private static int CountLocationIds(MapAuthoringDefinition map, string id)
        {
            int count = 0;
            for (int i = 0; !string.IsNullOrEmpty(id) && map != null && map.Locations != null && i < map.Locations.Count; i++)
                if (map.Locations[i] != null && string.Equals(map.Locations[i].Id, id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountMarkerIds(MapAuthoringDefinition map, string id)
        {
            int count = 0;
            for (int i = 0; !string.IsNullOrEmpty(id) && map != null && map.Markers != null && i < map.Markers.Count; i++)
                if (map.Markers[i] != null && string.Equals(map.Markers[i].Id, id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountLootIds(MapAuthoringDefinition map, string id)
        {
            int count = 0;
            for (int i = 0; !string.IsNullOrEmpty(id) && map != null && map.LootTables != null && i < map.LootTables.Count; i++)
                if (map.LootTables[i] != null && string.Equals(map.LootTables[i].Id, id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }

        private static int CountEncounterIds(MapAuthoringDefinition map, string id)
        {
            int count = 0;
            for (int i = 0; !string.IsNullOrEmpty(id) && map != null && map.EncounterTables != null && i < map.EncounterTables.Count; i++)
                if (map.EncounterTables[i] != null && string.Equals(map.EncounterTables[i].Id, id, StringComparison.OrdinalIgnoreCase)) count++;
            return count;
        }
    }
}
