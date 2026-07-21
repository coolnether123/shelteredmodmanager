using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Domain.Map;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class ScenarioMapTerrainProjection
    {
        private static readonly BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo MapRegionTopographyField = typeof(MapRegion).GetField("m_topography", InstanceAny);
        private static int BaseTerrainMapId = int.MinValue;
        private static BaseTerrainSnapshot BaseTerrain;

        public static int Apply(MapAuthoringDefinition definition, ExpeditionMap map, ScenarioApplyResult result)
        {
            if (definition == null || map == null)
                return 0;

            BaseTerrainSnapshot baseTerrain = GetBaseTerrainSnapshot(map);
            int changed = 0;
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    string terrainId = ResolveTerrainAtCell(definition, map, baseTerrain, x, y);
                    if (string.IsNullOrEmpty(terrainId))
                        continue;

                    string reason;
                    if (TryApplyCell(map, x, y, terrainId, out reason))
                        changed++;
                    else if (result != null)
                        result.AddMessage("Map terrain at " + x.ToString(CultureInfo.InvariantCulture) + ","
                            + y.ToString(CultureInfo.InvariantCulture) + " was skipped: " + reason);
                }
            }

            if (changed > 0 && result != null)
                result.AddMessage("Projected " + changed.ToString(CultureInfo.InvariantCulture) + " authored terrain cell(s) onto the expedition map.");
            return changed;
        }

        public static bool TryApplyCell(ExpeditionMap map, int gridX, int gridY, string terrainId, out string reason)
        {
            reason = null;
            if (map == null)
            {
                reason = "The expedition map is unavailable.";
                return false;
            }

            MapRegion.Topography topography;
            if (!TryParseTerrain(terrainId, out topography))
            {
                reason = "Unknown terrain id '" + (terrainId ?? string.Empty) + "'.";
                return false;
            }

            MapRegion region = map.GetRegionOnMap(new ExpeditionMap.GridRef(gridX, gridY));
            if (region == null)
            {
                reason = "No generated MapRegion exists at that cell.";
                return false;
            }

            string iconId = FindTerrainIcon(map, topography);
            if (MapRegionTopographyField == null)
            {
                reason = "MapRegion terrain reflection is unavailable on this game build.";
                return false;
            }

            MapRegionTopographyField.SetValue(region, topography);
            region.category = topography.ToString();
            region.SetGridReference(new ExpeditionMap.GridRef(gridX, gridY));
            if (!string.IsNullOrEmpty(iconId))
                region.ChangeIcon(iconId);
            if (topography == MapRegion.Topography.Woodland || topography == MapRegion.Topography.Mountains)
                region.SetShownOnMap(true);
            else if (topography == MapRegion.Topography.NowhereSpecial)
                region.SetShownOnMap(false);
            return true;
        }

        public static bool TryParseTerrain(string terrainId, out MapRegion.Topography topography)
        {
            topography = MapRegion.Topography.NowhereSpecial;
            if (string.IsNullOrEmpty(terrainId))
                return false;

            try
            {
                topography = (MapRegion.Topography)Enum.Parse(typeof(MapRegion.Topography), terrainId, true);
                return topography == MapRegion.Topography.NowhereSpecial
                    || topography == MapRegion.Topography.Woodland
                    || topography == MapRegion.Topography.Mountains;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveTerrainAtCell(
            MapAuthoringDefinition definition,
            ExpeditionMap map,
            BaseTerrainSnapshot baseTerrain,
            int gridX,
            int gridY)
        {
            string terrainId = definition.DefaultTerrainId;
            int priority = int.MinValue;
            MapTerrainPatchDefinition winner = null;
            for (int i = 0; definition.TerrainPatches != null && i < definition.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = definition.TerrainPatches[i];
                if (patch == null || !ContainsCell(patch, gridX, gridY) || patch.Priority < priority)
                    continue;

                terrainId = patch.TerrainId;
                priority = patch.Priority;
                winner = patch;
            }

            if (string.Equals(terrainId, ScenarioMapTerrainModes.GeneratedBlend, StringComparison.OrdinalIgnoreCase))
                return ResolveGeneratedBlend(definition, map, baseTerrain, winner, gridX, gridY);
            return terrainId;
        }

        private static string ResolveGeneratedBlend(
            MapAuthoringDefinition definition,
            ExpeditionMap map,
            BaseTerrainSnapshot baseTerrain,
            MapTerrainPatchDefinition patch,
            int gridX,
            int gridY)
        {
            if (baseTerrain == null || baseTerrain.Terrain == null)
                return null;

            MapRegion.Topography baseValue = baseTerrain.Terrain[gridX, gridY];
            if (!IsTerrainTopography(baseValue))
                return null;

            List<MapRegion.Topography> candidates = new List<MapRegion.Topography>();
            AddCandidate(candidates, baseValue, 4);
            int[] dx = new int[] { -1, 1, 0, 0 };
            int[] dy = new int[] { 0, 0, -1, 1 };
            for (int i = 0; i < dx.Length; i++)
            {
                int x = gridX + dx[i];
                int y = gridY + dy[i];
                if (x < 0 || y < 0 || x >= map.width || y >= map.height)
                    continue;

                AddCandidate(candidates, baseTerrain.Terrain[x, y], 1);
                MapRegion.Topography manual;
                if (TryResolveManualTerrain(definition, x, y, out manual))
                    AddCandidate(candidates, manual, 4);
            }

            if (candidates.Count == 0)
                return baseValue.ToString();

            string key = (map != null ? map.randomSeed.ToString(CultureInfo.InvariantCulture) : "0") + "|"
                + (patch != null ? patch.Id : "generated-blend") + "|"
                + gridX.ToString(CultureInfo.InvariantCulture) + "|" + gridY.ToString(CultureInfo.InvariantCulture);
            int hash = ContentRegistry.StableContentIdHash(key);
            int index = (int)((uint)hash % (uint)candidates.Count);
            return candidates[index].ToString();
        }

        private static bool TryResolveManualTerrain(MapAuthoringDefinition definition, int gridX, int gridY, out MapRegion.Topography topography)
        {
            topography = MapRegion.Topography.NowhereSpecial;
            string terrainId = definition != null ? definition.DefaultTerrainId : null;
            int priority = int.MinValue;
            for (int i = 0; definition != null && definition.TerrainPatches != null && i < definition.TerrainPatches.Count; i++)
            {
                MapTerrainPatchDefinition patch = definition.TerrainPatches[i];
                if (patch == null || !ContainsCell(patch, gridX, gridY) || patch.Priority < priority)
                    continue;
                if (string.Equals(patch.TerrainId, ScenarioMapTerrainModes.GeneratedBlend, StringComparison.OrdinalIgnoreCase))
                    continue;

                terrainId = patch.TerrainId;
                priority = patch.Priority;
            }
            return TryParseTerrain(terrainId, out topography);
        }

        private static void AddCandidate(List<MapRegion.Topography> candidates, MapRegion.Topography topography, int weight)
        {
            if (candidates == null || !IsTerrainTopography(topography))
                return;
            for (int i = 0; i < weight; i++)
                candidates.Add(topography);
        }

        private static bool IsTerrainTopography(MapRegion.Topography topography)
        {
            return topography == MapRegion.Topography.NowhereSpecial
                || topography == MapRegion.Topography.Woodland
                || topography == MapRegion.Topography.Mountains;
        }

        private static bool ContainsCell(MapTerrainPatchDefinition patch, int gridX, int gridY)
        {
            float x = gridX + 0.5f;
            float y = gridY + 0.5f;
            if (patch.Shape == MapTerrainBrushShape.Circle)
            {
                float dx = x - patch.X;
                float dy = y - patch.Y;
                return dx * dx + dy * dy <= patch.Radius * patch.Radius;
            }

            if (patch.Shape == MapTerrainBrushShape.Polygon)
                return ContainsPolygonPoint(patch.Points, x, y);

            return x >= patch.X && y >= patch.Y && x < patch.X + patch.Width && y < patch.Y + patch.Height;
        }

        private static bool ContainsPolygonPoint(List<MapPointDefinition> points, float x, float y)
        {
            if (points == null || points.Count < 3)
                return false;

            bool inside = false;
            int previous = points.Count - 1;
            for (int current = 0; current < points.Count; current++)
            {
                MapPointDefinition a = points[current];
                MapPointDefinition b = points[previous];
                if (a != null && b != null
                    && ((a.Y > y) != (b.Y > y))
                    && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }
                previous = current;
            }
            return inside;
        }

        private static string FindTerrainIcon(ExpeditionMap map, MapRegion.Topography topography)
        {
            BaseTerrainSnapshot snapshot = GetBaseTerrainSnapshot(map);
            string cached;
            if (snapshot != null && snapshot.Icons.TryGetValue(topography, out cached))
                return cached;

            for (int x = 0; map != null && x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    MapRegion sample = map.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    if (sample == null || sample.topography != topography)
                        continue;

                    UISprite sprite = sample.GetComponent<UISprite>();
                    if (sprite != null && !string.IsNullOrEmpty(sprite.spriteName))
                    {
                        if (snapshot != null)
                            snapshot.Icons[topography] = sprite.spriteName;
                        return sprite.spriteName;
                    }
                }
            }
            return null;
        }

        private static BaseTerrainSnapshot GetBaseTerrainSnapshot(ExpeditionMap map)
        {
            if (map == null)
                return null;

            int id = map.GetInstanceID();
            BaseTerrainSnapshot snapshot = BaseTerrain;
            if (BaseTerrainMapId == id
                && snapshot != null
                && snapshot.Width == map.width
                && snapshot.Height == map.height)
            {
                return snapshot;
            }

            snapshot = new BaseTerrainSnapshot(map.width, map.height);
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    MapRegion region = map.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    if (region == null)
                        continue;
                    snapshot.Terrain[x, y] = region.topography;
                    UISprite sprite = region.GetComponent<UISprite>();
                    if (sprite != null && !string.IsNullOrEmpty(sprite.spriteName) && !snapshot.Icons.ContainsKey(region.topography))
                        snapshot.Icons.Add(region.topography, sprite.spriteName);
                }
            }
            // Sheltered exposes one active ExpeditionMap singleton. Retaining only
            // its snapshot avoids leaking destroyed Unity map instances across
            // editor reloads while preserving the generated base for live previews.
            BaseTerrainMapId = id;
            BaseTerrain = snapshot;
            return snapshot;
        }

        private sealed class BaseTerrainSnapshot
        {
            public BaseTerrainSnapshot(int width, int height)
            {
                Width = width;
                Height = height;
                Terrain = new MapRegion.Topography[width, height];
                Icons = new Dictionary<MapRegion.Topography, string>();
            }

            public int Width { get; private set; }
            public int Height { get; private set; }
            public MapRegion.Topography[,] Terrain { get; private set; }
            public Dictionary<MapRegion.Topography, string> Icons { get; private set; }
        }
    }
}
