using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Trade
{
    internal sealed class ShelteredMultiplayerTradeStateRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ShelteredMultiplayerTradeState> _states =
            new Dictionary<string, ShelteredMultiplayerTradeState>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedEventIds =
            new HashSet<string>(StringComparer.Ordinal);

        public ShelteredMultiplayerTradeApplyResult Apply(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            if (tradeEvent == null || string.IsNullOrEmpty(tradeEvent.TradeId))
                return ShelteredMultiplayerTradeApplyResult.Ignored("missing-trade-event");

            string eventId = Normalize(tradeEvent.EventId);
            lock (_sync)
            {
                if (eventId.Length > 0 && _appliedEventIds.Contains(eventId))
                    return ShelteredMultiplayerTradeApplyResult.IgnoredDuplicate;

                ShelteredMultiplayerTradeState state;
                if (!_states.TryGetValue(tradeEvent.TradeId, out state) || state == null)
                    state = CreateState(tradeEvent);
                else
                    state = state.Copy();

                state.SourceOwnerId = string.IsNullOrEmpty(tradeEvent.SourceOwnerId) ? state.SourceOwnerId : tradeEvent.SourceOwnerId;
                state.TargetOwnerId = string.IsNullOrEmpty(tradeEvent.TargetOwnerId) ? state.TargetOwnerId : tradeEvent.TargetOwnerId;
                state.State = ResolveStateKind(tradeEvent.EventKind, state.State);
                state.LastAuthoritativeTick = tradeEvent.WorldTick;
                state.LastEventId = eventId;
                state.LastEventKind = tradeEvent.EventKind ?? string.Empty;
                state.FailureReason = tradeEvent.RejectionReason ?? string.Empty;

                _states[state.TradeId] = state;
                if (eventId.Length > 0)
                    _appliedEventIds.Add(eventId);
            }

            return ShelteredMultiplayerTradeApplyResult.Applied;
        }

        public bool TryGet(string tradeId, out ShelteredMultiplayerTradeState state)
        {
            state = null;
            if (string.IsNullOrEmpty(tradeId))
                return false;

            lock (_sync)
            {
                ShelteredMultiplayerTradeState existing;
                if (!_states.TryGetValue(tradeId, out existing) || existing == null)
                    return false;

                state = existing.Copy();
                return true;
            }
        }

        public IList<ShelteredMultiplayerTradeState> GetAll()
        {
            List<ShelteredMultiplayerTradeState> states = new List<ShelteredMultiplayerTradeState>();
            lock (_sync)
            {
                foreach (ShelteredMultiplayerTradeState state in _states.Values)
                {
                    if (state != null)
                        states.Add(state.Copy());
                }
            }

            states.Sort(CompareStates);
            return states;
        }

        public void Clear(string reason)
        {
            lock (_sync)
            {
                _states.Clear();
                _appliedEventIds.Clear();
            }
        }

        private static ShelteredMultiplayerTradeState CreateState(ShelteredMultiplayerTradeEvent tradeEvent)
        {
            return new ShelteredMultiplayerTradeState
            {
                TradeId = tradeEvent.TradeId ?? string.Empty,
                SourceOwnerId = tradeEvent.SourceOwnerId ?? string.Empty,
                TargetOwnerId = tradeEvent.TargetOwnerId ?? string.Empty,
                State = ResolveStateKind(tradeEvent.EventKind, ShelteredMultiplayerTradeStateKind.Offered)
            };
        }

        private static ShelteredMultiplayerTradeStateKind ResolveStateKind(string eventKind, ShelteredMultiplayerTradeStateKind fallback)
        {
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeOfferIntent, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Offered;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeOfferAccepted, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Accepted;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeOfferRejected, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Rejected;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCargoReserved, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.CargoReserved;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCaravanLaunched, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.CaravanLaunched;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCaravanArrived, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Arrived;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCompleted, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Completed;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeCancelled, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Cancelled;
            if (string.Equals(eventKind, ShelteredNetworkEventKinds.TradeFailed, StringComparison.Ordinal))
                return ShelteredMultiplayerTradeStateKind.Failed;

            return fallback;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static int CompareStates(ShelteredMultiplayerTradeState left, ShelteredMultiplayerTradeState right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            return string.Compare(left.TradeId, right.TradeId, StringComparison.Ordinal);
        }
    }
}
