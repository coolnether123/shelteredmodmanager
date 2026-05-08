using System.Collections.Generic;

namespace ShelteredAPI.Networking.Tests
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            List<TestCase> tests = new List<TestCase>();
            RuntimeEnvironmentInfoTests.Register(tests);
            ShelteredTradeEventTests.Register(tests);
            ShelteredTradeCargoValidationTests.Register(tests);
            ShelteredTradeCargoReservationTests.Register(tests);
            ShelteredTradeStateTests.Register(tests);
            ShelteredTradeCaravanTests.Register(tests);
            ShelteredMultiplayerTimePolicyTests.Register(tests);
            ShelteredMultiplayerSetupServiceTests.Register(tests);
            ShelteredWorldEventJournalTests.Register(tests);
            ShelteredWorldClockTests.Register(tests);
            ShelteredCompatibilityHashTests.Register(tests);
            ShelteredMapEntityRegistryTests.Register(tests);
            ShelteredMapKnowledgeTests.Register(tests);
            ShelteredMultiplayerMapMarkerTests.Register(tests);
            ShelteredMultiplayerMapAnchorDiagnosticsTests.Register(tests);
            ShelteredTravelPredictionTests.Register(tests);
            ShelteredTravelStateRegistryTests.Register(tests);
            ShelteredLocationStateTests.Register(tests);
            ShelteredLocationLootTests.Register(tests);
            ShelteredResourceNodeTests.Register(tests);
            ShelteredRaidStateTests.Register(tests);
            ShelterDefenseRatingTests.Register(tests);
            ShelteredSettlementStateTests.Register(tests);
            FactionBridgeNullTests.Register(tests);
            ShelteredMultiplayerWorldPersistenceTests.Register(tests);
            ShelteredMultiplayerCatchupTests.Register(tests);
            RngDiagnosticsTests.Register(tests);
            DesyncDigestTests.Register(tests);
            MultiplayerConnectionPanelHelperTests.Register(tests);
            ArchitectureGuardrailTests.Register(tests);
            return TestRunner.Run("ShelteredAPI networking tests", tests);
        }
    }
}
