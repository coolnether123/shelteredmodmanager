using System.Collections;
using System.Reflection;
using ModAPI.Items;
using ModAPI.Scenarios;
using ShelteredAPI.Content;

namespace ShelteredAPI.Scenarios
{
    internal sealed class InventoryApplyService
    {
        private static readonly FieldInfo InventoryRandomStartCountField = typeof(InventoryManager).GetField("numberOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartItemsField = typeof(InventoryManager).GetField("listOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.StartingInventory == null || definition.StartingInventory.Items.Count == 0)
                return;

            InventoryManager manager = InventoryManager.Instance;
            if (manager == null)
            {
                result.AddMessage("InventoryManager is not ready; inventory changes skipped.");
                return;
            }

            if (definition.StartingInventory.OverrideRandomStart)
            {
                if (InventoryRandomStartCountField != null)
                    InventoryRandomStartCountField.SetValue(manager, 0);

                IList randomItems = InventoryRandomStartItemsField != null ? InventoryRandomStartItemsField.GetValue(manager) as IList : null;
                if (randomItems != null)
                    randomItems.Clear();
            }

            ContentInjector.NotifyManagerReady("ScenarioApplyCoordinator");
            for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                    continue;

                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(entry.ItemId, out type))
                {
                    result.AddMessage("Unknown item id skipped: " + entry.ItemId);
                    continue;
                }

                if (manager.AddNewItems(type, entry.Quantity))
                    result.InventoryChanges += entry.Quantity;
                else
                    result.AddMessage("InventoryManager rejected item '" + entry.ItemId + "' quantity " + entry.Quantity + ".");
            }
        }
    }
}
