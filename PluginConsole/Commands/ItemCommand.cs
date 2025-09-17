using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ConsoleCommands
{
    public class ItemCommand : ICommand
    {
        public string Name => "item";
        public string Description => "Adds or sets items in the inventory. Usage: item <add|set> <item_name> <quantity>";

        public string Execute(string[] args)
        {
            if (args.Length == 0)
            {
                return ListAllItems();
            }

            if (args.Length < 3)
            {
                return "Invalid arguments. Usage: item <add|set> <item_name> <quantity>";
            }

            string subCommand = args[0].ToLower();
            string itemName = args[1];
            if (!int.TryParse(args[2], out int quantity))
            {
                return "Invalid quantity.";
            }

            switch (subCommand)
            {
                case "add":
                    return AddItem(itemName, quantity);
                case "set":
                    return SetItem(itemName, quantity);
                default:
                    return "Invalid sub-command. Use 'add' or 'set'.";
            }
        }

        private string ListAllItems()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Available items:");
            sb.AppendLine("Food");
            sb.AppendLine("Water");
            foreach (ItemManager.ItemType itemType in Enum.GetValues(typeof(ItemManager.ItemType)))
            {
                if (itemType != ItemManager.ItemType.Undefined)
                {
                    sb.AppendLine(itemType.ToString());
                }
            }
            return sb.ToString();
        }

        private string AddItem(string itemName, int quantity)
        {
            if (string.Equals(itemName, "food", StringComparison.OrdinalIgnoreCase))
            {
                if (FoodManager.Instance == null) return "FoodManager not found.";
                FoodManager.Instance.AddRations(quantity);
                return $"Added {quantity} food. New total: {FoodManager.Instance.Rations}";
            }
            if (string.Equals(itemName, "water", StringComparison.OrdinalIgnoreCase))
            {
                if (WaterManager.Instance == null) return "WaterManager not found.";
                WaterManager.Instance.AddWater(quantity);
                return $"Added {quantity} water. New total: {WaterManager.Instance.StoredWater}";
            }

            try
            {
                ItemManager.ItemType itemType = (ItemManager.ItemType)Enum.Parse(typeof(ItemManager.ItemType), itemName, true);
                if (Enum.IsDefined(typeof(ItemManager.ItemType), itemType))
                {
                    if (InventoryManager.Instance == null) return "InventoryManager not found.";
                    InventoryManager.Instance.AddNewItems(itemType, quantity);
                    return $"Added {quantity} of {itemType}.";
                }
            }
            catch (ArgumentException)
            {
                // The string could not be parsed
            }

            return $"Item '{itemName}' not found.";
        }

        private string SetItem(string itemName, int quantity)
        {
            if (string.Equals(itemName, "food", StringComparison.OrdinalIgnoreCase))
            {
                if (FoodManager.Instance == null) return "FoodManager not found.";
                int currentFood = FoodManager.Instance.Rations;
                if (quantity > currentFood)
                {
                    FoodManager.Instance.AddRations(quantity - currentFood);
                }
                else
                {
                    FoodManager.Instance.TakeRations(currentFood - quantity);
                }
                return $"Set food to {quantity}.";
            }
            if (string.Equals(itemName, "water", StringComparison.OrdinalIgnoreCase))
            {
                if (WaterManager.Instance == null) return "WaterManager not found.";
                float currentWater = WaterManager.Instance.StoredWater;
                if (quantity > currentWater)
                {
                    WaterManager.Instance.AddWater(quantity - currentWater);
                }
                else
                {
                    WaterManager.Instance.UseWater(currentWater - quantity);
                }
                return $"Set water to {quantity}.";
            }

            try
            {
                ItemManager.ItemType itemType = (ItemManager.ItemType)Enum.Parse(typeof(ItemManager.ItemType), itemName, true);
                if (Enum.IsDefined(typeof(ItemManager.ItemType), itemType))
                {
                    if (InventoryManager.Instance == null) return "InventoryManager not found.";
                    InventoryManager.Instance.RemoveAllItemsOfType(itemType);
                    InventoryManager.Instance.AddNewItems(itemType, quantity);
                    return $"Set {itemType} to {quantity}.";
                }
            }
            catch (ArgumentException)
            {
                // The string could not be parsed
            }

            return $"Item '{itemName}' not found.";
        }
    }
}
