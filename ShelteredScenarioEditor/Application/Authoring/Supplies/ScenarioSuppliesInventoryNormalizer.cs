using System;
using System.Collections.Generic;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredScenarioEditor.Application.Authoring.Supplies{
    /// <summary>
    /// Normalizes an authored starting-item list: merges duplicate stacks that share an
    /// item id (summing quantities) and removes zero- or negative-quantity stacks. This
    /// mirrors the quantity policy the inventory apply path already uses for scheduled
    /// stock and live projection (see InventoryApplyService.NormalizeSnapshot): duplicates
    /// are summed and non-positive quantities are dropped.
    /// </summary>
    internal static class ScenarioSuppliesInventoryNormalizer
    {
        internal sealed class NormalizeResult
        {
            /// <summary>Stacks removed because they duplicated an earlier item id.</summary>
            public int MergedStacks { get; set; }
            /// <summary>Stacks removed because their quantity was zero, negative, or their id was empty.</summary>
            public int RemovedStacks { get; set; }

            public bool ChangedAnything
            {
                get { return MergedStacks > 0 || RemovedStacks > 0; }
            }
        }

        /// <summary>
        /// Normalizes <paramref name="items"/> in place, preserving first-seen ordering.
        /// </summary>
        public static NormalizeResult Normalize(List<ItemEntry> items)
        {
            NormalizeResult result = new NormalizeResult();
            if (items == null)
                return result;

            List<ItemEntry> ordered = new List<ItemEntry>();
            Dictionary<string, ItemEntry> byId = new Dictionary<string, ItemEntry>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < items.Count; i++)
            {
                ItemEntry entry = items[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                {
                    result.RemovedStacks++;
                    continue;
                }

                ItemEntry existing;
                if (byId.TryGetValue(entry.ItemId, out existing))
                {
                    existing.Quantity += entry.Quantity;
                    result.MergedStacks++;
                }
                else
                {
                    byId[entry.ItemId] = entry;
                    ordered.Add(entry);
                }
            }

            items.Clear();
            items.AddRange(ordered);
            return result;
        }

        /// <summary>True when <paramref name="items"/> holds duplicate ids or non-positive quantities.</summary>
        public static bool NeedsNormalize(List<ItemEntry> items)
        {
            if (items == null)
                return false;

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                ItemEntry entry = items[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                    return true;
                if (!seen.Add(entry.ItemId))
                    return true;
            }
            return false;
        }
    }
}
