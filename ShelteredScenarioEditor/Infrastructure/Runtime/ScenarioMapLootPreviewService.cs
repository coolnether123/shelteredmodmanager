using System;
using System.Collections.Generic;

using ModAPI.Core;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredScenarioEditor.Infrastructure.Runtime
{
    internal sealed class ScenarioMapLootPreview
    {
        public ScenarioMapLootPreview()
        {
            ExactRoll = new List<ScenarioMapLootEntrySnapshot>();
            Distribution = new List<ScenarioMapLootDistributionEntry>();
        }

        public int FixedSeed { get; set; }
        public int SimulationSeed { get; set; }
        public int RollCount { get; set; }
        public string Error { get; set; }
        public List<ScenarioMapLootEntrySnapshot> ExactRoll { get; private set; }
        public List<ScenarioMapLootDistributionEntry> Distribution { get; private set; }
    }

    internal sealed class ScenarioMapLootDistributionEntry
    {
        public string ItemId { get; set; }
        public bool Hidden { get; set; }
        public int RollsContainingItem { get; set; }
        public double PercentOfRolls { get; set; }
        public double AverageQuantityPerRoll { get; set; }
    }

    /// <summary>
    /// Read-only authoring preview. Every sample delegates to the runtime loot planner;
    /// this class only aggregates its returned rolls.
    /// </summary>
    internal static class ScenarioMapLootPreviewService
    {
        public const int DefaultSimulationRolls = 1000;

        public static ScenarioMapLootPreview Build(
            ScenarioDefinition definition,
            MapLocationDefinition location,
            int simulationSeed,
            int rollCount)
        {
            ScenarioMapLootPreview preview = new ScenarioMapLootPreview();
            preview.FixedSeed = ModRandom.CurrentSeed;
            preview.SimulationSeed = simulationSeed;
            preview.RollCount = Math.Max(0, rollCount);

            MapLootTableDefinition table = FindTable(definition, location != null ? location.LootTableId : null);
            if (location == null)
            {
                preview.Error = "Select an authored location to preview loot.";
                return preview;
            }
            if (string.IsNullOrEmpty(location.LootTableId))
            {
                preview.Error = "Assign a loot table to preview its rolls.";
                return preview;
            }
            if (table == null)
            {
                preview.Error = "Loot table '" + location.LootTableId + "' was not found.";
                return preview;
            }

            preview.ExactRoll.AddRange(ShelteredScenarioRuntime.PlanMapLoot(definition, location, table));
            BuildDistribution(preview, definition, location, table);
            return preview;
        }

        private static void BuildDistribution(
            ScenarioMapLootPreview preview,
            ScenarioDefinition definition,
            MapLocationDefinition location,
            MapLootTableDefinition table)
        {
            Dictionary<string, DistributionAccumulator> totals = new Dictionary<string, DistributionAccumulator>(StringComparer.OrdinalIgnoreCase);
            for (int rollIndex = 0; rollIndex < preview.RollCount; rollIndex++)
            {
                int rollSeed = unchecked(preview.SimulationSeed + rollIndex);
                ScenarioMapLootEntrySnapshot[] roll = ShelteredScenarioRuntime.PlanMapLoot(
                    definition, location, table, rollSeed);
                for (int itemIndex = 0; roll != null && itemIndex < roll.Length; itemIndex++)
                {
                    ScenarioMapLootEntrySnapshot item = roll[itemIndex];
                    if (item == null || string.IsNullOrEmpty(item.ItemId) || item.Quantity <= 0)
                        continue;

                    string key = (item.Hidden ? "hidden|" : "visible|") + item.ItemId;
                    DistributionAccumulator total;
                    if (!totals.TryGetValue(key, out total))
                    {
                        total = new DistributionAccumulator { ItemId = item.ItemId, Hidden = item.Hidden };
                        totals[key] = total;
                    }
                    total.RollsContainingItem++;
                    total.TotalQuantity += item.Quantity;
                }
            }

            foreach (DistributionAccumulator total in totals.Values)
            {
                preview.Distribution.Add(new ScenarioMapLootDistributionEntry
                {
                    ItemId = total.ItemId,
                    Hidden = total.Hidden,
                    RollsContainingItem = total.RollsContainingItem,
                    PercentOfRolls = preview.RollCount > 0 ? (100.0 * total.RollsContainingItem) / preview.RollCount : 0.0,
                    AverageQuantityPerRoll = preview.RollCount > 0 ? ((double)total.TotalQuantity) / preview.RollCount : 0.0
                });
            }

            preview.Distribution.Sort(delegate(ScenarioMapLootDistributionEntry left, ScenarioMapLootDistributionEntry right)
            {
                int percent = right.PercentOfRolls.CompareTo(left.PercentOfRolls);
                return percent != 0 ? percent : string.Compare(left.ItemId, right.ItemId, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static MapLootTableDefinition FindTable(ScenarioDefinition definition, string id)
        {
            if (definition == null || definition.Map == null || definition.Map.LootTables == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < definition.Map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = definition.Map.LootTables[i];
                if (table != null && string.Equals(table.Id, id, StringComparison.OrdinalIgnoreCase))
                    return table;
            }
            return null;
        }

        private sealed class DistributionAccumulator
        {
            public string ItemId;
            public bool Hidden;
            public int RollsContainingItem;
            public long TotalQuantity;
        }
    }
}
