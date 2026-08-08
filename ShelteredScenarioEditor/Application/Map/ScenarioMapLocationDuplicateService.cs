using System;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredScenarioEditor.Application.Map
{
    internal static class ScenarioMapLocationDuplicateService
    {
        private const string ModePrefix = "duplicate:";

        public static string BuildMode(string sourceId)
        {
            return string.IsNullOrEmpty(sourceId) ? null : ModePrefix + ScenarioAutomationIdCodec.EncodeToken(sourceId);
        }

        public static bool TryReadSourceId(string mode, out string sourceId)
        {
            sourceId = null;
            if (string.IsNullOrEmpty(mode) || !mode.StartsWith(ModePrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            sourceId = ScenarioAutomationIdCodec.DecodeToken(mode.Substring(ModePrefix.Length));
            return !string.IsNullOrEmpty(sourceId);
        }

        public static bool TryDuplicateAtGrid(
            MapAuthoringDefinition map,
            string sourceId,
            int gridX,
            int gridY,
            float worldX,
            float worldY,
            out MapLocationDefinition copy,
            out string error)
        {
            copy = null;
            error = null;
            MapLocationDefinition source = FindLocation(map, sourceId);
            if (source == null)
            {
                error = "The source location was not found.";
                return false;
            }
            if (source.GridX == gridX && source.GridY == gridY)
            {
                error = "Choose a new target cell for the copy; locations are never duplicated in place.";
                return false;
            }
            MapLocationDefinition occupying = FindLocationAtGrid(map, gridX, gridY);
            if (occupying != null)
            {
                error = "Choose an empty authored cell; " + occupying.Id + " already uses this cell.";
                return false;
            }

            copy = Clone(source);
            copy.Id = BuildUniqueId(map, source.Id, gridX, gridY);
            copy.DisplayName = string.IsNullOrEmpty(source.DisplayName) ? "Location Copy" : source.DisplayName + " Copy";
            copy.X = gridX;
            copy.Y = gridY;
            copy.GridX = gridX;
            copy.GridY = gridY;
            SetProperty(copy, "authoring.duplicatedFrom", source.Id);
            SetProperty(copy, "authoring.world", worldX.ToString("0.###", CultureInfo.InvariantCulture) + "," + worldY.ToString("0.###", CultureInfo.InvariantCulture));
            map.Locations.Add(copy);
            return true;
        }

        private static MapLocationDefinition Clone(MapLocationDefinition source)
        {
            MapLocationDefinition copy = new MapLocationDefinition
            {
                Kind = source.Kind,
                Radius = source.Radius,
                Searchable = source.Searchable,
                DiscoveredAtStart = source.DiscoveredAtStart,
                VisibleAtStart = source.VisibleAtStart,
                HiddenUntilDiscovered = source.HiddenUntilDiscovered,
                IconId = source.IconId,
                // Marker identity is intentionally not shared by two locations.
                MarkerId = null,
                BoundaryId = source.BoundaryId,
                TerrainId = source.TerrainId,
                LootTableId = source.LootTableId,
                ReplaceGeneratedLoot = source.ReplaceGeneratedLoot,
                EncounterTableId = source.EncounterTableId,
                RequiredGateId = source.RequiredGateId,
                Danger = source.Danger
            };
            for (int i = 0; source.Properties != null && i < source.Properties.Count; i++)
            {
                ScenarioProperty property = source.Properties[i];
                if (property != null)
                    copy.Properties.Add(new ScenarioProperty { Key = property.Key, Value = property.Value });
            }
            return copy;
        }

        private static string BuildUniqueId(MapAuthoringDefinition map, string sourceId, int gridX, int gridY)
        {
            string root = (string.IsNullOrEmpty(sourceId) ? "authored" : sourceId) + "-copy-"
                + gridX.ToString(CultureInfo.InvariantCulture) + "-" + gridY.ToString(CultureInfo.InvariantCulture);
            string candidate = root;
            int suffix = 2;
            while (FindLocation(map, candidate) != null)
            {
                candidate = root + "-" + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }
            return candidate;
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

        private static MapLocationDefinition FindLocationAtGrid(MapAuthoringDefinition map, int gridX, int gridY)
        {
            for (int i = 0; map != null && map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location != null && location.GridX == gridX && location.GridY == gridY)
                    return location;
            }
            return null;
        }

        private static void SetProperty(MapLocationDefinition location, string key, string value)
        {
            for (int i = 0; location != null && location.Properties != null && i < location.Properties.Count; i++)
            {
                ScenarioProperty property = location.Properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    property.Value = value;
                    return;
                }
            }
            location.Properties.Add(new ScenarioProperty { Key = key, Value = value });
        }
    }
}
