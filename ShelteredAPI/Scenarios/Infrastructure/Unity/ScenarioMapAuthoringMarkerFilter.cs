using System.Collections.Generic;

using UnityEngine;

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
    }
}
