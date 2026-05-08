using System.Collections.Generic;

namespace ShelteredAPI.Networking.Tests
{
    internal static class Program
    {
        public static int Main(string[] args)
        {
            List<TestCase> tests = new List<TestCase>();
            ShelteredTradeEventTests.Register(tests);
            ShelteredTradeCargoValidationTests.Register(tests);
            ShelteredMultiplayerTimePolicyTests.Register(tests);
            ShelteredWorldEventJournalTests.Register(tests);
            ShelteredWorldClockTests.Register(tests);
            ShelteredMapEntityRegistryTests.Register(tests);
            ShelteredMultiplayerMapMarkerTests.Register(tests);
            ShelteredMultiplayerMapAnchorDiagnosticsTests.Register(tests);
            ShelteredTravelPredictionTests.Register(tests);
            return TestRunner.Run("ShelteredAPI networking tests", tests);
        }
    }
}
