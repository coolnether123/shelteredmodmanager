using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Raids
{
    internal sealed class ShelteredRaidStateRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ShelteredRaidState> _states =
            new Dictionary<string, ShelteredRaidState>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedEventIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly IShelteredMapEntityRegistry _mapEntities;

        public ShelteredRaidStateRegistry()
            : this(ShelteredMapEntities.Registry)
        {
        }

        internal ShelteredRaidStateRegistry(IShelteredMapEntityRegistry mapEntities)
        {
            _mapEntities = mapEntities;
        }

        public ShelteredRaidApplyResult Apply(ShelteredRaidEvent raidEvent, string eventId)
        {
            if (raidEvent == null || string.IsNullOrEmpty(raidEvent.RaidId))
                return ShelteredRaidApplyResult.Ignored("missing-raid-id");
            if (IsDuplicateEvent(eventId))
                return ShelteredRaidApplyResult.IgnoredDuplicate;

            ShelteredRaidLifecycleState nextState;
            if (!TryResolveState(raidEvent.EventKind, out nextState))
                return ShelteredRaidApplyResult.Ignored("unknown-raid-event");

            ShelteredRaidState updated;
            lock (_sync)
            {
                ShelteredRaidState current;
                _states.TryGetValue(raidEvent.RaidId, out current);
                if (current != null && IsRegression(current.State, nextState))
                {
                    MarkEventApplied(eventId);
                    return ShelteredRaidApplyResult.IgnoredOutOfOrder;
                }

                updated = current != null ? current.Copy() : new ShelteredRaidState();
                updated.RaidId = raidEvent.RaidId ?? string.Empty;
                updated.AttackerPlayerId = raidEvent.AttackerPlayerId;
                updated.DefenderPlayerId = raidEvent.DefenderPlayerId;
                updated.TargetBunkerOwnerId = raidEvent.TargetBunkerOwnerId;
                updated.StartTick = raidEvent.StartTick;
                updated.ArrivalTick = raidEvent.ArrivalTick;
                updated.RaidStrength = raidEvent.RaidStrength;
                updated.WarningTick = raidEvent.WarningTick;
                updated.DefenseScore = raidEvent.DefenseScore;
                updated.ResultPayloadJson = raidEvent.ResultPayloadJson ?? string.Empty;
                updated.State = nextState;
                updated.LastEventId = Normalize(eventId);
                _states[updated.RaidId] = updated.Copy();
                MarkEventApplied(eventId);
            }

            UpsertMapEntity(updated);
            return ShelteredRaidApplyResult.Applied;
        }

        public ShelteredRaidState Get(string raidId)
        {
            lock (_sync)
            {
                ShelteredRaidState state;
                return _states.TryGetValue(raidId ?? string.Empty, out state) ? state.Copy() : null;
            }
        }

        public IList<ShelteredRaidState> GetAll()
        {
            List<ShelteredRaidState> results = new List<ShelteredRaidState>();
            lock (_sync)
            {
                foreach (ShelteredRaidState state in _states.Values)
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

        internal static string CreateMapEntityId(string raidId)
        {
            return "mapentity:raid:" + (raidId ?? string.Empty);
        }

        private void UpsertMapEntity(ShelteredRaidState state)
        {
            if (_mapEntities == null || state == null || string.IsNullOrEmpty(state.RaidId))
                return;

            ShelteredMapEntity entity = new ShelteredMapEntity();
            entity.EntityId = CreateMapEntityId(state.RaidId);
            entity.Kind = ShelteredMapEntityKind.RaidParty;
            entity.OwnerPlayerId = state.AttackerPlayerId;
            entity.BunkerOwnerId = state.TargetBunkerOwnerId;
            entity.DisplayName = "Raid " + state.RaidId;
            entity.IsOnline = !IsTerminal(state.State);
            entity.IsVisible = true;
            entity.State = state.State.ToString();
            entity.UpdatedWorldTick = state.ArrivalTick > 0 ? state.ArrivalTick : state.StartTick;
            entity.PayloadJson = "{\"raidId\":\"" + EscapeJson(state.RaidId) + "\"}";
            _mapEntities.Upsert(entity);
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

        private static bool TryResolveState(string eventKind, out ShelteredRaidLifecycleState state)
        {
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidIntent, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Intent; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidAccepted, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Accepted; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidRejected, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Rejected; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidLaunched, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Launched; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidWarning, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Warning; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidArrived, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Arrived; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidResolved, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Resolved; return true; }
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.RaidCancelled, StringComparison.Ordinal)) { state = ShelteredRaidLifecycleState.Cancelled; return true; }
            state = ShelteredRaidLifecycleState.Intent;
            return false;
        }

        private static bool IsRegression(ShelteredRaidLifecycleState current, ShelteredRaidLifecycleState next)
        {
            return IsTerminal(current) && current != next;
        }

        private static bool IsTerminal(ShelteredRaidLifecycleState state)
        {
            return state == ShelteredRaidLifecycleState.Rejected
                || state == ShelteredRaidLifecycleState.Resolved
                || state == ShelteredRaidLifecycleState.Cancelled;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static int CompareStates(ShelteredRaidState left, ShelteredRaidState right)
        {
            return string.Compare(left != null ? left.RaidId : string.Empty, right != null ? right.RaidId : string.Empty, StringComparison.Ordinal);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
