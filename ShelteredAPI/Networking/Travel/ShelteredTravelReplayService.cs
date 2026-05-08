using System;

namespace ShelteredAPI.Networking.Travel
{
    internal sealed class ShelteredTravelReplayService
    {
        private readonly IShelteredTravelStateRegistry _registry;

        public ShelteredTravelReplayService(IShelteredTravelStateRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException("registry");

            _registry = registry;
        }

        public ShelteredTravelPredictionResult ReplayCorrection(
            ShelteredTravelCorrectedEvent corrected,
            string eventId,
            long replayToWorldTick)
        {
            return ReplayCorrection(corrected, eventId, replayToWorldTick, false);
        }

        public ShelteredTravelPredictionResult ReplayCorrection(
            ShelteredTravelCorrectedEvent corrected,
            string eventId,
            long replayToWorldTick,
            bool force)
        {
            ShelteredTravelApplyResult result = _registry.ApplyTravelCorrected(corrected, eventId, force);
            if (!result.AppliedEvent || corrected == null)
                return null;

            return _registry.Predict(corrected.TravelId, replayToWorldTick);
        }
    }
}
