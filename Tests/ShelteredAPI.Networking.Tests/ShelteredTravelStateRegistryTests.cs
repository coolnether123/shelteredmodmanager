using System.Collections.Generic;
using ShelteredAPI.Networking.Travel;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTravelStateRegistryTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Travel registry applies started event and predicts active location", StartedEventPredictsActiveLocation));
            tests.Add(new TestCase("Travel registry ignores duplicate event ids", DuplicateEventIdIgnored));
            tests.Add(new TestCase("Travel registry ignores out-of-order corrections", OlderCorrectionIgnored));
            tests.Add(new TestCase("Travel replay rewinds only corrected travel entity", ReplayCorrectionOnlyUpdatesTargetTravel));
        }

        private static void StartedEventPredictsActiveLocation()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            ShelteredTravelApplyResult applied = registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");

            ShelteredTravelPredictionResult prediction = registry.Predict("travel-1", 5);
            IList<ShelteredTravelState> active = registry.GetActive();

            TestAssert.Equal(true, applied.AppliedEvent, "Started event should apply.");
            TestAssert.Equal(1, active.Count, "Started travel should be active.");
            TestAssert.Equal(5, prediction.GridX, "Prediction should advance along the route.");
            TestAssert.Equal(0, prediction.GridY, "Prediction should preserve Y on a horizontal route.");
        }

        private static void DuplicateEventIdIgnored()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");
            ShelteredTravelApplyResult duplicate =
                registry.ApplyTravelStarted(CreateStarted("travel-2", 0, 0, 20, 0), "event-1");

            TestAssert.Equal(false, duplicate.AppliedEvent, "Duplicate event id should not apply.");
            TestAssert.Equal("duplicate-event-id", duplicate.Reason, "Duplicate should report duplicate reason.");
            TestAssert.Equal(1, registry.GetActive().Count, "Duplicate start must not add another active travel.");
        }

        private static void OlderCorrectionIgnored()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");

            ShelteredTravelCorrectedEvent newer = CreateCorrection("travel-1", 8, 8, 0, 20, 0);
            ShelteredTravelCorrectedEvent older = CreateCorrection("travel-1", 6, 6, 0, 30, 0);
            registry.ApplyTravelCorrected(newer, "event-2");
            ShelteredTravelApplyResult oldResult = registry.ApplyTravelCorrected(older, "event-3");
            ShelteredTravelPredictionResult prediction = registry.Predict("travel-1", 8);

            TestAssert.Equal(false, oldResult.AppliedEvent, "Older correction should be ignored.");
            TestAssert.Equal("out-of-order-event", oldResult.Reason, "Older correction should report ordering reason.");
            TestAssert.Equal(8, prediction.GridX, "Prediction should keep newer correction start.");
        }

        private static void ReplayCorrectionOnlyUpdatesTargetTravel()
        {
            ShelteredTravelStateRegistry registry = new ShelteredTravelStateRegistry(null);
            ShelteredTravelReplayService replay = new ShelteredTravelReplayService(registry);
            registry.ApplyTravelStarted(CreateStarted("travel-1", 0, 0, 10, 0), "event-1");
            registry.ApplyTravelStarted(CreateStarted("travel-2", 0, 10, 10, 10), "event-2");

            ShelteredTravelPredictionResult replayed =
                replay.ReplayCorrection(CreateCorrection("travel-1", 5, 3, 0, 13, 0), "event-3", 5);
            ShelteredTravelPredictionResult untouched = registry.Predict("travel-2", 5);

            TestAssert.Equal(3, replayed.GridX, "Replay should rewind corrected travel to corrected X.");
            TestAssert.Equal(0, replayed.GridY, "Replay should rewind corrected travel to corrected Y.");
            TestAssert.Equal(5, untouched.GridX, "Uncorrected travel should keep original prediction.");
            TestAssert.Equal(10, untouched.GridY, "Uncorrected travel should keep original Y.");
        }

        private static ShelteredTravelStartedEvent CreateStarted(string travelId, int startX, int startY, int destinationX, int destinationY)
        {
            return new ShelteredTravelStartedEvent
            {
                TravelId = travelId,
                OwnerPlayerId = 1,
                OwnerPeerId = 0,
                PartyId = travelId == "travel-1" ? 1 : 2,
                StartTick = 0,
                StartGridX = startX,
                StartGridY = startY,
                DestinationGridX = destinationX,
                DestinationGridY = destinationY,
                WorldUnitsPerTick = 1f,
                ExpectedArrivalTick = 10,
                SeedStreamName = "MultiplayerSync.Travel.test"
            };
        }

        private static ShelteredTravelCorrectedEvent CreateCorrection(
            string travelId,
            long correctionTick,
            int correctedX,
            int correctedY,
            int destinationX,
            int destinationY)
        {
            return new ShelteredTravelCorrectedEvent
            {
                TravelId = travelId,
                CorrectionTick = correctionTick,
                CorrectedGridX = correctedX,
                CorrectedGridY = correctedY,
                DestinationGridX = destinationX,
                DestinationGridY = destinationY,
                WorldUnitsPerTick = 1f,
                ExpectedArrivalTick = correctionTick + 10,
                Reason = ShelteredTravelCorrectionReasons.HostCorrection
            };
        }
    }
}
