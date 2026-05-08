using System;

namespace ShelteredAPI.Networking.Travel
{
    internal enum ShelteredTravelStateKind
    {
        Active,
        Interrupted,
        Corrected,
        Arrived,
        Cancelled
    }

    internal sealed class ShelteredTravelState
    {
        public ShelteredTravelState()
        {
            TravelId = string.Empty;
            LastEventId = string.Empty;
        }

        public string TravelId { get; set; }
        public int OwnerPlayerId { get; set; }
        public byte OwnerPeerId { get; set; }
        public int PartyId { get; set; }
        public ShelteredTravelStateKind State { get; set; }
        public long LastAuthoritativeTick { get; set; }
        public string LastEventId { get; set; }
        public ShelteredTravelStartedEvent StartedEvent { get; set; }
        public ShelteredTravelCorrectedEvent LatestCorrection { get; set; }
        public ShelteredTravelArrivedEvent ArrivalEvent { get; set; }
        public int LastPredictedGridX { get; set; }
        public int LastPredictedGridY { get; set; }

        public ShelteredTravelState Copy()
        {
            return new ShelteredTravelState
            {
                TravelId = TravelId ?? string.Empty,
                OwnerPlayerId = OwnerPlayerId,
                OwnerPeerId = OwnerPeerId,
                PartyId = PartyId,
                State = State,
                LastAuthoritativeTick = LastAuthoritativeTick,
                LastEventId = LastEventId ?? string.Empty,
                StartedEvent = StartedEvent != null ? StartedEvent.Copy() : null,
                LatestCorrection = LatestCorrection != null ? LatestCorrection.Copy() : null,
                ArrivalEvent = ArrivalEvent != null ? ArrivalEvent.Copy() : null,
                LastPredictedGridX = LastPredictedGridX,
                LastPredictedGridY = LastPredictedGridY
            };
        }
    }

    internal sealed class ShelteredTravelApplyResult
    {
        public static readonly ShelteredTravelApplyResult Applied =
            new ShelteredTravelApplyResult(true, string.Empty);

        public static readonly ShelteredTravelApplyResult IgnoredDuplicate =
            new ShelteredTravelApplyResult(false, "duplicate-event-id");

        public static readonly ShelteredTravelApplyResult IgnoredOutOfOrder =
            new ShelteredTravelApplyResult(false, "out-of-order-event");

        public static ShelteredTravelApplyResult Ignored(string reason)
        {
            return new ShelteredTravelApplyResult(false, reason);
        }

        private ShelteredTravelApplyResult(bool applied, string reason)
        {
            AppliedEvent = applied;
            Reason = reason ?? string.Empty;
        }

        public bool AppliedEvent { get; private set; }
        public string Reason { get; private set; }
    }

    internal interface IShelteredTravelStateRegistry
    {
        ShelteredTravelApplyResult ApplyTravelStarted(ShelteredTravelStartedEvent started, string eventId);
        ShelteredTravelApplyResult ApplyTravelCorrected(ShelteredTravelCorrectedEvent corrected, string eventId);
        ShelteredTravelApplyResult ApplyTravelCorrected(ShelteredTravelCorrectedEvent corrected, string eventId, bool force);
        ShelteredTravelApplyResult ApplyTravelArrived(ShelteredTravelArrivedEvent arrived, string eventId);
        ShelteredTravelPredictionResult Predict(string travelId, long worldTick);
        System.Collections.Generic.IList<ShelteredTravelState> GetActive();
        bool Remove(string travelId);
        void Clear(string reason);
    }
}
