using System.Collections.Generic;
using ShelteredAPI.Networking.Raids;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredRaidStateTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("RaidStateRegistry_AppliesLifecycleAndCreatesMapEntity", AppliesLifecycleAndCreatesMapEntity));
            tests.Add(new TestCase("RaidStateRegistry_IgnoresDuplicateEvent", IgnoresDuplicateEvent));
        }

        private static void AppliesLifecycleAndCreatesMapEntity()
        {
            ShelteredMapEntityRegistry mapEntities = new ShelteredMapEntityRegistry(delegate { return 10; });
            ShelteredRaidStateRegistry registry = new ShelteredRaidStateRegistry(mapEntities);

            ShelteredRaidEvent raidEvent = new ShelteredRaidEvent();
            raidEvent.RaidId = "raid-a";
            raidEvent.EventKind = ShelteredNetworkEventKinds.RaidLaunched;
            raidEvent.AttackerPlayerId = 1;
            raidEvent.DefenderPlayerId = 2;
            raidEvent.TargetBunkerOwnerId = 7;
            raidEvent.StartTick = 100;
            raidEvent.ArrivalTick = 160;
            raidEvent.RaidStrength = 30;

            ShelteredRaidApplyResult result = registry.Apply(raidEvent, "event-1");
            ShelteredRaidState state = registry.Get("raid-a");
            ShelteredMapEntity entity = mapEntities.Get(ShelteredRaidStateRegistry.CreateMapEntityId("raid-a"));

            TestAssert.True(result.AppliedEvent, "Raid event should apply.");
            TestAssert.Equal(ShelteredRaidLifecycleState.Launched, state.State, "Raid state should track lifecycle.");
            TestAssert.Equal(30, state.RaidStrength, "Raid strength should be retained.");
            TestAssert.True(entity != null, "Raid should exist as a map entity.");
            TestAssert.Equal(ShelteredMapEntityKind.RaidParty, entity.Kind, "Raid map entity should use RaidParty kind.");
        }

        private static void IgnoresDuplicateEvent()
        {
            ShelteredRaidStateRegistry registry = new ShelteredRaidStateRegistry(new ShelteredMapEntityRegistry(delegate { return 0; }));
            ShelteredRaidEvent raidEvent = new ShelteredRaidEvent();
            raidEvent.RaidId = "raid-b";
            raidEvent.EventKind = ShelteredNetworkEventKinds.RaidIntent;
            raidEvent.AttackerPlayerId = 1;
            raidEvent.DefenderPlayerId = 2;
            raidEvent.TargetBunkerOwnerId = 3;
            raidEvent.RaidStrength = 10;

            registry.Apply(raidEvent, "same-event");
            ShelteredRaidApplyResult duplicate = registry.Apply(raidEvent, "same-event");

            TestAssert.False(duplicate.AppliedEvent, "Duplicate raid event should be ignored.");
            TestAssert.Equal("duplicate-event-id", duplicate.Reason, "Duplicate reason should be explicit.");
        }
    }
}
