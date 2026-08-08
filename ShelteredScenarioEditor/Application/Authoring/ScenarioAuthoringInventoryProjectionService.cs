using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Infrastructure.Unity;
using UnityEngine;

namespace ShelteredScenarioEditor.Application.Authoring
{
    /// <summary>
    /// Owns the editor's reversible projection of draft inventory into the live shelter.
    /// This state must never enter ShelteredAPI's installed-scenario apply pipeline.
    /// </summary>
    internal sealed class ScenarioAuthoringInventoryProjectionService
    {
        private const float LiveTruthPollSeconds = 1f;
        private readonly object _sync = new object();
        private Dictionary<string, int> _lastProjected = NewSnapshot();
        private string _projectionKey;
        private float _lastLiveTruthPollRealtime;

        public void ResetForCurrentWorld(ScenarioEditorSession session)
        {
            if (session == null)
                return;

            lock (_sync)
            {
                _projectionKey = ProjectionKey(session.WorkingDefinition);
                _lastProjected = BuildProjectionSeed(
                    BuildStartingInventorySnapshot(session.WorkingDefinition != null ? session.WorkingDefinition.StartingInventory : null),
                    BuildLiveInventorySnapshot());
            }
        }

        public bool TryProject(ScenarioEditorSession session, string reason, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
                return false;
            if (!CanProjectInCurrentWorld(out message))
                return false;

            InventoryProjectionResult projection = Project(session.WorkingDefinition, out message);
            string status = projection.Changed
                ? "Shelter storage synced (+" + projection.Added + "/-" + projection.Removed + ")."
                : "Shelter storage already matches starting items.";
            message = AppendMessage(status, message);
            MMLog.WriteInfo("[ScenarioAuthoringInventoryProjection] " + message
                + " reason=" + (reason ?? "unspecified")
                + ", scenario=" + (session.WorkingDefinition.Id ?? "<none>") + ".");
            return true;
        }

        public void UpdateLiveTruth(ScenarioEditorSession session)
        {
            if (session == null || session.WorkingDefinition == null)
                return;

            float now;
            try { now = Time.realtimeSinceStartup; }
            catch { now = _lastLiveTruthPollRealtime + LiveTruthPollSeconds; }
            if (_lastLiveTruthPollRealtime > 0f && now - _lastLiveTruthPollRealtime < LiveTruthPollSeconds)
                return;

            _lastLiveTruthPollRealtime = now;
            string ignored;
            TryReconcileLiveTruth(session, "live truth poll", out ignored);
        }

        public bool TryReconcileLiveTruth(ScenarioEditorSession session, string reason, out string message)
        {
            message = null;
            if (session == null || session.WorkingDefinition == null)
                return false;
            if (!CanProjectInCurrentWorld(out message))
                return false;

            ScenarioDefinition definition = session.WorkingDefinition;
            if (definition.StartingInventory == null)
                definition.StartingInventory = new StartingInventoryDefinition();

            string key = ProjectionKey(definition);
            Dictionary<string, int> previous = GetPreviousProjection(key, definition);
            Dictionary<string, int> authored = BuildStartingInventorySnapshot(definition.StartingInventory);
            if (!SnapshotsEqual(previous, authored))
            {
                InventoryProjectionResult projected = Project(definition, out message);
                message = AppendMessage(
                    projected.Changed
                        ? "Shelter storage synced (+" + projected.Added + "/-" + projected.Removed + ")."
                        : "Shelter storage already matches starting items.",
                    message);
                return true;
            }

            Dictionary<string, int> live = BuildLiveInventorySnapshot();
            if (SnapshotsEqual(previous, live))
            {
                message = "Shelter storage already matches starting items.";
                return true;
            }

            List<InventoryProjectionDelta> deltas = PlanProjectionDeltas(previous, live);
            ReplaceStartingInventory(definition.StartingInventory, live);
            SetProjection(key, live);
            session.MarkDraftChanged(ScenarioDirtySection.Inventory, ScenarioEditCategory.Inventory);

            InventoryProjectionResult adopted = Summarize(deltas);
            adopted.DraftUpdated = true;
            message = "Shelter storage adopted as scenario starting inventory (+" + adopted.Added + "/-" + adopted.Removed + ").";
            MMLog.WriteInfo("[ScenarioAuthoringInventoryProjection] " + message
                + " reason=" + (reason ?? "unspecified")
                + ", scenario=" + (definition.Id ?? "<none>") + ".");
            return true;
        }

