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
            long expectedArrivalTick = ResolveExpectedArrivalTick(started);
            ShelteredTravelPredictionResult result = new ShelteredTravelPredictionResult();
            result.CurrentTick = clampedTick;
            result.ExpectedArrivalTick = expectedArrivalTick;

            if (expectedArrivalTick <= started.StartTick)
            {
                result.IsComplete = true;
                result.Progress01 = 1f;
                ApplyPosition(result, started, 1f);
                return result;
            }

            long elapsed = clampedTick - started.StartTick;
            long duration = expectedArrivalTick - started.StartTick;
            float progress = (float)((double)elapsed / (double)duration);
            if (progress < 0f)
                progress = 0f;
            if (progress > 1f)
                progress = 1f;

            result.IsComplete = clampedTick >= expectedArrivalTick;
            result.Progress01 = progress;
            ApplyPosition(result, started, result.IsComplete ? 1f : progress);
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
            corrected.HasWorldPosition = correction.HasWorldPosition;
            corrected.StartWorldX = correction.HasWorldPosition ? correction.CorrectedWorldX : 0f;
            corrected.StartWorldY = correction.HasWorldPosition ? correction.CorrectedWorldY : 0f;
            corrected.DestinationWorldX = correction.HasWorldPosition ? correction.DestinationWorldX : 0f;
            corrected.DestinationWorldY = correction.HasWorldPosition ? correction.DestinationWorldY : 0f;
            corrected.WorldUnitsPerTick = correction.WorldUnitsPerTick;
            corrected.ExpectedArrivalTick = correction.ExpectedArrivalTick;
            return corrected;
        }

        private static long ResolveExpectedArrivalTick(ShelteredTravelStartedEvent started)
        {
            if (started.ExpectedArrivalTick > started.StartTick)
                return started.ExpectedArrivalTick;

            double distance = ResolveDistance(started);
            if (distance <= 0d || started.WorldUnitsPerTick <= 0f)
                return started.StartTick;

            long ticks = (long)Math.Ceiling(distance / started.WorldUnitsPerTick);
            return started.StartTick + (ticks > 0 ? ticks : 1);
        }

        private static double ResolveDistance(ShelteredTravelStartedEvent started)
        {
            if (started.HasWorldPosition)
                return Distance(
                    started.StartWorldX,
                    started.StartWorldY,
                    started.DestinationWorldX,
                    started.DestinationWorldY);

            return Distance(
                started.StartGridX,
                started.StartGridY,
                started.DestinationGridX,
                started.DestinationGridY);
        }

        private static double Distance(double startX, double startY, double destinationX, double destinationY)
        {
            double dx = destinationX - startX;
            double dy = destinationY - startY;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static void ApplyPosition(
            ShelteredTravelPredictionResult result,
            ShelteredTravelStartedEvent started,
            float progress)
        {
            result.GridX = InterpolateGrid(started.StartGridX, started.DestinationGridX, progress);
            result.GridY = InterpolateGrid(started.StartGridY, started.DestinationGridY, progress);

            if (!started.HasWorldPosition)
            {
                result.HasWorldPosition = false;
                result.WorldX = 0f;
                result.WorldY = 0f;
                return;
            }

            result.HasWorldPosition = true;
            result.WorldX = InterpolateFloat(started.StartWorldX, started.DestinationWorldX, progress);
            result.WorldY = InterpolateFloat(started.StartWorldY, started.DestinationWorldY, progress);
        }

        private static int InterpolateGrid(int start, int destination, float progress)
        {
            double value = start + ((destination - start) * (double)progress);
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static float InterpolateFloat(float start, float destination, float progress)
        {
            return start + ((destination - start) * progress);
        }
    }
}
