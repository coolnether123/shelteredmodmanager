using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Reflection;
using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    [PatchPolicy(PatchDomain.World, "HomeShelterMapGeneration",
        TargetBehavior = "Resolve and apply the active home shelter placement during expedition map generation.",
        FailureMode = "Mods must patch ExpeditionMap themselves to move the home shelter safely.",
        RollbackStrategy = "Disable the World patch domain or remove the home-shelter map-generation patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch]
    internal static class HomeShelterMapGenerationPatches
    {
        [HarmonyPatch(typeof(ExpeditionMap), "CreateMap")]
        [HarmonyPrefix]
        private static void CreateMapPrefix(ExpeditionMap __instance)
        {
            HomeShelterMapGenerationRuntime.OnCreateMapStart(__instance);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "CreateMap")]
        [HarmonyPostfix]
        private static void CreateMapPostfix(ExpeditionMap __instance)
        {
            HomeShelterMapGenerationRuntime.OnCreateMapEnd(__instance);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "WorldPosToGridRef")]
        [HarmonyPrefix]
        private static void WorldPosToGridRefPrefix(ref Vector2 worldPos)
        {
            HomeShelterMapGenerationRuntime.RedirectWorldPosIfHomeOrigin(ref worldPos);
        }

        [HarmonyPatch(typeof(MapRegion), "GetTooltipText")]
        [HarmonyPostfix]
        private static void MapRegionGetTooltipTextPostfix(MapRegion __instance, ref string __result)
        {
            HomeShelterMapGenerationRuntime.ApplyHomeShelterTooltip(__instance, ref __result);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "PlaceBuildingsNearToShelter")]
        [HarmonyPrefix]
        private static bool PlaceBuildingsNearToShelterPrefix(ExpeditionMap __instance)
        {
            return HomeShelterMapGenerationRuntime.PlaceBuildingsNearToShelter(__instance);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "PlaceBuildingsNearToShelter_Remaining")]
        [HarmonyPrefix]
        private static bool PlaceBuildingsNearToShelterRemainingPrefix(ExpeditionMap __instance)
        {
            return HomeShelterMapGenerationRuntime.PlaceBuildingsNearToShelterRemaining(__instance);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "PlaceShelters")]
        [HarmonyPrefix]
        private static bool PlaceSheltersPrefix(ExpeditionMap __instance, int numToPlace)
        {
            return HomeShelterMapGenerationRuntime.PlaceShelters(__instance, numToPlace);
        }

        [HarmonyPatch(typeof(ExpeditionMap), "BuildMap")]
        [HarmonyPostfix]
        private static void BuildMapPostfix(ExpeditionMap __instance)
        {
            HomeShelterMapGenerationRuntime.SanitizeHomeShelterRegion(__instance, "BuildMap");
        }

        [HarmonyPatch(typeof(ExpeditionMap), "SaveLoad")]
        [HarmonyPostfix]
        private static void SaveLoadPostfix(ExpeditionMap __instance)
        {
            HomeShelterMapGenerationRuntime.SanitizeHomeShelterRegion(__instance, "SaveLoad");
        }
    }

    internal static class HomeShelterMapGenerationRuntime
    {
        internal static void OnCreateMapStart(ExpeditionMap map)
        {
            if (map == null || ModRuntime.IsQuitting)
                return;

            HomeShelterPositionSnapshot snapshot;
            bool allowProviders = !IsSaveLoading();
            if (TryResolveHomeShelterSnapshot("CreateMap start before vanilla map generation", allowProviders, out snapshot))
            {
                MMLog.WriteDebug("[ShelteredMap] Home shelter placement ready before CreateMap. "
                    + FormatSnapshot(snapshot)
                    + ", providerResolutionAllowed=" + allowProviders + ".");
                return;
            }

            // No provider is a valid state. Vanilla placement remains in control.
        }

        internal static void OnCreateMapEnd(ExpeditionMap map)
        {
            if (map == null || ModRuntime.IsQuitting)
                return;

            HomeShelterPositionSnapshot snapshot;
            bool allowProviders = !IsSaveLoading();
            if (!TryResolveHomeShelterSnapshot("CreateMap end verification", allowProviders, out snapshot))
            {
                return;
            }

            VerifyHomeShelterCell(map, snapshot, "CreateMap end");
        }

        internal static void RedirectWorldPosIfHomeOrigin(ref Vector2 worldPos)
        {
            if (ModRuntime.IsQuitting || !IsNearOrigin(worldPos, 0.01f))
                return;

            HomeShelterPositionSnapshot snapshot;
            if (!TryGetRegisteredHomeShelterSnapshot(out snapshot))
                return;

            Vector2 target;
            if (!TryGetWorld(snapshot, out target) || IsNearOrigin(target, 0.01f))
                return;

            worldPos = target;
        }

        internal static void ApplyHomeShelterTooltip(MapRegion region, ref string tooltipText)
        {
            if (region == null || region.topography != MapRegion.Topography.Shelter || ModRuntime.IsQuitting)
                return;

            HomeShelterPositionSnapshot snapshot;
            if (!TryGetRegisteredHomeShelterSnapshot(out snapshot))
                return;

            Vector2 homeWorld;
            if (!TryGetWorld(snapshot, out homeWorld))
                return;

            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null)
                return;

            try
            {
                if (!ReferenceEquals(region, map.GetRegionInWorld(homeWorld)))
                    return;

                tooltipText = "Your Bunker";
            }
            catch (Exception ex)
            {
                MMLog.WriteDebug("[ShelteredMap] Home shelter tooltip resolution failed: " + ex);
            }
        }

        internal static bool PlaceBuildingsNearToShelter(ExpeditionMap map)
        {
            if (ShouldUseVanillaMapMutation(map))
                return true;

            HomeShelterPositionSnapshot snapshot;
            if (!TryResolveHomeShelterSnapshot("PlaceBuildingsNearToShelter", !IsSaveLoading(), out snapshot))
                return true;

            try
            {
                CustomPlaceBuildings(map, snapshot);
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ShelteredMap] PlaceBuildingsNearToShelter failed: " + ex);
                return true;
            }
        }

        internal static bool PlaceBuildingsNearToShelterRemaining(ExpeditionMap map)
        {
            if (ShouldUseVanillaMapMutation(map))
                return true;

            HomeShelterPositionSnapshot snapshot;
            if (!TryResolveHomeShelterSnapshot("PlaceBuildingsNearToShelter_Remaining", !IsSaveLoading(), out snapshot))
                return true;

            try
            {
                CustomPlaceBuildingsRemaining(map, snapshot);
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ShelteredMap] PlaceBuildingsNearToShelter_Remaining failed: " + ex);
                return true;
            }
        }

        internal static bool PlaceShelters(ExpeditionMap map, int numToPlace)
        {
            if (ShouldUseVanillaMapMutation(map))
                return true;

            if (numToPlace <= 0)
                return false;

            HomeShelterPositionSnapshot snapshot;
            if (!TryResolveHomeShelterSnapshot("PlaceShelters", !IsSaveLoading(), out snapshot))
                return true;

            try
            {
                Array scratch = Safe.GetField<Array>(map, "m_mapScratchpad");
                if (scratch == null)
                    return true;

                Vector2 homeWorld = RequireWorld(snapshot, "PlaceShelters");
                ExpeditionMap.GridRef homeGrid = map.WorldPosToGridRef(homeWorld);
                if (homeGrid == null || !InBounds(scratch, homeGrid.x, homeGrid.y))
                {
                    MMLog.WriteError("[ShelteredMap] Resolved home shelter maps outside the expedition scratchpad during PlaceShelters. "
                        + "world=" + FormatVector(homeWorld)
                        + ", grid=" + FormatGrid(homeGrid)
                        + ", scratch=" + FormatArray(scratch) + ". Falling back to vanilla shelter placement.");
                    return true;
                }

                int placed = 0;
                if (ForcePlaceShelterCell(scratch, homeGrid.x, homeGrid.y))
                    placed++;

                for (int radius = 1; placed < numToPlace && radius <= 4; radius++)
                {
                    for (int dx = -radius; dx <= radius && placed < numToPlace; dx++)
                    {
                        for (int dy = -radius; dy <= radius && placed < numToPlace; dy++)
                        {
                            if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius)
                                continue;

                            int x = homeGrid.x + dx;
                            int y = homeGrid.y + dy;
                            if (!InBounds(scratch, x, y))
                                continue;
                            if (ForcePlaceShelterCell(scratch, x, y))
                                placed++;
                        }
                    }
                }

                if (placed <= 0)
                {
                    MMLog.WriteError("[ShelteredMap] Failed to stamp any shelter cells during PlaceShelters. "
                        + "world=" + FormatVector(homeWorld)
                        + ", grid=" + FormatGrid(homeGrid)
                        + ", scratch=" + FormatArray(scratch) + ". Falling back to vanilla shelter placement.");
                    return true;
                }

                MMLog.WriteDebug("[ShelteredMap] Stamped " + placed + " shelter cell(s) during PlaceShelters at grid="
                    + FormatGrid(homeGrid) + ", world=" + FormatVector(homeWorld) + ".");
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ShelteredMap] PlaceShelters failed: " + ex);
                return true;
            }
        }

        internal static void SanitizeHomeShelterRegion(ExpeditionMap map, string source)
        {
            if (map == null || ModRuntime.IsQuitting)
                return;

            try
            {
                ExpeditionMap.GridRef homeGrid;
                if (!TryResolveHomeShelterGrid(map, out homeGrid))
                    return;

                MapRegion[,] regions = Safe.GetField<MapRegion[,]>(map, "m_mapRegions");
                if (regions == null || !InBounds(regions, homeGrid.x, homeGrid.y))
                    return;

                MapRegion homeRegion = regions[homeGrid.x, homeGrid.y];
                if (homeRegion == null || homeRegion.topography != MapRegion.Topography.Shelter)
                    return;

                homeRegion.isSearchable = false;
                homeRegion.canContainSpecialItems = false;
                ClearRegionItems(homeRegion);

                RemoveLocationGridRef(
                    Safe.GetField<List<ExpeditionMap.GridRef>>(map, "m_locationGridRefs"),
                    homeGrid);

                List<MapRegion> validSpecialRegions = Safe.GetField<List<MapRegion>>(map, "m_validRegionsForSpecialItems");
                if (validSpecialRegions != null)
                    validSpecialRegions.Remove(homeRegion);

                homeRegion.SetShownOnMap(true);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredMap] SanitizeHomeShelterRegion failed during " + source + ": " + ex);
            }
        }

        private static void CustomPlaceBuildings(ExpeditionMap map, HomeShelterPositionSnapshot snapshot)
        {
            if (map == null)
                return;

            List<ExpeditionMap.GridRef> startingRefs =
                Safe.GetField<List<ExpeditionMap.GridRef>>(map, "m_StartingLocationGridRefs")
                ?? Safe.GetField<List<ExpeditionMap.GridRef>>(map, "StartingLocationGridRefs");
            if (startingRefs == null)
                return;

            Vector2 homeWorld = RequireWorld(snapshot, "PlaceBuildingsNearToShelter");
            ExpeditionMap.GridRef homeGrid = map.WorldPosToGridRef(homeWorld);

            if (!snapshot.GenerateStartingLocations)
            {
                startingRefs.Clear();
                startingRefs.Add(new ExpeditionMap.GridRef(homeGrid.x, homeGrid.y));
                Safe.SetField(map, "m_neighbourPlacementsRemaining", 0);
                return;
            }

            Array scratch = Safe.GetField<Array>(map, "m_mapScratchpad");
            var buildingsNearShelter = Safe.GetField<List<MapRegion.Topography>>(map, "buildingsNearShelter");
            if (scratch == null || buildingsNearShelter == null || buildingsNearShelter.Count == 0)
                return;

            var topographyPool = new List<MapRegion.Topography>(buildingsNearShelter);
            int neighbourPlacementsRemaining = 0;
            int shelterRing = GetShelterNeighbourRing(snapshot);

            startingRefs.Clear();
            for (int i = 0; i < map.shelterNeighbours; ++i)
            {
                int randomX = UnityEngine.Random.Range(homeGrid.x - shelterRing, homeGrid.x + shelterRing + 1);
                int randomY = UnityEngine.Random.Range(homeGrid.y - shelterRing, homeGrid.y + shelterRing + 1);

                int targetX;
                int targetY;
                if (UnityEngine.Random.value <= 0.5f)
                {
                    targetX = randomX;
                    targetY = UnityEngine.Random.value > 0.5f ? homeGrid.y + shelterRing : homeGrid.y - shelterRing;
                }
                else
                {
                    targetY = randomY;
                    targetX = UnityEngine.Random.value > 0.5f ? homeGrid.x + shelterRing : homeGrid.x - shelterRing;
                }

                if (!TryPlaceShelterNeighbourCell(scratch, targetX, targetY, topographyPool, buildingsNearShelter, startingRefs))
                    neighbourPlacementsRemaining++;
            }

            Safe.SetField(map, "m_neighbourPlacementsRemaining", neighbourPlacementsRemaining);
        }

        private static void CustomPlaceBuildingsRemaining(ExpeditionMap map, HomeShelterPositionSnapshot snapshot)
        {
            if (map == null || !snapshot.GenerateStartingLocations)
            {
                if (map != null)
                    Safe.SetField(map, "m_neighbourPlacementsRemaining", 0);
                return;
            }

            int remaining = Safe.GetField<int>(map, "m_neighbourPlacementsRemaining");
            if (remaining <= 0)
                return;

            List<ExpeditionMap.GridRef> startingRefs =
                Safe.GetField<List<ExpeditionMap.GridRef>>(map, "m_StartingLocationGridRefs")
                ?? Safe.GetField<List<ExpeditionMap.GridRef>>(map, "StartingLocationGridRefs");
            if (startingRefs == null)
                return;

            Array scratch = Safe.GetField<Array>(map, "m_mapScratchpad");
            var buildingsNearShelter = Safe.GetField<List<MapRegion.Topography>>(map, "buildingsNearShelter");
            if (scratch == null || buildingsNearShelter == null || buildingsNearShelter.Count == 0)
                return;

            Vector2 homeWorld = RequireWorld(snapshot, "PlaceBuildingsNearToShelter_Remaining");
            ExpeditionMap.GridRef homeGrid = map.WorldPosToGridRef(homeWorld);
            var topographyPool = new List<MapRegion.Topography>(buildingsNearShelter);
            var candidates = new List<ExpeditionMap.GridRef>();
            int shelterRing = GetShelterNeighbourRing(snapshot);

            for (int dx = -shelterRing; dx <= shelterRing; ++dx)
            {
                for (int dy = -shelterRing; dy <= shelterRing; ++dy)
                {
                    if (dx != -shelterRing && dx != shelterRing && dy != -shelterRing && dy != shelterRing)
                        continue;

                    int x = homeGrid.x + dx;
                    int y = homeGrid.y + dy;
                    if (!InBounds(scratch, x, y))
                        continue;

                    object cell = scratch.GetValue(x, y);
                    if (cell != null && IsReplaceableType(cell))
                        candidates.Add(new ExpeditionMap.GridRef(x, y));
                }
            }

            for (int i = candidates.Count - 1; i > 0; --i)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                ExpeditionMap.GridRef swap = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = swap;
            }

            int max = Mathf.Min(remaining, candidates.Count);
            for (int i = 0; i < max; ++i)
            {
                ExpeditionMap.GridRef candidate = candidates[i];
                TryPlaceShelterNeighbourCell(scratch, candidate.x, candidate.y, topographyPool, buildingsNearShelter, startingRefs);
            }

            Safe.SetField(map, "m_neighbourPlacementsRemaining", 0);
        }

        private static bool TryResolveHomeShelterSnapshot(
            string reason,
            bool allowProviders,
            out HomeShelterPositionSnapshot snapshot)
        {
            if (TryGetRegisteredHomeShelterSnapshot(out snapshot))
                return true;

            if (!allowProviders)
                return false;

            if (HomeShelterPlacementProviderRegistry.TryResolve(reason, out snapshot)
                && snapshot != null
                && snapshot.HasWorldPosition
                && IsFinite(snapshot.WorldPosition))
            {
                return true;
            }

            snapshot = null;
            return false;
        }

        private static bool TryGetRegisteredHomeShelterSnapshot(out HomeShelterPositionSnapshot snapshot)
        {
            if (HomeShelterPositionRegistry.TryGetActive(out snapshot)
                && snapshot != null
                && snapshot.HasWorldPosition
                && IsFinite(snapshot.WorldPosition))
            {
                return true;
            }

            snapshot = null;
            return false;
        }

        private static bool TryResolveHomeShelterGrid(ExpeditionMap map, out ExpeditionMap.GridRef grid)
        {
            grid = null;
            if (map == null)
                return false;

            HomeShelterPositionSnapshot snapshot;
            if (!TryGetRegisteredHomeShelterSnapshot(out snapshot))
                return false;

            Vector2 homeWorld;
            if (!TryGetWorld(snapshot, out homeWorld))
                return false;

            grid = map.WorldPosToGridRef(homeWorld);
            return grid != null;
        }

        private static bool ShouldUseVanillaMapMutation(ExpeditionMap map)
        {
            if (map == null || ModRuntime.IsQuitting)
                return true;

            if (!IsSaveLoading())
                return false;

            HomeShelterPositionSnapshot snapshot;
            return !TryGetRegisteredHomeShelterSnapshot(out snapshot);
        }

        private static bool IsSaveLoading()
        {
            try
            {
                return SaveManager.instance != null && SaveManager.instance.isLoading;
            }
            catch
            {
                return false;
            }
        }

        private static int GetShelterNeighbourRing(HomeShelterPositionSnapshot snapshot)
        {
            int ring = snapshot != null ? snapshot.MinimumEdgeDistanceInCells : 2;
            if (ring <= 0)
                ring = 1;
            return ring;
        }

        private static bool TryPlaceShelterNeighbourCell(
            Array scratch,
            int x,
            int y,
            List<MapRegion.Topography> topographyPool,
            List<MapRegion.Topography> poolTemplate,
            List<ExpeditionMap.GridRef> startingRefs)
        {
            if (!InBounds(scratch, x, y) || topographyPool == null || topographyPool.Count == 0)
                return false;

            object cell = scratch.GetValue(x, y);
            if (cell == null || !IsReplaceableType(cell))
                return false;

            int index = UnityEngine.Random.Range(0, topographyPool.Count);
            MapRegion.Topography chosen = topographyPool[index];
            Type cellType = cell.GetType();
            AccessTools.Field(cellType, "type")?.SetValue(cell, chosen);
            AccessTools.Field(cellType, "category")?.SetValue(cell, "Shelter neighbour");
            AccessTools.Field(cellType, "alwaysVisible")?.SetValue(cell, true);

            startingRefs.Add(new ExpeditionMap.GridRef(x, y));

            topographyPool.RemoveAt(index);
            if (topographyPool.Count == 0 && poolTemplate != null && poolTemplate.Count > 0)
                topographyPool.AddRange(poolTemplate);

            return true;
        }

        private static bool ForcePlaceShelterCell(Array scratch, int x, int y)
        {
            if (!InBounds(scratch, x, y))
                return false;

            object cell = scratch.GetValue(x, y);
            if (cell == null)
                return false;

            var typeField = AccessTools.Field(cell.GetType(), "type");
            var categoryField = AccessTools.Field(cell.GetType(), "category");
            if (typeField == null || categoryField == null)
                return false;

            typeField.SetValue(cell, MapRegion.Topography.Shelter);
            categoryField.SetValue(cell, "Shelter");
            return (MapRegion.Topography)typeField.GetValue(cell) == MapRegion.Topography.Shelter;
        }

        private static bool IsReplaceableType(object cell)
        {
            if (cell == null)
                return false;

            var typeField = AccessTools.Field(cell.GetType(), "type");
            if (typeField == null)
                return false;

            MapRegion.Topography current = (MapRegion.Topography)typeField.GetValue(cell);
            return current == MapRegion.Topography.NowhereSpecial
                || current == MapRegion.Topography.Woodland
                || current == MapRegion.Topography.Mountains;
        }

        private static void VerifyHomeShelterCell(ExpeditionMap map, HomeShelterPositionSnapshot snapshot, string source)
        {
            Vector2 homeWorld;
            if (map == null || !TryGetWorld(snapshot, out homeWorld))
                return;

            try
            {
                ExpeditionMap.GridRef grid = map.WorldPosToGridRef(homeWorld);
                MapRegion[,] regions = Safe.GetField<MapRegion[,]>(map, "m_mapRegions");
                if (grid == null || regions == null || !InBounds(regions, grid.x, grid.y))
                {
                    MMLog.WriteError("[ShelteredMap] Home shelter verification failed during " + source
                        + ". world=" + FormatVector(homeWorld)
                        + ", grid=" + FormatGrid(grid)
                        + ", regions=" + FormatArray(regions) + ".");
                    return;
                }

                MapRegion region = regions[grid.x, grid.y];
                if (region == null || region.topography != MapRegion.Topography.Shelter)
                {
                    MMLog.WriteError("[ShelteredMap] Home shelter verification found no Shelter region during " + source
                        + ". world=" + FormatVector(homeWorld)
                        + ", grid=" + FormatGrid(grid)
                        + ", topography=" + (region != null ? region.topography.ToString() : "null") + ".");
                    return;
                }

                MMLog.WriteDebug("[ShelteredMap] Home shelter verified during " + source
                    + ". world=" + FormatVector(homeWorld) + ", grid=" + FormatGrid(grid) + ".");
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredMap] Home shelter verification failed during " + source + ": " + ex);
            }
        }

        private static Vector2 RequireWorld(HomeShelterPositionSnapshot snapshot, string consumer)
        {
            Vector2 world;
            if (TryGetWorld(snapshot, out world))
                return world;

            throw new InvalidOperationException("Home shelter placement unresolved during " + consumer + ".");
        }

        private static bool TryGetWorld(HomeShelterPositionSnapshot snapshot, out Vector2 world)
        {
            world = Vector2.zero;
            if (snapshot == null || !snapshot.HasWorldPosition || !IsFinite(snapshot.WorldPosition))
                return false;

            world = new Vector2(snapshot.WorldPosition.X, snapshot.WorldPosition.Y);
            return true;
        }

        private static bool IsFinite(ExpeditionMapWorldPosition value)
        {
            return ExpeditionMapCoordinateConverter.IsFinite(value.X)
                && ExpeditionMapCoordinateConverter.IsFinite(value.Y);
        }

        private static bool IsNearOrigin(Vector2 value, float tolerance)
        {
            return value.sqrMagnitude < tolerance * tolerance;
        }

        private static bool InBounds(Array array, int x, int y)
        {
            return array != null
                && x >= 0
                && y >= 0
                && x < array.GetLength(0)
                && y < array.GetLength(1);
        }

        private static void RemoveLocationGridRef(List<ExpeditionMap.GridRef> refs, ExpeditionMap.GridRef target)
        {
            if (refs == null || target == null)
                return;

            for (int i = refs.Count - 1; i >= 0; --i)
            {
                ExpeditionMap.GridRef current = refs[i];
                if (current != null && current.x == target.x && current.y == target.y)
                    refs.RemoveAt(i);
            }
        }

        private static void ClearRegionItems(MapRegion region)
        {
            if (region == null)
                return;

            IList items = Safe.GetField<IList>(region, "m_items");
            if (items != null)
                items.Clear();

            Safe.SetField(region, "m_locationItemsCount", 0);
            Safe.SetField(region, "m_commonItemsCount", 0);
        }

        private static string FormatSnapshot(HomeShelterPositionSnapshot snapshot)
        {
            if (snapshot == null)
                return "snapshot=null";

            return "source=" + snapshot.SourceId
                + ", home=" + snapshot.HomeId
                + ", world=" + (snapshot.HasWorldPosition ? FormatWorld(snapshot.WorldPosition) : "none")
                + ", grid=" + (snapshot.HasGridPosition ? snapshot.GridPosition.ToString() : "none")
                + ", starterLocations=" + snapshot.GenerateStartingLocations
                + ", edge=" + snapshot.MinimumEdgeDistanceInCells;
        }

        private static string FormatWorld(ExpeditionMapWorldPosition value)
        {
            return string.Format("({0:F3}, {1:F3})", value.X, value.Y);
        }

        private static string FormatVector(Vector2 value)
        {
            return string.Format("({0:F3}, {1:F3})", value.x, value.y);
        }

        private static string FormatGrid(ExpeditionMap.GridRef grid)
        {
            return grid != null ? "(" + grid.x + "," + grid.y + ")" : "null";
        }

        private static string FormatArray(Array array)
        {
            return array != null ? array.GetLength(0) + "x" + array.GetLength(1) : "null";
        }
    }
}