        public void Clear()
        {
            _lastLiveTruthPollRealtime = 0f;
            lock (_sync)
            {
                _projectionKey = null;
                _lastProjected = NewSnapshot();
            }
        }

        internal static List<InventoryProjectionDelta> PlanProjectionDeltas(
            IDictionary<string, int> previous,
            IDictionary<string, int> authored)
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
                if (newQuantity != oldQuantity)
                    deltas.Add(new InventoryProjectionDelta(id, newQuantity - oldQuantity));
            }
            return deltas;
        }

        internal static Dictionary<string, int> BuildProjectionSeed(
            IDictionary<string, int> authored,
            IDictionary<string, int> live)
        {
            Dictionary<string, int> authoredSnapshot = NormalizeSnapshot(authored);
            Dictionary<string, int> liveSnapshot = NormalizeSnapshot(live);
            Dictionary<string, int> seed = NewSnapshot();
            foreach (KeyValuePair<string, int> pair in authoredSnapshot)
            {
                int liveQuantity = liveSnapshot.ContainsKey(pair.Key) ? liveSnapshot[pair.Key] : 0;
                int projectedQuantity = System.Math.Min(pair.Value, liveQuantity);
                if (projectedQuantity > 0)
                    seed[pair.Key] = projectedQuantity;
            }
            return seed;
        }

        internal static bool SnapshotsEqual(IDictionary<string, int> left, IDictionary<string, int> right)
        {
            Dictionary<string, int> normalizedLeft = NormalizeSnapshot(left);
            Dictionary<string, int> normalizedRight = NormalizeSnapshot(right);
            if (normalizedLeft.Count != normalizedRight.Count)
                return false;
            foreach (KeyValuePair<string, int> pair in normalizedLeft)
            {
                int quantity;
                if (!normalizedRight.TryGetValue(pair.Key, out quantity) || quantity != pair.Value)
                    return false;
            }
            return true;
        }

        private InventoryProjectionResult Project(ScenarioDefinition definition, out string warning)
        {
            warning = null;
            InventoryManager manager = InventoryManager.Instance;
            string key = ProjectionKey(definition);
            Dictionary<string, int> previous = GetPreviousProjection(key, definition);
            Dictionary<string, int> authored = BuildStartingInventorySnapshot(definition.StartingInventory);
            List<InventoryProjectionDelta> deltas = PlanProjectionDeltas(previous, authored);
            InventoryProjectionResult projection = new InventoryProjectionResult();

            for (int i = 0; i < deltas.Count; i++)
            {
                InventoryProjectionDelta delta = deltas[i];
                ItemManager.ItemType type;
                if (!ShelteredContent.Runtime.ResolveItemType(delta.ItemId, out type))
                {
                    warning = AppendMessage(warning, "Unknown item id skipped: " + delta.ItemId + ".");
                    continue;
                }

                if (delta.QuantityDelta > 0)
                {
                    if (manager.AddNewItems(type, delta.QuantityDelta))
                        projection.Added += delta.QuantityDelta;
                    else
                        warning = AppendMessage(warning, "Inventory rejected '" + delta.ItemId + "' quantity " + delta.QuantityDelta + ".");
                }
                else
                {
                    int removal = System.Math.Min(-delta.QuantityDelta, CountLiveStorage(manager, type));
                    if (removal > 0 && manager.RemoveItemsOfType(type, removal))
                        projection.Removed += removal;
                }
            }

            projection.Stacks = authored.Count;
            SetProjection(key, authored);
            return projection;
        }

        private Dictionary<string, int> GetPreviousProjection(string key, ScenarioDefinition definition)
        {
            lock (_sync)
            {
                if (!string.Equals(_projectionKey, key, System.StringComparison.Ordinal))
                {
                    _projectionKey = key;
                    _lastProjected = BuildProjectionSeed(
                        BuildStartingInventorySnapshot(definition != null ? definition.StartingInventory : null),
                        BuildLiveInventorySnapshot());
                }
                return CopySnapshot(_lastProjected);
            }
        }

        private void SetProjection(string key, IDictionary<string, int> snapshot)
        {
            lock (_sync)
            {
                _projectionKey = key;
                _lastProjected = NormalizeSnapshot(snapshot);
            }
        }

        private static Dictionary<string, int> BuildStartingInventorySnapshot(StartingInventoryDefinition inventory)
        {
            Dictionary<string, int> snapshot = NewSnapshot();
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
            Dictionary<string, int> snapshot = NewSnapshot();
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

        private static void ReplaceStartingInventory(StartingInventoryDefinition inventory, IDictionary<string, int> live)
        {
            Dictionary<string, int> snapshot = NormalizeSnapshot(live);
            List<string> ids = new List<string>();
            AddKeys(ids, snapshot);
            ids.Sort(System.StringComparer.OrdinalIgnoreCase);
            inventory.Items.Clear();
            for (int i = 0; i < ids.Count; i++)
                inventory.Items.Add(new ItemEntry { ItemId = ids[i], Quantity = snapshot[ids[i]] });
        }

        private static Dictionary<string, int> NormalizeSnapshot(IDictionary<string, int> source)
        {
            Dictionary<string, int> snapshot = NewSnapshot();
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

        private static Dictionary<string, int> CopySnapshot(IDictionary<string, int> source)
        {
            Dictionary<string, int> copy = NewSnapshot();
            if (source != null)
                foreach (KeyValuePair<string, int> pair in source)
                    copy[pair.Key] = pair.Value;
            return copy;
        }

        private static Dictionary<string, int> NewSnapshot()
        {
            return new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        }

        private static void AddKeys(List<string> ids, Dictionary<string, int> source)
        {
            foreach (KeyValuePair<string, int> pair in source)
                if (!ids.Contains(pair.Key))
                    ids.Add(pair.Key);
        }

        private static int CountLiveStorage(InventoryManager manager, ItemManager.ItemType type)
        {
            try { return manager.GetItemCountInStorage(type, false); }
            catch { return manager.GetNumItemsOfType(type); }
        }

        private static InventoryProjectionResult Summarize(List<InventoryProjectionDelta> deltas)
        {
            InventoryProjectionResult result = new InventoryProjectionResult();
            for (int i = 0; deltas != null && i < deltas.Count; i++)
            {
                if (deltas[i].QuantityDelta > 0)
                    result.Added += deltas[i].QuantityDelta;
                else
                    result.Removed += -deltas[i].QuantityDelta;
            }
            return result;
        }

        private static string ProjectionKey(ScenarioDefinition definition)
        {
            return definition == null
                ? string.Empty
                : (definition.Id ?? string.Empty) + "|" + definition.BaseGameMode;
        }

        private static string AppendMessage(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
                return second;
            if (string.IsNullOrEmpty(second))
                return first;
            return first + " " + second;
        }

        private static bool CanProjectInCurrentWorld(out string reason)
        {
            reason = null;
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
            {
                reason = "Authoring inventory projection skipped because no draft editor is active.";
                return false;
            }
            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                reason = "Authoring inventory projection skipped because playtest owns the live apply pipeline.";
                return false;
            }
            if (!ShelteredScenarioRuntime.IsShelterSceneActive())
            {
                reason = "Authoring inventory projection skipped because the shelter scene is not active.";
                return false;
            }
            if (InventoryManager.Instance == null)
            {
                reason = "Authoring inventory projection skipped because InventoryManager is not ready.";
                return false;
            }
            return true;
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
        public bool DraftUpdated { get; set; }
        public bool Changed { get { return Added > 0 || Removed > 0 || DraftUpdated; } }
    }
}
