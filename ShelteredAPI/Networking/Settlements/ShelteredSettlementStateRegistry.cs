using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Settlements
{
    internal sealed class ShelteredSettlementStateRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ShelteredSettlementState> _states =
            new Dictionary<string, ShelteredSettlementState>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedEventIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly IShelteredMapEntityRegistry _mapEntities;

        public ShelteredSettlementStateRegistry()
            : this(ShelteredMapEntities.Registry)
        {
        }

        internal ShelteredSettlementStateRegistry(IShelteredMapEntityRegistry mapEntities)
        {
            _mapEntities = mapEntities;
        }

        public ShelteredSettlementApplyResult Apply(ShelteredSettlementEvent settlementEvent, string eventId)
        {
            if (settlementEvent == null || string.IsNullOrEmpty(settlementEvent.SettlementId))
                return ShelteredSettlementApplyResult.Ignored("missing-settlement-id");
            if (IsDuplicateEvent(eventId))
                return ShelteredSettlementApplyResult.IgnoredDuplicate;

            ShelteredSettlementState updated = ToState(settlementEvent);
            updated.LastEventId = Normalize(eventId);
            if (string.Equals(settlementEvent.EventKind, ShelteredNetworkEventKinds.SettlementDestroyed, StringComparison.Ordinal))
                updated.State = "destroyed";

            lock (_sync)
            {
                _states[updated.SettlementId] = updated.Copy();
                MarkEventApplied(eventId);
            }

            UpsertMapEntity(updated);
            return ShelteredSettlementApplyResult.Applied;
        }

        public ShelteredSettlementState Get(string settlementId)
        {
            lock (_sync)
            {
                ShelteredSettlementState state;
                return _states.TryGetValue(settlementId ?? string.Empty, out state) ? state.Copy() : null;
            }
        }

        public IList<ShelteredSettlementState> GetAll()
        {
            List<ShelteredSettlementState> results = new List<ShelteredSettlementState>();
            lock (_sync)
            {
                foreach (ShelteredSettlementState state in _states.Values)
                    if (state != null)
                        results.Add(state.Copy());
            }

            results.Sort(CompareStates);
            return results;
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _states.Clear();
                _appliedEventIds.Clear();
            }
        }

        internal static string CreateMapEntityId(string settlementId)
        {
            return "mapentity:settlement:" + (settlementId ?? string.Empty);
        }

        private void UpsertMapEntity(ShelteredSettlementState state)
        {
            if (_mapEntities == null || state == null || string.IsNullOrEmpty(state.SettlementId))
                return;

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = CreateMapEntityId(state.SettlementId);
            entity.Kind = ShelteredMapEntityKind.Settlement;
            entity.OwnerPlayerId = state.OwnerPlayerId;
            entity.DisplayName = "Settlement " + state.SettlementId;
            entity.GridX = state.GridX;
            entity.GridY = state.GridY;
            entity.IsOnline = !string.Equals(state.State, "destroyed", StringComparison.Ordinal);
            entity.IsVisible = true;
            entity.State = state.State ?? string.Empty;
            entity.UpdatedWorldTick = state.LastProductionTick;
            entity.PayloadJson = "{\"settlementId\":\"" + EscapeJson(state.SettlementId) + "\",\"ownerFactionId\":\"" + EscapeJson(state.OwnerFactionId) + "\"}";
            _mapEntities.Upsert(entity);
        }

        private static ShelteredSettlementState ToState(ShelteredSettlementEvent settlementEvent)
        {
            ShelteredSettlementState state = new ShelteredSettlementState();
            state.SettlementId = settlementEvent.SettlementId ?? string.Empty;
            state.OwnerPlayerId = settlementEvent.OwnerPlayerId;
            state.OwnerFactionId = settlementEvent.OwnerFactionId ?? string.Empty;
            state.GridX = settlementEvent.GridX;
            state.GridY = settlementEvent.GridY;
            state.Population = settlementEvent.Population;
            state.Defense = settlementEvent.Defense;
            state.StorageStoreId = settlementEvent.StorageStoreId ?? string.Empty;
            state.LastProductionTick = settlementEvent.LastProductionTick;
            for (int i = 0; settlementEvent.ProductionTags != null && i < settlementEvent.ProductionTags.Count; i++)
                state.ProductionTags.Add(settlementEvent.ProductionTags[i] ?? string.Empty);
            return state;
        }

        private bool IsDuplicateEvent(string eventId)
        {
            string normalized = Normalize(eventId);
            if (normalized.Length == 0)
                return false;
            lock (_sync)
            {
                return _appliedEventIds.Contains(normalized);
            }
        }

        private void MarkEventApplied(string eventId)
        {
            string normalized = Normalize(eventId);
            if (normalized.Length > 0)
                _appliedEventIds.Add(normalized);
        }

        private static string Normalize(string value) { return (value ?? string.Empty).Trim(); }
        private static string EscapeJson(string value) { return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\""); }
        private static int CompareStates(ShelteredSettlementState left, ShelteredSettlementState right)
        {
            return string.Compare(left != null ? left.SettlementId : string.Empty, right != null ? right.SettlementId : string.Empty, StringComparison.Ordinal);
        }
    }
}
