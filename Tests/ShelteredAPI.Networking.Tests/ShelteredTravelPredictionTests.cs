using System.Collections.Generic;
using ShelteredAPI.Networking.Travel;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredTravelPredictionTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("Travel prediction is deterministic for the same start and tick", SameStartAndTickPredictSamePosition));
            tests.Add(new TestCase("Travel prediction clamps ticks before start", BeforeStartTickClampsToStart));
            tests.Add(new TestCase("Travel prediction clamps after arrival", AfterArrivalClampsToDestination));
            tests.Add(new TestCase("Travel correction creates a new start state from correction tick", CorrectionCreatesNewStartState));
        }

        private static void SameStartAndTickPredictSamePosition()
        {
            ShelteredTravelStartedEvent started = CreateStarted();

            ShelteredTravelPredictionResult first = ShelteredTravelPrediction.Predict(started, 15);
            ShelteredTravelPredictionResult second = ShelteredTravelPrediction.Predict(started, 15);

            TestAssert.Equal(first.CurrentTick, second.CurrentTick, "Current tick should be deterministic.");
            TestAssert.Near(first.Progress01, second.Progress01, 0.0001f, "Progress should be deterministic.");
            TestAssert.Equal(first.GridX, second.GridX, "Predicted grid X should be deterministic.");
            TestAssert.Equal(first.GridY, second.GridY, "Predicted grid Y should be deterministic.");
            TestAssert.Equal(first.IsComplete, second.IsComplete, "Completion should be deterministic.");
        }

        private static void BeforeStartTickClampsToStart()
        {
            ShelteredTravelStartedEvent started = CreateStarted();

            ShelteredTravelPredictionResult result = ShelteredTravelPrediction.Predict(started, 5);

            TestAssert.Equal((long)10, result.CurrentTick, "Prediction should clamp current tick to the travel start.");
            TestAssert.Near(0f, result.Progress01, 0.0001f, "Prediction before start should have no progress.");
            TestAssert.Equal(2, result.GridX, "Prediction before start should stay at start grid X.");
            TestAssert.Equal(4, result.GridY, "Prediction before start should stay at start grid Y.");
            TestAssert.Equal(false, result.IsComplete, "Prediction before start should not be complete.");
        }

        private static void AfterArrivalClampsToDestination()
        {
            ShelteredTravelStartedEvent started = CreateStarted();

            ShelteredTravelPredictionResult result = ShelteredTravelPrediction.Predict(started, 99);

            TestAssert.Equal((long)99, result.CurrentTick, "Prediction should report the requested current tick after start.");
            TestAssert.Near(1f, result.Progress01, 0.0001f, "Prediction after arrival should clamp progress to one.");
            TestAssert.Equal(12, result.GridX, "Prediction after arrival should clamp to destination grid X.");
            TestAssert.Equal(24, result.GridY, "Prediction after arrival should clamp to destination grid Y.");
            TestAssert.Equal(true, result.IsComplete, "Prediction after arrival should be complete.");
        }

        private static void CorrectionCreatesNewStartState()
        {
            ShelteredTravelStartedEvent started = CreateStarted();
            ShelteredTravelCorrectedEvent correction = new ShelteredTravelCorrectedEvent();
            correction.TravelId = "travel-1";
            correction.CorrectionTick = 16;
            correction.CorrectedGridX = 7;
            correction.CorrectedGridY = 9;
            correction.DestinationGridX = 20;
            correction.DestinationGridY = 30;
            correction.WorldUnitsPerTick = 1.5f;
            correction.ExpectedArrivalTick = 40;
            correction.Reason = "host-correction";

            ShelteredTravelStartedEvent correctedStart =
                ShelteredTravelPrediction.CreateCorrectedStart(started, correction);

            TestAssert.Equal("travel-1", correctedStart.TravelId, "Corrected start should preserve travel id.");
            TestAssert.Equal((long)16, correctedStart.StartTick, "Corrected start should begin at correction tick.");
            TestAssert.Equal(7, correctedStart.StartGridX, "Corrected start should use corrected grid X.");
            TestAssert.Equal(9, correctedStart.StartGridY, "Corrected start should use corrected grid Y.");
            TestAssert.Equal(20, correctedStart.DestinationGridX, "Corrected start should use updated destination grid X.");
            TestAssert.Equal(30, correctedStart.DestinationGridY, "Corrected start should use updated destination grid Y.");
            TestAssert.Near(1.5f, correctedStart.WorldUnitsPerTick, 0.0001f, "Corrected start should use updated speed.");
            TestAssert.Equal((long)40, correctedStart.ExpectedArrivalTick, "Corrected start should use updated arrival tick.");

            ShelteredTravelPredictionResult prediction =
                ShelteredTravelPrediction.Predict(correctedStart, 16);
            TestAssert.Equal(7, prediction.GridX, "Correction tick prediction should start at corrected grid X.");
            TestAssert.Equal(9, prediction.GridY, "Correction tick prediction should start at corrected grid Y.");
        }

        private static ShelteredTravelStartedEvent CreateStarted()
        {
            ShelteredTravelStartedEvent started = new ShelteredTravelStartedEvent();
            started.TravelId = "travel-1";
            started.OwnerPlayerId = 1;
            started.OwnerPeerId = 0;
            started.PartyId = 3;
            started.StartTick = 10;
            started.StartGridX = 2;
            started.StartGridY = 4;
            started.DestinationGridX = 12;
            started.DestinationGridY = 24;
            started.WorldUnitsPerTick = 1f;
            started.ExpectedArrivalTick = 30;
            started.SeedStreamName = "MultiplayerSync.Travel.test";
            return started;
        }
    }
}
