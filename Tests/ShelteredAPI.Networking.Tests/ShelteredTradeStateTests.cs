using System.Collections.Generic;
using ShelteredAPI.Networking.Trade;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTradeStateTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Trade state applies lifecycle transitions", AppliesLifecycleTransitions));
            tests.Add(new TestCase("Trade state ignores duplicate event ids", IgnoresDuplicateEventIds));
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
    }
}
