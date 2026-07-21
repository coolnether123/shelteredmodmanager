using System;
using System.Collections.Generic;
using System.Globalization;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Application.Authoring.Supplies{
    /// <summary>
    /// Produces a rough, clearly-approximate sustain estimate for an authored starting
    /// inventory: how many days of water and food it covers for the starting cast, how
    /// much medicine is stocked, and which survival essentials are missing.
    ///
    /// The per-survivor-per-day consumption figures are deliberate round approximations,
    /// not a simulation of Sheltered's stat drain. They are surfaced in the UI as such.
    /// </summary>
    internal static class ScenarioSuppliesBalanceEstimator
    {
        /// <summary>Approximate water units consumed per survivor per day.</summary>
        public const double WaterPerSurvivorPerDay = 1.0;
        /// <summary>Approximate food units consumed per survivor per day.</summary>
        public const double FoodPerSurvivorPerDay = 1.0;
        /// <summary>Fallback starting-cast size when no cast is authored yet.</summary>
        public const int DefaultSurvivorCount = 4;

        private enum Essential
        {
            Other = 0,
            Water = 1,
            Food = 2,
            Medicine = 3
        }

        private static readonly HashSet<string> WaterItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Water"
        };

        private static readonly HashSet<string> FoodItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ration", "Meat", "DesperateMeat"
        };

        private static readonly HashSet<string> MedicineItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FirstAid", "Bandages", "Antibiotics", "AntiRadMedicine", "Valium", "Adrenalin",
            "HomemadeAntibiotics", "HomemadeAntiemetics", "HomemadeAntiRad", "HomemadeAntidepressant",
            "ExpiredAntibiotics", "AnimalRepellent"
        };

        internal sealed class BalanceEstimate
        {
            public int SurvivorCount { get; set; }
            public int WaterUnits { get; set; }
            public int FoodUnits { get; set; }
            public int MedicineUnits { get; set; }
            public double WaterDays { get; set; }
            public double FoodDays { get; set; }
            public bool HasWater { get; set; }
            public bool HasFood { get; set; }
            public bool HasFirstAid { get; set; }

            public List<string> MissingEssentials { get; set; }
        }

        public static string AssumptionsLine()
        {
            return "Approximate only. Assumes about "
                + WaterPerSurvivorPerDay.ToString("0.#", CultureInfo.InvariantCulture)
                + " water and "
                + FoodPerSurvivorPerDay.ToString("0.#", CultureInfo.InvariantCulture)
                + " food per survivor per day; not a simulation of real stat drain.";
        }

        public static BalanceEstimate Estimate(StartingInventoryDefinition inventory, int survivorCount)
        {
            int survivors = survivorCount > 0 ? survivorCount : DefaultSurvivorCount;

            BalanceEstimate estimate = new BalanceEstimate();
            estimate.SurvivorCount = survivors;
            estimate.MissingEssentials = new List<string>();

            if (inventory != null && inventory.Items != null)
            {
                for (int i = 0; i < inventory.Items.Count; i++)
                {
                    ItemEntry entry = inventory.Items[i];
                    if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                        continue;

                    switch (Classify(entry.ItemId))
                    {
                        case Essential.Water:
                            estimate.WaterUnits += entry.Quantity;
                            break;
                        case Essential.Food:
                            estimate.FoodUnits += entry.Quantity;
                            break;
                        case Essential.Medicine:
                            estimate.MedicineUnits += entry.Quantity;
                            break;
                    }
                }
            }

            estimate.HasWater = estimate.WaterUnits > 0;
            estimate.HasFood = estimate.FoodUnits > 0;
            estimate.HasFirstAid = estimate.MedicineUnits > 0;

            double waterPerDay = survivors * WaterPerSurvivorPerDay;
            double foodPerDay = survivors * FoodPerSurvivorPerDay;
            estimate.WaterDays = waterPerDay > 0 ? estimate.WaterUnits / waterPerDay : 0.0;
            estimate.FoodDays = foodPerDay > 0 ? estimate.FoodUnits / foodPerDay : 0.0;

            if (!estimate.HasWater)
                estimate.MissingEssentials.Add("No water");
            if (!estimate.HasFood)
                estimate.MissingEssentials.Add("No food");
            if (!estimate.HasFirstAid)
                estimate.MissingEssentials.Add("No first aid");

            return estimate;
        }

        public static string FormatDays(double days)
        {
            return "about " + days.ToString("0.#", CultureInfo.InvariantCulture) + " day(s)";
        }

        private static Essential Classify(string itemId)
        {
            if (WaterItemIds.Contains(itemId))
                return Essential.Water;
            if (FoodItemIds.Contains(itemId))
                return Essential.Food;
            if (MedicineItemIds.Contains(itemId))
                return Essential.Medicine;

            // Supplement the static name sets with the live catalog category so custom or
            // less common items still classify when the game is running. Safe no-op offline.
            ScenarioInventoryItemCatalogEntry entry = ScenarioInventoryItemCatalog.Resolve(itemId);
            if (entry != null)
            {
                if (entry.Category == ItemManager.ItemCategory.Water)
                    return Essential.Water;
                if (entry.Category == ItemManager.ItemCategory.Food)
                    return Essential.Food;
                if (entry.Category == ItemManager.ItemCategory.Medicine)
                    return Essential.Medicine;
            }

            return Essential.Other;
        }
    }
}
