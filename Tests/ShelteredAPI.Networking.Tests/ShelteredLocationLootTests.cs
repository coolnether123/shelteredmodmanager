using System.Collections.Generic;
using ShelteredAPI.Networking.Locations;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredLocationLootTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("LocationLoot_SetGetCloneSafety", SetGetCloneSafety));
            tests.Add(new TestCase("LocationLoot_ApplyTakenRemovesCorrectCounts", ApplyTakenRemovesCorrectCounts));
            tests.Add(new TestCase("LocationLoot_DoubleLootSecondRequestRejected", DoubleLootSecondRequestRejected));
            tests.Add(new TestCase("LocationLoot_DuplicateTakenEventIsIgnored", DuplicateTakenEventIsIgnored));
            tests.Add(new TestCase("LocationLoot_DepletedAfterFinalItem", DepletedAfterFinalItem));
            tests.Add(new TestCase("LocationLoot_UnknownLocationRejectedSafely", UnknownLocationRejectedSafely));
            tests.Add(new TestCase("LocationLoot_AuthoritativeEventUpdatesClientRegistry", AuthoritativeEventUpdatesClientRegistry));
            tests.Add(new TestCase("LocationLoot_CustomAndVanillaIdsRemainSeparate", CustomAndVanillaIdsRemainSeparate));
        }

        private static void SetGetCloneSafety()
        {
            ShelteredLocationStateRegistry registry = CreateRegistryWithLocation("loc");
            List<LootItemRecord> source = new List<LootItemRecord>
            {
                new LootItemRecord { VanillaItemTypeInt = 5, Count = 2, Source = "test" }
            };

            TestAssert.True(registry.TrySetLoot("loc", source), "Registered location loot should be set.");
            source[0].Count = 99;

            IList<LootItemRecord> firstRead = registry.GetLoot("loc");
            TestAssert.Equal(2, firstRead[0].Count, "Registry should clone loot on set.");

            firstRead[0].Count = 1;
            IList<LootItemRecord> secondRead = registry.GetLoot("loc");
            TestAssert.Equal(2, secondRead[0].Count, "Registry should clone loot on get.");
        }

        private static void ApplyTakenRemovesCorrectCounts()
        {
            ShelteredLocationStateRegistry registry = CreateRegistryWithLocation("loc");
            registry.TrySetLoot("loc", new List<LootItemRecord>
            {
                new LootItemRecord { VanillaItemTypeInt = 5, Count = 5, Source = "test" },
                new LootItemRecord { VanillaItemTypeInt = 6, Count = 3, Source = "test" }
            });

            bool applied = registry.ApplyLootTaken("loc", "event-counts", new List<LootItemRecord>
            {
                new LootItemRecord { VanillaItemTypeInt = 5, Count = 2 },
                new LootItemRecord { VanillaItemTypeInt = 6, Count = 1 }
            }, 2, 10);

            IList<LootItemRecord> remaining = registry.GetLoot("loc");
            TestAssert.True(applied, "Valid loot-taken event should apply.");
            TestAssert.Equal(3, FindCount(remaining, 5), "Taken wood count should be removed.");
            TestAssert.Equal(2, FindCount(remaining, 6), "Taken metal count should be removed.");
        }

        private static void DoubleLootSecondRequestRejected()
        {
            ShelteredLocationStateRegistry registry = CreateRegistryWithLocation("loc");
            registry.TrySetLoot("loc", new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 } });
            List<LootItemRecord> taken = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 } };

            string error;
            TestAssert.True(registry.CanApplyLootTaken("loc", taken, out error), "Host should accept the first loot request.");
            TestAssert.True(registry.ApplyLootTaken("loc", "event-first", taken, 2, 10), "First loot-taken event should apply.");
            TestAssert.False(registry.CanApplyLootTaken("loc", taken, out error), "Host should reject a second client trying to take the same item.");
            TestAssert.False(registry.ApplyLootTaken("loc", "event-second", taken, 3, 11), "Second loot-taken event should not mutate state.");
            TestAssert.Equal(0, FindCount(registry.GetLoot("loc"), 5), "Second request should not underflow loot.");
        }

        private static void DuplicateTakenEventIsIgnored()
        {
            ShelteredLocationStateRegistry registry = CreateRegistryWithLocation("loc");
            registry.TrySetLoot("loc", new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 3, Source = "test" } });

            List<LootItemRecord> taken = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 2, Source = "test" } };

            bool first = registry.ApplyLootTaken("loc", "event-1", taken, 2, 10);
            bool second = registry.ApplyLootTaken("loc", "event-1", taken, 2, 10);
            IList<LootItemRecord> remaining = registry.GetLoot("loc");

            TestAssert.True(first, "First loot-taken event should apply.");
            TestAssert.False(second, "Duplicate loot-taken event should be ignored.");
            TestAssert.Equal(1, remaining[0].Count, "Duplicate event should not remove items twice.");
        }

        private static void DepletedAfterFinalItem()
        {
            ShelteredWorldEvents.Clear("location-loot-test");
            ShelteredLocationStateRegistry registry = CreateRegistryWithLocation("loc");
            registry.TrySetLoot("loc", new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 } });
            ShelteredLocationLootService service = new ShelteredLocationLootService(registry, false);

            ShelteredLocationEvent taken = CreateEvent("loc");
            taken.EventCorrelationId = "event-final";
            taken.Loot = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 } };

            TestAssert.True(service.ApplyAuthoritative(ShelteredNetworkEventKinds.LocationLootTaken, taken), "Final authoritative loot-taken event should apply.");

            LocationState state;
            TestAssert.True(registry.TryGet("loc", out state), "Location state should still exist.");
            TestAssert.True(state.IsDepleted, "Location should be marked depleted after the final item is taken.");
            TestAssert.True(registry.IsDepleted("loc"), "Loot list should be depleted.");
            service.Dispose();
        }

        private static void UnknownLocationRejectedSafely()
        {
            ShelteredLocationStateRegistry registry = new ShelteredLocationStateRegistry(delegate { return 1; });
            string error;
            List<LootItemRecord> taken = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 } };

            TestAssert.False(registry.CanApplyLootTaken("missing", taken, out error), "Unknown location should fail host validation.");
            TestAssert.False(registry.ApplyLootTaken("missing", "event-missing", taken, 2, 10), "Unknown location should not accept loot-taken events.");
            TestAssert.Equal(0, registry.GetLoot("missing").Count, "Unknown location should not create a loot list.");

            LocationState state;
            TestAssert.False(registry.TryGet("missing", out state), "Unknown location should not be created by a rejected loot event.");
        }

        private static void AuthoritativeEventUpdatesClientRegistry()
        {
            ShelteredWorldEvents.Clear("location-loot-test");
            ShelteredLocationStateRegistry clientRegistry = new ShelteredLocationStateRegistry(delegate { return 1; });
            ShelteredLocationLootService clientService = new ShelteredLocationLootService(clientRegistry, false);

            ShelteredLocationEvent set = CreateEvent("loc");
            set.EventCorrelationId = "loot-set";
            set.Loot = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 2 } };
            TestAssert.True(clientService.ApplyAuthoritative(ShelteredNetworkEventKinds.LocationLootGenerated, set), "Client should apply authoritative loot set.");

            ShelteredLocationEvent taken = CreateEvent("loc");
            taken.EventCorrelationId = "loot-taken";
            taken.Loot = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 } };
            TestAssert.True(clientService.ApplyAuthoritative(ShelteredNetworkEventKinds.LocationLootTaken, taken), "Client should apply authoritative loot-taken event.");

            TestAssert.Equal(1, FindCount(clientRegistry.GetLoot("loc"), 5), "Client registry should reflect host authoritative remaining loot.");
            clientService.Dispose();
        }

        private static void CustomAndVanillaIdsRemainSeparate()
        {
            LootItemRecord vanilla = new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 };
            LootItemRecord custom = new LootItemRecord { CustomItemId = "mod:item:wood", Count = 1 };

            TestAssert.False(ShelteredLocationStateRegistry.SameItem(vanilla, custom), "Vanilla enum ids and custom string ids must not collapse together.");
            TestAssert.True(ShelteredLocationStateRegistry.SameItem(custom, new LootItemRecord { CustomItemId = "mod:item:wood", Count = 3 }), "Matching custom ids should compare equal.");
        }

        private static ShelteredLocationStateRegistry CreateRegistryWithLocation(string locationId)
        {
            ShelteredLocationStateRegistry registry = new ShelteredLocationStateRegistry(delegate { return 1; });
            LocationState state = new LocationState();
            state.LocationId = locationId;
            state.MapIdentity = "map-test";
            state.LocationKind = "Test";
            state.IsGenerated = true;
            registry.Upsert(state);
            return registry;
        }

        private static ShelteredLocationEvent CreateEvent(string locationId)
        {
            ShelteredLocationEvent locationEvent = new ShelteredLocationEvent();
            locationEvent.LocationId = locationId;
            locationEvent.MapIdentity = "map-test";
            locationEvent.LocationKind = "Test";
            locationEvent.IsGenerated = true;
            locationEvent.WorldTick = 10;
            locationEvent.PlayerId = 2;
            return locationEvent;
        }

        private static int FindCount(IList<LootItemRecord> loot, int vanillaItemTypeInt)
        {
            for (int i = 0; loot != null && i < loot.Count; i++)
            {
                if (loot[i] != null && loot[i].VanillaItemTypeInt.HasValue && loot[i].VanillaItemTypeInt.Value == vanillaItemTypeInt)
                    return loot[i].Count;
            }

            return 0;
        }
    }
}
