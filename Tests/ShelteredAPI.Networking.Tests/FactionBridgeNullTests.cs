using System.Collections.Generic;
using ShelteredAPI.Networking.Factions;

namespace ShelteredAPI.Networking.Tests
{
    internal static class FactionBridgeNullTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("FactionBridgeNull_IsSafeDefault", IsSafeDefault));
        }

        private static void IsSafeDefault()
        {
            NullShelteredFactionBridge bridge = new NullShelteredFactionBridge();

            TestAssert.False(bridge.IsAvailable, "Null bridge should report unavailable optional integration.");
            TestAssert.Equal(0, bridge.GetKnownFactions().Count, "Null bridge should return no factions.");
            TestAssert.Equal(0, bridge.GetFactionTerritorySnapshot().Cells.Count, "Null bridge should return empty territory.");
            TestAssert.Equal((long)123, bridge.BuildFactionWorldTick(123).WorldTick, "Null bridge should still build a tick shell.");

            bridge.OnPlayerJoinedFaction(1, "none");
            bridge.OnSettlementFounded(null);
            bridge.OnRaidResolved(null);
            bridge.OnTradeCompleted(null);
        }
    }
}
