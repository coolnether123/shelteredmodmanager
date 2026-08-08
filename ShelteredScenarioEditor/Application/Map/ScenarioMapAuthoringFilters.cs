using System;

using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredScenarioEditor.Application.Map
{
    internal enum ScenarioMapAuthoringFilter
    {
        VanillaRegions,
        AuthoredLocations,
        HiddenUntilDiscovered,
        InvalidOrBlocked,
        DependencyLocked
    }

    internal static class ScenarioMapAuthoringFilterState
    {
        private static bool _vanillaRegions = true;
        private static bool _authoredLocations = true;
        private static bool _hiddenUntilDiscovered = true;
        private static bool _invalidOrBlocked = true;
        private static bool _dependencyLocked = true;

        public static bool IsVisible(ScenarioMapAuthoringFilter filter)
        {
            switch (filter)
            {
                case ScenarioMapAuthoringFilter.VanillaRegions: return _vanillaRegions;
                case ScenarioMapAuthoringFilter.AuthoredLocations: return _authoredLocations;
                case ScenarioMapAuthoringFilter.HiddenUntilDiscovered: return _hiddenUntilDiscovered;
                case ScenarioMapAuthoringFilter.InvalidOrBlocked: return _invalidOrBlocked;
                case ScenarioMapAuthoringFilter.DependencyLocked: return _dependencyLocked;
                default: return true;
            }
        }

        public static void Toggle(ScenarioMapAuthoringFilter filter)
        {
            SetVisible(filter, !IsVisible(filter));
        }

        public static void SetVisible(ScenarioMapAuthoringFilter filter, bool visible)
        {
            switch (filter)
            {
                case ScenarioMapAuthoringFilter.VanillaRegions: _vanillaRegions = visible; break;
                case ScenarioMapAuthoringFilter.AuthoredLocations: _authoredLocations = visible; break;
                case ScenarioMapAuthoringFilter.HiddenUntilDiscovered: _hiddenUntilDiscovered = visible; break;
                case ScenarioMapAuthoringFilter.InvalidOrBlocked: _invalidOrBlocked = visible; break;
                case ScenarioMapAuthoringFilter.DependencyLocked: _dependencyLocked = visible; break;
            }
        }

        public static float ResolveAuthoredMarkerAlpha(MapAuthoringDefinition map, MapLocationDefinition location, bool placementBlocked)
        {
            if (!_authoredLocations)
                return 0.16f;
            if (location == null)
                return 0.16f;
            if (location.HiddenUntilDiscovered && !_hiddenUntilDiscovered)
                return 0.16f;
            if (IsInvalidOrBlocked(map, location, placementBlocked) && !_invalidOrBlocked)
                return 0.16f;
            if (!string.IsNullOrEmpty(location.RequiredGateId) && !_dependencyLocked)
                return 0.16f;
            return 1f;
        }

        public static bool IsInvalidOrBlocked(MapAuthoringDefinition map, MapLocationDefinition location, bool placementBlocked)
        {
            if (location == null || placementBlocked)
                return true;
            if (location.ReplaceGeneratedLoot && string.IsNullOrEmpty(location.LootTableId))
                return true;
            if (!string.IsNullOrEmpty(location.LootTableId) && !ContainsLootTable(map, location.LootTableId))
                return true;
            if (!string.IsNullOrEmpty(location.EncounterTableId) && !ContainsEncounterTable(map, location.EncounterTableId))
                return true;
            return location.VisibleAtStart && location.HiddenUntilDiscovered;
        }

        private static bool ContainsLootTable(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; map != null && map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                if (table != null && string.Equals(table.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool ContainsEncounterTable(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; map != null && map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                if (table != null && string.Equals(table.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
