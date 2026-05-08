using System;

namespace ShelteredAPI.Networking.Travel
{
    internal static class ShelteredTravelPrediction
    {
        public static ShelteredTravelPredictionResult Predict(ShelteredTravelStartedEvent started, long currentTick)
        {
            if (started == null)
                throw new ArgumentNullException("started");

            long clampedTick = currentTick < started.StartTick ? started.StartTick : currentTick;
            ShelteredTravelPredictionResult result = new ShelteredTravelPredictionResult();
            result.CurrentTick = clampedTick;
            result.ExpectedArrivalTick = started.ExpectedArrivalTick;

            if (started.ExpectedArrivalTick <= started.StartTick)
            {
                result.IsComplete = true;
                result.Progress01 = 1f;
                result.GridX = started.DestinationGridX;
                result.GridY = started.DestinationGridY;
                return result;
            }

            long elapsed = clampedTick - started.StartTick;
            long duration = started.ExpectedArrivalTick - started.StartTick;
            float progress = (float)((double)elapsed / (double)duration);
            if (progress < 0f)
                progress = 0f;
            if (progress > 1f)
                progress = 1f;

            result.IsComplete = clampedTick >= started.ExpectedArrivalTick;
            result.Progress01 = progress;

            if (result.IsComplete)
            {
                result.GridX = started.DestinationGridX;
                result.GridY = started.DestinationGridY;
                return result;
            }

            result.GridX = InterpolateGrid(started.StartGridX, started.DestinationGridX, progress);
            result.GridY = InterpolateGrid(started.StartGridY, started.DestinationGridY, progress);
            return result;
        }

        public static ShelteredTravelStartedEvent CreateCorrectedStart(
            ShelteredTravelStartedEvent original,
            ShelteredTravelCorrectedEvent correction)
        {
            if (correction == null)
                throw new ArgumentNullException("correction");

            ShelteredTravelStartedEvent corrected = original != null
                ? original.Copy()
                : new ShelteredTravelStartedEvent();

            corrected.TravelId = correction.TravelId ?? string.Empty;
            corrected.StartTick = correction.CorrectionTick;
            corrected.StartGridX = correction.CorrectedGridX;
            corrected.StartGridY = correction.CorrectedGridY;
            corrected.DestinationGridX = correction.DestinationGridX;
            corrected.DestinationGridY = correction.DestinationGridY;
            corrected.WorldUnitsPerTick = correction.WorldUnitsPerTick;
            corrected.ExpectedArrivalTick = correction.ExpectedArrivalTick;
            return corrected;
        }

        private static int InterpolateGrid(int start, int destination, float progress)
        {
            double value = start + ((destination - start) * (double)progress);
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
