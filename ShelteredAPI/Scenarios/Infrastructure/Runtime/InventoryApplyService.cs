using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ShelteredAPI.Content;
using ModAPI.Scenarios;

using ShelteredAPI.Content.Compatibility;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class InventoryApplyService
    {
        private static readonly FieldInfo InventoryRandomStartCountField = typeof(InventoryManager).GetField("numberOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo InventoryRandomStartItemsField = typeof(InventoryManager).GetField("listOfRandomStartingItems", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly object _authoringProjectionSync = new object();
        private Dictionary<string, int> _authoringLastProjected = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        private string _authoringProjectionKey;

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.StartingInventory == null)
                return;

            if (ShouldUseAuthoringProjection(definition))
            {
                ProjectAuthoringStartingInventory(definition, result);
                return;
            }

            if (definition.StartingInventory.Items.Count == 0 && !ShouldApplyRandomStartOverride(definition.StartingInventory))
                return;

            InventoryManager manager = InventoryManager.Instance;
            if (manager == null)
            {
                result.AddMessage("InventoryManager is not ready; inventory changes skipped.");
                return;
            }

            ApplyRandomStartOverride(manager, definition.StartingInventory);
            AddAuthoredStartingInventory(manager, definition.StartingInventory, result);
        }

        public void ResetAuthoringProjection(ScenarioDefinition definition)
        {
            lock (_authoringProjectionSync)
            {
                _authoringProjectionKey = ProjectionKey(definition);
                _authoringLastProjected = SeedProjectedSnapshotFromLive(definition);
            }
        }

        public void AdoptAuthoringProjection(ScenarioDefinition definition)
        {
            lock (_authoringProjectionSync)
            {
                _authoringProjectionKey = ProjectionKey(definition);
                _authoringLastProjected = BuildStartingInventorySnapshot(definition != null ? definition.StartingInventory : null);
            }
        }

        public void ClearAuthoringProjection()
        {
            lock (_authoringProjectionSync)
            {
                _authoringProjectionKey = null;
                _authoringLastProjected = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            }
        }

        public InventoryProjectionResult ProjectAuthoringStartingInventory(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            InventoryProjectionResult projection = new InventoryProjectionResult();
            if (definition == null || definition.StartingInventory == null)
                return projection;

            InventoryManager manager = InventoryManager.Instance;
            if (manager == null)
            {
                if (result != null)
                    result.AddMessage("InventoryManager is not ready; authoring inventory projection skipped.");
                return projection;
            }

            Dictionary<string, int> previous;
            string key = ProjectionKey(definition);
            lock (_authoringProjectionSync)
            {
                if (!string.Equals(_authoringProjectionKey, key, System.StringComparison.Ordinal))
                {
                    _authoringProjectionKey = key;
                    _authoringLastProjected = SeedProjectedSnapshotFromLive(definition);
                }
                previous = CopySnapshot(_authoringLastProjected);
            }

            ApplyRandomStartOverride(manager, definition.StartingInventory);

            Dictionary<string, int> authored = BuildStartingInventorySnapshot(definition.StartingInventory);
            List<InventoryProjectionDelta> deltas = PlanProjectionDeltas(previous, authored);
            for (int i = 0; i < deltas.Count; i++)
            {
                InventoryProjectionDelta delta = deltas[i];
                if (delta == null || string.IsNullOrEmpty(delta.ItemId) || delta.QuantityDelta == 0)
                    continue;

                ItemManager.ItemType type;
                if (!InventoryHelper.ResolveItemType(delta.ItemId, out type))
                {
                    if (result != null)
                        result.AddMessage("Unknown item id skipped during authoring projection: " + delta.ItemId);
                    continue;
                }

                if (delta.QuantityDelta > 0)
                {
                    if (manager.AddNewItems(type, delta.QuantityDelta))
                    {
                        projection.Added += delta.QuantityDelta;
                        if (result != null)
                            result.InventoryChanges += delta.QuantityDelta;
                    }
                    else if (result != null)
                    {
                        result.AddMessage("InventoryManager rejected authoring item '" + delta.ItemId + "' quantity " + delta.QuantityDelta + ".");
                    }
                    continue;
                }

                int requestedRemoval = -delta.QuantityDelta;
                int available = CountLiveStorage(manager, type);
                int removal = System.Math.Min(requestedRemoval, available);
                if (removal <= 0)
                    continue;

                if (manager.RemoveItemsOfType(type, removal))
                {
                    projection.Removed += removal;
                    if (result != null)
                        result.InventoryChanges += removal;
                }
                else if (result != null)
                {
                    result.AddMessage("InventoryManager rejected authoring removal for '" + delta.ItemId + "' quantity " + removal + ".");
                }
            }

            lock (_authoringProjectionSync)
            {
                _authoringProjectionKey = key;
                _authoringLastProjected = authored;
            }

            projection.Stacks = authored.Count;
            return projection;
        }

        internal static List<InventoryProjectionDelta> PlanProjectionDeltas(IDictionary<string, int> previous, IDictionary<string, int> authored)
        {
            Dictionary<string, int> before = NormalizeSnapshot(previous);
            Dictionary<string, int> after = NormalizeSnapshot(authored);
            List<string> ids = new List<string>();
            AddKeys(ids, before);
            AddKeys(ids, after);
            ids.Sort(System.StringComparer.OrdinalIgnoreCase);

            List<InventoryProjectionDelta> deltas = new List<InventoryProjectionDelta>();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                int oldQuantity = before.ContainsKey(id) ? before[id] : 0;
                int newQuantity = after.ContainsKey(id) ? after[id] : 0;
                int delta = newQuantity - oldQuantity;
                if (delta != 0)
                    deltas.Add(new InventoryProjectionDelta(id, delta));
            }

            return deltas;
        }

        internal static Dictionary<string, int> BuildProjectionSeed(IDictionary<string, int> authored, IDictionary<string, int> live)
        {
            Dictionary<string, int> authoredSnapshot = NormalizeSnapshot(authored);
            Dictionary<string, int> liveSnapshot = NormalizeSnapshot(live);
            Dictionary<string, int> seed = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> pair in authoredSnapshot)
            {
                int liveQuantity = liveSnapshot.ContainsKey(pair.Key) ? liveSnapshot[pair.Key] : 0;
                int projectedQuantity = System.Math.Min(pair.Value, liveQuantity);
                if (projectedQuantity > 0)
                    seed[pair.Key] = projectedQuantity;
            }

            return seed;
        }

        internal static bool ShouldApplyRandomStartOverride(StartingInventoryDefinition inventory)
        {
            return inventory != null && inventory.OverrideRandomStart;
        }

        private void AddAuthoredStartingInventory(InventoryManager manager, StartingInventoryDefinition inventory, ScenarioApplyResult result)
        {
            if (manager == null || inventory == null)
                return;

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
            if (manager == null || !ShouldApplyRandomStartOverride(inventory))
                return;

            if (InventoryRandomStartCountField != null)
                InventoryRandomStartCountField.SetValue(manager, 0);

            IList randomItems = InventoryRandomStartItemsField != null ? InventoryRandomStartItemsField.GetValue(manager) as IList : null;
            if (randomItems != null)
                randomItems.Clear();
        }

        private static Dictionary<string, int> SeedProjectedSnapshotFromLive(ScenarioDefinition definition)
        {
            return BuildProjectionSeed(
                BuildStartingInventorySnapshot(definition != null ? definition.StartingInventory : null),
                BuildLiveInventorySnapshot());
        }

        private static Dictionary<string, int> BuildStartingInventorySnapshot(StartingInventoryDefinition inventory)
        {
            Dictionary<string, int> snapshot = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            for (int i = 0; inventory != null && inventory.Items != null && i < inventory.Items.Count; i++)
            {
                ItemEntry entry = inventory.Items[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Quantity <= 0)
                    continue;

                if (!snapshot.ContainsKey(entry.ItemId))
                    snapshot[entry.ItemId] = 0;
                snapshot[entry.ItemId] += entry.Quantity;
            }

            return snapshot;
        }

        private static Dictionary<string, int> BuildLiveInventorySnapshot()
        {
            Dictionary<string, int> snapshot = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            InventoryManager manager = InventoryManager.Instance;
            List<ItemStack> stacks = manager != null ? manager.GetItems() : null;
            for (int i = 0; stacks != null && i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.m_type == ItemManager.ItemType.Undefined || stack.m_count <= 0)
                    continue;

                string itemId = stack.m_type.ToString();
                if (!snapshot.ContainsKey(itemId))
                    snapshot[itemId] = 0;
                snapshot[itemId] += stack.m_count;
            }

            return snapshot;
        }

        private static Dictionary<string, int> NormalizeSnapshot(IDictionary<string, int> source)
        {
            Dictionary<string, int> snapshot = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            if (source == null)
                return snapshot;

            foreach (KeyValuePair<string, int> pair in source)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                    continue;

                if (!snapshot.ContainsKey(pair.Key))
                    snapshot[pair.Key] = 0;
                snapshot[pair.Key] += pair.Value;
            }

            return snapshot;
        }

        private static Dictionary<string, int> CopySnapshot(Dictionary<string, int> source)
        {
            Dictionary<string, int> copy = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, int> pair in source)
                copy[pair.Key] = pair.Value;
            return copy;
        }

        private static void AddKeys(List<string> ids, Dictionary<string, int> snapshot)
        {
            foreach (KeyValuePair<string, int> pair in snapshot)
                if (!ids.Contains(pair.Key))
                    ids.Add(pair.Key);
        }

        private static int CountLiveStorage(InventoryManager manager, ItemManager.ItemType type)
        {
            if (manager == null)
                return 0;

            try
            {
                return manager.GetItemCountInStorage(type, false);
            }
            catch
            {
                return manager.GetNumItemsOfType(type);
            }
        }

        private bool ShouldUseAuthoringProjection(ScenarioDefinition definition)
        {
            if (definition == null)
                return false;

            try
            {
                if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                    return false;
            }
            catch
            {
                return false;
            }

            lock (_authoringProjectionSync)
            {
                return string.Equals(_authoringProjectionKey, ProjectionKey(definition), System.StringComparison.Ordinal);
            }
        }

        private static string ProjectionKey(ScenarioDefinition definition)
        {
            if (definition == null)
                return string.Empty;

            return (definition.Id ?? string.Empty) + "|" + definition.BaseGameMode.ToString();
        }
    }

    internal sealed class InventoryProjectionDelta
    {
        public InventoryProjectionDelta(string itemId, int quantityDelta)
        {
            ItemId = itemId;
            QuantityDelta = quantityDelta;
        }

        public string ItemId { get; private set; }
        public int QuantityDelta { get; private set; }
    }

    internal sealed class InventoryProjectionResult
    {
        public int Added { get; set; }
        public int Removed { get; set; }
        public int Stacks { get; set; }
        public bool Changed { get { return Added > 0 || Removed > 0; } }
    }
}
