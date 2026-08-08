using System.Collections;
using System.Reflection;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Infrastructure.Runtime
{
    /// <summary>
    /// Applies the inventory portion of an installed scenario when its run starts.
    /// Live editor projection belongs to ShelteredScenarioEditor and deliberately
    /// does not share state with this runtime path.
    /// </summary>
    internal sealed class InventoryApplyService
    {
        private static readonly FieldInfo InventoryRandomStartCountField = typeof(InventoryManager).GetField(
            "numberOfRandomStartingItems",
            BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartItemsField = typeof(InventoryManager).GetField(
            "listOfRandomStartingItems",
            BindingFlags.NonPublic | BindingFlags.Instance);

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            StartingInventoryDefinition inventory = definition != null ? definition.StartingInventory : null;
            if (inventory == null)
                return;

            if ((inventory.Items == null || inventory.Items.Count == 0) && !inventory.OverrideRandomStart)
                return;

            InventoryManager manager = InventoryManager.Instance;
            if (manager == null)
            {
                if (result != null)
                    result.AddMessage("InventoryManager is not ready; inventory changes skipped.");
                return;
            }

            ApplyRandomStartOverride(manager, inventory);
            AddStartingInventory(manager, inventory, result);
        }

        private static void AddStartingInventory(
            InventoryManager manager,
            StartingInventoryDefinition inventory,
            ScenarioApplyResult result)
        {
            ContentInjector.NotifyManagerReady("ScenarioApplyCoordinator");
            for (int i = 0; inventory.Items != null && i < inventory.Items.Count; i++)
            {
                ItemEntry entry = inventory.Items[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                    continue;

                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(entry.ItemId, out type))
                {
                    if (result != null)
                        result.AddMessage("Unknown item id skipped: " + entry.ItemId);
                    continue;
                }

                if (manager.AddNewItems(type, entry.Quantity))
                {
                    if (result != null)
                        result.InventoryChanges += entry.Quantity;
                }
                else if (result != null)
                {
                    result.AddMessage("InventoryManager rejected item '" + entry.ItemId + "' quantity " + entry.Quantity + ".");
                }
            }
        }

        private static void ApplyRandomStartOverride(InventoryManager manager, StartingInventoryDefinition inventory)
        {
            if (!inventory.OverrideRandomStart)
                return;

            if (InventoryRandomStartCountField != null)
                InventoryRandomStartCountField.SetValue(manager, 0);

            IList randomItems = InventoryRandomStartItemsField != null
                ? InventoryRandomStartItemsField.GetValue(manager) as IList
                : null;
            if (randomItems != null)
                randomItems.Clear();
        }
    }
}
