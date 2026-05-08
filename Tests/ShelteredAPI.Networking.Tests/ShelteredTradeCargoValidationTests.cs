using System;
using System.Collections.Generic;
using ShelteredAPI.Networking.Trade;
using ShelteredAPI.Storage;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTradeCargoValidationTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Trade cargo validation rejects empty source owner", RejectsEmptySourceOwner));
            tests.Add(new TestCase("Trade cargo validation rejects empty target owner", RejectsEmptyTargetOwner));
            tests.Add(new TestCase("Trade cargo validation rejects empty cargo", RejectsEmptyCargo));
            tests.Add(new TestCase("Trade cargo validation rejects non-positive cargo", RejectsNonPositiveCargo));
            tests.Add(new TestCase("Trade cargo validation rejects owner mismatches", RejectsOwnerMismatches));
            tests.Add(new TestCase("Trade cargo validation rejects missing source store", RejectsMissingSourceStore));
            tests.Add(new TestCase("Trade cargo validation rejects cargo above source count", RejectsCargoAboveSourceCount));
            tests.Add(new TestCase("Trade cargo validation accepts valid cargo", AcceptsValidCargo));
        }

        private static void RejectsEmptySourceOwner()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(3);
            tradeEvent.SourceOwnerId = string.Empty;

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with empty source owner should be rejected.");
            TestAssert.Equal("Source owner is required.", result.ErrorMessage, "Validation should explain the missing source owner.");
        }

        private static void RejectsEmptyTargetOwner()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(3);
            tradeEvent.TargetOwnerId = string.Empty;

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with empty target owner should be rejected.");
            TestAssert.Equal("Target owner is required.", result.ErrorMessage, "Validation should explain the missing target owner.");
        }

        private static void RejectsEmptyCargo()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(3);
            tradeEvent.Cargo.Clear();

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with no cargo should be rejected.");
            TestAssert.Equal("Cargo is required.", result.ErrorMessage, "Validation should explain the missing cargo.");
        }

        private static void RejectsNonPositiveCargo()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(0);

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with zero cargo count should be rejected.");
            TestAssert.Equal("Cargo count must be positive.", result.ErrorMessage, "Validation should explain the invalid cargo count.");

            tradeEvent.Cargo[0].Count = -1;
            result = ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with negative cargo count should be rejected.");
            TestAssert.Equal("Cargo count must be positive.", result.ErrorMessage, "Validation should explain the invalid cargo count.");
        }

        private static void RejectsOwnerMismatches()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(3);
            tradeEvent.Cargo[0].SourceOwnerId = "player-c";

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with mismatched cargo source owner should be rejected.");
            TestAssert.Equal("Cargo source owner must match trade source owner.", result.ErrorMessage,
                "Validation should explain source owner mismatches.");

            tradeEvent = CreateOffer(3);
            tradeEvent.Cargo[0].TargetOwnerId = "player-c";

            result = ShelteredMultiplayerTradeCargoResolver.ValidateCargoShape(tradeEvent);

            TestAssert.False(result.Success, "Trade offer with mismatched cargo target owner should be rejected.");
            TestAssert.Equal("Cargo target owner must match trade target owner.", result.ErrorMessage,
                "Validation should explain target owner mismatches.");
        }

        private static void RejectsMissingSourceStore()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(3);

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoAvailable(
                    tradeEvent,
                    delegate { return null; });

            TestAssert.False(result.Success, "Trade offer without a resolved source store should be rejected.");
            TestAssert.Equal("Source store was not found for owner 'player-a'.", result.ErrorMessage, "Validation should identify the missing owner store.");
        }

        private static void RejectsCargoAboveSourceCount()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(6);
            FakeItemStore source = new FakeItemStore("store-a");
            source.SetCount("water", 5);

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoAvailable(
                    tradeEvent,
                    delegate(string ownerId) { return string.Equals(ownerId, "player-a", StringComparison.Ordinal) ? source : null; });

            TestAssert.False(result.Success, "Trade offer above source count should be rejected.");
            TestAssert.Equal("Source store does not contain enough cargo for item 'water'.", result.ErrorMessage, "Validation should explain the insufficient source count.");
        }

        private static void AcceptsValidCargo()
        {
            ShelteredMultiplayerTradeEvent tradeEvent = CreateOffer(3);
            FakeItemStore source = new FakeItemStore("store-a");
            source.SetCount("water", 5);

            ShelteredTradeCargoValidationResult result =
                ShelteredMultiplayerTradeCargoResolver.ValidateCargoAvailable(
                    tradeEvent,
                    delegate(string ownerId) { return string.Equals(ownerId, "player-a", StringComparison.Ordinal) ? source : null; });

            TestAssert.True(result.Success, "Trade offer within source count should be accepted.");
            TestAssert.Equal(1, result.TotalCargoLines, "Validation should summarize cargo line count.");
            TestAssert.Equal(3, result.TotalItemCount, "Validation should summarize total item count.");
        }

        private static ShelteredMultiplayerTradeEvent CreateOffer(int count)
        {
            ShelteredMultiplayerTradeEvent tradeEvent = new ShelteredMultiplayerTradeEvent();
            tradeEvent.EventKind = ShelteredNetworkEventKinds.TradeOfferIntent;
            tradeEvent.TradeId = "trade-1";
            tradeEvent.SourceOwnerId = "player-a";
            tradeEvent.TargetOwnerId = "player-b";

            ShelteredTradeCargoDto cargo = new ShelteredTradeCargoDto();
            cargo.ItemId = "water";
            cargo.Count = count;
            cargo.SourceOwnerId = "player-a";
            cargo.TargetOwnerId = "player-b";
            tradeEvent.Cargo.Add(cargo);

            return tradeEvent;
        }

        private sealed class FakeItemStore : IItemStore
        {
            private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

            public FakeItemStore(string storeId)
            {
                StoreId = storeId;
                DisplayName = storeId;
            }

            public string StoreId { get; private set; }
            public string DisplayName { get; private set; }
            public ItemStoreKind Kind { get { return ItemStoreKind.Mod; } }
            public int Capacity { get { return -1; } }
            public int Used { get { return 0; } }
            public bool IsReadOnly { get { return false; } }

            public void SetCount(string itemId, int count)
            {
                _counts[itemId] = count;
            }

            public ItemStoreSnapshot Snapshot()
            {
                return new ItemStoreSnapshot();
            }

            public int GetCount(string itemId)
            {
                int count;
                return itemId != null && _counts.TryGetValue(itemId, out count) ? count : 0;
            }

            public bool CanAdd(string itemId, int quantity)
            {
                return false;
            }

            public bool CanRemove(string itemId, int quantity)
            {
                return GetCount(itemId) >= quantity;
            }

            public ItemTransferResult Add(string itemId, int quantity)
            {
                return ItemTransferResult.Failed(itemId, quantity, "Not implemented.");
            }

            public ItemTransferResult Remove(string itemId, int quantity)
            {
                return ItemTransferResult.Failed(itemId, quantity, "Not implemented.");
            }
        }
    }
}
