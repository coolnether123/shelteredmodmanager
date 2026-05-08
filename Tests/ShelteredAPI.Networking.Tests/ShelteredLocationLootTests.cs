using System.Collections.Generic;
using ShelteredAPI.Networking.Locations;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredLocationLootTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("LocationLoot_DuplicateTakenEventIsIgnored", DuplicateTakenEventIsIgnored));
            tests.Add(new TestCase("LocationLoot_CustomAndVanillaIdsRemainSeparate", CustomAndVanillaIdsRemainSeparate));
        }

        private static void DuplicateTakenEventIsIgnored()
        {
            ShelteredLocationStateRegistry registry = new ShelteredLocationStateRegistry(delegate { return 1; });
            registry.SetLoot("loc", new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 3, Source = "test" } });

            List<LootItemRecord> taken = new List<LootItemRecord> { new LootItemRecord { VanillaItemTypeInt = 5, Count = 2, Source = "test" } };

            bool first = registry.ApplyLootTaken("loc", "event-1", taken, 2, 10);
            bool second = registry.ApplyLootTaken("loc", "event-1", taken, 2, 10);
            IList<LootItemRecord> remaining = registry.GetLoot("loc");

            TestAssert.True(first, "First loot-taken event should apply.");
            TestAssert.False(second, "Duplicate loot-taken event should be ignored.");
            TestAssert.Equal(1, remaining[0].Count, "Duplicate event should not remove items twice.");
        }

        private static void CustomAndVanillaIdsRemainSeparate()
        {
            LootItemRecord vanilla = new LootItemRecord { VanillaItemTypeInt = 5, Count = 1 };
            LootItemRecord custom = new LootItemRecord { CustomItemId = "mod:item:wood", Count = 1 };

            TestAssert.False(ShelteredLocationStateRegistry.SameItem(vanilla, custom), "Vanilla enum ids and custom string ids must not collapse together.");
            TestAssert.True(ShelteredLocationStateRegistry.SameItem(custom, new LootItemRecord { CustomItemId = "mod:item:wood", Count = 3 }), "Matching custom ids should compare equal.");
        }
    }
}
