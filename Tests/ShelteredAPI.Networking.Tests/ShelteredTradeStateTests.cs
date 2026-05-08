using System.Collections.Generic;
using ShelteredAPI.Networking.Encounters;
using ShelteredAPI.Networking.Trade;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTradeStateTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Trade state applies lifecycle transitions", AppliesLifecycleTransitions));
            tests.Add(new TestCase("Trade state ignores duplicate event ids", IgnoresDuplicateEventIds));
            tests.Add(new TestCase("Encounter negotiation state tracks trade fight and flee primitives", EncounterNegotiationTracksActionPrimitives));
            tests.Add(new TestCase("Encounter negotiation state ignores duplicate and stale intents", EncounterNegotiationIgnoresDuplicateAndStaleEvents));
        }

        private static void AppliesLifecycleTransitions()
        {
            ShelteredMultiplayerTradeStateRegistry registry = new ShelteredMultiplayerTradeStateRegistry();

            registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeOfferIntent, "event-offered", 1));
            AssertState(registry, ShelteredMultiplayerTradeStateKind.Offered);
            registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeOfferAccepted, "event-accepted", 2));
            AssertState(registry, ShelteredMultiplayerTradeStateKind.Accepted);
            registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeCargoReserved, "event-reserved", 3));
            AssertState(registry, ShelteredMultiplayerTradeStateKind.CargoReserved);
            registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeCaravanLaunched, "event-launched", 4));
            AssertState(registry, ShelteredMultiplayerTradeStateKind.CaravanLaunched);
            registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeCaravanArrived, "event-arrived", 5));
            AssertState(registry, ShelteredMultiplayerTradeStateKind.Arrived);
            registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeCompleted, "event-completed", 6));
            AssertState(registry, ShelteredMultiplayerTradeStateKind.Completed);
        }

        private static void IgnoresDuplicateEventIds()
        {
            ShelteredMultiplayerTradeStateRegistry registry = new ShelteredMultiplayerTradeStateRegistry();

            ShelteredMultiplayerTradeApplyResult first =
                registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeCompleted, "same-event", 1));
            ShelteredMultiplayerTradeApplyResult duplicate =
                registry.Apply(CreateEvent(ShelteredNetworkEventKinds.TradeCancelled, "same-event", 2));

            TestAssert.True(first.AppliedEvent, "First event should apply.");
            TestAssert.False(duplicate.AppliedEvent, "Duplicate event id should be ignored.");
            TestAssert.Equal("duplicate-event-id", duplicate.Reason, "Duplicate reason should be explicit.");
            AssertState(registry, ShelteredMultiplayerTradeStateKind.Completed);
        }

        private static void AssertState(ShelteredMultiplayerTradeStateRegistry registry, ShelteredMultiplayerTradeStateKind expected)
        {
            ShelteredMultiplayerTradeState state;
            TestAssert.True(registry.TryGet("trade-1", out state), "Trade state should exist.");
            TestAssert.Equal(expected, state.State, "Trade state should match latest event.");
        }

        private static ShelteredMultiplayerTradeEvent CreateEvent(string eventKind, string eventId, uint tick)
        {
            ShelteredMultiplayerTradeEvent tradeEvent = new ShelteredMultiplayerTradeEvent();
            tradeEvent.EventKind = eventKind;
            tradeEvent.EventId = eventId;
            tradeEvent.TradeId = "trade-1";
            tradeEvent.SourceOwnerId = "1";
            tradeEvent.TargetOwnerId = "2";
            tradeEvent.WorldTick = tick;
            return tradeEvent;
        }

        private static void EncounterNegotiationTracksActionPrimitives()
        {
            ShelteredEncounterNegotiationStateRegistry registry = new ShelteredEncounterNegotiationStateRegistry();

            registry.Apply(CreateEncounterEvent(
                "encounter-trade",
                ShelteredNetworkEventKinds.EncounterNegotiationAccepted,
                ShelteredEncounterActionKind.Trade,
                "event-trade",
                1));
            registry.Apply(CreateEncounterEvent(
                "encounter-fight",
                ShelteredNetworkEventKinds.EncounterNegotiationAccepted,
                ShelteredEncounterActionKind.Fight,
                "event-fight",
                2));
            registry.Apply(CreateEncounterEvent(
                "encounter-flee",
                ShelteredNetworkEventKinds.EncounterNegotiationAccepted,
                ShelteredEncounterActionKind.Flee,
                "event-flee",
                3));

            AssertEncounterState(registry, "encounter-trade", ShelteredEncounterActionKind.Trade, ShelteredEncounterNegotiationStateKind.Accepted);
            AssertEncounterState(registry, "encounter-fight", ShelteredEncounterActionKind.Fight, ShelteredEncounterNegotiationStateKind.Accepted);
            AssertEncounterState(registry, "encounter-flee", ShelteredEncounterActionKind.Flee, ShelteredEncounterNegotiationStateKind.Accepted);
        }

        private static void EncounterNegotiationIgnoresDuplicateAndStaleEvents()
        {
            ShelteredEncounterNegotiationStateRegistry registry = new ShelteredEncounterNegotiationStateRegistry();

            ShelteredEncounterNegotiationApplyResult accepted = registry.Apply(CreateEncounterEvent(
                "encounter-1",
                ShelteredNetworkEventKinds.EncounterNegotiationAccepted,
                ShelteredEncounterActionKind.Trade,
                "same-event",
                1));
            ShelteredEncounterNegotiationApplyResult duplicate = registry.Apply(CreateEncounterEvent(
                "encounter-1",
                ShelteredNetworkEventKinds.EncounterNegotiationResolved,
                ShelteredEncounterActionKind.Trade,
                "same-event",
                2));
            ShelteredEncounterNegotiationApplyResult staleIntent = registry.Apply(CreateEncounterEvent(
                "encounter-1",
                ShelteredNetworkEventKinds.EncounterInteractionIntent,
                ShelteredEncounterActionKind.Trade,
                "new-intent",
                3));

            TestAssert.True(accepted.AppliedEvent, "Initial accepted negotiation event should apply.");
            TestAssert.False(duplicate.AppliedEvent, "Duplicate event id should be ignored.");
            TestAssert.Equal("duplicate-event-id", duplicate.Reason, "Duplicate reason should be explicit.");
            TestAssert.False(staleIntent.AppliedEvent, "A proposed intent after acceptance should be ignored.");
            AssertEncounterState(registry, "encounter-1", ShelteredEncounterActionKind.Trade, ShelteredEncounterNegotiationStateKind.Accepted);
        }

        private static void AssertEncounterState(
            ShelteredEncounterNegotiationStateRegistry registry,
            string encounterId,
            ShelteredEncounterActionKind expectedAction,
            ShelteredEncounterNegotiationStateKind expectedState)
        {
            ShelteredEncounterNegotiationState state;
            TestAssert.True(registry.TryGet(encounterId, out state), "Encounter negotiation state should exist.");
            TestAssert.Equal(expectedAction, state.OfferedAction, "Encounter action should match.");
            TestAssert.Equal(expectedState, state.State, "Encounter state should match.");
        }

        private static ShelteredEncounterNegotiationEvent CreateEncounterEvent(
            string encounterId,
            string eventKind,
            ShelteredEncounterActionKind action,
            string eventId,
            uint tick)
        {
            ShelteredEncounterNegotiationEvent encounterEvent = new ShelteredEncounterNegotiationEvent();
            encounterEvent.EventKind = eventKind;
            encounterEvent.EventId = eventId;
            encounterEvent.WorldTick = tick;
            encounterEvent.EncounterId = encounterId;
            encounterEvent.InitiatorPlayerId = 1;
            encounterEvent.InitiatorPeerId = 1;
            encounterEvent.InitiatorTravelId = "travel-a";
            encounterEvent.ResponderPlayerId = 2;
            encounterEvent.ResponderPeerId = 2;
            encounterEvent.ResponderTravelId = "travel-b";
            encounterEvent.OfferedAction = action;
            encounterEvent.State = ShelteredEncounterNegotiationContractCodec.ResolveStateKind(
                eventKind,
                ShelteredEncounterNegotiationStateKind.Proposed);
            return encounterEvent;
        }
    }
}
