using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;

namespace ShelteredAPI.Map.Internal
{
    // One generation-scoped adapter for the existing policy registry. It owns no registrations.
    internal sealed class MapSizeGenerationScope
    {
        private static readonly string[] ContentFields = { "housingEstates", "loneBuildings", "shacks", "reservoirs", "lumberYards",
            "scrapYards", "clearings", "mountainPasses", "caves", "mines", "recyclingCentres", "restaurants" };
        private readonly ExpeditionMap _map;
        private readonly MapGenerationPolicySnapshot _policy;
        private readonly Dictionary<FieldInfo, int> _counts = new Dictionary<FieldInfo, int>();
        private readonly Dictionary<FieldInfo, object> _setup = new Dictionary<FieldInfo, object>();
        private bool _finished;

        private MapSizeGenerationScope(ExpeditionMap map, MapGenerationPolicySnapshot policy) { _map = map; _policy = policy; }

        internal static MapSizeGenerationScope Begin(ExpeditionMap map)
        {
            if (map == null || ModRuntime.IsQuitting) return null;
            MapGenerationPolicySnapshot policy = MapGenerationPolicyRegistry.Resolve();
            // Existing eligibility-only registrations do not opt into generation mutation.
            if (policy.SizePolicies.Count == 0 && policy.LocationDensityMultiplier == 1f && policy.TownDensityMultiplier == 1f) return null;
            MapSizeGenerationScope scope = new MapSizeGenerationScope(map, policy);
            try
            {
                int width = policy.Width ?? map.width, height = policy.Height ?? map.height;
                if (width < 1 || height < 1 || width > 4096 || height > 4096)
                    throw new InvalidOperationException("Map size policy resolved dimensions outside 1..4096 cells.");
                // Allocate both replacements before changing dimensions or either original field.
                FieldInfo regions = AccessTools.Field(typeof(ExpeditionMap), "m_mapRegions");
                FieldInfo scratch = AccessTools.Field(typeof(ExpeditionMap), "m_mapScratchpad");
                Array oldRegions = regions.GetValue(map) as Array, oldScratch = scratch.GetValue(map) as Array;
                Array newRegions = Fits(oldRegions, width, height) ? oldRegions : Array.CreateInstance(regions.FieldType.GetElementType(), width, height);
                Array newScratch = Fits(oldScratch, width, height) ? oldScratch : Array.CreateInstance(scratch.FieldType.GetElementType(), width, height);
                scope.SetSetup(regions, newRegions); scope.SetSetup(scratch, newScratch);
                bool resized = width != map.width || height != map.height;
                scope.SetSetup(AccessTools.Field(typeof(ExpeditionMap), "width"), width);
                scope.SetSetup(AccessTools.Field(typeof(ExpeditionMap), "height"), height);
                if (resized)
                {
                    ExplorationManager manager = ExplorationManager.Instance;
                    if (manager == null) throw new InvalidOperationException("Exploration manager is not ready for a map size change.");
                    if (manager.worldWidth > 0) scope.SetSetup(AccessTools.Field(typeof(ExpeditionMap), "m_mapGridScaleX"), (float)width / manager.worldWidth);
                    if (manager.worldHeight > 0) scope.SetSetup(AccessTools.Field(typeof(ExpeditionMap), "m_mapGridScaleY"), (float)height / manager.worldHeight);
                    scope.SetSetup(AccessTools.Field(typeof(ExpeditionMap), "m_gridPixelSizeX"), manager.mapImageWidth / width);
                    scope.SetSetup(AccessTools.Field(typeof(ExpeditionMap), "m_gridPixelSizeY"), manager.mapImageHeight / height);
                }
                scope.Scale("woodland", policy.TerrainMultiplier, false);
                scope.Scale("mountains", policy.TerrainMultiplier, false);
                float locations = policy.ContentMultiplier * policy.LocationDensityMultiplier;
                float towns = policy.ContentMultiplier * policy.TownDensityMultiplier;
                foreach (string field in ContentFields) scope.Scale(field, locations, true);
                scope.Scale("cities", towns, true); scope.Scale("towns", towns, true); scope.Scale("villages", towns, true);
                return scope;
            }
            catch (Exception error)
            {
                scope.Restore();
                foreach (KeyValuePair<FieldInfo, object> pair in scope._setup) pair.Key.SetValue(map, pair.Value);
                MMLog.WarnOnce("ShelteredMap.SizePolicy", "Map generation policy could not apply: " + error.Message);
                return null;
            }
        }

        private void SetSetup(FieldInfo field, object value)
        {
            if (!_setup.ContainsKey(field)) _setup.Add(field, field.GetValue(_map));
            field.SetValue(_map, value);
        }

        private static bool Fits(Array array, int width, int height)
        {
            return array != null && array.Rank == 2 && array.GetLength(0) == width && array.GetLength(1) == height;
        }

        private void Scale(string name, float factor, bool ceiling)
        {
            if (factor == 1f) return;
            FieldInfo field = AccessTools.Field(typeof(ExpeditionMap), name);
            int original = (int)field.GetValue(_map);
            _counts[field] = original;
            if (original <= 0) return;
            double scaled = ceiling ? Math.Ceiling(original * (double)factor) : Math.Round(original * (double)factor, MidpointRounding.AwayFromZero);
            field.SetValue(_map, (int)Math.Min(int.MaxValue, Math.Max(1, scaled)));
        }

        internal void Finish(Exception error)
        {
            if (_finished) return;
            _finished = true;
            try
            {
                foreach (MapSizePolicy policy in _policy.SizePolicies)
                    SubscriberDispatch.Invoke(policy.GenerationFinished, "ShelteredMap.GenerationFinished", _map, error);
            }
            finally { Restore(); }
        }

        private void Restore()
        {
            foreach (KeyValuePair<FieldInfo, int> pair in _counts) pair.Key.SetValue(_map, pair.Value);
        }
    }
}
