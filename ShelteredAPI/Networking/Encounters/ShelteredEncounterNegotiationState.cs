namespace ShelteredAPI.Networking.Encounters
{
    internal sealed class ShelteredEncounterNegotiationState
    {
        public ShelteredEncounterNegotiationState()
        {
            EncounterId = string.Empty;
            InitiatorTravelId = string.Empty;
            ResponderTravelId = string.Empty;
            LastEventId = string.Empty;
            LastEventKind = string.Empty;
            Reason = string.Empty;
        }

        public string EncounterId { get; set; }
        public int InitiatorPlayerId { get; set; }
        public byte InitiatorPeerId { get; set; }
        public string InitiatorTravelId { get; set; }
        public int ResponderPlayerId { get; set; }
        public byte ResponderPeerId { get; set; }
        public string ResponderTravelId { get; set; }
        public ShelteredEncounterActionKind OfferedAction { get; set; }
        public ShelteredEncounterNegotiationStateKind State { get; set; }
        public long LastAuthoritativeTick { get; set; }
        public string LastEventId { get; set; }
        public string LastEventKind { get; set; }
        public string Reason { get; set; }

        public ShelteredEncounterNegotiationState Copy()
        {
            return new ShelteredEncounterNegotiationState
            {
                EncounterId = EncounterId ?? string.Empty,
                InitiatorPlayerId = InitiatorPlayerId,
                InitiatorPeerId = InitiatorPeerId,
                InitiatorTravelId = InitiatorTravelId ?? string.Empty,
                ResponderPlayerId = ResponderPlayerId,
                ResponderPeerId = ResponderPeerId,
                ResponderTravelId = ResponderTravelId ?? string.Empty,
                OfferedAction = OfferedAction,
                State = State,
                LastAuthoritativeTick = LastAuthoritativeTick,
                LastEventId = LastEventId ?? string.Empty,
                LastEventKind = LastEventKind ?? string.Empty,
                Reason = Reason ?? string.Empty
            };
        }
    }

    internal sealed class ShelteredEncounterNegotiationApplyResult
    {
        public static readonly ShelteredEncounterNegotiationApplyResult Applied =
            new ShelteredEncounterNegotiationApplyResult(true, string.Empty);

        public static readonly ShelteredEncounterNegotiationApplyResult IgnoredDuplicate =
            new ShelteredEncounterNegotiationApplyResult(false, "duplicate-event-id");

        public static ShelteredEncounterNegotiationApplyResult Ignored(string reason)
        {
            return new ShelteredEncounterNegotiationApplyResult(false, reason);
        }

        private ShelteredEncounterNegotiationApplyResult(bool applied, string reason)
        {
            AppliedEvent = applied;
            Reason = reason ?? string.Empty;
        }

        public bool AppliedEvent { get; private set; }
        public string Reason { get; private set; }
    }
}
