using System;
using System.Collections.Generic;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Infrastructure.Unity;
namespace ShelteredScenarioEditor.Application.Authoring.Supplies{
    /// <summary>
    /// Starter loadout presets for the Supplies stage. Each preset is a small,
    /// survival-plausible list of item stacks derived from the vanilla item catalog
    /// (<see cref="ScenarioInventoryItemCatalog"/>). Quantities are intentionally modest;
    /// applying a preset replaces the authored starting items.
    /// </summary>
    internal static class ScenarioSuppliesPresetCatalog
    {
        public const string PresetScarce = "scarce";
        public const string PresetBalanced = "balanced";
        public const string PresetMedical = "medical";
        public const string PresetRepair = "repair";
        public const string PresetEmpty = "empty";

        internal sealed class PresetStack
        {
            public PresetStack(ItemManager.ItemType type, int quantity)
            {
                Type = type;
                Quantity = quantity;
            }

            public ItemManager.ItemType Type { get; private set; }
            public int Quantity { get; private set; }
        }

        internal sealed class PresetInfo
        {
            public PresetInfo(string id, string displayName, string description, PresetStack[] stacks)
            {
                Id = id;
                DisplayName = displayName;
                Description = description;
                Stacks = stacks ?? new PresetStack[0];
            }

            public string Id { get; private set; }
            public string DisplayName { get; private set; }
            public string Description { get; private set; }
            public PresetStack[] Stacks { get; private set; }
        }

        private static readonly PresetInfo[] Presets =
        {
            new PresetInfo(
                PresetScarce,
                "Scarce",
                "A lean, high-pressure start with only the bare essentials.",
                new[]
                {
                    new PresetStack(ItemManager.ItemType.Water, 3),
                    new PresetStack(ItemManager.ItemType.Ration, 3),
                    new PresetStack(ItemManager.ItemType.Wood, 2),
                    new PresetStack(ItemManager.ItemType.Bandages, 1)
                }),
            new PresetInfo(
                PresetBalanced,
                "Balanced",
                "A steady, survivable start with food, water, materials, and basic medicine.",
                new[]
                {
                    new PresetStack(ItemManager.ItemType.Water, 8),
                    new PresetStack(ItemManager.ItemType.Ration, 8),
                    new PresetStack(ItemManager.ItemType.Wood, 6),
                    new PresetStack(ItemManager.ItemType.Metal, 4),
                    new PresetStack(ItemManager.ItemType.FirstAid, 2),
                    new PresetStack(ItemManager.ItemType.Bandages, 2),
                    new PresetStack(ItemManager.ItemType.DuctTape, 2)
                }),
            new PresetInfo(
                PresetMedical,
                "Medical-focused",
                "Extra medicine for a hazardous or illness-heavy run.",
                new[]
                {
                    new PresetStack(ItemManager.ItemType.Water, 5),
                    new PresetStack(ItemManager.ItemType.Ration, 5),
                    new PresetStack(ItemManager.ItemType.FirstAid, 3),
                    new PresetStack(ItemManager.ItemType.Bandages, 4),
                    new PresetStack(ItemManager.ItemType.Antibiotics, 2),
                    new PresetStack(ItemManager.ItemType.AntiRadMedicine, 1)
                }),
            new PresetInfo(
                PresetRepair,
                "Repair-focused",
                "Extra building materials and a tool for a construction-heavy run.",
                new[]
                {
                    new PresetStack(ItemManager.ItemType.Water, 5),
                    new PresetStack(ItemManager.ItemType.Ration, 5),
                    new PresetStack(ItemManager.ItemType.Wood, 10),
                    new PresetStack(ItemManager.ItemType.Metal, 8),
                    new PresetStack(ItemManager.ItemType.DuctTape, 4),
                    new PresetStack(ItemManager.ItemType.Nails, 6),
                    new PresetStack(ItemManager.ItemType.Tool_Hammer, 1)
                }),
            new PresetInfo(
                PresetEmpty,
                "Empty",
                "Clear all authored starting items and start from nothing.",
                new PresetStack[0])
        };

        public static int Count
        {
            get { return Presets.Length; }
        }

        public static PresetInfo[] All()
        {
            PresetInfo[] copy = new PresetInfo[Presets.Length];
            Array.Copy(Presets, copy, Presets.Length);
            return copy;
        }

        public static PresetInfo ByIndex(int index)
        {
            if (index < 0 || index >= Presets.Length)
                return null;
            return Presets[index];
        }

        public static PresetInfo ById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < Presets.Length; i++)
            {
                if (string.Equals(Presets[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return Presets[i];
            }
            return null;
        }

        /// <summary>
        /// Materializes the preset's stacks into <see cref="ItemEntry"/> rows using stable
        /// catalog item ids. Returns an empty list for the Empty preset or an unknown id.
        /// </summary>
        public static List<ItemEntry> BuildStacks(string presetId)
        {
            return BuildStacks(ById(presetId));
        }

        public static List<ItemEntry> BuildStacks(PresetInfo preset)
        {
            List<ItemEntry> entries = new List<ItemEntry>();
            if (preset == null)
                return entries;

            for (int i = 0; i < preset.Stacks.Length; i++)
            {
                PresetStack stack = preset.Stacks[i];
                if (stack == null || stack.Quantity <= 0)
                    continue;

                entries.Add(new ItemEntry
                {
                    ItemId = ScenarioInventoryItemCatalog.GetStableItemId(stack.Type),
                    Quantity = stack.Quantity
                });
            }
            return entries;
        }
    }
}
