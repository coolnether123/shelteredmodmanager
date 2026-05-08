using System.Collections.Generic;
using ShelteredAPI.Networking.Trade;
using ShelteredAPI.Networking.World;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTradeCaravanTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Trade caravan launch creates trade map entity", LaunchCreatesTradeCaravanMapEntity));
            tests.Add(new TestCase("Trade completion transfers items once", CompletionTransfersItemsOnce));
            tests.Add(new TestCase("Duplicate trade completion is ignored", DuplicateCompletionDoesNotMoveItemsAgain));
        }

        private static void LaunchCreatesTradeCaravanMapEntity()
        {
            ShelteredMultiplayerTradeStateRegistry states = new ShelteredMultiplayerTradeStateRegistry();
            ShelteredMapEntityRegistry mapEntities = new ShelteredMapEntityRegistry(delegate { return 0; });
            ShelteredMultiplayerTradeCaravanService caravan =
                new ShelteredMultiplayerTradeCaravanService(states, null, mapEntities);

            caravan.LaunchCaravan(ShelteredTradeCargoReservationTests.CreateTrade(1), 1, 2, 3, 4, 1f, 10, 20);

            ShelteredMapEntity entity = mapEntities.Get(ShelteredMultiplayerTradeCaravanService.CreateMapEntityId("trade-1"));
            TestAssert.True(entity != null, "Launch should create a map entity.");
            TestAssert.Equal(ShelteredMapEntityKind.TradeCaravan, entity.Kind, "Map entity should be a trade caravan.");
            TestAssert.Equal("launched", entity.State, "Map entity state should describe launch.");
            TestAssert.Equal(1, entity.GridX, "Map entity should start at launch grid x.");
            TestAssert.Equal(2, entity.GridY, "Map entity should start at launch grid y.");
        }

        private static void CompletionTransfersItemsOnce()
        {
            ShelteredTradeCargoReservationTests.FakeReservableStore source =
                new ShelteredTradeCargoReservationTests.FakeReservableStore("source");
            source.SetCount("water", 5);
            ShelteredTradeCargoReservationTests.FakeItemStore target =
                new ShelteredTradeCargoReservationTests.FakeItemStore("target");

            ShelteredMultiplayerTradeEvent trade = ShelteredTradeCargoReservationTests.CreateTrade(3);
            ShelteredMultiplayerTradeCargoReservationService reservations =
                new ShelteredMultiplayerTradeCargoReservationService(
                    ShelteredTradeCargoReservationTests.Resolve(source, target),
                    new ShelteredTradeCargoReservationTests.FakeAssignmentService());
            TestAssert.True(reservations.Reserve(trade).Success, "Reservation should succeed before completion.");

            ShelteredMultiplayerTradeStateRegistry states = new ShelteredMultiplayerTradeStateRegistry();
            ShelteredMultiplayerTradeCaravanService caravan =
                new ShelteredMultiplayerTradeCaravanService(states, reservations, new ShelteredMapEntityRegistry(delegate { return 0; }));

            ItemTransferResult result = caravan.Complete(trade, 30);

            TestAssert.True(result.Success, "Completion should transfer reserved cargo.");
            TestAssert.Equal(2, source.GetCount("water"), "Source should lose cargo only at completion.");
            TestAssert.Equal(3, target.GetCount("water"), "Target should receive cargo at completion.");
            AssertTradeState(states, ShelteredMultiplayerTradeStateKind.Completed);
        }

        private static void DuplicateCompletionDoesNotMoveItemsAgain()
        {
            ShelteredTradeCargoReservationTests.FakeReservableStore source =
                new ShelteredTradeCargoReservationTests.FakeReservableStore("source");
            source.SetCount("water", 5);
            ShelteredTradeCargoReservationTests.FakeItemStore target =
                new ShelteredTradeCargoReservationTests.FakeItemStore("target");

            ShelteredMultiplayerTradeEvent trade = ShelteredTradeCargoReservationTests.CreateTrade(3);
            ShelteredMultiplayerTradeCargoReservationService reservations =
                new ShelteredMultiplayerTradeCargoReservationService(
                    ShelteredTradeCargoReservationTests.Resolve(source, target),
                    new ShelteredTradeCargoReservationTests.FakeAssignmentService());
            reservations.Reserve(trade);

            ShelteredMultiplayerTradeStateRegistry states = new ShelteredMultiplayerTradeStateRegistry();
            ShelteredMultiplayerTradeCaravanService caravan =
                new ShelteredMultiplayerTradeCaravanService(states, reservations, new ShelteredMapEntityRegistry(delegate { return 0; }));

            caravan.Complete(trade, 30);
            ItemTransferResult duplicate = caravan.Complete(trade, 31);

            TestAssert.True(duplicate.Success, "Duplicate completion should be a successful no-op.");
            TestAssert.Equal(2, source.GetCount("water"), "Duplicate completion must not remove more source items.");
            TestAssert.Equal(3, target.GetCount("water"), "Duplicate completion must not add target items again.");
        }

        private static void AssertTradeState(ShelteredMultiplayerTradeStateRegistry states, ShelteredMultiplayerTradeStateKind expected)
        {
            ShelteredMultiplayerTradeState state;
            TestAssert.True(states.TryGet("trade-1", out state), "Trade state should exist.");
            TestAssert.Equal(expected, state.State, "Trade state should match expected value.");
        }
    }
}
