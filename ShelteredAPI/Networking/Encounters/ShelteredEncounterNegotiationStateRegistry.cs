using System;
using System.Collections.Generic;

namespace ShelteredAPI.Networking.Encounters
{
    internal sealed class ShelteredEncounterNegotiationStateRegistry
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, ShelteredEncounterNegotiationState> _states =
            new Dictionary<string, ShelteredEncounterNegotiationState>(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedEventIds =
            new HashSet<string>(StringComparer.Ordinal);

        public ShelteredEncounterNegotiationApplyResult Apply(ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent == null || string.IsNullOrEmpty(encounterEvent.EncounterId))
                return ShelteredEncounterNegotiationApplyResult.Ignored("missing-encounter-event");

            string eventId = Normalize(encounterEvent.EventId);
            ShelteredEncounterNegotiationStateKind incomingState =
                ShelteredEncounterNegotiationContractCodec.ResolveStateKind(
                    encounterEvent.EventKind,
                    encounterEvent.State);

            lock (_sync)
            {
                if (eventId.Length > 0 && _appliedEventIds.Contains(eventId))
                    return ShelteredEncounterNegotiationApplyResult.IgnoredDuplicate;

                ShelteredEncounterNegotiationState state;
                if (!_states.TryGetValue(encounterEvent.EncounterId, out state) || state == null)
                    state = CreateState(encounterEvent, incomingState);
                else
                    state = state.Copy();

                if (ShouldIgnoreStateTransition(state.State, incomingState))
                {
                    if (eventId.Length > 0)
                        _appliedEventIds.Add(eventId);
                    return ShelteredEncounterNegotiationApplyResult.Ignored("stale-negotiation-state");
                }

                UpdateIdentity(state, encounterEvent);
                state.OfferedAction = encounterEvent.OfferedAction == ShelteredEncounterActionKind.Unknown
                    ? state.OfferedAction
                    : encounterEvent.OfferedAction;
                state.State = incomingState;
                state.LastAuthoritativeTick = encounterEvent.WorldTick;
                state.LastEventId = eventId;
                state.LastEventKind = encounterEvent.EventKind ?? string.Empty;
                state.Reason = encounterEvent.Reason ?? string.Empty;

                _states[state.EncounterId] = state;
                if (eventId.Length > 0)
                    _appliedEventIds.Add(eventId);
            }

            return ShelteredEncounterNegotiationApplyResult.Applied;
        }

        public bool TryGet(string encounterId, out ShelteredEncounterNegotiationState state)
        {
            state = null;
            string key = Normalize(encounterId);
            if (key.Length == 0)
                return false;

            lock (_sync)
            {
                ShelteredEncounterNegotiationState existing;
                if (!_states.TryGetValue(key, out existing) || existing == null)
                    return false;

                state = existing.Copy();
                return true;
            }
        }

        public IList<ShelteredEncounterNegotiationState> GetAll()
        {
            List<ShelteredEncounterNegotiationState> states = new List<ShelteredEncounterNegotiationState>();
            lock (_sync)
            {
                foreach (ShelteredEncounterNegotiationState state in _states.Values)
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

        private static ShelteredEncounterNegotiationState CreateState(
            ShelteredEncounterNegotiationEvent encounterEvent,
            ShelteredEncounterNegotiationStateKind stateKind)
        {
            ShelteredEncounterNegotiationState state = new ShelteredEncounterNegotiationState();
            state.EncounterId = encounterEvent.EncounterId ?? string.Empty;
            state.State = stateKind;
            UpdateIdentity(state, encounterEvent);
            state.OfferedAction = encounterEvent.OfferedAction;
            return state;
        }

        private static void UpdateIdentity(
            ShelteredEncounterNegotiationState state,
            ShelteredEncounterNegotiationEvent encounterEvent)
        {
            if (encounterEvent.InitiatorPlayerId > 0)
                state.InitiatorPlayerId = encounterEvent.InitiatorPlayerId;
            state.InitiatorPeerId = encounterEvent.InitiatorPeerId;
            if (!string.IsNullOrEmpty(encounterEvent.InitiatorTravelId))
                state.InitiatorTravelId = encounterEvent.InitiatorTravelId;

            if (encounterEvent.ResponderPlayerId > 0)
                state.ResponderPlayerId = encounterEvent.ResponderPlayerId;
            state.ResponderPeerId = encounterEvent.ResponderPeerId;
            if (!string.IsNullOrEmpty(encounterEvent.ResponderTravelId))
                state.ResponderTravelId = encounterEvent.ResponderTravelId;
        }

        private static bool ShouldIgnoreStateTransition(
            ShelteredEncounterNegotiationStateKind current,
            ShelteredEncounterNegotiationStateKind incoming)
        {
            if (incoming == ShelteredEncounterNegotiationStateKind.Proposed
                && current != ShelteredEncounterNegotiationStateKind.Proposed)
            {
                return true;
            }

            return IsTerminal(current) && !IsTerminal(incoming);
        }

        private static bool IsTerminal(ShelteredEncounterNegotiationStateKind state)
        {
            return state == ShelteredEncounterNegotiationStateKind.Declined
                || state == ShelteredEncounterNegotiationStateKind.Resolved
                || state == ShelteredEncounterNegotiationStateKind.Expired;
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static int CompareStates(
            ShelteredEncounterNegotiationState left,
            ShelteredEncounterNegotiationState right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return string.Compare(left.EncounterId, right.EncounterId, StringComparison.Ordinal);
        }
    }
}
