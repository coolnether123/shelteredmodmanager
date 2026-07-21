using System;
using System.Collections.Generic;

using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;

namespace ShelteredAPI.Scenarios.Infrastructure.Unity
{
    internal static class ScenarioMapAuthoringMarkerFilter
    {
        private static readonly Dictionary<UISprite, Color> VanillaColors = new Dictionary<UISprite, Color>();

        public static void ApplyVanillaRegionFilter()
        {
            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null)
                return;

            bool visible = ScenarioMapAuthoringFilterState.IsVisible(ScenarioMapAuthoringFilter.VanillaRegions);
            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    MapRegion region = map.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    UISprite sprite = region != null ? region.GetComponent<UISprite>() : null;
                    if (sprite == null)
                        continue;
                    Color original;
                    if (!VanillaColors.TryGetValue(sprite, out original))
                    {
                        original = sprite.color;
                        VanillaColors[sprite] = original;
                    }
                    sprite.color = new Color(original.r, original.g, original.b, visible ? original.a : original.a * 0.16f);
                }
            }
        }

        public static void RestoreVanillaRegionColors()
        {
            foreach (KeyValuePair<UISprite, Color> entry in VanillaColors)
            {
                if (entry.Key != null)
                    entry.Key.color = entry.Value;
            }
            VanillaColors.Clear();
        }

        public static void ApplyTerrainBrushPreview(ScenarioAuthoringState state, int centreX, int centreY, bool hasHoveredGrid)
        {
            if (state == null || !hasHoveredGrid || string.IsNullOrEmpty(state.MapAuthoringMode)
                || !state.MapAuthoringMode.StartsWith("terrain:", StringComparison.OrdinalIgnoreCase)
                || ExpeditionMap.Instance == null)
            {
                return;
            }

            int size = state.MapTerrainBrushSize > 0 ? state.MapTerrainBrushSize : 3;
            int half = size / 2;
            bool square = string.Equals(state.MapTerrainBrushShape, "square", StringComparison.OrdinalIgnoreCase);
            Color tint = ResolveBrushTint(state.MapAuthoringMode);
            for (int x = centreX - half; x <= centreX + half; x++)
            {
                for (int y = centreY - half; y <= centreY + half; y++)
                {
                    if (x < 0 || y < 0 || x >= ExpeditionMap.Instance.width || y >= ExpeditionMap.Instance.height)
                        continue;
                    if (!square)
                    {
                        float dx = x - centreX;
                        float dy = y - centreY;
                        float radius = size * 0.5f;
                        if (dx * dx + dy * dy > radius * radius)
                            continue;
                    }

                    MapRegion region = ExpeditionMap.Instance.GetRegionOnMap(new ExpeditionMap.GridRef(x, y));
                    UISprite sprite = region != null ? region.GetComponent<UISprite>() : null;
                    if (sprite == null)
                        continue;

                    Color original;
                    if (!VanillaColors.TryGetValue(sprite, out original))
                    {
                        original = sprite.color;
                        VanillaColors[sprite] = original;
                    }
                    sprite.color = new Color(
                        (original.r + tint.r) * 0.5f,
                        (original.g + tint.g) * 0.5f,
                        (original.b + tint.b) * 0.5f,
                        Math.Max(original.a, 0.72f));
                }
            }
        }

        private static Color ResolveBrushTint(string mode)
        {
            if (mode.IndexOf("Woodland", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.2f, 1f, 0.35f, 1f);
            if (mode.IndexOf("Mountains", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(1f, 0.72f, 0.25f, 1f);
            if (mode.IndexOf("GeneratedBlend", StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.72f, 0.4f, 1f, 1f);
            return new Color(0.3f, 0.8f, 1f, 1f);
        }
    }
}
