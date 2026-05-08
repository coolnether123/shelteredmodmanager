using System.Collections.Generic;
using ShelteredAPI.Networking.Locations;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredLocationStateTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("LocationStateRegistry_UpsertAssignsStableLocationId", UpsertAssignsStableLocationId));
            tests.Add(new TestCase("LocationStateRegistry_GetAllReturnsCopies", GetAllReturnsCopies));
        }

        private static void UpsertAssignsStableLocationId()
        {
            ShelteredLocationStateRegistry registry = new ShelteredLocationStateRegistry(delegate { return 99; });
            LocationState state = new LocationState();
            state.GridX = 4;
            state.GridY = 7;
            state.MapIdentity = "map-test";
            state.LocationKind = "SmallHouse";

            LocationState saved = registry.Upsert(state);

            TestAssert.Equal("location:map-test:4:7:SmallHouse", saved.LocationId, "Missing ids should derive from map identity, grid, and kind.");
            TestAssert.Equal((long)99, saved.LastUpdatedTick, "Missing update tick should use the supplied world tick.");
        }

        private static void GetAllReturnsCopies()
        {
            ShelteredLocationStateRegistry registry = new ShelteredLocationStateRegistry(delegate { return 1; });
            LocationState state = new LocationState();
            state.LocationId = "location:test";
            state.LocationKind = "Test";
            registry.Upsert(state);

            IList<LocationState> all = registry.GetAll();
            all[0].LocationKind = "Mutated";

            LocationState fetched;
            TestAssert.True(registry.TryGet("location:test", out fetched), "Location should be fetchable.");
            TestAssert.Equal("Test", fetched.LocationKind, "Registry should not expose mutable state.");
        }
    }
}
