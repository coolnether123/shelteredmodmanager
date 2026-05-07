namespace ShelteredAPI.Networking
{
    /// <summary>
    /// Shared event-kind keys for Sheltered gameplay synchronization.
    /// This is intentionally small; patches and mods can add more kind strings as they wire new events.
    /// </summary>
    public static class ShelteredNetworkEventKinds
    {
        public const string ExpeditionStarted = "Expedition.Started";
        public const string ExpeditionRouteChanged = "Expedition.RouteChanged";
        public const string ExpeditionReturned = "Expedition.Returned";
        public const string LocationSearched = "Location.Searched";
        public const string ResourceClaimed = "Resource.Claimed";
    }
}
