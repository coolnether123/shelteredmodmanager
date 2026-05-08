namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Shared event-kind keys for Sheltered gameplay synchronization.
    /// This is intentionally small; patches and mods can add more kind strings as they wire new events.
    /// </summary>
    public static class ShelteredNetworkEventKinds
    {
        public const string BunkerRegistered = "Bunker.Registered";
        public const string BunkerMoved = "Bunker.Moved";
        public const string BunkerOnlineStateChanged = "Bunker.OnlineStateChanged";
        public const string WorldClockSample = "World.ClockSample";
        public const string ExpeditionStarted = "Expedition.Started";
        public const string ExpeditionRouteChanged = "Expedition.RouteChanged";
        public const string ExpeditionReturned = "Expedition.Returned";
        public const string LocationSearched = "Location.Searched";
        public const string ResourceClaimed = "Resource.Claimed";
        public const string TradeOfferIntent = "Trade.OfferIntent";
        public const string TradeOfferAccepted = "Trade.OfferAccepted";
        public const string TradeOfferRejected = "Trade.OfferRejected";
        public const string TradeCaravanLaunched = "Trade.CaravanLaunched";
        public const string TradeCaravanArrived = "Trade.CaravanArrived";
        public const string TradeCompleted = "Trade.Completed";
        public const string TradeCancelled = "Trade.Cancelled";
        public const string TravelStarted = "Travel.Started";
        public const string TravelCorrected = "Travel.Corrected";
        public const string TravelArrived = "Travel.Arrived";
    }
}
